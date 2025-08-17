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
	public class CompProperties_RustedSculpture : CompProperties_Interactable
	{
		public PawnKindDef pawnKind;

		public Vector3 offset;

		public CompProperties_RustedSculpture()
		{
			compClass = typeof(CompRustedSculpture);
		}
	}
	public class CompRustedSculpture : CompInteractable
	{
		private new CompProperties_RustedSculpture Props => (CompProperties_RustedSculpture)props;

		private RustHeadDef head;

		public override AcceptanceReport CanInteract(Pawn activateBy = null, bool checkOptionalItems = true)
		{
			if (activateBy != null)
			{
				if (checkOptionalItems && !activateBy.HasReserved(ThingDefOf.Shard) && !ReservationUtility.ExistsUnreservedAmountOfDef(parent.MapHeld, ThingDefOf.Shard, Faction.OfPlayer, 1, (Thing t) => activateBy.CanReserveAndReach(t, PathEndMode.Touch, Danger.None)))
				{
					return "NAT_RustedSculptureActivateMissingShards".Translate();
				}
			}
			else if (checkOptionalItems && !ReservationUtility.ExistsUnreservedAmountOfDef(parent.MapHeld, ThingDefOf.Shard, Faction.OfPlayer, 1))
			{
				return "NAT_RustedSculptureActivateMissingShards".Translate();
			}
			return base.CanInteract(activateBy, checkOptionalItems);
		}

		public override void OrderForceTarget(LocalTargetInfo target)
		{
			if (ValidateTarget(target, showMessages: false))
			{
				OrderActivation(target.Pawn);
			}
		}

        public override void PostPostMake()
        {
            base.PostPostMake();
			TrySetHead();
		}

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
			if(head == null)
            {
				TrySetHead();
			}
			base.PostSpawnSetup(respawningAfterLoad);
		}

        private void TrySetHead()
        {
			CompProperties_RustedSoldier compProps = Props.pawnKind.race.GetCompProperties<CompProperties_RustedSoldier>();
			if (compProps != null && compProps.hasHead)
			{
				head = DefDatabase<RustHeadDef>.AllDefs.RandomElementByWeight((RustHeadDef x) => compProps.headTags.Contains(x.tag) ? x.selectionWeight : 0);
			}
		}

        public override string CompInspectStringExtra()
        {
			string s = base.CompInspectStringExtra();
			if (DebugSettings.ShowDevGizmos)
			{
				if (s.NullOrEmpty())
				{
					s = "";
				}
				else
				{
					s += "\n";
				}
				s += "DEV. Head: " + head == null ? "null" : head.defName;
			}
			return s;
		}

        public override void DrawAt(Vector3 drawLoc, bool flip)
		{
			Log.Message("draw");
			if (head == null)
            {
				return;
            }
			
			Mesh obj = head.graphicData.Graphic.MeshAt(Rot4.North);
			obj = MeshPool.GridPlaneFlip(obj);
			Vector3 drawPos = drawLoc;
			drawPos.y = AltitudeLayer.BuildingOnTop.AltitudeFor() + parent.def.graphicData.drawOffset.y;
			Graphics.DrawMesh(obj, drawPos + Props.offset, Quaternion.identity, head.graphicData.Graphic.MatAt(Rot4.North), 0);
		}

		protected override void OnInteracted(Pawn caster)
		{
			RustedPawn rust = PawnGenerator.GeneratePawn(Props.pawnKind, caster.Faction) as RustedPawn;
			rust.Head = head;
			rust.ageTracker.AgeBiologicalTicks = 0;
			rust.ageTracker.AgeChronologicalTicks = 0;
			rust.equipment.DestroyAllEquipment();
			rust.inventory.DestroyAll();
			rust.apparel.DestroyAll();
			rust.GetComp<CompRustedShield>()?.Destroy(false);
			GenSpawn.Spawn(rust, parent.PositionHeld, parent.MapHeld);
			parent.Destroy();
		}

		private void OrderActivation(Pawn pawn)
		{
			if (pawn.TryFindReserveAndReachableOfDef(ThingDefOf.Shard, out var thing))
			{
				Job job = JobMaker.MakeJob(JobDefOf.InteractThing, parent, thing);
				job.count = 1;
				job.playerForced = true;
				pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
			}
		}

		public override void PostExposeData()
        {
            base.PostExposeData();
			Scribe_Defs.Look(ref head, "head");
			if (Scribe.mode == LoadSaveMode.PostLoadInit && head == null)
			{
				TrySetHead();
			}
		}
    }
}