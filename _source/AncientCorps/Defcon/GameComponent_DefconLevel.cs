using Fortified;
using RimWorld;
using Verse;

namespace AncientCorps
{
    /// <summary>
    /// 舊版 DEFCON 元件，只負責把舊存檔的等級與戰爭目標搬進 FFF 的戒備系統（DMSAC_Defcon），之後不再做事。
    /// 保留類別本身，是因為存檔裡記著這個元件的類別名稱，刪掉會讓讀檔報錯。
    ///
    /// Legacy DEFCON component. It only moves an old save's level and war target into FFF's readiness system
    /// (DMSAC_Defcon) and then does nothing. The class stays because saves name it; removing it breaks loading.
    /// </summary>
    public class GameComponent_DefconLevel : GameComponent
    {
        private int level = -1;
        private int historicalLevel;
        private Faction targetedFaction;
        private bool migrated;

        public GameComponent_DefconLevel(Game game) { }

        /// <summary>目前等級（轉給 FFF）。Current level, read from FFF.</summary>
        public int Level => ReadinessUtility.LevelOf(DMSAC_DefOf.DMSAC_Defcon);

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            if (migrated) return;
            migrated = true;
            if (level < 0 || DMSAC_DefOf.DMSAC_Defcon == null) return;
            ReadinessState state = ReadinessUtility.StateOf(DMSAC_DefOf.DMSAC_Defcon);
            if (state == null) return;
            state.level = UnityEngine.Mathf.Clamp(level, 0, DMSAC_DefOf.DMSAC_Defcon.MaxLevel);
            state.peakLevel = UnityEngine.Mathf.Max(state.level, historicalLevel);
            state.warTarget = targetedFaction;
            state.ScheduleDrop();
            state.ScheduleAction();
            Log.Message($"[DMSAC] Migrated DEFCON {state.level} into FFF readiness.");
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref migrated, "Migrated");
            if (migrated) return;
            Scribe_Values.Look(ref level, "Level", -1);
            Scribe_Values.Look(ref historicalLevel, "HistoricalLevel");
            Scribe_References.Look(ref targetedFaction, "TargetedFaction");
        }
    }

    /// <summary>舊版元件，保留給舊存檔讀取；功能已移到 FFF 的 CompanyUtility.RaidMap。Legacy; kept so old saves load.</summary>
    public class GameComponent_RaidCompany : GameComponent
    {
        public GameComponent_RaidCompany(Game game) { }
    }
}
