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
	public class LordJob_DefendRust : LordJob
	{
		private bool sendWokenUpMessage;

		public bool awakeOnClamor;

		public IntVec3 position;

		public float wanderRadius;

		public string attackSignal = "";

		public bool forceWakeUp = false;

		public bool sleep = true;

		public LordJob_DefendRust()
		{
		}

		public LordJob_DefendRust(IntVec3 position, float wanderRadius, bool sleep, bool sendWokenUpMessage = true, bool awakeOnClamor = false, bool forceWakeUp = false, string attackSignal = "")
		{
			this.sendWokenUpMessage = sendWokenUpMessage;
			this.position = position;
			this.wanderRadius = wanderRadius;
			this.awakeOnClamor = awakeOnClamor;
			this.forceWakeUp = forceWakeUp;
			this.attackSignal = attackSignal;
			this.sleep = sleep;
		}

		protected virtual LordToil GetIdleToil()
		{
			if (sleep)
			{
				return new LordToil_Sleep();
			}
			return new LordToil_StageRust(position);
		}

		public override StateGraph CreateGraph()
		{
			StateGraph stateGraph = new StateGraph();
			LordToil firstSource = (stateGraph.StartingToil = GetIdleToil());
			LordToil_StageRust lordToil_Stage = new LordToil_StageRust(position);
			stateGraph.AddToil(lordToil_Stage);
			LordToil_AssaultColonyRust lordToil_AssaultColony = new LordToil_AssaultColonyRust();
			stateGraph.AddToil(lordToil_AssaultColony);
			Transition transition = new Transition(firstSource, lordToil_Stage);
			transition.AddTrigger(new Trigger_Custom((TriggerSignal signal) => sleep && (signal.type == TriggerSignalType.DormancyWakeup || (awakeOnClamor && signal.type == TriggerSignalType.Clamor))));
			if (sendWokenUpMessage)
			{
				transition.AddPreAction(new TransitionAction_Message("MessageSleepingPawnsWokenUp".Translate("NAT_RustedSoldiers".Translate().CapitalizeFirst()).CapitalizeFirst(), MessageTypeDefOf.ThreatBig, null, 1f, AnyAsleep));
			}
			transition.AddPostAction(new TransitionAction_WakeAll());
			stateGraph.AddTransition(transition);
			Transition transition2 = new Transition(firstSource, lordToil_AssaultColony);
			transition2.AddTrigger(new Trigger_PawnHarmed(1f, requireInstigatorWithFaction: false));
			transition2.AddTrigger(new Trigger_Custom((TriggerSignal signal) => ((signal.type == TriggerSignalType.BuildingDamaged || signal.type == TriggerSignalType.BuildingLost) && signal.thing is Building b && b.GetLord() == lord) || signal.signal.tag == "NAT_CrateOpened" || (!attackSignal.NullOrEmpty() && signal.signal.tag == attackSignal && (!sleep || signal.signal.args.GetArg<bool>("wakeUp") == true))));
			transition2.AddPostAction(new TransitionAction_Custom(delegate (Transition t)
			{
				Log.Message("NAT_?");
				foreach (Lord lord in t.Map.lordManager.lords)
				{
					lord.Notify_SignalReceived(new Signal(attackSignal, new NamedArgument(forceWakeUp == true, "wakeUp")));
				}
			}));
			if (sendWokenUpMessage)
			{
				transition2.AddPreAction(new TransitionAction_Message("MessageSleepingPawnsWokenUp".Translate("NAT_RustedSoldiers".Translate().CapitalizeFirst()).CapitalizeFirst(), MessageTypeDefOf.ThreatBig, null, 1f, AnyAsleep));
			}
			transition2.AddPostAction(new TransitionAction_WakeAll());
			stateGraph.AddTransition(transition2);
			Transition transition3 = new Transition(lordToil_Stage, lordToil_AssaultColony);
			transition3.AddTrigger(new Trigger_PawnHarmed(1f, requireInstigatorWithFaction: false));
			transition3.AddTrigger(new Trigger_Custom((TriggerSignal signal) => signal.type == TriggerSignalType.BuildingDamaged || signal.type == TriggerSignalType.BuildingLost || (!attackSignal.NullOrEmpty() && signal.signal.tag == attackSignal)));
			transition3.AddPostAction(new TransitionAction_Custom(delegate (Transition t)
			{
				Log.Message("NAT_!");
				foreach (Lord lord in t.Map.lordManager.lords)
				{
					lord.Notify_SignalReceived(new Signal(attackSignal, new NamedArgument(forceWakeUp == true, "wakeUp")));
				}
			}));
			stateGraph.AddTransition(transition3);
			Transition transition4 = new Transition(lordToil_AssaultColony, lordToil_Stage);
			transition4.AddTrigger(new Trigger_TicksPassedWithoutHarm(1200));
			stateGraph.AddTransition(transition4);
			Transition transition5 = new Transition(lordToil_Stage, firstSource);
			transition5.AddTrigger(new Trigger_TicksPassedWithoutHarm(5000));
			stateGraph.AddTransition(transition5);
			return stateGraph;
		}

		private bool AnyAsleep()
		{
			for (int i = 0; i < lord.ownedPawns.Count; i++)
			{
				if (lord.ownedPawns[i].Spawned && !lord.ownedPawns[i].Dead && !lord.ownedPawns[i].Awake())
				{
					return true;
				}
			}
			return false;
		}

		public override void ExposeData()
		{
			Scribe_Values.Look(ref sendWokenUpMessage, "sendWokenUpMessage", defaultValue: true);
			Scribe_Values.Look(ref awakeOnClamor, "awakeOnClamor", defaultValue: false);
			Scribe_Values.Look(ref position, "position");
			Scribe_Values.Look(ref wanderRadius, "wanderRadius", 0f);
		}
	}

	public class LordJob_AssistColony_Rust : LordJob
	{
		private IntVec3 fallbackLocation;

		public LordJob_AssistColony_Rust()
		{
		}

		public LordJob_AssistColony_Rust(IntVec3 fallbackLocation)
		{
			this.fallbackLocation = fallbackLocation;
		}

		public override StateGraph CreateGraph()
		{
			StateGraph stateGraph = new StateGraph();
			LordToil_HuntEnemies lordToil_HuntEnemies = (LordToil_HuntEnemies)(stateGraph.StartingToil = new LordToil_HuntEnemies(fallbackLocation));
			LordToil_ExitMap lordToil_ExitMap = new LordToil_ExitMap();
			stateGraph.AddToil(lordToil_ExitMap);
			Transition transition = new Transition(lordToil_HuntEnemies, lordToil_ExitMap);
			transition.AddPreAction(new TransitionAction_Message("NAT_MessageRustedTroopersLeaving".Translate()));
			transition.AddTrigger(new Trigger_TicksPassed(30000));
			//transition.AddPreAction(new TransitionAction_EnsureHaveExitDestination());
			stateGraph.AddTransition(transition);
			return stateGraph;
		}

		public override void ExposeData()
		{
			Scribe_Values.Look(ref fallbackLocation, "fallbackLocation");
		}
	}

	public class LordJob_DefendVoidStructure : LordJob
	{
		private Thing structure;

		private float? wanderRadius;

		private float? defendRadius;

		private bool isCaravanSendable;

		private bool addFleeToil;

		private int ticksBeforeAttack;

		public override bool IsCaravanSendable => isCaravanSendable;

		public override bool AddFleeToil => addFleeToil;

		public LordJob_DefendVoidStructure()
		{
		}

		public LordJob_DefendVoidStructure(Thing structure, int ticksBeforeAttack, float? wanderRadius = null, float? defendRadius = null, bool isCaravanSendable = false, bool addFleeToil = true)
		{
			this.structure = structure;
			this.ticksBeforeAttack = ticksBeforeAttack;
			this.wanderRadius = wanderRadius;
			this.defendRadius = defendRadius;
			this.isCaravanSendable = isCaravanSendable;
			this.addFleeToil = addFleeToil;
		}

		public override StateGraph CreateGraph()
		{
			StateGraph stateGraph = new StateGraph();
			LordToil_DefendPoint lordToil_DefendStructure = (LordToil_DefendPoint)(stateGraph.StartingToil = new LordToil_DefendPoint(structure.Position, wanderRadius: wanderRadius, defendRadius: defendRadius));
			LordToil_AssaultColonyRust lordToil_AssaultColony = new LordToil_AssaultColonyRust(attackDownedIfStarving: true)
			{
				useAvoidGrid = true
			};
			stateGraph.AddToil(lordToil_AssaultColony);
			Transition transition = new Transition(lordToil_DefendStructure, lordToil_AssaultColony);
			transition.AddTrigger(new Trigger_FractionPawnsLost(0.1f));
			transition.AddTrigger(new Trigger_PawnHarmed(0.5f));
			transition.AddTrigger(new Trigger_TicksPassed(ticksBeforeAttack));
			transition.AddTrigger(new Trigger_OnClamor(ClamorDefOf.Ability));
			transition.AddTrigger(new Trigger_StructureActivated(structure));
			transition.AddPostAction(new TransitionAction_WakeAll());
			TaggedString taggedString = "MessageDefendersAttacking".Translate("NAT_RustedSoldiers".Translate(), "NAT_RustedArmy".Translate(), Faction.OfPlayer.def.pawnsPlural).CapitalizeFirst();
			transition.AddPreAction(new TransitionAction_Message(taggedString, MessageTypeDefOf.ThreatBig));
			stateGraph.AddTransition(transition);
			return stateGraph;
		}

		public override void ExposeData()
		{
			Scribe_Deep.Look(ref structure, "structure");
			Scribe_Values.Look(ref wanderRadius, "wanderRadius");
			Scribe_Values.Look(ref defendRadius, "defendRadius");
			Scribe_Values.Look(ref isCaravanSendable, "isCaravanSendable", defaultValue: false);
			Scribe_Values.Look(ref addFleeToil, "addFleeToil", defaultValue: false);
		}
	}
	public class Trigger_TicksPassedWithoutHarm : Trigger_TicksPassed
	{
		public Trigger_TicksPassedWithoutHarm(int tickLimit)
			: base(tickLimit)
		{
		}

		public override bool ActivateOn(Lord lord, TriggerSignal signal)
		{
			if (Trigger_PawnHarmed.SignalIsHarm(signal))
			{
				base.Data.ticksPassed = 0;
			}
			return base.ActivateOn(lord, signal);
		}
	}



	public class TriggerData_StructureActivated : TriggerData
	{
		public Thing structure;

		public override void ExposeData()
		{
			Scribe_References.Look(ref structure, "structure", saveDestroyedThings: true);
		}
	}

	public class Trigger_StructureActivated : Trigger
	{
		protected TriggerData_StructureActivated Data => (TriggerData_StructureActivated)data;

		public Trigger_StructureActivated(Thing structure)
		{
			data = new TriggerData_StructureActivated();
			Data.structure = structure;
		}

		public override bool ActivateOn(Lord lord, TriggerSignal signal)
		{
			if (signal.type == TriggerSignalType.Tick)
			{
				if (data == null || !(data is TriggerData_StructureActivated))
				{
					return true;
				}
				TriggerData_StructureActivated triggerData_StructureActivated = Data;
				Thing structure = triggerData_StructureActivated.structure;
				if (!(structure is ThingWithComps s) || s.GetComp<CompVoidStructure>().Active)
				{
					return true;
				}
			}
			return false;
		}
	}
}