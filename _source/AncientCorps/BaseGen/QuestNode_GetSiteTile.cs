using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace AncientCorps
{
    public class QuestNode_GetSiteTileOnRoad : QuestNode
    {
        [NoTranslate]
        public SlateRef<string> storeAs;

        public SlateRef<bool> preferCloserTiles;

        public SlateRef<bool> allowCaravans;

        public SlateRef<bool?> clampRangeBySiteParts;

        public SlateRef<IEnumerable<SitePartDef>> sitePartDefs;

        protected override bool TestRunInt(Slate slate)
        {
            if (!TryFindTile(slate, out var tile))
            {
                return false;
            }
            if (clampRangeBySiteParts.GetValue(slate) == true && sitePartDefs.GetValue(slate) == null)
            {
                return false;
            }
            slate.Set(storeAs.GetValue(slate), tile);
            return true;
        }

        protected override void RunInt()
        {
            Slate slate = QuestGen.slate;
            if (!slate.TryGet<int>(storeAs.GetValue(slate), out var _) && TryFindTile(QuestGen.slate, out var tile))
            {
                QuestGen.slate.Set(storeAs.GetValue(slate), tile);
            }
        }

        private bool TryFindTile(Slate slate, out PlanetTile tile)
        {
            int nearThisTile = (slate.Get<Map>("map") ?? Find.RandomPlayerHomeMap)?.Tile ?? (-1);
            int num = int.MaxValue;
            bool? value = clampRangeBySiteParts.GetValue(slate);
            if (value.HasValue && value.Value)
            {
                foreach (SitePartDef item in sitePartDefs.GetValue(slate))
                {
                    if (item.conditionCauserDef != null)
                    {
                        num = Mathf.Min(num, item.conditionCauserDef.GetCompProperties<CompProperties_CausesGameCondition>().worldRange);
                    }
                }
            }
            if (!slate.TryGet<IntRange>("siteDistRange", out var var))
            {
                var = new IntRange(7, Mathf.Min(27, num));
            }
            else if (num != int.MaxValue)
            {
                var = new IntRange(Mathf.Min(var.min, num), Mathf.Min(var.max, num));
            }
            TileFinderMode tileFinderMode = (preferCloserTiles.GetValue(slate) ? TileFinderMode.Near : TileFinderMode.Random);

            var t = TileFinder.TryFindPassableTileWithTraversalDistance(nearThisTile, var.min, var.max, out tile, (PlanetTile x) => !Find.WorldObjects.AnyWorldObjectAt(x) && TileFinder.IsValidTileForNewSettlement(x) && (!Find.World.Impassable(x) || Find.WorldGrid[x].WaterCovered), ignoreFirstTilePassability: false, tileFinderMode, canTraverseImpassable: true);
            if (t) return true;
            else
                return TileFinder.TryFindNewSiteTile(out tile, var.min, var.max, allowCaravans.GetValue(slate), null, 0.5f, true, tileFinderMode);
        }
    }
}