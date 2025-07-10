using RimWorld;
using Verse;
using System.Linq;
using RimWorld.Planet;
using UnityEngine;
using System;
using System.Collections.Generic;
using RimWorld.BaseGen;
using Verse.AI.Group;
using Verse.Noise;

namespace AncientCorps
{
    public class Company : Site
    {
        public bool HasTarget => targetSettlement != null;
        public Settlement Target => targetSettlement;
        protected TaggedString name;
        protected Settlement targetSettlement = null;
        protected CompanyDef companyDef = null;

        //隸屬於Faction的Name
        public override string Label => "DMSAC_Company.Label".Translate(this.Faction.Name, name);
        public override Texture2D ExpandingIcon
        {
            get
            {
                if (companyDef != null)
                {
                    return ContentFinder<Texture2D>.Get(companyDef.IconPath);
                }
                else
                {
                    return base.ExpandingIcon;
                }
            }
        }
        public override IEnumerable<GenStepWithParams> ExtraGenStepDefs => base.ExtraGenStepDefs;
        public override string GetDescription()
        {
            string str = base.GetDescription();
            if (companyDef != null)
            {
                //這是一支隸屬於Faction的label
                str += "\n\n" + "DMSAC_Company.Desc".Translate(this.Faction.Name, companyDef.label);
            }
            if (targetSettlement != null)
            {
                //他們的目標很可能是targetSettlement。
                str += "DMSAC_Company.Target".Translate(targetSettlement.Name);
            }
            else
            {
                str += "DMSAC_Company.NoTarget".Translate();
            }
            return str;
        }
        public void SetCompany(CompanyDef company)
        {
            companyDef = company;
        }
        public void SetTarget(Settlement settlement)
        {
            if (settlement == null)
            {
                Log.Warning("Null settlement");
                return;
            }
            targetSettlement = settlement;
            var tiles = WorldUtils.GetNearbyTiles(targetSettlement.Tile, GetCompany().combatRadius);
            if (!tiles.NullOrEmpty())
            {
                this.Tile = tiles.RandomElement();
            }
        }
        public override void PostMake()
        {
            base.PostMake();
            AddPart(new SitePart()
            {
                def = DMS_DefOf.DMSAC_GarrisonSite,
                parms = new SitePartParams()
                {
                    threatPoints = StorytellerUtility.DefaultThreatPointsNow(Find.AnyPlayerHomeMap)
                }
            });
        }
        public override void SpawnSetup()
        {
            base.SpawnSetup();
            if (Faction == null) SetFaction(Corps);
            if (companyDef == null) companyDef = DefDatabase<CompanyDef>.AllDefsListForReading.Where(x => this.Faction.def == x.defaultFaction).RandomElement();
            if (name.NullOrEmpty()) name = NameGenerator.GenerateName(companyDef.NamePack);
            SetFaction(Find.FactionManager.FirstFactionOfDef(companyDef.defaultFaction));
        }
        private Faction Corps => AncientCorpsUltility.Corps;
        public void DoAction()
        {
            if (targetSettlement == null || targetSettlement.Destroyed || !targetSettlement.Spawned || targetSettlement.Faction == Corps) targetSettlement = null;
            GameComponent_DefconLevel comp = Current.Game.GetComponent<GameComponent_DefconLevel>();
            if (targetSettlement != null)
            {
                if (Rand.Chance(0.5f)) return;
                if (Rand.Chance(0.25f))
                {
                    AncientCorpsUltility.TriggerTakeover(targetSettlement);

                    List<PlanetTile> neighboringTiles = new List<PlanetTile>();
                    Find.WorldGrid.GetTileNeighbors(targetSettlement.Tile, neighboringTiles);
                    var t = neighboringTiles.ClosestTileTo(this.Tile);
                    Tile = (t != -1) ? t : Tile;
                    SettlementDefeatUtility.CheckDefeated(targetSettlement);
                    targetSettlement = null;
                    return;
                }
                if (Rand.Chance(0.25f))
                {
                    comp.DepolyNewCompany(targetSettlement);
                    return;
                }
                if (Rand.Chance(0.1f))
                {
                    DestroyedSettlement destroyedSettlement = (DestroyedSettlement)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.DestroyedSettlement);
                    destroyedSettlement.Tile = targetSettlement.Tile;
                    Find.WorldObjects.Add(destroyedSettlement);

                    List<PlanetTile> neighboringTiles = new List<PlanetTile>();
                    Find.WorldGrid.GetTileNeighbors(targetSettlement.Tile, neighboringTiles);
                    Tile = neighboringTiles.ClosestTileTo(destroyedSettlement.Tile);
                    targetSettlement.Destroy();
                    SettlementDefeatUtility.CheckDefeated(targetSettlement);
                    targetSettlement = null;
                    return;
                }
            }
            else
            {
                if (Rand.Chance(0.5f)) Destroy();
                if (comp.HasTarget)
                {
                    var a = comp.GetTargetSettlement();
                    if (a != null)
                    {
                        SetTarget(a);
                    }
                    else
                    {
                        AncientCorpsUltility.WarIsOver(comp.CurrentTarget);
                    }
                }
                else
                {
                    Tile = WorldUtils.GetNearbyTiles(this.Tile, companyDef.combatRadius/2).RandomElement();
                }
            }
        }
        public override bool ShouldRemoveMapNow(out bool alsoRemoveWorldObject)
        {
            alsoRemoveWorldObject = false;
            if (!base.Map.IsPlayerHome)
            {
                var pawns = Map.mapPawns.PawnsInFaction(Faction);
                if (pawns.NullOrEmpty())
                {
                    alsoRemoveWorldObject = true;
                }
                else if (pawns.Where(p => p.kindDef == DMS_DefOf.DMS_Mech_CommandWalker).EnumerableNullOrEmpty())
                {
                    alsoRemoveWorldObject = true;
                }
                return !base.Map.mapPawns.AnyPawnBlockingMapRemoval;
            }

            return false;
        }
        public override void PostMapGenerate()
        {
            base.PostMapGenerate();
            AncientCorpsUltility.Defcon_Rise();
            GenerateCompanyPawn();
        }
        public void GenerateCompanyPawn()
        {
            if (!HasMap) { Log.Error("Generate without map"); return; }
            IntVec3 baseCenter = this.Map.Center;
            Faction faction = this.Faction;
            var thing = Map.listerThings.ThingsMatching(ThingRequest.ForGroup(ThingRequestGroup.PowerTrader));
            Lord singlePawnLord = LordMaker.MakeNewLord(faction, new LordJob_MechanoidsDefend(thing, faction, 20, baseCenter, false, false), this.Map);
            foreach (var item in companyDef.GeneratePawns())
            {
                GenPlace.TryPlaceThing(item, baseCenter + (20 * Rand.InsideUnitCircleVec3).ToIntVec3(), this.Map, ThingPlaceMode.Near);
                singlePawnLord.AddPawn(item);
            }
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref name, "CompanyName");
            Scribe_Defs.Look(ref companyDef, "CompanyDef");
            Scribe_References.Look(ref targetSettlement, "TargetSettlement");
        }
        internal CompanyDef GetCompany()
        {
            if (companyDef == null)
            { 
                companyDef = DefDatabase<CompanyDef>.AllDefsListForReading.Where(x => x.defaultFaction == this.Faction.def).RandomElement();
            }
            return companyDef;
        }
    }
}