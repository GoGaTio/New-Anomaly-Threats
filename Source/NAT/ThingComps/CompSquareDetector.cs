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
	public class CompProperties_SquareDetector : CompProperties_Glower
    {
		public FleckDef fleck;

		public Vector3 offset;

		public int emissionInterval = -1;

		public SoundDef soundOnEmission;

		public int range;

		public EffecterDef spawnEffecter;

		public float activateOnDamagedChance = 0f;

		public SoundDef spawnSound;

		public float glowRadiusInactive;


        public CompProperties_SquareDetector()
		{
			//compClass = typeof(CompSquareDetector);
		}
	}

	public abstract class CompSquareDetector : CompGlower
    {
		public int ticksSinceLastEmitted;

		public new CompProperties_SquareDetector Props => (CompProperties_SquareDetector)props;

		public virtual bool Active => activeInt && parent.Spawned;

		protected bool activeInt = true;

		protected CompStunnable stunner;

        public override float GlowRadius { get => Active ? base.GlowRadius : Props.glowRadiusInactive; set => base.GlowRadius = value; }

		public override void CompTick()
		{
            if (!Active)
            {
				return;
            }
			if (Props.emissionInterval != -1)
			{
				if (ticksSinceLastEmitted >= Props.emissionInterval)
				{
					Emit();
					ticksSinceLastEmitted = 0;
				}
				else
				{
					ticksSinceLastEmitted++;
				}
			}
			if (parent.IsHashIntervalTick(10) && stunner?.StunHandler?.Stunned != true)
			{
				foreach (IntVec3 cell in parent.OccupiedRect().ExpandedBy(Props.range))
				{
					List<Thing> list = parent.Map.thingGrid.ThingsListAt(cell).Where((x) => x is Pawn).ToList();
					for (int i = 0; i < list.Count; i++)
					{
						Thing thing = list[i];
						if (thing.Faction.HostileTo(parent.Faction))
						{
							Activate();
							return;
						}
					}
				}
			}
		}

		public override void PostDrawExtraSelectionOverlays()
		{
			if (!Find.Selector.IsSelected(parent))
			{
				return;
			}
			CellRect rect = parent.OccupiedRect().ExpandedBy(Props.range);
			GenDraw.DrawRadiusRing(parent.Position, Props.range * 2, Color.red, (IntVec3 cell)=> rect.Contains(cell));
		}

		public virtual void Activate()
        {
			if (Props.spawnEffecter != null)
			{
				Effecter effecter = new Effecter(Props.spawnEffecter);
				effecter.Trigger(parent, TargetInfo.Invalid);
				effecter.Cleanup();
			}
			if (Props.spawnSound != null)
			{
				Props.spawnSound.PlayOneShot(parent);
			}
			activeInt = false;
            UpdateLit(parent.Map);
            if (Glows)
            {
                ForceRegister(parent.Map);
            }
        }

        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
			if (dinfo.Def.defName == "Nerve")
            {
				DamageInfo dinfo2 = dinfo;
				dinfo2.Def = DamageDefOf.NerveStun;
				dinfo2.SetAmount(4f);
				parent.TakeDamage(dinfo2);
			}
            if (Active && !dinfo.Def.causeStun && dinfo.Def.harmsHealth && stunner?.StunHandler?.Stunned != true && Rand.Chance(Props.activateOnDamagedChance))
            {
				Activate();
            }
			absorbed = false;
		}

        protected void Emit()
		{
			if(Props.fleck == null)
            {
				return;
            }
			FleckMaker.Static(parent.DrawPos + Props.offset, parent.MapHeld, Props.fleck);
			if (!Props.soundOnEmission.NullOrUndefined())
			{
				Props.soundOnEmission.PlayOneShot(SoundInfo.InMap(parent));
			}
		}

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
			stunner = parent.GetComp<CompStunnable>();
			base.PostSpawnSetup(respawningAfterLoad);
        }

        public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref ticksSinceLastEmitted, "ticksSinceLastEmitted", 0);
			Scribe_Values.Look(ref activeInt, "activeInt", defaultValue: true);
		}
	}
}