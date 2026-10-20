using RimWorld;
using Verse;
using System.Linq;
using RimWorld.Planet;
using UnityEngine;
using System;
using System.Collections.Generic;
using Verse.AI.Group;

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

        /// <summary>
        /// 把連隊放到目標附近。鄰格必須可通行、且沒有其他世界物件（flood fill 會包含目標格本身，
        /// 原本不檢查會讓連隊疊在目標聚落或海上）。
        /// Place the company next to its target: passable and not already occupied (the flood fill includes
        /// the root tile, so the old code could stack the company on the target settlement itself or on water).
        /// </summary>
        public void SetTarget(Settlement settlement)
        {
            if (settlement == null)
            {
                Log.Warning("Null settlement");
                return;
            }
            targetSettlement = settlement;
            CompanyDef def = GetCompany();
            if (def == null) return;
            var tiles = WorldUtils.GetNearbyTiles(settlement.Tile, def.combatRadius);
            if (tiles.NullOrEmpty()) return;
            var valid = tiles.Where(t => !Find.World.Impassable(t) && !Find.WorldObjects.AnyWorldObjectAt(t)).ToList();
            if (valid.Count == 0) return;
            Tile = valid.RandomElement();
        }

        public override void PostMake()
        {
            base.PostMake();
            AddPart(new SitePart()
            {
                def = DMSAC_DefOf.DMSAC_GarrisonSite,
                parms = new SitePartParams()
                {
                    threatPoints = Find.AnyPlayerHomeMap != null ? StorytellerUtility.DefaultThreatPointsNow(Find.AnyPlayerHomeMap) : 500f
                }
            });
        }

        public override void SpawnSetup()
        {
            base.SpawnSetup();
            if (Faction == null) SetFaction(Corps);
            if (companyDef == null)
            {
                companyDef = DefDatabase<CompanyDef>.AllDefsListForReading
                    .Where(x => x.defaultFaction == this.Faction?.def)
                    .RandomElementWithFallback();
                if (companyDef == null)
                {
                    Log.Error("DMSAC: no CompanyDef available for faction " + (Faction?.def?.defName ?? "null") + ", removing world object.");
                    Destroy();
                    return;
                }
            }
            if (name.NullOrEmpty()) name = NameGenerator.GenerateName(companyDef.NamePack);
            Faction owner = Find.FactionManager.FirstFactionOfDef(companyDef.defaultFaction);
            if (owner != null) SetFaction(owner);
        }
        private Faction Corps => AncientCorpsUltility.Corps;

        public void DoAction()
        {
            if (targetSettlement == null || targetSettlement.Destroyed || !targetSettlement.Spawned || targetSettlement.Faction == Corps)
            {
                targetSettlement = null;
            }
            GameComponent_DefconLevel comp = Current.Game.GetComponent<GameComponent_DefconLevel>();
            if (targetSettlement != null)
            {
                if (Rand.Chance(0.5f)) return;
                if (Rand.Chance(0.25f))
                {
                    // 先記下座標：TriggerTakeover 會 Destroy 這個聚落。
                    PlanetTile targetTile = targetSettlement.Tile;
                    AncientCorpsUltility.TriggerTakeover(targetSettlement);
                    List<PlanetTile> neighboringTiles = new List<PlanetTile>();
                    Find.WorldGrid.GetTileNeighbors(targetTile, neighboringTiles);
                    var t = neighboringTiles.ClosestTileTo(this.Tile);
                    if (t != -1) Tile = t;
                    targetSettlement = null;
                    return;
                }
                if (Rand.Chance(0.25f))
                {
                    Settlement victim = targetSettlement;
                    targetSettlement = null;
                    if (comp != null) comp.DepolyNewCompany(victim);
                    return;
                }
                if (Rand.Chance(0.1f))
                {
                    PlanetTile targetTile = targetSettlement.Tile;
                    DestroyedSettlement destroyedSettlement = (DestroyedSettlement)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.DestroyedSettlement);
                    destroyedSettlement.Tile = targetTile;
                    Find.WorldObjects.Add(destroyedSettlement);

                    List<PlanetTile> neighboringTiles = new List<PlanetTile>();
                    Find.WorldGrid.GetTileNeighbors(targetTile, neighboringTiles);
                    var t = neighboringTiles.ClosestTileTo(destroyedSettlement.Tile);
                    if (t != -1) Tile = t;
                    targetSettlement.Destroy();
                    SettlementDefeatUtility.CheckDefeated(targetSettlement);
                    targetSettlement = null;
                    return;
                }
            }
            else
            {
                if (Rand.Chance(0.5f))
                {
                    Destroy();
                    return;
                }
                if (comp != null && comp.HasTarget)
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
                    var tiles = WorldUtils.GetNearbyTiles(this.Tile, GetCompany().combatRadius / 2);
                    PlanetTile dest = tiles.RandomElementWithFallback(this.Tile);
                    if (dest != this.Tile) Tile = dest;
                }
            }
        }

        /// <summary>
        /// 保留原版 Site.ShouldRemoveMapNow 的所有守衛（pocket map、建築、condition causer、raid source、
        /// transporter），只額外加上「指揮步行者不在場就一起收掉站點」的條件。
        /// Keep every vanilla guard; only add the "no relay walker left → drop the world object too" rule.
        /// </summary>
        public override bool ShouldRemoveMapNow(out bool alsoRemoveWorldObject)
        {
            bool result = base.ShouldRemoveMapNow(out alsoRemoveWorldObject);
            if (!result) return false;
            if (Map != null && !Map.IsPlayerHome && Faction != null)
            {
                var pawns = Map.mapPawns.PawnsInFaction(Faction);
                if (pawns.NullOrEmpty() || !pawns.Any(p => p.kindDef == DMSAC_DefOf.DMS_Mech_CommandWalker))
                {
                    alsoRemoveWorldObject = true;
                }
            }
            return result;
        }

        public override void PostMapGenerate()
        {
            base.PostMapGenerate();
            if (AncientCorpsUltility.Corps != null) AncientCorpsUltility.Defcon_Rise();
            GenerateCompanyPawn();
        }

        public void GenerateCompanyPawn()
        {
            if (!HasMap) { Log.Error("Generate without map"); return; }
            if (companyDef == null) companyDef = GetCompany();
            if (companyDef == null) return;
            IntVec3 baseCenter = this.Map.Center;
            Faction faction = this.Faction;
            List<Thing> defendThings = Map.listerThings.ThingsMatching(ThingRequest.ForGroup(ThingRequestGroup.PowerTrader)).ToList();
            // canAssaultColony: true — 否則機兵只會死守中心點，玩家從地圖邊緣進來時不會交戰。
            // canAssaultColony: true, otherwise the mechs only hold the centre and never engage an edge arrival.
            Lord defendLord = LordMaker.MakeNewLord(faction, new LordJob_MechanoidsDefend(defendThings, faction, 20f, baseCenter, canAssaultColony: true, isMechCluster: false), this.Map);
            foreach (var item in companyDef.GeneratePawns())
            {
                GenPlace.TryPlaceThing(item, baseCenter + (20 * Rand.InsideUnitCircleVec3).ToIntVec3(), this.Map, ThingPlaceMode.Near);
                defendLord.AddPawn(item);
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
                companyDef = DefDatabase<CompanyDef>.AllDefsListForReading
                    .Where(x => x.defaultFaction == this.Faction?.def)
                    .RandomElementWithFallback();
            }
            return companyDef;
        }
    }
}
