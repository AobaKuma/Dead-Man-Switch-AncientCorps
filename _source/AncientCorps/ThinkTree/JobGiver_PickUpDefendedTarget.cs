using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace AncientCorps
{
    /// <summary>
    /// 指揮官倒下時把他扛起來。JobDriver_CarryDownedPawn 只讀 TargetIndex.A，所以目標點/數量都不寫，
    /// 扛起來之後由同一個 DutyDef 後面的 JobGiver_ExitMapBest 帶離地圖。
    /// Commander recovery. JobDriver_CarryDownedPawn only reads TargetIndex.A, so no destination or count is set;
    /// the JobGiver_ExitMapBest later in the same DutyDef walks the carrier off the map.
    /// </summary>
    public class JobGiver_PickUpDefendedTarget : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            Pawn pawnToFollow = LordJob_DefendTargetAndAssaultColony.GetFor(pawn)?.DefendedTargetOrNull;
            if (pawnToFollow == null || !pawnToFollow.Spawned || !pawnToFollow.Downed)
            {
                return null;
            }
            // 守衛本來寫反了：能預約時才 return null，結果永遠不會去扛。
            // The guard used to be inverted: it returned null exactly when the pawn *could* reserve.
            if (!pawn.CanReserve(pawnToFollow))
            {
                return null;
            }
            return JobMaker.MakeJob(JobDefOf.CarryDownedPawnToExit, pawnToFollow);
        }
    }
}
