using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace AncientCorps
{
    /// <summary>
    /// 一級兵力池：警戒等級達到 minDefcon 起可以抽到這些機兵（各級累加）。
    /// One unit tier: from DEFCON minDefcon upward these units join the pool (tiers stack).
    /// </summary>
    public class SiteGarrisonTier
    {
        public int minDefcon;
        public List<PawnGenOption> options;
    }

    /// <summary>
    /// 掛在站點 SitePartDef 上的守軍定義，取代佈局裡寫死的機兵：
    ///   守軍點數 = 站點威脅點數 × pointsFactor（站點類型）× defconPointsFactor（警戒等級），夾在 min/maxPoints 之間；
    ///   兵種 = 所有 minDefcon ≤ 目前警戒等級的 tiers 合併後依點數抽（見 ChooseKinds：成本上限隨 DEFCON 放寬、最高一級保證出現）。
    /// GenStep_DMSAC_GarrisonedStructure 用它生成守軍，SitePartWorker_DMSAC_Installation 用它估任務描述的敵人數。
    ///
    /// Garrison definition on a site's SitePartDef, replacing the mechs hard-coded into the layouts:
    ///   garrison points = site threat points × pointsFactor (site type) × defconPointsFactor (DEFCON), clamped;
    ///   units = every tier with minDefcon ≤ the current level, merged, then rolled by points (see ChooseKinds: the cost
    ///   cap widens with DEFCON and the top tier is guaranteed). The GenStep spawns from it; the site worker estimates
    ///   the quest's enemy count.
    /// </summary>
    public class ModExtension_SiteGarrison : DefModExtension
    {
        public float pointsFactor = 1f;
        public float minPoints = 300f;
        public float maxPoints = 10000f;

        public SimpleCurve defconPointsFactor = new SimpleCurve
        {
            new CurvePoint(0f, 0.75f),
            new CurvePoint(1f, 1f),
            new CurvePoint(2f, 1.15f),
            new CurvePoint(3f, 1.3f),
            new CurvePoint(4f, 1.5f),
            new CurvePoint(5f, 1.75f)
        };

        /// <summary>
        /// 每一級警戒的單隻成本下限（與派系曲線取大）：高等級時重型機兵在一般站點點數下也放得進來。
        /// Per-level floor for the per-pawn cost cap (the larger of this and the faction curve), so heavies fit at ordinary
        /// site points once the level unlocks them.
        /// </summary>
        public SimpleCurve defconMaxPawnCost = new SimpleCurve
        {
            new CurvePoint(0f, 200f),
            new CurvePoint(1f, 300f),
            new CurvePoint(2f, 400f),
            new CurvePoint(3f, 600f),
            new CurvePoint(4f, 800f),
            new CurvePoint(5f, 1000f)
        };

        public List<SiteGarrisonTier> tiers;

        public static int CurrentDefcon => Fortified.ReadinessUtility.LevelOf(DMSAC_DefOf.DMSAC_Defcon);

        public float GarrisonPoints(float threatPoints, int defcon)
        {
            float points = threatPoints * pointsFactor * defconPointsFactor.Evaluate(defcon);
            return Mathf.Clamp(points, minPoints, Mathf.Max(minPoints, maxPoints));
        }

        public List<PawnGenOption> OptionsFor(int defcon)
        {
            List<PawnGenOption> options = new List<PawnGenOption>();
            if (tiers == null) return options;
            for (int i = 0; i < tiers.Count; i++)
            {
                SiteGarrisonTier tier = tiers[i];
                if (tier?.options == null || tier.minDefcon > defcon) continue;
                options.AddRange(tier.options.Where(o => o?.kind != null));
            }
            return options;
        }

        /// <summary>守軍派系：非玩家的站點派系，否則軍團。Garrison faction: the site's if it isn't the player, else the corps.</summary>
        public static Faction GarrisonFaction(Faction siteFaction)
        {
            if (siteFaction != null && !siteFaction.IsPlayer) return siteFaction;
            return AncientCorpsUltility.Corps;
        }

        /// <summary>
        /// 依 DEFCON 挑守軍：
        ///   1. 每隻的成本上限 = max(派系的 maxPawnCostPerTotalPointsCurve, defconMaxPawnCost)。原版只看派系曲線，
        ///      一般站點點數下高等級才解鎖的重型機兵（300~600）永遠抽不到，DEFCON 只是名義上擴大了兵力池。
        ///   2. 目前解鎖的最高一級（minDefcon &gt; 0）在預算內時先保證放一隻，讓每升一級守軍組成都看得出來。
        ///   3. 其餘依權重隨機挑到點數用完。原版的首領保留（首領群未擊敗前不出現）照樣遵守。
        /// 固定種子：任務描述的估計與實際生成一致。
        ///
        /// Picks the garrison by DEFCON:
        ///   1. Per-pawn cost cap = max(the faction's maxPawnCostPerTotalPointsCurve, defconMaxPawnCost). Vanilla only reads
        ///      the faction curve, so the heavy units (300~600) that higher levels unlock never fit at ordinary site points
        ///      and DEFCON widened the pool in name only.
        ///   2. One unit from the highest unlocked tier (minDefcon &gt; 0) goes in first when it fits, so every level visibly
        ///      changes the garrison.
        ///   3. The rest is weighted random until the points run out. Vanilla's boss reservation (bosses stay away until
        ///      their bossgroup is beaten) still applies.
        /// Seeded, so the quest's estimate matches what spawns.
        /// </summary>
        public List<PawnKindDef> ChooseKinds(float points, Faction faction, PlanetTile tile, int? seed, int defcon)
        {
            List<PawnGenOption> options = OptionsFor(defcon).Where(o => !ReservedByBoss(o.kind)).ToList();
            List<PawnKindDef> kinds = new List<PawnKindDef>();
            if (faction == null || options.Count == 0) return kinds;
            float cap = MaxPawnCost(points, faction, defcon);
            HashSet<PawnKindDef> topTier = TopTierKinds(defcon);

            if (seed.HasValue) Rand.PushState(seed.Value);
            try
            {
                float left = points;
                bool Fits(PawnGenOption o) => o.kind.combatPower <= left && o.kind.combatPower <= cap;
                if (topTier.Count > 0 && options.Where(o => topTier.Contains(o.kind) && Fits(o)).TryRandomElementByWeight(o => o.selectionWeight, out PawnGenOption top))
                {
                    kinds.Add(top.kind);
                    left -= top.kind.combatPower;
                }
                for (int guard = 0; guard < 200; guard++)
                {
                    if (!options.Where(Fits).TryRandomElementByWeight(o => o.selectionWeight, out PawnGenOption pick)) break;
                    kinds.Add(pick.kind);
                    left -= pick.kind.combatPower;
                }
            }
            finally
            {
                if (seed.HasValue) Rand.PopState();
            }
            return kinds;
        }

        /// <summary>每一隻的成本上限。Per-pawn cost cap.</summary>
        public float MaxPawnCost(float points, Faction faction, int defcon)
        {
            float byFaction = faction?.def.maxPawnCostPerTotalPointsCurve?.Evaluate(points) ?? 0f;
            return Mathf.Max(byFaction, defconMaxPawnCost.Evaluate(defcon));
        }

        /// <summary>
        /// 目前解鎖、且還有可用兵種的最高一級（minDefcon &gt; 0）。整級都是被首領保留的兵種時往下一級找，
        /// 首領群未擊敗前升級仍看得出來。
        /// Kinds of the highest unlocked tier above 0 that still has a usable kind. A tier made only of bosses still
        /// reserved by their bossgroup is skipped for the one below, so escalation stays visible before the bosses fall.
        /// </summary>
        public HashSet<PawnKindDef> TopTierKinds(int defcon)
        {
            HashSet<PawnKindDef> kinds = new HashSet<PawnKindDef>();
            if (tiers == null) return kinds;
            foreach (int level in tiers.Where(t => t?.options != null && t.minDefcon > 0 && t.minDefcon <= defcon)
                .Select(t => t.minDefcon).Distinct().OrderByDescending(l => l))
            {
                foreach (SiteGarrisonTier tier in tiers.Where(t => t?.options != null && t.minDefcon == level))
                {
                    foreach (PawnGenOption o in tier.options) { if (o?.kind != null && !ReservedByBoss(o.kind)) kinds.Add(o.kind); }
                }
                if (kinds.Count > 0) break;
            }
            return kinds;
        }

        private static bool ReservedByBoss(PawnKindDef kind)
        {
            return ModsConfig.BiotechActive && Find.BossgroupManager != null && Find.BossgroupManager.ReservedByBossgroup(kind);
        }

        /// <summary>
        /// 站點的固定亂數種子（原版 Outpost 同一套），讓任務描述的估計與實際生成一致；
        /// 世界生成的站點沒有 randomValue，改用站點 ID，不然檢視面板每一幀都會重擲。
        /// The site's stable seed (vanilla outposts use the same), so the quest estimate matches what spawns;
        /// world-gen sites carry no randomValue, so they fall back to the site ID instead of re-rolling every frame.
        /// </summary>
        public static int SeedFor(Site site, SitePartParams parms)
        {
            if (parms != null && parms.randomValue != 0) return OutpostSitePartUtility.GetPawnGroupMakerSeed(parms);
            return Gen.HashCombineInt(site?.ID ?? 0, 615724193);
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string e in base.ConfigErrors()) yield return e;
            if (tiers.NullOrEmpty() || !tiers.Any(t => t != null && t.minDefcon <= 0 && !t.options.NullOrEmpty()))
            {
                yield return "ModExtension_SiteGarrison needs at least one tier with minDefcon 0, or DEFCON 0 sites get no garrison.";
            }
            if (minPoints > maxPoints) yield return "ModExtension_SiteGarrison: minPoints is above maxPoints.";
        }
    }
}
