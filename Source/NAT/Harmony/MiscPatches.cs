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
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
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
	[HarmonyPatch(typeof(Pawn), nameof(Pawn.IsDuplicate), MethodType.Getter)]
	public class Patch_Subdued
	{
		[HarmonyPostfix]
		public static void Postfix(ref bool __result, Pawn __instance)
		{
			if (__result) return;
            if (__instance.health.hediffSet.HasHediff<Hediff_Subdued>())
            {
				__result = true;
			}
		}
	}
	[HarmonyPatch(typeof(Pawn), nameof(Pawn.CanAttackWhenPathingBlocked), MethodType.Getter)]
	public class Patch_CanAttackWhenPathingBlocked
	{
		[HarmonyPostfix]
		public static void Postfix(ref bool __result, Pawn __instance)
		{
			if (__instance.kindDef == NATDefOf.NAT_Collector)
			{
				__result = false;
			}
		}
	}

	[HarmonyPatch(typeof(QuestPart_EntityArrival), "Notify_QuestSignalReceived")]
	public class Patch_EntityArrivalOverride
	{
		[HarmonyPrefix]
		public static bool Prefix(Map ___map, Signal signal, string ___inSignal)
		{
			if (!signal.tag.StartsWith(___inSignal))
			{
				return true;
			}
			VoidAwakeningUtility.DecodeWaveType(signal.tag, out var waveType, out var pointsFactor);
			if (waveType != VoidAwakeningUtility.WaveType.Twisted || Rand.Chance(0.65f))
			{
				return true;
			}
			float points = StorytellerUtility.DefaultThreatPointsNow(___map) * pointsFactor;
			string label = "VoidAwakeningEntityArrivalLabel".Translate();
			string desc = "VoidAwakeningEntityArrivalText".Translate();
			LookTargets lookTargets = null;
            if (Rand.Chance(0.4f))
            {
				lookTargets = FireSightstealerCollectorAssault(points, ___map);
			}
			else if (Rand.Chance(0.7f))
            {
				lookTargets = FireSerpentDevourerAssault(points, ___map);
			}
            else
            {
				lookTargets = FireSerpentAssault(points, ___map);
			}
			if (!label.NullOrEmpty())
			{
				Find.LetterStack.ReceiveLetter(label, desc, LetterDefOf.ThreatBig, lookTargets);
			}
			return false;
		}

		private static List<Pawn> FireSightstealerCollectorAssault(float points, Map map)
		{
			float points2 = Mathf.Max(points, 1500f);
			IncidentParms incidentParms = new IncidentParms
			{
				target = map,
				raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn,
				sendLetter = false,
				faction = Faction.OfEntities
			};
			if (!incidentParms.raidArrivalMode.Worker.TryResolveRaidSpawnCenter(incidentParms))
			{
				return null;
			}
			List<Pawn> list = PawnGroupMakerUtility.GeneratePawns(new PawnGroupMakerParms
			{
				groupKind = NATDefOf.NAT_SightstealersCollector,
				points = points2,
				faction = Faction.OfEntities
			}).ToList();
			PawnsArrivalModeDefOf.EdgeWalkInDistributedGroups.Worker.Arrive(list, incidentParms);
			LordMaker.MakeNewLord(Faction.OfEntities, new LordJob_AssaultColony(incidentParms.faction, canKidnap: false, canTimeoutOrFlee: false, sappers: false, useAvoidGridSmart: false, canSteal: false), map, list);
			return list.ToList();
		}

		private static List<Pawn> FireSerpentAssault(float points, Map map)
		{
			float points2 = Mathf.Max(points, Faction.OfEntities.def.MinPointsToGeneratePawnGroup(NATDefOf.NAT_Serpents) * 1.05f);
			IncidentParms incidentParms = new IncidentParms
			{
				target = map,
				raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn,
				sendLetter = false,
				faction = Faction.OfEntities
			};
			if (!incidentParms.raidArrivalMode.Worker.TryResolveRaidSpawnCenter(incidentParms))
			{
				return null;
			}
			List<Pawn> list = PawnGroupMakerUtility.GeneratePawns(new PawnGroupMakerParms
			{
				groupKind = NATDefOf.NAT_Serpents,
				points = points2,
				faction = Faction.OfEntities
			}).ToList();
			PawnsArrivalModeDefOf.EdgeWalkInDistributedGroups.Worker.Arrive(list, incidentParms);
			LordMaker.MakeNewLord(Faction.OfEntities, new LordJob_AssaultColony(incidentParms.faction, canKidnap: false, canTimeoutOrFlee: false, sappers: false, useAvoidGridSmart: false, canSteal: false), map, list);
			return list.ToList();
		}

		private static List<Pawn> FireSerpentDevourerAssault(float points, Map map)
		{
			float points2 = Mathf.Max(points / 2f, Faction.OfEntities.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Devourers) * 1.05f);
			float points3 = Mathf.Max(points / 2f, Faction.OfEntities.def.MinPointsToGeneratePawnGroup(NATDefOf.NAT_Serpents) * 1.05f);
			IncidentParms incidentParms = new IncidentParms
			{
				target = map,
				raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn,
				sendLetter = false,
				faction = Faction.OfEntities
			};
			if (!incidentParms.raidArrivalMode.Worker.TryResolveRaidSpawnCenter(incidentParms))
			{
				return null;
			}
			List<Pawn> list = PawnGroupMakerUtility.GeneratePawns(new PawnGroupMakerParms
			{
				groupKind = PawnGroupKindDefOf.Devourers,
				points = points2,
				faction = Faction.OfEntities
			}).ToList();
			PawnsArrivalModeDefOf.EdgeWalkInDistributedGroups.Worker.Arrive(list, incidentParms);
			LordMaker.MakeNewLord(Faction.OfEntities, new LordJob_DevourerAssault(), map, list);
			List<Pawn> list2 = PawnGroupMakerUtility.GeneratePawns(new PawnGroupMakerParms
			{
				groupKind = NATDefOf.NAT_Serpents,
				points = points3,
				faction = Faction.OfEntities
			}).ToList();
			PawnsArrivalModeDefOf.EdgeWalkInDistributedGroups.Worker.Arrive(list2, incidentParms);
			LordMaker.MakeNewLord(Faction.OfEntities, new LordJob_SerpentAssault(), map, list2);
			return list.Concat(list2).ToList();
		}
	}

	[HarmonyPatch(typeof(QuestNode_Root_VoidAwakening), "SpawnStructures")]
	public class Patch_SpawnRustedDefenders
	{
		private static readonly SimpleCurve DefenderPointsByCombatPoints = new SimpleCurve
	{
		new CurvePoint(0f, 1000f),
		new CurvePoint(1000f, 2500f),
		new CurvePoint(10000f, 5000f)
	};

		[HarmonyPostfix]
		public static void Postfix(List<CellRect> structureRects, int numStructures, ThingDef structureDef, List<Thing> tmpStageStructures, string structureActivatedSignal)
		{
			if (!LoadedModManager.GetMod<NewAnomalyThreatsMod>().GetSettings<NewAnomalyThreatsSettings>().allowEndGameRaid)
			{
				return;
			}
			Quest quest = QuestGen.quest;
			Map map = QuestGen.slate.Get<Map>("map");
			var parms = new IncidentParms();
			parms.target = map;
			parms.points = DefenderPointsByCombatPoints.Evaluate(StorytellerUtility.DefaultThreatPointsNow(map));
			IncidentWorker_RustedArmySiege worker = NATDefOf.NAT_RustedArmySiege.Worker as IncidentWorker_RustedArmySiege;
			worker.letterDescExtra = "NAT_".Translate();
			worker.TryExecute(parms);
			/*PawnGroupMakerParms pawnGroupMakerParms = new PawnGroupMakerParms
			{
				groupKind = parms.pawnGroupKind ?? DefDatabase<PawnGroupKindDef>.GetNamed("NAT_RustedArmy_Defence"),
				tile = map.Tile,
				faction = Faction.OfEntities,
				points = num
			};
			List<Pawn> list = PawnGroupMakerUtility.GeneratePawns(pawnGroupMakerParms).ToList();
			PawnsArrivalModeDefOf.EdgeWalkIn.Worker.TryResolveRaidSpawnCenter(parms);
			parms.attackTargets = new List<Thing> { ___structure };
			PawnsArrivalModeDefOf.EdgeWalkIn.Worker.Arrive(list, parms);
			if (AnomalyIncidentUtility.IncidentShardChance(num))
			{
				AnomalyIncidentUtility.PawnShardOnDeath(list.RandomElement());
			}
			LordMaker.MakeNewLord(Faction.OfEntities, new LordJob_DefendVoidStructure(___structure, new IntRange(80000, 140000).RandomInRange), map, list);
			Find.LetterStack.ReceiveLetter("NAT_RustedArmyRaid".Translate(), "NAT_RustedArmyRaid_VoidDefence_Desc".Translate(), LetterDefOf.ThreatBig, list, null, null, null, null, new IntRange(0, 120).RandomInRange);
			*/
		}
	}

	[HarmonyPatch(typeof(PsychicRitualToil_GatherForInvocation), "InvokerGatherPhaseToils")]
	public static class Patch_InvokerGatherPhaseToils
	{
		[HarmonyPostfix]
		public static IEnumerable<PsychicRitualToil> Postfix(IEnumerable<PsychicRitualToil> __result, PsychicRitualDef_InvocationCircle def)
		{
			if(def is PsychicRitualDef_CreateRust ritual)
            {
				yield return new PsychicRitualToil_CreateRust(def.InvokerRole, ritual);
			}
			foreach (PsychicRitualToil toil in __result)
			{
				yield return toil;
			}
		}
	}

	[HarmonyPatch(typeof(CompDisruptorFlare), nameof(CompDisruptorFlare.PostSpawnSetup))]
	public class Patch_CompDisruptorFlare
	{
		[HarmonyPostfix]
		public static void Postfix(bool respawningAfterLoad, CompDisruptorFlare __instance)
		{
			if (respawningAfterLoad)
			{
				return;
			}
			IntVec3 pos = __instance.parent.Position;
			float num = __instance.Props.radius;
			List<Thing> list = new List<Thing>();
			foreach (IntVec3 cell in CellRect.FromCell(__instance.parent.Position).ExpandedBy(Mathf.CeilToInt(num)).ClipInsideMap(__instance.parent.Map))
            {
				Building b = cell.GetFirstBuilding(__instance.parent.Map);
				if (b != null && !list.Contains(b) && cell.DistanceTo(pos) <= num && (b is Building_RustedTurret || b.HasComp<CompSquareDetector>()) && b.TryGetComp<CompStunnable>(out var comp))
                {
					comp.StunHandler?.StunFor(180, null);
					list.Add(b);
                }
            }
		}
	}

	[HarmonyPatch(typeof(BackCompatibility), nameof(BackCompatibility.BackCompatibleDefName))]
	public class Patch_BackCompatibility
	{
		[HarmonyPrefix]
		public static bool Prefix(Type defType, string defName, ref string __result)
		{
			string newDefName = BackCompatibleDefName(defType, defName);
			if (newDefName != null)
			{
				__result = newDefName;
				return false;
			}
			return true;
		}

		public static string BackCompatibleDefName(Type defType, string defName)
		{
			if (defType == typeof(ThingDef))
			{
				switch (defName)
				{
					case "NAT_EliteRustedSoldier":
						return "NAT_RustedSoldier";
					case "NAT_RustedFieldMarshal":
						return "NAT_RustedOfficer";
					case "NAT_RustedSphere":
						return "NAT_RustedMass";
					case "NAT_Collector_Reworked":
						return "NAT_Collector";
					case "NAT_RustedSculpture_Rifleman":
					case "NAT_RustedSculpture_Gunner":
					case "NAT_RustedSculpture_Grenadier":
					case "NAT_RustedSculpture_Hussar":
						return "NAT_RustedSculpture_Soldier";
					case "NAT_Gun_RustedHeatspiker":
						return "NAT_Gun_HeatspikeRifle";
					case "NAT_Gun_RustedFleshmelter":
						return "NAT_Gun_Fleshmelter";
				}
			}
			if (defType == typeof(PawnKindDef))
			{
				switch (defName)
				{
					case "NAT_EliteRustedSoldier":
						return "NAT_RustedSoldier";
					case "NAT_RustedFieldMarshal":
						return "NAT_RustedOfficer";
					case "NAT_RustedSphere":
						return "NAT_RustedMass";
					case "NAT_Collector_Reworked":
						return "NAT_Collector";
				}
			}
			if (defType == typeof(JobDef))
			{
				switch (defName)
				{
					case "NAT_CollectorWait_Reworked":
						return "NAT_CollectorWait";
					case "NAT_CollectorSteal_Reworked":
						return "NAT_CollectorSteal";
					case "NAT_RustedSphere":
						return "NAT_RustedMass";
					case "NAT_Collector_Reworked":
						return "NAT_Collector";
				}
			}
			return null;
		}
	}
}