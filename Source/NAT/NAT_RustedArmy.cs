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
using static NAT.IncidentWorker_RustedArmySiege;

namespace NAT
{
	[StaticConstructorOnStartup]
	public static class RustedArmyUtility
    {

		public static readonly Texture2D Use = ContentFinder<Texture2D>.Get("UI/Buttons/NAT_RustUse");
		public static NewAnomalyThreatsSettings Settings
		{
			get
			{
				if (settings == null)
				{
					settings = LoadedModManager.GetMod<NewAnomalyThreatsMod>().GetSettings<NewAnomalyThreatsSettings>();
				}
				return settings;
			}
		}

		private static NewAnomalyThreatsSettings settings;

		public static List<Pawn> ExecuteRaid(Map map, float points, int groupCount = 1, bool stageThenAttack = false, bool sendLetter = true, string extraDescString = null, PawnGroupKindDef groupKindOverride = null, bool forceDrop = false, bool randomDrop = false, bool centerDrop = false)
        {
			var parms = new IncidentParms();
			parms.target = map;
			float radius = 8;
			if (forceDrop || !RCellFinder.TryFindRandomPawnEntryCell(out var _, map, CellFinder.EdgeRoadChance_Hostile))
			{
				groupCount = 1;
				stageThenAttack = false;
				if (!forceDrop)
				{
					if (Rand.Chance(0.2f))
					{
						randomDrop = true;
					}
					else if (Rand.Chance(0.05f))
					{
						centerDrop = true;
						radius = 30;
					}
				}
				forceDrop = true;
			}
			PawnGroupMakerParms pawnGroupMakerParms = new PawnGroupMakerParms
			{
				groupKind = groupKindOverride ?? NATDefOf.NAT_RustedArmy,
				tile = map.Tile,
				faction = Faction.OfEntities,
				points = points/groupCount
			};
			List<Pawn> raiders = new List<Pawn>();
			for (int i = 0; i < groupCount; i++)
			{
				IntVec3 spot = new IntVec3();
				parms.spawnRotation = Rot4.Random;
				List<Pawn> list = PawnGroupMakerUtility.GeneratePawns(pawnGroupMakerParms).ToList();
				list.Add(PawnGenerator.GeneratePawn(NATDefOf.NAT_RustedBannerman, Faction.OfEntities));
				if (forceDrop || !RCellFinder.TryFindRandomPawnEntryCell(out spot, map, CellFinder.EdgeRoadChance_Hostile))
				{
					if (centerDrop)
					{
						spot = DropCellFinder.TradeDropSpot(map);
						radius = 30f;
					}
					else spot = DropCellFinder.FindRaidDropCenterDistant(map);
				}
				for (int j = 0; j < list.Count; j++)
				{
					IntVec3 loc;
					if (randomDrop)
					{
						if (!CellFinder.TryFindRandomCell(map, (IntVec3 x) => DropCellFinder.IsGoodDropSpot(x, map, false, true), out loc))
						{
							Log.Error("New Anomaly Threats - Cannot find dropspot. Continue");
							continue;
						}
					}
					else if (!CellFinder.TryFindRandomReachableNearbyCell(spot, map, radius, TraverseParms.For(forceDrop ? TraverseMode.PassAllDestroyableThingsNotWater : TraverseMode.NoPassClosedDoors), (IntVec3 c) => forceDrop ? DropCellFinder.IsGoodDropSpot(c, map, false, true) : c.Standable(map), null, out loc))
					{
						loc = spot;
					}
					if (forceDrop)
					{
						Skyfaller_RustedChunk skyfaller = (Skyfaller_RustedChunk)SkyfallerMaker.SpawnSkyfaller(RustedArmyUtility.GetSkyfaller(list[j].def), list[j], loc, map);
						skyfaller.frendlies = list.Select((x)=>x as Thing).ToList();
						skyfaller.faction = Faction.OfEntities;
					}
					else GenSpawn.Spawn(list[j], loc, map, parms.spawnRotation);
				}
				if (AnomalyIncidentUtility.IncidentShardChance(points / (groupCount * 1.5f)))
				{
					AnomalyIncidentUtility.PawnShardOnDeath(list.RandomElement());
				}
				raiders.AddRange(list);
				LordMaker.MakeNewLord(Faction.OfEntities, new LordJob_RustedArmy(RCellFinder.FindSiegePositionFrom(list[0].PositionHeld, map), stageThenAttack ? Rand.Range(10000, 30000) : -1), map, list);
			}
            if (sendLetter)
            {
				string desc = null;
				if (forceDrop)
				{
					desc = centerDrop ? "NAT_RustedArmyRaid_CenterDrop".Translate() : (randomDrop ? "NAT_RustedArmyRaid_RandomDrop".Translate() : "NAT_RustedArmyRaid_EdgeDrop".Translate());
				}
				else
				{
					desc = groupCount > 1 ? "NAT_RustedArmyRaid_Groups".Translate(groupCount) : "NAT_RustedArmyRaid_NoGroups".Translate();
				}
				desc += "\n\n" + (stageThenAttack ? "NAT_RustedArmyRaid_Stage".Translate() : "NAT_RustedArmyRaid_Immediate".Translate());
				if(extraDescString != null)
                {
					desc += "\n\n" + extraDescString;
				}
				Find.LetterStack.ReceiveLetter("NAT_RustedArmyRaid".Translate(), desc, LetterDefOf.ThreatBig, raiders);
			}
			return raiders;
		}

		public static ThingDef GetSkyfaller(ThingDef fromDef)
        {
			return fromDef.GetCompProperties<CompProperties_RustedSoldier>()?.skyfaller ?? GetBaseSkyfaller(fromDef);
		}

		public static ThingDef GetBaseSkyfaller(ThingDef fromDef)
		{
			if (fromDef.race?.baseBodySize != null)
			{
				if(fromDef.race.baseBodySize > 1.2)
                {
					return NATDefOf.NAT_RustedChunk2x2Incoming;
				}
				return NATDefOf.NAT_RustedChunk1x1Incoming;
			}
			if (fromDef.size.x == 1 && fromDef.size.z == 1)
			{
				return NATDefOf.NAT_RustedChunk1x1Incoming;
			}
			if (fromDef.size.x == 2 && fromDef.size.z == 2)
			{
				return NATDefOf.NAT_RustedChunk2x2Incoming;
			}
			return NATDefOf.NAT_RustedChunk3x3Incoming;
		}

		[DebugAction("Incidents", "Execute Rusted Army raid", false, false, false, false, false, 0, false, allowedGameStates = AllowedGameStates.PlayingOnMap)]
		public static void ExecuteRaidDebug()
		{
			Map map = Find.CurrentMap;
			int count = 1;
			float points = 0;
			List<DebugMenuOption> list = new List<DebugMenuOption>();
			foreach (float item in DebugActionsUtility.PointsOptions(extended: true))
			{
				float localPoints = item;
				list.Add(new DebugMenuOption(item + " points", DebugMenuOptionMode.Action, delegate
				{
					points = IncidentWorker_RustedArmyRaid.PointsFromPoints.Evaluate(localPoints);
					List<DebugMenuOption> list2 = new List<DebugMenuOption>();
					list2.Add(new DebugMenuOption("Drop", DebugMenuOptionMode.Action, delegate
					{
						List<DebugMenuOption> list3 = new List<DebugMenuOption>();
						list3.Add(new DebugMenuOption("Edge", DebugMenuOptionMode.Action, delegate
						{
							ExecuteRaid(map, points, 1, false, true, null, null, true);
						}));
						list3.Add(new DebugMenuOption("Center", DebugMenuOptionMode.Action, delegate
						{
							ExecuteRaid(map, points, 1, false, true, null, null, true, false, true);
						}));
						list3.Add(new DebugMenuOption("Random", DebugMenuOptionMode.Action, delegate
						{
							ExecuteRaid(map, points, 1, false, true, null, null, true, true);
						}));
						Find.WindowStack.Add(new Dialog_DebugOptionListLister(list3));
					}));
					list2.Add(new DebugMenuOption("Walk in", DebugMenuOptionMode.Action, delegate
					{
						List<DebugMenuOption> list3 = new List<DebugMenuOption>();
						for (int i = 1; i < 6; i++)
						{
							int local = i;
							list3.Add(new DebugMenuOption("Groups count: " + i, DebugMenuOptionMode.Action, delegate
							{
								count = local;
								List<DebugMenuOption> list4 = new List<DebugMenuOption>();
								list4.Add(new DebugMenuOption("Stage", DebugMenuOptionMode.Action, delegate
								{
									ExecuteRaid(map, points, count, true);
								}));
								list4.Add(new DebugMenuOption("Immediate", DebugMenuOptionMode.Action, delegate
								{
									ExecuteRaid(map, points, count, false);
								}));
								Find.WindowStack.Add(new Dialog_DebugOptionListLister(list4));
							}));
						}
						Find.WindowStack.Add(new Dialog_DebugOptionListLister(list3));
					}));
					Find.WindowStack.Add(new Dialog_DebugOptionListLister(list2));
				}));
			}
			Find.WindowStack.Add(new Dialog_DebugOptionListLister(list));
		}
	}





	

	public class LordToil_SleepRust : LordToil
	{
		public override void UpdateAllDuties()
		{
			for (int i = 0; i < lord.ownedPawns.Count; i++)
			{
				Pawn p = lord.ownedPawns[i];
				p.mindState.duty = new PawnDuty(DutyDefOf.SleepForever);
				if(p.canBeDormant != null && p.canBeDormant.Awake)
				{
					p.canBeDormant.ToSleep();
				}
			}
		}
	}
}