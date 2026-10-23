using System.Collections.Generic;
using System.Linq;
using AncientCorps;
using Fortified;
using RimWorld;
using RimWorld.Planet;
using Verse;
using static DMSAC.Acceptance.Fixture;

namespace DMSAC.Acceptance
{
    public static class Tests_Readiness
    {
        private const int Day = GenDate.TicksPerDay;

        /// <summary>R0: the DEFCON def, company defs, patrol comps, legacy components and text keys are wired up.</summary>
        public static IEnumerable<Wait> R0_Wiring(TestContext t)
        {
            ReadinessDef def = Defcon;
            t.Require(def != null, "DMSAC_Defcon exists");
            t.Require(Corps != null, "the corps faction exists");
            t.Check(def.levels.Count == 6, "six levels (0..V)");
            t.Check(def.Worker is ReadinessWorker_Corps, "worker is ReadinessWorker_Corps");
            t.Check(!def.ConfigErrors().Any(), "no config errors on DMSAC_Defcon: " + string.Join("; ", def.ConfigErrors()));
            t.Check(def.huntdown == Huntdown && def.huntdownMinLevel == 5, "huntdown linked at level V");
            t.Check(def.companyWorldObject?.defName == "DMSAC_Garrison", "companies use DMSAC_Garrison");
            t.Check(def.companyWorldObject != null && typeof(Company).IsAssignableFrom(def.companyWorldObject.worldObjectClass),
                "DMSAC_Garrison is an AncientCorps.Company (FFF_Company)");
            t.Check(def.companyWorldObject?.GetModExtension<ModExtension_CompanyWorldObject>()?.sitePart?.defName == "DMSAC_GarrisonSite",
                "camp site part comes from ModExtension_CompanyWorldObject");

            List<CompanyDef> companies = CorpsCompanyDefs.ToList();
            t.Check(companies.Count >= 5, $"corps company defs found ({companies.Count})");
            foreach (CompanyDef c in companies)
            {
                List<string> errors = c.ConfigErrors().ToList();
                t.Check(errors.Count == 0, $"{c.defName}: no config errors {string.Join("; ", errors)}");
                t.Check(c.Worker is CompanyWorker_Corps, $"{c.defName}: worker is CompanyWorker_Corps");
                t.Check(c.NamePack != null, $"{c.defName}: has a name pack");
                t.Check(c.squads.All(s => !s.leaderKindDef.NullOrEmpty()), $"{c.defName}: every platoon has a leader");
            }
            t.Note($"lowest company level: {companies.Min(c => c.minLevel)} (no company below it; deployments at lower levels find none, as before)");

            foreach (string name in new[] { "DMSAC_Garrison", "DMSAC_Graveyard", "DMSAC_FacilityEnterance", "DMSAC_LogisticTerminal" })
            {
                WorldObjectCompProperties_ReadinessPatrol patrol = DefDatabase<WorldObjectDef>.GetNamed(name).comps.OfType<WorldObjectCompProperties_ReadinessPatrol>().FirstOrDefault();
                t.Check(patrol != null, $"{name}: has the readiness patrol comp");
                t.Check(patrol != null && patrol.letterLabel.CanTranslate() && patrol.letterText.CanTranslate(), $"{name}: patrol letter keys resolve");
                t.Check(patrol != null && !patrol.possibleGeneratePawn.NullOrEmpty(), $"{name}: patrol has units (Saray patch kept the list)");
            }

            t.Check(Current.Game.GetComponent<GameComponent_Readiness>() != null, "GameComponent_Readiness exists");
            t.Check(Current.Game.GetComponent<GameComponent_DefconLevel>() != null, "legacy GameComponent_DefconLevel still loads");
            t.Check(Current.Game.GetComponent<GameComponent_RaidCompany>() != null, "legacy GameComponent_RaidCompany still loads");

            t.Check(def.LevelLabel(3) == Tr("DMSAC_Level_Label.3"), $"level label from Keyed ({def.LevelLabel(3)})");
            t.Check(def.RiseLetterLabel == Tr("DMSAC_Level_Rise"), "rise letter label from Keyed");
            t.Check(def.WarLetterText == Tr("DMSAC_World_Declaration_Desc"), "war letter text from Keyed");
            t.Check("FFF_Readiness_AlertLabel".CanTranslate() && "FFF_Company_DescTarget".CanTranslate() && "FFF_Company_DescNoTarget".CanTranslate(), "FFF default keys load");
            HuntdownDef hunt = Huntdown;
            t.Check(hunt.AlertLabel == Tr("DMSAC_Huntdown_AlertLabel") && hunt.AlertExplanation == Tr("DMSAC_Huntdown_AlertDesc")
                && hunt.AlertLabelCritical == Tr("DMSAC_Huntdown_AlertCritical") && hunt.AlertExplanationCritical == Tr("DMSAC_Huntdown_AlertCriticalDesc")
                && hunt.LetterLabel == Tr("DMSAC_Huntdown_LetterLabel") && hunt.LetterText == Tr("DMSAC_Huntdown_LetterText"),
                $"huntdown alert and letter text resolve from Keyed, not raw keys ({hunt.AlertLabel})");

            SetLevelQuiet(2);
            t.Check(ModExtension_SiteGarrison.CurrentDefcon == 2, "site garrisons read the readiness level");
            yield return new WaitFrames(1);
        }

        /// <summary>R1: rising, holding at the top, dropping, clamping and the per-level timers.</summary>
        public static IEnumerable<Wait> R1_Levels(TestContext t)
        {
            ReadinessDef def = Defcon;
            int letters = LetterCount;
            ReadinessUtility.Raise(def, 1, "test");
            int now = Find.TickManager.TicksGame;
            t.Check(State.level == 1, "Raise: 0 → 1");
            Letter rise = LettersSince(letters).FirstOrDefault(l => LabelIs(l, "DMSAC_Level_Rise"));
            t.Check(rise != null, "rise letter sent");
            t.Check(rise != null && rise.def == LetterDefOf.NegativeEvent, "level I letter is a negative event");
            t.Check(rise is ChoiceLetter cl && cl.Text.RawText.Contains(Corps.Name), "rise letter names the corps and carries the level text");
            int drop = State.nextDropTick - now;
            t.Check(drop >= 5 * Day - 1 && drop <= 10 * Day + 1, $"level I drops in 5~10 days ({drop.ToStringTicksToDays()})");
            int act = State.nextActionTick - now;
            t.Check(act >= 4 * Day - 1 && act <= 8 * Day + 1, $"level I acts in 4~8 days ({act.ToStringTicksToDays()})");

            ReadinessUtility.SetLevel(def, 4, "test");
            Letter four = Find.LetterStack.LettersListForReading.LastOrDefault(l => LabelIs(l, "DMSAC_Level_Rise"));
            t.Check(four != null && four.def == LetterDefOf.ThreatSmall, "level IV letter is a small threat");

            ReadinessUtility.SetLevel(def, 5, "test");
            Letter five = Find.LetterStack.LettersListForReading.LastOrDefault(l => LabelIs(l, "DMSAC_Level_Rise"));
            t.Check(five != null && five.def == LetterDefOf.ThreatBig, "level V letter is a big threat");
            t.Check(State.peakLevel == 5, "peak level tracked");

            letters = LetterCount;
            State.nextDropTick = Find.TickManager.TicksGame + 10;
            ReadinessUtility.Raise(def, 1, "test");
            t.Check(State.level == 5, "Raise at the top stays at V");
            t.Check(!LettersSince(letters).Any(l => LabelIs(l, "DMSAC_Level_Rise")), "no letter for a raise at the top");
            t.Check(State.nextDropTick - Find.TickManager.TicksGame >= 7.5f * Day - 1, "raise at the top re-rolls the drop timer (7.5~15 days)");

            letters = LetterCount;
            ReadinessUtility.Drop(def, 1, "test");
            t.Check(State.level == 4, "Drop: V → IV");
            Letter down = LettersSince(letters).FirstOrDefault(l => LabelIs(l, "DMSAC_Level_Down"));
            t.Check(down != null && down.def == LetterDefOf.PositiveEvent, "drop letter is a positive event");

            ReadinessUtility.SetLevel(def, -3, "test");
            t.Check(State.level == 0, "SetLevel clamps below 0");
            t.Check(State.nextDropTick == -1, "level 0 never drops");
            ReadinessUtility.SetLevel(def, 99, "test");
            t.Check(State.level == 5, "SetLevel clamps above V");

            // 讓計時器跑起來：到期就降一級。Let the timer run: it drops one level when due.
            ReadinessUtility.SetLevel(def, 2, "test");
            State.nextActionTick = -1;
            State.nextDropTick = Find.TickManager.TicksGame + 300;
            var waitDrop = new WaitUntil(() => State.level == 1, 2000);
            yield return waitDrop;
            t.Check(!waitDrop.TimedOut, "a due drop timer lowers the level on its own");
            Freeze();
        }

        /// <summary>R2: pressure accumulates, rises at 1, carries the remainder, clears on a drop and at the top.</summary>
        public static IEnumerable<Wait> R2_Pressure(TestContext t)
        {
            ReadinessDef def = Defcon;
            ReadinessUtility.AddPressure(def, 0.3f, "test");
            t.Check(State.level == 0 && UnityEngine.Mathf.Abs(State.pressure - 0.3f) < 0.001f, "0.3 pressure: no rise yet");
            ReadinessUtility.AddPressure(def, 0.8f, "test");
            t.Check(State.level == 1, "1.1 pressure: rises one level");
            t.Check(UnityEngine.Mathf.Abs(State.pressure - 0.1f) < 0.001f, $"remainder kept ({State.pressure:0.000})");
            ReadinessUtility.AddPressure(def, 2.2f, "test");
            t.Check(State.level == 3, "2.3 pressure: rises two levels at once");
            ReadinessUtility.Drop(def, 1, "test");
            t.Check(State.pressure == 0f, "a drop clears pressure");
            SetLevelQuiet(5);
            ReadinessUtility.AddPressure(def, 3f, "test");
            t.Check(State.level == 5 && State.pressure == 0f, "no pressure piles up at the top");
            Freeze();
            yield return new WaitFrames(1);
        }

        /// <summary>R3: built-in actions and the action-table rules (chance, exclusive, requireWar, requireHostileToPlayer, level change ends the tick).</summary>
        public static IEnumerable<Wait> R3_Actions(TestContext t)
        {
            ReadinessDef def = Defcon;
            t.Check(new ReadinessAction_RaiseLevel().TryRun(def, State) && State.level == 1, "RaiseLevel action");
            t.Check(new ReadinessAction_DropLevel().TryRun(def, State) && State.level == 0, "DropLevel action");
            t.Check(!new ReadinessAction_RaiseLevel { chance = 0f }.TryRun(def, State) && State.level == 0, "chance 0 never runs");
            Freeze();

            QuestScriptDef questDef = DefDatabase<QuestScriptDef>.GetNamed("DMSAC_Installation_Logistic");
            int questCount = Find.QuestManager.QuestsListForReading.Count;
            List<Site> sitesBefore = Find.WorldObjects.Sites.ToList();
            var give = new ReadinessAction_GiveQuest { quest = questDef };
            t.Check(give.TryRun(def, State), "GiveQuest offers the logistic quest");
            List<Quest> made = Find.QuestManager.QuestsListForReading.Skip(questCount).ToList();
            made.ForEach(Track);
            t.Check(made.Any(q => q.root == questDef), "the new quest is DMSAC_Installation_Logistic");
            foreach (Site s in Find.WorldObjects.Sites.Except(sitesBefore).ToList()) { Track(s); t.Check(!IsLandmark(s.Tile), "the quest site is not on a landmark"); }
            t.Check(!give.TryRun(def, State) && Find.QuestManager.QuestsListForReading.Count == questCount + made.Count, "skipIfActive: no second copy while one is pending");

            int corpsSettlements = Find.WorldObjects.SettlementBases.Count(s => s.Faction == Corps);
            t.Require(HostileSettlements().Count > 0, "a settlement hostile to the corps exists");
            t.Check(new ReadinessAction_Takeover().TryRun(def, State), "Takeover action ran");
            t.Check(Find.WorldObjects.SettlementBases.Count(s => s.Faction == Corps) == corpsSettlements + 1, "the corps gained a settlement");

            t.Check(!new ReadinessAction_CompanyOrders { requireWar = true }.TryRun(def, State), "requireWar blocks at peace");
            SetCorpsRelation(FactionRelationKind.Neutral);
            t.Check(!new ReadinessAction_CompanyRaid().TryRun(def, State), "CompanyRaid needs the corps hostile to the player");

            // 連隊編制最低從 1 級起；在 V 級部署，之後再回到要測的等級。Company defs start above 0; deploy at V, then go back.
            List<Settlement> targets = HostileSettlements();
            SetLevelQuiet(5);
            CompanyUtility.Deploy(def, State, targets[0]);
            CompanyUtility.Deploy(def, State, targets[targets.Count > 1 ? 1 : 0]);
            t.Check(CompanyUtility.CompaniesOf(Corps).Count == 2, "two companies deployed");
            t.Check(new ReadinessAction_DisbandCompanies().TryRun(def, State) && CompanyUtility.CompaniesOf(Corps).Count == 0, "DisbandCompanies removes all");
            SetLevelQuiet(0);

            // 等級改變會結束這次行動：0 級的「升級 → 解散」只會升級。A level change ends the tick.
            SetLevelQuiet(5);
            CompanyUtility.Deploy(def, State, targets[0]);
            SetLevelQuiet(0);
            ReadinessLevel level0 = def.levels[0];
            List<ReadinessAction> old0 = level0.actions;
            Override(() => level0.actions = new List<ReadinessAction> { new ReadinessAction_RaiseLevel(), new ReadinessAction_DisbandCompanies() },
                () => level0.actions = old0);
            def.Worker.RunActions(State);
            t.Check(State.level == 1 && CompanyUtility.CompaniesOf(Corps).Count == 1, "a level change ends the action tick (no disband after the raise)");
            Freeze();

            ReadinessLevel level1 = def.levels[1];
            List<ReadinessAction> old1 = level1.actions;
            Override(() => level1.actions = new List<ReadinessAction> { new ReadinessAction_DisbandCompanies { exclusive = true }, new ReadinessAction_RaiseLevel() },
                () => level1.actions = old1);
            def.Worker.RunActions(State);
            t.Check(State.level == 1 && CompanyUtility.CompaniesOf(Corps).Count == 0, "an exclusive action that fires ends the tick");
            Freeze();
            yield return new WaitFrames(1);
        }

        /// <summary>R11: the right-hand alert shows level, faction, drop countdown, pressure and the war target.</summary>
        public static IEnumerable<Wait> R11_Alert(TestContext t)
        {
            Alert_FFF_Readiness alert = new Alert_FFF_Readiness();
            t.Check(!alert.GetReport().active, "inactive at level 0");
            ReadinessUtility.SetLevel(Defcon, 3, "test");
            State.nextActionTick = -1;
            t.Check(alert.GetReport().active, "active at level III");
            string label = alert.GetLabel();
            t.Check(label.Contains(Tr("DMSAC_Level_Label.3")) && label.Contains(Corps.Name), $"label shows faction and level ({label})");
            string dropPrefix = Tr("FFF_Readiness_DropIn").Split('{')[0];
            string explanation = alert.GetExplanation().RawText;
            t.Check(explanation.Contains(dropPrefix), "explanation shows the drop countdown");
            State.pressure = 0.5f;
            t.Check(alert.GetExplanation().RawText.Contains("50%"), "explanation shows pressure progress");
            Faction foe = HostileSettlements().First().Faction;
            State.warTarget = foe;
            t.Check(alert.GetExplanation().RawText.Contains(foe.Name), "explanation names the war target");
            Freeze();
            yield return new WaitFrames(1);
        }

        /// <summary>R13: the legacy DEFCON component moves an old save's level and war target into readiness once.</summary>
        public static IEnumerable<Wait> R13_LegacyMigration(TestContext t)
        {
            GameComponent_DefconLevel legacy = Current.Game.GetComponent<GameComponent_DefconLevel>();
            t.Require(legacy != null, "legacy component present");
            Faction foe = HostileSettlements().First().Faction;
            SetField(legacy, "migrated", false);
            SetField(legacy, "level", 4);
            SetField(legacy, "historicalLevel", 5);
            SetField(legacy, "targetedFaction", foe);
            legacy.FinalizeInit();
            t.Check(State.level == 4, "level 4 migrated");
            t.Check(State.peakLevel == 5, "historical level becomes the peak");
            t.Check(State.warTarget == foe, "war target migrated");
            t.Check(Field<bool>(legacy, "migrated"), "marked as migrated");
            t.Check(legacy.Level == 4, "legacy Level forwards to readiness");
            SetLevelQuiet(1);
            legacy.FinalizeInit();
            t.Check(State.level == 1, "a second FinalizeInit does not migrate again");
            Freeze();
            yield return new WaitFrames(1);
        }
    }
}
