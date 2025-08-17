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
using HarmonyLib;

namespace NAT
{

	public class CompProperties_AbilityBile : CompProperties_AbilityEffect
	{
		public CompProperties_AbilityBile()
		{
			compClass = typeof(CompAbilityEffect_Bile);
		}
	}

	public class CompAbilityEffect_Bile : CompAbilityEffect
	{
		public new CompProperties_AbilityBile Props => (CompProperties_AbilityBile)props;

        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            return target.Pawn != null && target.Pawn.RaceProps.IsFlesh && !target.Pawn.IsEntity;
        }
    }

	public class CompProperties_AbilityShapeRust : CompProperties_AbilityEffect
	{
		public int bioferriteCount;

		public float connectRadius;

		public List<PawnKindDefCount> kinds = new List<PawnKindDefCount>();

		public CompProperties_AbilityShapeRust()
		{
			compClass = typeof(CompAbilityEffect_ShapeRust);
		}
	}
	public class CompAbilityEffect_ShapeRust : CompAbilityEffect
	{
		public static Color DustColor = new Color(0.55f, 0.55f, 0.55f, 3f);

		private List<Thing> foundChunksTemp;

		private int lastChunkUpdateFrame;

		public new CompProperties_AbilityShapeRust Props => (CompProperties_AbilityShapeRust)props;


		public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
		{
			base.Apply(target, dest);
			Map map = parent.pawn.Map;
			List<Thing> list = FindClosestBioferrite(target).ToList();
			if(list.Sum((Thing x) => x.stackCount) >= Props.bioferriteCount)
            {
				PsychicRitualToil_ActivateRust.RemoveItem(list, Props.bioferriteCount);
				GenSpawn.Spawn(PsychicRitualToil_ActivateRust.CreateRust(Props.kinds.RandomElementByWeight((PawnKindDefCount x) => x.count).kindDef, parent.pawn.Faction), target.Cell, map);
				EffecterDefOf.PsychicRitual_Complete.SpawnMaintained(target.Cell, map);
			}
            else
            {
				parent.ResetCooldown();
            }
		}

		public override IEnumerable<PreCastAction> GetPreCastActions()
		{
			yield return new PreCastAction
			{
				action = delegate (LocalTargetInfo t, LocalTargetInfo d)
				{
					foreach (Thing item in FindClosestBioferrite(t))
					{
						FleckMaker.Static(item.TrueCenter(), parent.pawn.Map, FleckDefOf.PsycastSkipFlashEntry, 0.72f);
					}
				},
				ticksAwayFromCast = 5
			};
		}

		private IEnumerable<Thing> FindClosestBioferrite(LocalTargetInfo target)
		{
			if (lastChunkUpdateFrame == Time.frameCount && foundChunksTemp != null)
			{
				return foundChunksTemp;
			}
			if (foundChunksTemp == null)
			{
				foundChunksTemp = new List<Thing>();
			}
			foundChunksTemp.Clear();
			IntVec3 cell = target.Cell;
			if (!cell.IsValid || !cell.InBounds(parent.pawn.Map))
            {
				return foundChunksTemp;
			}
			int range = Mathf.CeilToInt(Props.connectRadius / 2) + 1;
			CellRect rect = new CellRect(cell.x - range, cell.z - range, range * 2, range * 2).ClipInsideMap(parent.pawn.Map);
			foreach(IntVec3 c in rect.Cells)
            {
				Thing t = c.GetFirstThing(parent.pawn.Map, ThingDefOf.Bioferrite);
				if(t != null && c.DistanceTo(cell) < Props.connectRadius)
                {
					foundChunksTemp.Add(t);
				}
			}
			lastChunkUpdateFrame = Time.frameCount;
			return foundChunksTemp;
		}

		public override void DrawEffectPreview(LocalTargetInfo target)
		{
			foreach (Thing item in FindClosestBioferrite(target))
			{
				GenDraw.DrawLineBetween(item.TrueCenter(), target.CenterVector3);
				GenDraw.DrawTargetHighlight(item);
			}
			GenDraw.DrawRadiusRing(target.Cell, Props.connectRadius);
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
			if (FindClosestBioferrite(target).Sum((Thing t)=>t.stackCount) < Props.bioferriteCount)
			{
				if (throwMessages)
				{
					Messages.Message("CannotUseAbility".Translate(parent.def.label) + ": " + "AbilityNotEnoughFreeSpace".Translate(), parent.pawn, MessageTypeDefOf.RejectInput, historical: false);
				}
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
			if (FindClosestBioferrite(target).Sum((Thing t) => t.stackCount) < Props.bioferriteCount)
			{
				return false;
			}
			return base.CanApplyOn(target, dest);
		}

		public override string ExtraLabelMouseAttachment(LocalTargetInfo target)
		{

			int count = FindClosestBioferrite(target).Sum((Thing t) => t.stackCount);
			if (target.IsValid && count < Props.bioferriteCount)
			{
				return "AbilityNoChunkToSkip".Translate() + "(" + count.ToString() + "/" + Props.bioferriteCount.ToString() + ")";
			}
			return base.ExtraLabelMouseAttachment(target);
		}
	}

	public class CompProperties_AbilityCollectorHowl : CompProperties_AbilityEffect
	{
		public SimpleCurve sightstealersPointsFromPointsCurve = new SimpleCurve();

		public CompProperties_AbilityCollectorHowl()
		{
			compClass = typeof(CompAbilityEffect_CollectorHowl);
		}
	}
	public class CompAbilityEffect_CollectorHowl : CompAbilityEffect
	{
		public new CompProperties_AbilityCollectorHowl Props => (CompProperties_AbilityCollectorHowl)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
			Map map = parent.pawn.Map;
			PawnGroupMakerParms pawnGroupMakerParms = new PawnGroupMakerParms
			{
				groupKind = PawnGroupKindDefOf.Sightstealers,
				tile = map.Tile,
				faction = Faction.OfEntities,
				points = Props.sightstealersPointsFromPointsCurve.Evaluate(StorytellerUtility.DefaultThreatPointsNow(map))
			};
			pawnGroupMakerParms.points = Mathf.Max(pawnGroupMakerParms.points, Faction.OfEntities.def.MinPointsToGeneratePawnGroup(pawnGroupMakerParms.groupKind) * 1.05f);
			List<Pawn> list = PawnGroupMakerUtility.GeneratePawns(pawnGroupMakerParms).ToList();
			List<IntVec3> cells = parent.pawn.OccupiedRect().ExpandedBy(15).ClipInsideMap(map).Cells.ToList();
			List<IntVec3> spawnCells = new List<IntVec3>();
			foreach (Pawn p in list)
			{
				if(cells.TryRandomElement((IntVec3 c) => c.Standable(map) && c.Walkable(map) && GenSight.LineOfSight(parent.pawn.Position, c, map), out var result))
                {
					spawnCells.Add(result);
				}
			}
			SpawnRequest spawnRequest = new SpawnRequest(list.Cast<Thing>().ToList(), spawnCells, 1, 1f);
			spawnRequest.spawnSound = SoundDefOf.Pawn_Sightstealer_Howl;
			spawnRequest.preSpawnEffecterOffsetTicks = -40;
			spawnRequest.initialDelay = 180;
			spawnRequest.lord = LordMaker.MakeNewLord(Faction.OfEntities, new LordJob_SightstealerAssault(), map);
			Find.CurrentMap.deferredSpawner.AddRequest(spawnRequest);
		}
	}
}