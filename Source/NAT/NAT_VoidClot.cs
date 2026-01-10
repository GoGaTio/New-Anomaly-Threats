using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using RimWorld.BaseGen;
using RimWorld.IO;
using RimWorld.Planet;
using RimWorld.QuestGen;
using RimWorld.SketchGen;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Grammar;
using Verse.Noise;
using Verse.Profile;
using Verse.Sound;
using Verse.Steam;
using UnityEngine;
using System.Diagnostics;

namespace NAT
{
	

	public class JobDriver_UseItemByRust : JobDriver
	{
		private int useDuration = -1;

		private bool usingFromInventory;

		private bool targetsAnotherPawn;

		private Thing Item => job.GetTarget(TargetIndex.A).Thing;

		private RustedPawn Target => job.GetTarget(TargetIndex.B).Thing as RustedPawn;

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref useDuration, "useDuration", 0);
			Scribe_Values.Look(ref targetsAnotherPawn, "targetsAnotherPawn", defaultValue: false);
			Scribe_Values.Look(ref usingFromInventory, "usingFromInventory", defaultValue: false);
		}

		public override void Notify_Starting()
		{
			base.Notify_Starting();
			useDuration = job.GetTarget(TargetIndex.A).Thing.TryGetComp<CompUsableByRust>().Props.useDuration;
			job.count = 1;
			usingFromInventory = pawn.inventory != null && pawn.inventory.Contains(Item);
			if(job.GetTarget(TargetIndex.B).Thing != null && job.GetTarget(TargetIndex.B).Thing is RustedPawn rust && rust != pawn as RustedPawn)
            {
				targetsAnotherPawn = true;
			}
		}

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			if(job.GetTarget(TargetIndex.B).Thing != null)
            {
				if (!pawn.Reserve(Target, job, 1, 1, null, errorOnFailed))
				{
					return false;
				}
			}
			else if (!pawn.Reserve(Item, job, 10, 1, null, errorOnFailed))
			{
				return false;
			}
			return true;
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			
			this.FailOnIncapable(PawnCapacityDefOf.Manipulation);
			if(pawn is RustedPawn rust && rust.Controllable)
            {
				foreach (Toil item in PrepareToUseToils())
				{
					yield return item;
				}
				Toil toil1 = (targetsAnotherPawn ? Toils_General.WaitWith(TargetIndex.B, useDuration,  maintainPosture: true, maintainSleep: true) : Toils_General.Wait(useDuration, TargetIndex.A));
				toil1.WithProgressBarToilDelay(targetsAnotherPawn ? TargetIndex.B : TargetIndex.A);
				toil1.handlingFacing = true;
				toil1.tickAction = delegate
				{
					if (targetsAnotherPawn)
					{
						pawn.rotationTracker.FaceTarget(Target);
					}
				};
				yield return toil1;
				Toil use = ToilMaker.MakeToil("Use");
				use.initAction = delegate
				{
					CompUsableByRust comp = Item.TryGetComp<CompUsableByRust>();
					comp.UsedBy(targetsAnotherPawn ? Target : rust);
				};
				use.defaultCompleteMode = ToilCompleteMode.Instant;
				yield return use;
            }
            else
            {
				this.FailOn(() => true);
			}
		}

		private IEnumerable<Toil> PrepareToUseToils()
		{
			if (usingFromInventory)
			{
				yield return Toils_Misc.TakeItemFromInventoryToCarrier(pawn, TargetIndex.A);
			}
			else
			{
				yield return ReserveItem();
				yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(TargetIndex.A);
				Toil toil = ToilMaker.MakeToil("PickupItem");
				toil.initAction = delegate
				{
					Pawn actor = toil.actor;
					Job curJob = actor.jobs.curJob;
					Thing thing = Item;
					actor.carryTracker.TryStartCarry(thing, 1);
					if (thing != actor.carryTracker.CarriedThing && actor.Map.reservationManager.ReservedBy(thing, actor, curJob))
					{
						actor.Map.reservationManager.Release(thing, actor, curJob);
					}
					actor.jobs.curJob.targetA = actor.carryTracker.CarriedThing;
				};
				toil.defaultCompleteMode = ToilCompleteMode.Instant;
				yield return toil;
			}
            if (targetsAnotherPawn)
            {
				yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch).FailOnDespawnedOrNull(TargetIndex.B);
			}
		}

		private Toil ReserveItem()
		{
			Toil toil = ToilMaker.MakeToil("ReserveItem");
			toil.initAction = delegate
			{
				if (pawn.Faction != null)
				{
					Thing thing = job.GetTarget(TargetIndex.A).Thing;
					if (pawn.carryTracker.CarriedThing != thing)
					{
						if (!pawn.Reserve(thing, job, 10, 1))
						{
							Log.Error(string.Concat("NAT RustedPawn usable reservation for ", pawn, " on job ", this, " failed, because it could not register item from ", thing));
							pawn.jobs.EndCurrentJob(JobCondition.Errored);
						}
						job.count = 1;
					}
				}
			};
			toil.defaultCompleteMode = ToilCompleteMode.Instant;
			toil.atomicWithPrevious = true;
			return toil;
		}
	}
	public class ScenPart_StartingRustedSoldier : ScenPart
	{
		private PawnKindDef pawnKind;

		private IEnumerable<PawnKindDef> PossibleRusts => DefDatabase<PawnKindDef>.AllDefs.Where((PawnKindDef td) => td.GetModExtension<RustedPawnExtention>()?.scenarioAvailable == true);

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Defs.Look(ref pawnKind, "pawnKind");
		}

		public override void DoEditInterface(Listing_ScenEdit listing)
		{
			Rect scenPartRect = listing.GetScenPartRect(this, 2f * ScenPart.RowHeight + 4f);
			if (Widgets.ButtonText(new Rect(scenPartRect.xMin, scenPartRect.yMin, scenPartRect.width, ScenPart.RowHeight), (pawnKind != null) ? pawnKind.LabelCap : "Random".Translate()))
			{
				List<FloatMenuOption> list = new List<FloatMenuOption>();
				list.Add(new FloatMenuOption("Random".Translate().CapitalizeFirst(), delegate
				{
					pawnKind = null;
				}));
				foreach (PawnKindDef possibleMech in PossibleRusts)
				{
					PawnKindDef localKind = possibleMech;
					list.Add(new FloatMenuOption(localKind.LabelCap, delegate
					{
						pawnKind = localKind;
					}));
				}
				Find.WindowStack.Add(new FloatMenu(list));
			}
		}

		public override void Randomize()
		{
			pawnKind = PossibleRusts.RandomElement();
		}

		public override string Summary(Scenario scen)
		{
			return ScenSummaryList.SummaryWithList(scen, "PlayerStartsWith", ScenPart_StartingThing_Defined.PlayerStartWithIntro);
		}

		public override IEnumerable<string> GetSummaryListEntries(string tag)
		{
			if (tag == "PlayerStartsWith")
			{
				yield return "NAT_RustedSoldier".Translate().CapitalizeFirst() + ": " + pawnKind.LabelCap;
			}
		}

		public override IEnumerable<Thing> PlayerStartingThings()
		{
			if (pawnKind == null)
			{
				pawnKind = PossibleRusts.RandomElement();
			}
			//PawnGenerationRequest request = new PawnGenerationRequest(pawnKind, Faction.OfPlayer, PawnGenerationContext.NonPlayer, -1, forceGenerateNewPawn: false, allowDead: false, allowDowned: false, canGeneratePawnRelations: true, mustBeCapableOfViolence: false, 1f, forceAddFreeWarmLayerIfNeeded: false, allowGay: true, allowPregnant: false, allowFood: true, allowAddictions: true, inhabitant: false, certainlyBeenInCryptosleep: false, forceRedressWorldPawnIfFormerColonist: false, worldPawnFactionDoesntMatter: false, 0f, 0f, null, 1f, null, null, null, null, null, null, null, null, null, null, null, null, forceNoIdeo: false, forceNoBackstory: false, forbidAnyTitle: false, forceDead: false, null, null, null, null, null, 0f, DevelopmentalStage.Newborn);
			Pawn pawn = PawnGenerator.GeneratePawn(pawnKind, Faction.OfPlayer);
			yield return pawn;
		}

		public override int GetHashCode()
		{
			return base.GetHashCode() ^ ((pawnKind != null) ? pawnKind.GetHashCode() : 0);
		}
	}
	public class NewAnomalyThreatsSettings : ModSettings
    {

		public bool rustedSoldierName_Draft = true;
		public bool rustedSoldierName_NoDraft = true;
		public bool rustedSoldierWeaponChange = true;
		public bool rustedSoldierDeathNotification = true;
		public bool allowEndGameRaid = true;

		public override void ExposeData()
		{
			Scribe_Values.Look(ref rustedSoldierName_Draft, "rustedSoldierName_Draft", true);
			Scribe_Values.Look(ref rustedSoldierName_NoDraft, "rustedSoldierName_Draft", true);
			Scribe_Values.Look(ref rustedSoldierWeaponChange, "rustedSoldierWeaponChange", true);
			Scribe_Values.Look(ref rustedSoldierDeathNotification, "rustedSoldierDeathNotification", true);
			Scribe_Values.Look(ref allowEndGameRaid, "allowEndGameRaid", true);
			base.ExposeData();
		}
	}

	public class NewAnomalyThreatsMod : Mod
	{

		NewAnomalyThreatsSettings settings;

		public NewAnomalyThreatsMod(ModContentPack content) : base(content)
		{
			this.settings = GetSettings<NewAnomalyThreatsSettings>();
		}

		public override void DoSettingsWindowContents(Rect inRect)
		{
			Listing_Standard listingStandard = new Listing_Standard();
			listingStandard.Begin(inRect);
			//listingStandard.CheckboxLabeled("NAT_Setting_NameDraft".Translate(), ref settings.rustedSoldierName_Draft, "NAT_Setting_NameDraft_Desc".Translate());
			//listingStandard.CheckboxLabeled("NAT_Setting_NameNoDraft".Translate(), ref settings.rustedSoldierName_NoDraft, "NAT_Setting_NameNoDraft_Desc".Translate());
			//listingStandard.CheckboxLabeled("NAT_Setting_WeaponChange".Translate(), ref settings.rustedSoldierWeaponChange, "NAT_Setting_WeaponChange_Desc".Translate());
			//listingStandard.CheckboxLabeled("NAT_Setting_DeathNotification".Translate(), ref settings.rustedSoldierDeathNotification, "NAT_Setting_DeathNotification_Desc".Translate());
			listingStandard.CheckboxLabeled("NAT_Setting_AllowRaid".Translate(), ref settings.allowEndGameRaid, "NAT_Setting_AllowRaid_Desc".Translate());
			listingStandard.End();
			base.DoSettingsWindowContents(inRect);
		}
		public override string SettingsCategory()
		{
			return "New Anomaly Threats";
		}
	}

	public class JobGiver_ExtinguishSelfImmediately : ThinkNode_JobGiver
	{
		protected override Job TryGiveJob(Pawn pawn)
		{
			Fire fire = (Fire)pawn.GetAttachment(ThingDefOf.Fire);
			if (fire != null)
			{
				return JobMaker.MakeJob(JobDefOf.ExtinguishSelf, fire);
			}
			return null;
		}
	}
}
