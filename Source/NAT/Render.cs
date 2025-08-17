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

	public class PawnRenderNode_RustApparel : PawnRenderNode
	{
		private bool useHeadMesh;

		public PawnRenderNode_RustApparel(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
			: base(pawn, props, tree)
		{
		}

		public PawnRenderNode_RustApparel(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree, Apparel apparel)
			: base(pawn, props, tree)
		{
			base.apparel = apparel;
			useHeadMesh = props.parentTagDef == PawnRenderNodeTagDefOf.ApparelHead;
			meshSet = MeshSetFor(pawn);
		}

		public PawnRenderNode_RustApparel(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree, Apparel apparel, bool useHeadMesh)
			: base(pawn, props, tree)
		{
			base.apparel = apparel;
			this.useHeadMesh = useHeadMesh;
			meshSet = MeshSetFor(pawn);
		}

		public override GraphicMeshSet MeshSetFor(Pawn pawn)
		{
			if(apparel != null && pawn is RustedPawn rust)
            {
                if (!apparel.HasComp<CompRustedEquipment>())
                {
					return MeshPool.GetMeshSetForSize(1.5f, 1.5f);
				}
				if (base.Props.overrideMeshSize.HasValue)
				{
					return MeshPool.GetMeshSetForSize(base.Props.overrideMeshSize.Value.x, base.Props.overrideMeshSize.Value.y);
				}
				if (useHeadMesh)
				{
					return MeshPool.GetMeshSetForSize(rust.Head.graphicData.drawSize);
				}
				return MeshPool.GetMeshSetForSize(rust.kindDef.lifeStages.First().bodyGraphicData.drawSize);
			}
			return base.MeshSetFor(pawn);
		}

		protected override IEnumerable<Graphic> GraphicsFor(Pawn pawn)
		{
			if (pawn is RustedPawn rust && ApparelGraphicRecordGetter.TryGetGraphicApparel(apparel, rust.Comp.Props.bodyType, false, out var rec))
			{
				yield return rec.graphic;
			}
		}
	}

	public class PawnRenderNodeWorker_RustApparel_Body : PawnRenderNodeWorker
	{
		public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
		{
			if (!base.CanDrawNow(node, parms))
			{
				return false;
			}
			if (!parms.flags.FlagSet(PawnRenderFlags.Clothes))
			{
				return false;
			}
			return true;
		}

		public override Vector3 OffsetFor(PawnRenderNode n, PawnDrawParms parms, out Vector3 pivot)
		{
			Vector3 result = base.OffsetFor(n, parms, out pivot);
			PawnRenderNode_RustApparel pawnRenderNode_Apparel = (PawnRenderNode_RustApparel)n;
			if (parms.pawn is RustedPawn rust && pawnRenderNode_Apparel.apparel.def.apparel.wornGraphicData != null && pawnRenderNode_Apparel.apparel.RenderAsPack())
			{
				Vector2 vector = pawnRenderNode_Apparel.apparel.def.apparel.wornGraphicData.BeltOffsetAt(parms.facing, rust.Comp.Props.bodyType);
				result.x += vector.x;
				result.z += vector.y;
			}
			return result;
		}

		public override Vector3 ScaleFor(PawnRenderNode n, PawnDrawParms parms)
		{
			Vector3 result = base.ScaleFor(n, parms);
			PawnRenderNode_RustApparel pawnRenderNode_Apparel = (PawnRenderNode_RustApparel)n;
			if (parms.pawn is RustedPawn rust && pawnRenderNode_Apparel.apparel.def.apparel.wornGraphicData != null && pawnRenderNode_Apparel.apparel.RenderAsPack())
			{
				Vector2 vector = pawnRenderNode_Apparel.apparel.def.apparel.wornGraphicData.BeltScaleAt(parms.facing, rust.Comp.Props.bodyType);
				result.x *= vector.x;
				result.z *= vector.y;
			}
			return result;
		}

		public override float LayerFor(PawnRenderNode n, PawnDrawParms parms)
		{
			if (parms.flipHead && n.Props.oppositeFacingLayerWhenFlipped)
			{
				PawnDrawParms parms2 = parms;
				parms2.facing = parms.facing.Opposite;
				parms2.flipHead = false;
				return base.LayerFor(n, parms2);
			}
			return base.LayerFor(n, parms);
		}
	}

	public class PawnRenderNodeWorker_RustApparel_Head : PawnRenderNodeWorker
	{
		public override bool CanDrawNow(PawnRenderNode n, PawnDrawParms parms)
		{
			if (!base.CanDrawNow(n, parms))
			{
				return false;
			}
			if (!parms.flags.FlagSet(PawnRenderFlags.Clothes) || !parms.flags.FlagSet(PawnRenderFlags.Headgear))
			{
				return false;
			}
			return true;
		}

		public override float LayerFor(PawnRenderNode node, PawnDrawParms parms)
		{
			if (parms.Portrait)
			{
				return base.LayerFor(node, parms);
			}
			if (parms.flipHead)
			{
				parms.facing = parms.facing.Opposite;
			}
			if (parms.facing == Rot4.North)
			{
				return -base.LayerFor(node, parms);
			}
			return base.LayerFor(node, parms);
		}
	}

	public class PawnRenderNode_RustBody : PawnRenderNode
	{
		public PawnRenderNode_RustBody(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
			: base(pawn, props, tree)
		{
		}

		public override Graphic GraphicFor(Pawn pawn)
		{
			PawnKindLifeStage curKindLifeStage = pawn.ageTracker.CurKindLifeStage;
			AlternateGraphic ag;
			int index;
			Graphic graphic = (pawn.TryGetAlternate(out ag, out index) ? ag.GetGraphic(curKindLifeStage.bodyGraphicData.Graphic) : curKindLifeStage.bodyGraphicData.Graphic);
			return graphic;
		}

		public override GraphicMeshSet MeshSetFor(Pawn pawn)
		{
			Graphic graphic = GraphicFor(pawn);
			if (graphic != null)
			{
				return MeshPool.GetMeshSetForSize(graphic.drawSize.x, graphic.drawSize.y);
			}
			return null;
		}
	}

	public class PawnRenderNode_RustHead : PawnRenderNode
	{
		public PawnRenderNode_RustHead(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
			: base(pawn, props, tree)
		{
		}

		public override GraphicMeshSet MeshSetFor(Pawn pawn)
		{
			Graphic graphic = GraphicFor(pawn);

			if (graphic != null && pawn is RustedPawn rust && rust.Head != null)
			{
				return MeshPool.GetMeshSetForSize(graphic.drawSize.x * rust.Comp.Props.headSize, graphic.drawSize.y * rust.Comp.Props.headSize);
			}
			return null;
		}

		public override Graphic GraphicFor(Pawn pawn)
		{
			if (pawn is RustedPawn rust)
			{
				if (!rust.HasHead)
				{
					return null;
				}
				return rust?.Head?.GetGraphic(rust);
			}
			return null;
		}
    }

	public class PawnRenderNodeWorker_RustHead : PawnRenderNodeWorker
	{
		protected override Material GetMaterial(PawnRenderNode node, PawnDrawParms parms)
		{
			if (parms.flipHead)
			{
				parms.facing = parms.facing.Opposite;
			}
			return base.GetMaterial(node, parms);
		}

		public override Vector3 OffsetFor(PawnRenderNode node, PawnDrawParms parms, out Vector3 pivot)
        {
			Vector3 vector = base.OffsetFor(node, parms, out pivot);
			
			if (!parms.pawn.TryGetComp<CompRustedSoldier>(out var comp))
			{
				return vector;
			}
			vector += comp.Props.drawData.OffsetForRot(parms.facing);
			return new Vector3(vector.x, 0f, vector.z);
		}

		public override float LayerFor(PawnRenderNode node, PawnDrawParms parms)
		{
			if(parms.Portrait)
            {
				return base.LayerFor(node, parms);
			}
			if (parms.flipHead)
			{
				parms.facing = parms.facing.Opposite;
			}
			/*if (parms.pawn.TryGetComp<CompRustedSoldier>(out var comp))
			{
				return comp.Props.drawData.LayerForRot(parms.pawn.Rotation, base.LayerFor(node, parms));
			}*/
			if (parms.facing == Rot4.North)
			{
				return -base.LayerFor(node, parms);
			}
			return base.LayerFor(node, parms);
		}

		public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
		{
			if (base.CanDrawNow(node, parms))
			{
				return (parms.pawn is RustedPawn rust) && rust.HasHead;
			}
			return false;
		}

		public override Quaternion RotationFor(PawnRenderNode node, PawnDrawParms parms)
		{
			Quaternion result = base.RotationFor(node, parms);
			if (!parms.Portrait && parms.pawn.Crawling)
			{
				result *= PawnRenderUtility.CrawlingHeadAngle(parms.facing).ToQuat();
				if (parms.flipHead)
				{
					result *= 180f.ToQuat();
				}
			}
			return result;
		}

    }
}