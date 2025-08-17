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
	public class GameCondition_DeadlifeSpewer : GameCondition_ForceWeather
	{
		private static readonly IntRange ResurrectIntervalRange = new IntRange(600, 1800);

		private int nextResurrectTick;

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref nextResurrectTick, "nextResurrectTick", 0);
		}

		public override void Init()
		{
			base.Init();
			nextResurrectTick = Find.TickManager.TicksGame + ResurrectIntervalRange.RandomInRange;
		}

		public override void GameConditionTick()
		{
			if (Find.TickManager.TicksGame < nextResurrectTick || Find.TickManager.TicksGame % 60 != 0)
			{
				return;
			}
			List<Pawn> shamblers = new List<Pawn>();
			bool b = false;
			foreach (Map affectedMap in base.AffectedMaps)
			{
                if (b)
                {
					break;
                }
				foreach (Thing item in affectedMap.listerThings.ThingsInGroup(ThingRequestGroup.Corpse))
				{
					if (item is Corpse corpse && MutantUtility.CanResurrectAsShambler(corpse) && corpse.Age >= 15000)
					{
						Pawn pawn = ResurrectPawn(corpse);
						if (!pawn.Position.Fogged(affectedMap))
						{
							Messages.Message("DeathPallResurrectedMessage".Translate(pawn), pawn, MessageTypeDefOf.NegativeEvent, historical: false);
						}
						nextResurrectTick = Find.TickManager.TicksGame + ResurrectIntervalRange.RandomInRange;
						shamblers.Add(pawn);
						b = true;
						break;
					}
				}
				if (Rand.Chance(0.1f))
                {
					foreach (Pawn p in affectedMap.mapPawns.SpawnedPawnsInFaction(Faction.OfEntities))
					{
                        if (p.IsShambler && p.GetLord() == null)
                        {
							shamblers.Add(p);
						}
					}
					if (shamblers.Count > 5)
                    {
						LordMaker.MakeNewLord(Faction.OfEntities, new LordJob_ShamblerAssault(), affectedMap, shamblers);
					}
				}
			}
		}

		private Pawn ResurrectPawn(Corpse corpse)
		{
			Pawn innerPawn = corpse.InnerPawn;
			MutantUtility.ResurrectAsShambler(innerPawn, 120000, Faction.OfEntities);
			return innerPawn;
		}

		public override void End()
		{
			Find.LetterStack.ReceiveLetter("LetterLabelDeathPallEnded".Translate(), "LetterDeathPallEnded".Translate(), LetterDefOf.NeutralEvent);
			base.End();
			base.SingleMap.weatherDecider.StartNextWeather();
		}
	}
}