using RimWorld;
using Verse;
using RimWorld.Planet;
using RimWorld.QuestGen;
using System.Collections.Generic;
using Verse.Grammar;
using System.Linq;
using UnityEngine;

namespace AncientCorps
{
    /// <summary>
    /// 大致上有一個Company，然後這個Company會有一個目標，然後這個Company會隨著時間進行襲擊
    /// 對於非玩家陣營來說會使基地被占領。
    /// </summary>
    public class SitePartWorker_Company : SitePartWorker_Outpost
    {
        // 原版 Site.Tick() 會「每個 world tick」呼叫 SitePartWorkerTick，所以這裡必須自己節流，
        // 否則 lastRaidTick 一過期，後面的擲骰（50/25/25/10%）就會每個 tick 都跑一次。
        // Vanilla calls SitePartWorkerTick on every world tick, so throttle here; otherwise once
        // lastRaidTick expires the 50/25/25/10% rolls run every single tick.
        private const int TickInterval = 2500;
        private const int RaidCooldownTicks = 90000;

        public override void SitePartWorkerTick(SitePart sitePart)
        {
            base.SitePartWorkerTick(sitePart);
            if (sitePart?.site == null) return;
            if (!sitePart.site.IsHashIntervalTick(TickInterval)) return;
            if (sitePart.lastRaidTick != -1 && Find.TickManager.TicksGame < sitePart.lastRaidTick + RaidCooldownTicks) return;

            // 先上膛，兩條路徑（襲擊、公司行動）共用同一個冷卻。
            // Re-arm first: the raid path and the company-action path share the same cooldown.
            sitePart.lastRaidTick = Find.TickManager.TicksGame;

            // 與玩家敵對的部隊才會隨時間派出襲擊。
            // Only a company hostile to the player keeps raiding over time.
            if (sitePart.site.Faction != null && !sitePart.site.HasMap)
            {
                DoAction(sitePart);
            }
            if (sitePart.site is Company site_Company)
            {
                site_Company.DoAction();
            }
        }

        protected virtual void DoAction(SitePart sitePart)
        {
            CompanyDef company = GetCompany(sitePart);
            if (company == null) return;
            if (sitePart.site.Faction == null || !sitePart.site.Faction.HostileTo(Faction.OfPlayer)) return;

            List<Map> maps = Find.Maps.Where(m => m.IsPlayerHome).ToList();
            for (int i = 0; i < maps.Count; i++)
            {
                if (maps[i].GetRangeBetweenTiles(sitePart.site.Tile) > company.combatRadius) continue;
                if (Rand.MTBEventOccurs(QuestTuning.PointsToRaidSourceRaidsMTBDaysCurve.Evaluate(sitePart.parms.threatPoints), 60000f, TickInterval))
                {
                    StartRaid(maps[i], sitePart);
                }
            }
        }

        private CompanyDef GetCompany(SitePart sitePart)
        {
            if (sitePart.site is Company site_Company)
            {
                return site_Company.GetCompany();
            }
            return null;
        }

        public override void Init(Site site, SitePart sitePart)
        {
            base.Init(site, sitePart);
            sitePart.lastRaidTick = Find.TickManager.TicksGame;
        }

        public override void PostDrawExtraSelectionOverlays(SitePart sitePart)
        {
            base.PostDrawExtraSelectionOverlays(sitePart);
            CompanyDef company = GetCompany(sitePart);
            if (company != null)
            {
                GenDraw.DrawWorldRadiusRing(sitePart.site.Tile, company.combatRadius);
            }
        }

        private void StartRaid(Map map, SitePart sitePart)
        {
            IncidentParms incidentParms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, map);
            incidentParms.forced = true;
            incidentParms.points *= 0.6f;
            if (IncidentDefOf.RaidEnemy.Worker.CanFireNow(incidentParms))
            {
                Current.Game.GetComponent<GameComponent_RaidCompany>().RaidCompany(map, (sitePart.site as Company).GetCompany());
                sitePart.lastRaidTick = Find.TickManager.TicksGame;
            }
        }

        public override SitePartParams GenerateDefaultParams(float myThreatPoints, PlanetTile tile, Faction faction)
        {
            SitePartParams sitePartParams = base.GenerateDefaultParams(myThreatPoints, tile, faction);
            if (faction != null)
            {
                sitePartParams.threatPoints = Mathf.Max(sitePartParams.threatPoints, faction.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Combat));
            }
            sitePartParams.lootMarketValue = ThreatPointsLootMarketValue.Evaluate(sitePartParams.threatPoints);
            return sitePartParams;
        }

        // 原本用 private int count 快取：SitePartWorker 是「每個 SitePartDef 一份」的單例，
        // 所以第一個連的敵軍數會被套用到所有連。這裡改成每次重算。
        // The old private int count cache was per-def (a singleton), so the first company's enemy count
        // leaked into every other company. Recompute each time instead.
        protected int GetCompanyCount(Site site)
        {
            if (site is Company site_Company)
            {
                CompanyDef def = site_Company.GetCompany();
                if (def == null) return 25;
                return def.GeneratePawns().Count();
            }
            return 25;
        }

        public override void PostMapGenerate(Map map)
        {
            base.PostMapGenerate(map);
            // DEFCON 的提升由 Company.PostMapGenerate 統一處理，這裡不再重複加一級。
            // The DEFCON rise is handled once in Company.PostMapGenerate.
        }

        public override string GetPostProcessedThreatLabel(Site site, SitePart sitePart)
        {
            return "KnownSiteThreatEnemyCountAppend".Translate(GetCompanyCount(site), "Enemies".Translate());
        }

        public override void Notify_GeneratedByQuestGen(SitePart part, Slate slate, List<Rule> outExtraDescriptionRules, Dictionary<string, string> outExtraDescriptionConstants)
        {
            base.Notify_GeneratedByQuestGen(part, slate, outExtraDescriptionRules, outExtraDescriptionConstants);
            int enemiesCount = GetCompanyCount(part.site);
            float num = QuestTuning.PointsToRaidSourceRaidsMTBDaysCurve.Evaluate(part.parms.threatPoints);
            outExtraDescriptionRules.Add(new Rule_String("enemiesCount", enemiesCount.ToString()));
            outExtraDescriptionRules.Add(new Rule_String("mtbDays", ((int)(num * 60000f)).ToStringTicksToPeriod(allowSeconds: true, shortForm: false, canUseDecimals: false)));
            outExtraDescriptionRules.Add(new Rule_String("enemiesLabel", GetEnemiesLabel(part.site, enemiesCount)));
        }
    }
}
