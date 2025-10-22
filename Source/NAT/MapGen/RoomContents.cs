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
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using System.Xml.XPath;
using System.Xml.Xsl;
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
	public class RoomContents_RustedOutpost : RoomContentsWorker
	{
		public override void FillRoom(Map map, LayoutRoom room, Faction faction, float? threatPoints = null)
		{
			base.FillRoom(map, room, faction, threatPoints);
			if (room.TryGetRandomCellInRoom(NATDefOf.NAT_RustedTurret_Auto, map, out var cell, NATDefOf.NAT_RustedTurret_Auto.defaultPlacingRot, 3, 1))
			{
				List<Pawn> list = PawnGroupMakerUtility.GeneratePawns(new PawnGroupMakerParms
				{
					groupKind = NATDefOf.NAT_RustedArmyDefence,
					points = new FloatRange(300f, 500f).RandomInRange,
					faction = Faction.OfEntities
				}).ToList();
				CellRect rect = new CellRect(cell.x, cell.z, 2, 2).ExpandedBy(1);
				Lord lord = LordMaker.MakeNewLord(Faction.OfEntities, new LordJob_DefendRust(cell, 5f, true), map, list);
				PrefabUtility.SpawnPrefab(NATDefOf.NAT_RustedAutoTurretLabyrinth, map, cell, NATDefOf.NAT_RustedTurret_Auto.defaultPlacingRot, Faction.OfEntities, null, null , delegate(Thing t)
				{
					if(t.TryGetComp<CompCanBeDormant>(out var comp))
					{
						comp.ToSleep();
					}
					if(t is Building b)
					{
						lord.AddBuilding(b);
					}
				});
				if (!map.generatorDef.isUnderground)
				{
					DropSpawnNear(NATDefOf.NAT_RustedBeacon_Reinforcements, rect.RandomCell, map, 1, lord);
				}
				DropSpawnNear(NATDefOf.NAT_RustedArmyBanner, rect.RandomCell, map, 1, lord);
				DropSpawnNear(NATDefOf.NAT_RustedPallet, rect.RandomCell, map, 1, lord);
				for (int i = 0; i < list.Count; i++)
				{
					GenDrop.TryDropSpawn(list[i], rect.EdgeCells.RandomElement(), map, ThingPlaceMode.Near, out var _);
				}
			}
		}

		private void DropSpawnNear(ThingDef thing, IntVec3 cell, Map map, int amount = 1, Lord lord = null)
		{
			for (int i = 0; i < amount; i++)
			{
				if(GenDrop.TryDropSpawn(ThingMaker.MakeThing(thing), cell, map, ThingPlaceMode.Near, out var t) && lord != null && t is Building b)
				{
					lord.AddBuilding(b);
				}
			}
		}
	}



	public class RoomContents_CollectorLairBedroom : RoomContentsWorker
	{
		private static readonly IntRange TurretsRange = new IntRange(1, 2);

		public override void FillRoom(Map map, LayoutRoom room, Faction faction, float? threatPoints = null)
		{
			base.FillRoom(map, room, faction, threatPoints);
			SignalAction_Sightstealers signalAction_Ambush = (SignalAction_Sightstealers)ThingMaker.MakeThing(NATDefOf.NAT_SignalAction_Sightstealers);
			signalAction_Ambush.points = new FloatRange(140f, 420f).RandomInRange;
			signalAction_Ambush.spawnAround = room.rects[0];
			GenSpawn.Spawn(signalAction_Ambush, room.rects[0].CenterCell, map);
		}

		
	}

	public class RoomContents_CollectorLairEntrance : RoomContentsWorker
    {
        private static readonly IntRange TurretsRange = new IntRange(1, 2);

        public override void FillRoom(Map map, LayoutRoom room, Faction faction, float? threatPoints = null)
        {
            SpawnExit(map, room);
            base.FillRoom(map, room, faction, threatPoints);
        }

        private void SpawnExit(Map map, LayoutRoom room)
        {
            List<Thing> list = new List<Thing>();
            ThingDef exit = NATDefOf.NAT_CollectorLairExit;
            List<Thing> spawned = list;
            RoomGenUtility.FillWithPadding(exit, 1, room, map, null, null, spawned, 3);
            MapGenerator.PlayerStartSpot = list.First().Position;
        }
    }

    public class RoomContents_RustedRegular : RoomContentsWorker
	{
		public override void FillRoom(Map map, LayoutRoom room, Faction faction, float? threatPoints = null)
		{
			base.FillRoom(map, room, faction, threatPoints);
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
		public static FloatRange GrowthRange = new FloatRange(0.15f, 0.85f);
		public override void FillRoom(Map map, LayoutRoom room, Faction faction, float? threatPoints = null)
		{
			base.FillRoom(map, room, faction, threatPoints);
			foreach (CellRect rect in room.rects)
			{
				List<IntVec3> cells = rect.ContractedBy(2).ToList();
				List<Thing> list = new List<Thing>();
				foreach (IntVec3 cell in cells.InRandomOrder())
				{
					map.terrainGrid.SetTerrain(cell, TerrainDefOf.Soil);
					map.roofGrid.SetRoof(cell, null);
					if (Rand.Bool)
					{
						continue;
					}
					if (list.Any((Thing p) => p.def.plant.blockAdjacentSow && cell.IsAdjacentToCardinalOrInside(p.OccupiedRect())))
					{
						continue;
					}
					Plant plant = (Plant)ThingMaker.MakeThing(ThingDefOf.Plant_TreeHarbinger);
					plant.Growth = GrowthRange.RandomInRange;
					list.Add(plant);
					GenSpawn.Spawn(plant, cell, map);
				}
			}
		}
	}
	public class RoomContents_RustedPawns : RoomContents_RustedRegular
	{
		public virtual PawnGroupKindDef groupKindDef => NATDefOf.NAT_RustedArmyDefence;

		public virtual bool Sleep => false;

		public virtual bool ForceWakeUp => false;

		public virtual float WanderRadius(LayoutRoom room)
		{
			CellRect rect = room.Boundary;
			return Mathf.Max(rect.Height / 2f, rect.Width / 2f);
		}

		public override void FillRoom(Map map, LayoutRoom room, Faction faction, float? threatPoints = null)
		{
			base.FillRoom(map, room, faction, threatPoints);
			List<Pawn> list = GeneratePawns();
			if (Sleep)
			{
				foreach (Pawn p in list)
				{
					p.canBeDormant.ToSleep();
				}
			}
			Lord lord = LordMaker.MakeNewLord(Faction.OfEntities, new LordJob_DefendRust(room.Boundary.CenterCell, WanderRadius(room), Sleep, true, false, ForceWakeUp, "NAT_Rusts"), map, list);
			List<IntVec3> cells = new List<IntVec3>();
			foreach (Pawn p in list)
			{
				if (room.TryGetRandomCellInRoom(out var cell, 2, (c) => !cells.Contains(c)))
				{
					cells.Add(cell);
					GenPlace.TryPlaceThing(p, cell, map, ThingPlaceMode.Near);
				}
			}
		}

		public virtual List<Pawn> GeneratePawns() => PawnGroupMakerUtility.GeneratePawns(new PawnGroupMakerParms
		{
			groupKind = groupKindDef,
			points = new FloatRange(300f, 500f).RandomInRange,
			faction = Faction.OfEntities
		}).ToList();
	}

	public class RoomContents_RustedBarracks : RoomContents_RustedPawns
	{
		public override PawnGroupKindDef groupKindDef => NATDefOf.NAT_RustedArmyBarracks;

		public override bool Sleep => true;

	}

	public class RoomContents_RustedBedroom : RoomContents_RustedPawns
	{
		public override bool Sleep => true;

		public override bool ForceWakeUp => true;

		public override List<Pawn> GeneratePawns()
		{
			List<Pawn> list = new List<Pawn>();
			for(int i = 0; i < new IntRange(2, 3).RandomInRange; i++)
			{
				Pawn rust = PawnGenerator.GeneratePawn(NATDefOf.NAT_RustedOfficer, Faction.OfEntities);
				list.Add(rust);
			}
			return list;
		}
	}

}