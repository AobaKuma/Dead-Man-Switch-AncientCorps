using RimWorld;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace AncientCorps
{

    public class CompTargetEffect_ResurrectMech : CompTargetEffect
    {
        public CompProperties_TargetEffectResurrect Props => (CompProperties_TargetEffectResurrect)props;

        public override void DoEffectOn(Pawn user, Thing target)
        {
            if (user.IsColonistPlayerControlled)
            {
                Job job = JobMaker.MakeJob(DMS_DefOf.DMS_ResurrectMech, target, parent);
                job.count = 1;
                job.playerForced = true;
                user.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            }
        }
    }
    public class CompTargetEffect_RepairMech : CompTargetEffect
    {
        public CompProperties_TargetEffectResurrect Props => (CompProperties_TargetEffectResurrect)props;

        public override void DoEffectOn(Pawn user, Thing target)
        {
            if (user.IsColonistPlayerControlled)
            {
                Job job = JobMaker.MakeJob(JobDefOf.RepairMech, target, parent);
                job.count = 1;
                job.playerForced = true;
                user.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                this.parent.HitPoints -= 10;
                if (this.parent.HitPoints <= 0)
                {
                    this.parent.Destroy();
                }
            }
        }
    }
}