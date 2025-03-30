using RimWorld;

using HarmonyLib;

namespace AncientCorps
{
    [HarmonyPatch(typeof(GoodwillSituationWorker_SameIdeo), "GetNaturalGoodwillOffset")]
    internal class Patch_GoodwillSituationWorker_SameIdeo
    {
        public static bool Prefix(Faction other, ref int __result)
        {

            if (other.def == DMS_DefOf.DMS_AncientCorps)
            {
                __result = 0;
                return false;
            }
            return true;
        }
    }
}