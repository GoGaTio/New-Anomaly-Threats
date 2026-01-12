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
	public class DamageWorker_Deadlife : DamageWorker
	{
		public override void ExplosionAffectCell(Explosion explosion, IntVec3 c, List<Thing> damagedThings, List<Thing> ignoredThings, bool canThrowMotes)
		{
			if(c.DistanceTo(explosion.Position) < explosion.radius / 2f && canThrowMotes)
			{
				GasUtility.AddDeadifeGas(c, explosion.Map, explosion.instigator?.Faction ?? Faction.OfEntities, 255);
			}
			else
			{
				GasUtility.MarkDeadlifeCorpsesForFaction(c, explosion.Map, explosion.instigator?.Faction ?? Faction.OfEntities, 255);
			}
			base.ExplosionAffectCell(explosion, c, damagedThings, ignoredThings, canThrowMotes);
		}
	}

	public class Verb_CastTargetEffectPainLance : Verb_CastBase
	{
		public override void DrawHighlight(LocalTargetInfo target)
		{
			base.DrawHighlight(target);
			GenDraw.DrawRadiusRing(target.Cell, 5f, Color.white);
		}
		public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
		{
			Pawn pawn = target.Pawn;
			if (pawn != null)
			{
				if (!pawn.RaceProps.IsFlesh)
				{
					if (showMessages)
					{
						Messages.Message("MessageBiomutationLanceInvalidTargetRace".Translate(pawn), caster, MessageTypeDefOf.RejectInput, null, historical: false);
					}
					return false;
				}
			}
			return base.ValidateTarget(target, showMessages);
		}

		protected override bool TryCastShot()
		{
			Pawn casterPawn = CasterPawn;
			IntVec3 cell = currentTarget.Cell;
			if (casterPawn == null || !cell.IsValid)
			{
				return false;
			}
			foreach (CompTargetEffect comp in base.EquipmentSource.GetComps<CompTargetEffect>())
			{
				foreach(Pawn pawn in CasterPawn.Map.mapPawns.AllPawnsSpawned.Where((Pawn p)=> p.Position.DistanceTo(cell) <= 5f && p.RaceProps.IsFlesh))
                {
					comp.DoEffectOn(CasterPawn, pawn);
				}
			}
			DefDatabase<EffecterDef>.GetNamed("AgonyPulseExplosion").Spawn(cell, CasterPawn.Map);
			base.ReloadableCompSource?.UsedOnce();
			return true;
		}
	}
}