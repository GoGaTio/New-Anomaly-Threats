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
	public class HediffCompProperties_BannerBoost : HediffCompProperties
	{
		public int ticksToDecreaseLevel;

		public HediffCompProperties_BannerBoost()
		{
			compClass = typeof(HediffComp_BannerBoost);
		}
	}
	public class HediffComp_BannerBoost : HediffComp
	{
		public int ticksToDecreaseLevel;
		public HediffCompProperties_BannerBoost Props => (HediffCompProperties_BannerBoost)props;
		public override string CompLabelInBracketsExtra => "LevelNum".Translate(Mathf.RoundToInt(parent.Severity)).ToString();

		public override void CompPostTickInterval(ref float severityAdjustment, int delta)
		{
			if (base.Pawn.IsHashIntervalTick(200, delta))
			{
				ticksToDecreaseLevel += delta;
				if(ticksToDecreaseLevel >= Props.ticksToDecreaseLevel)
                {
					ticksToDecreaseLevel = 0;
				}
			}
		}

        public override void CompExposeData()
        {
            base.CompExposeData();
			Scribe_Values.Look(ref ticksToDecreaseLevel, "ticksToDecreaseLevel");
		}
    }
}