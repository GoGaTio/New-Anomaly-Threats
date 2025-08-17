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
	public class PsychicRitualDef_CreateRust : PsychicRitualDef_InvocationCircle
	{
		public SimpleCurve successChanceFromQualityCurve;

		public int bioferriteCount;

		public PawnKindDef rustKind;
		public override List<PsychicRitualToil> CreateToils(PsychicRitual psychicRitual, PsychicRitualGraph parent)
		{
			List<PsychicRitualToil> list = base.CreateToils(psychicRitual, parent);
			list.Add(new PsychicRitualToil_ActivateRust(InvokerRole));
			return list;
		}

		public override TaggedString OutcomeDescription(FloatRange qualityRange, string qualityNumber, PsychicRitualRoleAssignments assignments)
		{
			string text = successChanceFromQualityCurve.Evaluate(qualityRange.min).ToStringPercent();
			return outcomeDescription.Formatted(text);
		}

        public override IEnumerable<string> BlockingIssues(PsychicRitualRoleAssignments assignments, Map map)
        {
			foreach (string item in base.BlockingIssues(assignments, map))
			{
				yield return item;
			}
			List<Pawn> tmpGatheringPawns = new List<Pawn>(8);
			foreach (var (psychicRitualRoleDef, collection) in assignments.RoleAssignments)
			{
				if (psychicRitualRoleDef.CanHandleOfferings)
				{
					tmpGatheringPawns.AddRange(collection);
				}
			}
			tmpGatheringPawns.RemoveAll(map, (Map _map, Pawn _pawn) => _pawn.MapHeld != _map);
			IngredientCount ingredients = new IngredientCount();
			ingredients.SetBaseCount(bioferriteCount);
			ingredients.filter.SetDisallowAll();
			ingredients.filter.SetAllow(ThingDefOf.Bioferrite, true);
			if (RequiredOffering != null && !PsychicRitualDef.OfferingReachable(map, tmpGatheringPawns, ingredients, out var reachableCount))
			{
				yield return "PsychicRitualOfferingsInsufficient".Translate(ingredients.SummaryFilterFirst, reachableCount);
			}
		}

        public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
        {
			foreach(StatDrawEntry item in base.SpecialDisplayStats(req))
            {
				if(item.LabelCap == "StatsReport_Offering".Translate().CapitalizeFirst())
                {
					yield return new StatDrawEntry(StatCategoryDefOf.PsychicRituals, "StatsReport_Offering".Translate(), ThingDefOf.Bioferrite.LabelCap + " x" + bioferriteCount + "\n" + item.ValueString, "StatsReport_Offering_Desc".Translate(), 1000);
				}
                else
                {
					yield return item;
				}
            }
        }

		public override TaggedString TimeAndOfferingLabel()
		{
			return base.TimeAndOfferingLabel() + ", " + ThingDefOf.Bioferrite.LabelCap + " x" + bioferriteCount;
		}
	}

	public class PsychicRitualToil_ActivateRust : PsychicRitualToil
	{

		public PsychicRitualRoleDef invokerRole;

		protected PsychicRitualToil_ActivateRust()
		{
		}

		public PsychicRitualToil_ActivateRust(PsychicRitualRoleDef invokerRole)
		{
			this.invokerRole = invokerRole;
		}

		public override void Start(PsychicRitual psychicRitual, PsychicRitualGraph parent)
		{
			Pawn pawn = psychicRitual.assignments.FirstAssignedPawn(invokerRole);
			if (pawn != null)
			{
				ApplyOutcome(psychicRitual, pawn);
			}
		}

		private void ApplyOutcome(PsychicRitual psychicRitual, Pawn invoker)
		{
			float chance = ((PsychicRitualDef_CreateRust)psychicRitual.def).successChanceFromQualityCurve.Evaluate(psychicRitual.PowerPercent);
			List<Thing> list = new List<Thing>();
			int num = 0;
			foreach (Thing t in psychicRitual.assignments.Target.Thing.OccupiedRect().ExpandedBy(1).Cells.Select((IntVec3 c) => c.GetFirstThing(psychicRitual.Map, ThingDefOf.Bioferrite)))
            {
				if(t != null)
                {
					list.Add(t);
					num += t.stackCount;
				}
            }
			if (num < ((PsychicRitualDef_CreateRust)psychicRitual.def).bioferriteCount)
            {
				psychicRitual.CancelPsychicRitual("NAT_PsychicRitual_NotEnoughBioferrite".Translate());
			}
            if (Rand.Chance(chance))
            {
				RemoveItem(list, ((PsychicRitualDef_CreateRust)psychicRitual.def).bioferriteCount);
				RustedPawn rust = CreateRust(((PsychicRitualDef_CreateRust)psychicRitual.def).rustKind, invoker.Faction);
				GenSpawn.Spawn(rust, psychicRitual.assignments.Target.Thing.Position, psychicRitual.Map);
				TaggedString text1 = "NAT_CreateRustCompleteText_Success".Translate(invoker.Named("INVOKER"), psychicRitual.def.Named("RITUAL"), rust.Named("RUST"));
				Find.LetterStack.ReceiveLetter("PsychicRitualCompleteLabel".Translate(psychicRitual.def.label), text1, LetterDefOf.PositiveEvent, new LookTargets(rust));
			}
            else
            {
				RemoveItem(list, Mathf.RoundToInt(((float)((PsychicRitualDef_CreateRust)psychicRitual.def).bioferriteCount) * Rand.Range(0.1f, 0.5f)));
				TaggedString text2 = "NAT_CreateRustCompleteText_Fail".Translate(invoker.Named("INVOKER"), psychicRitual.def.Named("RITUAL"));
				Find.LetterStack.ReceiveLetter("PsychicRitualCompleteLabel".Translate(psychicRitual.def.label), text2, LetterDefOf.ThreatSmall);
				Find.PsychicRitualManager.ClearCooldown(psychicRitual.def);
			}
		}

		public static void RemoveItem(List<Thing> items, int count)
        {
			int num = 0;
			while(num < count)
            {
				Thing t = items.RandomElement();
				if(count - num >= t.stackCount)
                {
					num += t.stackCount;
					t.Destroy();
					items.Remove(t);
				}
                else
                {
					t.SplitOff(count - num).Destroy();
					num += count - num;
				}
            }
        }

		public static RustedPawn CreateRust(PawnKindDef kind, Faction faction)
		{
			RustedPawn rust = (RustedPawn)PawnGenerator.GeneratePawn(kind, faction);
			rust.equipment.DestroyAllEquipment();
			rust.inventory.DestroyAll();
			rust.apparel.DestroyAll();
			return rust;
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Defs.Look(ref invokerRole, "invokerRole");
		}
	}

	public class PsychicRitualToil_CreateRust : PsychicRitualToil
	{
		protected PsychicRitualRoleDef gathererRole;

		public bool rustCreated;

		private PsychicRitualDef_CreateRust def;

		public static bool rustCreatedFlag;

		protected PsychicRitualToil_CreateRust()
		{
			rustCreatedFlag = false;
		}

		public PsychicRitualToil_CreateRust(PsychicRitualRoleDef offeringGatherer, PsychicRitualDef_CreateRust def)
		{
			gathererRole = offeringGatherer;
			this.def = def;
			rustCreatedFlag = false;
		}

		public override void UpdateAllDuties(PsychicRitual psychicRitual, PsychicRitualGraph parent)
		{
			foreach (Pawn item in psychicRitual.assignments.AssignedPawns(gathererRole))
			{
				SetPawnDuty(item, psychicRitual, parent, (!rustCreated && psychicRitual.assignments.RoleForPawn(item) == PsychicRitualRoleDefOf.Invoker) ? NATDefOf.NAT_CreateRustForPsychicRitual : DutyDefOf.Idle);
			}
		}

		public override bool Tick(PsychicRitual psychicRitual, PsychicRitualGraph parent)
		{
			if (rustCreatedFlag)
			{
				rustCreated = true;
				rustCreatedFlag = false;
			}
			return rustCreated;
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Defs.Look(ref gathererRole, "gathererRole");
			Scribe_Values.Look(ref rustCreated, "rustCreated", defaultValue: false);
			Scribe_Defs.Look(ref def, "def");
		}

		public override void Notify_PawnJobDone(PsychicRitual psychicRitual, PsychicRitualGraph parent, Pawn pawn, Job job, JobCondition condition)
		{
			base.Notify_PawnJobDone(psychicRitual, parent, pawn, job, condition);
			if (psychicRitual.assignments.RoleForPawn(pawn) == gathererRole && rustCreated)
			{
				SetPawnDuty(pawn, psychicRitual, parent, DutyDefOf.Idle);
			}
		}

		public override string GetJobReport(PsychicRitual psychicRitual, PsychicRitualGraph parent, Pawn pawn)
		{
			if (psychicRitual.assignments.RoleForPawn(pawn) == gathererRole)
			{
				return "PsychicRitualToil_GatherOfferings_JobReport".Translate();
			}
			return base.GetJobReport(psychicRitual, parent, pawn);
		}
	}

	public class JobGiver_CreateRustForPsychicRitual : ThinkNode_JobGiver
	{
		protected override Job TryGiveJob(Pawn pawn)
		{
			Lord lord;
			if ((lord = pawn.GetLord()) == null)
			{
				return null;
			}
			if (!(lord.CurLordToil is LordToil_PsychicRitual lordToil_PsychicRitual))
			{
				return null;
			}
			PsychicRitualDef_CreateRust ritualDef;
			if ((ritualDef = lordToil_PsychicRitual.RitualData.psychicRitual.def as PsychicRitualDef_CreateRust) == null)
			{
				return null;
			}
			if (ritualDef.bioferriteCount < 1)
			{
				return null;
			}
			PsychicRitual psychicRitual = lordToil_PsychicRitual.RitualData.psychicRitual;
			PsychicRitualRoleDef role;
			if ((role = psychicRitual.assignments.RoleForPawn(pawn)) == null)
			{
				return null;
			}
			if(role != PsychicRitualRoleDefOf.Invoker)
            {
				return null;
            }
			if (lordToil_PsychicRitual.RitualData.psychicRitual.assignments.Target.Thing.OccupiedRect().ExpandedBy(2).Cells.Sum((IntVec3 c) => c.GetFirstThing(pawn.Map, ThingDefOf.Bioferrite)?.stackCount ?? 0) >= ritualDef.bioferriteCount)
			{
				PsychicRitualToil_CreateRust.rustCreatedFlag = true;
			}
			Thing thing = GenClosest.ClosestThingReachable(pawn.PositionHeld, pawn.MapHeld, ThingRequest.ForGroup(ThingRequestGroup.HaulableAlways), PathEndMode.Touch, TraverseParms.For(pawn), 9999f, delegate (Thing thing2)
			{
				if (thing2.def != ThingDefOf.Bioferrite)
				{
					return false;
				}
				if (thing2.IsForbidden(pawn))
				{
					return false;
				}
				return pawn.CanReserve(thing2, 10, Mathf.Min(ritualDef.bioferriteCount, thing2.stackCount)) ? true : false;
			});
			if (thing == null)
			{
				TaggedString reason = "PsychicRitualToil_GatherOfferings_OfferingUnavailable".Translate(pawn.Named("PAWN"), ThingDefOf.Bioferrite.LabelCap + " x" + ritualDef.bioferriteCount);
				psychicRitual.LeaveOrCancelPsychicRitual(role, pawn, reason);
				return null;
			}
			LocalTargetInfo value = (LocalTargetInfo)psychicRitual.assignments.Target;
			Job job = JobMaker.MakeJob(NATDefOf.NAT_BuildRust, thing, value.HasThing ? value.Thing.PositionHeld : value.Cell);
			job.count = Mathf.Min(ritualDef.bioferriteCount, thing.stackCount);
			return job;
		}
	}

	public class JobDriver_BuildRust : JobDriver
	{
		private Thing Item => job.GetTarget(TargetIndex.A).Thing;

		private IntVec3 Place => (IntVec3)job.GetTarget(TargetIndex.B).Cell;

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			return pawn.Reserve(Item, job, 1, job.count, null, errorOnFailed);
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch);
			yield return Toils_Haul.StartCarryThing(TargetIndex.A);
			yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.Touch);
			Toil doWork = ToilMaker.MakeToil("MakeNewToils");
			doWork.initAction = delegate
			{
				doWork.actor.carryTracker.TryDropCarriedThing(Place, ThingPlaceMode.Near, out var thing);
				thing.SetForbidden(true);
			};
			yield return doWork;
		}
	}
}