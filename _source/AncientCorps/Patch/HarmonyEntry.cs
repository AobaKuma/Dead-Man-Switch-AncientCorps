using System;
using HarmonyLib;
using Verse;

namespace AncientCorps
{
    [StaticConstructorOnStartup]
    public static class HarmonyEntry
    {
        private const string HarmonyId = "AOBA.TheDeadManSwitch.AncientCorps";

        static HarmonyEntry()
        {
            Harmony harmony = new Harmony(HarmonyId);
            // 逐類別套用：Harmony 3 只要一個補丁類別失敗，PatchAll 就會中止，排在後面的補丁全部不會套用。
            // Apply per class: on Harmony 3 a single failing class aborts PatchAll and silently skips the rest.
            int patched = 0;
            foreach (Type type in typeof(HarmonyEntry).Assembly.GetTypes())
            {
                if (type.GetCustomAttributes(typeof(HarmonyPatch), inherit: false).Length == 0) continue;
                try
                {
                    harmony.CreateClassProcessor(type).Patch();
                    patched++;
                }
                catch (Exception e)
                {
                    Log.Error($"[DMSAC] Harmony patch failed on {type.FullName}: {e}");
                }
            }
            Log.Message($"[DMSAC] Harmony: applied {patched} patch class(es).");
        }
    }
}
