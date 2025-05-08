using RimWorld;
using System.Linq;
using Verse;
using static RimWorld.MechClusterSketch;

namespace AncientCorps
{

    public class CompUseEffect_DisableMechs : CompUseEffect
    {
        public CompProperties_UseEffect_DisableMechs Props => (CompProperties_UseEffect_DisableMechs)props;
        public override void DoEffect(Pawn usedBy)
        {
            base.DoEffect(usedBy);
            var m = Find.CurrentMap.mapPawns.AllPawnsSpawned.Where(p => p.RaceProps.IsMechanoid && p.Faction == AncientCorpsUltility.Corps).ToList();
            for (int i = 0; i < m.Count; i++)
            {
                if (Rand.Chance(Props.chance))
                {
                    m[i].mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.BerserkMechanoid);
                }
                else if (Rand.Chance(0.95f))
                {
                    m[i].Kill(new DamageInfo(DamageDefOf.EMP, 1, -1, -1, null, null, null, DamageInfo.SourceCategory.ThingOrUnknown));
                }
            }
        }
    }
    public class CompProperties_UseEffect_DisableMechs : CompProperties_UseEffect
    {
        public float chance = 0.5f;
        public CompProperties_UseEffect_DisableMechs()
        {
            compClass = typeof(CompUseEffect_DisableMechs);
        }
    } 
}