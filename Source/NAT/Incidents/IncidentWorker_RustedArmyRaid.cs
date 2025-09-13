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
	public class IncidentWorker_RustedArmyRaid : IncidentWorker
	{
		public static readonly SimpleCurve PointsFromPoints = new SimpleCurve
		{
			new CurvePoint(0f, 900f),
			new CurvePoint(1000f, 950f),
			new CurvePoint(5000f, 3700f),
			new CurvePoint(10000f, 7000f),
			new CurvePoint(14000f, 10000f)
		};

		private static readonly SimpleCurve GroupsChanceFromPoints = new SimpleCurve
		{
			new CurvePoint(0f, 0f),
			new CurvePoint(1000f, 0f),
			new CurvePoint(2000f, 0.2f),
			new CurvePoint(10000f, 0.9f)
		};
		protected override bool TryExecuteWorker(IncidentParms parms)
		{
			Map map = (Map)parms.target;
			if (!map.TileInfo.OnSurface || Rand.Chance(0.3f))
			{
				RustedArmyUtility.ExecuteRaid(map, PointsFromPoints.Evaluate(parms.points), 1, false, true, null, null, true, Rand.Chance(0.5f));
				return true;
			}
			RustedArmyUtility.ExecuteRaid(map, PointsFromPoints.Evaluate(parms.points), Rand.Chance(GroupsChanceFromPoints.Evaluate(parms.points)) ? new IntRange(2, 3).RandomInRange : 1, Rand.Chance(0.2f));
			return true;
		}
	}

	public class LordJob_RustedArmy : LordJob
	{
		private bool canKidnap = true;

		private bool canTimeoutOrFlee = true;

		private bool sappers;

		private IntVec3 stageLoc;

		private bool canSteal = true;

		private bool breachers;

		private bool canPickUpOpportunisticWeapons;

		private int stageTicks = 0;

		private float fractionLostToAssault = 0.05f;

		private bool waitForever = false;

		public override bool GuiltyOnDowned => true;

		public LordJob_RustedArmy()
		{
		}

		public LordJob_RustedArmy(SpawnedPawnParams parms)
		{
			canKidnap = false;
			canTimeoutOrFlee = false;
			canSteal = false;
		}

		public LordJob_RustedArmy(IntVec3 stageLoc, int stageTicks, bool waitForever = false, bool sappers = false, bool canSteal = true, bool breachers = false, bool canPickUpOpportunisticWeapons = false)
		{
			this.stageLoc = stageLoc;
			this.stageTicks = stageTicks;
			this.sappers = sappers;
			this.canSteal = canSteal;
			this.breachers = breachers;
			this.canPickUpOpportunisticWeapons = canPickUpOpportunisticWeapons;
			this.waitForever = waitForever;
		}

		public override StateGraph CreateGraph()
		{
			StateGraph stateGraph = new StateGraph();
			List <LordToil> list = new List<LordToil>();
			LordToil lordToil = null;
			LordToil_StageRust lordToil_Stage = null;
			if (sappers)
			{
				lordToil = new LordToil_AssaultColonySappers();
				stateGraph.AddToil(lordToil);
				list.Add(lordToil);
				Transition transition = new Transition(lordToil, lordToil, canMoveToSameState: true);
				transition.AddTrigger(new Trigger_PawnLost());
				stateGraph.AddTransition(transition);
			}
			else if (breachers)
			{
				lordToil = new LordToil_AssaultColonyBreaching();
				stateGraph.AddToil(lordToil);
				list.Add(lordToil);
			}
            else
            {
				lordToil = new LordToil_AssaultColonyRust(attackDownedIfStarving: false, canPickUpOpportunisticWeapons);
				stateGraph.AddToil(lordToil);
			}
			if (waitForever || stageTicks > 0)
			{
				lordToil_Stage = new LordToil_StageRust(stageLoc);
				Transition transition = new Transition(lordToil_Stage, lordToil);
                if (!waitForever)
                {
					transition.AddTrigger(new Trigger_TicksPassed(stageTicks));
				}
				transition.AddTrigger(new Trigger_FractionPawnsLost(fractionLostToAssault));
				transition.AddPreAction(new TransitionAction_Message("MessageRaidersBeginningAssault".Translate("NAT_RustedSoldiers".Translate().CapitalizeFirst(), "NAT_RustedArmy".Translate()), MessageTypeDefOf.ThreatBig));
				transition.AddPostAction(new TransitionAction_WakeAll());
				stateGraph.AddTransition(transition);
				stateGraph.AddToil(lordToil_Stage);
				stateGraph.StartingToil = lordToil_Stage;
			}
			LordToil_AssaultColonyRust lordToil2 = new LordToil_AssaultColonyRust(attackDownedIfStarving: false, canPickUpOpportunisticWeapons);
			stateGraph.AddToil(lordToil2);
			LordToil_ExitMap lordToil_ExitMap = new LordToil_ExitMap(LocomotionUrgency.Jog, canDig: false, interruptCurrentJob: true);
			lordToil_ExitMap.useAvoidGrid = true;
			stateGraph.AddToil(lordToil_ExitMap);
			if (sappers)
			{
				Transition transition2 = new Transition(lordToil, lordToil2);
				transition2.AddTrigger(new Trigger_NoFightingSappers());
				stateGraph.AddTransition(transition2);
			}
			/*if (assaulterFaction != null && assaulterFaction.def.humanlikeFaction)
			{
				if (canTimeoutOrFlee)
				{
					Transition transition3 = new Transition(lordToil3, lordToil_ExitMap);
					transition3.AddSources(list);
					transition3.AddTrigger(new Trigger_TicksPassed((sappers ? SapTimeBeforeGiveUp : ((!breachers) ? AssaultTimeBeforeGiveUp : BreachTimeBeforeGiveUp)).RandomInRange));
					transition3.AddPreAction(new TransitionAction_Message("MessageRaidersGivenUpLeaving".Translate(assaulterFaction.def.pawnsPlural.CapitalizeFirst(), assaulterFaction.Name)));
					stateGraph.AddTransition(transition3);
					Transition transition4 = new Transition(lordToil3, lordToil_ExitMap);
					transition4.AddSources(list);
					float randomInRange = new FloatRange(0.25f, 0.35f).RandomInRange;
					transition4.AddTrigger(new Trigger_FractionColonyDamageTaken(randomInRange, 900f));
					transition4.AddPreAction(new TransitionAction_Message("MessageRaidersSatisfiedLeaving".Translate(assaulterFaction.def.pawnsPlural.CapitalizeFirst(), assaulterFaction.Name)));
					stateGraph.AddTransition(transition4);
				}
				if (canSteal)
				{
					LordToil startingToil2 = stateGraph.AttachSubgraph(new LordJob_Steal().CreateGraph()).StartingToil;
					Transition transition6 = new Transition(lordToil3, startingToil2);
					transition6.AddSources(list);
					transition6.AddPreAction(new TransitionAction_Message("MessageRaidersStealing".Translate(assaulterFaction.def.pawnsPlural.CapitalizeFirst(), assaulterFaction.Name)));
					transition6.AddTrigger(new Trigger_HighValueThingsAround());
					stateGraph.AddTransition(transition6);
				}
			}*/
			return stateGraph;
		}

		public override void ExposeData()
		{
			Scribe_Values.Look(ref stageLoc, "stageLoc");
			Scribe_Values.Look(ref fractionLostToAssault, "fractionLostToAssault", defaultValue: 0.05f);
			Scribe_Values.Look(ref waitForever, "waitForever", defaultValue: false);
			Scribe_Values.Look(ref canKidnap, "canKidnap", defaultValue: true);
			Scribe_Values.Look(ref canTimeoutOrFlee, "canTimeoutOrFlee", defaultValue: true);
			Scribe_Values.Look(ref sappers, "sappers", defaultValue: false);
			Scribe_Values.Look(ref canSteal, "canSteal", defaultValue: true);
			Scribe_Values.Look(ref breachers, "breaching", defaultValue: false);
			Scribe_Values.Look(ref canPickUpOpportunisticWeapons, "canPickUpOpportunisticWeapons", defaultValue: false);
		}
	}

	public class LordToil_AssaultColonyRust : LordToil
	{
		private bool attackDownedIfStarving;

		private bool canPickUpOpportunisticWeapons;

		public override bool ForceHighStoryDanger => true;

		public override bool AllowSatisfyLongNeeds => false;

		public LordToil_AssaultColonyRust(bool attackDownedIfStarving = false, bool canPickUpOpportunisticWeapons = false)
		{
			this.attackDownedIfStarving = attackDownedIfStarving;
			this.canPickUpOpportunisticWeapons = canPickUpOpportunisticWeapons;
		}

		public override void UpdateAllDuties()
		{
			for (int i = 0; i < lord.ownedPawns.Count; i++)
			{
				if (lord.ownedPawns[i].mindState != null)
				{
					lord.ownedPawns[i].mindState.duty = new PawnDuty(NATDefOf.NAT_RustAssaultColony);
					lord.ownedPawns[i].mindState.duty.attackDownedIfStarving = attackDownedIfStarving;
					lord.ownedPawns[i].mindState.duty.pickupOpportunisticWeapon = canPickUpOpportunisticWeapons;
					lord.ownedPawns[i].TryGetComp<CompCanBeDormant>()?.WakeUp();
				}
			}
		}
	}

	public class LordToil_StageRust : LordToil
	{
		public override IntVec3 FlagLoc => Data.stagingPoint;

		private LordToilData_Stage Data => (LordToilData_Stage)data;

		public override bool ForceHighStoryDanger => true;

		public LordToil_StageRust(IntVec3 stagingLoc)
		{
			data = new LordToilData_Stage();
			Data.stagingPoint = stagingLoc;
		}

		public override void UpdateAllDuties()
		{
			LordToilData_Stage lordToilData_Stage = Data;
			for (int i = 0; i < lord.ownedPawns.Count; i++)
			{
				lord.ownedPawns[i].mindState.duty = new PawnDuty(NATDefOf.NAT_RustDefend, lordToilData_Stage.stagingPoint);
				lord.ownedPawns[i].mindState.duty.radius = 28f;
			}
		}
	}
}