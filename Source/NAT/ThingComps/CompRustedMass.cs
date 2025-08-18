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
	public class CompProperties_RustedMass : CompProperties
	{
		public SimpleCurve pointsFromCurrentPoints;

		public SimpleCurve pointsFactorFromRaidIndex;

		public SimpleCurve pointsFactorFromActivity;

		public SimpleCurve cooldownFactorFromRaidIndex;

		public SimpleCurve cooldownFactorFromActivity;

		public FloatRange cooldownDaysRange;
		public CompProperties_RustedMass()
		{
			compClass = typeof(CompRustedMass);
		}
	}
	public class CompRustedMass : ThingComp, IActivity, IRoofCollapseAlert
	{
		public CompActivity activity => parent.TryGetComp<CompActivity>();
		public CompProperties_RustedMass Props => (CompProperties_RustedMass)props;

		public int raidIndex;

		public int ticksSinceRaid;

		public int ticksTillRaid;

		public Map mapToAttack;

		public bool passive;

		private static bool IsValidCell(IntVec3 cell, Map map)
		{
			if (cell.InBounds(map))
			{
				return cell.Walkable(map);
			}
			return false;
		}
		public RoofCollapseResponse Notify_OnBeforeRoofCollapse()
		{
			if (RCellFinder.TryFindRandomCellNearWith(parent.Position, (IntVec3 c) => IsValidCell(c, parent.MapHeld), parent.MapHeld, out var result, 10))
			{
				SkipUtility.SkipTo(parent, result, parent.MapHeld);
				activity.AdjustActivity(0.5f);
			}
			return RoofCollapseResponse.RemoveThing;
		}
		public void OnActivityActivated()
		{
			if (parent.MapHeld != null)
			{
				Activate(parent.MapHeld);
				parent.Kill();
			}
		}

		public void Activate(Map map)
		{
			if (map == null)
			{
				return;
			}
			if (parent.IsOnHoldingPlatform)
			{
				Building_HoldingPlatform building_HoldingPlatform = (Building_HoldingPlatform)parent.ParentHolder;
				building_HoldingPlatform.innerContainer.TryDrop(parent, building_HoldingPlatform.Position, building_HoldingPlatform.Map, ThingPlaceMode.Direct, 1, out var _);
				CompHoldingPlatformTarget compHoldingPlatformTarget = parent.TryGetComp<CompHoldingPlatformTarget>();
				if (compHoldingPlatformTarget != null)
				{
					compHoldingPlatformTarget.targetHolder = null;
				}
			}
			if (!parent.Spawned && parent.SpawnedOrAnyParentSpawned)
			{
				Thing t = parent.SpawnedParentOrMe;
				parent.ParentHolder.GetDirectlyHeldThings().TryDrop(parent, t.PositionHeld, t.MapHeld, ThingPlaceMode.Near, 1, out var _);
			}
			ExecuteRaid(map, 2f);
			if(parent.SpawnedOrAnyParentSpawned)
            {
				PawnGroupMakerParms pawnGroupMakerParms = new PawnGroupMakerParms
				{
					groupKind = DefDatabase<PawnGroupKindDef>.GetNamed("NAT_RustedMassDrop"),
					tile = map.Tile,
					faction = Faction.OfEntities,
					points = new FloatRange(450f, 1000f).RandomInRange
				};
				List<Pawn> list = PawnGroupMakerUtility.GeneratePawns(pawnGroupMakerParms).ToList();
				foreach (Pawn p in list)
				{
					GenSpawn.Spawn(p, parent.PositionHeld, map);
				}
				LordMaker.MakeNewLord(Faction.OfEntities, new LordJob_AssaultColony(), map, list);
			}
			passive = true;
		}

		public void OnPassive()
		{
		}

		public bool ShouldGoPassive()
		{
			return false;
		}

		public bool CanBeSuppressed()
		{
			return true;
		}

		public bool CanActivate()
		{
			return true;
		}

		public string ActivityTooltipExtra()
		{
			return null;
		}
		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref raidIndex, "raidIndex", 0);
			Scribe_Values.Look(ref ticksSinceRaid, "ticksSinceRaid", 60000);
			Scribe_Values.Look(ref ticksTillRaid, "ticksTillRaid", 0);
			Scribe_References.Look(ref mapToAttack, "mapToAttack");
		}

		public override void PostPostMake()
		{
			ticksSinceRaid = 240000;
		}

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            mapToAttack = parent.MapHeld;
        }

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			foreach (Gizmo item2 in base.CompGetGizmosExtra())
			{
				yield return item2;
			}
			if (DebugSettings.ShowDevGizmos)
			{
				yield return new Command_Action
				{
					defaultLabel = "DEV: Force raid",
					action = delegate
					{
						ExecuteRaid(mapToAttack);
						raidIndex++;
						ticksSinceRaid = Mathf.RoundToInt(Props.cooldownFactorFromRaidIndex.Evaluate(raidIndex) * Props.cooldownFactorFromActivity.Evaluate(activity.ActivityLevel) * Props.cooldownDaysRange.RandomInRange * 60000f);
					}
				};
			}
		}
		public override void CompTick()
		{
			if (!parent.IsHashIntervalTick(250))
			{
				return;
			}
			if (parent.MapHeld != null)
			{
				mapToAttack = parent.MapHeld;
                (parent as Pawn).Drawer.renderer.SetAllGraphicsDirty();
            }
			if (mapToAttack.IsPocketMap)
			{
                mapToAttack = mapToAttack.PocketMapParent.sourceMap;
            }
			if (mapToAttack != null)
			{
				if (ticksSinceRaid <= 0)
				{
					ExecuteRaid(mapToAttack);
					raidIndex++;
					ticksSinceRaid = Mathf.RoundToInt(Props.cooldownFactorFromRaidIndex.Evaluate(raidIndex) * Props.cooldownFactorFromActivity.Evaluate(activity.ActivityLevel) * Props.cooldownDaysRange.RandomInRange * 60000f);
				}
				if (ticksTillRaid > 0)
				{
					if (ticksTillRaid == 1)
					{
						ExecuteRaid(mapToAttack, 1.2f);
					}
					ticksTillRaid--;
				}
			}
			if (ticksSinceRaid > 0)
			{
				ticksSinceRaid -= 250;
			}
		}
		public void ExecuteRaid(Map map, float pointsFactor = 1)
		{
			RustedArmyUtility.ExecuteRaid(mapToAttack, Props.pointsFactorFromRaidIndex.Evaluate(raidIndex) * pointsFactor * Props.pointsFactorFromActivity.Evaluate(activity.ActivityLevel) * Props.pointsFromCurrentPoints.Evaluate(StorytellerUtility.DefaultThreatPointsNow(map)), Rand.Chance(0.7f) ? 1 : new IntRange(2, 7).RandomInRange, false, true, "NAT_RustedArmyRaid_Mass".Translate());
		}

		public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
		{
			if (prevMap != null)
			{
				mapToAttack = prevMap;
			}
            if (mapToAttack.IsPocketMap)
            {
                mapToAttack = mapToAttack.PocketMapParent.sourceMap;
            }
            Activate(mapToAttack);
			base.Notify_Killed(prevMap, dinfo);
		}
	}
}