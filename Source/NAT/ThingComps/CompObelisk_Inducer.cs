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
	public class CompObelisk_Inducer : CompObelisk_ExplodingSpawner
	{
		public List<Pawn> targets = new List<Pawn>();

		public int actionsCount;

		private static readonly SimpleCurve ActionsFromPoints = new SimpleCurve
		{
			new CurvePoint(0f, 3f),
			new CurvePoint(1000f, 5f),
			new CurvePoint(8000f, 9f),
			new CurvePoint(10000f, 10f)
		};

		public override void TriggerInteractionEffect(Pawn interactor, bool triggeredByPlayer = false)
		{
			int num = Mathf.CeilToInt(ActionsFromPoints.Evaluate(StorytellerUtility.DefaultThreatPointsNow(parent.Map)));
            if (triggeredByPlayer)
            {
				num *= 3;
			}
			List<Pawn> list = new List<Pawn>();
			int colonistsUnffected = parent.Map.mapPawns.ColonistsSpawnedCount;
			int colonists = Mathf.CeilToInt(colonistsUnffected * 0.4f);
			foreach (Pawn p in parent.Map.mapPawns.AllPawnsSpawned.InRandomOrder().ToList())
			{
				if (Affectable(p) && TryCastNegativeAction(p, p.IsColonist, true, false, true))
                {
					if (p.IsColonist)
					{
						colonistsUnffected--;
					}
					list.Add(p);
					num--;
					if (num <= 0)
					{
						break;
					}
				}
			}
			string text = triggeredByPlayer ? "NAT_InducerTriggeredPlayer_Desc".Translate() : "NAT_InducerTriggered_Desc".Translate(interactor.Named("PAWN"));
			Find.LetterStack.ReceiveLetter(LetterMaker.MakeLetter("NAT_InducerTriggered_Label".Translate(), text, triggeredByPlayer ? LetterDefOf.NeutralEvent : LetterDefOf.ThreatSmall, list));
			bool Affectable(Pawn p)
			{
				if (!CanAffect(p))
				{
					return false;
				}
				if(p.IsColonist)
				{
					if (triggeredByPlayer)
					{
                        return false;
                    }
					if(colonistsUnffected <= colonists)
					{
						return false;
					}
				}
				return true;
			}
		}

		public bool TryCastNegativeAction(Pawn pawn, bool sendLetters, bool canSubdue = true, bool onlySuppress = false, bool disallowSupress = false)
		{
            if (!onlySuppress)
            {
				float num = Rand.Value;
				if (disallowSupress)
				{
					num -= 0.31f;
                }
				if (canSubdue && num < 0.1f)
				{
					return TrySubduePawn(pawn, sendLetters);
				}
				if (num < 0.3f)
				{
					return TryStartRage(pawn);
				}
				if (num < 0.5f)
				{
					return TryStartHallucinations(pawn);
				}
				if (num < 0.7f)
				{
					return TryShockPawn(pawn, sendLetters);
				}
			}
			return TrySuppressPawn(pawn, sendLetters);
		}

		public override void OnActivityActivated()
		{
			base.OnActivityActivated();
			Find.LetterStack.ReceiveLetter("NAT_InducerObeliskLetterLabel".Translate(), "NAT_InducerObeliskLetter".Translate(), LetterDefOf.ThreatBig, parent);
			actionsCount = Mathf.FloorToInt(ActionsFromPoints.Evaluate(pointsRemaining));
		}

		public Lord lord;

		public override void CompTick()
		{
			base.CompTick();
			if (activated && !base.ActivityComp.Deactivated && explodeTick <= 0 && Find.TickManager.TicksGame >= nextSpawnTick && warmupComplete)
			{
				List<Pawn> list = parent.Map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer).Where((Pawn p) => CanAffect(p) && p.IsColonist).Except(targets).ToList();
				if (list.NullOrEmpty() || Rand.Chance(0.3f))
				{
					List<Pawn> animals = new List<Pawn>();
					for (int i = 0; i < 3; i++)
                    {
						if (parent.Map.mapPawns.AllPawnsSpawned.TryRandomElement((Pawn x)=> x.IsAnimal && !x.Position.Fogged(x.Map) && (!x.InMentalState || !x.MentalStateDef.IsAggro) && !x.Downed && x.Faction == null, out var result))
						{
							result.SetFaction(Faction.OfEntities);
							animals.Add(result);
                        }
					}
					if(lord == null)
                    {
						LordMaker.MakeNewLord(Faction.OfEntities, new LordJob_AssaultColony(Faction.OfEntities, false, false, false, false, false), parent.Map, animals);
					}
                    else
                    {
						lord.AddPawns(animals);
                    }
					Find.LetterStack.ReceiveLetter("NAT_InducerEffect_Animals".Translate(), "NAT_InducerEffectDesc_Animals".Translate(), LetterDefOf.ThreatBig, animals);
					actionsCount--;
				}
                else
                {
					Pawn p = list.RandomElement();
					if (TryCastNegativeAction(p, true, list.Count > 2 ? true : false, list.Count == 1))
                    {
						targets.Add(p);
						actionsCount--;
					}
				}
				nextSpawnTick = Find.TickManager.TicksGame + SpawnIntervalTicks.RandomInRange;
				if (actionsCount <= 0f)
				{
					PrepareExplosion();
				}
			}
		}

		public bool CanAffect(Pawn p)
        {
			if (!p.RaceProps.Humanlike || p.IsSubhuman || p.mindState.mentalStateHandler.InMentalState || p.Downed)
			{
				return false;
			}
			return true;
		}

		public bool TrySubduePawn(Pawn pawn, bool sendLetter)
        {
			if (pawn.health.hediffSet.HasHediff(NATDefOf.NAT_Subdued))
            {
				return false;
            }
			pawn.health.AddHediff(NATDefOf.NAT_Subdued);
			if (lord == null)
			{
				LordMaker.MakeNewLord(Faction.OfEntities, new LordJob_AssaultColony(Faction.OfEntities, false, false, false, false, false), parent.Map, new List<Pawn>() { pawn });
			}
			else
			{
				lord.AddPawn(pawn);
			}
			if (sendLetter)
			{
				Find.LetterStack.ReceiveLetter(LetterMaker.MakeLetter("NAT_InducerEffect_Subdue".Translate(pawn.Named("PAWN")), "NAT_InducerEffectDesc_Subdue".Translate(pawn.Named("PAWN")), LetterDefOf.ThreatBig, pawn));
			}
			return true;
		}

		public bool TryShockPawn(Pawn pawn, bool sendLetter)
        {
			if (pawn.health.hediffSet.HasHediff(HediffDefOf.DarkPsychicShock))
			{
				return false;
			}
			Hediff h = pawn.health.AddHediff(HediffDefOf.DarkPsychicShock);
			h.TryGetComp<HediffComp_Disappears>().ticksToDisappear = 60000;
			if (sendLetter)
			{
				Find.LetterStack.ReceiveLetter(LetterMaker.MakeLetter("NAT_InducerEffect_Shock".Translate(), "NAT_InducerEffectDesc_Shock".Translate(pawn.Named("PAWN")), LetterDefOf.ThreatSmall, pawn));
			}
			return true;
		}

		public bool TrySuppressPawn(Pawn pawn, bool sendLetter)
        {
            if (pawn.needs.mood.thoughts.memories.GetFirstMemoryOfDef(NATDefOf.NAT_ObeliskSuppression) != null)
            {
				return false;
            }
			pawn.needs.mood.thoughts.memories.TryGainMemory(NATDefOf.NAT_ObeliskSuppression);
            if (sendLetter)
            {
				Find.LetterStack.ReceiveLetter(LetterMaker.MakeLetter("NAT_InducerEffect_Suppression".Translate(), "NAT_InducerEffectDesc_Suppression".Translate(pawn.Named("PAWN")), LetterDefOf.ThreatSmall, pawn));
			}
			return true;
        }

		public bool TryStartHallucinations(Pawn pawn)
		{
			return pawn.mindState.mentalBreaker.TryDoMentalBreak("NAT_ObeliskCausedBreak".Translate(), NATDefOf.TerrifyingHallucinations);
		}

		public bool TryStartRage(Pawn pawn)
		{
			return pawn.mindState.mentalBreaker.TryDoMentalBreak("NAT_ObeliskCausedBreak".Translate(), MentalBreakDefOf.BerserkShort);
		}

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			foreach (Gizmo item2 in base.CompGetGizmosExtra())
			{
				yield return item2;
			}
			if (DebugSettings.ShowDevGizmos)
			{
				yield return new Command_Action
				{
					defaultLabel = "DEV: transforn one",
					action = delegate
					{
						TrySubduePawn(parent.Map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer).Where((Pawn p) => p.RaceProps.Humanlike).RandomElement(), true);
					}
				};
			}

		}
	}
}