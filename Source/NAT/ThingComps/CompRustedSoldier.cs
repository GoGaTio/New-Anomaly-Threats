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
	public class CompProperties_RustedSoldier : CompProperties
	{
		public InteractionDef interaction;

		public bool canInteract = false;

		public bool canRecieveInteraction = true;

		public bool canEquipWeapons = true;

		public bool hasHead = false;

		public DrawData drawData = new DrawData();

		public float headSize = 1f;

		public List<string> headTags = new List<string>();

		public ThingDef skyfaller;

		public List<string> apparelTagsToAllow = new List<string>();

		public bool canWearApparel = true;

		public SimpleCurve interactionChanceFromIndexCurve = new SimpleCurve
		{
			new CurvePoint(0f, 0f),
			new CurvePoint(200f, 0.3f),
			new CurvePoint(900f, 0.6f),
			new CurvePoint(2000f, 1f)
		};

		public BodyTypeDef bodyType;

		public CompProperties_RustedSoldier()
		{
			compClass = typeof(CompRustedSoldier);
		}

        public override void ResolveReferences(ThingDef parentDef)
        {
            base.ResolveReferences(parentDef);
			if(bodyType == null)
            {
				bodyType = BodyTypeDefOf.Thin;
			}
			apparelTagsToAllow.Add("Gunlink");
			apparelTagsToAllow.Add("NAT_Rust_All");
		}
    }
	public class CompRustedSoldier : ThingComp
	{
		public int interactionIndex;

		public CompProperties_RustedSoldier Props => (CompProperties_RustedSoldier)props;

		public RustedPawn Rust => parent as RustedPawn;

		public override void PostPostMake()
		{
			base.PostPostMake();
			interactionIndex = new IntRange(0, 70).RandomInRange;
		}

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref interactionIndex, "interactionIndex", 0);
		}
		public override void CompTick()
		{
			base.CompTick();
			if (Props.canInteract && Rust.Spawned && Rust.IsHashIntervalTick(350) && !Rust.stances.stunner.Stunned && Rust.Awake() && !Rust.Downed)
			{
				if (interactionIndex > 1 && Rand.Chance(Props.interactionChanceFromIndexCurve.Evaluate(interactionIndex)))
				{
					TryInteractRandomly();
				}
				else
				{
					interactionIndex++;
				}
			}
		}

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			if (Props.canInteract && DebugSettings.ShowDevGizmos)
			{
				yield return new Command_Action
				{
					defaultLabel = "DEV: Force interaction",
					action = delegate
					{
						TryInteractRandomly();
					}
				};
			}
		}

		private bool TryInteractRandomly()
		{
			if (Rust.Map != null && Rust.Faction != null)
			{
				List<Pawn> pawns = new List<Pawn>();
				List<Pawn> collection = Rust.Map.mapPawns.SpawnedPawnsInFaction(Rust.Faction);
				pawns.AddRange(collection);
				pawns.Shuffle<Pawn>();
				for (int i = 0; i < pawns.Count; i++)
				{
					Pawn p = pawns[i];
					if (p != Rust && !p.Downed && ((p is RustedPawn rust && rust.Comp?.Props?.canRecieveInteraction == true) || (p.RaceProps.Humanlike && Rand.Chance(0.2f))) && SocialInteractionUtility.IsGoodPositionForInteraction(Rust, p))
					{
						if (TryInteractWith(p, Props.interaction))
						{
							return true;
						}
					}
				}
			}
			return false;
		}
		public bool TryInteractWith(Pawn recipient, InteractionDef intDef)
		{
			Pawn pawn = this.parent as Pawn;
			List<RulePackDef> list = new List<RulePackDef>();
			string text;
			string str;
			LetterDef letterDef;
			LookTargets lookTargets;
			intDef.Worker.Interacted(pawn, recipient, list, out text, out str, out letterDef, out lookTargets);
			MoteMaker.MakeInteractionBubble(pawn, recipient, intDef.interactionMote, intDef.GetSymbol(pawn.Faction, null), intDef.GetSymbolColor(pawn.Faction));
			PlayLogEntry_Interaction playLogEntry_Interaction = new PlayLogEntry_Interaction(intDef, pawn, recipient, list);
			Find.PlayLog.Add(playLogEntry_Interaction);
			interactionIndex = 0;
			return true;
		}

		public bool CanWearApparel(Apparel apparel)
        {
			if (!Props.canWearApparel)
			{
				return false;
			}
			if(apparel.def.apparel.LastLayer.IsUtilityLayer || apparel.def.apparel.layers[0] == ApparelLayerDefOf.Belt || apparel.def.apparel.tags.SharesElementWith(Props.apparelTagsToAllow))
            {
				return true;
            }
			return false;
        }

		private float headApparelOffset = 0f;

		private float bodyApparelOffset = 0f;

		public override List<PawnRenderNode> CompRenderNodes()
        {
			List<PawnRenderNode> list = new List<PawnRenderNode>();
			RustedPawn rust = Rust;
			if (rust.apparel == null || rust.apparel.WornApparelCount == 0)
			{
				return list;
			}
			headApparelOffset = 0f;
			bodyApparelOffset = 0f;
			foreach (Apparel item in rust.apparel.WornApparel)
			{
				try
				{
					list.Add(ProcessApparel(item));
				}
				catch (Exception arg)
				{
					Log.Error($"Exception setting up node for {item.def.defName} on {rust}: {arg}");
				}
			}
			return list;
		}

		private PawnRenderNode ProcessApparel(Apparel ap)
		{
			if (ap.def.apparel.HasDefinedGraphicProperties)
			{
				return null;
			}
			PawnRenderNodeProperties pawnRenderNodeProperties = null;
			DrawData drawData = ap.def.apparel.drawData;
			ApparelLayerDef lastLayer = ap.def.apparel.LastLayer;
			bool flag = lastLayer == ApparelLayerDefOf.Overhead || lastLayer == ApparelLayerDefOf.EyeCover;
			bool? flagOffset = null;
			if (ap.def.apparel.parentTagDef == PawnRenderNodeTagDefOf.ApparelHead)
			{
				flag = true;
				flagOffset = true;
			}
			else if (ap.def.apparel.parentTagDef == PawnRenderNodeTagDefOf.ApparelBody)
            {
				flag = false;
				flagOffset = false;
			}
			if (Rust.Head != null && flag)
			{
				pawnRenderNodeProperties = new PawnRenderNodeProperties
				{
					debugLabel = ap.def.defName,
					parentTagDef = PawnRenderNodeTagDefOf.ApparelHead,
					workerClass = typeof(PawnRenderNodeWorker_RustApparel_Head),
					baseLayer = 70f + headApparelOffset,
					drawData = drawData
				};
			}
			else
			{
				pawnRenderNodeProperties = new PawnRenderNodeProperties
				{
					debugLabel = ap.def.defName,
					parentTagDef = PawnRenderNodeTagDefOf.ApparelBody,
					workerClass = typeof(PawnRenderNodeWorker_RustApparel_Body),
					baseLayer = 20f + bodyApparelOffset,
					drawData = drawData
				};
				if (drawData == null && !ap.def.apparel.shellRenderedBehindHead)
				{
					if (lastLayer == ApparelLayerDefOf.Shell)
					{
						pawnRenderNodeProperties.drawData = DrawData.NewWithData(new DrawData.RotationalData(Rot4.North, 88f));
						pawnRenderNodeProperties.oppositeFacingLayerWhenFlipped = true;
					}
					else if (ap.RenderAsPack())
					{
						pawnRenderNodeProperties.drawData = DrawData.NewWithData(new DrawData.RotationalData(Rot4.North, 93f), new DrawData.RotationalData(Rot4.South, -3f));
						pawnRenderNodeProperties.oppositeFacingLayerWhenFlipped = true;
					}
				}
			}
            if (flagOffset == false)
            {
				bodyApparelOffset++;
			}
            else if (flagOffset == true)
			{
				headApparelOffset++;
			}
			pawnRenderNodeProperties.pawnType = PawnRenderNodeProperties.RenderNodePawnType.Any;
			return new PawnRenderNode_RustApparel(Rust, pawnRenderNodeProperties, Rust.Drawer.renderer.renderTree, ap, flag);
		}
	}
}