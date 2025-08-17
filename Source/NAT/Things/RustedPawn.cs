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
	public class RustedPawnExtention : DefModExtension
	{
		public bool defaultDraftable = true;

		public bool scenarioAvailable = true;

		public bool sendDeathLetter = true;

		public bool nonPlayer = false;
	}

	public class RustedPawn : Pawn
	{
		public Need_RustRest restNeed;

		private CompRustedSoldier comp;

		public CompRustedSoldier Comp
		{
			get
			{
				if (comp == null)
				{
					comp = this.GetComp<CompRustedSoldier>();
				}
				return comp;
			}
		}

		private CompRustedCommander commander;

		public CompRustedCommander Commander
		{
			get
			{
				if (commander == null)
				{
					commander = this.GetComp<CompRustedCommander>();
				}
				return commander;
			}
		}

		private CompRustedWorker worker;

		public CompRustedWorker Worker
		{
			get
			{
				if (worker == null)
				{
					worker = this.GetComp<CompRustedWorker>();
				}
				return worker;
			}
		}

		public bool HasHead
		{
			get
			{
				if(Comp?.Props?.hasHead == true && Head != null)
                {
					return health.hediffSet.GetNotMissingParts().Any((BodyPartRecord x) => x.def.defName == "NAT_RustedHead");
				}
				return false;
			}
		}

		private RustHeadDef head;

		public RustHeadDef Head
		{
			get
			{
				return head;
			}
			set
			{
				if (head == value)
				{
					return;
				}
				head = value;
				this.Drawer.renderer.SetAllGraphicsDirty();
			}
		}
		public bool Draftable
		{
			get
			{
				if (kindDef.HasModExtension<RustedPawnExtention>() && !kindDef.GetModExtension<RustedPawnExtention>().defaultDraftable)
				{
					return false;
				}
				return Controllable;
			}
		}

		public bool Controllable
		{
			get
			{
				if (kindDef.HasModExtension<RustedPawnExtention>() && kindDef.GetModExtension<RustedPawnExtention>().nonPlayer)
				{
					return false;
				}
                
				return true;
			}
		}

		public bool PlayerControlled
		{
			get
			{
                if (!Controllable)
                {
					return false;
                }
				if (!Spawned)
				{
					return false;
				}
				return Faction?.IsPlayer == true;
			}
		}

        protected override void Tick()
		{
			base.Tick();
			if (Faction?.IsPlayer != false)
			{
				return;
			}
			if (this.IsHashIntervalTick(300) && Map != null && this.GetLord() == null)
			{
				LordMaker.MakeNewLord(Faction, new LordJob_AssaultColony(), MapHeld, new List<Pawn>() { this });
			}
		}
        public override void PostPostMake()
        {
            base.PostPostMake();
			if (Comp?.Props?.hasHead == true)
			{
				head = DefDatabase<RustHeadDef>.AllDefs.RandomElementByWeight((RustHeadDef x) => Comp.Props.headTags.Contains(x.tag) ? x.selectionWeight : 0);
			}
		}

        public override void DrawGUIOverlay()
		{
			base.DrawGUIOverlay();
            if (!Spawned || Map.fogGrid.IsFogged(Position) || WorldComponent_GravshipController.CutsceneInProgress)
            {
				return;
            }
			if (Name != null && Name.IsValid)
			{
				Vector2 pos = GenMapUI.LabelDrawPosFor(this, -0.7f);
				GenMapUI.DrawPawnLabel(this, pos);
			}
			else
			{
				Name = PawnBioAndNameGenerator.GeneratePawnName(this, NameStyle.Full);
			}
		}

		public static AcceptanceReport CanEquip(ThingWithComps equipment, RustedPawn rust)
		{
			string cantReason;
			if (equipment.def.IsRangedWeapon && rust.WorkTagIsDisabled(WorkTags.Shooting))
			{
				return "IsIncapableOfShootingLower".Translate(rust);
			}
			else if (!rust.CanReach(equipment, PathEndMode.ClosestTouch, Danger.Deadly))
			{
				return "NoPath".Translate().CapitalizeFirst();
			}
			else if (!rust.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
			{
				return "Incapable".Translate().CapitalizeFirst();
			}
			else if (equipment.IsBurning())
			{
				return "BurningLower".Translate();
			}
			else if (rust.IsQuestLodger() && !EquipmentUtility.QuestLodgerCanEquip(equipment, rust))
			{
				return "QuestRelated".Translate().CapitalizeFirst();
			}
			else if (!EquipmentUtility.CanEquip(equipment, rust, out cantReason, checkBonded: false))
			{
				return cantReason.CapitalizeFirst();
			}
			return true;
		}

		private IEnumerable<IReloadableComp> GetReloadablesUsingAmmo(Pawn pawn, Thing clickedThing)
		{
			if (pawn.equipment?.PrimaryEq != null && pawn.equipment.PrimaryEq is IReloadableComp reloadableComp && clickedThing.def == reloadableComp.AmmoDef)
			{
				yield return reloadableComp;
			}
			if (pawn.apparel == null)
			{
				yield break;
			}
			foreach (Apparel item in pawn.apparel.WornApparel)
			{
				IReloadableComp reloadableComp2 = item.TryGetComp<CompApparelReloadable>();
				if (reloadableComp2 != null && clickedThing.def == reloadableComp2.AmmoDef)
				{
					yield return reloadableComp2;
				}
			}
		}

		public IEnumerable<Gizmo> GetDraftedGizmos()
		{
			if (drafter.ShowDraftGizmo)
			{
				Command_Toggle command_Toggle = new Command_Toggle
				{
					hotKey = KeyBindingDefOf.Command_ColonistDraft,
					isActive = () => drafter.Drafted,
					toggleAction = delegate
					{
						drafter.Drafted = !drafter.Drafted;
						PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.Drafting, KnowledgeAmount.SpecificInteraction);
						if (drafter.Drafted)
						{
							LessonAutoActivator.TeachOpportunity(ConceptDefOf.QueueOrders, OpportunityType.GoodToKnow);
						}
					},
					defaultDesc = "CommandToggleDraftDesc".Translate(),
					icon = TexCommand.Draft,
					turnOnSound = SoundDefOf.DraftOn,
					turnOffSound = SoundDefOf.DraftOff,
					groupKeyIgnoreContent = 81729172,
					defaultLabel = (Drafted ? "CommandUndraftLabel" : "CommandDraftLabel").Translate()
				};
				if (this.Downed)
				{
					command_Toggle.Disable("IsIncapped".Translate(this.LabelShort, this));
				}
				command_Toggle.tutorTag = ((!Drafted) ? "Draft" : "Undraft");
				yield return command_Toggle;
			}
			if (Drafted && this.equipment.Primary != null && equipment.Primary.def.IsRangedWeapon)
			{
				yield return new Command_Toggle
				{
					hotKey = KeyBindingDefOf.Misc6,
					isActive = () => drafter.FireAtWill,
					toggleAction = delegate
					{
						drafter.FireAtWill = !drafter.FireAtWill;
					},
					icon = TexCommand.FireAtWill,
					defaultLabel = "CommandFireAtWillLabel".Translate(),
					defaultDesc = "CommandFireAtWillDesc".Translate(),
					tutorTag = "FireAtWillToggle"
				};
			}
		}

		public override IEnumerable<Gizmo> GetGizmos()
		{
			foreach (Gizmo g in base.GetGizmos())
			{
				yield return g;
			}
            if (!Spawned)
            {
				yield break;
            }
			bool flag = restNeed?.exhausted == true;
			if (Faction == Faction.OfPlayer && Draftable)
			{
				AcceptanceReport allowsDrafting = this.GetLord()?.AllowsDrafting(this) ?? ((AcceptanceReport)true);
				if (drafter != null)
				{
					foreach (Gizmo gizmo2 in GetDraftedGizmos())
					{
						if (!allowsDrafting && !gizmo2.Disabled)
						{
							gizmo2.Disabled = true;
							gizmo2.disabledReason = allowsDrafting.Reason;
						}
						else if (flag)
						{
							gizmo2.Disable("IsIncapped".Translate(this.LabelShort, this));
						}
						yield return gizmo2;
					}
				}
                if (!flag)
                {
					foreach (Gizmo attackGizmo in PawnAttackGizmoUtility.GetAttackGizmos(this))
					{
						if (!allowsDrafting && !attackGizmo.Disabled)
						{
							attackGizmo.Disabled = true;
							attackGizmo.disabledReason = allowsDrafting.Reason;
						}
						yield return attackGizmo;
					}
				}
			}
			if(abilities == null || abilities.AllAbilitiesForReading.NullOrEmpty())
            {
				yield break;
            }
			foreach (Ability a in abilities.AllAbilitiesForReading)
			{
				if (Faction == Faction.OfPlayer && !DebugSettings.ShowDevGizmos)
				{
					bool visibleSecondary = (Drafted || a.def.displayGizmoWhileUndrafted) && a.GizmosVisible();
					if (visibleSecondary)
					{
						
						foreach (Command gizmo in a.GetGizmos())
						{
							if (flag)
							{
								gizmo.Disable();
							}
							yield return gizmo;
						}
					}
				}
                if (!flag)
                {
					foreach (Gizmo item in a.GetGizmosExtra())
					{
						yield return item;
					}
				}
			}
		}
		public override void Kill(DamageInfo? dinfo, Hediff exactCulprit = null)
		{
			IntVec3 pos = PositionHeld;
			Map map = MapHeld;
			bool isPlayer = Faction == Faction.OfPlayerSilentFail;
			if (pos.IsValid && equipment?.Primary != null && equipment.Primary.TryGetComp<CompRustedBanner>(out var banner))
			{
				banner.ApplyEffect(pos, map);
				equipment.Remove(banner.parent);
                if (!banner.parent.Destroyed)
                {
					banner.parent.Destroy();
				}
			}
			float chance = this.GetStatValue(NATDefOf.NAT_CoreDropChance);
			if (Faction != Faction.OfPlayerSilentFail)
			{
				
			}
			foreach (Apparel ap in apparel.WornApparel.ToList())
			{
				if (ap.HitPoints < 35f)
				{
					apparel.Remove(ap);
					ap.Destroy();
				}
                else
                {
					ap.TakeDamage(new DamageInfo(DamageDefOf.Deterioration, 35f));
					apparel.TryDrop(ap);
				}
			}
			Caravan caravan = this.GetCaravan();
			base.Kill(dinfo, exactCulprit);
			RustedCore core = null;
			if (((map != null && pos.IsValid) || caravan != null) && Rand.Chance(chance))
            {
				core = (RustedCore)ThingMaker.MakeThing(NATDefOf.NAT_RustedCore);
				core.Rust = this;
				if (this.Discarded)
				{
					Log.Warning("New Anomaly Threats - " + Name.ToStringFull + " was discarded after core creation, fixing");
					ForceSetStateToUnspawned();
					DecrementMapIndex();
				}
				if (caravan == null)
                {
					GenPlace.TryPlaceThing(core, pos, map, ThingPlaceMode.Near);
				}
                else
                {
					caravan.AddPawnOrItem(core, false);
				}
            }
			if (isPlayer && RustedArmyUtility.Settings.rustedSoldierDeathNotification)
			{
				TaggedString diedLetterText = HealthUtility.GetDiedLetterText(this, dinfo, exactCulprit);
				LookTargets targets = null;
				if(core != null)
                {
					if (caravan == null)
					{
						targets = core;
					}
                    else
                    {
						targets = caravan;
					}
				}
                else if(pos.IsValid)
                {
					targets = new LookTargets(pos, map);
				}
				Find.LetterStack.ReceiveLetter("Death".Translate() + ": " + Name, diedLetterText, LetterDefOf.Death, targets);
			}
		}

		public override void PostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
		{
			base.PostApplyDamage(dinfo, totalDamageDealt);
			if (dinfo.Def.makesBlood && totalDamageDealt > 0f && Rand.Chance(0.5f))
			{
				health.DropBloodFilth();
			}
		}

		public override void DrawExtraSelectionOverlays()
        {
            base.DrawExtraSelectionOverlays();
			if (PlayerControlled)
			{
				pather.curPath?.DrawPath(this);
				jobs.DrawLinesBetweenTargets();
			}
		}

        public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Defs.Look(ref head, "head");
			if(Scribe.mode == LoadSaveMode.PostLoadInit && head== null && Comp?.Props?.hasHead == true)
            {
				head = DefDatabase<RustHeadDef>.AllDefs.RandomElementByWeight((RustHeadDef x) => Comp.Props.headTags.Contains(x.tag) ? x.selectionWeight : 0);
			}
		}
	}
}