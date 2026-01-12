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
using static System.Net.Mime.MediaTypeNames;

namespace NAT
{
	public class CompProperties_RustedWorker : CompProperties
	{
		public List<SkillGain> skills = new List<SkillGain>();

		public bool canWork = false;

		public WorkTags workTags;

		public List<WorkTypeDef> forceAllowWorkTypes = new List<WorkTypeDef>();

		public List<WorkTypeDef> forceDisallowWorkTypes = new List<WorkTypeDef>();

		public List<WorkGiverDef> forceAllowWorkGivers = new List<WorkGiverDef>();

		public CompProperties_RustedWorker()
		{
			compClass = typeof(CompRustedWorker);
		}

		private static List<WorkTypeDef> workTypes;

		public static List<WorkTypeDef> WorkTypes
		{
			get
			{
				if (workTypes == null)
				{
					workTypes = DefDatabase<WorkTypeDef>.AllDefsListForReading;
				}
				return workTypes;
			}
		}

		private List<WorkTypeDef> disabledWorkTypes = null;

		public List<WorkTypeDef> DisabledWorkTypes
		{
			get
			{
				if (Scribe.mode != 0)
				{
					disabledWorkTypes = null;
				}
				if (disabledWorkTypes == null)
				{
					disabledWorkTypes = new List<WorkTypeDef>();
					List<WorkTypeDef> list = AvailableWorkTypes.ToList();
					foreach (WorkTypeDef t in WorkTypes)
					{
						if (!list.Contains(t))
						{
							disabledWorkTypes.Add(t);
						}
					}
				}
				return disabledWorkTypes;
			}
		}
		public IEnumerable<WorkTypeDef> AvailableWorkTypes
		{
			get
			{
				foreach (WorkTypeDef t in forceAllowWorkTypes)
				{
					yield return t;
				}
				List<WorkTypeDef> list = WorkTypes;
				for (int i = 0; i < list.Count; i++)
				{
					if ((workTags & list[i].workTags) != 0 && !forceDisallowWorkTypes.Contains(list[i]))
					{
						yield return list[i];
					}
				}
			}
		}

		public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
		{
			foreach (StatDrawEntry item in base.SpecialDisplayStats(req))
			{
				yield return item;
			}
			if (!skills.NullOrEmpty() && (!req.HasThing || req.Thing.Faction == Faction.OfPlayerSilentFail))
			{
				foreach(SkillGain skill in skills)
				{
					yield return new StatDrawEntry(NATDefOf.NAT_Skills, skill.skill.LabelCap, skill.amount.ToString(), skill.skill.description, Mathf.RoundToInt(skill.skill.listOrder), overridesHideStats: true);
				}
			}
		}
	}
	public class CompRustedWorker : ThingComp
	{
		public CompProperties_RustedWorker Props => (CompProperties_RustedWorker)props;

		public RustedPawn Rust => parent as RustedPawn;

		public bool CanWork => Props.canWork;
		public override void PostPostMake()
        {
            base.PostPostMake();
			Rust.skills = new Pawn_SkillTracker(Rust);
			foreach (SkillGain s in Props.skills)
            {
				Rust.skills.GetSkill(s.skill).Level = s.amount;
			}
		}

		public List<WorkTypeDef> DisabledWorkTypes => Props.DisabledWorkTypes;
		public IEnumerable<WorkTypeDef> AvailableWorkTypes => Props.AvailableWorkTypes;
	}
}