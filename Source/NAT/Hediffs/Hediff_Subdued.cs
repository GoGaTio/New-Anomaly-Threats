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
	public class Hediff_Subdued : HediffWithComps
	{
        public Faction faction;

        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            faction = pawn.Faction;
            pawn.SetFaction(Faction.OfEntities);
        }

        public override void Tick()
        {
            base.Tick();
            pawn.health.forceDowned = true; //We dont want to lose pawns by 33/66 rule
        }

        public override void PostRemoved()
        {
            base.PostRemoved();
            pawn.SetFaction(faction);
            faction = null;
        }

        public override void ExposeData()
        {
            Scribe_References.Look(ref faction, "faction");
        }

        public override void CopyFrom(Hediff other)
        {
            base.CopyFrom(other);
            if(other is Hediff_Subdued subdued)
            {
                faction = subdued.faction;
            }
        }
    }
}