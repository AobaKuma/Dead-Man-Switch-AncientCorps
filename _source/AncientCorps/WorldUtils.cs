using RimWorld;
using RimWorld.Planet;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace AncientCorps
{
    public static class WorldUtils
    {
        public static PlanetTile ClosestTileTo(this List<PlanetTile> tiles, PlanetTile targetTile, List<PlanetTile> ignoreTile = null)
        {
            float minDistance = float.MaxValue;
            int closest = -1;
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
        public static Vector3 Position(float angle, Map map)
        {
            float theta = Mathf.Deg2Rad * angle; // 角度轉換為弧度

            // 計算方向向量
            float dx = Mathf.Sin(theta);
            float dy = -Mathf.Cos(theta);

            float x = map.Center.x;
            float y = map.Center.y;

            while (x >= 0 && x < map.Size.x && y >= 0 && y < map.Size.y)
            {
                x += dx;
                y += dy;
            }

            int edgeX = Mathf.RoundToInt(x - dx);
            int edgeY = Mathf.RoundToInt(y - dy);

            return new Vector3(edgeX, 0, edgeY);
        }
        public static float GetRangeBetweenTiles(this int tileA, int tileB)
        {
            return Find.WorldGrid.ApproxDistanceInTiles(tileA, tileB);
        }
        public static float GetRangeBetweenTiles(this Map map, int tileB)
        {
            return Find.WorldGrid.ApproxDistanceInTiles(map.Tile, tileB);
        }
        /// <summary>
        /// 取得從 tileA 指向 tileB 的角度（以北為 0 度，順時針）
        /// </summary>
        public static float GetAngleBetweenTiles(this Map map, int tileB)
        {
            int tileA = map.Tile;
            // 將經緯度轉為世界座標（球面上的點）
            Vector3 posA = Find.WorldGrid.GetTileCenter(tileA);
            Vector3 posB = Find.WorldGrid.GetTileCenter(tileB);

            // 將三維向量轉為平面 2D 座標
            Vector2 flatA = new Vector2(posA.x, posA.z);
            Vector2 flatB = new Vector2(posB.x, posB.z);

            // 計算方向向量
            Vector2 dir = (flatB - flatA).normalized;

            // 計算角度：以北為 0，順時針為正
            float angle = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;

            if (angle < 0)
                angle += 360f;

            return angle;
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