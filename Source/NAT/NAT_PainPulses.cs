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
	public class CompProperties_PainFigure : CompProperties
	{
		public CompProperties_PainFigure()
		{
			compClass = typeof(CompPainFigure);
		}
	}
	public class CompPainFigure : ThingComp
	{
		public int ticksBeforePulse = 72;

		public bool active = true;

        public override void CompTickRare()
        {
			ticksBeforePulse--;
			if(ticksBeforePulse <= 0)
            {
				List<Pawn> list1 = new List<Pawn>();
				foreach (Pawn pawn in parent.MapHeld.mapPawns.AllPawnsSpawned)
				{
					if (IsPawnAffected(pawn, 10f))
					{
						list1.Add(pawn);
					}
					if (pawn.carryTracker.CarriedThing is Pawn target && IsPawnAffected(target, 10f))
					{
						list1.Add(target);
					}
				}
				foreach (Pawn p in list1)
				{
					Hediff hediff = p.health.hediffSet.GetFirstHediffOfDef(NATDefOf.NAT_InducedPain);
					if (hediff == null)
					{
						hediff = p.health.AddHediff(NATDefOf.NAT_InducedPain);
						hediff.Severity = new FloatRange(0.3f, 0.8f).RandomInRange;
					}
					hediff.Severity += new FloatRange(0.1f, 0.3f).RandomInRange;
					hediff.TryGetComp<HediffComp_Disappears>().ticksToDisappear += new IntRange(2000, 2500).RandomInRange;
					p.health.Notify_HediffChanged(hediff);
				}
				list1.Clear();
				DefDatabase<EffecterDef>.GetNamed("AgonyPulseExplosion").Spawn(parent.Position, parent.Map);
				parent.Destroy(DestroyMode.KillFinalize);
				return;
			}
            if (!active)
            {
				return;
            }
			List<Pawn> list2 = new List<Pawn>();
			foreach (Pawn pawn in parent.MapHeld.mapPawns.AllPawnsSpawned)
			{
				if (IsPawnAffected(pawn))
				{
					list2.Add(pawn);
				}
				if (pawn.carryTracker.CarriedThing is Pawn target && IsPawnAffected(target))
				{
					list2.Add(target);
				}
			}
			foreach(Pawn p in list2)
            {
				InducePain(p);
			}
			list2.Clear();
		}

        public override string CompInspectStringExtra()
        {
			string s = ""; 
			if (!active)
            {
				s = "DormantCompInactive".Translate();
			}
			if (DebugSettings.ShowDevGizmos)
			{
				s += "\n" + "DEV: ticks before pulse: " + ticksBeforePulse;
			}
			return s;

		}

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach(Gizmo g in base.CompGetGizmosExtra())
            {
				yield return g;
            }
			if (DebugSettings.ShowDevGizmos)
			{
				Command_Action command_Action4 = new Command_Action();
				command_Action4.defaultLabel = "DEV: Activate";
				command_Action4.groupable = false;
				command_Action4.action = delegate
				{
					ticksBeforePulse = 1;
				};
				yield return command_Action4;
			}
		}
        private bool IsPawnAffected(Pawn target, float radius = 5f)
		{
			if (target.Dead || target.health == null)
			{
				return false;
			}
			if (target.RaceProps.Humanlike || target.IsAnimal)
			{
				return target.PositionHeld.DistanceTo(parent.PositionHeld) <= radius;
			}
			return false;
		}
		public void InducePain(Pawn p)
        {
			Hediff hediff = p.health.hediffSet.GetFirstHediffOfDef(NATDefOf.NAT_InducedPain);
			if (hediff == null)
			{
				hediff = p.health.AddHediff(NATDefOf.NAT_InducedPain);
				hediff.Severity = new FloatRange(0.02f, 0.05f).RandomInRange;
			}
			hediff.Severity += new FloatRange(0.01f, 0.02f).RandomInRange;
			hediff.TryGetComp<HediffComp_Disappears>().ticksToDisappear += new IntRange(200, 500).RandomInRange;
			p.health.Notify_HediffChanged(hediff);
		}

        public override void PostExposeData()
        {
            base.PostExposeData();
			Scribe_Values.Look(ref active, "active", defaultValue: true);
			Scribe_Values.Look(ref ticksBeforePulse, "ticksBeforePulse", defaultValue: 72);
		}
    }

	public class CompUseEffect_AddPainToTargetPawns : CompUseEffect
	{
		private CompTargetable targetable;

		public override void Initialize(CompProperties props)
		{
			base.Initialize(props);
			targetable = parent.GetComp<CompTargetable>();
		}

		public override void DoEffect(Pawn usedBy)
		{
			if (!ModsConfig.AnomalyActive)
			{
				return;
			}
			if (targetable == null)
			{
				Log.Error("CompUseEffect_AddPainToTargetPawns requires a CompTargetable");
				return;
			}
			Thing[] array = targetable.GetTargets().ToArray();
			for (int i = 0; i < array.Length; i++)
			{
				if (array[i] is Pawn pawn)
				{
					if(pawn.RaceProps.IsFlesh)
                    {
						Hediff hediff = HediffMaker.MakeHediff(NATDefOf.NAT_InducedPain, pawn);
						if (pawn.health.hediffSet.HasHediff(NATDefOf.NAT_InducedPain))
						{
							Hediff hediff2 = pawn.health.hediffSet.GetFirstHediffOfDef(NATDefOf.NAT_InducedPain);
							hediff.Severity = hediff2.Severity + new FloatRange(0.1f, 0.3f).RandomInRange;
							hediff.TryGetComp<HediffComp_Disappears>().ticksToDisappear += hediff2.TryGetComp<HediffComp_Disappears>().ticksToDisappear;
							pawn.health.RemoveHediff(hediff2);
						}
						else
						{
							hediff.Severity = new FloatRange(0.2f, 0.7f).RandomInRange;
						}
						pawn.health.AddHediff(hediff);
					}
				}
			}
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

	public class CompTargetEffect_InducePain : CompTargetEffect
	{
		public override void DoEffectOn(Pawn user, Thing target)
		{
			if(target is Pawn pawn && pawn.RaceProps.IsFlesh)
            {
				Hediff hediff = HediffMaker.MakeHediff(NATDefOf.NAT_InducedPain, pawn);
				if (pawn.health.hediffSet.HasHediff(NATDefOf.NAT_InducedPain))
				{
					Hediff hediff2 = pawn.health.hediffSet.GetFirstHediffOfDef(NATDefOf.NAT_InducedPain);
					hediff.Severity = hediff2.Severity + new FloatRange(0.2f, 1f).RandomInRange;
					hediff.TryGetComp<HediffComp_Disappears>().ticksToDisappear += hediff2.TryGetComp<HediffComp_Disappears>().ticksToDisappear;
					pawn.health.RemoveHediff(hediff2);
				}
				else
				{
					hediff.TryGetComp<HediffComp_Disappears>().ticksToDisappear = new IntRange(15000, 20000).RandomInRange;
					hediff.Severity = new FloatRange(0.4f, 3f).RandomInRange;
				}
				pawn.health.AddHediff(hediff);
			}
		}
	}
}