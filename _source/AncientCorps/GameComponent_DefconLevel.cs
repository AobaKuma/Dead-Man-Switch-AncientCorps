using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using static HarmonyLib.Code;
using Verse.AI.Group;
using Verse.AI;
using UnityEngine.Diagnostics;
using UnityEngine;

namespace AncientCorps
{
    [StaticConstructorOnStartup]
    public partial class GameComponent_DefconLevel : GameComponent
    {
        public int Level => level;

        private int level = 0;//0級開始，最高五級
        private int historicalLevel = 0;

        protected IntRange actionIntervalRange = new IntRange(240000, 480000); //默認(1級)是4~8天
        protected int actionInterval = 0;

        //戒備等級乘數
        public float[] Scale => scale;
        protected float[] scale;

        public int MTBAction => actionInterval;

        public float GetCurrentScale => Scale[Level];
        public GameComponent_DefconLevel(Game game)
        {
            level = 0;
            historicalLevel = 0;
            actionInterval = 0;
            scale = new float[] { 2, 1, 0.75f, 0.5f, 0.25f, 0.2f };
        }

        //每個等級的下降時間，如果攻擊了設施就會重置時間。
        private readonly IntRange[] intervalPerLevel = new IntRange[]
        {
            new IntRange(-1,-1),
            new IntRange(300000,600000), //5~10天
            new IntRange(300000,600000), //5~10天
            new IntRange(600000,1200000),//10~20天
            new IntRange(450000,900000), //7.5~15天
            new IntRange(450000,900000)  //7.5~15天
        };
        private int interval;

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            if (interval < 0) return;
            interval--;
            if (interval <= 0)
            {
                LevelDown();
                return;
            }

            if (Level > 0)
            {
                actionInterval--;
                if (actionInterval > 0) return;
                StartAction();
                ResetActionInterval();
            }
        }

        public void LevelRise()
        {
            level++;
            if (level > 5) level = 5;
            if (level > historicalLevel) historicalLevel = level;
            ResetInterval();
            ResetActionInterval();
            //由於近期發生的事件使得(機兵師)指揮部提高了其部隊的警戒等級，現在它們的警戒等級為:
            Find.LetterStack.ReceiveLetter("DMSAC_Level_Rise".Translate(), "DMSAC_Level_Rise_Desc".Translate(AncientCorpsUltility.Corps.NameColored, level) + "\n\n" + GetLevelDesc(), Level < 2 ? LetterDefOf.NegativeEvent : Level <= 4 ? LetterDefOf.ThreatSmall : LetterDefOf.ThreatBig);
        }
        public void LevelDown()
        {
            if (level == 0) { ResetInterval(); return; }
            level--;
            if (level < 0) level = 0;
            ResetInterval();
            ResetActionInterval();
            //根據近期觀察的活動變化，(機兵師)指揮部下降了其內部的警戒等級，現在它們的警戒等級為:
            Find.LetterStack.ReceiveLetter("DMSAC_Level_Down".Translate(), "DMSAC_Level_Down_Desc".Translate(AncientCorpsUltility.Corps.NameColored, level) + "\n\n" + GetLevelDesc(), LetterDefOf.PositiveEvent);
        }
        private TaggedString GetLevelDesc()
        {
            // O級警戒: (機兵師)僅保留最低程度的設施間巡邏編制，並解散了部隊中的中型機兵返回駐地進行檢修。
            var taggedString = "DMSAC_Level.0".Translate(AncientCorpsUltility.Corps.NameColored);
            switch (level)
            {
                case 1:
                    // I級警戒: (機兵師)在其地面設施之間部屬了全天候的巡邏部隊使受襲設施的增援間隔縮短，並且在部隊中伴隨大型機兵。
                    taggedString = "DMSAC_Level.1".Translate(AncientCorpsUltility.Corps.NameColored);
                    break;
                case 2:
                    // II級警戒: 根據近期當前局勢，(機兵師)將部分的大型機兵重啟並隨時調動，並編制少量的裝甲排作為對地面設施增援的特遣部隊
                    taggedString = "DMSAC_Level.2".Translate(AncientCorpsUltility.Corps.NameColored);
                    break;
                case 3:
                    // III級警戒: 由於近期的緊張局勢，超重型機兵已進入戰備狀態並部屬在各區域隨時增援，(機兵師)將根據殖民艦隊公約的戒備狀態條例對鄰近的非法軍事站點進行武裝驅離。
                    taggedString = "DMSAC_Level.3".Translate(AncientCorpsUltility.Corps.NameColored);
                    break;
                case 4:
                    // IV級警戒: (機兵師)確認海牙第一公約已不再適用於當前狀況，並根據海牙第三公約進行正式的宣戰佈告。
                    // 根據海牙第二公約第二條，處於未佔領區拿起武器對抗(機兵師)部隊的平民將被視為戰爭法所定義的戰鬥員。
                    taggedString = "DMSAC_Level.4".Translate(AncientCorpsUltility.Corps.NameColored);
                    break;
                case 5:
                    // V級警戒: 根據殖民艦隊公約的戒備狀態條例，(機兵師)確認當前狀況已達到必須展開進攻並調動重型裝甲旅來執行作戰的合規條件。。
                    // 部隊將在遵守國際人道法和《羅馬規約》條例的前提下對已識別的敵對控制區進行佔領與接管，並根據比例原則對襲擊設施的敵對勢力予以對等打擊。
                    taggedString = "DMSAC_Level.5".Translate(AncientCorpsUltility.Corps.NameColored);
                    break;
            }
            return taggedString;
        }
        public void ResetInterval()
        {
            if (level < intervalPerLevel.Length) interval = intervalPerLevel[level].RandomInRange;
        }

        public void ResetActionInterval()
        {
            float newInterval = actionIntervalRange.RandomInRange * GetCurrentScale;//默認是4~8天*警戒等級倍率，0級 ，5級是
            actionInterval = (int)newInterval;
        }
        public void StartAction()
        {
            switch (level)
            {
                case 0: //8~16天
                    //啥都不幹
                    break;
                case 1: //4~8天
                    //5%機率自己上升
                    if (Rand.Chance(0.05f)) { LevelRise(); return; }

                    //80%生成站點
                    if (Rand.Chance(0.8f)) AncientCorpsUltility.GenerateQuest(DMS_DefOf.DMSAC_OpportunitySite_Graveyard);
                    break;
                case 2: //3~6天
                    //1%機率自己上升
                    if (Rand.Chance(0.01f)) { LevelRise(); return; }

                    //40%生成站點
                    if (Rand.Chance(0.4f)) { AncientCorpsUltility.GenerateQuest(DMS_DefOf.DMSAC_OpportunitySite_Graveyard); return; }

                    //對最近的敵對NPC基地實施攻擊，有25%機率佔領。
                    if (Rand.Chance(0.5f)) AncientCorpsUltility.TriggerTakeover();
                    break;
                case 3://2~4天
                    //40%生成站點
                    if (Rand.Chance(0.4f)) { AncientCorpsUltility.GenerateQuest(DMS_DefOf.DMSAC_OpportunitySite_Graveyard); return; }

                    //對最近的敵對NPC基地實施攻擊，有50%機率佔領。
                    if (Rand.Chance(0.5f)) { AncientCorpsUltility.TriggerTakeover(); return; }

                    //對玩家實施連級襲擊
                    break;
                case 4://1~2天
                    //對最近的敵對NPC基地實施攻擊，有50%機率佔領。
                    if (Rand.Chance(0.5f)) { AncientCorpsUltility.TriggerTakeover(); return; }

                    //對玩家實施重型機兵連襲擊
                    break;
                case 5: //0.8~1.6天
                    //對最近的敵對NPC基地實施攻擊，有75%機率佔領。
                    if (Rand.Chance(0.75f)) { AncientCorpsUltility.TriggerTakeover(); return; }

                    //對玩家實施稜堡洪流襲擊
                    //玩家攻擊設施時會遭到襲擊。
                    break;
            }
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref level, "Level", 0);
            Scribe_Values.Look(ref interval, "Interval", 0);
            Scribe_Values.Look(ref historicalLevel, "HistoricalLevel", 0);
            Scribe_Values.Look(ref actionInterval, "ActionInterval", 0);
        }
    }
    public partial class GameComponent_DefconLevel
    {
        public void RaidCompany(Map map, CompanyDef company, int raidPoint, int LordID)
        {
            Faction faction = Find.FactionManager.FirstFactionOfDef(company.defaultFaction);
            if (faction == null) { Log.Error("Company faction have null faction, using pirate instead"); faction = Find.FactionManager.OfPirates; }

            //訊息
            Find.LetterStack.ReceiveLetter("DMSAC_CompanyRaid".Translate(), "DMSAC_CompanyRaid_Desc".Translate(), LetterDefOf.ThreatBig);

            for (int i = 0; i < company.squadCountRange.RandomInRange; i++)
            {
                PlatoonMaker maker = company.squads.RandomElement();

                if (map != null && RCellFinder.TryFindRandomPawnEntryCell(out var result, map, CellFinder.EdgeRoadChance_Animal + 0.2f))
                {
                    Pawn leaderPawn = (Pawn)GenSpawn.Spawn(PawnGenerator.GeneratePawn(maker.leaderKindDef.RandomElement(), faction), result, map, WipeMode.VanishOrMoveAside);

                    List<Pawn> group = new List<Pawn>();
                    foreach (PawnGenOption pawnOption in maker.fixedPawnkind)
                    {
                        for (int j = 0; j < (int)pawnOption.selectionWeight; j++)
                        {
                            Pawn squadPawnFix = (Pawn)GenSpawn.Spawn(PawnGenerator.GeneratePawn(pawnOption.kind, faction), result, map, WipeMode.VanishOrMoveAside);
                            group.Add(squadPawnFix);
                        }
                    }

                    for (int k = 0; k < maker.memberCountRange.RandomInRange; k++)
                    {
                        Pawn squadPawnFix = (Pawn)GenSpawn.Spawn(PawnGenerator.GeneratePawn(maker.memberKindDefs.RandomElement(), faction), result, map, WipeMode.VanishOrMoveAside);
                        group.Add(squadPawnFix);
                    }
                    LordMaker.MakeNewLord(faction, new LordJob_StageThenAttack(faction, result, Rand.Range(1, 10)), map, new List<Pawn>() { leaderPawn });
                    //TODO，分配Pawn
                }
            }
        }
    }
}