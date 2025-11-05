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
	public class FloatMenuOptionProvider_UseRustItem : FloatMenuOptionProvider
	{
		protected override bool Drafted => true;

		protected override bool Undrafted => true;

		protected override bool Multiselect => false;

		protected override bool CanSelfTarget => true;

		protected override bool AppliesInt(FloatMenuContext context)
		{
			return context.FirstSelectedPawn is RustedPawn;
		}

		public override IEnumerable<FloatMenuOption> GetOptionsFor(Thing clickedThing, FloatMenuContext context)
		{
			if (!context.FirstSelectedPawn.CanReach(clickedThing, PathEndMode.ClosestTouch, Danger.Deadly))
			{
				yield break;
			}
			if (clickedThing.TryGetComp<CompUsableByRust>(out var comp))
			{
				yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(comp.JobReport + " " + clickedThing.Label, delegate
				{
					clickedThing.SetForbidden(value: false, warnOnFail: false);
					context.FirstSelectedPawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(NATDefOf.NAT_UseItemByRust, clickedThing), JobTag.Misc);
				}, MenuOptionPriority.High), context.FirstSelectedPawn, clickedThing);
			}
		}

        public override IEnumerable<FloatMenuOption> GetOptionsFor(Pawn clickedPawn, FloatMenuContext context)
        {
			if (context.FirstSelectedPawn != clickedPawn && !context.FirstSelectedPawn.CanReach(clickedPawn, PathEndMode.ClosestTouch, Danger.Deadly))
			{
				yield break;
			}
			if (clickedPawn is RustedPawn rust)
			{
				List<Thing> list = context.FirstSelectedPawn.inventory?.innerContainer?.Where((Thing x) => x.HasComp<CompUsableByRust>())?.ToList();
				if (list.NullOrEmpty())
				{
					yield break;
				}
				List<ThingDef> list2 = new List<ThingDef>();
				foreach (Thing thing in list)
				{
					if (!list2.Contains(thing.def))
					{
						CompUsableByRust comp = thing.TryGetComp<CompUsableByRust>();
						yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(comp.JobReport + " " + thing.Label + (context.FirstSelectedPawn == clickedPawn ? "" : (" (" + clickedPawn.Name?.ToStringShort + ")") ?? ""), delegate
						{
							context.FirstSelectedPawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(NATDefOf.NAT_UseItemByRust, thing, rust), JobTag.Misc);
						}, MenuOptionPriority.High), context.FirstSelectedPawn, clickedPawn);
						list2.Add(thing.def);
					}
				}
			}
		}
    }
}