using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GotA.EditorTools
{
    public enum GateStatus
    {
        /// <summary>Not enough rows in the log to evaluate.</summary>
        NoData = 0,
        /// <summary>Needs a human survey answer that has not been entered yet.</summary>
        Pending = 1,
        Pass = 2,
        Fail = 3
    }

    /// <summary>Human survey answers for G2 and G5 (null = not entered yet).</summary>
    public sealed class ManualGateInputs
    {
        /// <summary>G2: players whose stated reason for losing matched the logged cause.</summary>
        public int? G2MatchingLosses;

        /// <summary>G2: losses that were checked against the log.</summary>
        public int? G2CheckedLosses;

        /// <summary>G5: players who pressed Play Again without being prompted.</summary>
        public int? G5PlayAgainPlayers;

        /// <summary>G5: number of playtesters (5 for P-07).</summary>
        public int G5TotalPlayers = 5;
    }

    /// <summary>Result of analysing a run_log.csv (P-07 Prototype Gate draft).</summary>
    public sealed class GateReport
    {
        // ---- Thresholds (Prototype Gate) ----
        public const double G1MinHammerToDaggersRatio = 4.0;
        public const double G2MinMatchRate = 0.80;
        public const double G3MinMedianDecisionSeconds = 3.0;
        public const double G3MinContinueRate = 0.40;
        public const double G3MaxContinueRate = 0.85;
        public const double G4MinRunMinutes = 1.5;
        public const double G4MaxRunMinutes = 2.5;
        public const int G5MinPlayAgainPlayers = 3;

        public readonly List<string> Warnings = new List<string>();
        public string SourcePath;
        public int RunCount;
        public int SkippedRows;
        public int DefeatCount;

        // ---- G1 ----
        public int HammerRuns, DaggersRuns;
        public double HammerAvgPush, DaggersAvgPush;     // pooled: sum(push_total) / sum(hits)
        public double HammerMeanOfRuns, DaggersMeanOfRuns;
        public double G1Ratio;                           // +Infinity when Daggers = 0 and Hammer > 0
        public GateStatus G1;

        // ---- G3 ----
        public int DecisionCount;
        public double MedianDecisionSeconds;
        public GateStatus G3Median;
        public int Wave1Decisions, Wave1Continues;
        public double ContinueRateAfterWave1;
        public GateStatus G3Continue;
        public GateStatus G3;

        // ---- G4 ----
        public double AvgFightMinutes;                   // simulated fight time (run_duration_s)
        public double AvgDecisionMinutes;                // time on Extract/Continue screens
        public double AvgRunMinutes;                     // fight + decision (gated value)
        public GateStatus G4;

        // ---- Manual ----
        public double? G2MatchRate;
        public GateStatus G2;
        public GateStatus G5;
        public ManualGateInputs Manual = new ManualGateInputs();

        public static string Mark(GateStatus s)
        {
            switch (s)
            {
                case GateStatus.Pass: return "PASS";
                case GateStatus.Fail: return "FAIL";
                case GateStatus.Pending: return "PENDING (manual)";
                default: return "NO DATA";
            }
        }

        public string ToText()
        {
            CultureInfo ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            string line = new string('=', 64);

            sb.AppendLine(line);
            sb.AppendLine("GOD OF THE ARENA - P-07 PROTOTYPE GATE (DRAFT REPORT)");
            sb.AppendLine(line);
            sb.AppendLine("Log file : " + (SourcePath ?? "(none)"));
            sb.AppendLine("Runs     : " + RunCount + " (defeats " + DefeatCount + ", unreadable rows skipped " + SkippedRows + ")");
            sb.AppendLine();

            sb.AppendLine("[" + Mark(G1) + "] G1  Weapon differences  (Hammer avg push >= " +
                          G1MinHammerToDaggersRatio.ToString("0.#", ci) + " x Daggers)");
            sb.AppendLine("      Giant Hammer : " + Fmt(HammerAvgPush, HammerRuns > 0) + " u/hit  (" + HammerRuns +
                          " runs, mean of runs " + Fmt(HammerMeanOfRuns, HammerRuns > 0) + ")");
            sb.AppendLine("      Dual Daggers : " + Fmt(DaggersAvgPush, DaggersRuns > 0) + " u/hit  (" + DaggersRuns +
                          " runs, mean of runs " + Fmt(DaggersMeanOfRuns, DaggersRuns > 0) + ")");
            sb.AppendLine("      Ratio        : " + (G1 == GateStatus.NoData ? "n/a"
                          : double.IsInfinity(G1Ratio) ? "infinite (Daggers = 0)" : G1Ratio.ToString("0.00", ci) + " x"));
            sb.AppendLine();

            sb.AppendLine("[" + Mark(G2) + "] G2  Understand why they lost  (>= " + (G2MinMatchRate * 100).ToString("0", ci) + "% match the log)  MANUAL");
            sb.AppendLine("      Matching losses : " + (Manual.G2MatchingLosses.HasValue ? Manual.G2MatchingLosses.Value.ToString(ci) : "____") +
                          " / " + (Manual.G2CheckedLosses.HasValue ? Manual.G2CheckedLosses.Value.ToString(ci) : "____") +
                          (G2MatchRate.HasValue ? "  = " + (G2MatchRate.Value * 100).ToString("0.#", ci) + "%" : ""));
            sb.AppendLine("      Hint: the log has " + DefeatCount + " Defeat run(s); compare each player's own words with cause_of_death.");
            sb.AppendLine();

            sb.AppendLine("[" + Mark(G3) + "] G3  Hesitation");
            sb.AppendLine("      [" + Mark(G3Median) + "] Median decision time : " + Fmt(MedianDecisionSeconds, DecisionCount > 0) +
                          " s  (>= " + G3MinMedianDecisionSeconds.ToString("0.0", ci) + " s; " + DecisionCount + " decisions)");
            sb.AppendLine("      [" + Mark(G3Continue) + "] Continue rate after Wave 1 : " +
                          (Wave1Decisions > 0 ? (ContinueRateAfterWave1 * 100).ToString("0.#", ci) + "%" : "n/a") +
                          "  (" + (G3MinContinueRate * 100).ToString("0", ci) + "% - " + (G3MaxContinueRate * 100).ToString("0", ci) +
                          "%; " + Wave1Continues + " of " + Wave1Decisions + ")");
            sb.AppendLine();

            sb.AppendLine("[" + Mark(G4) + "] G4  Session length  (average per run " + G4MinRunMinutes.ToString("0.0", ci) + " - " +
                          G4MaxRunMinutes.ToString("0.0", ci) + " min)");
            sb.AppendLine("      Average per run : " + Fmt(AvgRunMinutes, RunCount > 0) + " min  (fight " + Fmt(AvgFightMinutes, RunCount > 0) +
                          " + decisions " + Fmt(AvgDecisionMinutes, RunCount > 0) + ")");
            sb.AppendLine();

            sb.AppendLine("[" + Mark(G5) + "] G5  Want to play again  (>= " + G5MinPlayAgainPlayers + "/" + Manual.G5TotalPlayers +
                          " pressed Play Again unprompted)  MANUAL");
            sb.AppendLine("      Players : " + (Manual.G5PlayAgainPlayers.HasValue ? Manual.G5PlayAgainPlayers.Value.ToString(ci) : "____") +
                          " / " + Manual.G5TotalPlayers);
            sb.AppendLine();

            GateStatus[] all = { G1, G2, G3, G4, G5 };
            bool anyFail = false, anyOpen = false;
            foreach (GateStatus s in all)
            {
                if (s == GateStatus.Fail) anyFail = true;
                else if (s != GateStatus.Pass) anyOpen = true;
            }
            sb.AppendLine("OVERALL: " + (anyFail ? "FAIL (at least one gate failed)"
                                         : anyOpen ? "INCOMPLETE (manual or missing data)" : "PASS (G1-G5)"));

            if (Warnings.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Notes:");
                foreach (string w in Warnings) sb.AppendLine(" - " + w);
            }
            sb.AppendLine(line);
            return sb.ToString();
        }

        private static string Fmt(double v, bool hasData) =>
            hasData ? v.ToString("0.00", CultureInfo.InvariantCulture) : "n/a";
    }

    /// <summary>
    /// Reads run_log.csv text (the P-06 format) and evaluates Prototype Gates G1, G3, G4.
    /// Pure C#: no UnityEngine, no file access, read-only with respect to the log.
    /// Columns are located by header name, so extra or reordered columns are fine.
    /// </summary>
    public static class GateAnalyzer
    {
        private static readonly string[] RequiredColumns =
        {
            "build_weapon", "decision_times", "hits", "push_total", "run_duration_s", "end_reason"
        };

        public static GateReport Analyze(IEnumerable<string> csvLines, ManualGateInputs manual = null, string sourcePath = null)
        {
            var report = new GateReport { SourcePath = sourcePath, Manual = manual ?? new ManualGateInputs() };
            CultureInfo ci = CultureInfo.InvariantCulture;

            Dictionary<string, int> col = null;
            double hammerPush = 0, hammerHits = 0, daggersPush = 0, daggersHits = 0;
            double hammerRunAvgSum = 0, daggersRunAvgSum = 0;
            int hammerRunsWithHits = 0, daggersRunsWithHits = 0;
            var decisionSeconds = new List<double>();
            double fightSecondsTotal = 0, decisionSecondsTotal = 0;

            foreach (string raw in csvLines)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                List<string> cells = SplitCsvLine(raw);

                if (col == null)
                {
                    col = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < cells.Count; i++) col[cells[i].Trim()] = i;
                    foreach (string need in RequiredColumns)
                    {
                        if (!col.ContainsKey(need))
                            report.Warnings.Add("Missing column '" + need + "' - is this a run_log.csv from P-06?");
                    }
                    continue;
                }

                if (!TryGet(cells, col, "run_duration_s", out string durText) ||
                    !double.TryParse(durText, NumberStyles.Float, ci, out double fightSeconds))
                {
                    report.SkippedRows++;
                    continue;
                }

                report.RunCount++;
                fightSecondsTotal += fightSeconds;

                if (TryGet(cells, col, "end_reason", out string reason) &&
                    string.Equals(reason, "Defeat", StringComparison.OrdinalIgnoreCase))
                    report.DefeatCount++;

                // --- decisions: "W1:Continue:3.420;W2:Extract:1.100" ---
                if (TryGet(cells, col, "decision_times", out string decisions) && decisions.Length > 0)
                {
                    foreach (string part in decisions.Split(';'))
                    {
                        string[] f = part.Split(':');
                        if (f.Length != 3 || !double.TryParse(f[2], NumberStyles.Float, ci, out double sec)) continue;

                        decisionSeconds.Add(sec);
                        decisionSecondsTotal += sec;

                        if (string.Equals(f[0], "W1", StringComparison.OrdinalIgnoreCase))
                        {
                            report.Wave1Decisions++;
                            if (string.Equals(f[1], "Continue", StringComparison.OrdinalIgnoreCase)) report.Wave1Continues++;
                        }
                    }
                }

                // --- G1: push distance per weapon ---
                if (TryGet(cells, col, "build_weapon", out string weapon))
                {
                    bool isHammer = weapon.IndexOf("hammer", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool isDaggers = weapon.IndexOf("dagger", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (isHammer) report.HammerRuns++;
                    if (isDaggers) report.DaggersRuns++;

                    if ((isHammer || isDaggers) &&
                        TryGet(cells, col, "push_total", out string pushText) &&
                        TryGet(cells, col, "hits", out string hitsText) &&
                        double.TryParse(pushText, NumberStyles.Float, ci, out double push) &&
                        double.TryParse(hitsText, NumberStyles.Float, ci, out double hits))
                    {
                        if (isHammer)
                        {
                            hammerPush += push; hammerHits += hits;
                            if (hits > 0) { hammerRunAvgSum += push / hits; hammerRunsWithHits++; }
                        }
                        else
                        {
                            daggersPush += push; daggersHits += hits;
                            if (hits > 0) { daggersRunAvgSum += push / hits; daggersRunsWithHits++; }
                        }
                    }
                }
            }

            if (col == null) report.Warnings.Add("The file is empty.");

            // ---------------- G1 ----------------
            report.HammerAvgPush = hammerHits > 0 ? hammerPush / hammerHits : 0;
            report.DaggersAvgPush = daggersHits > 0 ? daggersPush / daggersHits : 0;
            report.HammerMeanOfRuns = hammerRunsWithHits > 0 ? hammerRunAvgSum / hammerRunsWithHits : 0;
            report.DaggersMeanOfRuns = daggersRunsWithHits > 0 ? daggersRunAvgSum / daggersRunsWithHits : 0;

            if (hammerHits <= 0 || daggersHits <= 0)
            {
                report.G1 = GateStatus.NoData;
                report.Warnings.Add("G1 needs at least one Giant Hammer run and one Dual Daggers run that landed hits.");
            }
            else
            {
                report.G1Ratio = report.DaggersAvgPush > 0
                    ? report.HammerAvgPush / report.DaggersAvgPush
                    : (report.HammerAvgPush > 0 ? double.PositiveInfinity : 0);
                report.G1 = report.G1Ratio >= GateReport.G1MinHammerToDaggersRatio ? GateStatus.Pass : GateStatus.Fail;
            }

            // ---------------- G3 ----------------
            report.DecisionCount = decisionSeconds.Count;
            if (report.DecisionCount > 0)
            {
                report.MedianDecisionSeconds = Median(decisionSeconds);
                report.G3Median = report.MedianDecisionSeconds >= GateReport.G3MinMedianDecisionSeconds
                    ? GateStatus.Pass : GateStatus.Fail;
            }
            else report.G3Median = GateStatus.NoData;

            if (report.Wave1Decisions > 0)
            {
                report.ContinueRateAfterWave1 = (double)report.Wave1Continues / report.Wave1Decisions;
                report.G3Continue = report.ContinueRateAfterWave1 >= GateReport.G3MinContinueRate &&
                                    report.ContinueRateAfterWave1 <= GateReport.G3MaxContinueRate
                    ? GateStatus.Pass : GateStatus.Fail;
            }
            else report.G3Continue = GateStatus.NoData;

            if (report.G3Median == GateStatus.Fail || report.G3Continue == GateStatus.Fail) report.G3 = GateStatus.Fail;
            else if (report.G3Median == GateStatus.Pass && report.G3Continue == GateStatus.Pass) report.G3 = GateStatus.Pass;
            else report.G3 = GateStatus.NoData;

            // ---------------- G4 ----------------
            if (report.RunCount > 0)
            {
                report.AvgFightMinutes = fightSecondsTotal / report.RunCount / 60.0;
                report.AvgDecisionMinutes = decisionSecondsTotal / report.RunCount / 60.0;
                report.AvgRunMinutes = report.AvgFightMinutes + report.AvgDecisionMinutes;
                report.G4 = report.AvgRunMinutes >= GateReport.G4MinRunMinutes &&
                            report.AvgRunMinutes <= GateReport.G4MaxRunMinutes
                    ? GateStatus.Pass : GateStatus.Fail;
                report.Warnings.Add("G4 run time = simulated fight time (run_duration_s) + time on Extract/Continue screens; " +
                                    "build select and result screens are not logged.");
            }
            else report.G4 = GateStatus.NoData;

            // ---------------- G2 / G5 (manual) ----------------
            ManualGateInputs m = report.Manual;
            if (m.G2MatchingLosses.HasValue && m.G2CheckedLosses.HasValue && m.G2CheckedLosses.Value > 0)
            {
                report.G2MatchRate = (double)m.G2MatchingLosses.Value / m.G2CheckedLosses.Value;
                report.G2 = report.G2MatchRate.Value >= GateReport.G2MinMatchRate ? GateStatus.Pass : GateStatus.Fail;
            }
            else report.G2 = GateStatus.Pending;

            report.G5 = !m.G5PlayAgainPlayers.HasValue
                ? GateStatus.Pending
                : (m.G5PlayAgainPlayers.Value >= GateReport.G5MinPlayAgainPlayers ? GateStatus.Pass : GateStatus.Fail);

            if (report.HammerRuns > 0 || report.DaggersRuns > 0)
                report.Warnings.Add("G1 push distance counts knockback of every unit (including hits the Gladiator takes from enemies), " +
                                    "pooled per weapon as sum(push_total) / sum(hits).");

            return report;
        }

        public static double Median(List<double> values)
        {
            if (values == null || values.Count == 0) return 0;
            var sorted = new List<double>(values);
            sorted.Sort();
            int n = sorted.Count;
            return n % 2 == 1 ? sorted[n / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) / 2.0;
        }

        private static bool TryGet(List<string> cells, Dictionary<string, int> col, string name, out string value)
        {
            value = null;
            if (!col.TryGetValue(name, out int index) || index >= cells.Count) return false;
            value = cells[index].Trim();
            return true;
        }

        /// <summary>Splits one CSV line (RFC 4180 quotes; the P-06 writer never emits line breaks inside cells).</summary>
        public static List<string> SplitCsvLine(string line)
        {
            var cells = new List<string>();
            var sb = new StringBuilder();
            bool quoted = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (quoted)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                        else quoted = false;
                    }
                    else sb.Append(c);
                }
                else if (c == '"') quoted = true;
                else if (c == ',') { cells.Add(sb.ToString()); sb.Length = 0; }
                else if (c != '\r') sb.Append(c);
            }
            cells.Add(sb.ToString());
            return cells;
        }
    }
}
