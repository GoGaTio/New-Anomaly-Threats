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

    public class ThinkNode_ConditionalCollectorState : ThinkNode_Conditional
	{
		public CollectorState state;

		protected override bool Satisfied(Pawn pawn)
		{
			CompCollector comp = pawn.GetComp<CompCollector>();
			if (comp != null && comp.active)
			{
				return comp.state == state;
			}
			return false;
		}
	}

	public class JobGiver_CollectorSteal : ThinkNode_JobGiver
	{
        private static readonly SimpleCurve StealPawnChanceFromCountCurve = new SimpleCurve
        {
            new CurvePoint(3f, 0.06f),
            new CurvePoint(6f, 0.15f),
            new CurvePoint(10f, 0.5f)
        };

        protected override Job TryGiveJob(Pawn pawn)
		{
			if (pawn.CurJob != null && pawn.CurJob.def == NATDefOf.NAT_CollectorStealPawn)
			{
				return pawn.CurJob;
			}
			CompCollector comp = pawn.GetComp<CompCollector>();
			Map map = pawn.Map;
			int num = map.mapPawns.ColonistCount;
            if (num > 2 && Rand.Chance(StealPawnChanceFromCountCurve.Evaluate(num)))
            {
				Pawn p = JobDriver_CollectorStealPawn.GetClosestTargetInRadius(pawn, 999f);
				if(p != null && pawn.Map.pathFinder.FindPathNow(pawn.Position, p.Position, TraverseParms.For(pawn, Danger.Deadly, TraverseMode.PassDoors)) != null)
                {
					pawn.mindState.enemyTarget = p;
					Job job1 = JobMaker.MakeJob(NATDefOf.NAT_CollectorStealPawn, pawn.mindState.enemyTarget);
					job1.count = 1;
					return job1;
				}
			}
			Thing t = GenClosest.ClosestThing_Global_Reachable(pawn.Position, map, comp.PriorityThingsToSteal(), PathEndMode.ClosestTouch, TraverseParms.For(pawn));
			if(t == null)
            {
				t = GenClosest.ClosestThing_Global_Reachable(pawn.Position, map, comp.ThingsToSteal(), PathEndMode.ClosestTouch, TraverseParms.For(pawn));
				if(t == null)
                {
					if (comp.innerContainer?.Any == true)
					{
                        comp.state = CollectorState.Escape;
                    }
					else
					{
                        comp.waitTicks = comp.Props.waitingTicksRange.RandomInRange;
                        comp.state = CollectorState.Wait;
                    }
					return null;
				}
            }
			Job job2 = JobMaker.MakeJob(NATDefOf.NAT_CollectorStealThing, t);
			job2.count = t.stackCount;
			return job2;
		}

		private bool IsGoodTarget(Thing thing)
		{
			if (thing is Pawn pawn && !pawn.Downed && pawn.RaceProps.Humanlike)
			{
				return !pawn.IsPsychologicallyInvisible();
			}
			return false;
		}
	}

	public class JobDriver_CollectorStealThing : JobDriver
	{
		private const TargetIndex ItemInd = TargetIndex.A;

		protected Thing Item => job.GetTarget(TargetIndex.A).Thing;

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			return pawn.Reserve(Item, job, 1, -1, null, errorOnFailed, true);
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnDestroyedOrNull(TargetIndex.A);
			CompCollector comp = pawn.TryGetComp<CompCollector>();
			Toil toil = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch).FailOnDespawnedOrNull(TargetIndex.A);
			yield return toil;
			yield return Toils_Construct.UninstallIfMinifiable(TargetIndex.A).FailOnSomeonePhysicallyInteracting(TargetIndex.A);
			yield return Toils_Haul.StartCarryThing(TargetIndex.A);
			Toil toil2 = ToilMaker.MakeToil("MakeNewToils");
			toil2.initAction = delegate
			{
				comp.AddThing(Item);
			};
			toil2.defaultCompleteMode = ToilCompleteMode.Instant;
			yield return toil2;
		}
	}

	public class JobDriver_CollectorStealPawn : JobDriver
	{
		private CompCollector Comp => pawn.TryGetComp<CompCollector>();

		private int HypnotizeDurationTicks => (RevenantUtility.HypnotizeDurationSecondsFromNumColonistsCurve.Evaluate(RevenantUtility.NumSpawnedUnhypnotizedColonists(pawn.Map)) * 0.7f).SecondsToTicks();

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref hypnotizeEndTick, "hypnotizeEndTick", 0);
		}

		private int hypnotizeEndTick;

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			return true;
		}

		public override void Notify_Starting()
		{
			base.Notify_Starting();
			if (pawn.mindState.enemyTarget != null)
			{
				job.SetTarget(TargetIndex.A, pawn.mindState.enemyTarget);
			}
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnDespawnedOrNull(TargetIndex.A);
			Toil toil1 = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch).FailOnDespawnedOrNull(TargetIndex.A);
			toil1.tickIntervalAction = (Action<int>)Delegate.Combine(toil1.tickIntervalAction, (Action<int>)delegate (int delta)
			{
				Job curJob = toil1.actor.jobs.curJob;
				if (Rand.MTBEventOccurs(180f, 1f, delta))
				{
					curJob.SetTarget(TargetIndex.A, GetClosestTargetInRadius(pawn, 20f, true) ?? curJob.GetTarget(TargetIndex.A).Pawn);
					pawn.mindState.enemyTarget = curJob.GetTarget(TargetIndex.A).Pawn;
				}
				if (curJob.targetA == null)
				{
					EndJobWith(JobCondition.Incompletable);
				}
			});
			toil1.AddFinishAction(delegate
			{
				hypnotizeEndTick = Find.TickManager.TicksGame + HypnotizeDurationTicks;
				Find.Anomaly.Hypnotize(job.GetTarget(TargetIndex.A).Pawn, pawn, HypnotizeDurationTicks * 2);
			});
			yield return toil1;
			Toil toil2 = ToilMaker.MakeToil("MakeNewToils");
			toil2.FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
			toil2.initAction = (Action)Delegate.Combine(toil2.initAction, (Action)delegate
			{
				if (!Comp.Invisibility.PsychologicallyVisible)
				{
					Find.LetterStack.ReceiveLetter("NAT_LetterLabelCollectorRevealed".Translate(), "NAT_LetterCollectorRevealed".Translate(), LetterDefOf.ThreatBig, Comp.parent, null, null, null, null, 60);
				}
				Comp.Invisibility.BecomeVisible();
				Find.TickManager.slower.SignalForceNormalSpeed();
			});
			//toil2.AddEndCondition(() => (Find.TickManager.TicksGame < hypnotizeEndTick) ? JobCondition.Ongoing : JobCondition.Succeeded);
			toil2.defaultCompleteMode = ToilCompleteMode.Delay;
			toil2.defaultDuration = job.GetTarget(TargetIndex.A).Pawn.Downed ? 1 : HypnotizeDurationTicks;
			yield return toil2;
			Toil toil3 = Toils_Haul.StartCarryThing(TargetIndex.A);
			toil3.AddFinishAction(delegate
			{
				Pawn victim = job.GetTarget(TargetIndex.A).Pawn;
				Comp.state = CollectorState.Escape;
				if(pawn.carryTracker.CarriedThing == null)
                {
                    if (pawn.carryTracker.TryStartCarry(victim))
                    {
					}
				}
                Hediff h = victim.health.GetOrAddHediff(NATDefOf.NAT_CollectorHypnosis);
                h.Severity = 1f;
                Find.Anomaly.EndHypnotize(victim);
				//Find.TickManager.Pause();
			});
			yield return toil3;
		}

		public static Pawn GetClosestTargetInRadius(Pawn pawn, float radius, bool requireLoS = false)
		{
			List<Thing> list = pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Pawn);
			float num = float.MaxValue;
			Pawn result = null;
			foreach (Pawn item in list)
			{
				if (ValidTarget(item) && pawn.Position.InHorDistOf(item.Position, radius) && (float)item.Position.DistanceToSquared(pawn.Position) < num && (!requireLoS || GenSight.LineOfSightToThing(pawn.Position, item, pawn.Map)))
				{
					num = item.Position.DistanceToSquared(pawn.Position);
					result = item;
				}
			}
			return result;
		}

		public static bool ValidTarget(Pawn pawn)
		{
			if (pawn.RaceProps.Humanlike && pawn.Faction == Faction.OfPlayerSilentFail && !pawn.IsSubhuman && pawn.Spawned)
			{
				return true;
			}
			return false;
		}

		public override bool IsContinuation(Job j)
		{
			return job.GetTarget(TargetIndex.A) == j.GetTarget(TargetIndex.A);
		}

		public override void Notify_PatherFailed()
		{
			Pawn pawn = job.GetTarget(TargetIndex.A).Pawn;
			if (pawn != null)
			{
				Find.Anomaly.EndHypnotize(pawn);
			}
			job.SetTarget(TargetIndex.A, GetClosestTargetInRadius(base.pawn, 20f));
			base.pawn.mindState.enemyTarget = job.GetTarget(TargetIndex.A).Pawn;
			if (base.pawn.mindState.enemyTarget == null)
			{
				Comp.state = CollectorState.Escape;
			}
			base.Notify_PatherFailed();
		}
	}

	public class JobGiver_CollectorEscape : ThinkNode_JobGiver
	{
		protected override Job TryGiveJob(Pawn pawn)
		{
			IntVec3 intVec = IntVec3.Invalid;
			if (!RCellFinder.TryFindBestExitSpot(pawn, out intVec))
			{
				return null;
			}
			using (PawnPath pawnPath = pawn.Map.pathFinder.FindPathNow(pawn.Position, intVec, TraverseParms.For(pawn, Danger.Deadly, TraverseMode.PassDoors)))
			{
				if (!pawnPath.Found)
				{
					pawn.GetComp<CompCollector>().state = CollectorState.Attack;
					return null;
				}
			}
			Job job2 = JobMaker.MakeJob(NATDefOf.NAT_CollectorEscape, intVec);
			job2.locomotionUrgency = LocomotionUrgency.Sprint;
			job2.canBashDoors = true;
			job2.canBashFences = true;
			return job2;
		}
	}

	public class JobDriver_CollectorEscape : JobDriver
	{
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
			return true;
        }
        protected override IEnumerable<Toil> MakeNewToils()
		{
			Toil toil = Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
			toil.tickAction = delegate
			{
				if (base.Map.exitMapGrid.IsExitCell(pawn.Position))
				{
					Escape();
				}
			};
			yield return toil;
			Toil toil2 = ToilMaker.MakeToil("MakeNewToils");
			toil2.initAction = delegate
			{
				if (pawn.Position.OnEdge(pawn.Map) || pawn.Map.exitMapGrid.IsExitCell(pawn.Position))
				{
					Escape();
				}
			};
			toil2.defaultCompleteMode = ToilCompleteMode.Instant;
			yield return toil2;
		}

		public void Escape()
        {
			CompCollector comp = pawn.GetComp<CompCollector>();
			Pawn target = null;
			if (pawn.carryTracker?.CarriedThing != null)
			{
				target = pawn.carryTracker.CarriedThing as Pawn;
				if(target == null)
                {
					comp.innerContainer.TryAddOrTransfer(pawn.carryTracker.CarriedThing);
				}
			}
			comp.questPart.EscapeCollector(target);
			if(target != null)
            {
				Find.WorldPawns.PassToWorld(target, PawnDiscardDecideMode.KeepForever);
				pawn.carryTracker.innerContainer.Remove(target);
				if (target.Faction == Faction.OfPlayer)
				{
					PawnDiedOrDownedThoughtsUtility.TryGiveThoughts(target, null, PawnDiedOrDownedThoughtsKind.Lost);
					BillUtility.Notify_ColonistUnavailable(target);
					Find.LetterStack.ReceiveLetter("LetterLabelPawnsKidnapped".Translate(target.Named("PAWN")), "NAT_LetterPawnStealed".Translate(target.Named("PAWN")), LetterDefOf.NegativeEvent);
				}
				QuestUtility.SendQuestTargetSignals(target.questTags, "Kidnapped", target.Named("SUBJECT"), pawn.Named("KIDNAPPER"));
				Find.GameEnder.CheckOrUpdateGameOver();
                foreach (Hediff hediff in target.health.hediffSet.GetHediffsTendable())
                {
                    if (!hediff.IsTended())
                    {
                        hediff.Tended(new FloatRange(0.8f, 1.1f).RandomInRange, 1f);
                    }
                }
            }
			pawn.ExitMap(allowedToJoinOrCreateCaravan: false, CellRect.WholeMap(base.Map).GetClosestEdge(pawn.Position));
		}
	}

	public class JobGiver_CollectorWait : ThinkNode_JobGiver
	{
		public static float WanderDist = 10f;

		protected override Job TryGiveJob(Pawn pawn)
		{
			CellFinder.TryFindRandomReachableNearbyCell(pawn.Position, pawn.Map, WanderDist, TraverseParms.For(TraverseMode.PassDoors), (IntVec3 x) => x.Standable(pawn.Map), null, out var result);
			Job job = JobMaker.MakeJob(NATDefOf.NAT_CollectorWait, result);
			job.locomotionUrgency = LocomotionUrgency.Walk;
			return job;
		}
	}

	public class JobDriver_CollectorWait : JobDriver
	{
		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			return true;
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			Toil toil = Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
			yield return toil;
			Toil toil2 = ToilMaker.MakeToil("MakeNewToils");
			toil2.defaultCompleteMode = ToilCompleteMode.Instant;
			yield return toil2;
		}
	}

    public class ThinkNode_ConditionalNotHasJobDef : ThinkNode_Conditional
    {
		public JobDef def;
        protected override bool Satisfied(Pawn pawn)
        {
            if (pawn.CurJob != null && pawn.CurJob.def == def)
            {
                return false;
            }
			return true;
        }
    }
}