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
	public class CompProperties_RustedMassDeactivation : CompProperties_Interactable
	{
		public int shardsRequired = 1;

		public CompProperties_RustedMassDeactivation()
		{
			compClass = typeof(CompRustedMassDeactivation);
		}
	}
	public class CompRustedMassDeactivation : CompInteractable
	{
		private new CompProperties_RustedMassDeactivation Props => (CompProperties_RustedMassDeactivation)props;

		private CompStudyUnlocks studyComp;

		private CompStudyUnlocks StudyComp => studyComp ?? (studyComp = parent.GetComp<CompStudyUnlocks>());

		public override AcceptanceReport CanInteract(Pawn activateBy = null, bool checkOptionalItems = true)
		{
			if (!StudyComp.Completed)
			{
				return false;
			}
			if (activateBy != null)
			{
				if (checkOptionalItems && !activateBy.HasReserved(ThingDefOf.Shard) && !ReservationUtility.ExistsUnreservedAmountOfDef(parent.MapHeld, ThingDefOf.Shard, Faction.OfPlayer, Props.shardsRequired, (Thing t) => activateBy.CanReserveAndReach(t, PathEndMode.Touch, Danger.None)))
				{
					return "NAT_RustedSphereDeactivateMissingShards".Translate(Props.shardsRequired);
				}
			}
			else if (checkOptionalItems && !ReservationUtility.ExistsUnreservedAmountOfDef(parent.MapHeld, ThingDefOf.Shard, Faction.OfPlayer, Props.shardsRequired))
			{
				return "NAT_RustedSphereDeactivateMissingShards".Translate(Props.shardsRequired);
			}
			return base.CanInteract(activateBy, checkOptionalItems);
		}

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			if (!StudyComp.Completed)
			{
				yield break;
			}
			foreach (Gizmo item in base.CompGetGizmosExtra())
			{
				yield return item;
			}
		}

		public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
		{
			if (!StudyComp.Completed)
			{
				yield break;
			}
			foreach (FloatMenuOption item in base.CompFloatMenuOptions(selPawn))
			{
				yield return item;
			}
		}

		public override void OrderForceTarget(LocalTargetInfo target)
		{
			if (ValidateTarget(target, showMessages: false))
			{
				OrderDeactivation(target.Pawn);
			}
		}

		protected override void OnInteracted(Pawn caster)
		{
			parent.TryGetComp<CompActivity>().Deactivate();
			caster.infectionVectors.AddInfectionVector(DefDatabase<InfectionPathwayDef>.GetNamed("EntityAttacked"), parent as Pawn);
			parent.TryGetComp<CompRustedMass>().passive = true;
			parent.Kill();
		}

		private void OrderDeactivation(Pawn pawn)
		{
			if (pawn.TryFindReserveAndReachableOfDef(ThingDefOf.Shard, out var thing))
			{
				Job job = JobMaker.MakeJob(JobDefOf.InteractThing, parent, thing);
				job.count = 1;
				job.playerForced = true;
				pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
			}
		}
	}
}