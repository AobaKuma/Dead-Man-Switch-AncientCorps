using RimWorld;
using System.Linq;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace AncientCorps
{
    public class JobGiver_PickUpTeammates : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            LordJob_DefendTargetAndAssaultColony lord = (pawn.GetLord().LordJob as LordJob_DefendTargetAndAssaultColony);
            if (lord == null)
            {
                return null;
            }
            lord.CheckDownedPawns();
            if (!lord.downedPawns.Any())
            {
                return null;
            }
            Pawn target = null;
            float minDistance = 9999999f;
            foreach (var newTarget in lord.downedPawns)
            {
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
            RCellFinder.TryFindBestExitSpot(pawn, out IntVec3 exitSpot , TraverseMode.ByPawn, false);
            Job job = JobMaker.MakeJob(JobDefOf.CarryDownedPawnToExit, target, exitSpot);
            job.count = 1;
            return job;
        }
    }
}