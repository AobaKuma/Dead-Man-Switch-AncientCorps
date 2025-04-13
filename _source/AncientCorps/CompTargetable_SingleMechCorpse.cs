using RimWorld;
using System.Collections.Generic;
using Verse;

namespace AncientCorps
{
    public class CompTargetable_SingleMech : CompTargetable
    {
        protected override bool PlayerChoosesTarget => true;

        public override IEnumerable<Thing> GetTargets(Thing targetChosenByPlayer = null)
        {
            yield return targetChosenByPlayer;
        }

        protected override TargetingParameters GetTargetingParameters()
        {
            return new TargetingParameters
            {
                canTargetHumans = false,
                canTargetAnimals = false,
                canTargetPawns = true,
                canTargetBuildings = false,
                canTargetItems = false,
                canTargetCorpses = false,
                canTargetMechs = true,
                onlyTargetControlledPawns = true,
                mapObjectTargetsMustBeAutoAttackable = false
            };
        }
    }

    public class CompTargetable_SingleMechCorpse : CompTargetable
    {
        protected override bool PlayerChoosesTarget => true;

        protected override TargetingParameters GetTargetingParameters()
        {
            return new TargetingParameters
            {
                canTargetPawns = false,
                canTargetBuildings = false,
                canTargetItems = false,
                canTargetCorpses = true,
                mapObjectTargetsMustBeAutoAttackable = false
            };
        }

        public override IEnumerable<Thing> GetTargets(Thing targetChosenByPlayer = null)
        {
            yield return targetChosenByPlayer;
        }

        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            if (target.Thing is Corpse c && c.InnerPawn.RaceProps.IsMechanoid)
            {
                return base.ValidateTarget(target.Thing, showMessages);
            }
            return false;
        }
    }
}