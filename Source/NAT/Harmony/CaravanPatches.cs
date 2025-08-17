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
using HarmonyLib;

namespace NAT
{
    /*[HarmonyPatch(typeof(FloatMenuOptionProvider_DraftedMove), nameof(FloatMenuOptionProvider_DraftedMove.PawnGotoAction))]
    public static class Patch_PawnGotoAction
    {
        [HarmonyPrefix]
        public static bool Prefix(IntVec3 clickCell, Pawn pawn, IntVec3 gotoLoc)
        {
            if (pawn is RustedPawn && pawn.Faction?.IsPlayer == true)
            {
                bool flag;
                if (pawn.Position == gotoLoc || (pawn.CurJobDef == JobDefOf.Goto && pawn.CurJob.targetA.Cell == gotoLoc))
                {
                    flag = true;
                }
                else
                {
                    Job job = JobMaker.MakeJob(JobDefOf.Goto, gotoLoc);
                    if (pawn.Map.exitMapGrid.IsExitCell(clickCell))
                    {
                        job.exitMapOnArrival = true;
                    }
                    else if (!pawn.Map.IsPlayerHome && !pawn.Map.exitMapGrid.MapUsesExitGrid && CellRect.WholeMap(pawn.Map).IsOnEdge(clickCell, 3) && pawn.Map.Parent.GetComponent<FormCaravanComp>() != null && MessagesRepeatAvoider.MessageShowAllowed("MessagePlayerTriedToLeaveMapViaExitGrid-" + pawn.Map.uniqueID, 60f))
                    {
                        if (pawn.Map.Parent.GetComponent<FormCaravanComp>().CanFormOrReformCaravanNow)
                        {
                            Messages.Message("MessagePlayerTriedToLeaveMapViaExitGrid_CanReform".Translate(), pawn.Map.Parent, MessageTypeDefOf.RejectInput, historical: false);
                        }
                        else
                        {
                            Messages.Message("MessagePlayerTriedToLeaveMapViaExitGrid_CantReform".Translate(), pawn.Map.Parent, MessageTypeDefOf.RejectInput, historical: false);
                        }
                    }
                    flag = pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                }
                if (flag)
                {
                    FleckMaker.Static(gotoLoc, pawn.Map, FleckDefOf.FeedbackGoto);
                }
                return false;
            }
            return true;
        }
    }*/

    [HarmonyPatch(typeof(MapPawns), nameof(MapPawns.AnyPawnBlockingMapRemoval), MethodType.Getter)]
    public class Patch_AnyPawnBlockingMapRemoval
    {
        [HarmonyPostfix]
        public static void Postfix(ref bool __result, MapPawns __instance)
        {
            if (__result) return;
            foreach (Pawn item in __instance.AllPawns)
            {
                if (item is RustedPawn && item.Faction?.IsPlayer == true)
                {
                    __result = true;
                    return;
                }
            }
        }
    }

    [HarmonyPatch(typeof(CaravanExitMapUtility), nameof(CaravanExitMapUtility.ExitMapAndJoinOrCreateCaravan))]
    public static class Patch_ExitMapAndJoinOrCreateCaravan
    {
        [HarmonyPrefix]
        [HarmonyPriority(501)]
        public static bool Prefix(Pawn pawn, Rot4 exitDir)
        {
            if (pawn is RustedPawn && pawn.Faction?.IsPlayer == true)
            {
                Caravan caravan = CaravanExitMapUtility.FindCaravanToJoinFor(pawn);
                if (caravan != null)
                {
                    //CaravanExitMapUtility.AddCaravanExitTaleIfShould(pawn);
                    caravan.AddPawn(pawn, addCarriedPawnToWorldPawnsIfAny: true);
                    pawn.ExitMap(allowedToJoinOrCreateCaravan: false, exitDir);
                }
                else
                {
                    Map map = pawn.Map;
                    PlanetTile directionTile = (PlanetTile)findRandomStartingTileBasedOnExitDir.Invoke(null, new object[2] { map.Tile, exitDir });
                    Caravan caravan2 = CaravanExitMapUtility.ExitMapAndCreateCaravan(Gen.YieldSingle(pawn), pawn.Faction, map.Tile, directionTile, PlanetTile.Invalid, sendMessage: false);
                    caravan2.autoJoinable = true;
                    bool flag = false;
                    IReadOnlyList<Pawn> allPawnsSpawned = map.mapPawns.AllPawnsSpawned;
                    for (int i = 0; i < allPawnsSpawned.Count; i++)
                    {
                        if (CaravanExitMapUtility.FindCaravanToJoinFor(allPawnsSpawned[i]) != null && !allPawnsSpawned[i].Downed && !allPawnsSpawned[i].Drafted)
                        {
                            if (allPawnsSpawned[i].IsAnimal)
                            {
                                flag = true;
                            }
                            RestUtility.WakeUp(allPawnsSpawned[i]);
                            allPawnsSpawned[i].jobs.CheckForJobOverride();
                        }
                    }
                    TaggedString taggedString = "MessagePawnLeftMapAndCreatedCaravan".Translate(pawn.LabelShort, pawn).CapitalizeFirst();
                    if (flag)
                    {
                        taggedString += " " + "MessagePawnLeftMapAndCreatedCaravan_AnimalsWantToJoin".Translate();
                    }
                    Messages.Message(taggedString, caravan2, MessageTypeDefOf.TaskCompletion);
                }
                return false;
            }
            return true;
        }

        public static MethodInfo findRandomStartingTileBasedOnExitDir = AccessTools.Method(typeof(CaravanExitMapUtility), "FindRandomStartingTileBasedOnExitDir", new Type[2] { typeof(PlanetTile), typeof(Rot4) }, (Type[])null);
    }

    [HarmonyPatch(typeof(CaravanExitMapUtility), "CanExitMapAndJoinOrCreateCaravanNow")]
    public static class Patch_CanExitMapAndJoinOrCreateCaravanNow
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, ref bool __result)
        {
            if (__result || !pawn.Spawned)
            {
                return;
            }
            if (!pawn.Map.exitMapGrid.MapUsesExitGrid)
            {
                return;
            }
            if (pawn is RustedPawn rust && rust.Controllable && pawn.Faction?.IsPlayer == true)
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(JobDriver_PrepareCaravan_GatherItems), nameof(JobDriver_PrepareCaravan_GatherItems.IsUsableCarrier))]
    public static class Patch_IsUsableCarrier
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn p, Pawn forPawn, bool allowColonists, ref bool __result)
        {
            if (__result)
            {
                return;
            }
            if (!p.IsFormingCaravan())
            {
                return;
            }
            if (p.DestroyedOrNull() || !p.Spawned || p.inventory.UnloadEverything || !forPawn.CanReach(p, PathEndMode.Touch, Danger.Deadly))
            {
                return;
            }
            if (allowColonists && p is RustedPawn && p.workSettings?.WorkIsActive(WorkTypeDefOf.Hauling) == true && p.Faction?.IsPlayer == true)
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch]
    public static class Patch_CheckForErrors
    {
        public static MethodBase TargetMethod()
        {
            return AccessTools.Method(AccessTools.Inner(typeof(Dialog_FormCaravan), "<>c__DisplayClass95_0"), "<CheckForErrors>b__1");
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            var codes = new List<CodeInstruction>(instructions);

            var jumpLabel = il.DefineLabel();
            codes[3].labels.Add(jumpLabel);

            var newCodes = new List<CodeInstruction>();

            newCodes.Add(new CodeInstruction(OpCodes.Ldarg_1));
            //newCodes.Add(new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(Pawn), "get_RaceProps")));
            //newCodes.Add(new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(RaceProperties), "get_IsMechanoid")));
            newCodes.Add(new CodeInstruction(OpCodes.Call, (object)AccessTools.Method(typeof(Patch_CheckForErrors), "GoodCaravanLeader", (Type[])null, (Type[])null)));
            newCodes.Add(new CodeInstruction(OpCodes.Brtrue_S, jumpLabel));

            codes.InsertRange(0, newCodes);
            return codes.AsEnumerable();
        }

        public static bool GoodCaravanLeader(Pawn pawn)
        {
            return pawn is RustedPawn rust && rust.Controllable;
        }
    }

    /*[HarmonyPatch(typeof(LordToil_PrepareCaravan_GatherItems), "LordToilTick")]
    public static class Patch_LordToilTick_Patch
    {


        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            var codes = new List<CodeInstruction>(instructions);

            var jumpLabel = il.DefineLabel();
            var insertionPoint = -1;

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Stloc_2)
                {
                    insertionPoint = i + 1;
                    codes[i + 4].labels.Add(jumpLabel);
                    break;
                }
            }

            if (insertionPoint == -1)
            {
                Log.Error("MechsCaravan could not find LordToil_PrepareCaravan_GatherItems_LordToilTick_Patch insertion point!");
            }
            else
            {
                var newCodes = new List<CodeInstruction>();

                newCodes.Add(new CodeInstruction(OpCodes.Ldloc_2)); // Load local Pawn variable
                newCodes.Add(new CodeInstruction(OpCodes.Call, (object)AccessTools.Method(typeof(Patch_LordToilTick_Patch), "CanGather", (Type[])null, (Type[])null)));
                newCodes.Add(new CodeInstruction(OpCodes.Brtrue_S, jumpLabel)); // Jump if it is a mechanoid (effectively equivalent to changing original line of code to: (Pawn.RaceProps.Is_Mechanoid || Pawn.IsColonist))

                codes.InsertRange(insertionPoint, newCodes);
            }
            return codes.AsEnumerable();
        }

        public static bool CanGather(Pawn pawn)
        {
            return pawn is RustedPawn rust && rust.Controllable && rust.workSettings?.WorkIsActive(WorkTypeDefOf.Hauling) == true;
        }
    }*/

    [HarmonyPatch(typeof(LordToil_PrepareCaravan_GatherItems), "UpdateAllDuties")]
    public static class Patch_LordToil_PrepareCaravan_GatherItems
    {
        public static FieldInfo meetingPoint = AccessTools.Field(typeof(LordToil_PrepareCaravan_GatherItems), "meetingPoint");

        [HarmonyPostfix]
        public static void Postfix(LordToil_PrepareCaravan_GatherDownedPawns __instance)
        {
            for (int i = 0; i < __instance.lord.ownedPawns.Count; i++)
            {
                Pawn pawn = __instance.lord.ownedPawns[i];
                if (pawn is RustedPawn p && p.workSettings?.WorkIsActive(WorkTypeDefOf.Hauling) == true)
                {
                    pawn.mindState.duty = new PawnDuty(DutyDefOf.PrepareCaravan_GatherItems, (IntVec3)meetingPoint.GetValue(__instance));
                }
            }
        }
    }

    [HarmonyPatch(typeof(LordToil_PrepareCaravan_GatherDownedPawns), "UpdateAllDuties")]
    public static class Patch_LordToil_PrepareCaravan_GatherDownedPawns
    {
        public static FieldInfo meetingPoint = AccessTools.Field(typeof(LordToil_PrepareCaravan_GatherDownedPawns), "meetingPoint");

        public static FieldInfo exitSpot = AccessTools.Field(typeof(LordToil_PrepareCaravan_GatherDownedPawns), "exitSpot");

        [HarmonyPostfix]
        public static void Postfix(LordToil_PrepareCaravan_GatherDownedPawns __instance)
        {
            for (int i = 0; i < __instance.lord.ownedPawns.Count; i++)
            {
                Pawn pawn = __instance.lord.ownedPawns[i];
                if (pawn is RustedPawn p && p.workSettings?.WorkIsActive(WorkTypeDefOf.Hauling) == true)
                {
                    pawn.mindState.duty = new PawnDuty(DutyDefOf.PrepareCaravan_GatherDownedPawns, (IntVec3)meetingPoint.GetValue(__instance), (IntVec3)exitSpot.GetValue(__instance));
                }
            }
        }
    }

    [HarmonyPatch(typeof(CaravanUtility), "IsOwner")]
    public static class Patch_CaravanUtility
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, Faction caravanFaction, ref bool __result)
        {
            if (__result)
            {
                return;
            }
            if (caravanFaction == null)
            {
                return;
            }
            if (pawn is RustedPawn p && p.Controllable && pawn.Faction == caravanFaction && pawn.HostFaction == null)
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(WITab_Caravan_Social), "OnOpen")]
    public static class Patch_WITab_Caravan_Social
    {
        public static FieldInfo specificSocialTabForPawn = AccessTools.Field(typeof(WITab_Caravan_Social), "specificSocialTabForPawn");

        [HarmonyPostfix]
        public static void Postfix(WITab_Caravan_Social __instance)
        {
            if ((Pawn)specificSocialTabForPawn.GetValue(__instance) is RustedPawn)
            {
                specificSocialTabForPawn.SetValue(__instance, null);
            }
        }
    }

    [HarmonyPatch(typeof(SettleInExistingMapUtility), "SettleCommand")]
    public static class Patch_SettleInExistingMapUtility
    {
        [HarmonyPostfix]
        public static void Postfix(Map map, bool requiresNoEnemies, ref Command __result)
        {
            if (__result.disabledReason == "CommandSettleFailNoColonists".Translate() && map.mapPawns.SpawnedColonyMechs.Any((Pawn x) => x is RustedPawn && x.Faction?.IsPlayer == true && !x.Downed))
            {
                if (requiresNoEnemies)
                {
                    foreach (IAttackTarget item in map.attackTargetsCache.TargetsHostileToColony)
                    {
                        if (GenHostility.IsActiveThreatToPlayer(item))
                        {
                            __result.Disable("CommandSettleFailEnemies".Translate());
                            return;
                        }
                    }
                }
                __result.disabledReason = null;
                __result.Disabled = false;
            }
        }
    }
}

