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
	public class Hediff_InducedPain : HediffWithComps
	{
		public override bool Visible
		{
			get
			{
				if (pawn.health.hediffSet.PainTotal <= 0f)
				{
					return false;
				}
				return base.Visible;
			}
		}
		public override float PainOffset
		{
			get
			{
				if (pawn.genes != null)
				{
					float num = pawn.genes.PainFactor;
					if (num > 0f && num < 1f)
					{
						return Severity / num;
					}
				}
				return Severity;
			}
		}
	}
}