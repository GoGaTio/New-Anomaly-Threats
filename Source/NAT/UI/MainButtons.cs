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

	public class PawnTable_Rusts : PawnTable
	{
		protected override IEnumerable<Pawn> LabelSortFunction(IEnumerable<Pawn> input)
		{
			return input.OrderBy(displayOrderGetter);
		}

		private static Func<Pawn, int> displayOrderGetter = (Pawn x) => Mathf.RoundToInt(x.kindDef.combatPower);

		public PawnTable_Rusts(PawnTableDef def, Func<IEnumerable<Pawn>> pawnsGetter, int uiWidth, int uiHeight)
			: base(def, pawnsGetter, uiWidth, uiHeight)
		{
		}
	}

	public class MainTabWindow_Rusts : MainTabWindow_PawnTable
	{
		private PawnTable table;

		private bool workTab = false;
		protected override PawnTableDef PawnTableDef => NATDefOf.NAT_Rusts;

		protected override IEnumerable<Pawn> Pawns => from p in Find.CurrentMap.mapPawns.PawnsInFaction(Faction.OfPlayer)
													  where p is RustedPawn rust && rust.Controllable
													  select p;

		public override void DoWindowContents(Rect rect)
		{
            if (workTab)
            {
				table.PawnTableOnGUI(new Vector2(rect.x, rect.y + ExtraTopSpace));
			}
            else
            {
				base.DoWindowContents(rect);
			}
			Rect rect1 = new Rect(rect.x, rect.y, 100f, 32f);
			Rect rect2 = new Rect(rect.x + 120f, rect.y, 100f, 32f);
			Text.Font = GameFont.Small;
			if (Widgets.ButtonText(rect1, "NAT_SelectAllRusts".Translate()))
			{
				Find.Selector.ClearSelection();
				foreach(Pawn p in Pawns)
                {
					if (p.Spawned)
					{
						Find.Selector.Select(p);
					}
				}
			}
		}

		public override void PostOpen()
		{
			base.PostOpen();
			if (table == null)
			{
				table = CreateTable();
			}
			table.SetDirty();
		}

		private PawnTable CreateTable()
		{
			return (PawnTable)Activator.CreateInstance(NATDefOf.NAT_RustsWork.workerClass, NATDefOf.NAT_RustsWork, (Func<IEnumerable<Pawn>>)(() => Pawns), UI.screenWidth - (int)(Margin * 2f), (int)((float)(UI.screenHeight - 35) - ExtraBottomSpace - ExtraTopSpace - Margin * 2f));
		}	
	}
	public class MainButtonWorker_ToggleRustsTab : MainButtonWorker_ToggleTab
	{
		public override bool Disabled
		{
			get
			{
				if (base.Disabled)
				{
					return true;
				}
				Map currentMap = Find.CurrentMap;
				if (currentMap != null)
				{
					List<Pawn> list = currentMap.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer);
					for (int i = 0; i < list.Count; i++)
					{
						if (list[i] is RustedPawn rust && rust.Controllable)
						{
							return false;
						}
					}
					List<Pawn> list2 = currentMap.mapPawns.PawnsInFaction(Faction.OfPlayer);
					for (int j = 0; j < list2.Count; j++)
					{
						if (list2[j] is RustedPawn rust && rust.Controllable)
						{
							return false;
						}
					}
				}
				return true;
			}
		}

		public override bool Visible => !Disabled;
	}
}