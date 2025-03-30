using RimWorld;

using HarmonyLib;
using System;

namespace AncientCorps
{
    [HarmonyPatch(typeof(GoodwillSituationWorker_MemeCompatibility), "Applies",
        new Type[] { typeof(Faction), typeof(Faction)},
        new ArgumentType[] { ArgumentType.Normal, ArgumentType.Normal})]
    internal class Patch_GoodwillSituationWorker_MemeCompatibility
    {
        public static bool Prefix(Faction a, Faction b, ref bool __result)
        {
            if (a.def == DMS_DefOf.DMS_AncientCorps || b.def == DMS_DefOf.DMS_AncientCorps)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }
}