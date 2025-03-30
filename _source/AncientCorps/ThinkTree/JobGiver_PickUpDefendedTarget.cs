using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace AncientCorps
{
    public class JobGiver_PickUpDefendedTarget : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            Pawn pawnToFollow = (pawn.GetLord().LordJob as LordJob_DefendTargetAndAssaultColony)?.defendedTarget;
            if (pawnToFollow == null || !pawnToFollow.Spawned || pawn.CanReserve(pawnToFollow))
            {
                return null;
            }
            RCellFinder.TryFindBestExitSpot(pawn, out IntVec3 exitSpot , TraverseMode.ByPawn, false);
            Job job = JobMaker.MakeJob(JobDefOf.CarryDownedPawnToExit, pawnToFollow, exitSpot);
            job.count = 1;
            return job;
        }
    }
}