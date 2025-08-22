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
	public class CompProperties_BombardmentBeacon : CompProperties_SquareDetector
	{
		public int strikes = 1;

		public int interval = 120;

		public int warmUp = 120;

		public float areaRange = 3f;

		public FloatRange explosionRange = new FloatRange(2f,3f);

		public CompProperties_BombardmentBeacon()
		{
			compClass = typeof(CompBombardmentBeacon);
		}
	}

	public class CompBombardmentBeacon : CompSquareDetector
	{
		public new CompProperties_BombardmentBeacon Props => (CompProperties_BombardmentBeacon)props;

		public override void Activate()
		{
			Bombardment obj = (Bombardment)GenSpawn.Spawn(ThingDefOf.Bombardment, parent.Position, parent.Map);
			obj.impactAreaRadius = Props.areaRange;
			obj.explosionRadiusRange = Props.explosionRange;
			obj.bombIntervalTicks = Props.interval;
			obj.randomFireRadius = 1;
			obj.explosionCount = Props.strikes;
			obj.warmupTicks = Props.warmUp;
			obj.instigator = parent;
			Messages.Message("NAT_BombardmentRequested".Translate(parent.LabelCap), null, MessageTypeDefOf.ThreatBig);
			base.Activate();
		}
	}
}