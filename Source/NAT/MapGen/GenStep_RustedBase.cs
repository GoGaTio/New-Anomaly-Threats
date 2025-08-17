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
	public class GenStep_RustedBase : GenStep
	{
		public RustedLayoutDef layout;

		public IntVec2 minBaseSize = new IntVec2(40, 40);

		public IntVec2 maxBaseSize = new IntVec2(40, 40);

		public override int SeedPart => 235635049;

		public static Faction defeatedFaction = null;

		public override void Generate(Map map, GenStepParams parms)
		{
			defeatedFaction = parms.sitePart.site.Faction;
			CellRect mapRect = CellRect.WholeMap(map);
			CellRect mainRect = mapRect.ContractedBy(layout.floorChanceFromLayerCurve.Count() + 5);
			bool flag = true;
			CellRect inRect = new CellRect();
			while (flag && mainRect.Area < mapRect.Area)
			{
				if (mainRect.TryFindRandomInnerRect(new IntVec2(new IntRange(minBaseSize.x, maxBaseSize.x).RandomInRange, new IntRange(minBaseSize.z, maxBaseSize.z).RandomInRange), out inRect))
				{
					flag = false;
				}
				else
				{
					mainRect = mainRect.ExpandedBy(1).ClipInsideMap(map);
				}
			}
			GenerateAndSpawn(inRect, map, parms, layout);
			defeatedFaction = null;
		}

		public StructureGenParams GetStructureGenParams(CellRect rect, Map map, GenStepParams parms, LayoutDef layoutDef)
		{
			return new StructureGenParams
			{
				size = rect.Size
			};
		}

		public virtual LayoutStructureSketch GenerateAndSpawn(CellRect rect, Map map, GenStepParams parms, LayoutDef layoutDef)
		{
			StructureGenParams structureGenParams = GetStructureGenParams(rect, map, parms, layoutDef);
			LayoutWorker worker = layoutDef.Worker;
			LayoutStructureSketch layoutStructureSketch = worker.GenerateStructureSketch(structureGenParams);
			using (new RandBlock(layoutStructureSketch.id))
			{
				map.layoutStructureSketches.Add(layoutStructureSketch);
				float? threatPoints = null;
				if (parms.sitePart != null)
				{
					threatPoints = parms.sitePart.parms.points;
				}
				if (!threatPoints.HasValue && map.Parent is Site site)
				{
					threatPoints = site.ActualThreatPoints;
				}
				worker.Spawn(layoutStructureSketch, map, rect.Min, threatPoints, null, roofs: true, canReuseSketch: false, Faction.OfEntities);
				MapGenerator.UsedRects.Add(rect.ExpandedBy(1));
				return layoutStructureSketch;
			}
		}
	}
}