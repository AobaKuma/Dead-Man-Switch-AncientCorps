using RimWorld;
using System.Linq;
using Verse;

namespace AncientCorps
{
    /// <summary>
    /// 稜堡在場時播放首領音樂。原本先看「地圖上有沒有原版機械族」——只要有任何機械族 pawn
    /// 就直接以機械族判定 return，古代機兵的分支永遠走不到（而定義上稜堡兩邊都可能出現）。
    /// Plays the boss track while a Krepost is on the map. The original checked the vanilla mechanoid
    /// faction first and returned early, so the AncientCorps branch was unreachable.
    /// </summary>
    public class KrepostAttackTransition : MusicTransition
    {
        public override bool IsTransitionSatisfied()
        {
            if (!base.IsTransitionSatisfied()) return false;
            Map map = Find.CurrentMap;
            if (map == null) return false;
            PawnKindDef krepost = DMSAC_DefOf.DMS_Mech_Krepost;
            if (krepost == null) return false;

            // 先判古代機兵（敵對時才算）
            FactionDef corpsDef = DMSAC_DefOf.DMS_AncientCorps;
            if (corpsDef != null)
            {
                Faction corps = Find.FactionManager.FirstFactionOfDef(corpsDef);
                if (corps != null && corps.HostileTo(Faction.OfPlayer))
                {
                    var corpsPawns = map.mapPawns.SpawnedPawnsInFaction(corps);
                    if (!corpsPawns.NullOrEmpty() && corpsPawns.Any(p => p.kindDef == krepost)) return true;
                }
            }
            // 再判原版機械族（首領任務的稜堡可能掛在機械族底下）
            var mechPawns = map.mapPawns.SpawnedPawnsInFaction(Faction.OfMechanoids);
            return !mechPawns.NullOrEmpty() && mechPawns.Any(p => p.kindDef == krepost);
        }
    }
}
