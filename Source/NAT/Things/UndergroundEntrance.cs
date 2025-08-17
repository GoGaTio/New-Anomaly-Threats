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
	[StaticConstructorOnStartup]
	public class UndergroundEntrance : MapPortal
	{
		public bool isSealed;

		public bool isOpened;

		private CompUndergroundEntrance compInt;

		public CompUndergroundEntrance Comp => compInt ?? (compInt = GetComp<CompUndergroundEntrance>());

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref isSealed, "isSealed", defaultValue: false);
			Scribe_Values.Look(ref isOpened, "isOpened", defaultValue: false);
		}

		public override void Print(SectionLayer layer)
		{
			if (isSealed)
			{
				Comp.Props.sealedGraphicData.Graphic.Print(layer, this, 0f);
			}
			else if (IsEnterable(out var _))
			{
				Comp.Props.openedGraphicData.Graphic.Print(layer, this, 0f);
			}
			else
			{
				Graphic.Print(layer, this, 0f);
			}
		}

		protected override IEnumerable<GenStepWithParams> GetExtraGenSteps()
		{
			var num = new GenStepWithParams(NATDefOf.NAT_UndergroundLayout, new GenStepParams
			{
				layout = Comp.Props.layout
			});
			yield return num;
		}

		public override bool IsEnterable(out string reason)
		{
			if (!isOpened)
			{
				reason = "Locked".Translate();
				return false;
			}
			if (isSealed)
			{
				reason = "Sealed".Translate();
				return false;
			}
			return base.IsEnterable(out reason);
		}

		public void Seal()
		{
            if (!Comp.Props.canBeSealed)
            {
				return;
            }
			if (!base.PocketMapExists)
			{
				Log.Error("Tried to seal ancient hatch but pocket map doesn't exist");
				return;
			}
			PocketMapUtility.DestroyPocketMap(pocketMap);
			DirtyMapMesh(base.Map);
			isSealed = true;
		}

		public void Open()
		{
			if (isOpened)
			{
				return;
			}
			DirtyMapMesh(base.Map);
			isOpened = true;
		}

		public override IEnumerable<Gizmo> GetGizmos()
		{
			foreach (Gizmo gizmo in base.GetGizmos())
			{
				yield return gizmo;
			}
			if (!base.PocketMapExists || !Comp.Props.canBeSealed)
			{
				yield break;
			}
			Command_Action command_Action = new Command_Action
			{
				defaultLabel = Comp.Props.sealGizmoLabel,
				defaultDesc = Comp.Props.sealGizmoDesc,
				icon = Comp.SealIcon,
				action = delegate
				{
					Find.Targeter.BeginTargeting(TargetingParameters.ForColonist(), delegate (LocalTargetInfo target)
					{
						Job job = JobMaker.MakeJob(NATDefOf.NAT_Seal, this);
						target.Pawn?.jobs.TryTakeOrderedJob(job, JobTag.Misc);
					}, delegate (LocalTargetInfo target)
					{
						Pawn pawn2 = target.Pawn;
						if (pawn2 != null && pawn2.IsColonistPlayerControlled)
						{
							GenDraw.DrawTargetHighlight(target);
						}
					}, (LocalTargetInfo target) => ValidateSealer(target).Accepted, null, null, Comp.SealIcon, playSoundOnAction: true, delegate (LocalTargetInfo target)
					{
						AcceptanceReport acceptanceReport = ValidateSealer(target);
						Pawn pawn = target.Pawn;
						if (pawn != null && pawn.IsColonistPlayerControlled && !acceptanceReport.Accepted)
						{
							if (!acceptanceReport.Reason.NullOrEmpty())
							{
								Widgets.MouseAttachedLabel(("CannotChooseSealer".Translate() + ": " + acceptanceReport.Reason.CapitalizeFirst()).Colorize(ColorLibrary.RedReadable));
							}
							else
							{
								Widgets.MouseAttachedLabel("CannotChooseSealer".Translate());
							}
						}
					});
				}
			};
            if (!base.PocketMap.mapPawns.PawnsInFaction(Faction.OfPlayer).NullOrEmpty())
            {
				command_Action.Disable("NAT_PlayerPawnsInPocketMap".Translate());
			}
			yield return command_Action;
		}

		private AcceptanceReport ValidateSealer(LocalTargetInfo target)
		{
			if (!(target.Thing is Pawn pawn))
			{
				return false;
			}
			if (!pawn.CanReach(this, PathEndMode.Touch, Danger.Deadly))
			{
				return "NoPath".Translate();
			}
			if (pawn.Downed)
			{
				return "DownedLower".Translate();
			}
			return true;
		}
	}
}