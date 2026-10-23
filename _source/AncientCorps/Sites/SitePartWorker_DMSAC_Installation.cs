using System.Collections.Generic;
using Fortified;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using UnityEngine;
using Verse;
using Verse.Grammar;

namespace AncientCorps
{
    /// <summary>
    /// 軍團站點的 SitePartWorker，比照 FFF 的 SitePartWorker_FFF_TimedHostileSite（DMS2 站點同一套）：
    /// 威脅點數決定戰利品價值，進入地圖後啟動依難度縮放的增援倒數（FFF_TimedSiteExtension）。
    /// 不同處：
    ///  - 任務描述的敵人數用 ModExtension_SiteGarrison 估，跟實際守軍同一份定義；
    ///  - 世界生成、帶 WorldObjectComp_PatrolSquad 的站點由巡邏隊負責增援，不再另外倒數，避免兩套增援疊在一起。
    ///
    /// Site worker for corps sites, modelled on FFF's SitePartWorker_FFF_TimedHostileSite (what DMS2's sites use):
    /// threat points set the loot value and entering starts a difficulty-scaled reinforcement countdown
    /// (FFF_TimedSiteExtension). Differences: the quest's enemy count comes from ModExtension_SiteGarrison, the same
    /// definition the garrison spawns from; and world-gen sites with WorldObjectComp_PatrolSquad keep their patrols as
    /// the reinforcement, with no extra countdown stacked on top.
    /// </summary>
    public class SitePartWorker_DMSAC_Installation : SitePartWorker
    {
        private static readonly FFF_TimedSiteExtension DefaultTimed = new FFF_TimedSiteExtension();

        private FFF_TimedSiteExtension Timed => def.GetModExtension<FFF_TimedSiteExtension>() ?? DefaultTimed;
        private ModExtension_SiteGarrison Garrison => def.GetModExtension<ModExtension_SiteGarrison>();

        public override SitePartParams GenerateDefaultParams(float myThreatPoints, PlanetTile tile, Faction faction)
        {
            SitePartParams parms = base.GenerateDefaultParams(myThreatPoints, tile, faction);
            parms.lootMarketValue = SitePartWorker_FFF_TimedHostileSite.LootMarketValueFor(parms.threatPoints);
            return parms;
        }

        public override void Notify_GeneratedByQuestGen(SitePart part, Slate slate, List<Rule> outExtraDescriptionRules, Dictionary<string, string> outExtraDescriptionConstants)
        {
            base.Notify_GeneratedByQuestGen(part, slate, outExtraDescriptionRules, outExtraDescriptionConstants);
            int count = EstimateEnemies(part.site, part.parms);
            outExtraDescriptionRules.Add(new Rule_String("enemiesCount", count.ToString()));
            outExtraDescriptionRules.Add(new Rule_String("enemiesLabel", EnemiesLabel(part.site, count)));
        }

        public override string GetPostProcessedThreatLabel(Site site, SitePart sitePart)
        {
            if (site.Faction != null && site.Faction.IsPlayer) return null;
            int count = EstimateEnemies(site, sitePart.parms);
            if (count <= 0) return base.GetPostProcessedThreatLabel(site, sitePart);
            return base.GetPostProcessedThreatLabel(site, sitePart) + ": "
                + "KnownSiteThreatEnemyCountAppend".Translate(count, "Enemies".Translate());
        }

        public override void PostMapGenerate(Map map)
        {
            base.PostMapGenerate(map);
            if (map.Parent.GetComponent<WorldObjectComp_ReadinessPatrol>() != null) return;

            FFF_TimedSiteExtension ext = Timed;
            int ticks = FFF_TimedSiteUtility.EntryCountdownTicks(ext.baseEntryHours, ext.minEntryHours, ext.maxEntryHours);
            TimedDetectionRaids timedRaids = map.Parent.GetComponent<TimedDetectionRaids>();
            if (timedRaids == null) return;
            timedRaids.StartDetectionCountdown(ticks, 0);
            timedRaids.alertRaidsArrivingIn = true;
            Find.LetterStack.ReceiveLetter(ext.entryLetterLabelKey.Translate(), ext.entryLetterTextKey.Translate(ticks.ToStringTicksToPeriod()),
                LetterDefOf.ThreatBig, map.Parent);
        }

        private int EstimateEnemies(Site site, SitePartParams parms)
        {
            ModExtension_SiteGarrison ext = Garrison;
            if (ext == null || site?.Faction == null || parms == null) return 0;
            float threat = parms.threatPoints > 0f ? parms.threatPoints : StorytellerUtility.DefaultSiteThreatPointsNow();
            int defcon = ModExtension_SiteGarrison.CurrentDefcon;
            return ext.ChooseKinds(ext.GarrisonPoints(threat, defcon), ModExtension_SiteGarrison.GarrisonFaction(site.Faction), site.Tile, ModExtension_SiteGarrison.SeedFor(site, parms), defcon).Count;
        }

        private static string EnemiesLabel(Site site, int count)
        {
            if (site?.Faction == null) return count == 1 ? "Enemy".Translate() : "Enemies".Translate();
            return count == 1 ? site.Faction.def.pawnSingular : site.Faction.def.pawnsPlural;
        }
    }
}
