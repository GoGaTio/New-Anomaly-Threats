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
	public class PrefabOption
	{
		public PrefabDef prefab;

		public float cost;

		public float weight;

		public bool canWipeEdifices = false;
	}
	public class RustedLayoutDef : StructureLayoutDef
	{
		public IntRange mergeRange = new IntRange(1, 4);

		public int corridorExpansion = 2;

		public SimpleCurve wallChanceFromLayerCurve;

		public SimpleCurve floorChanceFromLayerCurve;

		public List<PrefabOption> exteriorPrefabs = new List<PrefabOption>();

		public IntRange exteriorDoorsRange = new IntRange(1, 1);

		public SimpleCurve actualPointsFromPoints = new SimpleCurve
		{
			new CurvePoint(0f, 0f),
			new CurvePoint(10000f, 11000f)
		};
	}

	public class LayoutWorker_RustedBase : LayoutWorker_Structure
	{
		protected override float RoomToExteriorDoorRatio => 0f;

		protected override ThingDef ForceExteriorDoor => null;

		protected override bool CanConnectRoomsExternally => false;

		private static readonly List<Thing> tmpSpawnedThreatThings = new List<Thing>();

		public new RustedLayoutDef Def => (RustedLayoutDef)base.Def;

		public LayoutWorker_RustedBase(LayoutDef def)
			: base(def)
		{
		}

		protected override StructureLayout GetStructureLayout(StructureGenParams parms, CellRect rect)
		{
			LayoutStructureSketch sketch = parms.sketch;
			float areaPrunePercent = Def.areaPrunePercent;
			RoomLayoutParams roomParms = default(RoomLayoutParams);
			roomParms.sketch = sketch;
			roomParms.container = rect;
			roomParms.areaPrunePercent = areaPrunePercent;
			roomParms.minRoomHeight = Def.minRoomWidth;
			roomParms.minRoomWidth = Def.minRoomHeight;
			roomParms.canRemoveRooms = true;
			roomParms.generateDoors = false;
			roomParms.corridor = Def.corridorDef;
			roomParms.corridorExpansion = Def.corridorExpansion;
			roomParms.maxMergeRoomsRange = Def.mergeRange;
			roomParms.corridorShapes = Def.corridorShapes;
			roomParms.canDisconnectRooms = Def.canDisconnectRooms;
			roomParms.singleRoom = false;
			roomParms.entranceCount = 0;
			return RoomLayoutGenerator.GenerateRandomLayout(minRoomWidth: Def.minRoomWidth, minRoomHeight: Def.minRoomHeight, areaPrunePercent: areaPrunePercent, canRemoveRooms: true, generateDoors: false, maxMergeRoomsRange: Def.mergeRange, sketch: sketch, container: rect, corridor: Def.corridorDef, corridorExpansion: Def.corridorExpansion, corridorShapes: Def.corridorShapes, canDisconnectRooms: Def.canDisconnectRooms);
		}

		public override void Spawn(LayoutStructureSketch layoutStructureSketch, Map map, IntVec3 pos, float? threatPoints = null, List<Thing> allSpawnedThings = null, bool roofs = true, bool canReuseSketch = false, Faction faction = null)
		{
			faction = Faction.OfEntities;
			float points = (threatPoints ?? StorytellerUtility.DefaultSiteThreatPointsNow());
			Log.Message("NAT - Exterior prefabs points: " + Def.actualPointsFromPoints.Evaluate(points) + "/" + points.ToString());
			points = Def.actualPointsFromPoints.Evaluate(points);
			IntVec3 offset = ((!layoutStructureSketch.spawned) ? pos : (pos - layoutStructureSketch.center));
			foreach(LayoutRoom r in layoutStructureSketch.structureLayout.Rooms)
            {
				Thing.allowDestroyNonDestroyable = true;
				try
				{
					foreach (IntVec3 c in r.Cells)
					{
						IntVec3 cell = c + offset;
						map.terrainGrid.SetTerrain(cell, Def.terrainDef);
						foreach (Thing item in cell.GetThingList(map).ToList())
						{
							item.Destroy();
						}
					}
				}
				finally
				{
					Thing.allowDestroyNonDestroyable = false;
				}
            }
			List<Thing> list = allSpawnedThings ?? new List<Thing>();
			base.Spawn(layoutStructureSketch, map, pos, threatPoints, list, roofs, canReuseSketch, faction);
			MakeOuterPart(layoutStructureSketch, map, ref points);
			PlaceDoorsIfNotThere(layoutStructureSketch, map);
			List<LayoutRoom> rooms = layoutStructureSketch.structureLayout.Rooms;
			//SpawnThings(layoutStructureSketch, map, rooms, list);
			tmpSpawnedThreatThings.Clear();
			
		}

		public void MakeOuterPart(LayoutStructureSketch layoutStructureSketch, Map map, ref float points)
		{
			int seed = Rand.Int;
			List<IntVec3> doors = new List<IntVec3>();
			List<List<IntVec3>> list = new List<List<IntVec3>>();
			List<IntVec3> edgeCells = new List<IntVec3>();
			List<IntVec3> cells = new List<IntVec3>();
			int num = Mathf.Max(Def.wallChanceFromLayerCurve.PointsCount, Def.floorChanceFromLayerCurve.PointsCount);
			for (int i = 0; i < num; i++)
			{
				list.Add(new List<IntVec3>());
			}
			foreach (LayoutRoom room in layoutStructureSketch.structureLayout.Rooms)
			{
				foreach (CellRect r in room.rects)
				{
					for (int j = 0; j < num; j++)
					{
						list[j].AddRange(r.ExpandedBy(j + 1).Cells);
					}
					cells.AddRange(r.Cells);
					//edgeCells.AddRange(r.EdgeCells);
				}
			}
			for (int k = (num - 1); k >= 0; k--)
			{
				if (k == 0)
				{
					list[k] = list[k].Except(cells).ToList();
				}
				else
				{
					list[k] = list[k].Except(list[k - 1]).ToList();
				}
			}
			List<IntVec3> floor = new List<IntVec3>();
			List<IntVec3> walls = new List<IntVec3>();
			for (int m = 0; m < Def.floorChanceFromLayerCurve.PointsCount; m++)
			{
				float chance = Def.floorChanceFromLayerCurve[m].y;
				foreach (IntVec3 c in list[m])
				{
					if (Rand.ChanceSeeded(chance, seed))
					{
						map.terrainGrid.SetTerrain(c, Def.terrainDef);
						floor.Add(c);
					}
					seed += m;
				}
			}
			for (int n = 0; n < Def.wallChanceFromLayerCurve.PointsCount; n++)
			{
				float chance = Def.wallChanceFromLayerCurve[n].y;
				foreach (IntVec3 c in list[n])
				{
					if (Rand.ChanceSeeded(chance, seed))
					{
						TrySpawnWall(c, map);
						walls.Add(c);
					}
					seed += n;
				}
			}

			SpawnEntrances(layoutStructureSketch, walls, map);
			SpawnExteriorPrefabs(layoutStructureSketch.center, floor.Except(walls), map, ref points);
		}

		public void SpawnEntrances(LayoutStructureSketch layoutStructureSketch, List<IntVec3> cells, Map map)
        {
			int count = Def.exteriorDoorsRange.RandomInRange;
			int num = 0;
			int num2 = 100;
			List<LayoutRoom> list = new List<LayoutRoom>();
			List<IntVec3> edge = layoutStructureSketch.structureLayout.container.EdgeCells.ToList();
			while (num < count && num2 > 0)
            {
				if(layoutStructureSketch.structureLayout.Rooms.InRandomOrder().TryRandomElement((LayoutRoom x) => !list.Contains(x) && !x.HasLayoutDef(NATDefOf.NAT_CitadelCorridor) && !x.HasLayoutDef(NATDefOf.NAT_OutpostCorridor) && x.Corners.Any((IntVec3 y)=> edge.Contains(y)), out var room))
                {
					list.Add(room);
					CellRect rect = FindDoorRect(room, edge, out var rot);
					if(rect == CellRect.Empty || rect.ExpandedBy(1).Count((IntVec3 c)=>c.GetEdifice(map) == null) < 2)
                    {
						continue;
                    }
					if(rect.Count() != 2)
                    {
						Log.Error("NAT - For some reason door rect has more than 2 cells");
						foreach(IntVec3 k in rect)
                        {
							map.terrainGrid.SetTerrain(k, TerrainDefOf.Concrete);
							map.terrainGrid.SetTerrainColor(k, NATColorDefOf.Structure_Red);
						}
                    }
					foreach (IntVec3 c in rect.Cells.Concat(rect.ExpandedBy(rot == Rot4.South ? 1 : Def.wallChanceFromLayerCurve.Count(), rot == Rot4.East ? 1 : Def.wallChanceFromLayerCurve.Count()).Cells.Except(layoutStructureSketch.structureLayout.container)))
                    {
						List<Thing> thingList = c.GetThingList(map);
						for (int i = thingList.Count - 1; i >= 0; i--)
						{
							thingList[i].Destroy();
						}
					}
					Thing thing = ThingMaker.MakeThing(NATDefOf.NAT_RustedDoor_Double);
					thing.SetFaction(Faction.OfEntities);
					GenSpawn.Spawn(thing, rect.CenterCell, map, rot);
					num++;
					if(RCellFinder.) MapGenerator.PlayerStartSpot
				}
                else
                {
					break;
                }
				num2--;
			}
		}

		public CellRect FindDoorRect(LayoutRoom room, List<IntVec3> edge, out Rot4 rot)
        {
			rot = Rot4.Invalid;
			List<IntVec3> points = new List<IntVec3>();
			foreach (CellRect r in room.rects)
			{
				points.Clear();
				foreach (IntVec3 corner in r.Corners)
				{
					if (edge.Contains(corner))
					{
						points.Add(corner);
					}
				}
				if (points.Count > 1)
				{
					break;
				}
			}
			IntVec3 a = points[0];
			IntVec3 b = points[1];
			if (points.Count > 2)
            {
				bool stop = false;
				for(int i = 0; i < points.Count; i++)
                {
                    if (stop)
                    {
						break;
                    }
					for (int j = 0; j < points.Count; j++)
					{
						if(points[i].z == points[j].z || points[i].x == points[j].x)
                        {
							a = points[i];
							b = points[j];
							points = new List<IntVec3>() { a, b };
							stop = true;
							break;
                        }
					}
				}
				a = points[0];
				b = points[1];
			}
			if(a.DistanceTo(b) <= 4)
            {
				return CellRect.Empty;
			}
			if(a.z == b.z)
            {
				rot = Rot4.South;
				return new CellRect(Rand.RangeInclusive(Mathf.Min(a.x, a.x) + 1, Mathf.Max(a.x, a.x) - 2), a.z, 2, 1);
			}
            else
            {
				rot = Rot4.East;
				return new CellRect(a.x, Rand.RangeInclusive(Mathf.Min(a.z, a.z) + 1, Mathf.Max(a.z, a.z) - 2), 1, 2);
			}
		}

		public void SpawnExteriorPrefabs(IntVec3 center, IEnumerable<IntVec3> cells, Map map, ref float points)
        {
            if (Def.exteriorPrefabs.NullOrEmpty())
            {
				return;
            }
			CellRect rect = new CellRect(center.x, center.z, 1, 1);
			float flag = points * 0.7f;
			int num = 100;
			IntVec3 cell = IntVec3.Invalid;
			PrefabOption exteriorPrefab;
			while (points > flag && num > 0)
            {
				exteriorPrefab = Def.exteriorPrefabs.RandomElementByWeight((PrefabOption x) => x.weight);
				if (cells.TryRandomElement(Validator, out cell))
				{
					Rot4 opposite = rect.GetClosestEdge(cell).Opposite;
					points -= exteriorPrefab.cost;
					PrefabUtility.SpawnPrefab(exteriorPrefab.prefab, map, cell, opposite, Faction.OfEntities);
				}
				num--;
				bool Validator(IntVec3 x)
				{
					Rot4 opposite2 = rect.GetClosestEdge(x).Opposite;
					if (x.GetRoof(map) == null && x.GetAffordances(map).Contains(TerrainAffordanceDefOf.Light))
					{
						return PrefabUtility.CanSpawnPrefab(exteriorPrefab.prefab, map, x, opposite2, canWipeEdifices: false);
					}
					return false;
				}
			}
		}

		public bool AdjacentTo(IntVec3 first, IntVec3 second)
		{
			if (first.x == second.x)
			{
				return Mathf.Abs(first.z - second.z) == 1;
			}
			if (first.z == second.z)
			{
				return Mathf.Abs(first.x - second.x) == 1;
			}
			return false;
		}

		private void TrySpawnWall(IntVec3 c, Map map)
		{
			List<Thing> thingList = c.GetThingList(map);
			for (int num = thingList.Count - 1; num >= 0; num--)
			{
				thingList[num].Destroy();
			}
			Thing thing = ThingMaker.MakeThing(NATDefOf.NAT_RustedWall, null);
			thing.SetFaction(Faction.OfEntities);
			GenSpawn.Spawn(thing, c, map);
		}

		private void TrySpawnDoor(IntVec3 c, Map map)
		{
			List<Thing> thingList = c.GetThingList(map);
			for (int num = thingList.Count - 1; num >= 0; num--)
			{
				thingList[num].Destroy();
			}
			Thing thing = ThingMaker.MakeThing(NATDefOf.NAT_RustedDoor);
			thing.SetFaction(Faction.OfEntities);
			GenSpawn.Spawn(thing, c, map);
		}

		public void PlaceDoorsIfNotThere(LayoutStructureSketch layoutStructureSketch, Map map)
		{
			StructureLayout layout = layoutStructureSketch.structureLayout;
			foreach (LayoutRoom room in layoutStructureSketch.structureLayout.Rooms)
			{
				if (!room.rects.Any((CellRect x) => x.EdgeCells.Any((IntVec3 c) => c.GetFirstThing(map, NATDefOf.NAT_RustedWall) == null)))
				{
					Log.Message("Missing door in room at " + room.Boundary.CenterCell.ToString());
					TrySpawnDoor(room.rects.SelectMany((CellRect y) => y.EdgeCellsNoCorners).InRandomOrder().FirstOrDefault((IntVec3 p) => (layout.TryGetRoom(p + IntVec3.North, out var r) && r != room && r != null) || (layout.TryGetRoom(p + IntVec3.West, out var r2) && r2 != null && r2 != room ) || (layout.TryGetRoom(p + IntVec3.East, out var r3) && r3 != null && r3 != room) || (layout.TryGetRoom(p + IntVec3.South, out var r4) && r4 != null && r4 != room)), map);
				}
			}
		}
	}
}