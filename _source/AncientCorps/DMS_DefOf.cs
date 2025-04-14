using RimWorld;
using Verse;

namespace AncientCorps
{
    [RimWorld.DefOf, StaticConstructorOnStartup]
    public static class DMS_DefOf
    {
        static DMS_DefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(DMS_DefOf));
        }
        public static HediffDef DMSAC_StructuralDamage;
        public static FactionDef DMS_AncientCorps;
        public static JobDef DMS_EjectDeactivatedMech;
        public static JobDef DMS_HackDeactivatedMech;
        public static JobDef DMS_ResurrectMech;
        public static PawnKindDef DMS_Mech_Krepost;
        public static PawnKindDef DMS_Mech_CommandWalker;
        public static QuestScriptDef DMSAC_OpportunitySite_LogisticTerminal;
        public static SitePartDef DMSAC_GarrisonSite;

        public static WorldObjectDef DMSAC_Garrison;
    }
}