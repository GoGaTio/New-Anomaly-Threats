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
	public class CompProperties_CollectorNotes : CompProperties_Interactable
	{
		public CompProperties_CollectorNotes()
		{
			compClass = typeof(CompCollectorNotes);
		}

		public override void ResolveReferences(ThingDef parentDef)
		{
			base.ResolveReferences(parentDef);
			MonolithLevelDefOf.Waking.entityCountCompletionRequired += 1;
			MonolithLevelDefOf.VoidAwakened.entityCountCompletionRequired += 3;
			NATDefOf.NAT_RustRest.description = NeedDefOf.Rest.description;
			NATDefOf.NAT_RustRest.label = NeedDefOf.Rest.label;
		}
	}
	public class CompCollectorNotes : CompInteractable
	{
		private CompStudyUnlocks studyComp;

		private CompStudyUnlocks StudyComp => studyComp ?? (studyComp = parent.GetComp<CompStudyUnlocks>());

		public QuestPart_Collector questPart;

		public override string ExposeKey => "Interactor";

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_References.Look(ref questPart, "questPart");
		}

		public override AcceptanceReport CanInteract(Pawn activateBy = null, bool checkOptionalItems = true)
		{
			if (!StudyComp.Completed)
			{
				return false;
			}
			return base.CanInteract(activateBy, checkOptionalItems);
		}

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			if (!StudyComp.Completed)
			{
				yield break;
			}
			foreach (Gizmo item in base.CompGetGizmosExtra())
			{
				yield return item;
			}
		}

        public override string CompInspectStringExtra()
        {
            string s = base.CompInspectStringExtra();
            if (DebugSettings.ShowDevGizmos)
            {
                if (!s.NullOrEmpty())
                {
					s += "\n";
                }
				s += questPart == null ? "tracker is null" : questPart.GetUniqueLoadID();
            }
			return s;
        }

        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
		{
			if (!StudyComp.Completed)
			{
				yield break;
			}
			foreach (FloatMenuOption item in base.CompFloatMenuOptions(selPawn))
			{
				yield return item;
			}
		}

		protected override void OnInteracted(Pawn caster)
		{
			if (StudyComp.Completed)
			{
				questPart.GenerateSite(caster.MapHeld ?? parent.MapHeld ?? Current.Game.CurrentMap, caster);
                parent.Destroy();
			}
		}
	}
}