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

namespace NAT
{
	public class SignalAction_Sightstealers : SignalAction
	{
		public float points;

		public CellRect spawnAround;

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref points, "points", 0f);
			Scribe_Values.Look(ref spawnAround, "spawnAround");
		}

		protected override void Tick()
		{
			base.Tick();
			if (this.IsHashIntervalTick(60) && this.GetRoom() != null && (!Position.Fogged(Map) || this.GetRoom().ContainedThings<Pawn>().Any((Pawn p) => p.HostileTo(Faction.OfEntities))))
			{
				DoAction(new Signal().args);
			}
		}

		protected override void DoAction(SignalArgs args)
		{
			if (points <= 0f)
			{
				return;
			}
			List<Pawn> list = new List<Pawn>();
			IntVec3 result;
			foreach (Pawn item in GenerateSightstealers())
			{
				if (!spawnAround.EdgeCells.TryRandomElement(out result))
				{
					Find.WorldPawns.PassToWorld(item);
					break;
				}
				GenSpawn.Spawn(item, result, base.Map);
				list.Add(item);
			}
			if (!list.Any())
			{
				return;
			}
			Faction faction = list[0].Faction;
			LordMaker.MakeNewLord(faction, new LordJob_AssaultColony(faction), base.Map, list);
			//Find.LetterStack.ReceiveLetter("LetterLabelAmbushInExistingMap".Translate(), "LetterAmbushInExistingMap".Translate(PawnKindDefOf.Sightstealer.GetLabelPlural()).CapitalizeFirst(), LetterDefOf.ThreatBig, list);
			if (!base.Destroyed)
			{
				Destroy();
			}
		}

		private List<Pawn> GenerateSightstealers()
		{
			PawnGroupMakerParms pawnGroupMakerParms = new PawnGroupMakerParms
			{
				groupKind = PawnGroupKindDefOf.Sightstealers,
				tile = Map.Tile,
				faction = Faction.OfEntities,
				points = points
			};
			pawnGroupMakerParms.points = Mathf.Max(pawnGroupMakerParms.points, Faction.OfEntities.def.MinPointsToGeneratePawnGroup(pawnGroupMakerParms.groupKind) * 1.05f);
			return PawnGroupMakerUtility.GeneratePawns(pawnGroupMakerParms).ToList();
		}
	}
}
