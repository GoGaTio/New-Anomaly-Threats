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
	public class CompProperties_ReinforcementsBeacon : CompProperties_SquareDetector
	{
		public int units;

		public ThingDef skyfaller;

		public PawnKindDef pawnKind;

		public CompProperties_ReinforcementsBeacon()
		{
			compClass = typeof(CompReinforcementsBeacon);
		}
	}

	public class CompReinforcementsBeacon : CompSquareDetector
	{
		public new CompProperties_ReinforcementsBeacon Props => (CompProperties_ReinforcementsBeacon)props;

		public override void Activate()
		{
			if(parent.Faction == null)
            {
				parent.SetFaction(Faction.OfEntities);
            }
			List<Pawn> list = new List<Pawn>();
			for (int i = 0; i < Props.units; i++)
			{
				Pawn p = PawnGenerator.GeneratePawn(Props.pawnKind, parent.Faction);
				list.Add(p);
				RCellFinder.TryFindRandomCellNearWith(parent.Position, (IntVec3 c) => !c.Impassable(parent.Map) && c.GetRoof(parent.MapHeld)?.isThickRoof != true, parent.MapHeld, out var cell2, 5, Props.range);
				(SkyfallerMaker.SpawnSkyfaller(Props.skyfaller, p, cell2, parent.MapHeld ?? Find.CurrentMap) as Skyfaller_RustedChunk).faction = parent.Faction ?? Faction.OfEntities;
			}
			RCellFinder.TryFindRandomSpotJustOutsideColony(parent.PositionHeld, parent.MapHeld, out var result);
			LordMaker.MakeNewLord(parent.Faction, new LordJob_AssaultColony(parent.Faction, false, false, false, false, false, false, false), parent.MapHeld, list);
			Messages.Message("NAT_DropRequested".Translate(parent.LabelCap), list, MessageTypeDefOf.ThreatBig);
			base.Activate();
		}
	}
}