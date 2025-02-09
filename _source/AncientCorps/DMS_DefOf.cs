using RimWorld;

namespace AncientCorps
{
    [RimWorld.DefOf]
    public static class DMS_DefOf
    {
        static DMS_DefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(DMS_DefOf));
        }
        public static FactionDef DMS_AncientCorps;
    }
}