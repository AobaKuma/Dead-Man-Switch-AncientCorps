using RimWorld;
using Verse;

namespace AncientCorps
{
    [DefOf]
    internal static class DMSAC_DefOf
    {
        static DMSAC_DefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(DMSAC_DefOf));
        }
        public static HediffDef DMSAC_StructuralDamage;
        public static FactionDef DMS_AncientCorps;
        public static JobDef FFF_EjectDeactivatedMech;
        public static JobDef FFF_HackDeactivatedMech;
        public static JobDef FFF_ResurrectMech;
        public static PawnKindDef DMS_Mech_Krepost;
        public static PawnKindDef DMS_Mech_CommandWalker;
        public static QuestScriptDef DMSAC_OpportunitySite_LogisticTerminal;
        public static SitePartDef DMSAC_GarrisonSite;
        public static WorldObjectDef DMSAC_Garrison;
    }
}