using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace AncientCorps
{
    /// <summary>
    /// 扛走倒下的隊友。同 JobGiver_PickUpDefendedTarget：CarryDownedPawnToExit 只讀 TargetIndex.A，
    /// 之後由 JobGiver_ExitMapBest 帶離地圖。
    /// Carry a downed teammate. Same as JobGiver_PickUpDefendedTarget: only TargetIndex.A is used by the driver,
    /// and JobGiver_ExitMapBest handles leaving the map afterwards.
    /// </summary>
    public class JobGiver_PickUpTeammates : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            LordJob_DefendTargetAndAssaultColony lord = LordJob_DefendTargetAndAssaultColony.GetFor(pawn);
            if (lord == null)
            {
                return null;
            }
            lord.CheckDownedPawns();
            if (lord.downedPawns.Count == 0)
            {
                return null;
            }
            Pawn target = null;
            float minDistance = float.MaxValue;
            foreach (var newTarget in lord.downedPawns)
            {
                if (newTarget == null || !newTarget.Spawned || !newTarget.Downed) continue;
                if (!pawn.CanReserve(newTarget))
                {
                    continue;
                }
                float newDistance = (newTarget.Position - pawn.Position).SqrMagnitude;
                if (newDistance < minDistance)
                {
                    minDistance = newDistance;
                    target = newTarget;
                }
            }
            if (target == null)
            {
                return null;
            }
            return JobMaker.MakeJob(JobDefOf.CarryDownedPawnToExit, target);
        }
    }
}
