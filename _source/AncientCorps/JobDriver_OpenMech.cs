using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace AncientCorps
{
    public class JobDriver_OpenMech : JobDriver
    {
        protected Building Target
        {
            get
            {
                return this.job.targetA.Thing as Building;
            }
        }
        private Building_DeactivatedMech Building => (Building_DeactivatedMech)job.GetTarget(TargetIndex.A).Thing;
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return ReservationUtility.Reserve(this.pawn, this.Target, this.job, 1, -1, null, errorOnFailed);
        }
        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoCell(this.Target.Position, PathEndMode.Touch);
            yield return Toils_General.WaitWith(TargetIndex.A, 250, true, true, false, face: TargetIndex.A).WithEffect(EffecterDefOf.Hacking, TargetIndex.A);
            Toil gearDown = new Toil()
            {
                initAction = () =>
                {
                    Pawn actor = this.GetActor();
                    Building.EjectContents(actor);
                }
            };
            yield return gearDown;
            yield break;
        }
    }

    public class JobDriver_DisableMech : JobDriver
    {
        protected Building Target
        {
            get
            {
                return this.job.targetA.Thing as Building;
            }
        }
        private Building_DeactivatedMech Building => (Building_DeactivatedMech)job.GetTarget(TargetIndex.A).Thing;
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return ReservationUtility.Reserve(this.pawn, this.Target, this.job, 1, -1, null, errorOnFailed);
        }
        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoCell(this.Target.Position, PathEndMode.OnCell);
            yield return Toils_General.WaitWith(TargetIndex.A, 250, true, true, false, face: TargetIndex.A).WithEffect(EffecterDefOf.ConstructMetal, TargetIndex.A);
            Toil gearDown = new Toil()
            {
                initAction = () =>
                {
                    Pawn actor = this.GetActor();
                    Building.EjectContents(actor,true);
                }
            };
            yield return gearDown;
            yield break;
        }
    }
}