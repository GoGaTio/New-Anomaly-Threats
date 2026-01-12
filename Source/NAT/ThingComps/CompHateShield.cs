using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using System.Xml.XPath;
using System.Xml.Xsl;
using DelaunatorSharp;
using Gilzoide.ManagedJobs;
using Ionic.Crc;
using Ionic.Zlib;
using JetBrains.Annotations;
using KTrie;
using LudeonTK;
using NVorbis.NAudioSupport;
using RimWorld;
using RimWorld.BaseGen;
using RimWorld.IO;
using RimWorld.Planet;
using RimWorld.QuestGen;
using RimWorld.SketchGen;
using RimWorld.Utility;
using RuntimeAudioClipLoader;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Grammar;
using Verse.Noise;
using Verse.Profile;
using Verse.Sound;
using Verse.Steam;

namespace NAT
{
	public class CompProperties_HateShield : CompProperties
	{
		public int startingTicksToReset = 3200;

		public float minDrawSize = 2.5f;

		public float maxDrawSize = 3.5f;

		public float energyLossPerDamage = 0.01f;

		public float energyGain = 0.01f;

		public float energyMax = 1f;

		public CompProperties_HateShield()
		{
			compClass = typeof(CompHateShield);
		}
	}
	[StaticConstructorOnStartup]
	public class CompHateShield : ThingComp
	{
		public float energy;

		protected int ticksToReset = -1;

		protected int lastKeepDisplayTick = -9999;

		private Vector3 impactAngleVect;

		private int lastAbsorbDamageTick = -9999;

		private static readonly Material BubbleMat = MaterialPool.MatFrom("Things/Mote/NAT_HateShield", ShaderDatabase.Transparent);

		public CompProperties_HateShield Props => (CompProperties_HateShield)props;

		public ShieldState ShieldState
		{
			get
			{
				if (ticksToReset <= 0)
				{
					return ShieldState.Active;
				}
				return ShieldState.Resetting;
			}
		}

		protected bool ShouldDisplay
		{
			get
			{
				Pawn pawnOwner = PawnOwner;
				if (!pawnOwner.Spawned || pawnOwner.Dead || pawnOwner.Downed)
				{
					return false;
				}
                if (pawnOwner.Drafted)
                {
					return true;
                }
				if(pawnOwner.Faction?.IsPlayer != true)
                {
					return true;
                }
				return false;
			}
		}

        public override float GetStatFactor(StatDef stat)
        {
			if (ShieldState == ShieldState.Active && stat == StatDefOf.Flammability)
			{
				return 0f;
			}
			return 1f;
		}

        protected Pawn PawnOwner
		{
			get
			{
				if (parent is Apparel apparel)
				{
					return apparel.Wearer;
				}
				return null;
			}
		}

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref energy, "energy", 0f);
			Scribe_Values.Look(ref ticksToReset, "ticksToReset", -1);
			Scribe_Values.Look(ref lastKeepDisplayTick, "lastKeepDisplayTick", 0);
		}

		public override IEnumerable<Gizmo> CompGetWornGizmosExtra()
		{
			foreach (Gizmo item in base.CompGetWornGizmosExtra())
			{
				yield return item;
			}
			foreach (Gizmo gizmo in GetGizmos())
			{
				yield return gizmo;
			}
			if (!DebugSettings.ShowDevGizmos)
			{
				yield break;
			}
			Command_Action command_Action = new Command_Action();
			command_Action.defaultLabel = "DEV: Break";
			command_Action.action = Break;
			yield return command_Action;
			if (ticksToReset > 0)
			{
				Command_Action command_Action2 = new Command_Action();
				command_Action2.defaultLabel = "DEV: Clear reset";
				command_Action2.action = delegate
				{
					ticksToReset = 0;
				};
				yield return command_Action2;
			}
		}

		private IEnumerable<Gizmo> GetGizmos()
		{
			if ((PawnOwner.Faction == Faction.OfPlayer || (parent is Pawn pawn && pawn.RaceProps.IsMechanoid)) && Find.Selector.SingleSelectedThing == PawnOwner)
			{
				Gizmo_HateShieldStatus gizmo_ShieldStatus = new Gizmo_HateShieldStatus();
				gizmo_ShieldStatus.shield = this;
				yield return gizmo_ShieldStatus;
			}
		}

		public override void CompTick()
		{
			base.CompTick();
			if (ShieldState == ShieldState.Resetting)
			{
				ticksToReset--;
				if (ticksToReset <= 0)
				{
					Reset();
				}
			}
			else if (ShieldState == ShieldState.Active)
			{
				energy += Props.energyGain;
				if (energy > Props.energyMax)
				{
					energy = Props.energyMax;
				}
			}
		}

		public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
		{
			absorbed = false;
			if (ShieldState != 0 || PawnOwner == null)
			{
				return;
			}
			if (dinfo.Def.isRanged || dinfo.Def.isExplosive || dinfo.Tool != null)
			{
				energy -= dinfo.Amount * Props.energyLossPerDamage;
				if (energy < 0f)
				{
					Break();
				}
				else
				{
					AbsorbedDamage(dinfo);
				}
				absorbed = true;
			}
		}

		public void KeepDisplaying()
		{
			lastKeepDisplayTick = Find.TickManager.TicksGame;
		}

		private void AbsorbedDamage(DamageInfo dinfo)
		{
			SoundDefOf.EnergyShield_AbsorbDamage.PlayOneShot(new TargetInfo(PawnOwner.Position, PawnOwner.Map));
			impactAngleVect = Vector3Utility.HorizontalVectorFromAngle(dinfo.Angle);
			Vector3 loc = PawnOwner.TrueCenter() + impactAngleVect.RotatedBy(180f) * 0.5f;
			float num = Mathf.Min(10f, 2f + dinfo.Amount / 10f);
			FleckMaker.Static(loc, PawnOwner.Map, FleckDefOf.ExplosionFlash, num);
			int num2 = (int)num;
			for (int i = 0; i < num2; i++)
			{
				FleckMaker.ThrowDustPuff(loc, PawnOwner.Map, Rand.Range(0.8f, 1.2f));
			}
			lastAbsorbDamageTick = Find.TickManager.TicksGame;
			KeepDisplaying();
		}

		private void Break()
		{
			if (parent.Spawned)
			{
				float scale = Mathf.Lerp(Props.minDrawSize, Props.maxDrawSize, energy);
				EffecterDefOf.Shield_Break.SpawnAttached(parent, parent.MapHeld, scale);
				FleckMaker.Static(PawnOwner.TrueCenter(), PawnOwner.Map, FleckDefOf.ExplosionFlash, 12f);
				for (int i = 0; i < 6; i++)
				{
					FleckMaker.ThrowDustPuff(PawnOwner.TrueCenter() + Vector3Utility.HorizontalVectorFromAngle(Rand.Range(0, 360)) * Rand.Range(0.3f, 0.6f), PawnOwner.Map, Rand.Range(0.8f, 1.2f));
				}
			}
			energy = 0f;
			ticksToReset = Props.startingTicksToReset;
		}

		private void Reset()
		{
			ticksToReset = -1;
			energy = Props.energyMax;
		}

		private float lastAngle;

		public override void CompDrawWornExtras()
		{
			base.CompDrawWornExtras();
			if (ShieldState == ShieldState.Active && ShouldDisplay)
			{
				float num = Mathf.Lerp(Props.minDrawSize, Props.maxDrawSize, energy);
				Vector3 drawPos = PawnOwner.Drawer.DrawPos;
				drawPos.y = AltitudeLayer.MoteOverhead.AltitudeFor();
				int num2 = Find.TickManager.TicksGame - lastAbsorbDamageTick;
				if (num2 < 8)
				{
					float num3 = (float)(8 - num2) / 8f * 0.05f;
					drawPos += impactAngleVect * num3;
					num -= num3;
				}
				float angle = lastAngle + Rand.Range(-30, 60);
				lastAngle = angle;
				Vector3 s = new Vector3(num, 1f, num);
				Matrix4x4 matrix = default(Matrix4x4);
				matrix.SetTRS(drawPos, Quaternion.AngleAxis(angle, Vector3.up), s);
				Graphics.DrawMesh(MeshPool.plane10, matrix, BubbleMat, 0);
			}
		}
	}
}