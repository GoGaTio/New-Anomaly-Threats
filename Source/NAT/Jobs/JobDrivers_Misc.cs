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
	public class JobDriver_Seal : JobDriver
	{
		protected UndergroundEntrance Entrance => job.GetTarget(TargetIndex.A).Thing as UndergroundEntrance;

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			return pawn.Reserve(job.GetTarget(TargetIndex.A), job, 1, -1, null, errorOnFailed);
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
			yield return Toils_Goto.GotoThing(TargetIndex.A, Entrance.def.hasInteractionCell ? PathEndMode.InteractionCell : PathEndMode.Touch);
			yield return Toils_General.WaitWith(TargetIndex.A, Entrance.Comp.Props.sealTicks, useProgressBar: true).WithEffect(() => Entrance.Comp.Props.sealEffect, TargetIndex.A);
			Toil toil = ToilMaker.MakeToil("MakeNewToils");
			toil.initAction = delegate
			{
				Entrance.Seal();
			};
			yield return toil;
		}

		public override string GetReport()
		{
			
			if (string.IsNullOrEmpty(Entrance.Comp.Props.sealJobReportOverride))
			{
				return base.GetReport();
			}
			return Entrance.Comp.Props.sealJobReportOverride.Formatted(Entrance.LabelShort);
		}
	}

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
			if (job.GetTarget(TargetIndex.B).Thing != null && job.GetTarget(TargetIndex.B).Thing is RustedPawn rust && rust != pawn as RustedPawn)
			{
				targetsAnotherPawn = true;
			}
		}

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			if (job.GetTarget(TargetIndex.B).Thing != null)
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
			if (pawn is RustedPawn rust && rust.Controllable)
			{
				foreach (Toil item in PrepareToUseToils())
				{
					yield return item;
				}
				Toil toil1 = (targetsAnotherPawn ? Toils_General.WaitWith(TargetIndex.B, useDuration, maintainPosture: true, maintainSleep: true) : Toils_General.Wait(useDuration, TargetIndex.A));
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
}