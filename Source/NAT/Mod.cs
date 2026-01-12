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
	public class NewAnomalyThreatsSettings : ModSettings
    {

		public bool rustedSoldierName_Draft = true;
		public bool rustedSoldierName_NoDraft = true;
		public bool rustedSoldierWeaponChange = true;
		public bool rustedSoldierDeathNotification = true;
		public bool allowEndGameRaid = true;

		public override void ExposeData()
		{
			Scribe_Values.Look(ref rustedSoldierName_Draft, "rustedSoldierName_Draft", true);
			Scribe_Values.Look(ref rustedSoldierName_NoDraft, "rustedSoldierName_Draft", true);
			Scribe_Values.Look(ref rustedSoldierWeaponChange, "rustedSoldierWeaponChange", true);
			Scribe_Values.Look(ref rustedSoldierDeathNotification, "rustedSoldierDeathNotification", true);
			Scribe_Values.Look(ref allowEndGameRaid, "allowEndGameRaid", true);
			base.ExposeData();
		}
	}

	public class NewAnomalyThreatsMod : Mod
	{

		NewAnomalyThreatsSettings settings;

		public NewAnomalyThreatsMod(ModContentPack content) : base(content)
		{
			this.settings = GetSettings<NewAnomalyThreatsSettings>();
		}

		public override void DoSettingsWindowContents(Rect inRect)
		{
			Listing_Standard listingStandard = new Listing_Standard();
			listingStandard.Begin(inRect);
			//listingStandard.CheckboxLabeled("NAT_Setting_NameDraft".Translate(), ref settings.rustedSoldierName_Draft, "NAT_Setting_NameDraft_Desc".Translate());
			//listingStandard.CheckboxLabeled("NAT_Setting_NameNoDraft".Translate(), ref settings.rustedSoldierName_NoDraft, "NAT_Setting_NameNoDraft_Desc".Translate());
			//listingStandard.CheckboxLabeled("NAT_Setting_WeaponChange".Translate(), ref settings.rustedSoldierWeaponChange, "NAT_Setting_WeaponChange_Desc".Translate());
			//listingStandard.CheckboxLabeled("NAT_Setting_DeathNotification".Translate(), ref settings.rustedSoldierDeathNotification, "NAT_Setting_DeathNotification_Desc".Translate());
			listingStandard.CheckboxLabeled("NAT_Setting_AllowRaid".Translate(), ref settings.allowEndGameRaid, "NAT_Setting_AllowRaid_Desc".Translate());
			listingStandard.End();
			base.DoSettingsWindowContents(inRect);
		}
		public override string SettingsCategory()
		{
			return "New Anomaly Threats";
		}
	}
}
