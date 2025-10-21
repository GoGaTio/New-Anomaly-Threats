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
	public enum CastLocation
	{
		OnTarget,
		Turret,
		BehindTarget,
		NearCaster
	}

	[Flags]
	public enum CastCondition
	{
		None,
		EnemyNearby,
		OverwhelmingEnemies,
		Attacking
	}

	public class CastParms
	{
		public CastCondition conditions;

		public CastLocation location;

		public float weight = 1f;

		public Ability ability;

		public CastParms()
        {

        }

		public CastParms LinkWithAbility(Ability ability)
        {
			CastParms p = new CastParms();
			p.ability = ability;
			p.conditions = this.conditions;
			p.location = this.location;
			p.weight = this.weight;
			return p;
		}
	}
	public class CompProperties_AbilityRustedCommander : CompProperties_AbilityEffect
	{
		public int cost;

		public List<CastParms> castParms = new List<CastParms>();

		public CompProperties_AbilityRustedCommander()
		{
			compClass = typeof(CompAbilityEffect_RustedCommander);
		}
	}
	public class CompAbilityEffect_RustedCommander : CompAbilityEffect
	{
		public new CompProperties_AbilityRustedCommander Props => (CompProperties_AbilityRustedCommander)props;

		public CompRustedCommander Comp => parent.pawn.GetComp<CompRustedCommander>();

		public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
			Comp.units -= Props.cost;
        }

        public override bool GizmoDisabled(out string reason)
        {
            if (Comp == null || Comp.units < Props.cost)
            {
				reason = "NAT_NoUnits".Translate();
				return true;
            }
            return base.GizmoDisabled(out reason);
        }

        public override bool CanCast => Comp.units >= Props.cost && base.CanCast;

        public IEnumerable<CastParms> CastParms()
        {
			foreach(CastParms p in Props.castParms)
            {
				yield return p.LinkWithAbility(parent);
            }
        }
    }

	public class CompProperties_AbilitySpawnWithFaction : CompProperties_AbilityEffect
	{
		public ThingDef thingDef;

		public bool allowOnBuildings;

		public Color? color = null;

		public CompProperties_AbilitySpawnWithFaction()
		{
			compClass = typeof(CompAbilityEffect_SpawnWithFaction);
		}
	}
	public class CompAbilityEffect_SpawnWithFaction : CompAbilityEffect
	{
		public new CompProperties_AbilitySpawnWithFaction Props => (CompProperties_AbilitySpawnWithFaction)props;

		public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
		{
			base.Apply(target, dest);
			Thing t = GenSpawn.Spawn(Props.thingDef, target.Cell, parent.pawn.Map);
			if (t.def.CanHaveFaction)
			{
				t.SetFaction(parent.pawn.Faction);
			}
			if (t is RustedBoostField r)
			{
				r.rustFaction = parent.pawn.Faction;
				r.rust = parent.pawn;
			}
		}

		public override void DrawEffectPreview(LocalTargetInfo target)
		{
			base.DrawEffectPreview(target);
			if (Props.color != null)
			{
				GenDraw.DrawFieldEdges(CellRect.FromCell(target.Cell).ExpandedBy((Props.thingDef.Size.x - 1) / 2, (Props.thingDef.Size.z - 1) / 2).Cells.ToList(), Props.color.Value);
			}
		}

		public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
		{
			if (target.Cell.Filled(parent.pawn.Map) || (!Props.allowOnBuildings && target.Cell.GetEdifice(parent.pawn.Map) != null))
			{
				if (throwMessages)
				{
					Messages.Message("CannotUseAbility".Translate(parent.def.label) + ": " + "AbilityOccupiedCells".Translate(), target.ToTargetInfo(parent.pawn.Map), MessageTypeDefOf.RejectInput, historical: false);
				}
				return false;
			}
			return true;
		}
	}

	public class CompProperties_AbilityReinforcements : CompProperties_AbilityEffect
	{
		public float missRadius;

		public List<PawnKindDefCount> kindDefs = new List<PawnKindDefCount>();

		public List<ThingDefCountClass> thingDefs = new List<ThingDefCountClass>();

		public CompProperties_AbilityReinforcements()
		{
			compClass = typeof(CompAbilityEffect_Reinforcements);
		}
	}
	public class CompAbilityEffect_Reinforcements : CompAbilityEffect
	{
		public new CompProperties_AbilityReinforcements Props => (CompProperties_AbilityReinforcements)props;

		public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
		{
			Map map = parent.pawn.Map;
			IntVec3 cell = target.Cell;
			IntVec3 dropCell = cell;
			List<Thing> list = new List<Thing>();
			List<Skyfaller_RustedChunk> skyfallers = new List<Skyfaller_RustedChunk>();
			Faction faction = parent.pawn.Faction ?? Faction.OfEntities;
			Lord lord = parent.pawn.GetLord();
			foreach (PawnKindDefCount p in Props.kindDefs)
            {
				ThingDef skyfaller = RustedArmyUtility.GetSkyfaller(p.kindDef.race);
				for (int i = 0; i < p.count; i++)
				{
					Pawn pawn = PawnGenerator.GeneratePawn(p.kindDef, faction);
					if(lord != null)
                    {
						lord.AddPawn(pawn);
                    }
					list.Add(pawn);
					int num = Rand.Range(0, GenRadial.NumCellsInRadius(Props.missRadius));
					dropCell = target.Cell + GenRadial.RadialPattern[num];
					dropCell.ClampInsideMap(map);
					Skyfaller_RustedChunk chunk = SkyfallerMaker.SpawnSkyfaller(skyfaller, pawn, dropCell, map) as Skyfaller_RustedChunk;
					chunk.faction = faction;
					skyfallers.Add(chunk);
				}
			}
			foreach (ThingDefCountClass t in Props.thingDefs)
			{
				ThingDef skyfaller = RustedArmyUtility.GetBaseSkyfaller(t.thingDef);
				for (int i = 0; i < t.count; i++)
				{
					Thing thing = ThingMaker.MakeThing(t.thingDef);
					thing.SetFaction(faction);
					list.Add(thing);
					int num = Rand.Range(0, GenRadial.NumCellsInRadius(Props.missRadius));
					dropCell = target.Cell + GenRadial.RadialPattern[num];
					dropCell.ClampInsideMap(map);
					Skyfaller_RustedChunk chunk = SkyfallerMaker.SpawnSkyfaller(skyfaller, thing, dropCell, map) as Skyfaller_RustedChunk;
					chunk.faction = faction;
					skyfallers.Add(chunk);
				}
			}
			foreach(Skyfaller_RustedChunk unit in skyfallers)
            {
				unit.frendlies = list;
			}
			Messages.Message("NAT_DropRequested".Translate(parent.pawn.LabelCap), list, MessageTypeDefOf.ThreatBig);
			base.Apply(target, dest);
		}

		public override void DrawEffectPreview(LocalTargetInfo target)
		{
			GenDraw.DrawRadiusRing(target.Cell, Props.missRadius);
		}

		public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
		{
			if (!target.Cell.IsValid)
			{
				return false;
			}
			if (!target.Cell.Standable(parent.pawn.Map))
			{
				return false;
			}
			if (target.Cell.Filled(parent.pawn.Map))
			{
				return false;
			}
			return base.Valid(target, throwMessages);
		}

		public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
		{
			if (!target.Cell.IsValid)
			{
				return false;
			}
			if (!target.Cell.Standable(parent.pawn.Map))
			{
				return false;
			}
			if (target.Cell.Filled(parent.pawn.Map))
			{
				return false;
			}
			return base.CanApplyOn(target, dest);
		}
	}
}