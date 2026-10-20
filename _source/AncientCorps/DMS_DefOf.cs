using Fortified;
using RimWorld;
using Verse;
using Verse.AI;

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
        public static PawnKindDef DMS_Mech_Krepost;
        public static PawnKindDef DMS_Mech_CommandWalker;
        public static QuestScriptDef DMSAC_OpportunitySite_LogisticTerminal;
        public static SitePartDef DMSAC_GarrisonSite;
        public static WorldObjectDef DMSAC_Garrison;
        // 本模組自己的 DutyDef（Defs/DutyDefs.xml）。用 DefOf 取代 DefDatabase.GetNamed，避免 Def 缺失時擲例外。
        public static DutyDef DMS_XF_Bodyguard;
        public static DutyDef DMS_XF_ExitAndPickUpTeammates;
        // 1.7 新增：DEFCON 五級啟動的 FFF 追殺令（Defs/DMSAC_Huntdown.xml）。
        public static HuntdownDef DMSAC_Huntdown;
    }
}
