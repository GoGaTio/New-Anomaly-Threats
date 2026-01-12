using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using RimWorld.BaseGen;
using RimWorld.IO;
using RimWorld.Planet;
using RimWorld.QuestGen;
using RimWorld.SketchGen;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Grammar;
using Verse.Noise;
using Verse.Profile;
using Verse.Sound;
using Verse.Steam;
using UnityEngine;
using System.Diagnostics;

namespace NAT
{
	public class ScenPart_StartingRustedSoldier : ScenPart
	{
		private PawnKindDef pawnKind;

		private IEnumerable<PawnKindDef> PossibleRusts => DefDatabase<PawnKindDef>.AllDefs.Where((PawnKindDef td) => td.GetModExtension<RustedPawnExtention>()?.scenarioAvailable == true);

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Defs.Look(ref pawnKind, "pawnKind");
		}

		public override void DoEditInterface(Listing_ScenEdit listing)
		{
			Rect scenPartRect = listing.GetScenPartRect(this, 2f * ScenPart.RowHeight + 4f);
			if (Widgets.ButtonText(new Rect(scenPartRect.xMin, scenPartRect.yMin, scenPartRect.width, ScenPart.RowHeight), (pawnKind != null) ? pawnKind.LabelCap : "Random".Translate()))
			{
				List<FloatMenuOption> list = new List<FloatMenuOption>();
				list.Add(new FloatMenuOption("Random".Translate().CapitalizeFirst(), delegate
				{
					pawnKind = null;
				}));
				foreach (PawnKindDef possibleMech in PossibleRusts)
				{
					PawnKindDef localKind = possibleMech;
					list.Add(new FloatMenuOption(localKind.LabelCap, delegate
					{
						pawnKind = localKind;
					}));
				}
				Find.WindowStack.Add(new FloatMenu(list));
			}
		}

		public override void Randomize()
		{
			pawnKind = PossibleRusts.RandomElement();
		}

		public override string Summary(Scenario scen)
		{
			return ScenSummaryList.SummaryWithList(scen, "PlayerStartsWith", ScenPart_StartingThing_Defined.PlayerStartWithIntro);
		}

		public override IEnumerable<string> GetSummaryListEntries(string tag)
		{
			if (tag == "PlayerStartsWith")
			{
				yield return "NAT_RustedSoldier".Translate().CapitalizeFirst() + ": " + pawnKind.LabelCap;
			}
		}

		public override IEnumerable<Thing> PlayerStartingThings()
		{
			if (pawnKind == null)
			{
				pawnKind = PossibleRusts.RandomElement();
			}
			Pawn pawn = PawnGenerator.GeneratePawn(pawnKind, Faction.OfPlayer);
			yield return pawn;
		}

		public override int GetHashCode()
		{
			return base.GetHashCode() ^ ((pawnKind != null) ? pawnKind.GetHashCode() : 0);
		}
	}
}
