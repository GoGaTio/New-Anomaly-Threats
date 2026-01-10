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
	public enum CollectorState
	{
		Wait,
		Collect,
		Attack,
		Escape
	}
	public class CompProperties_Collector : CompProperties
	{
		public List<ThingDef> highPriorityThings;

		public IntRange thingsRange;

		public IntRange waitingTicksRange;

		public CompProperties_Collector()
		{
			compClass = typeof(CompCollector);
		}
	}
	public class CompCollector : ThingComp, IThingHolder
	{
		public CompProperties_Collector Props => (CompProperties_Collector)props;

		[Unsaved(false)]
		public HediffComp_Invisibility invisibility;

		private int lastDetectedTick = -99999;

		private static float lastNotified = -99999f;

		public ThingOwner innerContainer;

		public bool active;

		public CollectorState state = CollectorState.Attack;

		public List<ThingDef> stealedDefs = new List<ThingDef>();

		public float speedOffset = 0;

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
			Scribe_Values.Look(ref active, "active", defaultValue: false);
			Scribe_Values.Look(ref lastDetectedTick, "lastDetectedTick", 0);
            Scribe_Values.Look(ref speedOffset, "speedOffset", 0);
            Scribe_Values.Look(ref thingsToStealLeft, "thingsToStealLeft", 0);
			Scribe_Values.Look(ref state, "state", CollectorState.Attack);
			Scribe_References.Look(ref questPart, "questPart");
		}
        private static readonly SimpleCurve SpeedOffsetFromSpeedThisTickCurve = new SimpleCurve
        {
            new CurvePoint(0f, 9f),
            new CurvePoint(9f, 0f),
            new CurvePoint(18f, -9f),
			new CurvePoint(1000f, -992f)
        };

		private static readonly SimpleCurve DamageOffsetCurve = new SimpleCurve
		{
			new CurvePoint(0f, 0f),
			new CurvePoint(10f, 0f),
			new CurvePoint(30f, 0.3f),
			new CurvePoint(60f, 0.6f),
			new CurvePoint(120f, 0.9f)
		};

		private Pawn Collector => (Pawn)parent;

		public QuestPart_Collector questPart;

		public HediffComp_Invisibility Invisibility => invisibility ?? (invisibility = Collector.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.HoraxianInvisibility)?.TryGetComp<HediffComp_Invisibility>());

		public int thingsToStealLeft;

		public int waitTicks = 0;

		public int takenDamage;

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			foreach (Gizmo item in base.CompGetGizmosExtra())
			{
				yield return item;
			}
			if (DebugSettings.ShowDevGizmos)
			{
				Command_Action command_Action = new Command_Action();
				command_Action.defaultLabel = "DEV: Activate";
				command_Action.action = delegate
				{
                    
				};
				yield return command_Action;
				Command_Action command_Action2 = new Command_Action();
				command_Action2.defaultLabel = "DEV: Change state(Current: " + state.ToString() + ")";
				command_Action2.action = delegate
				{
					List<FloatMenuOption> list = new List<FloatMenuOption>();
					list.Add(new FloatMenuOption("Wait", delegate
					{
						waitTicks = Props.waitingTicksRange.RandomInRange;
						state = CollectorState.Wait;
					}));
					list.Add(new FloatMenuOption("Collect", delegate
					{
						thingsToStealLeft = Props.thingsRange.RandomInRange;
						state = CollectorState.Collect;
					}));
					list.Add(new FloatMenuOption("Escape", delegate
					{
						state = CollectorState.Escape;
					}));
					list.Add(new FloatMenuOption("Attack", delegate
					{
						state = CollectorState.Attack;
					}));
					Find.WindowStack.Add(new FloatMenu(list));
				};
				yield return command_Action2;
				Command_Action command_Action3 = new Command_Action();
				command_Action3.defaultLabel = "DEV: ThingsToSteal +1(Current: " + thingsToStealLeft.ToString() + ")";
				command_Action3.action = delegate
				{
					thingsToStealLeft++;
				};
				yield return command_Action3;
				Command_Action command_Action4 = new Command_Action();
				command_Action4.defaultLabel = "DEV: Force steal pawn";
				command_Action4.action = delegate
				{
					Pawn p = JobDriver_CollectorStealPawn.GetClosestTargetInRadius(Collector, 999f);
					if (p != null && parent.Map.pathFinder.FindPathNow(parent.Position, p.Position, TraverseParms.For(Collector, Danger.Deadly, TraverseMode.PassDoors)) != null)
					{
						Collector.mindState.enemyTarget = p;
						Job job = JobMaker.MakeJob(NATDefOf.NAT_CollectorStealPawn, Collector.mindState.enemyTarget);
						job.count = 1;
						Collector.jobs.StartJob(job, JobCondition.InterruptForced);
					}
				};
				yield return command_Action4;
				Command_Action command_Action5 = new Command_Action();
				command_Action5.defaultLabel = "DEV: Drop bag";
				command_Action5.action = delegate
				{
					DropBag(parent.PositionHeld, parent.MapHeld);
				};
				yield return command_Action5;
			}
		}

        public override void PostPostMake()
        {
            base.PostPostMake();
			innerContainer = new ThingOwner<Thing>(this, oneStackOnly: false);
			state = CollectorState.Wait;
		}

		public override void PostSpawnSetup(bool respawningAfterLoad)
		{
			base.PostSpawnSetup(respawningAfterLoad);
		}
        public override void CompTickInterval(int delta)
        {
            base.CompTickInterval(delta);
			if(takenDamage > 0)
			{
				takenDamage -= delta;
			}
		}

        public override string CompInspectStringExtra()
        {
			string s = null;
			if(DebugSettings.ShowDevGizmos && Collector.CurJob != null)
            {
				s = Collector.CurJob.def.defName;
			}
			return s;
        }

        public override void PostDrawExtraSelectionOverlays()
        {
            base.PostDrawExtraSelectionOverlays();
			if (DebugSettings.ShowDevGizmos && Collector.CurJob != null)
			{
				if (Collector.CurJob.targetA.IsValid)
				{
					GenDraw.DrawCircleOutline(Collector.CurJob.targetA.CenterVector3, 0.7f, SimpleColor.Red);
					GenDraw.DrawLineBetween(parent.TrueCenter(), Collector.CurJob.targetA.CenterVector3, SimpleColor.Red);
				}
				if (Collector.CurJob.targetB.IsValid)
				{
					GenDraw.DrawCircleOutline(Collector.CurJob.targetB.CenterVector3, 0.7f, SimpleColor.Blue);
					GenDraw.DrawLineBetween(parent.TrueCenter(), Collector.CurJob.targetB.CenterVector3, SimpleColor.Blue);
				}
			}
        }

        public override void CompTick()
		{
			base.CompTick();
			if (Collector.IsShambler)
			{
				return;
			}
			if (Invisibility == null)
			{
				Collector.health.AddHediff(HediffDefOf.HoraxianInvisibility);
			}
			if (!Collector.Spawned)
			{
				return;
			}
			if (parent.IsHashIntervalTick(14))
			{
				if (state != CollectorState.Wait && Find.TickManager.TicksGame > lastDetectedTick + 1200)
				{
					CheckDetected();
				}
				if (Find.TickManager.TicksGame > lastDetectedTick + 1200)
				{
					Invisibility.BecomeInvisible();
				}
			}
            if (parent.IsHashIntervalTick(90))
            {
				speedOffset += SpeedOffsetFromSpeedThisTickCurve.Evaluate(Collector.GetStatValue(StatDefOf.MoveSpeed));
            }
            if (active && state == CollectorState.Wait)
            {
				waitTicks--;
				if(waitTicks <= 0)
                {
					thingsToStealLeft = Props.thingsRange.RandomInRange;
					state = CollectorState.Collect;
				}
			}
		}

        public override float GetStatOffset(StatDef stat)
        {
            if(stat == StatDefOf.MoveSpeed)
			{
				return speedOffset;
			}
			if(stat == StatDefOf.IncomingDamageFactor)
			{
				return -DamageOffsetCurve.Evaluate(takenDamage);
			}
			return base.GetStatOffset(stat);
        }

		private void CheckDetected()
		{
			foreach (Pawn item in Collector.Map.listerThings.ThingsInGroup(ThingRequestGroup.Pawn))
			{
				if (PawnCanDetect(item))
				{
					if (!Invisibility.PsychologicallyVisible)
					{
						Invisibility.BecomeVisible();
					}
					lastDetectedTick = Find.TickManager.TicksGame;
				}
			}
		}

		private bool PawnCanDetect(Pawn pawn)
		{
			if (pawn.Faction == Faction.OfEntities || pawn.Faction == Faction.OfMechanoids || pawn.Downed || !pawn.Awake())
			{
				return false;
			}
			if (pawn.IsAnimal)
			{
				return false;
			}
			if (!Collector.Position.InHorDistOf(pawn.Position, GetPawnSightRadius(pawn, Collector)))
			{
				return false;
			}
			return GenSight.LineOfSightToThing(pawn.Position, Collector, parent.Map);
		}

		private static float GetPawnSightRadius(Pawn pawn, Pawn collector)
		{
			float num = 7f;
			if (pawn.genes == null || pawn.genes.AffectedByDarkness)
			{
				float t = collector.Map.glowGrid.GroundGlowAt(collector.Position);
				num *= Mathf.Lerp(0.33f, 1f, t);
			}
			return num * pawn.health.capacities.GetLevel(PawnCapacityDefOf.Sight);
		}

		public override void Notify_BecameVisible()
		{
			SoundDefOf.Pawn_Sightstealer_Howl.PlayOneShotOnCamera();
			foreach (Pawn item in Collector.MapHeld.listerThings.ThingsInGroup(ThingRequestGroup.Pawn))
			{
				if (item.kindDef == NATDefOf.NAT_Collector && item != Collector && item.Position.InHorDistOf(Collector.Position, 30f) && !item.IsPsychologicallyInvisible() && GenSight.LineOfSight(item.Position, Collector.Position, item.Map))
				{
					return;
				}
			}
			if (RealTime.LastRealTime > lastNotified + 60f)
			{
				Find.LetterStack.ReceiveLetter("NAT_LetterLabelCollectorRevealed".Translate(), "NAT_LetterCollectorRevealed".Translate(), LetterDefOf.ThreatBig, Collector, null, null, null, null, 6);
			}
			else
			{
				Messages.Message("NAT_MessageCollectorRevealed".Translate(), Collector, MessageTypeDefOf.ThreatBig);
			}
			lastNotified = RealTime.LastRealTime;
			lastDetectedTick = Find.TickManager.TicksGame;
			if(state == CollectorState.Wait)
			{
				state = CollectorState.Escape;
				Collector.jobs.CheckForJobOverride();
			}
		}

		public void GetChildHolders(List<IThingHolder> outChildren)
		{
			ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
		}

		public ThingOwner GetDirectlyHeldThings()
		{
			return innerContainer;
		}

        public override void Notify_UsedVerb(Pawn pawn, Verb verb)
        {
            base.Notify_UsedVerb(pawn, verb);
			if (!Collector.IsShambler)
			{
				Invisibility.BecomeVisible();
				lastDetectedTick = Find.TickManager.TicksGame;
			}
		}

        public override void PostSwapMap()
        {
			base.PostSwapMap();
			questPart.mapParent = parent.MapHeld.Parent;
		}

        public override void Notify_MapRemoved()
        {
			questPart.EscapeCollector(Collector);
			base.Notify_MapRemoved();
        }

		public IEnumerable<Thing> PriorityThingsToSteal()
		{
            if (ModsConfig.OdysseyActive)
            {
				Thing engine = GravshipUtility.GetPlayerGravEngine_NewTemp(parent.Map);
				if(engine != null)
                {
					if (engine.Spawned)
					{
						yield return engine;
					}
					else if (engine.ParentHolder != null && engine.ParentHolder is Thing t && t.def == engine.def.minifiedDef)
					{
						yield return t;
					}
				}
				foreach (Thing unique in parent.Map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon).Where((Thing x)=>x.HasComp<CompUniqueWeapon>()))
				{
					yield return unique;
				}
			}
			foreach(ThingDef def in Props.highPriorityThings)
            {
				foreach (Thing thing in parent.Map.listerThings.ThingsOfDef(def))
				{
					yield return thing;
				}
			}
		}

		public IEnumerable<Thing> ThingsToSteal()
        {
			foreach(Thing t in parent.Map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver))
            {
                if (!(t is Corpse) && t.MarketValue > 30f && !stealedDefs.Contains(t.def))
                {
					yield return t;
                }
            }
        }

		public void AddThing(Thing t)
		{
			innerContainer.TryAddOrTransfer(t);
			if (!Props.highPriorityThings.Contains(t.def) && t.TryGetComp<CompArt>()?.Active != true && !stealedDefs.Contains(t.def))
			{
				stealedDefs.Add(t.def);
			}
			thingsToStealLeft--;
			if(thingsToStealLeft <= 0)
            {
				state = CollectorState.Escape;
            }
		}

		public override void Notify_Downed()
		{
			if (active)
			{
				DropBag(parent.PositionHeld, parent.MapHeld);
				innerContainer.TryDropAll(parent.PositionHeld, parent.MapHeld, ThingPlaceMode.Near);
			}
			base.Notify_Downed();
		}

        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
			if (active)
			{
				DropBag(parent.PositionHeld, prevMap);
				innerContainer.TryDropAll(parent.PositionHeld, prevMap, ThingPlaceMode.Near);
			}
			base.Notify_Killed(prevMap, dinfo);
        }

        public override void PostPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.PostPostApplyDamage(dinfo, totalDamageDealt);
			takenDamage += Mathf.RoundToInt(dinfo.Amount);
			if (innerContainer != null && parent.Spawned && Rand.Chance(totalDamageDealt * 0.01f))
            {
				if (innerContainer.Any)
				{
                    innerContainer.TryDrop(innerContainer.RandomElement(), parent.Position, parent.Map, ThingPlaceMode.Near, 1, out var _);
                }
				if (Collector.CurJobDef == NATDefOf.NAT_CollectorStealThing && Rand.Bool)
				{
					thingsToStealLeft = 1;
                }
			}
			
        }

        public void DropBag(IntVec3 cell, Map map)
		{
			Thing thing = ThingMaker.MakeThing(NATDefOf.NAT_CollectorNotes);
			CompCollectorNotes comp = thing.TryGetComp<CompCollectorNotes>();
			comp.questPart = questPart;
			questPart.caught = true; 
			GenSpawn.Spawn(thing, cell, map);
			active = false;
		}
	}
}