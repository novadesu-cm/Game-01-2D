using System.Collections.Generic;
using GotA.Client;
using GotA.EditorTools;
using GotA.Simulation;
using NUnit.Framework;

namespace GotA.Tests
{
    /// <summary>P-07: Gate report maths on rows written by the real P-06 CSV writer (run, hit and decision files).</summary>
    public class GateAnalyzerTests
    {
        private static string RunRow(string weapon, float durationSeconds, int ringOuts = 0, string trait = "None")
        {
            return RunLogCsv.ToRow(new RunLogRecord
            {
                Seed = 1,
                Weapon = weapon,
                Trait = trait,
                WaveResults = "W1:Cleared",
                DurationSeconds = durationSeconds,
                FighterRingOuts = ringOuts
            });
        }

        /// <summary>One hit row per kb value, from the given attacker.</summary>
        private static List<string> Hits(string weapon, int attackerId, int seed, params float[] kbDistances)
        {
            var rows = new List<string>();
            for (int i = 0; i < kbDistances.Length; i++)
                rows.Add(RunLogCsv.ToHitRow(seed, new HitRow(i + 1, attackerId, 101, weapon, 5, kbDistances[i], HitTargetDeath.None)));
            return rows;
        }

        private static List<string> Decisions(params (int wave, bool cont, float sec)[] decisions)
        {
            var rows = new List<string>();
            foreach (var d in decisions)
                rows.Add(RunLogCsv.ToDecisionRow(1, new DecisionRecord(d.wave, d.cont, d.sec)));
            return rows;
        }

        private static List<string> WithHeader(string header, IEnumerable<string> rows)
        {
            var lines = new List<string> { header };
            lines.AddRange(rows);
            return lines;
        }

        private static GateReport Analyze(IEnumerable<string> runRows, IEnumerable<string> hitRows = null,
                                          IEnumerable<string> decisionRows = null, ManualGateInputs manual = null)
        {
            return GateAnalyzer.Analyze(
                WithHeader(RunLogCsv.Header(), runRows), manual, null,
                WithHeader(RunLogCsv.HitHeader(), hitRows ?? new string[0]),
                WithHeader(RunLogCsv.DecisionHeader(), decisionRows ?? new string[0]));
        }

        private static List<string> Concat(params List<string>[] parts)
        {
            var all = new List<string>();
            foreach (List<string> p in parts) all.AddRange(p);
            return all;
        }

        private static readonly string[] OneFistsRun = { RunRow("Fists", 90) };

        [Test]
        public void G1_HammerAtLeast4xDaggers_Passes_AndJustBelowFails()
        {
            GateReport pass = Analyze(new[] { RunRow("Giant Hammer", 90), RunRow("Dual Daggers", 90) },
                Concat(Hits("Giant Hammer", 1, 1, 4f, 4f), Hits("Dual Daggers", 1, 2, 1f, 1f)));
            Assert.AreEqual(4.0, pass.G1Ratio, 1e-3);
            Assert.AreEqual(GateStatus.Pass, pass.G1);

            GateReport fail = Analyze(new[] { RunRow("Giant Hammer", 90), RunRow("Dual Daggers", 90) },
                Concat(Hits("Giant Hammer", 1, 1, 3.9f, 3.9f), Hits("Dual Daggers", 1, 2, 1f, 1f)));
            Assert.AreEqual(GateStatus.Fail, fail.G1);
        }

        [Test]
        public void G1_MissingWeapon_IsNoData()
        {
            Assert.AreEqual(GateStatus.NoData,
                Analyze(new[] { RunRow("Giant Hammer", 90) }, Hits("Giant Hammer", 1, 1, 4f)).G1);
        }

        [Test]
        public void G1_UsesOnlyRowsWhereAttackerIsTheFighter()
        {
            GateReport r = Analyze(new[] { RunRow("Giant Hammer", 90), RunRow("Dual Daggers", 90) },
                Concat(Hits("Giant Hammer", 1, 1, 4f),
                       Hits("Dual Daggers", 1, 2, 1f),
                       Hits("Dual Daggers", 101, 2, 50f, 50f),   // enemy hits must not count
                       Hits("Enemy", 101, 2, 50f)));

            Assert.AreEqual(1.0, r.DaggersAvgPush, 1e-4);
            Assert.AreEqual(4.0, r.HammerAvgPush, 1e-4);
            Assert.AreEqual(3, r.EnemyHitRowsExcluded);
            Assert.AreEqual(GateStatus.Pass, r.G1);
        }

        [Test]
        public void G1_PoolsByHitRows_AndReportsMeanOfRuns()
        {
            // Seed 1: 1 hit at 10; seed 2: 3 hits at 2 -> pooled (10+6)/4 = 4.0, mean of runs (10+2)/2 = 6.0
            GateReport r = Analyze(new[] { RunRow("Giant Hammer", 90), RunRow("Giant Hammer", 90), RunRow("Dual Daggers", 90) },
                Concat(Hits("Giant Hammer", 1, 1, 10f), Hits("Giant Hammer", 1, 2, 2f, 2f, 2f), Hits("Dual Daggers", 1, 3, 0.5f)));
            Assert.AreEqual(4.0, r.HammerAvgPush, 1e-4);
            Assert.AreEqual(6.0, r.HammerMeanOfRuns, 1e-4);
            Assert.AreEqual(0.5, r.DaggersAvgPush, 1e-4);
        }

        [Test]
        public void G1b_RingOutsPerWeaponPerRun_FromRunRows()
        {
            GateReport r = Analyze(new[]
            {
                RunRow("Giant Hammer", 90, ringOuts: 3), RunRow("Giant Hammer", 90, ringOuts: 1),
                RunRow("Dual Daggers", 90, ringOuts: 0), RunRow("Fists", 90, ringOuts: 2)
            });
            Assert.AreEqual(2.0, r.HammerRingOutsPerRun, 1e-6);
            Assert.AreEqual(0.0, r.DaggersRingOutsPerRun, 1e-6);
            Assert.AreEqual(2.0, r.FistsRingOutsPerRun, 1e-6);
        }

        [Test]
        public void G3_MedianDecisionTime_OddAndEvenCounts()
        {
            GateReport odd = Analyze(OneFistsRun, null, Decisions((1, true, 2f), (2, true, 4f), (1, true, 6f)));
            Assert.AreEqual(4.0, odd.MedianDecisionSeconds, 1e-3);
            Assert.AreEqual(GateStatus.Pass, odd.G3Median);

            GateReport even = Analyze(OneFistsRun, null, Decisions((1, true, 1f), (2, true, 4f)));
            Assert.AreEqual(2.5, even.MedianDecisionSeconds, 1e-3);
            Assert.AreEqual(GateStatus.Fail, even.G3Median);
        }

        [Test]
        public void G3_ContinueRateAfterWave1_UsesOnlyWave1Decisions()
        {
            GateReport r = Analyze(OneFistsRun, null, Decisions(
                (1, true, 3f), (2, false, 3f),   // W2 choice must be ignored
                (1, true, 3f),
                (1, false, 3f)));

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
            Assert.AreEqual(GateStatus.Fail, Analyze(OneFistsRun, null, Decisions((1, true, 5f))).G3Continue);
            // 0% continue -> below 40% -> FAIL, and overall G3 fails
            GateReport none = Analyze(OneFistsRun, null, Decisions((1, false, 5f)));
            Assert.AreEqual(GateStatus.Fail, none.G3Continue);
            Assert.AreEqual(GateStatus.Fail, none.G3);
        }

        [Test]
        public void G4_AverageRunMinutes_IncludesDecisionTime_AndChecksRange()
        {
            // 90 s fight + 12 s deciding = 102 s = 1.7 min -> PASS
            GateReport pass = Analyze(OneFistsRun, null, Decisions((1, true, 12f)));
            Assert.AreEqual(1.7, pass.AvgRunMinutes, 1e-3);
            Assert.AreEqual(GateStatus.Pass, pass.G4);

            Assert.AreEqual(GateStatus.Fail, Analyze(new[] { RunRow("Fists", 30) }).G4);   // 0.5 min
            Assert.AreEqual(GateStatus.Fail, Analyze(new[] { RunRow("Fists", 200) }).G4);  // 3.33 min
        }

        [Test]
        public void TraitWithCommaAndQuotes_DoesNotShiftColumns()
        {
            GateReport r = Analyze(new[] { RunRow("Giant Hammer", 90, 0, "A \"B\", C"), RunRow("Dual Daggers", 90) },
                Concat(Hits("Giant Hammer", 1, 1, 4f), Hits("Dual Daggers", 1, 2, 1f)));
            Assert.AreEqual(0, r.SkippedRows);
            Assert.AreEqual(2, r.RunCount);
            Assert.AreEqual(GateStatus.Pass, r.G1);
        }

        [Test]
        public void ManualGates_PendingUntilEntered_ThenPassOrFail()
        {
            var runLines = WithHeader(RunLogCsv.Header(), OneFistsRun);

            GateReport pending = GateAnalyzer.Analyze(runLines);
            Assert.AreEqual(GateStatus.Pending, pending.G2);
            Assert.AreEqual(GateStatus.Pending, pending.G5);

            GateReport pass = GateAnalyzer.Analyze(runLines, new ManualGateInputs
            { G2MatchingLosses = 4, G2CheckedLosses = 5, G5PlayAgainPlayers = 3 });
            Assert.AreEqual(GateStatus.Pass, pass.G2);   // 80% exactly
            Assert.AreEqual(GateStatus.Pass, pass.G5);   // 3/5 exactly

            GateReport fail = GateAnalyzer.Analyze(runLines, new ManualGateInputs
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
