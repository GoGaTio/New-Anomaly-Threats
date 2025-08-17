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
	public class ThingSetMaker_FridgePawn : ThingSetMaker
	{
		public PawnKindDef killerKind;

		public bool allowApparel = false;

		public IntRange countRange;

		public FloatRange? corpseAgeRangeDays;

		private Pawn killer;

		public Pawn RandomPawn()
        {
			Find.FactionManager.AllFactionsVisible.TryRandomElement((Faction f) => f.def.humanlikeFaction && !f.Hidden && !f.temporary && !f.IsPlayer && !f.defeated && ((f.def.basicMemberKind != null) || (f.def.pawnGroupMakers.Any((PawnGroupMaker x) => x.options.Any((PawnGenOption y) => y.kind.race.race.meatDef == ThingDefOf.Meat_Human)))), out var faction);
			return PawnGenerator.GeneratePawn(PawnKindFromFaction(faction), faction);
		}

		public PawnKindDef PawnKindFromFaction(Faction f)
        {
			PawnKindDef kind = null;
			int i = 0;
			while(i < 50)
            {
				kind = f.RandomPawnKind();
				if(kind.race.race.meatDef == ThingDefOf.Meat_Human)
                {
					return kind;
                }
				i++;
            }
			return f.def.basicMemberKind ?? PawnKindDefOf.Villager;
		}

		private Pawn CorpsePawn(ThingSetMakerParams parms)
        {
			Faction faction = parms.makingFaction;
			if (faction == null)
            {
				return RandomPawn();
			}
			return PawnGenerator.GeneratePawn(PawnKindFromFaction(GenStep_RustedBase.defeatedFaction ?? parms.makingFaction), GenStep_RustedBase.defeatedFaction ?? parms.makingFaction);
		}
		protected override void Generate(ThingSetMakerParams parms, List<Thing> outThings)
		{
			killer = PawnGenerator.GeneratePawn(killerKind, Faction.OfEntities);
			int count = countRange.RandomInRange;
			for (int i = 0; i < count; i++)
            {
				Pawn pawn = CorpsePawn(parms);
                if (!allowApparel)
                {
					pawn.apparel.DestroyAll();
					pawn.equipment.DestroyAllEquipment();
					pawn.inventory.DestroyAll();
                }
				HealthUtility.SimulateKilledByPawn(pawn, killer);
				Corpse corpse = pawn.Corpse;
				if (corpse == null)
				{
					continue;
				}
				if (corpseAgeRangeDays.HasValue)
				{
					int num = Mathf.RoundToInt(corpseAgeRangeDays.Value.RandomInRange * 60000f);
					pawn.Corpse.timeOfDeath = Find.TickManager.TicksGame - num;
				}
				outThings.Add(corpse);
			}
		}

		protected override IEnumerable<ThingDef> AllGeneratableThingsDebugSub(ThingSetMakerParams parms)
		{
			yield return ThingDefOf.Human;
		}

		public override IEnumerable<string> ConfigErrors()
		{
			if (killerKind == null)
			{
				yield return "killerKind is null.";
			}
		}
	}

	public class ThingSetMaker_Rusts : ThingSetMaker
	{
		public IntRange countRange;

		public FloatRange ageRange;

		public PawnKindDef kindDef;

		public ThingDef weaponOverride;

		protected override void Generate(ThingSetMakerParams parms, List<Thing> outThings)
		{
			int num = countRange.RandomInRange;
			float age = ageRange.RandomInRange;
			PawnGenerationRequest req = new PawnGenerationRequest(kindDef, Faction.OfEntities, fixedBiologicalAge: age, fixedChronologicalAge: age);
			for (int i = 0; i < num; i++)
			{
				Pawn rust = PawnGenerator.GeneratePawn(req);
				if(weaponOverride != null)
                {
					rust.equipment.DestroyAllEquipment();
					rust.equipment.AddEquipment(ThingMaker.MakeThing(weaponOverride) as ThingWithComps);
				}
				rust.inventory.DestroyAll();
				rust.apparel.DestroyAll();
				outThings.Add(rust);
			}
		}

		protected override IEnumerable<ThingDef> AllGeneratableThingsDebugSub(ThingSetMakerParams parms)
		{
			yield return kindDef.race;
		}
	}
}
