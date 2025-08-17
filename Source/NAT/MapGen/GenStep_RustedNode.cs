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
	public class GenStep_RustedNode : GenStep
	{
		public IntRange mainPlatformSizeRange = new IntRange(30, 40);

		public IntRange platformSizeRange = new IntRange(5, 10);

		public IntRange platformCountRange = new IntRange(3, 7);

		public SimpleCurve floorChanceFromLayerCurve;

		public List<PrefabOption> mainPlatformPrefabs = new List<PrefabOption>();

		public List<PrefabOption> prefabs = new List<PrefabOption>();

		public FloatRange fixedPoints;

		public override int SeedPart => 829256151;

		public override void Generate(Map map, GenStepParams parms)
        {
			int seed = Rand.Int;
			map.OrbitalDebris = NATDefOf.NAT_RustedDebris;
			List<CellRect> list = new List<CellRect>();
			CellRect rect = CellRect.CenteredOn(map.Center, mainPlatformSizeRange.RandomInRange, mainPlatformSizeRange.RandomInRange);
			list.Add(rect);
			for (int i = platformCountRange.RandomInRange; i > 0; i--)
            {
				list.Add(CellRect.CenteredOn(rect.EdgeCells.RandomElement(), platformSizeRange.RandomInRange, platformSizeRange.RandomInRange));
			}
			foreach (CellRect r in list)
			{
				for (int j = 0; j < floorChanceFromLayerCurve.PointsCount; j++)
				{
					float chance = floorChanceFromLayerCurve[j].y;
					foreach(IntVec3 c in j == 0 ? r.Cells : r.ExpandedBy(j).EdgeCells)
                    {
						if (Rand.ChanceSeeded(chance, seed))
						{
							map.terrainGrid.SetTerrain(c, NATDefOf.NAT_RustedFloor);
						}
						seed += j;
					}
				}
			}
			float points = fixedPoints.RandomInRange;
			PrefabUtility.SpawnPrefab(NATDefOf.NAT_RustedDish, map, rect.CenterCell, Rot4.South, Faction.OfEntities);
			SpawnMainPrefabs(rect, map, ref points);
			SpawnExteriorPrefabs(list, map, ref points);
		}

		public void SpawnExteriorPrefabs(List<CellRect> rects, Map map, ref float points)
		{
			List<IntVec3> cells = new List<IntVec3>();
			for (int i = 1; i < rects.Count; i++)
			{
				cells.AddRange(rects[i].Cells);
			}
			IntVec3 cell = IntVec3.Invalid;
			PrefabOption prefab;
			int num = 100;
			while (num > 0 && points > 0)
			{
				prefab = prefabs.RandomElementByWeight((PrefabOption x) => x.weight);
				if (cells.TryRandomElement(Validator, out cell))
				{
					Rot4 opposite = rects[0].GetClosestEdge(cell).Opposite;
					points -= prefab.cost;
					PrefabUtility.SpawnPrefab(prefab.prefab, map, cell, opposite, Faction.OfEntities);
				}
				num--;
				bool Validator(IntVec3 x)
				{
					Rot4 opposite2 = rects[0].GetClosestEdge(x).Opposite;
					if (x.GetRoof(map) == null && x.GetAffordances(map).Contains(TerrainAffordanceDefOf.Light))
					{
						return PrefabUtility.CanSpawnPrefab(prefab.prefab, map, x, opposite2, canWipeEdifices: false);
					}
					return false;
				}
			}
		}

		public void SpawnMainPrefabs(CellRect rect, Map map, ref float points)
		{
			IntVec3 cell = IntVec3.Invalid;
			PrefabOption prefab;
			int num = 100;
			while (num > 0 && points > 0)
			{
				prefab = mainPlatformPrefabs.RandomElementByWeight((PrefabOption x) => x.weight);
				if (rect.Cells.TryRandomElement(Validator, out cell))
				{
					Rot4 opposite = rect.GetClosestEdge(cell).Opposite;
					points -= prefab.cost;
					PrefabUtility.SpawnPrefab(prefab.prefab, map, cell, opposite, Faction.OfEntities);
				}
				num--;
				bool Validator(IntVec3 x)
				{
					Rot4 opposite2 = rect.GetClosestEdge(x).Opposite;
					if (x.GetRoof(map) == null && x.GetAffordances(map).Contains(TerrainAffordanceDefOf.Light))
					{
						return PrefabUtility.CanSpawnPrefab(prefab.prefab, map, x, opposite2, canWipeEdifices: false);
					}
					return false;
				}
			}
		}
	}
}