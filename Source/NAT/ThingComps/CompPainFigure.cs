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
	public class CompProperties_PainFigure : CompProperties
	{
		public CompProperties_PainFigure()
		{
			compClass = typeof(CompPainFigure);
		}
	}
	public class CompPainFigure : ThingComp
	{
		public int ticksBeforePulse = 72;

		public bool active = true;

		public override void CompTickRare()
		{
			ticksBeforePulse--;
			if (ticksBeforePulse <= 0)
			{
				List<Pawn> list1 = new List<Pawn>();
				foreach (Pawn pawn in parent.MapHeld.mapPawns.AllPawnsSpawned)
				{
					if (IsPawnAffected(pawn, 10f))
					{
						list1.Add(pawn);
					}
					if (pawn.carryTracker.CarriedThing is Pawn target && IsPawnAffected(target, 10f))
					{
						list1.Add(target);
					}
				}
				foreach (Pawn p in list1)
				{
					Hediff hediff = p.health.hediffSet.GetFirstHediffOfDef(NATDefOf.NAT_InducedPain);
					if (hediff == null)
					{
						hediff = p.health.AddHediff(NATDefOf.NAT_InducedPain);
						hediff.Severity = new FloatRange(0.3f, 0.8f).RandomInRange;
					}
					hediff.Severity += new FloatRange(0.1f, 0.3f).RandomInRange;
					hediff.TryGetComp<HediffComp_Disappears>().ticksToDisappear += new IntRange(2000, 2500).RandomInRange;
					p.health.Notify_HediffChanged(hediff);
				}
				list1.Clear();
				DefDatabase<EffecterDef>.GetNamed("AgonyPulseExplosion").Spawn(parent.Position, parent.Map);
				parent.Destroy(DestroyMode.KillFinalize);
				return;
			}
			if (!active)
			{
				return;
			}
			List<Pawn> list2 = new List<Pawn>();
			foreach (Pawn pawn in parent.MapHeld.mapPawns.AllPawnsSpawned)
			{
				if (IsPawnAffected(pawn))
				{
					list2.Add(pawn);
				}
				if (pawn.carryTracker.CarriedThing is Pawn target && IsPawnAffected(target))
				{
					list2.Add(target);
				}
			}
			foreach (Pawn p in list2)
			{
				InducePain(p);
			}
			list2.Clear();
		}

		public override string CompInspectStringExtra()
		{
			string s = "";
			if (!active)
			{
				s = "DormantCompInactive".Translate();
			}
			if (DebugSettings.ShowDevGizmos)
			{
				s += "\n" + "DEV: ticks before pulse: " + ticksBeforePulse;
			}
			return s;
		}

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			foreach (Gizmo g in base.CompGetGizmosExtra())
			{
				yield return g;
			}
			if (DebugSettings.ShowDevGizmos)
			{
				Command_Action command_Action4 = new Command_Action();
				command_Action4.defaultLabel = "DEV: Activate";
				command_Action4.groupable = false;
				command_Action4.action = delegate
				{
					ticksBeforePulse = 1;
				};
				yield return command_Action4;
			}
		}
		private bool IsPawnAffected(Pawn target, float radius = 5f)
		{
			if (target.Dead || target.health == null)
			{
				return false;
			}
			if (target.RaceProps.Humanlike || target.IsAnimal)
			{
				return target.PositionHeld.DistanceTo(parent.PositionHeld) <= radius;
			}
			return false;
		}
		public void InducePain(Pawn p)
		{
			Hediff hediff = p.health.hediffSet.GetFirstHediffOfDef(NATDefOf.NAT_InducedPain);
			if (hediff == null)
			{
				hediff = p.health.AddHediff(NATDefOf.NAT_InducedPain);
				hediff.Severity = new FloatRange(0.02f, 0.05f).RandomInRange;
			}
			hediff.Severity += new FloatRange(0.01f, 0.02f).RandomInRange;
			hediff.TryGetComp<HediffComp_Disappears>().ticksToDisappear += new IntRange(200, 500).RandomInRange;
			p.health.Notify_HediffChanged(hediff);
		}

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref active, "active", defaultValue: true);
			Scribe_Values.Look(ref ticksBeforePulse, "ticksBeforePulse", defaultValue: 72);
		}
	}
}