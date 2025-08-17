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
	public class RustedCore : ThingWithComps, IThingHolder
    {
        public int accumulationInterval = 10;

        public int accumulatedRustTicks;

        private Graphic graphicInt;

        protected ThingOwner<RustedPawn> innerContainer;

        public override void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.LoadingVars && Rust != null && Rust.Discarded)
            {
                Log.Warning("New Anomaly Threats - " + Rust.Name.ToStringFull + " was discarded during saving of core, fixing");
                ForceSetStateToUnspawned();
                DecrementMapIndex();
            };
            base.ExposeData();
            Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
            Scribe_Values.Look(ref accumulatedRustTicks, "accumulatedRustTicks", defaultValue: 0);
            Scribe_Values.Look(ref accumulationInterval, "accumulationInterval", defaultValue: 10);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && innerContainer.removeContentsIfDestroyed)
            {
                innerContainer.removeContentsIfDestroyed = false;
            }
        }

        public RustedPawn Rust
        {
            get
            {
                if (innerContainer.Count <= 0)
                {
                    return null;
                }
                return innerContainer[0];
            }
            set
            {
                innerContainer.Clear();
                if (value == null)
                {
                    return;
                }
                innerContainer.TryAdd(value);
            }
        }

        public RustedCore()
        {
            innerContainer = new ThingOwner<RustedPawn>(this, oneStackOnly: true, LookMode.Reference, removeContentsIfDestroyed: false);
        }

        public ThingOwner GetDirectlyHeldThings()
        {
            return innerContainer;
        }

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        public override Graphic Graphic
        {
            get
            {
                if (graphicInt == null)
                {
                    GraphicData graphicData = new GraphicData();
                    graphicData.CopyFrom(def.graphicData);
                    graphicData.texPath = graphicData.texPath.Remove(graphicData.texPath.Length - 1) + GetLetterFromLevel();
                    graphicInt = graphicData.GraphicColoredFor(this);
                }
                return graphicInt;
            }
        }

        public char GetLetterFromLevel()
        {
            if (accumulatedRustTicks > 9000)
            {
                return 'F';
            }
            if (accumulatedRustTicks > 8000)
            {
                return 'E';
            }
            if (accumulatedRustTicks > 6000)
            {
                return 'D';
            }
            if (accumulatedRustTicks > 4000)
            {
                return 'C';
            }
            if (accumulatedRustTicks > 2000)
            {
                return 'B';
            }
            return 'A';
        }

        public void Resurrect()
        {
            if (!base.Spawned || Rust == null)
            {
                return;
            }
            RustedPawn rust = Rust;
            if (rust.Discarded)
            {
                Log.Warning("New Anomaly Threats - " + rust.Name.ToStringFull + " was discarded during resurrection, fixing");
                ForceSetStateToUnspawned();
                DecrementMapIndex();
            };
            string label = Label;
            ResurrectionParams parms = new ResurrectionParams();
            parms.restoreMissingParts = true;
            parms.dontSpawn = true;
            ResurrectionUtility.TryResurrect(Rust, parms);
			GenSpawn.Spawn(rust, PositionHeld, MapHeld);
            try
            {
                if (rust.Faction != null && !rust.Faction.IsPlayer)
                {
                    Lord lord = rust.GetLord();
                    if (lord != null && lord.LordJob is LordJob_RustedArmy)
                    {
                        lord?.Notify_PawnUndowned(rust);
                    }
                    else if (rust.Faction == Faction.OfEntities && ((lord = MapHeld.lordManager.lords.FirstOrDefault((Lord x) => x.LordJob is LordJob_RustedArmy)) != null))
                    {
                        lord.AddPawn(rust);
                    }
                    else
                    {
                        LordJob lordJob = null;
                        if (rust.Faction == Faction.OfEntities)
                        {
                            lordJob = new LordJob_RustedArmy(IntVec3.Invalid, -1);
                        }
                        else
                        {
                            lordJob = new LordJob_AssaultColony(rust.Faction, false, false, false, false, false, false, false);
                        }
                        LordMaker.MakeNewLord(lordJob: lordJob, faction: rust.Faction, map: MapHeld, startingPawns: Gen.YieldSingle(rust));
                    }
                }
            }
            catch(Exception ex)
            {
                Log.Error("New Anomaly Threats - Error in RustedCore.Resurrect(Lord maker part): " + ex);
            }
            if(rust.Faction?.IsPlayer == true)
            {
                Find.LetterStack.ReceiveLetter("NAT_RustResurrected".Translate(rust.Name.ToStringFull).CapitalizeFirst(), "NAT_RustResurrected_Desc".Translate(label, rust.Name.ToStringFull).CapitalizeFirst(), LetterDefOf.NeutralEvent, rust);
            }
            Rust = null;
            this.Destroy();
		}

        protected override void Tick()
        {
            base.Tick();
            if (this.IsHashIntervalTick(accumulationInterval))
            {
                accumulatedRustTicks++;
                if(accumulatedRustTicks > 10000)
                {
                    Resurrect();
                }
                else if (accumulatedRustTicks == 2001 || accumulatedRustTicks == 4001 || accumulatedRustTicks == 6001 || accumulatedRustTicks == 8001 || accumulatedRustTicks == 9001)
                {
                    graphicInt = null;
                    if (Map != null)
                    {
                        DirtyMapMesh(Map);
                    }
                }
            }
        }

        public override string GetInspectString()
        {
            string s = base.GetInspectString();
            if (DebugSettings.ShowDevGizmos || Rust.Faction == Faction.OfPlayerSilentFail)
            {
                if (s.NullOrEmpty())
                {
                    s = "";
                }
                else
                {
                    s += "\n";
                }
                s += "NAT_ResurrectIn".Translate(((10000 - accumulatedRustTicks) * accumulationInterval).ToStringTicksToPeriod(allowSeconds: true));
            }
            if (DebugSettings.ShowDevGizmos)
            {
                s += "\n" + "Ticks passed: " + accumulatedRustTicks + "\n" + "Interval: " + accumulationInterval;
            }
            return s;
        }

        public override string LabelNoCount => Rust == null ? def.label : (string)"NAT_RustedCoreLabel".Translate(Rust.Name.ToStringFull);

        public override string LabelNoParenthesis => LabelNoCount;

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo g in base.GetGizmos())
            {
                yield return g;
            }
            if (DebugSettings.ShowDevGizmos)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Resurrect",
                    action = delegate
                    {
                        Resurrect();
                    }
                };
                yield return new Command_Action
                {
                    defaultLabel = "DEV: +100 progress",
                    action = delegate
                    {
                        accumulatedRustTicks += 100;
                    }
                };
                yield return new Command_Action
                {
                    defaultLabel = "DEV: +1000 progress",
                    action = delegate
                    {
                        accumulatedRustTicks += 1000;
                    }
                };
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Interval --",
                    action = delegate
                    {
                        accumulationInterval--;
                    }
                };
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Interval ++",
                    action = delegate
                    {
                        accumulationInterval++;
                    }
                };
            }
        }

        public override void Discard(bool silentlyRemoveReferences = false)
        {
            RustedPawn rust = Rust;
            if (rust != null && !rust.Discarded)
            {
                innerContainer.Remove(rust);
                rust.Discard();
            }
            base.Discard(silentlyRemoveReferences);
        }
    }

    public class CompProperties_DestroyRustedCore : CompProperties_Interactable
    {
        public CompProperties_DestroyRustedCore()
        {
            compClass = typeof(CompDestroyRustedCore);
            activeTicks = 1;
            ticksToActivate = 180;
            activateTexPath = "UI/Commands/DestroyUnnaturalCorpse";
            targetingParameters = new TargetingParameters
            {
                canTargetBuildings = false,
                canTargetAnimals = false,
                canTargetMechs = false,
                onlyTargetControlledPawns = true
            };
        }
    }
    public class CompDestroyRustedCore : CompInteractable
    {
        public RustedCore Core => (RustedCore)parent;

        public new CompProperties_DestroyRustedCore Props => (CompProperties_DestroyRustedCore)props;

        public override void OrderForceTarget(LocalTargetInfo target)
        {
            if (Core.Rust.Faction == Faction.OfPlayer)
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("NAT_DestroyCoreConfirmation".Translate(Core.Rust.Name.ToStringFull), delegate
                {
                    OrderActivation(target.Pawn);
                }));
            }
            else
            {
                OrderActivation(target.Pawn);
            }
        }

        protected override void OnInteracted(Pawn caster)
        {
            Core.Destroy();
        }

        public override string CompInspectStringExtra()
        {
            return null;
        }

        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
        {
            AcceptanceReport acceptanceReport = CanInteract(selPawn);
            FloatMenuOption floatMenuOption = new FloatMenuOption(Props.jobString.CapitalizeFirst(), delegate
            {
                if (Core.Rust.Faction == Faction.OfPlayer)
                {
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("NAT_DestroyCoreConfirmation".Translate(Core.Rust.Name.ToStringFull, Core.Rust.kindDef.label), delegate
                    {
                        OrderActivation(selPawn);
                    }));
                }
                else
                {
                    OrderActivation(selPawn);
                }
            });
            if (!acceptanceReport.Accepted)
            {
                floatMenuOption.Disabled = true;
                floatMenuOption.Label = floatMenuOption.Label + " (" + acceptanceReport.Reason + ")";
            }
            yield return floatMenuOption;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo item in base.CompGetGizmosExtra())
            {
                if(item is Command_Action command_Action)
                {
                    command_Action.defaultLabel = "NAT_RustedCoreDeactivate".Translate(parent.Label);
                    command_Action.defaultDesc = "NAT_RustedCoreDeactivateDesc".Translate(parent.Label);
                }
                yield return item;
            }
        }

        private void OrderActivation(Pawn pawn)
        {
            Job job = JobMaker.MakeJob(JobDefOf.InteractThing, parent);
            job.count = 1;
            job.playerForced = true;
            pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }
    }
}