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
	public class RustHeadDef : Def
	{
		public GraphicData graphicData;

		public List<StatModifier> statFactors;

		public List<StatModifier> statOffsets;

		public float selectionWeight = 1f;

		public string tag;

		public override IEnumerable<string> ConfigErrors()
		{
			foreach (string s in base.ConfigErrors()) yield return s;
			if (graphicData.graphicClass != typeof(Graphic_Multi))
			{
				yield return defName + "NAT.RustHeadDef should have Graphic_Multi graphicClass";
			}

		}
		public Graphic_Multi GetGraphic(RustedPawn rust)
		{
			Graphic graphic = graphicData.Graphic;
			return graphic.GetColoredVersion(graphicData.shaderType.Shader, Color.white, Color.white) as Graphic_Multi;
		}
	}
}