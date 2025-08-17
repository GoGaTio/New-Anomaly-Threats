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
	public class CompProperties_RustedDish : CompProperties
	{
		public GraphicData graphicData;

		public CompProperties_RustedDish()
		{
			compClass = typeof(CompRustedDish);
		}
	}

	public class CompRustedDish : ThingComp
	{
		public float rot;
		public CompProperties_RustedDish Props => (CompProperties_RustedDish)props;

		public override void PostDraw()
		{
			Log.Message("dish");
			Mesh obj = Props.graphicData.Graphic.MeshAt(parent.Rotation);
			Vector3 drawPos = parent.DrawPos;
			drawPos.y = AltitudeLayer.MoteOverhead.AltitudeFor() + parent.def.graphicData.drawOffset.y;
			Graphics.DrawMesh(obj, drawPos + Props.graphicData.drawOffset.RotatedBy(parent.Rotation), Quaternion.AngleAxis(rot, Vector3.up), Props.graphicData.Graphic.MatAt(parent.Rotation), 0);
		}

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
			foreach (Gizmo g in base.CompGetGizmosExtra())
			{
				yield return g;
			}
			if (DebugSettings.ShowDevGizmos)
			{
				yield return new Command_Action
				{
					defaultLabel = "DEV: Change rot +1 (" + rot.ToString() + ")",
					action = delegate
                    {
						rot += 1f;
                    }
				};
				yield return new Command_Action
				{
					defaultLabel = "DEV: Change rot -1 (" + rot.ToString() + ")",
					action = delegate
					{
						rot -= 1f;
					}
				};
			}
		}

        public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref rot, "rot", defaultValue: 0f);
		}
	}
}
