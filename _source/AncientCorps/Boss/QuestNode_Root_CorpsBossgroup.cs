using System.Reflection;
using RimWorld;
using RimWorld.QuestGen;
using Verse;

namespace AncientCorps
{
    /// <summary>
    /// 軍團首領群的任務根節點：首領與護衛屬於軍團。軍團與玩家不敵對時（例如最後的駕駛員讓它停火）改用 fallbackFaction
    /// （預設 DMS_Legacy，即 DMS2 原本「遊蕩的殘存機兵」），否則召來的部隊會是不攻擊的友軍。
    /// DMS2 的 QuestNode_Root_BossgroupFactionExposed 把派系寫死在私有欄位 factionDef，這裡在每次生成前改掉它再交給原本的流程。
    ///
    /// Quest root for corps bossgroups: the boss and escorts belong to the corps. When the corps isn't hostile to the
    /// player (e.g. the Last Pilot made it stand down) it uses fallbackFaction instead (DMS_Legacy by default, DMS2's
    /// original "wandering remnants"), or the summoned force would be friendlies that never attack.
    /// DMS2's QuestNode_Root_BossgroupFactionExposed keeps its faction in the private field factionDef; this sets it before
    /// each run and hands over to the original flow.
    /// </summary>
    public class QuestNode_Root_CorpsBossgroup : DMS.QuestNode_Root_BossgroupFactionExposed
    {
        public FactionDef fallbackFaction;

        private static readonly FieldInfo FactionField =
            typeof(DMS.QuestNode_Root_BossgroupFactionExposed).GetField("factionDef", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>這次要用的派系定義。The faction def this run uses.</summary>
        public static FactionDef ChooseFaction(FactionDef fallback)
        {
            Faction corps = AncientCorpsUltility.Corps;
            if (corps != null && corps.HostileTo(Faction.OfPlayer)) return DMSAC_DefOf.DMS_AncientCorps;
            return fallback ?? DMSAC_DefOf.DMS_AncientCorps;
        }

        protected override void RunInt()
        {
            if (FactionField == null)
            {
                Log.ErrorOnce("[DMSAC] QuestNode_Root_BossgroupFactionExposed.factionDef not found; the corps bossgroup keeps DMS2's faction.", 0x5AC0B0);
            }
            else
            {
                FactionField.SetValue(this, ChooseFaction(fallbackFaction));
            }
            base.RunInt();
        }
    }
}
