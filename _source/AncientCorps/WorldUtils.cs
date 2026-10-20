using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using Verse;

namespace AncientCorps
{
    public static class WorldUtils
    {
        public static PlanetTile ClosestTileTo(this List<PlanetTile> tiles, PlanetTile targetTile, List<PlanetTile> ignoreTile = null)
        {
            float minDistance = float.MaxValue;
            PlanetTile closest = PlanetTile.Invalid;
            foreach (var tileA in tiles)
            {
                if (!ignoreTile.NullOrEmpty() && ignoreTile.Contains(tileA)) continue;
                float distance = Find.WorldGrid.ApproxDistanceInTiles(tileA, targetTile);

                if (distance < minDistance)
                {
                    minDistance = distance;
                    closest = tileA;
                }
            }
            return closest;
        }
        public static float GetRangeBetweenTiles(this int tileA, int tileB)
        {
            return Find.WorldGrid.ApproxDistanceInTiles(tileA, tileB);
        }
        public static float GetRangeBetweenTiles(this Map map, int tileB)
        {
            return Find.WorldGrid.ApproxDistanceInTiles(map.Tile, tileB);
        }
        public static List<PlanetTile> GetNearbyTiles(PlanetTile rootTileId, int maxDistance)
        {
            List<PlanetTile> result = new List<PlanetTile>();

            // 使用 WorldFloodFiller 來搜尋
            Find.WorldGrid.FirstLayerOfDef(PlanetLayerDefOf.Surface).Filler.FloodFill(
                rootTileId,
                (PlanetTile tile) => tile.Tile.elevation > 0,  // 你可以加條件限制這裡
                (PlanetTile tile, int traversalDistance) =>
                {
                    if (traversalDistance <= maxDistance)
                    {
                        result.Add(tile);
                        return false;  // 繼續搜尋
                    }
                    return true;  // 超出範圍就不繼續
                },
                int.MaxValue,
                null
            );

            return result;
        }
        public static (Settlement, Settlement, float) FindClosestSettlementPair(List<Settlement> listA, List<Settlement> listB)
        {
            Settlement closestA = null;
            Settlement closestB = null;
            float minDistance = float.MaxValue;

            foreach (var a in listA)
            {
                int tileA = a.Tile;

                foreach (var b in listB)
                {
                    int tileB = b.Tile;
                    float distance = Find.WorldGrid.ApproxDistanceInTiles(tileA, tileB);

                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        closestA = a;
                        closestB = b;
                    }
                }
            }
            return (closestA, closestB, minDistance);
        }
    }
}
