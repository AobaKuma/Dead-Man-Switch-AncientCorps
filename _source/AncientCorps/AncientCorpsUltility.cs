using RimWorld;
using System.Linq;
using Verse;

namespace AncientCorps
{
    public static class AncientCorpsUltility
    {
        public static void SetHostileToPlayer(Map map, Pawn targetPawn = null)
        {
            Faction faction = Find.FactionManager.FirstFactionOfDef(DMS_DefOf.DMS_AncientCorps);
            if (faction == null) return;

            if (!LastPilotCheck(faction.RelationKindWith(Faction.OfPlayer)))
            {
                SendWarning();
            }

            bool LastPilotCheck(FactionRelationKind factionRelation)
            {
                if (map.mapPawns.FreeColonists.NullOrEmpty()) return false;
                Pawn pawn = map.mapPawns.FreeColonists.Where(p => p.kindDef.defName == "DMS_LastPilot").FirstOrDefault();
                if (pawn == null) return false;

                //如果有帶老兵那麼會有特殊對話，並且不會敵對。
                switch (factionRelation)
                {
                    case FactionRelationKind.Hostile:
                        //"代碼5，識別到登記對象。這裡是{0}，敵對行為已臨時中止，並在此向扎爾塔的屠戮者致敬。"
                        //出乎意料的，這個古老的機兵指揮系統識別出{1}的身分並中止了敵對行為。出於對{1}戰鬥能力的尊重，{0}暫時停止了對你的敵對識別。
                        Find.LetterStack.ReceiveLetter("DMSAC_CorpNeutral".Translate(), "DMSAC_CorpNeutralDesc".Translate(faction.NameColored,pawn.NameShortColored, faction.NameColored), LetterDefOf.PositiveEvent);
                        faction.TryAffectGoodwillWith(Faction.OfPlayer, 100, reason: HistoryEventDefOf.PeaceTalksSuccess, lookTarget: pawn);
                        break;

                    case FactionRelationKind.Neutral:
                        //"代碼5，識別到登記對象。這裡是{0}，如果有任何資源需求請向{0}後勤辦事處正式遞交軍剩品徵用申請表。"
                        //古老的機兵指揮系統出於對{1}的信任，對殖民者的行為睜一隻眼閉一隻眼，但出於流程需要發出了警告。
                        Find.LetterStack.ReceiveLetter("DMSAC_CorpAlly".Translate(), "DMSAC_CorpAllyDesc".Translate(faction.NameColored,pawn.NameShortColored), LetterDefOf.PositiveEvent);
                        faction.TryAffectGoodwillWith(Faction.OfPlayer, -10, reason: HistoryEventDefOf.PeaceTalksSuccess, lookTarget: pawn);
                        break;

                    case FactionRelationKind.Ally:
                        //"代碼5，識別到登記對象。這裡是{0}，在此向扎爾塔的屠戮者致敬，並感謝您對殖民計畫做出的貢獻。"
                        //這個古老的機兵指揮系統識別出{1}的身分。並出於對{1}的尊重授權了殖民者在這裡自由活動的權力。
                        Find.LetterStack.ReceiveLetter("DMSAC_CorpAllyDown".Translate(), "DMSAC_CorpAllyDownDesc".Translate(faction.NameColored,pawn.NameShortColored), LetterDefOf.PositiveEvent);
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