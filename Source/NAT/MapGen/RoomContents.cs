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
	public class RoomContents_RustedRegular : RoomContentsWorker
	{
		public override void FillRoom(Map map, LayoutRoom room, Faction faction, float? threatPoints = null)
		{
			base.FillRoom(map, room, faction, threatPoints);
			//List<IntVec3> edgeCells = new List<IntVec3>();
			//foreach (CellRect r in room.rects)
			//{
			//	edgeCells.AddRange(r.ContractedBy(1).EdgeCells);
			//}
			//int num = Mathf.CeilToInt((float)(edgeCells.Count) / 30f);
			//Log.Message(num);
			//SpawnThings(NATDefOf.NAT_RustedTurret_Foam, edgeCells, map, num);
		}

		public static void SpawnThings(ThingDef def, List<IntVec3> cells, Map map, int amount = 1)
        {
			IntVec3 cell = new IntVec3();
			for (int i = 0; i < amount; i++)
			{
				if (cells.TryRandomElement((IntVec3 x) => !RoomGenUtility.IsDoorAdjacentTo(x, map), out cell))
				{
					SpawnThingRandom(def, cell, map);
				}
				else
				{
					break;
				}
			}
		}

		public static void SpawnThingRandom(ThingDef def, IntVec3 center, Map map)
		{
			Thing thing = ThingMaker.MakeThing(def);
			thing.SetFaction(Faction.OfEntities);
			CellRect rect = new CellRect(center.x - 1, center.z - 1, 3, 3);
			if (rect.Cells.TryRandomElement((IntVec3 x) => CanSpawnAt(def, x, map), out var cell))
			{
				GenSpawn.Spawn(thing, cell, map);
			}
		}

		public static bool CanSpawnAt(ThingDef thingDef, IntVec3 c, Map map)
		{
			Rot4 rot = thingDef.defaultPlacingRot;
			if (!thingDef.CanSpawnAt(c, rot, map))
			{
				return false;
			}
			foreach (IntVec3 item in GenAdj.OccupiedRect(c, rot, thingDef.Size))
			{
				if (!item.InBounds(map))
				{
					return false;
				}
				if (!c.Walkable(map))
				{
					return false;
				}
				if (map.edificeGrid[item] != null)
				{
					return false;
				}
				foreach (Thing thing in c.GetThingList(map))
				{
					if (GenSpawn.SpawningWipes(thingDef, thing.def, ignoreDestroyable: false))
					{
						return false;
					}
				}
			}
			return true;
		}
	}

	public class RoomContents_CitadelCorridor : RoomContents_RustedRegular
	{
		public override void FillRoom(Map map, LayoutRoom room, Faction faction, float? threatPoints = null)
		{
			base.FillRoom(map, room, faction, threatPoints);
			List<IntVec3> list = new List<IntVec3>();
			CellRect mainRect = room.rects.MaxBy((CellRect x) => x.Area);
			foreach (CellRect rect in room.rects)
			{
				list.Add(rect.ClipInsideRect(mainRect).CenterCell);
			}
			list.Shuffle();
			for (int i = 0; i < 2; i++)
			{
				SpawnThingRandom(NATDefOf.NAT_RustedTurret_Sniper, list[i], map);
			}
		}
	}

	public class RoomContents_HarbingerGarden : RoomContents_RustedRegular
	{
		public override void FillRoom(Map map, LayoutRoom room, Faction faction, float? threatPoints = null)
		{
			base.FillRoom(map, room, faction, threatPoints);
			List<IntVec3> list = new List<IntVec3>();
			CellRect mainRect = room.rects.MaxBy((CellRect x) => x.Area);
			foreach (CellRect rect in room.rects)
			{
				list.Add(rect.ClipInsideRect(mainRect).CenterCell);
			}
			list.Shuffle();
			for (int i = 0; i < 2; i++)
			{
				SpawnThingRandom(NATDefOf.NAT_RustedTurret_Sniper, list[i], map);
			}
		}
	}
}