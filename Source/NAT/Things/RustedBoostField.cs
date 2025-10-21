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
using static HarmonyLib.Code;
using static UnityEngine.GraphicsBuffer;

namespace NAT
{
	public class RustedBoostField : Thing
	{
		public int ticksLeft = 2500;

		public Faction rustFaction;

		public Pawn rust;

		private Color color = new Color(1f, 0.12f, 0.12f);

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			base.SpawnSetup(map, respawningAfterLoad);
			if(rustFaction == null)
			{
				rustFaction = Faction.OfEntities;
			}
		}

		protected override void Tick()
		{
			ticksLeft--;
			if (!this.IsHashIntervalTick(30))
			{
				return;
			}
			if (ticksLeft <= 0 && !base.Destroyed)
			{
				Destroy();
				return;
			}
			Map map = Map;
			foreach (IntVec3 cell in this.OccupiedRect().Cells.InRandomOrder())
			{
				List<Thing> list = map.thingGrid.ThingsListAt(cell).ToList();
				for (int i = 0; i < list.Count; i++)
				{
					if (list[i] is Pawn p)
					{
						Affect(p);
						return;
					}
				}
			}
			
		}

		public void Affect(Pawn pawn)
		{
			if(pawn.Faction.HostileTo(rustFaction))
			{
				pawn.TakeDamage(new DamageInfo(DamageDefOf.TornadoScratch, 10f, 1f, pawn.Position.ToVector2().AngleTo(Position.ToVector2()), rust));
			}
		}

		public override void DrawExtraSelectionOverlays()
		{
			GenDraw.DrawFieldEdges(this.OccupiedRect().Cells.ToList(), color);
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_References.Look(ref rustFaction, "rustFaction");
			Scribe_References.Look(ref rust, "rust", saveDestroyedThings: true);
			Scribe_Values.Look(ref ticksLeft, "ticksLeft", 0);
		}

		protected override void DrawAt(Vector3 drawLoc, bool flip = false)
		{
			
		}
	}
}
