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
	public class GenStep_RustedPoint : GenStep
	{
		public int rectSize = 33;

		public int rustsRectSize = 33;

		public List<PrefabOption> mainPlatformPrefabs = new List<PrefabOption>();

		public List<PrefabOption> cornerPrefabs = new List<PrefabOption>();

		public PrefabDef centerDef;

		public FloatRange fixedPoints;

		public override int SeedPart => 729156151;

		public override void Generate(Map map, GenStepParams parms)
        {
			IntVec3 center = map.Center;
			List<CellRect> list = new List<CellRect>();
			Rot4 rot = Rot4.North;
			List<IntVec3> list1 = CellRect.CenteredOn(center, rectSize, rectSize).Corners.ToList();
			List<IntVec3> list2 = CellRect.CenteredOn(center, rustsRectSize, rustsRectSize).Corners.ToList();
			for (int i = 0; i < list1.Count; i++)
			{
				List<Thing> things = new List<Thing>();
				PrefabUtility.SpawnPrefab(cornerPrefabs.RandomElementByWeight((x) => x.weight).prefab, map, list1[i], rot, Faction.OfEntities, things);
				rot.Rotate(RotationDirection.Clockwise);
				GenerateRusts(map, list2[i], things);
			}
			PrefabUtility.SpawnPrefab(centerDef, map, center, Rot4.North, Faction.OfEntities);
		}

		public void GenerateRusts(Map map, IntVec3 cell, List<Thing> things)
		{
			List<Pawn> list = PawnGroupMakerUtility.GeneratePawns(new PawnGroupMakerParms
			{
				groupKind = NATDefOf.NAT_RustedArmyDefence,
				points = fixedPoints.RandomInRange,
				faction = Faction.OfEntities
			}).ToList();
			CellRect rect = CellRect.CenteredOn(cell, rustsRectSize / 3);
			Lord lord = LordMaker.MakeNewLord(Faction.OfEntities, new LordJob_DefendRust(cell, rustsRectSize / 3, false, false, false, false, "NAT_Rusts"), map, list);
			List<IntVec3> cells = new List<IntVec3>();
			foreach (Pawn p in list)
			{
				if (rect.TryFindRandomCell(out var c, (x) => !cells.Contains(x)))
				{
					cells.Add(c);
					GenPlace.TryPlaceThing(p, c, map, ThingPlaceMode.Near);
				}
			}
			foreach(Thing t in things)
			{
				if(t is Building_Turret b)
				{
					lord.AddBuilding(b);
				}
			}
		}
	}
}