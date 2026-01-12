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
using static NAT.IncidentWorker_RustedArmySiege;

namespace NAT
{
	[StaticConstructorOnStartup]
	public class RustedShieldGizmo : Gizmo
	{
		private CompRustedShield shield;

		private const float Width = 160f;

		private static readonly Texture2D EmptyBarTex = SolidColorMaterials.NewSolidColorTexture(GenUI.FillableBar_Empty);

		private const int BarThresholdTickIntervals = 100;

		public RustedShieldGizmo(CompRustedShield comp)
		{
			shield = comp;
		}

		public override float GetWidth(float maxWidth)
		{
			return 160f;
		}

		private Texture2D BarTex
        {
            get
            {
                if (shield.destroyed)
                {
					return SolidColorMaterials.NewSolidColorTexture(new Color32(95, 88, 88, byte.MaxValue));
				}
				return SolidColorMaterials.NewSolidColorTexture(new Color32(102, 95, 95, byte.MaxValue));
			}
        }

		private string Label
        {
            get
            {
                if (shield.destroyed)
                {
					return "NAT_Restored".Translate() + ": " + Mathf.FloorToInt(FillPercent * 100).ToString() + "%";
				}
				return shield.health + "/" + shield.Props.maxHealth;
			}
        }

		private float FillPercent
        {
            get
            {
                if (shield.destroyed)
                {
					return (float)shield.ticksSinceDestroyed / (float)shield.Props.ticksToRestore;
				}
				return (float)shield.health / (float)shield.Props.maxHealth;
			}
        }

		public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
		{
			Rect rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
			Rect rect2 = rect.ContractedBy(10f);
			Widgets.DrawWindowBackground(rect);
			string text = (string)"NAT_RustedShield".Translate();
			Rect rect3 = new Rect(rect2.x, rect2.y, rect2.width, Text.CalcHeight(text, rect2.width) + 8f);
			Text.Font = GameFont.Small;
			Widgets.Label(rect3, text);
			Rect barRect = new Rect(rect2.x, rect3.yMax, rect2.width, rect2.height - rect3.height);
			Widgets.FillableBar(barRect, FillPercent, BarTex, EmptyBarTex, doBorder: true);
			Text.Anchor = TextAnchor.MiddleCenter;
			Widgets.Label(barRect, Label);
			Text.Anchor = TextAnchor.UpperLeft;
			string tooltip;
			tooltip = "NAT_RustedShieldTip".Translate();
			TooltipHandler.TipRegion(rect2, () => tooltip, Gen.HashCombineInt(shield.GetHashCode(), 34242369));
			return new GizmoResult(GizmoState.Clear);
		}
	}

	[StaticConstructorOnStartup]
	public class Gizmo_RustedCommander : Gizmo
	{
		private static readonly float Width = 160f;

		private CompRustedCommander comp;

		public Gizmo_RustedCommander(CompRustedCommander comp)
		{
			this.comp = comp;
			Order = -100f;
		}

		public override float GetWidth(float maxWidth)
		{
			return Width;
		}

		public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
		{
			Rect rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
			Rect rect2 = rect.ContractedBy(6f);
			Widgets.DrawWindowBackground(rect);
			Rect rect3 = rect2;
			rect3.height = rect.height / 2f;
			Text.Font = GameFont.Small;
			Text.Anchor = TextAnchor.UpperLeft;
			Widgets.Label(rect3, "NAT_Drops".Translate() + ": " + comp.units + "/" + comp.Props.maxUnits);
			Text.Anchor = TextAnchor.UpperLeft;
			Text.Font = GameFont.Tiny;
			Rect rect4 = rect;
			rect4.y += rect3.height - 5f;
			rect4.height = rect.height / 2f;
			Text.Anchor = TextAnchor.MiddleCenter;
			if(comp.units < comp.Props.maxUnits)
            {
				Widgets.Label(rect4, "NAT_Restoring".Translate() + ": " + (comp.Props.ticksToRestore - comp.ticksSinceRestore).ToStringTicksToDays());
			}
			Text.Anchor = TextAnchor.UpperLeft;
			return new GizmoResult(GizmoState.Clear);
		}
	}

	[StaticConstructorOnStartup]
	public class Gizmo_HateShieldStatus : Gizmo
	{
		public CompHateShield shield;

		private static readonly Texture2D FullShieldBarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.74f, 0.11f, 0.11f));

		private static readonly Texture2D EmptyShieldBarTex = SolidColorMaterials.NewSolidColorTexture(Color.clear);

		public Gizmo_HateShieldStatus()
		{
			Order = -100f;
		}

		public override float GetWidth(float maxWidth)
		{
			return 140f;
		}

		public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
		{
			Rect rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
			Rect rect2 = rect.ContractedBy(6f);
			Widgets.DrawWindowBackground(rect);
			Rect rect3 = rect2;
			rect3.height = rect.height / 2f;
			Text.Font = GameFont.Tiny;
			Widgets.Label(rect3, shield.parent.LabelCap);
			Rect rect4 = rect2;
			rect4.yMin = rect2.y + rect2.height / 2f;
			float fillPercent = shield.energy / Mathf.Max(1f, shield.parent.GetStatValue(StatDefOf.EnergyShieldEnergyMax));
			Widgets.FillableBar(rect4, fillPercent, FullShieldBarTex, EmptyShieldBarTex, doBorder: false);
			Text.Font = GameFont.Small;
			Text.Anchor = TextAnchor.MiddleCenter;
			Widgets.Label(rect4, (shield.energy * 100f).ToString("F0") + " / " + (shield.Props.energyMax * 100f).ToString("F0"));
			Text.Anchor = TextAnchor.UpperLeft;
			TooltipHandler.TipRegion(rect2, shield.parent.def.description);
			return new GizmoResult(GizmoState.Clear);
		}
	}
}