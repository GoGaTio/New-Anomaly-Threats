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
	public class GenStep_AncientRuinSingle : GenStep_BaseRuins
	{
		private LayoutDef layoutDef;

		private static readonly FloatRange BlastMarksPer10K = new FloatRange(2f, 6f);

		public override int SeedPart => 9164521;

		protected override LayoutDef LayoutDef => layoutDef;

		protected override int RegionSize => 45;

		protected override FloatRange DefaultMapFillPercentRange => new FloatRange(0.15f, 0.3f);

		protected override FloatRange MergeRange => new FloatRange(0.1f, 0.35f);

		protected override int MoveRangeLimit => 3;

		protected override int ContractLimit => 3;

		protected override int MinRegionSize => 14;

		protected override Faction Faction => null;

		protected override IEnumerable<CellRect> GetRects(CellRect area, Map map)
		{
			yield return area;
		}

		protected override CellRect GetBounds(Map map)
		{
			return map.Center.RectAbout(50, 60, Rot4.Random);
		}

		public override void GenerateRuins(Map map, GenStepParams parms, FloatRange mapFillPercentRange)
		{
			base.GenerateRuins(map, parms, mapFillPercentRange);
			MapGenUtility.SpawnScatter(map, ThingDefOf.Filth_BlastMark, BlastMarksPer10K);
		}
	}
	public class GenStep_UndergroundEntrance : GenStep
	{
		public ThingDef entranceDef;

		public bool trySpawnInRoom;

		public bool trySpawnInSettlement;

		public bool trySpawnInRect;

		public override int SeedPart => 1234731256;

		public override void Generate(Map map, GenStepParams parms)
		{
			IntVec3 cell = IntVec3.Invalid;
			if ((trySpawnInSettlement && MapGenerator.TryGetVar<CellRect>("SettlementRect", out var rect)) || (trySpawnInRect && (rect = MapGenerator.UsedRects.Last()) != null))
			{
				if (!trySpawnInRoom || !rect.TryFindRandomCell(out cell, (IntVec3 c) => Validator(c, map, mustBeInRoom: true)))
				{
					rect.TryFindRandomCell(out cell, (IntVec3 c) => Validator(c, map, mustBeInRoom: false));
				}
			}
			else if (trySpawnInRoom)
			{
				CellFinder.TryFindRandomCell(map, (IntVec3 c) => Validator(c, map, mustBeInRoom: true), out cell);
			}
			if (!cell.IsValid)
            {
				CellFinder.TryFindRandomCell(map, (IntVec3 c) => Validator(c, map, mustBeInRoom: false), out cell);
			}
			int tick = Find.TickManager.TicksGame;
			foreach (IntVec3 c in CellRect.FromCell(cell).ExpandedBy(1).Cells)
			{
				map.terrainGrid.SetTerrain(c, TerrainDefOf.AncientConcrete);
			}
			foreach (IntVec3 c in CellRect.FromCell(cell).ExpandedBy(2).EdgeCells)
            {
				if(Rand.ChanceSeeded(0.9f, tick))
                {
					map.terrainGrid.SetTerrain(c, TerrainDefOf.AncientConcrete);
				}
				tick += c.z * c.x;
            }
			Thing entrance = ThingMaker.MakeThing(entranceDef);
			entrance.SetFaction(Faction.OfEntities);
			GenSpawn.Spawn(entrance, cell, map);
		}

		private bool Validator(IntVec3 c, Map map, bool mustBeInRoom)
		{
			if (c.DistanceToEdge(map) <= 3)
			{
				return false;
			}
			if (mustBeInRoom && (c.GetRoom(map) == null || !c.GetRoom(map).ProperRoom))
			{
				return false;
			}
			if (!map.generatorDef.isUnderground && !map.reachability.CanReachMapEdge(c, TraverseMode.PassDoors))
			{
				return false;
			}
			if (CellRect.FromCell(c).ExpandedBy(1).Cells.Any((x) => x.Impassable(map)))
			{
				return false;
			}
			return true;
		}
	}

	public class GenStep_UndergroundLayout : GenStep
	{
		public IntRange sizeRange = new IntRange(60, 70);

		public override int SeedPart => 92548734;

		public override void Generate(Map map, GenStepParams parms)
		{
			CellRect cellRect = map.Center.RectAbout(new IntVec2(sizeRange.RandomInRange, sizeRange.RandomInRange));
			StructureGenParams parms2 = new StructureGenParams
			{
				size = cellRect.Size
			};
			LayoutWorker obj = parms.layout?.Worker;
			LayoutStructureSketch layoutStructureSketch = obj.GenerateStructureSketch(parms2);
			map.layoutStructureSketches.Add(layoutStructureSketch);
			float? threatPoints = null;
			if (parms.sitePart != null)
			{
				threatPoints = parms.sitePart.parms.points;
			}
			obj.Spawn(layoutStructureSketch, map, cellRect.Min, threatPoints);
		}
	}
}