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
	public class GenStep_RustedPlatform : GenStep_RustedBase
	{
		public IntVec2 minPlatformSize = new IntVec2(40, 40);

		public IntVec2 maxPlatformSize = new IntVec2(40, 40);

		public IntRange platformCountRange = new IntRange(40, 40);

		//public override int SeedPart => 829256151;

        public override LayoutStructureSketch GenerateAndSpawn(CellRect rect, Map map, GenStepParams parms, LayoutDef layoutDef)
        {
			map.OrbitalDebris = NATDefOf.NAT_RustedDebris;

			return base.GenerateAndSpawn(rect, map, parms, layoutDef);
        }

		private void SpawnPlatforms(CellRect rect, Map map, GenStepParams parms)
        {
			CellRect inRect = rect.ContractedBy(1);
			CellRect outRect = rect.ExpandedBy(Mathf.Max(maxPlatformSize.x, maxPlatformSize.z));
			for(int i = 0; i < 3; i++)
            {

			}
		}

		/*private void SpawnExteriorPrefabs(Map map, CellRect rect, Faction faction)
		{
			foreach (PrefabRange exteriorPrefab in exteriorPrefabs)
			{
				PrefabRange set = exteriorPrefab;
				int randomInRange = set.countRange.RandomInRange;
				for (int i = 0; i < randomInRange; i++)
				{
					if (rect.TryFindRandomCell(out var cell, Validator))
					{
						Rot4 opposite = rect.GetClosestEdge(cell).Opposite;
						PrefabUtility.SpawnPrefab(set.prefab, map, cell, opposite, faction);
					}
				}
				bool Validator(IntVec3 x)
				{
					Rot4 opposite2 = rect.GetClosestEdge(x).Opposite;
					if (x.GetRoof(map) == null && x.GetAffordances(map).Contains(TerrainAffordanceDefOf.Medium) && rect.DistanceToEdge(x) <= 5f)
					{
						return PrefabUtility.CanSpawnPrefab(set.prefab, map, x, opposite2, canWipeEdifices: false);
					}
					return false;
				}
			}
		}*/

		public override void PostMapInitialized(Map map, GenStepParams parms)
		{
			MapGenUtility.SetMapRoomTemperature(map, layout, -75f);
		}
	}
}