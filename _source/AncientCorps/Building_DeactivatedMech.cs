using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AncientCorps
{

    //在遺跡裡面生成的機體，玩家能夠駭入來獲取，並有機率失敗。
    public class Building_DeactivatedMech : Building, IThingHolder
    {
        public Building_DeactivatedMech()
		{
			this.innerContainer = new ThingOwner<Pawn>(this, false, LookMode.Deep);
        }
        private Faction PawnFaction => Find.FactionManager.FirstFactionOfDef(DMS_DefOf.DMS_AncientCorps) ?? Faction.OfAncients;
        private ModExtension_DeactivatedMech Extension => this.def.GetModExtension<ModExtension_DeactivatedMech>();
        public bool HasPawn
        {
            get
            {
                if (!innerContainer.NullOrEmpty())
                {
                    if(innerContainer.First() is Pawn || innerContainer.First() is Corpse)
                    return true;
                }
                return  false;
            }
        }

        public Pawn Pawn
        {
            get
            {
                if (this != null && !this.innerContainer.NullOrEmpty())
                {
                    if (innerContainer.First() is Pawn) return innerContainer?.First() as Pawn;
                    if (innerContainer.First() is Corpse) return (innerContainer?.First() as Corpse).InnerPawn;
                }
                return null;
            }
        }

        private Vector3 weaponDrawOffset = Vector3.zero;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            if (!respawningAfterLoad && (Rand.Chance(Extension.spawnChance) || DebugSettings.godMode))
            {
                float weightSelector(PawnGenOption PawnGenOption) => PawnGenOption.selectionWeight;
                innerContainer.TryAdd(PawnGenerator.GeneratePawn(this.Extension.possibleGeneratePawn.RandomElementByWeight(weightSelector).kind, PawnFaction));

                //這裡加一下給機體隨機受傷害。
                BodyPartRecord bodyPart = Pawn.RaceProps.body.AllParts.Where(p => !p.IsCorePart).RandomElement();
                Pawn.health.hediffSet.AddDirect(HediffMaker.MakeHediff(HediffDefOf.MissingBodyPart, Pawn, bodyPart));
                if (Rand.Chance(0.5f))
                {
                    bodyPart = Pawn.RaceProps.body.AllParts.Where(p => !p.IsCorePart).RandomElement();
                    if (!Pawn.health.hediffSet.HasMissingPartFor(bodyPart))
                    {
                        Pawn.health.hediffSet.AddDirect(HediffMaker.MakeHediff(HediffDefOf.MissingBodyPart, Pawn, bodyPart));
                    }
                }
                if (!Rand.Chance(Extension.weaponChance))
                {
                    Pawn.equipment.DestroyAllEquipment();
                    Pawn.inventory.DestroyAll();
                    weaponDrawOffset = new Vector3(Rand.Gaussian(), 0, Rand.Gaussian()) * Extension.weaponscatterRange;
                }
            }
            if (!HasPawn) Destroy(DestroyMode.Vanish);
        }
        public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
		{
			if (this.HasPawn)
			{
                Vector3 drawLoc2 = base.Position.ToVector3ShiftedWithAltitude(AltitudeLayer.BuildingOnTop) + (Extension != null ? Extension.innerPawnDrawOffset : Vector3.zero);
                this.Pawn.Drawer.renderer.DynamicDrawPhaseAt(phase, drawLoc2 - (weaponDrawOffset / 2), this.Rotation, false);
                this.Pawn.equipment.Primary?.DynamicDrawPhaseAt(phase, drawLoc2 + weaponDrawOffset);
            }
			base.DynamicDrawPhaseAt(phase, drawLoc, flip);
		}
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (Extension.bottomGraphic != null)
            {
                if (this.bottomGraphic == null)
                {
                    this.bottomGraphic = this.Extension.bottomGraphic.GraphicColoredFor(this);
                }
            }
            this.bottomGraphic?.Draw(base.Position.ToVector3ShiftedWithAltitude(AltitudeLayer.Item), this.Rotation, this, 0f);
            base.DrawAt(drawLoc, flip);
        }
        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            if (mode != DestroyMode.Vanish && this.HasPawn)
            {
                EjectContents(killPawn: true);
            }
            base.DeSpawn(mode);
        }
        public void GetChildHolders(List<IThingHolder> outChildren)
		{
			ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, this.GetDirectlyHeldThings());
		}
        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
        {
            foreach (FloatMenuOption option in base.GetFloatMenuOptions(selPawn))
            {
                yield return option;
            }
            if (this.HasPawn)
            {
                if (selPawn.CanReach(this, PathEndMode.InteractionCell, Danger.Deadly, false, false, TraverseMode.ByPawn))
                {
                    yield return new FloatMenuOption("AncientCorps.DeactivatedMech_Disable".Translate(), delegate ()
                    {
                        selPawn.jobs.TryTakeOrderedJob(new Job(DMS_DefOf.DMS_EjectDeactivatedMech, this));//直接幹掉。
                    });

                    if (selPawn.WorkTypeIsDisabled(WorkTypeDefOf.Research))
                    {
                        yield return DisableOption("AncientCorps.Reason_WorkTypeDisabled".Translate().CapitalizeFirst());
                    }
                    else if (!MechanitorUtility.IsMechanitor(selPawn))
                    {
                        yield return DisableOption("AncientCorps.Reason_NotMechanitor".Translate().CapitalizeFirst());
                    }
                    else if (selPawn.mechanitor.TotalBandwidth - selPawn.mechanitor.UsedBandwidth < Pawn.GetStatValue(StatDefOf.BandwidthCost))
                    {
                        yield return DisableOption("AncientCorps.Reason_NoEnoughBandwidth".Translate(Pawn.GetStatValue(StatDefOf.BandwidthCost)).CapitalizeFirst());
                    }
                    else
                    {
                        yield return new FloatMenuOption("AncientCorps.DeactivatedMech_TryHack".Translate(Pawn.LabelCap), delegate ()
                        {
                            selPawn.jobs.TryTakeOrderedJob(new Job(DMS_DefOf.DMS_HackDeactivatedMech, this));
                        });
                    }
                }
                else
                {
                    yield return DisableOption("NoPath".Translate().CapitalizeFirst());
                }
            }
        }
        private FloatMenuOption DisableOption(string reason)
        {
            return new FloatMenuOption("AncientCorps.DeactivatedMech_CannotHack".Translate() + ": " + reason, null);
        }
        public ThingOwner GetDirectlyHeldThings()
		{
			return this.innerContainer;
		}
		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Deep.Look<ThingOwner>(ref this.innerContainer, "innerContainer", new object[]
			{
                this
			});
            Scribe_Values.Look(ref this.weaponDrawOffset, "weaponDrawOffset", Vector3.zero, false);
		}
		public void EjectContents(Pawn usedBy = null,bool killPawn = false)
        {
            if (killPawn)//非駭入的情況下，機體會被摧毀。
            {
                if (!Pawn.Dead)
                {
                    Pawn.SetFactionDirect(Faction.OfAncients);
                    Pawn?.Kill(null);
                }
                if(!innerContainer.NullOrEmpty())
                innerContainer.TryDropAll(base.Position, base.Map, ThingPlaceMode.Near);
            }
            else
            {
                Pawn.SetFaction(Faction.OfPlayer);
                if (usedBy != null && MechanitorUtility.IsMechanitor(usedBy))
                {
                    if(usedBy.mechanitor.TotalBandwidth - usedBy.mechanitor.UsedBandwidth < Pawn.GetStatValue(StatDefOf.BandwidthCost))
                    if (MechanitorUtility.EverControllable(Pawn))
                    {
                        Pawn.GetOverseer()?.relations.RemoveDirectRelation(PawnRelationDefOf.Overseer, Pawn);
                    }
                    usedBy.relations.AddDirectRelation(PawnRelationDefOf.Overseer, Pawn);
                }
                innerContainer.TryDropAll(base.Position, base.Map, ThingPlaceMode.Near);
            }
            DeSpawnOrDeselect();
            Destroy(DestroyMode.Vanish);
        }
		private Graphic bottomGraphic;
		public ThingOwner innerContainer;
    }
}
