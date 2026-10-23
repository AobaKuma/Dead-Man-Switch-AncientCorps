using Fortified;
using RimWorld;
using System.Linq;
using Verse;

namespace AncientCorps
{
    /// <summary>
    /// 軍團特有的工具。DEFCON、連隊、接管與宣戰都已移到 FFF 的戒備系統（Fortified.ReadinessUtility / CompanyUtility）。
    /// Corps-only helpers. DEFCON, companies, takeovers and wars moved to FFF's readiness system
    /// (Fortified.ReadinessUtility / CompanyUtility).
    /// </summary>
    public static class AncientCorpsUltility
    {
        public static Faction Corps => Find.FactionManager.FirstFactionOfDef(DMSAC_DefOf.DMS_AncientCorps);

        public const string LastPilotKind = "DMS_LastPilot";

        /// <summary>
        /// 殖民地（任何地圖、商隊、運輸中）是否有活著的最後的駕駛員（老兵）。有他在，DEFCON II 不會造成永久敵對。
        /// Whether the colony (any map, caravan or transporter) has a living Last Pilot. With him around, DEFCON II never
        /// makes the corps permanently hostile.
        /// </summary>
        public static bool PlayerHasLastPilot()
        {
            return PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists.Any(p => p.kindDef?.defName == LastPilotKind);
        }

        public static void SetHostileToPlayer(Map map, Pawn targetPawn = null)
        {
            Faction faction = Corps;
            if (faction == null) return;

            if (LastPilotCheck(faction.RelationKindWith(Faction.OfPlayer))) return;

            SendWarning();
            ReadinessUtility.Raise(DMSAC_DefOf.DMSAC_Defcon, 1, "trespass");

            bool LastPilotCheck(FactionRelationKind factionRelation)
            {
                if (map.mapPawns.FreeColonists.NullOrEmpty()) return false;
                Pawn pawn = map.mapPawns.FreeColonists.Where(p => p.kindDef.defName == LastPilotKind).FirstOrDefault();
                if (pawn == null) return false;

                // 老兵被認出：解除永久敵對鎖，後面的停火／好感度變化才會生效。
                // The veteran is recognised: lift the permanent hostility lock so the ceasefire below can take effect.
                FactionHostilityLock.Unlock(faction);

                //如果有帶老兵那麼會有特殊對話，並且不會敵對。
                switch (factionRelation)
                {
                    case FactionRelationKind.Hostile:
                        //"代碼5，識別到登記對象。這裡是{0}，敵對行為已臨時中止，並在此向扎爾塔的屠戮者致敬。"
                        //出乎意料的，這個古老的機兵指揮系統識別出{1}的身分並中止了敵對行為。出於對{1}戰鬥能力的尊重，{0}暫時停止了對你的敵對識別。
                        Find.LetterStack.ReceiveLetter("DMSAC_CorpNeutral".Translate(), "DMSAC_CorpNeutralDesc".Translate(faction.NameColored, pawn.NameShortColored, faction.NameColored), LetterDefOf.PositiveEvent);
                        faction.TryAffectGoodwillWith(Faction.OfPlayer, 100, reason: HistoryEventDefOf.PeaceTalksSuccess, lookTarget: pawn);
                        break;

                    case FactionRelationKind.Neutral:
                        //"代碼5，識別到登記對象。這裡是{0}，如果有任何資源需求請向{0}後勤辦事處正式遞交軍剩品徵用申請表。"
                        //古老的機兵指揮系統出於對{1}的信任，對殖民者的行為睜一隻眼閉一隻眼，但出於流程需要發出了警告。
                        Find.LetterStack.ReceiveLetter("DMSAC_CorpAlly".Translate(), "DMSAC_CorpAllyDesc".Translate(faction.NameColored, pawn.NameShortColored), LetterDefOf.PositiveEvent);
                        // 非法闖入設施的警告：用「攻擊聚落」當歷史理由比「和平會談成功」合理。
                        // A trespass warning is recorded as an attack, not as a successful peace talk.
                        faction.TryAffectGoodwillWith(Faction.OfPlayer, -10, reason: HistoryEventDefOf.AttackedSettlement, lookTarget: pawn);
                        break;

                    case FactionRelationKind.Ally:
                        //"代碼5，識別到登記對象。這裡是{0}，在此向扎爾塔的屠戮者致敬，並感謝您對殖民計畫做出的貢獻。"
                        //這個古老的機兵指揮系統識別出{1}的身分。並出於對{1}的尊重授權了殖民者在這裡自由活動的權力。
                        Find.LetterStack.ReceiveLetter("DMSAC_CorpAllyDown".Translate(), "DMSAC_CorpAllyDownDesc".Translate(faction.NameColored, pawn.NameShortColored), LetterDefOf.PositiveEvent);
                        break;
                }
                return true;
            }
            void SendWarning()
            {
                //古老的機兵指揮系統檢測到了殖民者的入侵行為，並將其視為敵對威脅。
                Find.LetterStack.ReceiveLetter("DMSAC_CorpHostile".Translate(), "DMSAC_CorpHostileDesc".Translate(), LetterDefOf.NegativeEvent, targetPawn);
                faction.TryAffectGoodwillWith(Faction.OfPlayer, faction.GoodwillToMakeHostile(Faction.OfPlayer), reason: HistoryEventDefOf.AttackedSettlement);
            }
        }
    }
}
