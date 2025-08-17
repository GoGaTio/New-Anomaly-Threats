using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using RimWorld;
using RimWorld.BaseGen;
using RimWorld.IO;
using RimWorld.Planet;
using RimWorld.QuestGen;
using RimWorld.SketchGen;
using RimWorld.Utility;
using LudeonTK;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Grammar;
using Verse.Noise;
using Verse.Profile;
using Verse.Sound;
using Verse.Steam;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace NAT
{
	public class IncidentWorker_ObeliskInducer : IncidentWorker_Obelisk
	{
		public override ThingDef ObeliskDef => NATDefOf.NAT_WarpedObelisk_Inducer;
	}
	

	public class IncidentWorker_SkullArrival : IncidentWorker
	{
		protected override bool TryExecuteWorker(IncidentParms parms)
		{
			Map map = (Map)parms.target;
			if (!map.mapPawns.AllHumanlikeSpawned.Where((Pawn p) => p.Faction == Faction.OfPlayer).TryRandomElement(out Pawn pawn))
			{
				return false;
			}
			Thing t = ThingMaker.MakeThing(ThingDefOf.Skull);
			t.TryGetComp<CompHasSources>().AddSource(pawn.LabelShort);
			DropPodUtility.DropThingsNear(CellFinder.StandableCellNear(pawn.Position, map, 45f), map, new List<Thing> { t }, 90, false, true, true, false, false);
			Find.LetterStack.ReceiveLetter("NAT_SkullArrival".Translate(), "NAT_SkullArrival_Desc".Translate(pawn.Named("PAWN")), LetterDefOf.NeutralEvent, t);
			return true;
		}
	}

	public class IncidentWorker_Collector : IncidentWorker
	{
		protected override bool TryExecuteWorker(IncidentParms parms)
		{
			Map map = (Map)parms.target;
			Slate slate = new Slate();
			slate.Set("map", map);
			QuestUtility.GenerateQuestAndMakeAvailable(NATDefOf.NAT_CollectorScript, slate);
			return true;
		}
	}
}