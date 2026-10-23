using System.Collections.Generic;
using Fortified.Structures;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace AncientCorps
{
    /// <summary>
    /// 軍團站點的結構生成：結構、殘骸、戰利品、外圍廢墟全照 FFF 的 GenStep_FFFGarrisonedStructure，
    /// 只把守軍換成站點 SitePartDef 上 ModExtension_SiteGarrison 的定義（威脅點數 × 站點類型 × 警戒等級）。
    /// FFF 自己的 spawnDefenders 預設關掉，否則會多一支用派系通用 Combat 編組的守軍。
    ///
    /// Corps site generation: structure, debris, loot and satellite ruins exactly as FFF's
    /// GenStep_FFFGarrisonedStructure; only the garrison comes from the SitePartDef's ModExtension_SiteGarrison
    /// (threat points × site type × DEFCON). FFF's own spawnDefenders defaults off here, or a second garrison would
    /// spawn from the faction's generic Combat groups.
    /// </summary>
    public class GenStep_DMSAC_GarrisonedStructure : GenStep_FFFGarrisonedStructure
    {
        public override int SeedPart => 615724193;

        public GenStep_DMSAC_GarrisonedStructure()
        {
            spawnDefenders = false;
        }

        public override void Generate(Map map, GenStepParams parms)
        {
            base.Generate(map, parms);
            if (map == null || parms.sitePart == null) return;
            ModExtension_SiteGarrison ext = parms.sitePart.def.GetModExtension<ModExtension_SiteGarrison>();
            if (ext == null) return;
            // 守軍一律是軍團的：站點派系是玩家（例如玩家已接手的站點）或缺失時改用軍團。玩家派系沒有
            // maxPawnCostPerTotalPointsCurve，交給原版挑兵種會 NRE。
            // The garrison always belongs to the corps: when the site's faction is the player (a site the player took
            // over) or missing, use the corps. The player faction has no maxPawnCostPerTotalPointsCurve, which made
            // vanilla's pick throw.
            Faction faction = ModExtension_SiteGarrison.GarrisonFaction(parms.sitePart.site?.Faction ?? map.ParentFaction);
            if (faction == null) return;

            // 世界生成時放下的站點沒有威脅點數（threatPoints = 0），改用生成地圖當下的站點點數。
            // World-gen sites carry no threat points; use the site points at map generation time instead.
            float threat = parms.sitePart.parms.threatPoints;
            if (threat <= 0f) threat = StorytellerUtility.DefaultSiteThreatPointsNow();

            int defcon = ModExtension_SiteGarrison.CurrentDefcon;
            List<PawnKindDef> kinds = ext.ChooseKinds(ext.GarrisonPoints(threat, defcon), faction, map.Tile,
                ModExtension_SiteGarrison.SeedFor(parms.sitePart.site, parms.sitePart.parms), defcon);
            if (kinds.Count == 0) return;

            IntVec3 center = hasLastStructure ? lastStructureRect.CenterCell : map.Center;
            if (!center.InBounds(map)) center = map.Center;
            int radius = Mathf.RoundToInt(defendRadius);

            List<Pawn> pawns = new List<Pawn>();
            foreach (PawnKindDef kind in kinds)
            {
                Pawn pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, faction, PawnGenerationContext.NonPlayer,
                    tile: map.Tile, inhabitant: true));
                if (!CellFinder.TryFindRandomCellNear(center, map, radius, c => c.Standable(map), out IntVec3 cell))
                {
                    cell = CellFinder.RandomClosewalkCellNear(center, map, radius);
                }
                GenSpawn.Spawn(pawn, cell, map);
                pawns.Add(pawn);
            }
            LordMaker.MakeNewLord(faction, new LordJob_DefendPoint(center, defendRadius, null), map, pawns);
        }
    }
}
