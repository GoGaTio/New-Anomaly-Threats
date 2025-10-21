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
	public class CompProperties_Crate : CompProperties
	{
		public ThingSetMakerDef contents;

		public float chance = 1f;

		public CompProperties_Crate()
		{
			compClass = typeof(CompCrate);
		}
	}

	public class CompCrate : ThingComp
	{
		public CompProperties_Crate Props => (CompProperties_Crate)props;

		public bool preventTrigger;

		public override void ReceiveCompSignal(string signal)
		{
			base.ReceiveCompSignal(signal);
			if (!preventTrigger && parent is Building_Crate crate && signal == "CrateContentsChanged")
			{
				crate.GetLord()?.Notify_SignalReceived(new Signal("NAT_CrateOpened"));
			}
		}

		public override void PostSpawnSetup(bool respawningAfterLoad)
		{
			if (respawningAfterLoad)
			{
				return;
			}
			Building_Crate building_Crate = (Building_Crate)parent;
			if (Props.contents == null || Rand.Chance(1f - Props.chance))
			{
				preventTrigger = true;
				building_Crate.Open();
				preventTrigger = false;
				return;
			}
			preventTrigger = true;
			List<Thing> list = Props.contents.root.Generate(default(ThingSetMakerParams));
			for (int num = list.Count - 1; num >= 0; num--)
			{
				Thing thing = list[num];
				if (!building_Crate.TryAcceptThing(thing, allowSpecialEffects: false))
				{
					thing.Destroy();
				}
			}
			preventTrigger = false;
		}
	}
}