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
	public class CompProperties_RustedShield : CompProperties
	{
		public int maxHealth;

		public int regenInterval;

		public int ticksToRestore;

		public SoundDef destroyedSound;

		public EffecterDef destroyedEffect;

		public PawnRenderNodeProperties renderProps;

		public List<StatModifier> statFactorsInactive = new List<StatModifier>();

		public List<StatModifier> statFactors = new List<StatModifier>();

		public FloatRange effectorOffsetRange = new FloatRange(-0.4f, 0.4f);

		public bool combatExtendedArmor = false;

		public CompProperties_RustedShield()
		{
			compClass = typeof(CompRustedShield);
		}
	}
	public class CompRustedShield : ThingComp
	{
		public int health = -1;

		public int ticksToRegen = -1;

		public int ticksSinceDestroyed = -1;

		public bool destroyed = false;
		public CompProperties_RustedShield Props => (CompProperties_RustedShield)props;

		public RustedPawn Owner => parent as RustedPawn;

		public override void PostPostMake()
		{
			base.PostPostMake();
			if (health == -1 && !destroyed)
			{
				health = Props.maxHealth;
			}
		}

		public override void CompTick()
		{
			base.CompTick();
			if (Owner == null)
			{
				return;
			}
			if (destroyed)
			{
				ticksSinceDestroyed++;
				if (Props.ticksToRestore <= ticksSinceDestroyed)
				{
					ticksSinceDestroyed = -1;
					health = Props.maxHealth;
					destroyed = false;
					Owner.Drawer.renderer.SetAllGraphicsDirty();
				}
			}
			else if (Props.maxHealth > health)
			{
				ticksToRegen++;
				if (ticksToRegen >= Props.regenInterval)
				{
					ticksToRegen = 0;
					health++;
				}
			}
		}

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			foreach (Gizmo g in base.CompGetGizmosExtra())
			{
				yield return g;
			}
			if (Find.Selector.SingleSelectedThing == parent)
			{
				yield return new RustedShieldGizmo(this);
			}
		}

		private int lastDamageCheckTick = -99999;

		public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
		{
			absorbed = false;
			if (destroyed)
			{
				return;
			}
			if(Owner.jobs != null)
			{
				Job job = Owner.CurJob;
				if(job != null && dinfo.Def.ExternalViolenceFor(Owner) && dinfo.Def.canInterruptJobs && !job.playerForced && Find.TickManager.TicksGame >= lastDamageCheckTick + 180)
				{
					Thing instigator = dinfo.Instigator;
					if (job.def.checkOverrideOnDamage == CheckJobOverrideOnDamageMode.Always || (job.def.checkOverrideOnDamage == CheckJobOverrideOnDamageMode.OnlyIfInstigatorNotJobTarget && !job.AnyTargetIs(instigator)))
					{
						lastDamageCheckTick = Find.TickManager.TicksGame;
						Owner.jobs?.CheckForJobOverride();
					}
				}
			}
			if (dinfo.Def.armorCategory != null)
			{
				StatDef armorRatingStat = dinfo.Def.armorCategory.armorRatingStat;
				float armorPenetration = dinfo.ArmorPenetrationInt;
				float armorRating = parent.GetStatValue(armorRatingStat);
				if (Props.combatExtendedArmor)
				{
					if (armorPenetration < armorRating)
					{
						dinfo.SetAmount(GenMath.RoundRandom(dinfo.Amount / 2f));
					}
				}
				else
				{
					float num = Mathf.Max(armorRating - armorPenetration, 0f);
					float value = Rand.Value;
					float num2 = num * 0.5f;
					float num3 = num;
					if (value < num2)
					{
						absorbed = true;
					}
					else if (value < num3)
					{
						dinfo.SetAmount(GenMath.RoundRandom(dinfo.Amount / 2f));
					}
				}
				if (absorbed)
				{
					EffecterDef effecterDef = (dinfo.Def == DamageDefOf.Bullet) ? EffecterDefOf.Deflect_Metal_Bullet : EffecterDefOf.Deflect_Metal;
					effecterDef.Spawn(parent.OccupiedRect().RandomCell, parent.Map, new Vector3(Props.effectorOffsetRange.RandomInRange, 0, Props.effectorOffsetRange.RandomInRange));
					return;
				}
			}
			if (dinfo.Def != DamageDefOf.EMP && dinfo.Def.harmsHealth)
			{
				absorbed = true;
				health -= Mathf.RoundToInt(dinfo.Amount);
				if (health <= 0)
				{
					Destroy();
				}
				if (dinfo.Def.makesBlood && Rand.Chance(0.5f))
				{
					Owner.health.DropBloodFilth();
				}
			}
		}

		public void Destroy(bool doEffect = true)
        {
			health = 0;
			ticksSinceDestroyed = 0;
			destroyed = true;
			Owner.Drawer.renderer.SetAllGraphicsDirty();
			if (doEffect && Owner.SpawnedOrAnyParentSpawned)
			{
				Props.destroyedSound?.PlayOneShot(Owner);
				Props.destroyedEffect.Spawn(Owner.PositionHeld, Owner.MapHeld);
			}
		}

		public override float GetStatFactor(StatDef stat)
		{
			float num = 1f;
			if (destroyed)
			{
				if (Props.statFactorsInactive != null)
				{
					num *= Props.statFactorsInactive.GetStatFactorFromList(stat);
				}
			}
			else
			{
				if (Props.statFactors != null)
				{
					num *= Props.statFactors.GetStatFactorFromList(stat);
				}
			}
			return num;
		}

		public override List<PawnRenderNode> CompRenderNodes()
		{
			List<PawnRenderNode> list = new List<PawnRenderNode>();
			if (!destroyed && Owner != null)
			{
				PawnRenderNodeProperties pawnRenderNodeProperties = Props.renderProps;
				PawnRenderNode pawnRenderNode = (PawnRenderNode)Activator.CreateInstance(Props.renderProps.nodeClass, Owner, pawnRenderNodeProperties, Owner.Drawer.renderer.renderTree);
				list.Add(pawnRenderNode);
			}
			return list;
		}

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref destroyed, "destroyed", false);
			Scribe_Values.Look(ref health, "health", -1);
			Scribe_Values.Look(ref ticksToRegen, "ticksToRegen", -1);
			Scribe_Values.Look(ref ticksSinceDestroyed, "ticksSinceDestroyed", -1);
		}
	}
}