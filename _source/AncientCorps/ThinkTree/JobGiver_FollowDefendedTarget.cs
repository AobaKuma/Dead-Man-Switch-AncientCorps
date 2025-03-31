using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace AncientCorps
{
    public class JobGiver_FollowDefendedTarget : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            Pawn pawnToFollow = (pawn.GetLord().LordJob as LordJob_DefendTargetAndAssaultColony)?.defendedTarget;
            if (pawnToFollow == null|| !pawnToFollow.Spawned||pawnToFollow.Downed || pawnToFollow.Position.InHorDistOf(pawn.Position, FollowDistance) )
            {
                return null;
            }
            Job job = JobMaker.MakeJob(JobDefOf.Follow, pawnToFollow);
            job.locomotionUrgency = LocomotionUrgency.Sprint;
            job.canBashDoors = true;
            job.canUseRangedWeapon = true;
            job.checkOverrideOnExpire = true;
            job.expiryInterval = 240;
            return job;
        }
        public const float FollowDistance = 15f;
    }
}