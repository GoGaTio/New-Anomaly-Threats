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
	public class CompProperties_RustedCommander : CompProperties
	{
		public int startingUnits = 1;

		public int maxUnitsToRestore = 99;

		public int maxUnits = 99;

		public int ticksToRestore = 60000;
		public CompProperties_RustedCommander()
		{
			compClass = typeof(CompRustedCommander);
		}
	}
	public class CompRustedCommander : ThingComp
	{
		public int units = -1;

		public int ticksSinceRestore = -1;

		public RustedPawn Rust => parent as RustedPawn;
		public CompProperties_RustedCommander Props => (CompProperties_RustedCommander)props;
		public override void PostPostMake()
		{
			base.PostPostMake();
			if (units == -1)
			{
				units = Props.startingUnits;
			}
		}

		public override void PostDrawExtraSelectionOverlays()
		{
			base.PostDrawExtraSelectionOverlays();
			if (DebugSettings.ShowDevGizmos && Rust.mindState.enemyTarget != null)
			{
				GenDraw.DrawCircleOutline(Rust.mindState.enemyTarget.TrueCenter(), 0.7f, SimpleColor.Red);
				GenDraw.DrawLineBetween(parent.TrueCenter(), Rust.mindState.enemyTarget.TrueCenter(), SimpleColor.Red);
			}
		}

		public override void CompTick()
		{
			base.CompTick();
			if (units < Props.maxUnitsToRestore)
			{
				ticksSinceRestore++;
				if (ticksSinceRestore >= Props.ticksToRestore)
				{
					ticksSinceRestore = 0;
					units++;
				}
			}
		}

		public LocalTargetInfo TryCallSupport(out Ability ability)
        {
			ability = null;
			List<CastParms> castParms = new List<CastParms>();
			foreach(Ability a in Rust.abilities.AllAbilitiesForReading)
            {
				CompAbilityEffect_RustedCommander comp = a.EffectComps.FirstOrDefault((CompAbilityEffect x)=>x is CompAbilityEffect_RustedCommander) as CompAbilityEffect_RustedCommander;
				if(comp != null && comp.Props.cost <= units)
                {
					castParms.AddRange(comp.CastParms());
				}
            }
            if (castParms.NullOrEmpty() || castParms[0].ability.OnCooldown)
            {
				return LocalTargetInfo.Invalid;
			}
			Thing enemy = null;
			TraverseParms tp = TraverseParms.For(TraverseMode.PassAllDestroyableThings);
			RegionTraverser.BreadthFirstTraverse(parent.Position, parent.Map, (Region from, Region to) => to.Allows(tp, isDestination: false), delegate (Region r)
			{
				List<Thing> list = r.ListerThings.ThingsInGroup(ThingRequestGroup.AttackTarget);
				for (int i = 0; i < list.Count; i++)
				{
					Thing thing = list[i];
					if (thing.Position.InHorDistOf(parent.Position, 44f) && thing.HostileTo(parent) && (!(thing is Pawn pawn) || !pawn.Downed))
					{
						enemy = thing;
						break;
					}
				}
				return enemy != null;
			});
			if(enemy == null)
            {
				return LocalTargetInfo.Invalid;
			}
			bool enemyNearby = PawnUtility.EnemiesAreNearby(Rust, 9, passDoors: true, 6f, 1, true);
			bool overwhelmingEnemies = PawnUtility.EnemiesAreNearby(Rust, 25, passDoors: true, 45f, Rust.GetLord()?.ownedPawns?.Count ?? 2);
			if (castParms.TryRandomElementByWeight((CastParms p) => WeightCalc(p, enemyNearby, overwhelmingEnemies), out var result))
            {
				Map map = parent.Map;
				ability = result.ability;
				if (result.location == CastLocation.OnTarget)
				{
					return enemy;
				}
				if (result.location == CastLocation.BehindTarget && enemy.OccupiedRect().ExpandedBy(7).ClipInsideMap(map).Cells.TryRandomElementByWeight((IntVec3 c) => WeightCalc2(map, enemy.Position, c) ? c.DistanceTo(parent.Position) : 0f, out var cell1))
				{
					return cell1;
				}
				if (result.location == CastLocation.Turret && enemy.OccupiedRect().ExpandedBy(30).ClipInsideMap(map).Cells.TryRandomElementByWeight((IntVec3 c) => WeightCalc2(map, enemy.Position, c) ? c.DistanceTo(enemy.Position) : 0f, out var cell2))
				{
					return cell2;
				}
				if ((result.location == CastLocation.NearCaster || result.location == CastLocation.Turret) && parent.OccupiedRect().ExpandedBy(4).ClipInsideMap(map).Cells.TryRandomElement((IntVec3 c) => c.Standable(map) && c.Walkable(map) && GenSight.LineOfSight(parent.Position, c, map), out var cell3))
				{
					return cell3;
                }
				
            }
			return LocalTargetInfo.Invalid;
			float WeightCalc(CastParms parms, bool flag1, bool flag2)
            {
				if(parms.conditions.HasFlag(CastCondition.Attacking) || (parms.conditions.HasFlag(CastCondition.EnemyNearby) && flag1) || (parms.conditions.HasFlag(CastCondition.OverwhelmingEnemies) && flag2))
                {
					return parms.weight;
                }
				return 0f;
            }
			bool WeightCalc2(Map map, IntVec3 cell, IntVec3 c)
			{
				if(c.Standable(map) && c.Walkable(map) && c.InHorDistOf(parent.Position, 44f) && GenSight.LineOfSight(c, cell, map))
				{
					return true;
				}
				return false;
			}
		}

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			if (Find.Selector.SingleSelectedThing == parent && (parent.Faction == Faction.OfPlayer || DebugSettings.ShowDevGizmos))
			{
				yield return new Gizmo_RustedCommander(this);
			}
		}

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref units, "units", -1);
			Scribe_Values.Look(ref ticksSinceRestore, "ticksSinceRestore", -1);
		}
	}
}