using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace AncientCorps
{
    /// <summary>
    /// 指揮核心的「停機指令」：對附近的古代機兵做一次判定，部分狂暴、其餘直接停機。
    /// 原本是兩次獨立的 Rand.Chance（0.5 再 0.95），會有 2.5% 什麼都不做，而且作用在
    /// Find.CurrentMap 而不是使用者所在的地圖，對友好機兵也一律擊殺。
    /// The command core's shutdown pulse: one roll per mech, on the user's map and within a radius.
    /// </summary>
    public class CompUseEffect_DisableMechs : CompUseEffect
    {
        public CompProperties_UseEffect_DisableMechs Props => (CompProperties_UseEffect_DisableMechs)props;

        public override void DoEffect(Pawn usedBy)
        {
            base.DoEffect(usedBy);
            Map map = usedBy?.MapHeld ?? Find.CurrentMap;
            if (map == null) return;
            Faction corps = AncientCorpsUltility.Corps;
            if (corps == null) return;
            float radius = Props.radius;

            List<Pawn> mechs = map.mapPawns.AllPawnsSpawned
                .Where(p => p != null
                            && p.RaceProps.IsMechanoid
                            && p.Faction == corps
                            && (radius <= 0f || usedBy == null || p.Position.InHorDistOf(usedBy.Position, radius)))
                .ToList();
            for (int i = 0; i < mechs.Count; i++)
            {
                Pawn mech = mechs[i];
                if (mech.Dead || !mech.Spawned) continue;
                if (Rand.Chance(Props.chance))
                {
                    mech.mindState?.mentalStateHandler?.TryStartMentalState(MentalStateDefOf.BerserkMechanoid);
                }
                else
                {
                    mech.Kill(new DamageInfo(DamageDefOf.EMP, 1, -1, -1, usedBy, null, null, DamageInfo.SourceCategory.ThingOrUnknown));
                }
            }
        }
    }

    public class CompProperties_UseEffect_DisableMechs : CompProperties_UseEffect
    {
        public float chance = 0.5f;
        /// <summary>作用半徑（格）。0 = 整張地圖（維持原行為）。Radius in cells; 0 means the whole map.</summary>
        public float radius = 0f;
        public CompProperties_UseEffect_DisableMechs()
        {
            compClass = typeof(CompUseEffect_DisableMechs);
        }
    }
}
