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
	public class CompTargetEffect_InducePain : CompTargetEffect
	{
		public override void DoEffectOn(Pawn user, Thing target)
		{
			if (target is Pawn pawn && pawn.RaceProps.IsFlesh)
			{
				Hediff hediff = HediffMaker.MakeHediff(NATDefOf.NAT_InducedPain, pawn);
				if (pawn.health.hediffSet.HasHediff(NATDefOf.NAT_InducedPain))
				{
					Hediff hediff2 = pawn.health.hediffSet.GetFirstHediffOfDef(NATDefOf.NAT_InducedPain);
					hediff.Severity = hediff2.Severity + new FloatRange(0.2f, 1f).RandomInRange;
					hediff.TryGetComp<HediffComp_Disappears>().ticksToDisappear += hediff2.TryGetComp<HediffComp_Disappears>().ticksToDisappear;
					pawn.health.RemoveHediff(hediff2);
				}
				else
				{
					hediff.TryGetComp<HediffComp_Disappears>().ticksToDisappear = new IntRange(15000, 20000).RandomInRange;
					hediff.Severity = new FloatRange(0.4f, 3f).RandomInRange;
				}
				pawn.health.AddHediff(hediff);
			}
		}
	}
}