using RimWorld;
using Verse;

namespace AncientCorps
{
    public class CompUseEffect_LevelUpDefcon : CompUseEffect
    {
        public CompProperties_UseEffect_LevelUpDefcon Props => (CompProperties_UseEffect_LevelUpDefcon)props;
        public override void DoEffect(Pawn usedBy)
        {
            base.DoEffect(usedBy);
            if (Rand.Chance(Props.chance))
            {
                AncientCorpsUltility.Defcon_Rise();
            }
        }
    }
    public class CompProperties_UseEffect_LevelUpDefcon : CompProperties_UseEffect
    {
        public float chance = 0.2f;
        public CompProperties_UseEffect_LevelUpDefcon()
        {
            compClass = typeof(CompUseEffect_LevelUpDefcon);
        }
    }
}