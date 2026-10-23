using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace DMSAC.Acceptance
{
    // ======================================================================
    //  Waits (yielded by test scripts) — same scheme as DMSE.BVRAcceptance
    // ======================================================================

    public abstract class Wait
    {
        public abstract bool Done { get; }
    }

    public sealed class WaitTicks : Wait
    {
        private readonly int until;
        public WaitTicks(int ticks) { until = Find.TickManager.TicksGame + Math.Max(1, ticks); }
        public override bool Done => Find.TickManager.TicksGame >= until;
    }

    public sealed class WaitFrames : Wait
    {
        private readonly int until;
        public WaitFrames(int frames) { until = Time.frameCount + Math.Max(1, frames); }
        public override bool Done => Time.frameCount >= until;
    }

    /// <summary>Waits until a condition holds or a tick timeout passes; check <see cref="TimedOut"/> afterwards.</summary>
    public sealed class WaitUntil : Wait
    {
        private readonly Func<bool> condition;
        private readonly int timeoutTick;
        public bool TimedOut { get; private set; }
        public WaitUntil(Func<bool> condition, int timeoutTicks)
        {
            this.condition = condition;
            timeoutTick = Find.TickManager.TicksGame + timeoutTicks;
        }
        public override bool Done
        {
            get
            {
                if (condition()) { return true; }
                if (Find.TickManager.TicksGame >= timeoutTick) { TimedOut = true; return true; }
                return false;
            }
        }
    }

    /// <summary>Aborts the current test with a failure.</summary>
    public sealed class AcceptanceAbort : Exception
    {
        public AcceptanceAbort(string message) : base(message) { }
    }

    // ======================================================================
    //  Test definition / context
    // ======================================================================

    public sealed class AcceptanceTest
    {
        public string id;
        public string title;
        public Func<TestContext, IEnumerable<Wait>> body;
        public AcceptanceTest(string id, string title, Func<TestContext, IEnumerable<Wait>> body)
        {
            this.id = id; this.title = title; this.body = body;
        }
    }

    public sealed class TestContext
    {
        public readonly AcceptanceTest test;
        public readonly List<string> failures = new List<string>();
        public readonly List<string> notes = new List<string>();
        public int checks;

        public TestContext(AcceptanceTest test) { this.test = test; }

        public bool Check(bool condition, string what)
        {
            checks++;
            if (!condition) { failures.Add(what); AcceptanceDriver.Log($"    FAIL: {what}"); }
            else { AcceptanceDriver.Log($"    ok:   {what}"); }
            return condition;
        }

        public void Require(bool condition, string what)
        {
            if (!Check(condition, what)) { throw new AcceptanceAbort(what); }
        }

        public void Note(string text)
        {
            notes.Add(text);
            AcceptanceDriver.Log($"    note: {text}");
        }
    }

    // ======================================================================
    //  GameComponent hook (auto-instantiated by RimWorld for every game)
    // ======================================================================

    public class GameComponent_DMSACAcceptance : GameComponent
    {
        public GameComponent_DMSACAcceptance(Game game) { }

        public override void GameComponentUpdate()
        {
            AcceptanceDriver.Update();
        }
    }

    // ======================================================================
    //  Driver
    // ======================================================================

    public static class AcceptanceDriver
    {
        public const string ArgKey = "dmsac-acceptance";
        /// <summary>Optional comma-separated test ids to run (e.g. <c>-dmsac-only=R4,R9</c>).</summary>
        public const string OnlyArgKey = "dmsac-only";

        private static bool? enabled;
        private static string outDir;
        private static StreamWriter writer;

        private static List<AcceptanceTest> tests;
        private static int index = -1;
        private static TestContext ctx;
        private static IEnumerator<Wait> routine;
        private static Wait wait;
        private static Game lastGame;
        private static int settleFrames;
        private static bool finished;
        private static float quitAt = -1f;
        private static readonly List<(string id, string title, bool pass, string detail)> results
            = new List<(string, string, bool, string)>();

        public static bool Enabled
        {
            get
            {
                if (enabled == null)
                {
                    enabled = GenCommandLine.TryGetCommandLineArg(ArgKey, out outDir) && !outDir.NullOrEmpty();
                }
                return enabled.Value;
            }
        }

        public static string OutDir => outDir;

        public static void Log(string line)
        {
            string stamped = Current.Game != null && Find.TickManager != null
                ? $"[t={Find.TickManager.TicksGame,7}] {line}" : line;
            Verse.Log.Message("[DMSAC-ACCEPT] " + stamped);
            try
            {
                if (writer == null)
                {
                    Directory.CreateDirectory(outDir);
                    writer = new StreamWriter(Path.Combine(outDir, "dmsac_acceptance_log.txt"), append: false, Encoding.UTF8);
                    writer.AutoFlush = true;
                }
                writer.WriteLine(stamped);
            }
            catch (Exception) { }
        }

        public static void Update()
        {
            if (!Enabled || finished)
            {
                if (finished && quitAt > 0f && Time.realtimeSinceStartup >= quitAt)
                {
                    quitAt = -1f;
                    Root.Shutdown();
                }
                return;
            }
            if (Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null) { return; }
            if (LongEventHandler.AnyEventNowOrWaiting) { return; }

            if (Current.Game != lastGame)
            {
                lastGame = Current.Game;
                settleFrames = 120;
                Log($"Game ready (map {Find.CurrentMap.uniqueID}, tile {Find.CurrentMap.Tile}).");
                if (tests == null)
                {
                    Fixture.PrepareGameOnce();
                    tests = AcceptanceSuite.Build();
                    if (GenCommandLine.TryGetCommandLineArg(OnlyArgKey, out string only) && !only.NullOrEmpty())
                    {
                        HashSet<string> ids = new HashSet<string>(only.Split(',').Select(s => s.Trim()));
                        tests = tests.Where(x => ids.Contains(x.id)).ToList();
                    }
                    Log($"Running {tests.Count} acceptance tests.");
                }
                else
                {
                    // A test reloaded the game; let it continue from where it left off.
                    Fixture.OnGameReloaded();
                }
            }
            if (settleFrames > 0) { settleFrames--; return; }

            CloseBlockingWindows();
            if (Find.TickManager.CurTimeSpeed != TimeSpeed.Ultrafast) { Find.TickManager.CurTimeSpeed = TimeSpeed.Ultrafast; }

            if (wait != null && !wait.Done) { return; }
            wait = null;

            if (routine == null)
            {
                index++;
                if (index >= tests.Count) { Finish(); return; }
                AcceptanceTest test = tests[index];
                ctx = new TestContext(test);
                Log("");
                Log($"=== {test.id}: {test.title}");
                try
                {
                    Fixture.BeforeTest();
                    routine = test.body(ctx).GetEnumerator();
                }
                catch (Exception ex)
                {
                    EndTest(ex);
                    return;
                }
            }

            try
            {
                if (routine.MoveNext()) { wait = routine.Current; }
                else { EndTest(null); }
            }
            catch (Exception ex)
            {
                EndTest(ex);
            }
        }

        private static void EndTest(Exception ex)
        {
            if (ex is AcceptanceAbort abort)
            {
                Log($"    ABORT: {abort.Message}");
            }
            else if (ex != null)
            {
                ctx.failures.Add("exception: " + ex.GetType().Name + ": " + ex.Message);
                Log($"    EXCEPTION: {ex}");
            }
            // 每個測試結束時，期間出現的任何錯誤記錄都算失敗。Any error logged during the test fails it.
            foreach (string error in Fixture.NewErrors())
            {
                ctx.failures.Add("log error: " + error);
                Log($"    LOG ERROR: {error}");
            }
            if (ctx.checks == 0 && ex == null) { ctx.failures.Add("no checks ran"); }
            bool pass = ctx.failures.Count == 0;
            string detail = pass ? $"{ctx.checks} checks" : string.Join(" | ", ctx.failures);
            results.Add((ctx.test.id, ctx.test.title, pass, detail));
            Log($"--- {ctx.test.id}: {(pass ? "PASS" : "FAIL")} ({detail})");

            try { Fixture.CleanupAfterTest(); }
            catch (Exception cleanupEx) { Log($"    cleanup exception: {cleanupEx}"); }

            routine = null;
            wait = null;
            ctx = null;
        }

        private static void Finish()
        {
            finished = true;
            int passed = results.Count(r => r.pass);
            Log("");
            Log($"=== SUMMARY: {passed}/{results.Count} passed");
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("id\tresult\ttitle\tdetail");
            foreach (var r in results)
            {
                Log($"{(r.pass ? "PASS" : "FAIL")}  {r.id}  {r.title}  -- {r.detail}");
                sb.AppendLine($"{r.id}\t{(r.pass ? "PASS" : "FAIL")}\t{r.title}\t{r.detail}");
            }
            try
            {
                File.WriteAllText(Path.Combine(outDir, "dmsac_acceptance_results.tsv"), sb.ToString(), Encoding.UTF8);
                File.WriteAllText(Path.Combine(outDir, "dmsac_acceptance_done.txt"), DateTime.Now.ToString("o"));
            }
            catch (Exception ex) { Log("could not write results: " + ex); }
            Fixture.DeleteTestSave();
            quitAt = Time.realtimeSinceStartup + 3f;
        }

        /// <summary>Close dialogs that would block input (scenario intro, mod popups); keep everything else.</summary>
        private static void CloseBlockingWindows()
        {
            foreach (Window w in Find.WindowStack.Windows.ToList())
            {
                if (w.layer == WindowLayer.Dialog || w.layer == WindowLayer.SubSuper || w.layer == WindowLayer.Super)
                {
                    if (w is Dialog_MessageBox || w is Dialog_NodeTree || w.forcePause || w.absorbInputAroundWindow)
                    {
                        Log($"closing window {w.GetType().Name}");
                        Find.WindowStack.TryRemove(w, doCloseSound: false);
                    }
                }
            }
        }
    }
}
