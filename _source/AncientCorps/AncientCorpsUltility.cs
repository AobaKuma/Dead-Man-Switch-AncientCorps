using LudeonTK;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Verse;
using static HarmonyLib.Code;

namespace AncientCorps
{

    public static class AncientCorpsUltility
    {
        [DebugAction("DMS", null, false, false, actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.Playing)]
        public static void Defcon_Rise()
        {
            GameComponent_DefconLevel comp = Current.Game.GetComponent<GameComponent_DefconLevel>();
            comp.LevelRise();
        }
        [DebugAction("DMS", null, false, false, actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.Playing)]
        private static void Site_SpawnPatrol()
        {
            var a = Current.Game.World.worldObjects.ObjectsAt(Current.Game.CurrentMap.Tile).First();
            a.GetComponent<WorldObjectComp_PatrolSquad>()?.SpawnPatrol();
        }
        [DebugAction("DMS", null, false, false, actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.Playing)]
        private static void Defcon_SpawnCompany()
        {
            Current.Game.GetComponent<GameComponent_RaidCompany>().RaidCompany(Find.CurrentMap, null);
        }
        [DebugAction("DMS", null, false, false, actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.Playing)]
        public static void Defcon_Down()
        {
            GameComponent_DefconLevel comp = Current.Game.GetComponent<GameComponent_DefconLevel>();
            comp.LevelDown();
        }

        [DebugAction("DMS", null, false, false, actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.Playing)]
        private static void Defcon_PrintCurrentStatus()
        {
            GameComponent_DefconLevel comp = Current.Game.GetComponent<GameComponent_DefconLevel>();
            Log.Message($"DEFCON: {comp.Level} ActionTime:{comp.MTBAction}");
        }
        [DebugAction("DMS", null, false, false, actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.Playing)]
        private static void Defcon_TriggerAction()
        {
            GameComponent_DefconLevel comp = Current.Game.GetComponent<GameComponent_DefconLevel>();
            comp.StartAction();
        }

        public static void SetHostileToPlayer(Map map, Pawn targetPawn = null)
        {
            Faction faction = Find.FactionManager.FirstFactionOfDef(DMS_DefOf.DMS_AncientCorps);
            if (faction == null) return;

            if (LastPilotCheck(faction.RelationKindWith(Faction.OfPlayer))) return;

            SendWarning();
            Current.Game.GetComponent<GameComponent_DefconLevel>().LevelRise();

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
                        Find.LetterStack.ReceiveLetter("DMSAC_CorpNeutral".Translate(), "DMSAC_CorpNeutralDesc".Translate(faction.NameColored, pawn.NameShortColored, faction.NameColored), LetterDefOf.PositiveEvent);
                        faction.TryAffectGoodwillWith(Faction.OfPlayer, 100, reason: HistoryEventDefOf.PeaceTalksSuccess, lookTarget: pawn);
                        break;

                    case FactionRelationKind.Neutral:
                        //"代碼5，識別到登記對象。這裡是{0}，如果有任何資源需求請向{0}後勤辦事處正式遞交軍剩品徵用申請表。"
                        //古老的機兵指揮系統出於對{1}的信任，對殖民者的行為睜一隻眼閉一隻眼，但出於流程需要發出了警告。
                        Find.LetterStack.ReceiveLetter("DMSAC_CorpAlly".Translate(), "DMSAC_CorpAllyDesc".Translate(faction.NameColored, pawn.NameShortColored), LetterDefOf.PositiveEvent);
                        faction.TryAffectGoodwillWith(Faction.OfPlayer, -10, reason: HistoryEventDefOf.PeaceTalksSuccess, lookTarget: pawn);
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
        public static void GenerateQuest(QuestScriptDef questDef)
        {
            Slate slate = new Slate();
            slate.Set("points", StorytellerUtility.DefaultThreatPointsNow(Find.AnyPlayerHomeMap));
            Quest quest = QuestUtility.GenerateQuestAndMakeAvailable(questDef, slate);
            if (!quest.hidden && quest.root.sendAvailableLetter)
            {
                QuestUtility.SendLetterQuestAvailable(quest);
            }
        }
        public static Faction Corps => Find.FactionManager.FirstFactionOfDef(DMS_DefOf.DMS_AncientCorps);

        [DebugAction("DMS", null, false, false, actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.Playing)]
        public static void TriggerRandomCompanyAction()
        {
            var cs = Find.WorldObjects.AllWorldObjects.Where(s => s is Company && s.Faction == Corps);
            if (cs.EnumerableNullOrEmpty()) return;
            var c = cs.RandomElement();
            (c as Company).DoAction();
        }

        [DebugAction("DMS", null, false, false, actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.Playing)]
        public static void TriggerRandomCompanyActionAll()
        {
            List<WorldObject> c = Find.WorldObjects.AllWorldObjects.Where(s => s is Company && s.Faction == Corps).ToList();
            while (c.Count > 0)
            {
                (c[0] as Company).DoAction();
                c.RemoveAt(0);
            }
        }

        [DebugAction("DMS", null, false, false, actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.Playing)]
        public static void DeployCompany()
        {
            GameComponent_DefconLevel comp = Current.Game.GetComponent<GameComponent_DefconLevel>();
            comp.DepolyNewCompany();
        }
        public static void TriggerTakeover()
        {
            Faction faction = 
                Find.FactionManager.AllFactions.Where(f => 
                Corps.HostileTo(f) && 
                !f.defeated && 
                !f.IsPlayer).RandomElement();

            Settlement settlement = 
                Find.World.worldObjects.SettlementBases.Where(s => s.Faction == faction).RandomElement();

            if (settlement == null)
            {
                faction = 
                    Find.FactionManager.AllFactions.Where(
                        f => Corps.HostileTo(f) && 
                        f.def.humanlikeFaction && 
                        !f.defeated && 
                        !f.IsPlayer).RandomElement();
                settlement = Find.World.worldObjects.SettlementBases.Where(s => s.Faction == faction).RandomElement();
            }
            TriggerTakeover(settlement);
        }
        public static void TriggerTakeover(Settlement settlement)
        {
            if (settlement == null) return;
            TaggedString faction = settlement.Faction.NameColored.Colorize(settlement.Faction.Color);
            TaggedString oldName = settlement.Name;
            int tile = settlement.Tile;
            settlement.Destroy();
            SettlementDefeatUtility.CheckDefeated(settlement);

            Settlement newSettlement = (Settlement)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement);
            newSettlement.Name = SettlementNameGenerator.GenerateSettlementName(newSettlement, Corps.def.settlementNameMaker);
            newSettlement.Tile = tile;
            newSettlement.SetFaction(Corps);
            Find.WorldObjects.Add(newSettlement);
            //接管: 根據最新的消息，一支隸屬於{機兵師}的部隊接管了原屬於{受害者}的{基地舊名稱}，現在該基地被重新命名為{基地新名稱}。
            if (Rand.Chance(0.25f)) Find.LetterStack.ReceiveLetter("DMSAC_World_Takeover".Translate(), "DMSAC_World_Takeover_Desc".Translate(Corps.NameColored, faction, oldName, newSettlement.Name), LetterDefOf.PositiveEvent, lookTargets: newSettlement);
        }
        public static void SendDeclaration(Faction targetedFaction)
        {
            if (targetedFaction == null) return;
            //宣戰: 根據最新的消息，{0}廣播了對本地陣營{受害者}的宣戰公告，並提及了所有對{1}控制區域的軍事行動都將在遵守戰爭法的前提下完成。 \n\n我們尚不清楚這將會造成什麼樣的影響。
            Find.LetterStack.ReceiveLetter("DMSAC_World_Declaration".Translate(), "DMSAC_World_Declaration_Desc".Translate(Corps.NameColored.Colorize(Corps.Color), targetedFaction.NameColored.Colorize(targetedFaction.Color)), LetterDefOf.NeutralEvent);
        }

        public static void WarIsOver(Faction targetedFaction)
        {
            GameComponent_DefconLevel comp = Current.Game.GetComponent<GameComponent_DefconLevel>();
            if (comp.Level > 2)
            {
                //行動結束: 在接管了最後一個基地後，{0}宣布針對{1}的軍事行動已圓滿結束，作為行動的結果{1}在本星球的影響力被徹底的拔除。\n\n而根據情報，這支傾巢而出的軍隊在完成了它們的任務後已經重新回到原來的巡邏模式。\n戰爭結束了...\n\n暫時的。   
                Find.LetterStack.ReceiveLetter("DMSAC_World_WarOver".Translate(), "DMSAC_World_WarOver_Desc".Translate(Corps.NameColored.Colorize(Corps.Color), targetedFaction.NameColored.Colorize(targetedFaction.Color), targetedFaction.leader.NameShortColored), LetterDefOf.NeutralEvent);

                comp.LevelDown(2);
            }
        }
    }
}