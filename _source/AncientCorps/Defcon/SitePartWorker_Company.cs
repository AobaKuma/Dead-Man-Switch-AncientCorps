using RimWorld;
using Verse;
using RimWorld.Planet;
using RimWorld.QuestGen;
using System.Collections.Generic;
using Verse.Grammar;
using System.Linq;
using Verse.Noise;
using UnityEngine;
using DMS;

namespace AncientCorps
{
    public class SitePartWorker_Artillery : SitePartWorker_Company
    { 
    
    }
    /// <summary>
    /// 大致上有一個Company，然後這個Company會有一個目標，然後這個Company會隨著時間進行襲擊
    /// 對於非玩家陣營來說會使基地被占領。
    /// </summary>
    public class SitePartWorker_Company : SitePartWorker_Outpost
    {
        public override void SitePartWorkerTick(SitePart sitePart)
        {
            base.SitePartWorkerTick(sitePart);
            if (sitePart.lastRaidTick != -1 && !(Find.TickManager.TicksGame > sitePart.lastRaidTick + 90000f)) return;

            //如果與玩家陣營敵對，則會隨著時間進行襲擊
            if (sitePart.site?.Faction != null && !sitePart.site.HasMap)
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

            if (sitePart.site.Faction.HostileTo(Faction.OfPlayer)) return;
            List<Map> maps = Find.Maps.Where(m => m.IsPlayerHome).ToList();
            for (int i = 0; i < maps.Count; i++)
            {
                if (maps[i].GetRangeBetweenTiles(sitePart.site.Tile) > company.combatRadius) continue;
                if (sitePart.site.IsHashIntervalTick(2500) && Rand.MTBEventOccurs(QuestTuning.PointsToRaidSourceRaidsMTBDaysCurve.Evaluate(sitePart.parms.threatPoints), 60000f, 2500f))
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
            if (sitePart.site is Company)
            {
                GenDraw.DrawWorldRadiusRing(sitePart.site.Tile, GetCompany(sitePart).combatRadius);
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
            sitePartParams.threatPoints = Mathf.Max(sitePartParams.threatPoints, faction.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Combat));
            sitePartParams.lootMarketValue = ThreatPointsLootMarketValue.Evaluate(sitePartParams.threatPoints);
            return sitePartParams;
        }
        private int count = -1;
        protected int GetCompanyCount(Site site)
        {
            if (site is Company site_Company)
            {
                if (count != -1) return count;
                return count = site_Company.GetCompany().GeneratePawns().ToList().Count();
            }
            return 25;
        }
        public override void PostMapGenerate(Map map)
        {
            base.PostMapGenerate(map);
            if (AncientCorpsUltility.Corps.HostileTo(Faction.OfPlayer)) AncientCorpsUltility.Defcon_Rise();
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