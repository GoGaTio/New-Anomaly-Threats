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
	public class FilthBile : Filth
	{
		public int activeTicks = 180;

        protected override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (activeTicks == 0 || !Spawned)
            {
				return;
            }
			foreach (Thing item in Map.thingGrid.ThingsAt(Position))
			{
				if (item is Pawn p && (p.RaceProps.Humanlike || p.IsAnimal))
				{
                    if (TryAttachBile(p))
                    {
						break;
					}
				}
			}
			activeTicks--;
		}

		private bool TryAttachBile(Pawn pawn)
		{
			if (pawn.health.hediffSet.HasHediff(NATDefOf.NAT_BilePowerSerum))
            {
				return false;
            }
			Hediff h = pawn.health.GetOrAddHediff(NATDefOf.NAT_SlowedByBile);
			if(h.Severity >= 1.5f)
            {
				return false;
            }
			h.Severity += 0.49f;
			DamageInfo dinfo = new DamageInfo(DamageDefOf.Deterioration, 150f);
			this.TakeDamage(dinfo);
			return true;
		}
	}
}
