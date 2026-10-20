using RimWorld;
using System;
using Verse;
using Verse.AI;
using UnityEngine;
using AncientCorps;
using System.Linq;
using System.Collections.Generic;
using RimWorld.Planet;
using Fortified;

namespace AncientCorps
{
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
        public Faction CorpsFaction => AncientCorpsUltility.Corps;
        public float GetCurrentScale => Scale[Mathf.Clamp(Level, 0, Scale.Length - 1)];
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
            new IntRange(480000,960000), //8~16天（0級：原本是 (-1,-1)，會讓 interval 永遠 < 0，0 級就不可能自己上升）
            new IntRange(300000,600000), //5~10天
            new IntRange(300000,600000), //5~10天
            new IntRange(600000,1200000),//10~20天
            new IntRange(450000,900000), //7.5~15天
            new IntRange(450000,900000)  //7.5~15天
        };
        private int interval;

        public override void GameComponentTick()
        {
            if (CorpsFaction == null) return;
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
            if (CorpsFaction == null) return;
            var _ = level;
            level++;
            if (level > 5) level = 5;
            if (level > historicalLevel) historicalLevel = level;
            ResetInterval();
            ResetActionInterval();
            if (level != _) //五級向上就不會跳信件了
            {
                //由於近期發生的事件使得(機兵師)指揮部提高了其部隊的警戒等級，現在它們的警戒等級為:
                Find.LetterStack.ReceiveLetter("DMSAC_Level_Rise".Translate(), "DMSAC_Level_Rise_Desc".Translate(CorpsFaction.NameColored, level) + "\n\n" + GetLevelDesc(), Level < 2 ? LetterDefOf.NegativeEvent : Level <= 4 ? LetterDefOf.ThreatSmall : LetterDefOf.ThreatBig);
                if (level == 5)
                {
                    targetedFaction = GetFaction();
                }
                SyncHuntdown();
            }
        }

        /// <summary>
        /// 五級警戒時啟動 FFF 的追殺（Huntdown）：警告 → 倒數 → 逐波增援，右側會有內建倒數提示。
        /// 其他等級（含降級）停止追殺，沿用既有的 patrol/DEFCON 動作表，不取代它。
        /// Level 5 starts FFF's Huntdown (warning → countdown → escalating waves with a built-in alert);
        /// every other level stops it. The existing patrol/DEFCON action table is left untouched.
        /// </summary>
        private void SyncHuntdown()
        {
            if (DMSAC_DefOf.DMSAC_Huntdown == null) return;
            if (level >= 5)
            {
                Map map = Find.AnyPlayerHomeMap;
                if (map != null && !HuntdownUtility.IsActive(DMSAC_DefOf.DMSAC_Huntdown))
                {
                    HuntdownUtility.Start(DMSAC_DefOf.DMSAC_Huntdown, map, false, "DMSAC_Defcon5", null);
                }
            }
            else if (HuntdownUtility.IsActive(DMSAC_DefOf.DMSAC_Huntdown))
            {
                HuntdownUtility.Stop(DMSAC_DefOf.DMSAC_Huntdown);
            }
        }
        public void LevelDown(int lv)
        {
            if (CorpsFaction == null) return;
            if (level == 0) { ResetInterval(); return; }
            // LevelDown(int) 只會下降，不會把警戒等級「調升」（原本戰爭結束設 2 級時可能會拉高目前等級）。
            level = Mathf.Clamp(lv, 0, level);
            ResetInterval();
            ResetActionInterval();
            SyncHuntdown();
            Find.LetterStack.ReceiveLetter("DMSAC_Level_Down".Translate(), "DMSAC_Level_Down_Desc".Translate(CorpsFaction.NameColored, level) + "\n\n" + GetLevelDesc(), LetterDefOf.PositiveEvent);
        }
        public void LevelDown()
        {
            if (CorpsFaction == null) return;
            if (level == 0) { ResetInterval(); return; }
            level--;
            if (level < 0) level = 0;
            ResetInterval();
            ResetActionInterval();
            SyncHuntdown();
            //根據近期觀察的活動變化，(機兵師)指揮部下降了其內部的警戒等級，現在它們的警戒等級為:
            Find.LetterStack.ReceiveLetter("DMSAC_Level_Down".Translate(), "DMSAC_Level_Down_Desc".Translate(CorpsFaction.NameColored, level) + "\n\n" + GetLevelDesc(), LetterDefOf.PositiveEvent);
        }
        private TaggedString GetLevelDesc()
        {
            // O級警戒: (機兵師)僅保留最低程度的設施間巡邏編制，並解散了部隊中的中型機兵返回駐地進行檢修。
            var taggedString = "DMSAC_Level.0".Translate(CorpsFaction.NameColored);
            switch (level)
            {
                case 1:
                    // I級警戒: (機兵師)在其地面設施之間部屬了全天候的巡邏部隊使受襲設施的增援間隔縮短，並且在部隊中伴隨大型機兵。
                    taggedString = "DMSAC_Level.1".Translate(CorpsFaction.NameColored);
                    break;
                case 2:
                    // II級警戒: 根據近期當前局勢，(機兵師)將部分的大型機兵重啟並隨時調動，並編制少量的裝甲排作為對地面設施增援的特遣部隊
                    taggedString = "DMSAC_Level.2".Translate(CorpsFaction.NameColored);
                    break;
                case 3:
                    // III級警戒: 由於近期的緊張局勢，超重型機兵已進入戰備狀態並部屬在各區域隨時增援，(機兵師)將根據殖民艦隊公約的戒備狀態條例對鄰近的非法軍事站點進行武裝驅離。
                    taggedString = "DMSAC_Level.3".Translate(CorpsFaction.NameColored);
                    break;
                case 4:
                    // IV級警戒: (機兵師)確認海牙第一公約已不再適用於當前狀況，並根據海牙第三公約進行正式的宣戰佈告。
                    // 根據海牙第二公約第二條，處於未佔領區拿起武器對抗(機兵師)部隊的平民將被視為戰爭法所定義的戰鬥員。
                    taggedString = "DMSAC_Level.4".Translate(CorpsFaction.NameColored);
                    break;
                case 5:
                    // V級警戒: 根據殖民艦隊公約的戒備狀態條例，(機兵師)確認當前狀況已達到必須展開進攻並調動重型裝甲旅來執行作戰的合規條件。。
                    // 部隊將在遵守國際人道法和《羅馬規約》條例的前提下對已識別的敵對控制區進行佔領與接管，並根據比例原則對襲擊設施的敵對勢力予以對等打擊。
                    taggedString = "DMSAC_Level.5".Translate(CorpsFaction.NameColored);
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
            if (CorpsFaction == null) return;
            AdjustCompanyCount();
            List<Company> companies = GetCompanies();
            switch (level)
            {
                case 0: //8~16天
                    //啥都不幹
                    if (Rand.Chance(0.25f)) { LevelRise(); return; }
                    for (int i = 0; i < companies.Count; i++)
                    {
                        companies[i]?.Destroy();
                    }
                    companies.Clear();
                    break;
                case 1: //4~8天
                    //5%機率自己上升
                    if (Rand.Chance(0.05f)) { LevelRise(); return; }
                    //80%生成站點
                    if (Rand.Chance(0.8f)) AncientCorpsUltility.GenerateQuest(DMSAC_DefOf.DMSAC_OpportunitySite_LogisticTerminal);
                    AdjustCompanyCount();
                    AncientCorpsUltility.TriggerRandomCompanyAction();
                    break;
                case 2: //3~6天
                    //1%機率自己上升
                    if (Rand.Chance(0.01f)) { LevelRise(); return; }

                    //40%生成站點
                    if (Rand.Chance(0.4f)) { AncientCorpsUltility.GenerateQuest(DMSAC_DefOf.DMSAC_OpportunitySite_LogisticTerminal); return; }

                    //對最近的敵對NPC基地實施攻擊，有25%機率佔領。
                    AncientCorpsUltility.TriggerRandomCompanyAction();
                    break;
                case 3://2~4天
                    //40%生成站點
                    if (Rand.Chance(0.4f)) { AncientCorpsUltility.GenerateQuest(DMSAC_DefOf.DMSAC_OpportunitySite_LogisticTerminal); return; }

                    //對最近的敵對NPC基地實施攻擊，有50%機率佔領。
                    if (HasTarget)
                    {
                        DepolyNewCompany();
                        AncientCorpsUltility.TriggerRandomCompanyAction();
                    }
                    TryTriggerCompanyRaid();
                    break;
                case 4://1~2天
                    //對最近的敵對NPC基地實施攻擊，有50%機率佔領。
                    if (Rand.Chance(0.5f)) { AncientCorpsUltility.TriggerTakeover(); return; }
                    if (HasTarget)
                    {
                        AdjustCompanyCount();
                        AncientCorpsUltility.TriggerRandomCompanyAction();
                    }
                    TryTriggerCompanyRaid();
                    break;
                case 5: //0.8~1.6天
                    //對最近的敵對NPC基地實施攻擊，有75%機率佔領。

                    if (HasTarget)
                    {
                        AncientCorpsUltility.TriggerRandomCompanyAction();
                        AdjustCompanyCount();
                    }
                    //玩家攻擊設施時會遭到襲擊。
                    break;
            }
            int extraActions = level * companies.Count / 2;
            for (int i = 0; i < extraActions; i++)
            {
                AncientCorpsUltility.TriggerRandomCompanyAction();
            }
        }

        private List<Company> GetCompanies()
        {
            return Find.WorldObjects.AllWorldObjects.OfType<Company>().Where(x => x.Faction == CorpsFaction && x.Tile != -1).ToList();
        }

        /// <summary>
        /// 讓連隊數量靠近目前警戒等級允許的上限：和平時解散多餘的連，戰時補到上限（找不到落腳點就停手）。
        /// Trim or top up the company count towards the current DEFCON ceiling; stops when no tile can be found.
        /// </summary>
        private void AdjustCompanyCount()
        {
            List<Company> companies = GetCompanies();
            while (!HasTarget && companies.Count > TargetCompanyCountMax)
            {
                Company c = companies[companies.Count - 1];
                companies.RemoveAt(companies.Count - 1);
                c?.Destroy();
            }
            if (!HasTarget) return;
            int guard = 0;
            while (GetCompanies().Count < TargetCompanyCountMax && guard++ <= TargetCompanyCountMax)
            {
                int before = GetCompanies().Count;
                DepolyNewCompany();
                if (GetCompanies().Count <= before) break;
            }
        }
        public void TryTriggerCompanyRaid()
        {
            if (CorpsFaction == null || !CorpsFaction.HostileTo(Faction.OfPlayer)) return;
            Map map = Find.AnyPlayerHomeMap;
            if (map != null)
            {
                CompanyDef company = DefDatabase<CompanyDef>.AllDefsListForReading
                    .Where(x => x.defaultFaction == DMSAC_DefOf.DMS_AncientCorps && Level >= x.defconRating)
                    .RandomElementWithFallback();
                Current.Game.GetComponent<GameComponent_RaidCompany>().RaidCompany(map, company);
            }
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref level, "Level", 0);
            Scribe_Values.Look(ref interval, "Interval", 0);
            Scribe_Values.Look(ref historicalLevel, "HistoricalLevel", 0);
            Scribe_Values.Look(ref actionInterval, "ActionInterval", 0);
            Scribe_References.Look(ref targetedFaction, "TargetedFaction");

        }

    }
    public partial class GameComponent_DefconLevel : GameComponent
    {
        public int TargetCompanyCountMax => Mathf.Clamp(Mathf.RoundToInt(4f / Mathf.Max(GetCurrentScale, 0.1f)), 2, 20);
        public bool HasTarget => GetFaction() != null;
        private Faction targetedFaction = null;

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            // 舊存檔若沒有 Interval/ActionInterval，Scribe 會回 0，下一個 tick 就會立刻降級並觸發一次行動。
            // Old saves without these values would drop a level and fire an action on the first tick.
            if (interval <= 0) ResetInterval();
            if (actionInterval <= 0) ResetActionInterval();
        }
        public Faction CurrentTarget => targetedFaction;
        public Faction GetFaction()
        {
            if (CorpsFaction == null) return null;
            if (targetedFaction != null && targetedFaction.defeated)
            {
                AncientCorpsUltility.WarIsOver(targetedFaction);
                targetedFaction = null;
            }
            if (targetedFaction == null && level == 5)
            {
                // 原本用 .First()：沒有任何合法敵對派系時會直接擲 InvalidOperationException。
                targetedFaction = Find.FactionManager.AllFactions.Where(
                        f =>
                        f.HostileTo(CorpsFaction) &&
                        f.def.humanlikeFaction &&
                        !f.Hidden &&
                        !f.defeated &&
                        !f.IsPlayer).OrderByDescending(f => f.def.techLevel).FirstOrDefault();

                if (targetedFaction != null)
                {
                    AncientCorpsUltility.SendDeclaration(targetedFaction);
                    return targetedFaction;
                }
            }
            return targetedFaction;
        }
        internal Settlement GetTargetSettlement()
        {
            if (targetedFaction == null) return null;
            // 原本把「已成為連隊目標的聚落」累積在一個永不清空的欄位裡（無限成長 + 卡住已摧毀物件）。
            // The old cache list was never cleared, grew forever and kept destroyed settlements alive.
            List<Settlement> alreadyTargeted = Find.WorldObjects.AllWorldObjects
                .OfType<Company>()
                .Where(c => c.HasTarget && c.Target != null)
                .Select(c => c.Target)
                .ToList();
            List<Settlement> settlementsA = Find.World.worldObjects.Settlements
                .Where(s => s.Faction == targetedFaction && s.Tile != -1)
                .ToList();
            if (settlementsA.NullOrEmpty()) return null;

            List<Settlement> settlementsB = Find.World.worldObjects.Settlements
                .Where(s => s.Faction == CorpsFaction && s.Tile != -1)
                .ToList();
            settlementsA.RemoveAll(x => alreadyTargeted.Contains(x));

            var v = WorldUtils.FindClosestSettlementPair(settlementsA, settlementsB);
            if (v.Item1 == null)
            {
                var a = Find.World.worldObjects.Settlements
                    .Where(s => s.Faction == targetedFaction && s.Tile != -1)
                    .ToList();
                if (a.NullOrEmpty()) return null;
                return a.RandomElement();
            }
            return v.Item1;
        }
        public void DepolyNewCompany()
        {
            if (CorpsFaction == null) return;
            if (targetedFaction == null) GetFaction();
            if (targetedFaction == null) return;
            List<Settlement> settlementsA = Find.World.worldObjects.SettlementBases
                .Where(s => s.Faction == targetedFaction && s.Tile != -1)
                .ToList();
            List<Settlement> settlementsB = Find.World.worldObjects.SettlementBases
                .Where(s => s.Faction == CorpsFaction && s.Tile != -1)
                .ToList();

            var v = WorldUtils.FindClosestSettlementPair(settlementsA, settlementsB);

            DepolyNewCompany(v.Item1);
        }
        //生成部分
        public void DepolyNewCompany(Settlement target)
        {
            if (target == null)
            {
                Log.Warning("worldObject is null!");
                return;
            }
            if (CorpsFaction == null) return;

            var worldObject = WorldObjectMaker.MakeWorldObject(DMSAC_DefOf.DMSAC_Garrison);
            if (worldObject == null)
            {
                Log.Warning("worldObject is null!");
                return;
            }
            worldObject.SetFaction(CorpsFaction);

            //根據警戒等級生成單位。
            CompanyDef companyDef = DefDatabase<CompanyDef>.AllDefsListForReading
                .Where(c => Level >= c.defconRating)
                .RandomElementWithFallback();
            if (companyDef == null || worldObject is not Company company)
            {
                Log.Warning("DMSAC: could not create a company (no CompanyDef or wrong world object class).");
                worldObject.Destroy();
                return;
            }
            company.SetCompany(companyDef);

            // 原本的 `if (result == -1) { ... } else if (...)` 讓 else 分支永遠走不到；
            // 另外 SetTarget 之後 Tile 會被覆寫成目標附近的可通行格，所以這裡只需要找到一個新站點格。
            // The old `if (result == -1) { … } else if (…)` made the else branch unreachable, and SetTarget
            // overwrites Tile with a passable cell near the target anyway.
            if (!TileFinder.TryFindNewSiteTile(out PlanetTile result, maxDist: 19, allowCaravans: true) || result == -1)
            {
                worldObject.Destroy();
                return;
            }
            worldObject.Tile = result;
            company.SetTarget(target);
            Find.WorldObjects.Add(worldObject);
        }
    }
}