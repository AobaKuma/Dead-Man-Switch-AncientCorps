using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AncientCorps;
using Fortified;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace DMSAC.Acceptance
{
    /// <summary>
    /// Shared setup for the corps readiness tests: the DEFCON def and state, the corps faction, a quiet storyteller,
    /// timers frozen between steps, error capture and cleanup of everything a test spawned.
    /// </summary>
    public static class Fixture
    {
        public const string TestSaveName = "DMSAC_Acceptance";

        public static ReadinessDef Defcon => DefDatabase<ReadinessDef>.GetNamed("DMSAC_Defcon");
        public static ReadinessState State => ReadinessUtility.StateOf(Defcon);
        public static ReadinessWorker Worker => Defcon.Worker;
        public static Faction Corps => Find.FactionManager.FirstFactionOfDef(FactionDef.Named("DMS_AncientCorps"));
        public static Map Home => Find.Maps.FirstOrDefault(m => m.IsPlayerHome) ?? Find.CurrentMap;
        public static HuntdownDef Huntdown => DefDatabase<HuntdownDef>.GetNamed("DMSAC_Huntdown");

        public static IEnumerable<CompanyDef> CorpsCompanyDefs =>
            DefDatabase<CompanyDef>.AllDefsListForReading.Where(c => c.defaultFaction == Corps?.def);

        private static readonly List<WorldObject> worldObjects = new List<WorldObject>();
        private static readonly List<Thing> things = new List<Thing>();
        private static readonly List<Quest> quests = new List<Quest>();
        private static readonly List<Action> restoreActions = new List<Action>();
        private static HashSet<LogMessage> knownMessages = new HashSet<LogMessage>();
        private static FactionRelationKind corpsRelation;

        // ==================================================================
        //  Lifecycle
        // ==================================================================

        public static void PrepareGameOnce()
        {
            Find.Storyteller.storytellerComps.Clear();
            Faction corps = Corps;
            AcceptanceDriver.Log($"Corps faction: {corps?.Name ?? "NONE"}; readiness def {Defcon?.defName ?? "NONE"}.");
            corpsRelation = corps != null ? corps.RelationKindWith(Faction.OfPlayer) : FactionRelationKind.Neutral;
        }

        public static void OnGameReloaded()
        {
            worldObjects.Clear();
            things.Clear();
            quests.Clear();
            restoreActions.Clear();
            Find.Storyteller.storytellerComps.Clear();
            // 不重拍日誌快照：讀檔時的錯誤要算進正在跑的測試。No new log snapshot: load errors count against the running test.
        }

        public static void BeforeTest()
        {
            FactionHostilityLock.Unlock(Corps);
            ResetReadiness();
            SnapshotLog();
        }

        public static void CleanupAfterTest()
        {
            for (int i = restoreActions.Count - 1; i >= 0; i--) { restoreActions[i](); }
            restoreActions.Clear();

            foreach (Quest quest in quests)
            {
                if (quest.State == QuestState.NotYetAccepted || quest.State == QuestState.Ongoing) { quest.End(QuestEndOutcome.Unknown, sendLetter: false); }
                Find.QuestManager.Remove(quest);
            }
            quests.Clear();

            foreach (Thing t in things) { if (!t.Destroyed) { t.Destroy(DestroyMode.Vanish); } }
            things.Clear();

            // Corps pawns (raids, patrols) left on the home map, and their lords.
            Map home = Home;
            foreach (Pawn p in home.mapPawns.AllPawnsSpawned.Where(p => p.Faction == Corps).ToList()) { p.Destroy(DestroyMode.Vanish); }
            foreach (Corpse c in home.listerThings.ThingsInGroup(ThingRequestGroup.Corpse).OfType<Corpse>().Where(c => c.InnerPawn?.Faction == Corps).ToList()) { c.Destroy(DestroyMode.Vanish); }
            foreach (Verse.AI.Group.Lord lord in home.lordManager.lords.Where(l => l.faction == Corps).ToList()) { home.lordManager.RemoveLord(lord); }

            foreach (WorldObject wo in worldObjects) { DestroyWorldObject(wo); }
            worldObjects.Clear();
            foreach (FFF_Company company in CompanyUtility.CompaniesOf(Corps)) { DestroyWorldObject(company); }

            if (HuntdownUtility.IsActive(Huntdown)) { HuntdownUtility.Stop(Huntdown); }
            FactionHostilityLock.Unlock(Corps);
            ResetReadiness();
            SetCorpsRelation(corpsRelation);
            if (Find.CurrentMap != home) { Current.Game.CurrentMap = home; }
            CameraJumper.TryHideWorld();
        }

        private static void DestroyWorldObject(WorldObject wo)
        {
            if (wo == null || wo.Destroyed) { return; }
            if (wo is MapParent mp && mp.HasMap) { Current.Game.DeinitAndRemoveMap(mp.Map, false); }
            if (!wo.Destroyed) { wo.Destroy(); }
        }

        public static void DeleteTestSave()
        {
            try
            {
                string path = GenFilePaths.FilePathForSavedGame(TestSaveName);
                if (File.Exists(path)) { File.Delete(path); }
            }
            catch (Exception) { }
        }

        // ==================================================================
        //  Readiness helpers
        // ==================================================================

        /// <summary>Back to level 0, no pressure, no war, timers frozen.</summary>
        public static void ResetReadiness()
        {
            ReadinessState state = State;
            if (state == null) { return; }
            state.level = 0;
            state.peakLevel = 0;
            state.pressure = 0f;
            state.warTarget = null;
            Freeze();
        }

        /// <summary>
        /// Stops the readiness component from dropping or acting on its own between test steps. Level changes reschedule
        /// both timers, so tests call this again after changing the level when they wait for ticks.
        /// </summary>
        public static void Freeze()
        {
            ReadinessState state = State;
            if (state == null) { return; }
            state.nextDropTick = -1;
            state.nextActionTick = -1;
        }

        /// <summary>Sets the level without letters or side effects (direct state write) and freezes the timers.</summary>
        public static void SetLevelQuiet(int level)
        {
            State.level = level;
            Freeze();
        }

        /// <summary>The corps uses goodwill, so the relation is set through goodwill (-100 hostile, 0 neutral, 100 ally).</summary>
        public static void SetCorpsRelation(FactionRelationKind kind)
        {
            Faction corps = Corps;
            if (corps == null) { return; }
            // 測試需要自由切換關係，先解除永久敵對鎖。Tests switch relations freely; lift the hostility lock first.
            FactionHostilityLock.Unlock(corps);
            if (corps.RelationKindWith(Faction.OfPlayer) == kind) { return; }
            int target = kind == FactionRelationKind.Hostile ? -100 : kind == FactionRelationKind.Ally ? 100 : 0;
            corps.TryAffectGoodwillWith(Faction.OfPlayer, target - corps.GoodwillWith(Faction.OfPlayer), canSendMessage: false, canSendHostilityLetter: false);
        }

        /// <summary>Applies a temporary change and registers how to undo it at cleanup.</summary>
        public static void Override(Action apply, Action restore)
        {
            apply();
            restoreActions.Add(restore);
        }

        /// <summary>Sets the order odds of every corps company def for this test.</summary>
        public static void ForceOrders(float hold = 0f, float takeover = 0f, float split = 0f, float raze = 0f, float idleDisband = 0f)
        {
            foreach (CompanyDef def in CorpsCompanyDefs.ToList())
            {
                float h = def.holdChance, t = def.takeoverChance, s = def.splitChance, r = def.razeChance, d = def.idleDisbandChance;
                Override(() =>
                {
                    def.holdChance = hold; def.takeoverChance = takeover; def.splitChance = split; def.razeChance = raze; def.idleDisbandChance = idleDisband;
                }, () =>
                {
                    def.holdChance = h; def.takeoverChance = t; def.splitChance = s; def.razeChance = r; def.idleDisbandChance = d;
                });
            }
        }

        // ==================================================================
        //  World helpers
        // ==================================================================

        public static void Track(WorldObject wo) { if (wo != null) { worldObjects.Add(wo); } }
        public static void Track(Thing t) { if (t != null) { things.Add(t); } }
        public static void Track(Quest q) { if (q != null) { quests.Add(q); } }

        /// <summary>Settlements of factions hostile to the corps (not the player), humanlike first.</summary>
        public static List<Settlement> HostileSettlements()
        {
            Faction corps = Corps;
            return Find.WorldObjects.SettlementBases
                .Where(s => s.Faction != null && s.Faction != corps && !s.Faction.IsPlayer && s.Faction.HostileTo(corps))
                .OrderByDescending(s => s.Faction.def.humanlikeFaction)
                .ToList();
        }

        public static bool IsLandmark(PlanetTile tile) => FFF_SiteTileUtility.IsLandmarkTile(tile);

        public static float Distance(PlanetTile a, PlanetTile b) => Find.WorldGrid.ApproxDistanceInTiles(a, b);

        /// <summary>A free, passable, non-landmark land tile 6..30 tiles from home.</summary>
        public static PlanetTile FreeTile()
        {
            PlanetTile home = Home.Tile;
            List<PlanetTile> tiles = CompanyUtility.NearbyTiles(home, 30)
                .Where(t => Distance(t, home) >= 6f && !Find.World.Impassable(t) && !Find.WorldObjects.AnyWorldObjectAt(t)
                    && !IsLandmark(t) && t.Tile.PrimaryBiome.canBuildBase)
                .ToList();
            return tiles.RandomElementWithFallback(PlanetTile.Invalid);
        }

        // ==================================================================
        //  Letters, log and reflection
        // ==================================================================

        public static int LetterCount => Find.LetterStack.LettersListForReading.Count;

        public static List<Letter> LettersSince(int count) => Find.LetterStack.LettersListForReading.Skip(count).ToList();

        public static string Tr(string key) => key.Translate().RawText;

        /// <summary>Letter labels are capitalised by the letter stack; compare without case.</summary>
        public static bool LabelIs(Letter letter, string key) => string.Equals(letter.Label.RawText, Tr(key), System.StringComparison.OrdinalIgnoreCase);

        public static void SnapshotLog()
        {
            knownMessages = new HashSet<LogMessage>(Log.Messages);
        }

        /// <summary>Errors logged since the last snapshot (warnings are ignored).</summary>
        public static List<string> NewErrors()
        {
            List<string> errors = Log.Messages
                .Where(m => !knownMessages.Contains(m) && m.type == LogMessageType.Error)
                .Select(m => m.text.Split('\n')[0])
                .ToList();
            SnapshotLog();
            return errors;
        }

        public static T Field<T>(object obj, string name)
        {
            FieldInfo f = obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return (T)f.GetValue(obj);
        }

        public static void SetField(object obj, string name, object value)
        {
            FieldInfo f = obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            f.SetValue(obj, value);
        }
    }
}
