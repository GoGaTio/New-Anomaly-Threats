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

namespace NAT
{
	public class PawnColumnWorker_DraftRusts : PawnColumnWorker
	{
		public override void DoCell(Rect rect, Pawn pawn, PawnTable table)
		{
			if(pawn is RustedPawn rust)
            {
				rect.xMin += (rect.width - 24f) / 2f;
				rect.yMin += (rect.height - 24f) / 2f;
				bool drafted = pawn.Drafted;
				Widgets.Checkbox(rect.position, ref drafted, 24f, paintable: def.paintable, disabled: (!rust.Draftable || !rust.Spawned || rust.DeadOrDowned || rust.restNeed.exhausted));
				if (drafted != pawn.Drafted)
				{
					pawn.drafter.Drafted = drafted;
				}
				if(Widgets.ButtonInvisible(new Rect(rect.position, new Vector2(24f, 24f)), false) && Event.current.shift)
                {
					foreach(Pawn p in table.PawnsListForReading)
                    {
						if(p is RustedPawn r && r != rust && drafted != r.Drafted && r.Draftable && !r.restNeed.exhausted && r.Spawned && !r.DeadOrDowned)
                        {
							r.drafter.Drafted = drafted;
						}
                    }
                }
			}
		}

		public override int GetMinWidth(PawnTable table)
		{
			return Mathf.Max(base.GetMinWidth(table), 24);
		}

		public override int GetMaxWidth(PawnTable table)
		{
			return Mathf.Min(base.GetMaxWidth(table), GetMinWidth(table));
		}

		public override int GetMinCellHeight(Pawn pawn)
		{
			return Mathf.Max(base.GetMinCellHeight(pawn), 24);
		}
	}

	public class PawnColumnWorker_RenameRust : PawnColumnWorker
	{
		public override void DoCell(Rect rect, Pawn pawn, PawnTable table)
		{
			if (pawn is RustedPawn rust)
			{
				TooltipHandler.TipRegionByKey(rect, "Rename");
				if (Widgets.ButtonImage(rect, TexButton.Rename))
				{
					Find.WindowStack.Add(NamePawnDialog(rust));
				}
			}
		}

		public static Dialog_NameRustedSoldier NamePawnDialog(Pawn pawn, string initialFirstNameOverride = null)
		{
			Dictionary<NameFilter, List<string>> suggestedNames = null;
			NameFilter editableNames;
			NameFilter visibleNames;
			if (pawn.babyNamingDeadline >= Find.TickManager.TicksGame || DebugSettings.ShowDevGizmos)
			{
				editableNames = NameFilter.First | NameFilter.Nick | NameFilter.Last;
				visibleNames = NameFilter.First | NameFilter.Nick | NameFilter.Last;
				List<string> list = new List<string>();
				list.RemoveDuplicates();
				suggestedNames = new Dictionary<NameFilter, List<string>> {
				{
					NameFilter.Last,
					list
				} };
			}
			else
			{
				visibleNames = NameFilter.First | NameFilter.Nick | NameFilter.Last | NameFilter.Title;
				editableNames = NameFilter.Nick | NameFilter.Title;
			}
			return new Dialog_NameRustedSoldier(pawn, visibleNames, editableNames, suggestedNames, initialFirstNameOverride);
		}

		public override int GetMinWidth(PawnTable table)
		{
			return 24;
		}

		public override int GetMaxWidth(PawnTable table)
		{
			return 24;
		}

		public override int GetMinCellHeight(Pawn pawn)
		{
			return Mathf.Max(base.GetMinCellHeight(pawn), 24);
		}
	}

	public class PawnColumnWorker_RustedWeapon : PawnColumnWorker_Icon
	{
		protected override Texture2D GetIconFor(Pawn pawn)
		{
			return pawn.equipment?.Primary?.def?.uiIcon;
		}

		protected override Color GetIconColor(Pawn pawn)
		{
			return XenotypeDef.IconColor;
		}

		protected override string GetIconTip(Pawn pawn)
		{
			if (pawn.equipment?.Primary != null)
			{
				return pawn.equipment.Primary.LabelCap;
			}
			return null;
		}
	}

	[StaticConstructorOnStartup]
	public class PawnColumnWorker_HealthRusts : PawnColumnWorker
	{
		public static readonly Texture2D HealthBarTex = SolidColorMaterials.NewSolidColorTexture(new Color32(102, 95, 95, 110));

		public override void DoCell(Rect rect, Pawn pawn, PawnTable table)
		{
			Widgets.FillableBar(rect.ContractedBy(4f), pawn.health.summaryHealth.SummaryHealthPercent, HealthBarTex, BaseContent.ClearTex, doBorder: false);
			Text.Font = GameFont.Small;
			Text.Anchor = TextAnchor.MiddleCenter;
			Widgets.Label(rect, Mathf.RoundToInt(pawn.health.summaryHealth.SummaryHealthPercent * 100f) + "%");
			Text.Anchor = TextAnchor.UpperLeft;
			Text.Font = GameFont.Small;
		}

		public override int GetMinWidth(PawnTable table)
		{
			return Mathf.Max(base.GetMinWidth(table), 120);
		}

		public override int GetMaxWidth(PawnTable table)
		{
			return Mathf.Min(base.GetMaxWidth(table), GetMinWidth(table));
		}
	}
}