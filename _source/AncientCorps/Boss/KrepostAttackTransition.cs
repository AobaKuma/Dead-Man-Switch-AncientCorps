using RimWorld;
using System.Linq;
using Verse;

namespace AncientCorps
{
    public class KrepostAttackTransition : MusicTransition
    {
        public override bool IsTransitionSatisfied()
        {
            if (!base.IsTransitionSatisfied())
            {
                return false;
            }
            if (!Find.CurrentMap.mapPawns.SpawnedPawnsInFaction(Faction.OfMechanoids).NullOrEmpty())
            { 
                return Find.CurrentMap.mapPawns.SpawnedPawnsInFaction(Faction.OfMechanoids).FirstOrDefault(p => p.kindDef.defName == "DMS_Mech_Krepost") != null;
            }
            Faction faction = Find.FactionManager.FirstFactionOfDef(DMSAC_DefOf.DMS_AncientCorps);
            if (faction == null || !faction.HostileTo(Faction.OfPlayer)) return false;
            var pawns = Find.CurrentMap.mapPawns.SpawnedPawnsInFaction(faction);
            if(pawns.NullOrEmpty()) return false;

            return pawns.FirstOrDefault(p => p.kindDef.defName == "DMS_Mech_Krepost") != null;
        }
    }
}