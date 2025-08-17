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
	public class IncidentWorker_RustedMassArrival : IncidentWorker
	{
		public override float ChanceFactorNow(IIncidentTarget target)
		{
			if (!(target is Map map))
			{
				return base.ChanceFactorNow(target);
			}
			int num = map.mapPawns.PawnsInFaction(Faction.OfEntities).Where((Pawn p) => p.kindDef == NATDefOf.NAT_RustedMass).Count();
			return ((num > 0) ? (0.5f / (float)num) : 1f) * base.ChanceFactorNow(target);
		}

		protected override bool CanFireNowSub(IncidentParms parms)
		{
			Map map = (Map)parms.target;
			IntVec3 cell;
			return TryFindCell(out cell, map);
		}

		protected override bool TryExecuteWorker(IncidentParms parms)
		{
			Map map = (Map)parms.target;
			Skyfaller skyfaller = SpawnObeliskIncoming(map);
			if (skyfaller == null)
			{
				return false;
			}
			skyfaller.impactLetter = LetterMaker.MakeLetter("NAT_RustedMassArrival".Translate(), "NAT_RustedMassArrival_Desc".Translate(), LetterDefOf.NeutralEvent, new TargetInfo(skyfaller.Position, map));
			return true;
		}

		private Skyfaller SpawnObeliskIncoming(Map map)
		{
			if (!TryFindCell(out var cell, map))
			{
				return null;
			}
			Pawn p = PawnGenerator.GeneratePawn(NATDefOf.NAT_RustedMass);
			return SkyfallerMaker.SpawnSkyfaller(NATDefOf.NAT_RustedMassIncoming, p, cell, map);
		}

		private static bool TryFindCell(out IntVec3 cell, Map map)
		{
			return CellFinderLoose.TryFindSkyfallerCell(NATDefOf.NAT_RustedMassIncoming, map, TerrainAffordanceDefOf.Light, out cell, 10, default(IntVec3), -1, allowRoofedCells: true, allowCellsWithItems: false, allowCellsWithBuildings: false, colonyReachable: false, avoidColonistsIfExplosive: true, alwaysAvoidColonists: true, delegate (IntVec3 x)
			{
				if ((float)x.DistanceToEdge(map) < 20f + (float)map.Size.x * 0.1f)
				{
					return false;
				}
				foreach (IntVec3 item in CellRect.CenteredOn(x, 1, 1))
				{
					if (!item.InBounds(map) || !item.Standable(map) || item.Fogged(map))
					{
						return false;
					}
				}
				return true;
			});
		}
	}
}