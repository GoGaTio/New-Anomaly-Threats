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
	public class CompProperties_UndergroundEntrance : CompProperties_Interactable
	{
		[MustTranslate]
		public string openedLabelOverride;

		[MustTranslate]
		public string sealedLabelOverride;

		public LayoutDef layout;

		public GraphicData openedGraphicData;

		public GraphicData sealedGraphicData;

		public bool canBeSealed = true;

		[NoTranslate]
		public string sealTexPath;

		public int sealTicks = 300;

		public EffecterDef sealEffect;

		[MustTranslate]
		public string sealJobReportOverride;

		[MustTranslate]
		public string sealGizmoLabel;

		[MustTranslate]
		public string sealGizmoDesc;

		public CompProperties_UndergroundEntrance()
		{
			compClass = typeof(CompUndergroundEntrance);
		}
	}
	public class CompUndergroundEntrance : CompInteractable
	{
		public new CompProperties_UndergroundEntrance Props => props as CompProperties_UndergroundEntrance;

		public UndergroundEntrance Entrance => parent as UndergroundEntrance;

		[Unsaved(false)]
		private Texture2D sealTex;

		public Texture2D SealIcon
		{
			get
			{
				if (!(sealTex != null))
				{
					return sealTex = ContentFinder<Texture2D>.Get(Props.sealTexPath);
				}
				return sealTex;
			}
		}

		public override string TransformLabel(string label)
        {
			if (Entrance.isSealed && !Props.sealedLabelOverride.NullOrEmpty())
			{
				return Props.sealedLabelOverride;
			}
			if (Entrance.isOpened && !Props.openedLabelOverride.NullOrEmpty())
            {
				return Props.openedLabelOverride;
            }
			return label;
        }

        public override string ExposeKey => "Interactor";

		public override AcceptanceReport CanInteract(Pawn activateBy = null, bool checkOptionalItems = true)
		{
			if (Entrance.isOpened || Entrance.isSealed)
			{
				return false;
			}
			return base.CanInteract(activateBy, checkOptionalItems);
		}

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			if (Entrance.isOpened || Entrance.isSealed)
			{
				yield break;
			}
			foreach (Gizmo item in base.CompGetGizmosExtra())
			{
				yield return item;
			}
		}

        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
		{
			if (Entrance.isOpened || Entrance.isSealed)
			{
				yield break;
			}
			foreach (FloatMenuOption item in base.CompFloatMenuOptions(selPawn))
			{
				yield return item;
			}
		}

		protected override void OnInteracted(Pawn caster)
		{
			Entrance.Open();
		}
	}
}