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
        public static FactionDef DMS_AncientCorps;
        public static JobDef DMS_EjectDeactivatedMech;
        public static JobDef DMS_HackDeactivatedMech;
        public static PawnKindDef DMS_Mech_Krepost;
    }
}