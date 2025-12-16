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
using static NAT.IncidentWorker_RustedArmySiege;

namespace NAT
{
	public class Building_RustedTurret : Building_TurretGun
	{
		public int ticksSinceDeadlife = -1;
		public override LocalTargetInfo TryFindNewTarget()
		{
			IAttackTargetSearcher attackTargetSearcher = TargSearcher();
			Faction faction = attackTargetSearcher.Thing.Faction;
			float range = AttackVerb.verbProps.range;
			if (AttackVerb.ProjectileFliesOverhead() && faction.HostileTo(Faction.OfPlayer))
			{
				if(gun.TryGetComp<CompChangeableProjectile>(out var comp) && comp.Loaded && comp.LoadedShell.projectileWhenLoaded.projectile.damageDef.defName.Contains("Deadlife"))
				{
					List<Corpse> list = new List<Corpse>();
					foreach (Thing item in Map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse))
					{
						if (item is Corpse corpse && MutantUtility.CanResurrectAsShambler(corpse, true))
						{
							list.Add(corpse);
						}
					}
					if(list.Count > 0)
					{
						return GetGoodDeadlifeTarget(list, comp.LoadedShell.projectileWhenLoaded.projectile.explosionRadius, Map);
					}
					else
					{
						comp.allowedShellsSettings.filter.SetAllow(comp.LoadedShell, false);
						GenPlace.TryPlaceThing(comp.RemoveShell(), InteractionCell, Map, ThingPlaceMode.Near);
					}
				}
				if (Rand.Value < 0.5f && base.Map.listerBuildings.allBuildingsColonist.Where(delegate (Building x)
				{
					float num = AttackVerb.verbProps.EffectiveMinRange(x, this);
					float num2 = x.Position.DistanceToSquared(base.Position);
					return num2 > num * num && num2 < range * range;
				}).TryRandomElement(out var result))
				{
					return result;
				}
			}
			
			TargetScanFlags targetScanFlags = TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable;
			if (!AttackVerb.ProjectileFliesOverhead())
			{
				targetScanFlags |= TargetScanFlags.NeedLOSToAll;
			}
			if (AttackVerb.IsIncendiary_Ranged())
			{
				targetScanFlags |= TargetScanFlags.NeedNonBurning;
			}
			return (Thing)AttackTargetFinder.BestShootTargetFromCurrentPosition(attackTargetSearcher, targetScanFlags, IsValidTarget);
		}

		private Corpse GetGoodDeadlifeTarget(List<Corpse> list, float range, Map map)
		{
			int count = -1;
			Corpse result = null;
			foreach(Corpse corpse in list.InRandomOrder())
			{
				if (corpse.Position.Roofed(map))
				{
					continue;
				}
				int num = list.Count((x)=>x.Position.DistanceTo(corpse.Position) <= range);
				if(num > count)
				{
					result = corpse;
					count = num;
				}
			}
			return result;
		}

		private IAttackTargetSearcher TargSearcher()
		{
			if (mannableComp != null && mannableComp.MannedNow)
			{
				return mannableComp.ManningPawn;
			}
			return this;
		}

		private bool IsValidTarget(Thing t)
		{
			if (t is Pawn pawn)
			{
				if (base.Faction == Faction.OfPlayer && pawn.IsPrisoner)
				{
					return false;
				}
				if (AttackVerb.ProjectileFliesOverhead())
				{
					RoofDef roofDef = base.Map.roofGrid.RoofAt(t.Position);
					if (roofDef != null && roofDef.isThickRoof)
					{
						return false;
					}
				}
				if (mannableComp == null)
				{
					return !GenAI.MachinesLike(base.Faction, pawn);
				}
				if (pawn.RaceProps.Animal && pawn.Faction == Faction.OfPlayer)
				{
					return false;
				}
			}
			return true;
		}

        public override void PostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.PostApplyDamage(dinfo, totalDamageDealt);
			if (dinfo.Def.defName == "Nerve")
			{
				DamageInfo dinfo2 = dinfo;
				dinfo2.Def = DamageDefOf.NerveStun;
				dinfo2.SetAmount(4f);
				TakeDamage(dinfo2);
			}
		}

		protected override void Tick()
		{
			base.Tick();
			if (this.IsHashIntervalTick(2500) && Faction == Faction.OfEntities && gun.TryGetComp<CompChangeableProjectile>(out var comp) )
			{
				comp.allowedShellsSettings = new StorageSettings(comp);
				if (comp.parent.def.building.defaultStorageSettings != null)
				{
					comp.allowedShellsSettings.CopyFrom(comp.parent.def.building.defaultStorageSettings);
				}
				if (Rand.Chance(0.1f) && !comp.Loaded)
				{
					List<Thing> things = new List<Thing>();
					for (int i = 0; i < new IntRange(4, 9).RandomInRange; i++)
					{
						things.Add(ThingMaker.MakeThing(gun.def.building.fixedStorageSettings.filter.AllowedThingDefs.RandomElement()));
					}
					Skyfaller_RustedChunk skyfaller = (Skyfaller_RustedChunk)SkyfallerMaker.SpawnSkyfaller(NATDefOf.NAT_RustedChunk1x1Incoming, things, this.OccupiedRect().ExpandedBy(5).ClipInsideMap(Map).RandomCell, Map);
					skyfaller.frendlies = things;
					skyfaller.faction = Faction.OfEntities;
				}
			}
		}

        public override IEnumerable<Gizmo> GetGizmos()
        {
			foreach(Gizmo g in base.GetGizmos())
            {
				yield return g;
            }
			/*yield return new Command_Action
			{
				defaultLabel = "DEV: + 0.05",
				action = delegate
				{
					def.building.turretTopOffset.y += 0.05f;
				}
			};
			yield return new Command_Action
			{
				defaultLabel = "DEV: - 0.05",
				action = delegate
				{
					def.building.turretTopOffset.y -= 0.05f;
				}
			};*/
		}
    }
}