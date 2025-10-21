﻿using System;
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
	public class JobGiver_Sleep : ThinkNode_JobGiver
	{
		public float wantedLevel = 0f;

		public float startFromLevel = 0f;

		public bool forceIfExhausted = false;
		protected override Job TryGiveJob(Pawn pawn)
		{
			if(pawn is RustedPawn rust && rust.restNeed != null)
            {
                if (ShouldKeep(rust))
                {
					if (pawn.CurJob?.def == JobDefOf.Wait_AsleepDormancy)
					{
						return pawn.CurJob;
					}
                    if (forceIfExhausted)
                    {
						return RestJob(rust);
					}
				}
				if (rust.restNeed.CurLevel < startFromLevel)
				{
					return RestJob(rust);
				}
			}
			return null;
		}
		private bool ShouldKeep(RustedPawn p)
        {
            if (forceIfExhausted && p.restNeed.exhausted)
            {
				return true;
            }
            if(p.restNeed.CurLevel < wantedLevel)
            {
				return true;
            }
			return false;
        }
		public static Job RestJob(Pawn p, bool forced = false)
        {
			p.TryGetComp<CompCanBeDormant>(out var comp);
			comp.wokeUpTick = int.MinValue;
			if (p is IAttackTarget t)
			{
				p.Map.attackTargetsCache.UpdateTarget(t);
			}
            if (p.Drafted)
            {
				p.drafter.Drafted = false;
			}
			Job job = JobMaker.MakeJob(JobDefOf.Wait_AsleepDormancy, p.Position);
			job.forceSleep = true;
            if (forced)
            {
				job.startInvoluntarySleep = true;
			}
			return job;
		}
	}

	public class JobGiver_RustedCommander : ThinkNode_JobGiver
	{
		protected override Job TryGiveJob(Pawn pawn)
		{
			if (pawn.CurJob?.ability?.def != null)
			{
				return null;
			}
			if (pawn.Faction?.IsPlayer == false && pawn is RustedPawn rust && rust.Awake() && rust.Commander is CompRustedCommander comp && comp.units > 0)
			{
				LocalTargetInfo target = comp.TryCallSupport(out var ability);
                if (!target.IsValid)
                {
					return null;
                }
				return ability.GetJob(target, target);
			}
			return null;
		}
	}

	public class ThinkNode_ConditionalReinforcement : ThinkNode_Conditional
	{
        protected override bool Satisfied(Pawn pawn)
        {
			if(pawn is RustedPawn rust && !rust.Controllable)
            {
				return true;
            }
			return false;
        }
    }
}