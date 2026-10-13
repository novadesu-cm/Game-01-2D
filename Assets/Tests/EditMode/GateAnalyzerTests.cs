using System.Collections.Generic;
using GotA.Client;
using GotA.EditorTools;
using NUnit.Framework;

namespace GotA.Tests
{
    /// <summary>P-07: Gate report maths on rows written by the real P-06 CSV writer.</summary>
    public class GateAnalyzerTests
    {
        private static string Row(string weapon, double push, int hits, float durationSeconds,
                                  string cause = "Killed by Grunt in Wave 2.", params DecisionRecord[] decisions)
        {
            var rec = new RunLogRecord
            {
                Seed = 1,
                Weapon = weapon,
                Trait = "None",
                CauseOfDeath = cause,
                WaveResults = "W1:Cleared",
                TotalPushDistance = (float)push,
                TotalHits = hits,
                AveragePushDistance = hits > 0 ? (float)(push / hits) : 0f,
                DurationSeconds = durationSeconds,
                ContinueRate = float.NaN
            };
            rec.Decisions.AddRange(decisions);
            return RunLogCsv.ToRow(rec);
        }

        private static GateReport Analyze(params string[] rows)
        {
            var lines = new List<string> { RunLogCsv.Header() };
            lines.AddRange(rows);
            return GateAnalyzer.Analyze(lines);
        }

        private static DecisionRecord D(int wave, bool cont, float sec) => new DecisionRecord(wave, cont, sec);

        [Test]
        public void G1_HammerAtLeast4xDaggers_Passes_AndJustBelowFails()
        {
            GateReport pass = Analyze(Row("Giant Hammer", 40, 10, 90), Row("Dual Daggers", 10, 10, 90));
            Assert.AreEqual(4.0, pass.G1Ratio, 1e-3);
            Assert.AreEqual(GateStatus.Pass, pass.G1);

            GateReport fail = Analyze(Row("Giant Hammer", 39, 10, 90), Row("Dual Daggers", 10, 10, 90));
            Assert.AreEqual(GateStatus.Fail, fail.G1);
        }

        [Test]
        public void G1_MissingWeapon_IsNoData()
        {
            Assert.AreEqual(GateStatus.NoData, Analyze(Row("Giant Hammer", 40, 10, 90)).G1);
        }

        [Test]
        public void G1_PoolsByHits_NotByRun()
        {
            // Run A: 10 push / 10 hits, run B: 30 push / 10 hits -> pooled 2.0 (not mean of ratios either way here)
            GateReport r = Analyze(Row("Giant Hammer", 10, 10, 90), Row("Giant Hammer", 30, 10, 90), Row("Dual Daggers", 5, 10, 90));
            Assert.AreEqual(2.0, r.HammerAvgPush, 1e-4);
            Assert.AreEqual(0.5, r.DaggersAvgPush, 1e-4);
        }

        [Test]
        public void G3_MedianDecisionTime_OddAndEvenCounts()
        {
            GateReport odd = Analyze(Row("Fists", 1, 1, 90, "x", D(1, true, 2f), D(2, true, 4f)),
                                     Row("Fists", 1, 1, 90, "x", D(1, true, 6f)));
            Assert.AreEqual(4.0, odd.MedianDecisionSeconds, 1e-3);
            Assert.AreEqual(GateStatus.Pass, odd.G3Median);

            GateReport even = Analyze(Row("Fists", 1, 1, 90, "x", D(1, true, 1f), D(2, true, 4f)));
            Assert.AreEqual(2.5, even.MedianDecisionSeconds, 1e-3);
            Assert.AreEqual(GateStatus.Fail, even.G3Median);
        }

        [Test]
        public void G3_ContinueRateAfterWave1_UsesOnlyWave1Decisions()
        {
            GateReport r = Analyze(
                Row("Fists", 1, 1, 90, "x", D(1, true, 3f), D(2, false, 3f)),   // W2 choice must be ignored
                Row("Fists", 1, 1, 90, "x", D(1, true, 3f)),
                Row("Fists", 1, 1, 90, "x", D(1, false, 3f)));

            Assert.AreEqual(3, r.Wave1Decisions);
            Assert.AreEqual(2, r.Wave1Continues);
            Assert.AreEqual(2.0 / 3.0, r.ContinueRateAfterWave1, 1e-6);
            Assert.AreEqual(GateStatus.Pass, r.G3Continue);
            Assert.AreEqual(GateStatus.Pass, r.G3);
        }

        [Test]
        public void G3_ContinueRateBoundaries()
        {
            // 100% continue -> above 85% -> FAIL
            Assert.AreEqual(GateStatus.Fail, Analyze(Row("Fists", 1, 1, 90, "x", D(1, true, 5f))).G3Continue);
            // 0% continue -> below 40% -> FAIL, and overall G3 fails
            GateReport none = Analyze(Row("Fists", 1, 1, 90, "x", D(1, false, 5f)));
            Assert.AreEqual(GateStatus.Fail, none.G3Continue);
            Assert.AreEqual(GateStatus.Fail, none.G3);
        }

        [Test]
        public void G4_AverageRunMinutes_IncludesDecisionTime_AndChecksRange()
        {
            // 90 s fight + 12 s deciding = 102 s = 1.7 min -> PASS
            GateReport pass = Analyze(Row("Fists", 1, 1, 90, "x", D(1, true, 12f)));
            Assert.AreEqual(1.7, pass.AvgRunMinutes, 1e-3);
            Assert.AreEqual(GateStatus.Pass, pass.G4);

            Assert.AreEqual(GateStatus.Fail, Analyze(Row("Fists", 1, 1, 30)).G4);   // 0.5 min
            Assert.AreEqual(GateStatus.Fail, Analyze(Row("Fists", 1, 1, 200)).G4);  // 3.33 min
        }

        [Test]
        public void CauseWithCommaAndQuotes_DoesNotShiftColumns()
        {
            GateReport r = Analyze(Row("Giant Hammer", 40, 10, 90, "Killed by \"Grunt\", then ring-out"),
                                   Row("Dual Daggers", 10, 10, 90));
            Assert.AreEqual(0, r.SkippedRows);
            Assert.AreEqual(2, r.RunCount);
            Assert.AreEqual(GateStatus.Pass, r.G1);
        }

        [Test]
        public void ManualGates_PendingUntilEntered_ThenPassOrFail()
        {
            var lines = new List<string> { RunLogCsv.Header(), Row("Fists", 1, 1, 90) };

            GateReport pending = GateAnalyzer.Analyze(lines);
            Assert.AreEqual(GateStatus.Pending, pending.G2);
            Assert.AreEqual(GateStatus.Pending, pending.G5);

            GateReport pass = GateAnalyzer.Analyze(lines, new ManualGateInputs
            { G2MatchingLosses = 4, G2CheckedLosses = 5, G5PlayAgainPlayers = 3 });
            Assert.AreEqual(GateStatus.Pass, pass.G2);   // 80% exactly
            Assert.AreEqual(GateStatus.Pass, pass.G5);   // 3/5 exactly

            GateReport fail = GateAnalyzer.Analyze(lines, new ManualGateInputs
            { G2MatchingLosses = 3, G2CheckedLosses = 5, G5PlayAgainPlayers = 2 });
            Assert.AreEqual(GateStatus.Fail, fail.G2);
            Assert.AreEqual(GateStatus.Fail, fail.G5);
        }

        [Test]
        public void EmptyLog_GivesNoDataAndAReadableReport()
        {
            GateReport r = GateAnalyzer.Analyze(new string[0]);
            Assert.AreEqual(GateStatus.NoData, r.G1);
            Assert.AreEqual(GateStatus.NoData, r.G3);
            Assert.AreEqual(GateStatus.NoData, r.G4);
            StringAssert.Contains("G2", r.ToText());
            StringAssert.Contains("G5", r.ToText());
        }
    }
}
