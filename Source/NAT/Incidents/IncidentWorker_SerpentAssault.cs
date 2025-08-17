using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using RimWorld;
using RimWorld.BaseGen;
using RimWorld.IO;
using RimWorld.Planet;
using RimWorld.QuestGen;
using RimWorld.SketchGen;
using RimWorld.Utility;
using LudeonTK;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Grammar;
using Verse.Noise;
using Verse.Profile;
using Verse.Sound;
using Verse.Steam;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace NAT
{
	public class IncidentWorker_SerpentAssault : IncidentWorker
	{
		protected override bool TryExecuteWorker(IncidentParms parms)
		{
			Map map = (Map)parms.target;
			if (map.TileInfo.Isnt<SurfaceTile>(out var casted))
			{
				return false;
			}
			bool num = !casted.Rivers.NullOrEmpty();
			bool isCoastal = map.TileInfo.IsCoastal;
			bool flag = (num || isCoastal) && Rand.Chance(0.9f);
            if (flag && TryExecuteWaterAssault(parms))
            {
				return true;
			}
			if (TryExecuteGroundAssault(parms))
			{
				return true;
			}
			return false;
		}

		public bool TryExecuteGroundAssault(IncidentParms parms)
		{
			parms.faction = Faction.OfEntities;
			parms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn;
			PawnGroupMakerParms defaultPawnGroupMakerParms = IncidentParmsUtility.GetDefaultPawnGroupMakerParms(NATDefOf.NAT_Serpents, parms);
			float num = Faction.OfEntities.def.MinPointsToGeneratePawnGroup(NATDefOf.NAT_Serpents);
			if (parms.points < num)
			{
				parms.points = (defaultPawnGroupMakerParms.points = num * 2f);
			}
			List<Pawn> list = PawnGroupMakerUtility.GeneratePawns(defaultPawnGroupMakerParms).ToList();
			if (!parms.raidArrivalMode.Worker.TryResolveRaidSpawnCenter(parms))
			{
				return false;
			}
			parms.raidArrivalMode.Worker.Arrive(list, parms);
			if (AnomalyIncidentUtility.IncidentShardChance(parms.points))
			{
				AnomalyIncidentUtility.PawnShardOnDeath(list.RandomElement());
			}
			LordMaker.MakeNewLord(Faction.OfEntities, new LordJob_SerpentAssault(), parms.target as Map, list);
			SendStandardLetter(def.letterLabel, def.letterText, def.letterDef, parms, list);
			return true;
		}

		public bool TryExecuteWaterAssault(IncidentParms parms)
		{
			parms.faction = Faction.OfEntities;
			parms.raidArrivalMode = PawnsArrivalModeDefOf.EmergeFromWater;
			PawnGroupMakerParms defaultPawnGroupMakerParms = IncidentParmsUtility.GetDefaultPawnGroupMakerParms(NATDefOf.NAT_Serpents, parms);
			float num = Faction.OfEntities.def.MinPointsToGeneratePawnGroup(NATDefOf.NAT_Serpents);
			defaultPawnGroupMakerParms.points = parms.points * 0.7f;
			if (defaultPawnGroupMakerParms.points < num)
			{
				defaultPawnGroupMakerParms.points = num * 2f;
			}
			List<Pawn> list = PawnGroupMakerUtility.GeneratePawns(defaultPawnGroupMakerParms).ToList();
			if (!parms.raidArrivalMode.Worker.TryResolveRaidSpawnCenter(parms))
			{
				return false;
			}
			Lord lord = LordMaker.MakeNewLord(Faction.OfEntities, new LordJob_SerpentAssault(), parms.target as Map);
			parms.lord = lord;
			parms.raidArrivalMode.Worker.Arrive(list, parms);
			if (AnomalyIncidentUtility.IncidentShardChance(defaultPawnGroupMakerParms.points))
			{
				AnomalyIncidentUtility.PawnShardOnDeath(list.RandomElement());
			}
			SendStandardLetter(def.letterLabel, def.letterText, def.letterDef, parms, list);
			return true;
		}
	}
	public class IncidentWorker_SerpentWaterAssault : IncidentWorker
	{
		protected override bool TryExecuteWorker(IncidentParms parms)
		{
			parms.faction = Faction.OfEntities;
			parms.raidArrivalMode = PawnsArrivalModeDefOf.EmergeFromWater;
			PawnGroupMakerParms defaultPawnGroupMakerParms = IncidentParmsUtility.GetDefaultPawnGroupMakerParms(NATDefOf.NAT_Serpents, parms);
			float num = Faction.OfEntities.def.MinPointsToGeneratePawnGroup(NATDefOf.NAT_Serpents);
			defaultPawnGroupMakerParms.points = parms.points * 0.7f;
			if (defaultPawnGroupMakerParms.points < num)
			{
				defaultPawnGroupMakerParms.points = num * 2f;
			}
			List<Pawn> list = PawnGroupMakerUtility.GeneratePawns(defaultPawnGroupMakerParms).ToList();
			if (!parms.raidArrivalMode.Worker.TryResolveRaidSpawnCenter(parms))
			{
				return false;
			}
			Lord lord = LordMaker.MakeNewLord(Faction.OfEntities, new LordJob_SerpentAssault(), parms.target as Map);
			parms.lord = lord;
			parms.raidArrivalMode.Worker.Arrive(list, parms);
			if (AnomalyIncidentUtility.IncidentShardChance(defaultPawnGroupMakerParms.points))
			{
				AnomalyIncidentUtility.PawnShardOnDeath(list.RandomElement());
			}
			SendStandardLetter(def.letterLabel, def.letterText, def.letterDef, parms, list);
			return true;
		}
	}

	public class LordToil_SerpentAssault : LordToil
	{
		public override void UpdateAllDuties()
		{
			foreach (Pawn ownedPawn in lord.ownedPawns)
			{
				ownedPawn.mindState.duty = new PawnDuty(NATDefOf.NAT_SerpentAssault);
			}
		}
	}

	public class LordJob_SerpentAssault : LordJob
	{
		public override StateGraph CreateGraph()
		{
			StateGraph stateGraph = new StateGraph();
			LordToil toil = new LordToil_SerpentAssault();
			stateGraph.AddToil(toil);
			return stateGraph;
		}
	}
}