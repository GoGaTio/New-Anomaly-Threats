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
	public class Skyfaller_RustedChunk : Skyfaller
	{
		public List<Thing> frendlies = new List<Thing>();

        public bool causesExplosion = true;

        public Faction faction;
        protected override void Impact()
        {
            if (causesExplosion)
            {
                GenExplosion.DoExplosion(base.Position, base.Map, def.skyfaller.explosionRadius, NATDefOf.NAT_RustedBomb, null, GenMath.RoundRandom((float)NATDefOf.NAT_RustedBomb.defaultDamage * def.skyfaller.explosionDamageFactor), -1f, null, null, null, null, null, 0f, 1, null, null, 255, applyDamageToExplosionCellsNeighbors: false, null, 0f, 1, 0f, damageFalloff: false, null, frendlies.ConcatIfNotNull(Frendlies())?.ToList());
            }
            base.Impact();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref faction, "faction");
        }

        public IEnumerable<Thing> Frendlies()
        {
            Map map = this.Map;
            if(map == null)
            {
                yield break;
            }
            foreach(Thing inner in base.innerContainer)
            {
                yield return inner;
            }
            CellRect rect = this.OccupiedRect().ExpandedBy(Mathf.CeilToInt(def.skyfaller.explosionRadius + 1)).ClipInsideMap(map);
            foreach(IntVec3 cell in rect.Cells)
            {
                List<Thing> list = cell.GetThingList(map);
                if (!list.NullOrEmpty())
                {
                    foreach(Thing t in list)
                    {
                        if(t.Faction == faction)
                        {
                            yield return t;
                        }
                    }
                }
            }
        }
    }
}