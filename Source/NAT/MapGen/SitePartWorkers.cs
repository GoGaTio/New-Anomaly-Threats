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
using RimWorld.SketchGen;
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
	public class SitePartWorker_CollectorLair : SitePartWorker
	{
		/*public override string GetPostProcessedThreatLabel(Site site, SitePart sitePart)
        {
			if(sitePart.parms is CollectorLairParams p)
            {
				string s = "";
				if(p.questPart == null)
                {
					s += "questPart is null";
				}
                else
                {
					s += "questPart debug tag: " + p.questPart.debugTag;
				}
				return s + "\n" + p.threatPoints;
            }
            else
            {
				return "Parms is not CollectorLairParams";
			}
        }*/

		public override void PostMapGenerate(Map map)
		{
			base.PostMapGenerate(map);
			MapGenUtility.DestroyTurrets(map);
			MapGenUtility.DestroyProcessedFood(map);
			MapGenUtility.ForbidAllItems(map);
		}
    }

	public class SitePartWorker_DistressCall_RustedArmy : SitePartWorker_DistressCall
	{
        public override SitePartParams GenerateDefaultParams(float myThreatPoints, PlanetTile tile, Faction faction)
        {
            return base.GenerateDefaultParams(myThreatPoints, tile, faction);
        }

        private static readonly SimpleCurve FleshbeastsPointsModifierCurve = new SimpleCurve
		{
			new CurvePoint(100f, 200f),
			new CurvePoint(500f, 800f),
			new CurvePoint(1000f, 1200f),
			new CurvePoint(5000f, 1600f)
		};

		public override void PostMapGenerate(Map map)
		{
			Site site = map.Parent as Site;
			IntVec3 cell = map.Center;
			int radius = 20;
            Faction faction = site.Faction ?? Find.FactionManager.RandomEnemyFaction();
            if (faction.IsPlayer)
            {
                faction = Find.FactionManager.RandomEnemyFaction();
            }
            List<Pawn> list = PawnGroupMakerUtility.GeneratePawns(new PawnGroupMakerParms
			{
				faction = faction,
				groupKind = PawnGroupKindDefOf.Settlement,
				points = SymbolResolver_Settlement.DefaultPawnsPoints.RandomInRange * 0.33f,
				tile = map.Tile
			}).ToList();
			float num = Faction.OfEntities.def.MinPointsToGeneratePawnGroup(NATDefOf.NAT_RustedArmy) * 1.1f;
			float num2 = Mathf.Max(FleshbeastsPointsModifierCurve.Evaluate(site.desiredThreatPoints), num);
			List<Pawn> list2 = PawnGroupMakerUtility.GeneratePawns(new PawnGroupMakerParms
			{
				groupKind = NATDefOf.NAT_RustedArmyDefence,
				points = num2,
				faction = Faction.OfEntities,
				raidStrategy = RaidStrategyDefOf.ImmediateAttack
			}).ToList();
            if (MapGenerator.TryGetVar<CellRect>("SettlementRect", out var rect))
            {
                cell = rect.CenterCell;
                radius = rect.Width / 2;
            }
            DistressCallUtility.SpawnCorpses(map, list, list2, cell, radius);
			DistressCallUtility.SpawnPawns(map, list2, cell, radius);
			//map.fogGrid.
			AnomalyIncidentUtility.PawnShardOnDeath(list2.RandomElement());
			foreach (Thing allThing in map.listerThings.AllThings)
			{
				if (allThing.def.category == ThingCategory.Item)
				{
					CompForbiddable compForbiddable = allThing.TryGetComp<CompForbiddable>();
					if (compForbiddable != null && !compForbiddable.Forbidden)
					{
						allThing.SetForbidden(value: true, warnOnFail: false);
					}
				}
			}
			LordMaker.MakeNewLord(Faction.OfEntities, new LordJob_RustedArmy(cell, 15000), map, list2);
			foreach (Pawn p in map.mapPawns.AllPawnsSpawned.Where((Pawn p2)=> p2.Faction == Faction.OfPlayer && p2.drafter != null))
            {
				p.drafter.Drafted = true;
            }
			foreach(Building_Turret b in map.listerThings.GetThingsOfType<Building_Turret>())
			{
				if(b.Faction != Faction.OfEntities)
				{
					b.SetFaction(Faction.OfEntities);
				}
			}
		}
	}

	public class SitePartWorker_RustedCitadel : SitePartWorker
	{
		public override void Init(Site site, SitePart sitePart)
		{
			base.Init(site, sitePart);
			site.doorsAlwaysOpenForPlayerPawns = true;
		}
	}
}