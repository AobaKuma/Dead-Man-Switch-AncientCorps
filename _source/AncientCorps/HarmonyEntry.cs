using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Verse;
using UnityEngine;

using HarmonyLib;

namespace AncientCorps
{
    [StaticConstructorOnStartup]
    public static class HarmonyEntry
    {
        static HarmonyEntry()
        {
            Harmony entry = new Harmony("AOBA.TheDeadManSwitch.AncientCorps");
            entry.PatchAll();
        }
    }
}