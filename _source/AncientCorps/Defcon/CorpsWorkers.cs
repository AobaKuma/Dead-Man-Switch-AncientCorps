using System.Collections.Generic;
using Fortified;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace AncientCorps
{
    /// <summary>
    /// 軍團的 DEFCON 行為：通用部分交給 FFF 的 ReadinessWorker，這裡只放軍團特有的反應——
    /// 第一支巡邏隊抵達時宣告敵對（帶著最後的駕駛員則會被認出而停火）。
    /// The corps' DEFCON behaviour: FFF's ReadinessWorker does the generic part; this only adds the corps reaction to
    /// the first patrol arrival (turn hostile, unless the Last Pilot is recognised).
    /// </summary>
    public class ReadinessWorker_Corps : ReadinessWorker
    {
        /// <summary>
        /// DEFCON II 的永久敵對有一個例外：殖民地帶著最後的駕駛員（老兵）時不鎖。
        /// DEFCON II's permanent hostility has one exception: no lock while the colony has the Last Pilot.
        /// </summary>
        public override bool ShouldLockHostility(ReadinessState state, int oldLevel, string reason)
        {
            return base.ShouldLockHostility(state, oldLevel, reason) && !AncientCorpsUltility.PlayerHasLastPilot();
        }

        public override void OnPatrolArrived(Map map, List<Pawn> pawns, bool first, float pressure)
        {
            if (first)
            {
                AncientCorpsUltility.SetHostileToPlayer(map, pawns.RandomElementWithFallback());
                return;
            }
            base.OnPatrolArrived(map, pawns, first, pressure);
        }
    }

    /// <summary>
    /// 軍團連隊：襲擊時每一排守著自己的指揮步行者推進（LordJob_DefendTargetAndAssaultColony）。
    /// Corps companies: in a raid each platoon advances guarding its own command walker.
    /// </summary>
    public class CompanyWorker_Corps : CompanyWorker
    {
        public override Lord MakeRaidLord(Faction faction, Pawn leader, List<Pawn> platoon, Map map)
        {
            return LordMaker.MakeNewLord(faction, new LordJob_DefendTargetAndAssaultColony(leader), map, platoon);
        }
    }
}
