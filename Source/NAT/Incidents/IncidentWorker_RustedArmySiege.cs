using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using RimWorld;
using RimWorld.BaseGen;
using RimWorld.IO;
using RimWorld.Planet;
using RimWorld.QuestGen;
using RimWorld.SketchGen;
using RimWorld.Utility;
using LudeonTK;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Grammar;
using Verse.Noise;
using Verse.Profile;
using Verse.Sound;
using Verse.Steam;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace NAT
{
	public class IncidentWorker_RustedArmySiege : IncidentWorker
	{
		public static float minPoints = -1f;
		public struct Unit
		{
			public Thing thing;

			public CellRect rect;

			public IntVec2 size;

			public Unit(Thing thing)
			{
				this.thing = thing;
				this.rect = CellRect.Empty;
				size = new IntVec2(1, 1);
			}
		}

		private static List<ThingDef> buildings;
		private static List<ThingDef> Buildings
        {
            get
            {
				if (buildings == null)
				{
					buildings = new List<ThingDef>();
					foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefsListForReading.Where(delegate (ThingDef x)
					{
						if (x.category != ThingCategory.Building)
						{
							return false;
						}
						return x.building.buildingTags.Contains("NAT_RustedSiegeMember");
					}))
					{
						buildings.Add(thingDef);
					}
				}
				return buildings;
            }
        }

		private static List<PawnGenOption> pawns;
		private static List<PawnGenOption> Pawns
		{
			get
			{
				if (pawns == null)
				{
					pawns = new List<PawnGenOption>();
					foreach (PawnGroupMaker groupMaker in FactionDefOf.Entities.pawnGroupMakers.Where((PawnGroupMaker x)=> x.kindDef == NATDefOf.NAT_RustedArmyDefence))
					{
						foreach(PawnGenOption op1 in groupMaker.options)
                        {
                            if (op1.kind.allowInMechClusters)
                            {
								PawnGenOption op2 = pawns.FirstOrDefault((PawnGenOption y) => y.kind == op1.kind);
								if(op2 == null)
                                {
									pawns.Add(op1);
								}
                                else
                                {
									op2.selectionWeight += op1.selectionWeight;
                                }
							}
                        }
					}
				}
				return pawns;
			}
		}

		protected override bool TryExecuteWorker(IncidentParms parms)
		{
			if(minPoints < 0f)
            {
				minPoints = Mathf.Min(Pawns.MinBy((PawnGenOption p) => p.kind.combatPower).kind.combatPower, Buildings.MinBy((ThingDef t) => t.building.combatPower).building.combatPower);
            }
			Map map = (Map)parms.target;
			return TryExecuteSiege(IntVec3.Invalid, map, parms.points);
        }

		public bool TryExecuteSiege(IntVec3 center, Map map, float points, string letterDescExtra = "")
		{
			if(points < 1000f)
			{
				points = 1000f;
			}
            List<Unit> list = GenerateUnits(points, map).ToList();
            GenerateProblems(list, points, map, out var extraDesc, out var descThingDef);
            if (!center.IsValid)
            {
                center = FindSiegePosition(map, list, 100);
                if (!center.IsValid)
                {
                    return false;
                }
            }
            SiegeArrive(center, list, map, out var targets);
            Find.LetterStack.ReceiveLetter("NAT_RustedArmySiege".Translate(), "NAT_RustedArmySiege_Desc".Translate() + extraDesc + letterDescExtra, LetterDefOf.ThreatBig, new LookTargets(targets), hyperlinkThingDefs: descThingDef == null ? null : new List<ThingDef>() { descThingDef });
            letterDescExtra = "";
            return true;
        }

		public void SiegeArrive(IntVec3 cell, List<Unit> units, Map map, out List<Thing> sentThings, int baseRectSize = 5)
        {
			List<Unit> sentUnits = new List<Unit>();
			List<Unit> waitingUnits = units.ToList();
			CellRect searchRect = new CellRect(cell.x, cell.z, 1, 1).ExpandedBy(baseRectSize).ClipInsideMap(map);
			int num1 = 0;
			int num2 = 0;
			while (sentUnits.Count < units.Count)
            {
				if(searchRect.TryFindRandomInnerRect(waitingUnits[0].size, out var rect, delegate (CellRect x)
				{
					foreach(Unit u in sentUnits)
                    {
                        if (u.rect.ExpandedBy(1).Overlaps(x))
                        {
							return false;
                        }
                    }
					foreach (IntVec3 c in x.Cells)
					{
						if (c.Impassable(map))
						{
							return false;
						}
						if (c.GetRoof(map)?.isThickRoof == true)
						{
							return false;
						}
					}
					return true;
                }))
                {
					Unit u = waitingUnits[0];
					u.rect = rect;
					sentUnits.Add(u);
					waitingUnits.RemoveAt(0);
					num1++;
					if(num1 > 10)
                    {
						searchRect = searchRect.ExpandedBy(2).ClipInsideMap(map);
						num1 = 0;
					}
				}
                else
                {
					searchRect = searchRect.ExpandedBy(2).ClipInsideMap(map);
					num2++;
					if(num2 > 100)
                    {
						break;
                    }
				}
            }
			sentThings = (from u in sentUnits
						 where u.thing != null
						 select u.thing).ToList();
			List<Pawn> lordPawns = new List<Pawn>();
			foreach(Unit unit in sentUnits)
            {
				Skyfaller_RustedChunk skyfaller = (Skyfaller_RustedChunk)SkyfallerMaker.SpawnSkyfaller(RustedArmyUtility.GetSkyfaller(unit.thing.def), unit.thing, unit.rect.CenterCell, map);
				skyfaller.frendlies = sentThings;
				skyfaller.faction = Faction.OfEntities;
				if (unit.thing is Pawn p)
                {
					lordPawns.Add(p);
                }
			}
            if (!lordPawns.NullOrEmpty())
            {
				LordMaker.MakeNewLord(Faction.OfEntities, new LordJob_RustedArmy(cell, 60, true), map, lordPawns);
			}
		}

		public static IEnumerable<Unit> GenerateUnits(float points, Map map)
		{
			bool num = false;
			while (points > minPoints)
            {
				Unit unit = new Unit(null);
				float cost = 0;
				if (!num || Rand.Chance(0.2f))
                {
					PawnKindDef kind = Pawns.RandomElementByWeight((PawnGenOption x) => x.Cost > points ? 0.001f : x.selectionWeight).kind;
					unit.thing = PawnGenerator.GeneratePawn(kind, Faction.OfEntities);
					cost = kind.combatPower;
					num = true;
                }
                else
                {
					ThingDef def = Buildings.RandomElementByWeight((ThingDef x) => !x.building.buildingTags.Contains("NAT_RustedSiegeProblem") && x.building.combatPower > points ? 0.001f : x.generateCommonality);
					unit.thing = ThingMaker.MakeThing(def);
					unit.thing.SetFaction(Faction.OfEntities);
					cost = def.building.combatPower;
					unit.size = def.size;
				}
				if(unit.thing == null)
                {
					continue;
                }
				yield return unit;
				points -= cost;
            }
		}

		public static void GenerateProblems(List<Unit> units, float points, Map map, out string extraLetterString, out ThingDef problemThingDef)
		{
			IEnumerable<ThingDef> items = Buildings.Where((ThingDef x) => x.building.buildingTags.Contains("NAT_RustedSiegeProblem"));
			if (items.EnumerableNullOrEmpty())
            {
				extraLetterString = "";
				problemThingDef = null;
				return;
            }
			problemThingDef = items.RandomElementByWeight((ThingDef y) => (y.building.combatPower > points ? 0.001f : y.generateCommonality));
			extraLetterString = problemThingDef.description;
			int num = 1;
			for(int i = 0; i < num; i++)
			{
				Unit unit = new Unit(null);
				unit.thing = ThingMaker.MakeThing(problemThingDef);
				unit.thing.SetFaction(Faction.OfEntities);
				unit.size = problemThingDef.size;
				units.Insert(0, unit);
			}
		}

		public static IntVec3 FindSiegePosition(Map map, List<Unit> units, int maxTries = 100)
		{
			IntVec3 result = IntVec3.Invalid;
			float num = float.MinValue;
			for (int j = 0; j < maxTries; j++)
			{
				IntVec3 intVec = CellFinderLoose.RandomCellWith((IntVec3 x) => x.Standable(map), map);
				if (!intVec.IsValid)
				{
					continue;
				}
				IntVec3 intVec2 = RCellFinder.FindSiegePositionFrom(intVec, map, allowRoofed: false, errorOnFail: false);
				if (intVec2.IsValid)
				{
					float clusterPositionScore2 = GetSiegePositionScore(intVec2, map, units);
					if (clusterPositionScore2 >= 1f)
					{
						return intVec2;
					}
					if (clusterPositionScore2 > num)
					{
						result = intVec;
						num = clusterPositionScore2;
					}
				}
			}
			if (!result.IsValid)
			{
				return CellFinder.RandomCell(map);
			}
			return result;
		}

		public static float GetSiegePositionScore(IntVec3 center, Map map, List<Unit> units)
		{
			int areaRequired = Mathf.CeilToInt(units.Sum((Unit u) => u.size.x * u.size.z) * 2f);
			int length = Mathf.CeilToInt(Mathf.Sqrt(areaRequired) / 2f);
			CellRect rect = new CellRect(center.x - length, center.y - length, center.x + length, center.y + length).ClipInsideMap(map);
			while(rect.Area < areaRequired)
            {
				rect = rect.ExpandedBy(2).ClipInsideMap(map);
			}
			int fogged = 0;
			int roofed = 0;
			int roofedThick = 0;
			int indoors = 0;
			int walls = 0;
			int heavyBuildings = 0;
			int impassableCells = 0;
			int badTerrain = 0;
			foreach (IntVec3 c in rect.Cells)
			{
				if (c.Fogged(map))
				{
					fogged++;
				}
				if (c.Roofed(map))
				{
					roofed++;
					if (c.GetRoof(map).isThickRoof)
					{
						roofedThick++;
					}
				}
				if (c.GetRoom(map) != null && !c.GetRoom(map).PsychologicallyOutdoors)
				{
					indoors++;
				}
                if (c.Impassable(map))
                {
					impassableCells++;
                }
                if (!c.GetTerrain(map).affordances.Contains(TerrainAffordanceDefOf.Medium))
                {
					badTerrain++;
				}
				foreach (Thing thing in c.GetThingList(map))
				{
					if(thing is Pawn p && p.IsColonist)
                    {
						return 0.05f;
                    }
					if (thing.def.preventSkyfallersLandingOn)
					{
						return -1f;
					}
					if (thing is Building)
					{
						if (!thing.def.destroyable)
						{
							return -1f;
						}
						if(thing.def.fillPercent >= 1f)
                        {
							walls++;
                        }
						if(thing.HitPoints >= 300f)
                        {
							heavyBuildings++;
						}
					}
				}
			}
			return (float)((float)areaRequired - ((float)fogged + (float)roofedThick + ((float)(indoors + walls + roofed + heavyBuildings) / 2f) + (float)impassableCells + (float)badTerrain)) / (float)areaRequired;
		}
	}
}