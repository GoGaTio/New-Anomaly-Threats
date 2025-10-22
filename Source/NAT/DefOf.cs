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
	[DefOf]
	public static class NATColorDefOf
	{
		public static ColorDef Structure_Red;

		public static ColorDef Structure_Blue;

		public static ColorDef Structure_Green;
	}

	[DefOf]
	public static class NATDefOf
	{
		public static ThingDef NAT_WarpedObelisk_Inducer;

		public static ThingDef NAT_RustedMassIncoming;

		public static ThingDef NAT_RustedTrooperIncoming;

		public static ThingDef NAT_RustedWall;

		public static ThingDef NAT_RustedDoor;

		public static ThingDef NAT_RustedDoor_Double;

		public static ThingDef NAT_RustedTurret_Mini;

		public static ThingDef NAT_RustedTurret_Auto;

		public static ThingDef NAT_RustedTurret_Sniper;

		public static ThingDef NAT_RustedTurret_Foam;

		public static ThingDef NAT_RustedBeacon_Reinforcements;

		public static ThingDef NAT_RustedChunkPawnIncoming;

		public static ThingDef NAT_RustedChunk1x1Incoming;

		public static ThingDef NAT_RustedChunk2x2Incoming;

		public static ThingDef NAT_RustedChunk3x3Incoming;

		public static TerrainDef NAT_RustedFloor;

		//public static ThingDef NAT_RustedFreezer;

		public static ThingDef NAT_RustedCore;

		public static ThingDef NAT_RustedPallet;

		public static ThingDef NAT_RustedBroadcastDish;

		public static ThingDef NAT_CollectorNotes;

		public static ThingDef NAT_CollectorGlassCase;

		public static ThingDef NAT_SignalAction_Sightstealers;

		public static ThingDef NAT_CollectorLairExit;

		public static ThingDef NAT_RustedArmyBanner;

		public static JobDef NAT_CollectorStealPawn;

		public static JobDef NAT_CollectorStealThing;

		public static JobDef NAT_CollectorWait;

		public static JobDef NAT_CollectorEscape;

		public static JobDef NAT_BuildRust;

		public static JobDef NAT_UseItemByRust;

		public static JobDef NAT_Seal;

		public static HediffDef NAT_InducedPain;

		public static HediffDef NAT_Subdued;

		public static HediffDef NAT_BilePowerSerum;

		public static HediffDef NAT_SlowedByBile;

		public static HediffDef NAT_EmotionSuppression;

		public static HediffDef NAT_BannerBoost;

		public static HediffDef NAT_CollectorHypnosis;

		public static PawnKindDef NAT_Collector;

		public static PawnKindDef NAT_RustedMass;

		public static PawnKindDef NAT_RustedBannerman;

		public static PawnKindDef NAT_RustedSoldier;

		public static PawnKindDef NAT_RustedOfficer;

		public static PawnGroupKindDef NAT_RustedArmy;

		public static PawnGroupKindDef NAT_RustedArmyDefence;

		public static PawnGroupKindDef NAT_RustedArmyBarracks;

		public static PawnGroupKindDef NAT_Serpents;

		public static PawnGroupKindDef NAT_SightstealersCollector;

		public static PawnTableDef NAT_Rusts;

		public static PawnTableDef NAT_RustsWork;

		public static NeedDef NAT_RustRest; //heh

		public static DutyDef NAT_SerpentAssault;

		public static DutyDef NAT_RustAssaultColony;

		public static DutyDef NAT_RustDefend;

		public static DutyDef NAT_CreateRustForPsychicRitual;

		public static StatDef NAT_CoreDropChance;

		public static LayoutRoomDef NAT_OutpostCorridor;

		public static LayoutRoomDef NAT_CitadelCorridor;

		public static LayoutRoomDef NAT_CollectionRoom;

		public static ThoughtDef NAT_ObeliskSuppression;

		public static MentalBreakDef TerrifyingHallucinations;

		[MayRequireOdyssey]
		public static OrbitalDebrisDef NAT_RustedDebris;

		public static PrefabDef NAT_RustedDish;

		public static PrefabDef NAT_RustedAutoTurretLabyrinth;

		public static EffecterDef NAT_BannerBoostEffect;

		public static SoundDef NAT_World_RustedBannerCall;

        public static SoundDef GestatorGlassShattered;

        public static QuestScriptDef NAT_CollectorScript;

		//public static QuestScriptDef NAT_CollectorLair;

		public static GenStepDef NAT_UndergroundLayout;

		public static DamageDef NAT_RustedBomb;

		public static IncidentDef NAT_RustedArmySiege;

		public static ThingSetMakerDef NAT_CollectoirLairCase;
	}
}
