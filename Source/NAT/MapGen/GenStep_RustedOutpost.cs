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
	public class GenStep_RustedOutpost : GenStep
	{
		public SimpleCurve floorChanceFromLayerCurve;

		public List<PrefabOption> prefabs = new List<PrefabOption>();
		
		public FloatRange points = new FloatRange(500, 1000);

		public override int SeedPart => 849686151;

		public bool spawnBeacon;

		public override void Generate(Map map, GenStepParams parms)
        {
			if(!MapGenerator.TryGetVar<CellRect>("SettlementRect", out var rect))
			{
				rect = CellRect.FromCell(map.Center).ExpandedBy(45);
			}
			rect = rect.ExpandedBy(5);
            SpawnExteriorPrefabs(rect, map, points.RandomInRange);
			foreach(IntVec3 cell in rect.Cells)
			{
				if(map.terrainGrid.FoundationAt(cell) != null && cell.GetTerrain(map) == NATDefOf.NAT_RustedFloor)
				{
                    map.terrainGrid.RemoveFoundation(cell, false);
                }
			}
			if (spawnBeacon)
			{
                SpawnBeacon(rect, map);
            }
		}

		public void SpawnExteriorPrefabs(CellRect rect, Map map, float points)
		{
			List<IntVec3> cells = rect.Cells.ToList();
			IntVec3 cell = IntVec3.Invalid;
			PrefabOption prefab;
			int num = 100;
			while (num > 0 && points > 0)
			{
				prefab = prefabs.RandomElementByWeight((PrefabOption x) => x.weight);
				if (cells.TryRandomElement(Validator, out cell))
				{
					Rot4 opposite = rect.GetClosestEdge(cell).Opposite;
					points -= prefab.cost;
					PrefabUtility.SpawnPrefab(prefab.prefab, map, cell, opposite, Faction.OfEntities);
					foreach (IntVec3 c in CellRect.FromCell(cell).ExpandedBy(prefab.prefab.size.ToIntVec3.RotatedBy(opposite)))
					{
						if (cells.Contains(c))
						{
							cells.Remove(c);
						}
					}
				}
				num--;
				bool Validator(IntVec3 x)
				{
					Rot4 opposite2 = rect.GetClosestEdge(x).Opposite;
					if (x.GetRoof(map) == null && x.GetAffordances(map).Contains(TerrainAffordanceDefOf.Light))
					{
						return PrefabUtility.CanSpawnPrefab(prefab.prefab, map, x, opposite2, canWipeEdifices: prefab.canWipeEdifices);
					}
					return false;
				}
			}
		}

		public void SpawnBeacon(CellRect rect, Map map)
		{
            IntVec3 cell = IntVec3.Invalid;
			ThingDef def = DefDatabase<ThingDef>.GetNamed("ShardBeacon", false);
			if(def == null)
			{
				return;
			}
            if (!rect.Cells.TryRandomElement((IntVec3 c) => Validator(c, map, def, mustBeInRoom: true), out cell))
            {
                rect.Cells.TryRandomElement((IntVec3 c) => Validator(c, map, def), out cell);
            }
            Thing beacon = ThingMaker.MakeThing(def);
            beacon.SetFaction(Faction.OfEntities);
            GenSpawn.Spawn(beacon, cell, map);
        }

        private bool Validator(IntVec3 c, Map map, ThingDef def, bool mustBeInRoom = false)
        {
            if (!GenSpawn.CanSpawnAt(def, c, map))
            {
                return false;
            }
            if (c.DistanceToEdge(map) <= 2)
            {
                return false;
            }
            if ((mustBeInRoom && c.GetRoom(map) == null) || !c.GetRoom(map).ProperRoom)
            {
                return false;
            }
            if (!map.generatorDef.isUnderground && !map.reachability.CanReachMapEdge(c, TraverseMode.PassDoors))
            {
                return false;
            }
            return true;
        }
    }
}