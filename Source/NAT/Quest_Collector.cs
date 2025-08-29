using LudeonTK;
using RimWorld;
using RimWorld.BaseGen;
using RimWorld.IO;
using RimWorld.Planet;
using RimWorld.QuestGen;
using RimWorld.SketchGen;
using RimWorld.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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
using static UnityEngine.GraphicsBuffer;

namespace NAT
{
	public class CollectorLairParams : SitePartParams, IExposable
    {
		public QuestPart_Collector questPart;

		public new void ExposeData()
		{
			base.ExposeData();
			Scribe_References.Look(ref questPart, "questPart");
		}
	}
	public class QuestNode_Root_Collector : QuestNode
	{
		protected override void RunInt()
		{
			Slate slate = QuestGen.slate;
			Quest quest = QuestGen.quest;
			Map map = slate.Get<Map>("map");
			Faction faction = Faction.OfEntities;
			IntVec3 result = IntVec3.Invalid;
			if (!RCellFinder.TryFindRandomPawnEntryCell(out result, map, CellFinder.EdgeRoadChance_Hostile))
			{
				Log.Error("NAT.QuestNode_Root_Collector from New Anomaly Threats mod cannot find arrive spot for new collector");
				quest.End(QuestEndOutcome.Success, 0, null, QuestGen.slate.Get<string>("inSignal")); 
				return;
			}
			Rot4 rot = Rot4.FromAngleFlat((map.Center - result).AngleFlat);
			Pawn collector = PawnGenerator.GeneratePawn(new PawnGenerationRequest(NATDefOf.NAT_Collector, Faction.OfEntities, PawnGenerationContext.NonPlayer, map.Tile));
			GenSpawn.Spawn(collector, result, map, rot);
			CompCollector comp = collector.GetComp<CompCollector>();
			comp.active = true;
			comp.waitTicks = comp.Props.waitingTicksRange.RandomInRange;
			comp.state = CollectorState.Wait;
			string text = QuestGen.GenerateNewSignal("CollectorNotesRead");
			QuestPart_Collector questPart_Collector = new QuestPart_Collector();
			questPart_Collector.mapParent = map.Parent;
			questPart_Collector.collector = collector;
			questPart_Collector.inSignalEnable = QuestGen.slate.Get<string>("inSignal");
			questPart_Collector.outSignalsCompleted.Add(text);
			quest.AddPart(questPart_Collector);
			comp.questPart = questPart_Collector;
			quest.End(QuestEndOutcome.Success, 0, null, text);
		}

		protected override bool TestRunInt(Slate slate)
		{
			return slate.Exists("map");
		}
	}

	public class QuestPart_Collector : QuestPartActivable
	{
		private readonly IntRange CollectorTimeoutRange = new IntRange(60000, 180000);

		public MapParent mapParent;

		public Pawn collector;

		public List<Pawn> stolenPawns = new List<Pawn>();

		public List<Thing> stolenThings = new List<Thing>();

		public int returnTick;

		public bool currentlyOutside;

		public bool caught;

		public int ticksPassedCaught;

		public string debugTag = "Default";

		public List<Building_CollectionCase> cases = new List<Building_CollectionCase>();

		public bool casesGenerated = false;


        public override void ExposeData()
		{
			base.ExposeData();
			Scribe_References.Look(ref collector, "collector");
			Scribe_Values.Look(ref returnTick, "returnTick", 0);
			Scribe_Values.Look(ref currentlyOutside, "currentlyOutside");
			Scribe_Values.Look(ref caught, "caught");
            Scribe_Values.Look(ref casesGenerated, "casesGenerated");
            Scribe_Values.Look(ref debugTag, "debugTag", defaultValue: "Default");
			Scribe_Collections.Look(ref stolenThings, "stolenThings", LookMode.Deep);
			Scribe_Collections.Look(ref stolenPawns, "stolenPawns", saveDestroyedThings: true, LookMode.Reference);
            Scribe_Collections.Look(ref cases, "cases", saveDestroyedThings: true, LookMode.Reference);
            Scribe_References.Look(ref mapParent, "mapParent");
		}

		public Map GetMap()
        {
			Map map = mapParent?.Map;
			if (map == null || map.Disposed || map.IsPocketMap || !Current.Game.Maps.Contains(map))
			{
				if (!Find.Maps.TryRandomElement((Map m) => !m.IsPocketMap, out map))
				{
					if (!Find.Maps.TryRandomElement((Map m) => !m.IsPocketMap, out map))
					{
						return null;
					}
				}
				mapParent = map.Parent;
			}
			return map;
		}

        public override bool QuestPartReserves(Pawn p)
        {
            if ((!caught && p == collector) || stolenPawns.Contains(p))
            {
				return true;
            }
            return base.QuestPartReserves(p);
        }
        public override void QuestPartTick()
		{
			if(mapParent == null)
			{
				GetMap();
            }
			if (mapParent.IsHashIntervalTick(2500))
			{
                if (currentlyOutside && !caught && Find.TickManager.TicksGame > returnTick)
                {
                    if (!TryArriveOnMap())
                    {
                        returnTick += 60000;
                    }
                }
				if (casesGenerated && cases.NullOrEmpty())
				{
					Complete();
				}
            }
			
		}

		public bool TryArriveOnMap()
		{
			Map map = GetMap();
			if (map == null)
            {
				return false;
            }
			if (!RCellFinder.TryFindRandomPawnEntryCell(out var result, map, CellFinder.EdgeRoadChance_Hostile))
			{
				return false;
			}
			foreach(Ability ab in collector.abilities.AllAbilitiesForReading)
            {
				ab.ResetCooldown();
            }
			Rot4 rot = Rot4.FromAngleFlat((map.Center - result).AngleFlat);
			GenSpawn.Spawn(collector, result, map, rot);
			currentlyOutside = false;
			CompCollector comp = collector.GetComp<CompCollector>();
			comp.active = true;
			comp.waitTicks = comp.Props.waitingTicksRange.RandomInRange;
			comp.state = CollectorState.Wait;
			return true;
		}

		public void GenerateSite(Map map, Pawn reader)
		{
			/*foreach (Pawn p in stolenPawns)
			{
				GenSpawn.Spawn(p, reader.Position, map);
			}
			foreach (Thing t in stolenThings)
			{
				GenPlace.TryPlaceThing(t, reader.Position, map, ThingPlaceMode.Near);
			}*/
			TileFinder.TryFindNewSiteTile(out var tile, map.Tile, 3, 9, allowCaravans: false, AllowedLandmarks, 0.5f, canSelectComboLandmarks: true, TileFinderMode.Near);
			IEnumerable<SitePartDef> source = DefDatabase<SitePartDef>.AllDefs.Where((SitePartDef def) => def.tags != null && def.tags.Contains("NAT_CollectorLair"));
			Site site = GenerateSite(new SitePartDefWithParams[1]
			{
				new SitePartDefWithParams(source.RandomElementByWeight((SitePartDef sp) => sp.selectionWeight), new CollectorLairParams
				{
					threatPoints = 1216f,
					questPart = this
				})
			}, tile, null);
			Find.WorldObjects.Add(site);
            Find.LetterStack.ReceiveLetter("NAT_CollectorLairFound".Translate(), "NAT_CollectorLairFound_Desc".Translate(site.Label), LetterDefOf.PositiveEvent, site);
        }

		public void EscapeCollector(Pawn stolenPawn = null)
		{
			CompCollector comp = collector.GetComp<CompCollector>();
			stolenThings.AddRange(comp.innerContainer.ToList());
			comp.innerContainer.Clear();
			if (stolenPawn != null)
			{
				stolenPawns.Add(stolenPawn);
            }
			currentlyOutside = true;
			returnTick = Find.TickManager.TicksGame + CollectorTimeoutRange.RandomInRange;
            foreach (Hediff h in collector.health.hediffSet.hediffs.ToList())
            {
				if(h is Hediff_Injury i && !i.IsPermanent())
				{
					collector.health.RemoveHediff(i);
                }
            }
        }

		public void Notify_LairGenerated()
        {
            casesGenerated = true;

        }

		public override void DoDebugWindowContents(Rect innerRect, ref float curY)
		{
			if (base.State == QuestPartState.Enabled)
			{
				string text = "Stolen pawns:";
				foreach (Pawn p in stolenPawns)
                {
					text = text + "\n   " + p.Name.ToStringFull + "(" + p.Faction == null ? "factionless" : p.Faction.Name + ")";
				}
				text = text + "\nStolen things:";
				foreach (Thing t in stolenThings)
				{
					text = text + "\n   " + t.LabelCap + "(" + t.stackCount + ")";
				}
				text = text + "\nCurrently outside: " + currentlyOutside;
				if (currentlyOutside)
                {
					text = text + "\nTicks until return: " + (returnTick - Find.TickManager.TicksGame);
				}
				text = text + "\nCaught: " + caught;
				Vector2 val = Text.CalcSize(text);
				Rect rect = new Rect(innerRect.x, curY, innerRect.width, val.y + 40f);
				Widgets.Label(rect, text);
				curY += rect.height + 4f;
				Rect rect2 = new Rect(innerRect.x, curY, 500f, 25f);
				if (Widgets.ButtonText(rect2, "Return"))
				{
					TryArriveOnMap();
				}
				curY += rect2.height + 4f;
				Rect rect3 = new Rect(innerRect.x, curY, 500f, 25f);
				debugTag = Widgets.TextField(rect3, debugTag);
				curY += rect3.height;
			}
		}

		public static List<LandmarkDef> AllowedLandmarks
		{
			get
			{
				if (ModsConfig.OdysseyActive)
				{
					return new List<LandmarkDef>
					{
						LandmarkDefOf.Oasis,
						LandmarkDefOf.Lake,
						LandmarkDefOf.LakeWithIsland,
						LandmarkDefOf.LakeWithIslands,
						LandmarkDefOf.Pond,
						LandmarkDefOf.DryLake,
						LandmarkDefOf.ToxicLake,
						LandmarkDefOf.Wetland,
						LandmarkDefOf.HotSprings,
						LandmarkDefOf.CoastalIsland,
						LandmarkDefOf.Peninsula,
						LandmarkDefOf.Valley,
						LandmarkDefOf.Cavern,
						LandmarkDefOf.Chasm,
						LandmarkDefOf.Cliffs,
						LandmarkDefOf.Hollow,
						LandmarkDefOf.TerraformingScar,
						LandmarkDefOf.Dunes
					};
				}
				return null;
			}
		}

		public static Site GenerateSite(IEnumerable<SitePartDefWithParams> sitePartsParams, PlanetTile tile, Faction faction, bool hiddenSitePartsPossible = false, RulePack singleSitePartRules = null, WorldObjectDef worldObjectDef = null)
		{
			Slate slate = QuestGen.slate;
			bool flag = false;
			foreach (SitePartDefWithParams sitePartsParam in sitePartsParams)
			{
				if (sitePartsParam.def.defaultHidden)
				{
					flag = true;
					break;
				}
			}
			if (flag || hiddenSitePartsPossible)
			{
				SitePartParams parms = SitePartDefOf.PossibleUnknownThreatMarker.Worker.GenerateDefaultParams(0f, tile, faction);
				SitePartDefWithParams val = new SitePartDefWithParams(SitePartDefOf.PossibleUnknownThreatMarker, parms);
				sitePartsParams = sitePartsParams.Concat(Gen.YieldSingle(val));
			}
			Site site = SiteMaker.MakeSite(sitePartsParams, tile, faction, ifHostileThenMustRemainHostile: true, worldObjectDef);
			return site;
		}
	}
}