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
	public class CompProperties_RustedBanner : CompProperties
	{
		public float range;
		public CompProperties_RustedBanner()
		{
			compClass = typeof(CompRustedBanner);
		}

    }

	public class CompRustedBanner : ThingComp
	{
		public CompProperties_RustedBanner Props => (CompProperties_RustedBanner)props;

        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
			ApplyEffect(parent.PositionHeld, prevMap);
			base.Notify_Killed(prevMap, dinfo);
        }

		public void ApplyEffect(IntVec3 cell, Map map)
        {
			if(!cell.IsValid || map == null)
            {
				Log.Message("Rusted Army Banner didnt work");
				return;
            }
			NATDefOf.NAT_BannerBoostEffect.Spawn(cell, map);
			NATDefOf.NAT_World_RustedBannerCall.PlayOneShot(new TargetInfo(cell, map));
			foreach (Pawn p in map.mapPawns.AllPawnsSpawned.ToList())
            {
				if(p is RustedPawn && p.Faction == Faction.OfEntities && cell.DistanceTo(p.Position) <= Props.range)
                {
					if (p.health.hediffSet.TryGetHediff(NATDefOf.NAT_BannerBoost, out var hediff))
					{
						hediff.Severity++;
					}
                    else
                    {
						p.health.AddHediff(NATDefOf.NAT_BannerBoost);
					}
                }
            }
		}
    }
}
