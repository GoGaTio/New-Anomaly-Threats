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
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
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
using HarmonyLib;

namespace NAT
{
	public class CompRustedGrenadesPack : CompAIUsablePack
	{
		private Thing target;
		protected override float ChanceToUse(Pawn wearer)
		{
			TargetScanFlags targetScanFlags = TargetScanFlags.NeedLOSToAll | TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable;
			target = (Thing)AttackTargetFinder.BestShootTargetFromCurrentPosition(wearer, targetScanFlags, null, 0, parent.def.Verbs[0].range);
			if(target == null)
            {
				return 0;
            }
			return 0.5f;
		}

        public override void Notify_UsedVerb(Pawn pawn, Verb verb)
        {
            if (verb.EquipmentSource == parent)
            {
				pawn.jobs.EndCurrentJob(JobCondition.Succeeded);
            }
        }

        protected override void UsePack(Pawn wearer)
		{
			if (target == null)
            {
				return;
            }
			Verb verb = parent.GetComp<CompApparelVerbOwner>().AllVerbs[0];
			Job job = JobMaker.MakeJob(JobDefOf.UseVerbOnThing);
			job.verbToUse = verb;
			job.targetA = target;
			job.endIfCantShootInMelee = false;
			job.endIfCantShootTargetFromCurPos = true;
			wearer.jobs.TryTakeOrderedJob(job, JobTag.Misc);
		}
	}
}