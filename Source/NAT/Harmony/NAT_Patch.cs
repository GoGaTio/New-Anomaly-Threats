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
	[HarmonyPatch(typeof(Pawn_NeedsTracker), "ShouldHaveNeed")]
	public class Patch_Needs
	{
		[HarmonyPrefix]
		public static bool Prefix(NeedDef nd, Pawn ___pawn, ref bool __result)
		{
			if (nd == NATDefOf.NAT_RustRest)
			{
				if (___pawn is RustedPawn && ___pawn.Faction?.IsPlayer == true)
				{
					__result = true;
				}
				else
				{
					__result = false;
				}
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(Toils_LayDown), "ApplyBedRelatedEffects")]
	public class Patch_ApplyBedRelatedEffects
	{

		[HarmonyPostfix]
		public static void Postfix(Pawn p, Building_Bed bed, bool asleep, bool gainRest, int delta)
		{
			if (p is RustedPawn rust)
			{
				rust.restNeed?.TickResting();
			}
		}
	}

	[HarmonyPatch(typeof(Caravan_NeedsTracker), "TrySatisfyPawnNeeds")]
	public class Patch_TrySatisfyPawnNeeds
	{

		[HarmonyPostfix]
		public static void Postfix(Pawn pawn, int delta, Caravan_NeedsTracker __instance)
		{
			if (pawn is RustedPawn rust && !rust.Dead && !__instance.caravan.pather.MovingNow)
			{
				rust.restNeed.TickResting();
			}
		}
	}

	[HarmonyPatch(typeof(Caravan), "CantMove", MethodType.Getter)]
	public class Patch_CantMove
	{

		[HarmonyPostfix]
		public static void Postfix(ref bool __result, Caravan __instance)
		{
			if (__result) return;
			foreach (Pawn p in __instance.PawnsListForReading)
			{
				if (p is RustedPawn rust && rust.restNeed?.exhausted == true)
				{
					__result = true;
					return;
				}
			}
		}
	}

	[HarmonyPatch(typeof(Pawn), nameof(Pawn.CurrentEffectiveVerb), MethodType.Getter)]
	public class Patch_CurrentEffectiveVerb
	{

		[HarmonyPostfix]
		public static void Postfix(ref Verb __result, Pawn __instance)
		{
			if(__result.verbProps.onlyManualCast && __instance is RustedPawn && __instance.Faction == Faction.OfPlayer)
            {
				__result = __instance.TryGetAttackVerb(null, false);
			}
		}
	}

	[HarmonyPatch(typeof(Pawn_NeedsTracker), "BindDirectNeedFields")]
	public class Patch_BindNeed
	{

		[HarmonyPostfix]
		public static void Postfix(Pawn ___pawn, Pawn_NeedsTracker __instance)
		{
			if(___pawn is RustedPawn rust)
            {
				rust.restNeed = __instance.TryGetNeed<Need_RustRest>();
			}
		}
	}

	[HarmonyPatch(typeof(Site), "ShouldRemoveMapNow")]
	public class Patch_DontRemoveMap
	{

		[HarmonyPostfix]
		public static bool Postfix(bool alsoRemoveWorldObject, ref bool __result, Site __instance)
		{
			foreach (Pawn pawn in __instance.Map.mapPawns.AllPawnsSpawned)
			{
				if (pawn is RustedPawn && pawn.Faction == Faction.OfPlayer)
				{
					return false;
				}
			}
			return __result;
		}
	}

	[HarmonyPatch(typeof(AutoUndrafter), "ShouldAutoUndraft")]
	public class Patch_DisableUndraft
	{
		[HarmonyPrefix]
		public static bool Prefix(Pawn ___pawn, ref bool __result)
		{
			if (___pawn is RustedPawn)
			{
				__result = false;
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(PawnCapacityDef), "CanShowOnPawn")]
	public static class Patch_CanShowOnPawn
	{
		[HarmonyPostfix]
		public static void Postfix(Pawn p, ref bool __result, PawnCapacityDef __instance)
		{
			if (__result) return;
			if (p is RustedPawn rust)
			{
				if (__instance.defName == "Manipulation" || __instance.defName == "Moving" || __instance.defName == "Sight")
				{
					__result = true;
				}
			}
		}
	}

	[HarmonyPatch(typeof(HealthCardUtility), "DrawOverviewTab")]
	public static class Patch_DrawOverviewTab
	{
		[HarmonyPostfix]
		public static void Postfix(Rect rect, Pawn pawn, float curY, ref float __result)
		{
			if (pawn.kindDef == NATDefOf.NAT_Collector || (pawn is RustedPawn rust && rust.Faction == Faction.OfPlayer))
			{
				if (!pawn.Dead)
				{
					IEnumerable<PawnCapacityDef> source = DefDatabase<PawnCapacityDef>.AllDefs.Where((PawnCapacityDef x) => x == PawnCapacityDefOf.Manipulation || x == PawnCapacityDefOf.Moving || x == PawnCapacityDefOf.Sight);
					foreach (PawnCapacityDef item in source.OrderBy((PawnCapacityDef act) => act.listOrder))
					{
						PawnCapacityDef activityLocal;
						if (PawnCapacityUtility.BodyCanEverDoCapacity(pawn.RaceProps.body, item))
						{
							activityLocal = item;
							Pair<string, Color> efficiencyLabel = HealthCardUtility.GetEfficiencyLabel(pawn, item);
							DrawLeftRow(rect, ref curY, item.GetLabelFor(pawn).CapitalizeFirst(), efficiencyLabel.First, efficiencyLabel.Second, new TipSignal(TipGetter, pawn.thingIDNumber ^ item.index));
						}
						string TipGetter()
						{
							if (!pawn.Dead)
							{
								return HealthCardUtility.GetPawnCapacityTip(pawn, activityLocal);
							}
							return "";
						}
					}
				}
			}
		}

		private static readonly Color HighlightColor = new Color(0.5f, 0.5f, 0.5f, 1f);

		private static void DrawLeftRow(Rect rect, ref float curY, string leftLabel, string rightLabel, Color rightLabelColor, TipSignal tipSignal)
		{
			Rect rect2 = new Rect(17f, curY, rect.width - 34f - 10f, 22f);
			if (Mouse.IsOver(rect2))
			{
				using (new TextBlock(HighlightColor))
				{
					GUI.DrawTexture(rect2, TexUI.HighlightTex);
				}
			}
			Text.Anchor = TextAnchor.MiddleLeft;
			Widgets.Label(rect2, leftLabel);
			GUI.color = rightLabelColor;
			Text.Anchor = TextAnchor.MiddleRight;
			Widgets.Label(rect2, rightLabel);
			GUI.color = Color.white;
			Text.Anchor = TextAnchor.UpperLeft;
			Rect rect3 = new Rect(0f, curY, rect.width, 20f);
			if (Mouse.IsOver(rect3))
			{
				TooltipHandler.TipRegion(rect3, tipSignal);
			}
			curY += rect2.height;
		}

	}

	[HarmonyPatch(typeof(Widgets), nameof(Widgets.ThingIcon), new Type[]
	{
		typeof(Rect),
		typeof(Thing),
		typeof(float),
		typeof(Rot4?),
		typeof(bool),
		typeof(float),
		typeof(bool)
	})]
	public class Patch_ThingIcon
	{
		[HarmonyPrefix]
		public static bool Prefix(Rect rect, Thing thing, float alpha, Rot4? rot, bool stackOfOne, float scale, bool grayscale)
		{
			if (thing is RustedPawn rust)
			{
				float scale2;
				float angle;
				Vector2 iconProportions;
				Color color;
				Material material;
				Texture texture = Widgets.GetIconFor(thing, new Vector2(rect.width, rect.height), rot, stackOfOne, out scale2, out angle, out iconProportions, out color, out material);
				if (texture == null || texture == BaseContent.BadTex)
				{
					return false;
				}
				GUI.color = color;
				ThingStyleDef styleDef = thing.StyleDef;
				texture = PortraitsCache.Get(rust, new Vector2(rect.width, rect.height), rot ?? Rot4.East);
				Material mat = material;
				if (grayscale)
				{
					MaterialRequest materialRequest = default(MaterialRequest);
					materialRequest.shader = ShaderDatabase.GrayscaleGUI;
					materialRequest.color = color;
					MaterialRequest req = materialRequest;
					req.maskTex = Texture2D.redTexture;
					mat = MaterialPool.MatFrom(req);
				}
				thingIconWorker.Invoke(null, new object[8] { rect, thing.def, texture, angle, scale2 * scale, rot, mat, alpha });
				GUI.color = Color.white;
				return false;
			}
			return true;
		}

		public static MethodInfo thingIconWorker = AccessTools.Method(typeof(Widgets), "ThingIconWorker", new Type[8] { typeof(Rect), typeof(ThingDef), typeof(Texture), typeof(float), typeof(float), typeof(Rot4?), typeof(Material), typeof(float) }, (Type[])null);
	}


	[HarmonyPatch(typeof(PawnAttackGizmoUtility), "CanOrderPlayerPawn")]
	public class Patch_AddAttackGizmo
	{
		[HarmonyPostfix]
		public static void Postfix(Pawn pawn, ref bool __result)
		{
			if (pawn is RustedPawn rust && rust.Draftable && pawn.Faction == Faction.OfPlayer)
			{
				__result = true;
			}
		}
	}

	[HarmonyPatch(typeof(StunHandler), "CanAdaptToDamage")]
	public class Patch_AddEMPResistance
	{
		[HarmonyPostfix]
		public static void Postfix(DamageDef def, ref bool __result, StunHandler __instance)
		{
			if (__instance.parent is RustedPawn rust && def == DamageDefOf.EMP)
			{
				__result = true;
			}
		}
	}

	[HarmonyPatch(typeof(CaravanUIUtility), "AddPawnsSections")]
	public class Patch_AddCaravanSection
	{
		[HarmonyPostfix]
		public static void Postfix(TransferableOneWayWidget widget, List<TransferableOneWay> transferables)
		{
			IEnumerable<TransferableOneWay> source = transferables.Where((TransferableOneWay x) => x.ThingDef.category == ThingCategory.Pawn);
			if (source.Any((TransferableOneWay x) => x.AnyThing is RustedPawn))
			{
				widget.AddSection("NAT_RustedSoldiers".Translate(), source.Where((TransferableOneWay x) => x.AnyThing is RustedPawn rust && rust.restNeed?.exhausted != true && rust.Faction == Faction.OfPlayer && rust.Controllable));
			}
		}
	}

	[HarmonyPatch(typeof(PawnUtility), "ShouldSendNotificationAbout")]
	public class Patch_ShouldSendNotification
	{
		[HarmonyPostfix]
		public static void Postfix(Pawn p, ref bool __result)
		{
			if (p is RustedPawn rust && p.Faction == Faction.OfPlayer)
			{
				__result = false;
			}
		}
	}

	[HarmonyPatch(typeof(PawnGenerator), "GenerateRandomAge")]
	public class Patch_GenereteRustedPawn
	{
		[HarmonyPostfix]
		public static void Postfix(Pawn pawn, PawnGenerationRequest request)
		{
			if(pawn is RustedPawn rust && rust.def.race.GetNameGenerator(pawn.gender) != null)
            {
				pawn.Name = new NameSingle(NameGenerator.GenerateName(rust.def.race.GetNameGenerator(pawn.gender), (string x) => !NameTriple.FromString(x).UsedThisGame, appendNumberIfNameUsed: false, null, null));
			}
		}
	}

	[HarmonyPatch(typeof(FloatMenuOptionProvider_Romance), "AppliesInt")]
	public class Patch_FloatMenuOptionProvider_Romance
	{
		[HarmonyPostfix]
		public static void Postfix(FloatMenuContext context, ref bool __result)
		{
			if(context.FirstSelectedPawn is RustedPawn)
            {
				__result = false;
            }
		}
	}

	[HarmonyPatch]
	public static class Patch_SelectRustedSoldiers
	{
		public static MethodBase TargetMethod()
		{
			return AccessTools.Method(typeof(Selector), "<SelectInsideDragBox>g__IsColonist|42_1");
		}

		public static void Postfix(Thing t, ref bool __result)
		{
			if (__result) return;
			if (t is RustedPawn rust && rust.Faction == Faction.OfPlayer)
			{
				__result = true;
			}
		}
	}

	[HarmonyPatch(typeof(FoodUtility))]
	[HarmonyPatch(nameof(FoodUtility.WillIngestFromInventoryNow))]
	public class Patch_WillIngestFromInventoryNow
	{
		[HarmonyPostfix]
		public static void Postfix(Pawn pawn, Thing inv, ref bool __result)
		{
			if (!__result) return;
			if (pawn is RustedPawn rust)
			{
				__result = false;
			}
		}
	}

	
	[HarmonyPatch(typeof(ITab_Pawn_Gear))]
	[HarmonyPatch("DrawThingRow")]
	public class Patch_UseGear
	{

		[HarmonyPostfix]
		public static void Postfix(ref float y, float width, Thing thing, bool inventory = false)
		{
			if (inventory && thing.TryGetComp<CompUsableByRust>(out CompUsableByRust comp) && Find.Selector.SingleSelectedThing is RustedPawn rust && rust.restNeed?.exhausted != true && rust.Faction == Faction.OfPlayer && rust.CarriedBy == null && rust.Controllable && rust.Spawned && !rust.Downed)
			{
				Rect rect = new Rect(width - 72f, y - 28f, 24f, 24f);
				TooltipHandler.TipRegion(rect, comp.JobReport);
				if (Widgets.ButtonImage(rect, RustedArmyUtility.Use))
				{
					SoundDefOf.Tick_High.PlayOneShotOnCamera();
					rust.jobs.TryTakeOrderedJob(JobMaker.MakeJob(NATDefOf.NAT_UseItemByRust, thing), JobTag.DraftedOrder);
				}
			}
		}
	}

	[HarmonyPatch(typeof(ITab_Pawn_Gear))]
	[HarmonyPatch("CanControlColonist")]
	[HarmonyPatch(MethodType.Getter)]
	public class Patch_CanDropEquipment
	{
		[HarmonyPostfix]
		public static void Postfix(ref bool __result)
		{
			if (__result) return;
			if (Find.Selector.SingleSelectedThing is RustedPawn rust && rust.restNeed?.exhausted != true && rust.Faction == Faction.OfPlayer && rust.CarriedBy == null && rust.Controllable && !rust.Downed)
			{
				__result = true;
			}
		}
	}

	[HarmonyPatch(typeof(ITab_Pawn_Gear))]
	[HarmonyPatch("IsVisible")]
	[HarmonyPatch(MethodType.Getter)]
	public class Patch_IsVisible
	{
		[HarmonyPostfix]
		public static void Postfix(ref bool __result)
		{
			if (__result) return;
			if (Find.Selector.SingleSelectedThing is RustedPawn rust && (DebugSettings.godMode || rust.Faction == Faction.OfPlayer))
			{
				__result = true;
			}
		}
	}

	[HarmonyPatch(typeof(Pawn))]
	[HarmonyPatch("CanTakeOrder")]
	[HarmonyPatch(MethodType.Getter)]
	public class Patch_MovingOrders
	{
		[HarmonyPostfix]
		public static void Postfix(ref bool __result, Pawn __instance)
		{
			if (__instance is RustedPawn rust && rust.restNeed?.exhausted != true && rust.Draftable && rust.Faction == Faction.OfPlayer)
			{
				__result = true;
			}
		}
	}

	[HarmonyPatch(typeof(FloatMenuUtility), "GetRangedAttackAction")]
	public class Patch_DraftedRustedSoldiers_Ranged
	{
		[HarmonyPostfix]
		public static void MakePawnControllable(Pawn pawn, LocalTargetInfo target, ref string failStr, ref Action __result)
		{
			if (pawn.Faction != Faction.OfPlayer || !(pawn is RustedPawn rust) || !rust.Draftable)
			{
				return;
			}
			Verb primaryVerb = pawn.equipment.PrimaryEq.PrimaryVerb;
			if (primaryVerb.verbProps.IsMeleeAttack)
			{
				__result = null;
				return;
			}
			if (!pawn.Drafted)
			{
				failStr = "IsNotDraftedLower".Translate(pawn.LabelShort, pawn);
			}
			else if (target.IsValid && !pawn.equipment.PrimaryEq.PrimaryVerb.CanHitTarget(target))
			{
				if (!pawn.Position.InHorDistOf(target.Cell, primaryVerb.verbProps.range))
				{
					failStr = "OutOfRange".Translate();
				}
				else
				{
					float num = primaryVerb.verbProps.EffectiveMinRange(target, pawn);
					if ((float)pawn.Position.DistanceToSquared(target.Cell) < num * num)
					{
						failStr = "TooClose".Translate();
					}
					else
					{
						failStr = "CannotHitTarget".Translate();
					}
				}
			}
			else if (pawn == target.Thing)
			{
				failStr = "CannotAttackSelf".Translate();
			}
            else
            {
				__result = delegate
				{
					Job job = JobMaker.MakeJob(JobDefOf.AttackStatic, target);
					pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
				};
			}
			failStr = failStr.CapitalizeFirst();
		}
	}

	[HarmonyPatch(typeof(FloatMenuUtility), "GetMeleeAttackAction")]
	public class Patch_DraftedRustedSoldiers_Melee
	{
		[HarmonyPostfix]
		public static void MakePawnControllable(Pawn pawn, LocalTargetInfo target, ref string failStr, ref Action __result)
		{
			if (pawn.Faction != Faction.OfPlayer || !(pawn is RustedPawn rust) || !rust.Draftable )
			{
				return;
			}
			if (!pawn.Drafted)
			{
				failStr = "IsNotDraftedLower".Translate(pawn.LabelShort, pawn);
			}
			else if (target.IsValid && !pawn.CanReach(target, PathEndMode.Touch, Danger.Deadly))
			{
				failStr = "NoPath".Translate();
			}
			else if (pawn.meleeVerbs.TryGetMeleeVerb(target.Thing) == null)
			{
				failStr = "Incapable".Translate();
			}
			else if (pawn == target.Thing)
			{
				failStr = "CannotAttackSelf".Translate();
			}
			else
			{
				if (!(target.Thing is Pawn pawn2) || !pawn2.RaceProps.Animal || !HistoryEventUtility.IsKillingInnocentAnimal(pawn, pawn2) || new HistoryEvent(HistoryEventDefOf.KilledInnocentAnimal, pawn.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo())
				{
					__result = delegate
					{
						Job job = JobMaker.MakeJob(JobDefOf.AttackMelee, target);
						if (target.Thing is Pawn pawn3)
						{
							job.killIncappedTarget = pawn3.Downed;
						}
						pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
					};
				}
				failStr = "IdeoligionForbids".Translate();
			}
			failStr = failStr.CapitalizeFirst();
		}
	}

}