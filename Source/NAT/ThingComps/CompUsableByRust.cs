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
	public class CompProperties_UsableByRust : CompProperties
	{
		public string jobReport = null;

		public int useDuration = 80;

		public HediffDef hediff;

		public bool replaceHediff = true;

		public int? duration;

		public float? severity;

		public float restOffset = 0f;

		public int unitOffset = 0;

		public bool destroyAfterUse = true;

		public EffecterDef useEffect;

		public bool combatEnhancing = true;
		public CompProperties_UsableByRust()
		{
			compClass = typeof(CompUsableByRust);
		}

        public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
        {
			foreach (StatDrawEntry item in base.SpecialDisplayStats(req))
			{
				yield return item;
			}
			if (restOffset != 0f)
			{
				string text = ((restOffset > 0f) ? "+" : string.Empty);
				yield return new StatDrawEntry(StatCategoryDefOf.Drug, NATDefOf.NAT_RustRest.LabelCap, text + restOffset.ToStringPercent(), NATDefOf.NAT_RustRest.description, 500);
			}
		}
    }
	public class CompUsableByRust : ThingComp
	{
		public CompProperties_UsableByRust Props => (CompProperties_UsableByRust)props;
		public virtual string JobReport => Props.jobReport ?? "NAT_UseItem".Translate();

		public virtual AcceptanceReport CanBeUsedBy(RustedPawn rust)
		{
			if (!Props.replaceHediff && Props.hediff != null && rust.health.hediffSet.GetFirstHediffOfDef(Props.hediff) != null)
			{
				return false;
			}
			return true;
		}

		public bool ShouldUseForCombat(RustedPawn r)
        {
            if (!Props.combatEnhancing)
            {
				return false;
            }
			if(Props.hediff != null && r.health.hediffSet.HasHediff(Props.hediff))
            {
				return false;
            }
			return true;
        }

		public virtual void UsedBy(RustedPawn rust)
		{
			if(Props.restOffset > 0f && rust.restNeed != null)
            {
				rust.restNeed.CurLevel += Props.restOffset;
			}
			if (Props.unitOffset > 0 && rust.TryGetComp<CompRustedCommander>(out var comp) && (comp.units + Props.unitOffset) <= comp.Props.maxUnits)
			{
				comp.units += Props.unitOffset;
			}
			if (Props.hediff != null)
			{
				Hediff hediff = rust.health.GetOrAddHediff(Props.hediff);
				if (Props.duration != null)
				{
					hediff.TryGetComp<HediffComp_Disappears>()?.SetDuration(Props.duration.Value);
				}
				if (Props.severity != null)
				{
					hediff.Severity = Props.severity.Value;
				}
			}
			if(Props.useEffect != null && rust.SpawnedOrAnyParentSpawned)
            {
				Props.useEffect.SpawnAttached(rust.SpawnedParentOrMe, rust.MapHeld);

			}
            if (Props.destroyAfterUse)
            {
				if(parent.stackCount > 1)
                {
					parent.SplitOff(1).Destroy();
				}
                else
                {
					parent.Destroy();
				}
            }
		}
	}
}