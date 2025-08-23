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
using static Verse.HediffCompProperties_RandomizeSeverityPhases;

namespace NAT
{
	[StaticConstructorOnStartup]
	public class Building_CollectionCase : Building, IThingHolder
	{
		public bool glassBroken = false;

        public ThingOwner innerContainer;

		private GraphicData brokenGlassGraphicData;

        public Pawn pawn;

		public QuestPart_Collector questPart;

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			base.SpawnSetup(map, respawningAfterLoad);
			brokenGlassGraphicData = new GraphicData();
			brokenGlassGraphicData.CopyFrom(def.graphicData);
			brokenGlassGraphicData.texPath += "_Broken";
            base.DirtyMapMesh(Map);
        }


        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (glassBroken)
            {
                GraphicData graphicData = brokenGlassGraphicData;
                Mesh obj = graphicData.Graphic.MeshAt(Rotation);
                Vector3 drawPos = drawLoc;
                drawPos.y = AltitudeLayer.BuildingOnTop.AltitudeFor() + graphicData.drawOffset.y + 0.1f;
                Graphics.DrawMesh(obj, drawPos + graphicData.drawOffset.RotatedBy(Rotation), Quaternion.identity, graphicData.Graphic.MatAt(Rotation), 0);
            }
            else
            {
                base.DrawAt(drawLoc, flip);
            }
        }

        public override void PostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.PostApplyDamage(dinfo, totalDamageDealt);
			if(!glassBroken && (dinfo.Def.armorCategory.armorRatingStat == StatDefOf.ArmorRating_Sharp || dinfo.Def.armorCategory.armorRatingStat == StatDefOf.ArmorRating_Blunt))
            {
				BreakGlass();
			}
        }

		public void BreakGlass()
        {
			glassBroken = true;
			NATDefOf.GestatorGlassShattered.PlayOneShot(this);
            EjectContents();
            base.DirtyMapMesh(Map);
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            EjectContents();
            base.Destroy(mode);
        }

        public Building_CollectionCase()
        {
            innerContainer = new ThingOwner<Thing>(this);
        }

        public override string GetInspectString()
		{
			string text = base.GetInspectString();
			if (!glassBroken)
			{
				if (!text.NullOrEmpty())
				{
					text += "\n";
				}
				text += "NAT_AttackToOpen".Translate();
			}
			return text;
		}

		public override void ExposeData()
		{
			base.ExposeData();
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_References.Look(ref questPart, "questPart");
            Scribe_Values.Look(ref glassBroken, "glassBroken");
            Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
        }

        public virtual void EjectContents()
        {
            if(pawn != null)
            {
                questPart.stolenPawns.Remove(pawn);
                Hediff h = pawn.health.GetOrAddHediff(NATDefOf.NAT_CollectorHypnosis);
                h.Severity = 1f;
                GenPlace.TryPlaceThing(pawn, Position, Map, ThingPlaceMode.Near);
            }
            innerContainer.TryDropAll(Position, Map, ThingPlaceMode.Near);
            questPart?.cases.Remove(this);
        }

        public ThingOwner GetDirectlyHeldThings()
        {
            return innerContainer;
        }

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }
    }
}