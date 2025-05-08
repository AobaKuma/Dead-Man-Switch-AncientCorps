using RimWorld;
using Verse;
using Verse.AI;

namespace AncientCorps
{
    public class CompProperties_TargetEffect_AddHediffChance : CompProperties
    {
        public HediffDef hediffDef;
        public float chance = 0.05f; // 50% chance to apply the hediff
        public HediffDef sideHediffDef;

        public CompProperties_TargetEffect_AddHediffChance()
        {
            compClass = typeof(CompTargetEffect_AddHediffChance);
        }
    }
    public class CompTargetEffect_AddHediffChance : CompTargetEffect
    {
        public CompProperties_TargetEffect_AddHediffChance Props => (CompProperties_TargetEffect_AddHediffChance)props;
        public override void DoEffectOn(Pawn user, Thing target)
        {
            if ((target is Pawn pawn))
            {
                if (Props.hediffDef != null) pawn.health.AddHediff(Props.hediffDef);
                if (Rand.Chance(Props.chance) && Props.sideHediffDef != null)
                {
                    pawn.health.AddHediff(Props.sideHediffDef, pawn.RaceProps.body.AllParts.RandomElement());
                }
            }
        }
    }
}