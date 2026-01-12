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
	public class Need_RustRest : Need
	{
		private int lastRestTick = -999;

		public bool exhausted = false;

		public override int GUIChangeArrow
		{
			get
			{
				if (Resting)
				{
					return 1;
				}
				return -1;
			}
		}

		public float CurLoss
		{
			get
			{
				if (CurLevel > 0.3f)
				{
					return 1f;
				}
				if (CurLevel > 0.1f)
				{
					return 0.5f;
				}
				return 0.25f;
			}
		}

		public bool Resting => Find.TickManager.TicksGame < lastRestTick + 2;

		public Need_RustRest(Pawn pawn)
			: base(pawn)
		{
			threshPercents = new List<float>();
			threshPercents.Add(0.9f);
			threshPercents.Add(0.7f);
			threshPercents.Add(0.3f);
			threshPercents.Add(0.1f);
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref exhausted, "exhausted", defaultValue: false);
		}

		public override void SetInitialLevel()
		{
			CurLevel = Rand.Range(0.9f, 1f);
		}

		public override void NeedInterval()
		{
			if (Resting)
			{
				CurLevel += 0.0025f;
			}
			else
			{
				CurLevel -= 0.0025f * CurLoss;
			}
			if (exhausted && CurLevel > 0.1f)
			{
				exhausted = false;
				pawn.jobs?.CheckForJobOverride();
				Messages.Message("NAT_RustSleptEnough".Translate(), pawn, MessageTypeDefOf.NeutralEvent, false);
			}
			if (exhausted || CurLevel < 0.01f)
            {
				if(pawn.jobs?.curDriver?.asleep == false)
                {
					Job job = JobMaker.MakeJob(JobDefOf.Wait_AsleepDormancy, pawn.Position);
					job.forceSleep = true;
					pawn.drafter.Drafted = false;
					pawn.jobs.StartJob(JobGiver_Sleep.RestJob(pawn, true), JobCondition.InterruptForced, null, resumeCurJobAfterwards: false, cancelBusyStances: true, null, JobTag.SatisfyingNeeds, fromQueue: false, canReturnCurJobToPool: false, null, continueSleeping: false, addToJobsThisTick: true, preToilReservationsCanFail: true);
				}
				exhausted = true;
			}
			if (CurLevel > 0.6f && Resting)
			{
				pawn.jobs?.CheckForJobOverride();
			}
		}

		public void TickResting()
		{
			lastRestTick = Find.TickManager.TicksGame;
		}
	}
}