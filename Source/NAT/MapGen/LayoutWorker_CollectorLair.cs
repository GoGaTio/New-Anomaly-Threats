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
	public class LayoutWorker_CollectorLair : LayoutWorker_Structure
	{
		public LayoutWorker_CollectorLair(LayoutDef def)
			: base(def)
		{
		}

        public override void Spawn(LayoutStructureSketch layoutStructureSketch, Map map, IntVec3 pos, float? threatPoints = null, List<Thing> allSpawnedThings = null, bool roofs = true, bool canReuseSketch = false, Faction faction = null)
        {
            base.Spawn(layoutStructureSketch, map, pos, threatPoints, allSpawnedThings, roofs, canReuseSketch, faction);
			if (map.PocketMapParent.sourceMap.Parent is Site site && site.parts[0].parms is CollectorLairParams parms)
            {
				QuestPart_Collector questPart_Collector = parms.questPart;
				List<CellRect> list = new List<CellRect>();
				foreach(var room in layoutStructureSketch.structureLayout.Rooms)
                {
                    if (room.HasLayoutDef(NATDefOf.NAT_CollectionRoom))
                    {
						list.Add(room.rects[0].ContractedBy(2));
                    }
				}
				IntVec2 size = NATDefOf.NAT_CollectorGlassCase.size;
				foreach (Pawn p in questPart_Collector.stolenPawns)
				{
                    while (true)
                    {
						if(list.RandomElement().TryFindRandomInnerRect(size, out var rect, Validator))
                        {
							Building_CollectionCase glassCase = ThingMaker.MakeThing(NATDefOf.NAT_CollectorGlassCase) as Building_CollectionCase;
							glassCase.pawn = p;
							glassCase.questPart = questPart_Collector;
                            GenSpawn.Spawn(glassCase, rect.CenterCell, map);
							break;
						}
                    }
				}
				foreach (Thing t in questPart_Collector.stolenThings)
				{
					GenPlace.TryPlaceThing(t, list.RandomElement().CenterCell, map, ThingPlaceMode.Near);
				}
				questPart_Collector.stolenPawns.Clear();
				questPart_Collector.stolenThings.Clear();
				parms.questPart = null;
				questPart_Collector.Notify_LairGenerated();
            }
			bool Validator(CellRect r)
            {
				foreach(IntVec3 cell in r.Cells)
                {
					if(!cell.GetThingList(map).NullOrEmpty())
                    {
						return false;
                    }
                }
				return true;
            }
        }

        protected override StructureLayout GetStructureLayout(StructureGenParams parms, CellRect rect)
		{
			return RoomLayoutGenerator.GenerateRandomLayout(parms.sketch, rect, minRoomHeight: base.Def.minRoomHeight, minRoomWidth: base.Def.minRoomWidth, areaPrunePercent: 0.25f, canRemoveRooms: true, generateDoors: false, corridor: null, corridorExpansion: 2, maxMergeRoomsRange: new IntRange(2, 4), corridorShapes: CorridorShape.All, canDisconnectRooms: false);
		}

		protected override void PostGraphsGenerated(StructureLayout layout, StructureGenParams parms)
		{
			foreach (LayoutRoom room in layout.Rooms)
			{
				room.noExteriorDoors = base.Def.exteriorDoorDef == null;
			}
		}
	}
}