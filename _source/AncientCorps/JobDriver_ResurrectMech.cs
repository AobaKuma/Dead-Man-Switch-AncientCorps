using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AncientCorps
{
    public class JobDriver_ResurrectMech : JobDriver
    {
        private const TargetIndex CorpseInd = TargetIndex.A;

        private const TargetIndex ItemInd = TargetIndex.B;

        private const int DurationTicks = 450;

        private Mote warmupMote;

        private Corpse Corpse => (Corpse)job.GetTarget(TargetIndex.A).Thing;

        private Thing Item => job.GetTarget(TargetIndex.B).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (pawn.Reserve(Corpse, job, 1, -1, null, errorOnFailed))
            {
                return pawn.Reserve(Item, job, 1, -1, null, errorOnFailed);
            }
            return false;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch).FailOnDespawnedOrNull(TargetIndex.B).FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Haul.StartCarryThing(TargetIndex.B);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch).FailOnDespawnedOrNull(TargetIndex.A);
            Toil toil = Toils_General.Wait(600);
            toil.WithProgressBarToilDelay(TargetIndex.A);
            toil.FailOnDespawnedOrNull(TargetIndex.A);
            toil.FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
            toil.WithEffect(effectDef: EffecterDefOf.ConstructMetal, TargetIndex.A);
            toil.tickAction = delegate
            {
                CompUsable compUsable = Item.TryGetComp<CompUsable>();
                if (compUsable != null && warmupMote == null && compUsable.Props.warmupMote != null)
                {
                    warmupMote = MoteMaker.MakeAttachedOverlay(Corpse, compUsable.Props.warmupMote, Vector3.zero);
                }
                warmupMote?.Maintain();
            };
            yield return toil;
            yield return Toils_General.Do(Resurrect);
        }

        private void Resurrect()
        {
            Pawn innerPawn = Corpse.InnerPawn;
            ResurrectionUtility.TryResurrect(innerPawn);
            Item.SplitOff(1).Destroy();
        }
    }
}