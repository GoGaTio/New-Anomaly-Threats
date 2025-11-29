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
	[HarmonyPatch(typeof(Pawn_WorkSettings), "EnableAndInitialize")]
	public static class Patch_EnableAndInitialize
	{
		[HarmonyPostfix]
		public static void Postfix(Pawn_WorkSettings __instance, Pawn ___pawn)
		{
			if (___pawn is RustedPawn rust && rust.Worker?.CanWork == true)
			{
				foreach (WorkTypeDef item in rust.Worker.AvailableWorkTypes)
                {
					__instance.SetPriority(item, 3);
				}
			}
		}
	}

	[HarmonyPatch(typeof(PawnComponentsUtility), "AddAndRemoveDynamicComponents")]
	public class Patch_DraftedRustedSoldiers
	{
		[HarmonyPostfix]
		public static void Postfix(Pawn pawn, bool actAsIfSpawned)
		{
			if (pawn.Faction == Faction.OfPlayerSilentFail && pawn is RustedPawn rust)
			{
                if (pawn.drafter == null && (actAsIfSpawned || pawn.Spawned) && rust.Draftable)
                {
					pawn.drafter = new Pawn_DraftController(pawn);
				}
				if (rust.Worker != null && pawn.workSettings == null)
				{
					pawn.workSettings = new Pawn_WorkSettings(pawn);
				}
				if (pawn.abilities == null)
				{
					pawn.abilities = new Pawn_AbilityTracker(pawn);
				}
			}
		}
	}

	[HarmonyPatch(typeof(JobGiver_DropUnusedInventory), nameof(JobGiver_DropUnusedInventory.ShouldKeepDrugInInventory))]
	public class Patch_ShouldKeepDrugInInventory
	{
		[HarmonyPostfix]
		public static void Postfix(Pawn pawn, Thing drug, ref bool __result)
		{
			if (__result) return;
			if (drug.HasComp<CompUsableByRust>())
			{
				__result = true;
			}
		}
	}

	[HarmonyPatch(typeof(PawnUtility), nameof(PawnUtility.CanPickUp))]
	public class Patch_CanPickUp
	{
		[HarmonyPostfix]
		public static void Postfix(Pawn pawn, ThingDef thingDef, ref bool __result)
		{
			if (__result) return;
			if (thingDef.HasComp<CompUsableByRust>())
			{
				__result = true;
			}
		}
	}


	[HarmonyPatch(typeof(JobGiver_Work), "PawnCanUseWorkGiver")]
	public class Patch_PawnCanUseWorkGiver
	{
		[HarmonyPostfix]
		public static void Postfix(Pawn pawn, WorkGiver giver, ref bool __result)
		{
			if (pawn is RustedPawn rust && CanUseWorkGiver(rust, giver))
			{
				__result = true;
			}
		}

		private static bool CanUseWorkGiver(RustedPawn rust, WorkGiver giver)
		{
			if (giver.def.workType != null && rust.WorkTypeIsDisabled(giver.def.workType))
			{
				return false;
			}
			if (giver.ShouldSkip(rust))
			{
				return false;
			}
			if (giver.MissingRequiredCapacity(rust) != null)
			{
				return false;
			}
			if (rust.Worker.Props.forceAllowWorkGivers.Contains(giver.def))
			{
				return true;
			}
			if (giver.def.canBeDoneByMechs || giver.def.nonColonistsCanDo)
			{
				return true;
			}
			return false;
		}
	}

	/*[HarmonyPatch(typeof(FloatMenuMakerMap))]
	[HarmonyPatch("AddJobGiverWorkOrders")]
	public static class Patch_RemoveWorkFromSoldiers
	{
		[HarmonyPrefix]
		public static bool Prefix(Pawn pawn)
		{
			if (pawn.Faction == Faction.OfPlayerSilentFail && pawn is RustedPawn rust)
			{
                if (rust.Controllable)
                {
					return true;
                }
				return false;
			}
			return true;
		}
	}*/

	[HarmonyPatch(typeof(Pawn))]
	[HarmonyPatch("GetReasonsForDisabledWorkType")]
	public static class Patch_GetReasonsForDisabledWorkType
	{
		[HarmonyPrefix]
		public static bool Prefix(WorkTypeDef workType, ref List<string> __result, Pawn __instance)
		{
			if (__instance is RustedPawn rust)
			{
				__result = new List<string>();
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(Pawn))]
	[HarmonyPatch(nameof(Pawn.GetDisabledWorkTypes))]
	public static class Patch_DisableWorkTypes
	{
		[HarmonyPrefix]
		public static bool Prefix(ref List<WorkTypeDef> __result, Pawn __instance)
		{
			if (__instance is RustedPawn rust)
			{
				__result = rust.Worker?.DisabledWorkTypes ?? CompProperties_RustedWorker.WorkTypes;
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(SkillRecord))]
	public static class Patch_Skills_Intreval
	{
		[HarmonyPrefix]
		[HarmonyPriority(int.MaxValue)]
		[HarmonyPatch(nameof(SkillRecord.Interval))]
		public static bool Interval(Pawn ___pawn)
		{
			if (___pawn is RustedPawn)
			{
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(SkillRecord))]
	public static class Patch_Skills_Learn
	{
		[HarmonyPrefix]
		[HarmonyPriority(int.MaxValue)]
		[HarmonyPatch(nameof(SkillRecord.Learn))]
		public static bool Learn(Pawn ___pawn)
		{
			if (___pawn is RustedPawn)
			{
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch]
	public static class DestroyingROMShittyPatch
	{
		public static MethodBase TargetMethod()
		{
			Type type = AccessTools.TypeByName("TorannMagic.TorannMagicMod");
			if(type == null)
			{
				return null;
			}
			MethodInfo method = AccessTools.Method(AccessTools.Inner(type, "Pawn_SkillTracker_Base_Patch"), "Prefix");
			return method;
		}

		public static bool Prepare(MethodBase method)
		{
			Type type = AccessTools.TypeByName("TorannMagic.TorannMagicMod");
			if (type == null)
			{
				return false;
			}
			MethodInfo m = AccessTools.Method(AccessTools.Inner(type, "Pawn_SkillTracker_Base_Patch"), "Prefix");
			if (m == null)
			{
				return false;
			}
			return true;
		}

		[HarmonyPrefix]
		[HarmonyPriority(int.MaxValue)]
		public static bool Prefix(ref bool __result)
		{
			__result = true;
			return false;
		}
	}

	[HarmonyPatch(typeof(PawnColumnDefGenerator), "ImpliedPawnColumnDefs")]
	public static class Patch_ImpliedPawnColumnDefs
	{
		public static IEnumerable<WorkTypeDef> Defs()
        {
			List<ThingDef> list = DefDatabase<ThingDef>.AllDefs.Where((ThingDef d) => d.GetCompProperties<CompProperties_RustedWorker>() != null).ToList();
			List<WorkTypeDef> list2 = new List<WorkTypeDef>();
			foreach(ThingDef def in list)
            {
				CompProperties_RustedWorker comp = def.GetCompProperties<CompProperties_RustedWorker>();
				foreach(WorkTypeDef w in comp.AvailableWorkTypes)
                {
                    if (!list2.Contains(w))
                    {
						list2.Add(w);
						yield return w;
                    }
                }
			}
        }
		public static IEnumerable<PawnColumnDef> Postfix(IEnumerable<PawnColumnDef> __result, bool hotReload)
		{
			foreach (PawnColumnDef item in __result)
			{
				yield return item;
			}
			PawnTableDef workTable = NATDefOf.NAT_Rusts;
			bool moveWorkTypeLabelDown = false;
			int num = 1;
			foreach (WorkTypeDef item2 in WorkTypeDefsUtility.WorkTypeDefsInPriorityOrder.Where((WorkTypeDef d) => d.visible && Defs().Contains(d)))
			{
				moveWorkTypeLabelDown = !moveWorkTypeLabelDown;
				string defName2 = "NAT_WorkPriority_" + item2.defName;
				PawnColumnDef pawnColumnDef2 = (hotReload ? (DefDatabase<PawnColumnDef>.GetNamed(defName2, errorOnFail: false) ?? new PawnColumnDef()) : new PawnColumnDef());
				pawnColumnDef2.defName = defName2;
				pawnColumnDef2.workType = item2;
				pawnColumnDef2.moveWorkTypeLabelDown = moveWorkTypeLabelDown;
				pawnColumnDef2.workerClass = typeof(PawnColumnWorker_WorkPriority);
				pawnColumnDef2.sortable = true;
				pawnColumnDef2.modContentPack = item2.modContentPack;
				workTable.columns.Insert(workTable.columns.FindIndex((PawnColumnDef x) => x.Worker is PawnColumnWorker_CopyPasteWorkPriorities) + num, pawnColumnDef2);
				yield return pawnColumnDef2;
				num++;
			}
		}
	}
}