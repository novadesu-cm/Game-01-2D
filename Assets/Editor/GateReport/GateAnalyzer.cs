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
        public int HammerRuns, DaggersRuns, FistsRuns;   // from run rows (build_weapon)
        public double HammerAvgPush, DaggersAvgPush;     // D-08 kb_distance per Fighter hit, pooled over hit rows
        public double HammerMeanOfRuns, DaggersMeanOfRuns;
        public int HammerHitRows, DaggersHitRows;        // Fighter hit rows used
        public int EnemyHitRowsExcluded;                 // attacker is not the Fighter
        public double G1Ratio;                           // +Infinity when Daggers = 0 and Hammer > 0
        public GateStatus G1;

        // ---- G1b (D-09): RingOut count per weapon per run, Fighter hits only (info, no threshold) ----
        public double HammerRingOutsPerRun, DaggersRingOutsPerRun, FistsRingOutsPerRun;

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
            sb.AppendLine("      G1b RingOuts per run (Fighter hits): Hammer " + Fmt(HammerRingOutsPerRun, HammerRuns > 0) +
                          ", Daggers " + Fmt(DaggersRingOutsPerRun, DaggersRuns > 0) +
                          ", Fists " + Fmt(FistsRingOutsPerRun, FistsRuns > 0));
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
    /// Reads the P-06 CSV text and evaluates Prototype Gates G1, G1b, G3, G4:
    ///   run rows      (run_log.csv)      build, ring-outs per weapon, run duration
    ///   hit rows      (hit_log.csv)      G1 uses only rows whose attacker is the Fighter (UnitID 1)
    ///   decision rows (decision_log.csv) G3 decision time and continue rate, G4 decision time
    /// Pure C#: no UnityEngine, no file access, read-only with respect to the logs.
    /// Columns are located by header name, so extra or reordered columns are fine.
    /// </summary>
    public static class GateAnalyzer
    {
        public const string FighterAttackerId = "1";

        private static readonly string[] RunColumns = { "build_weapon", "run_duration_s", "end_reason", "ringouts_by_weapon" };
        private static readonly string[] HitColumns = { "seed", "attacker_id", "weapon", "kb_distance" };
        private static readonly string[] DecisionColumns = { "wave", "choice", "decision_time_s" };

        public static GateReport Analyze(IEnumerable<string> runLines, ManualGateInputs manual = null, string sourcePath = null,
                                         IEnumerable<string> hitLines = null, IEnumerable<string> decisionLines = null)
        {
            var report = new GateReport { SourcePath = sourcePath, Manual = manual ?? new ManualGateInputs() };
            CultureInfo ci = CultureInfo.InvariantCulture;

            // ---------------- run rows: runs per weapon, duration, ring-outs ----------------
            double fightSecondsTotal = 0;
            double hammerRingOuts = 0, daggersRingOuts = 0, fistsRingOuts = 0;
            List<Dictionary<string, string>> runRows = ReadTable(runLines, RunColumns, report, "run_log.csv", out bool runHeaderSeen);
            foreach (Dictionary<string, string> row in runRows)
            {
                if (!row.TryGetValue("run_duration_s", out string durText) ||
                    !double.TryParse(durText, NumberStyles.Float, ci, out double fightSeconds))
                {
                    report.SkippedRows++;
                    continue;
                }

                report.RunCount++;
                fightSecondsTotal += fightSeconds;

                if (row.TryGetValue("end_reason", out string reason) &&
                    string.Equals(reason, "Defeat", StringComparison.OrdinalIgnoreCase))
                    report.DefeatCount++;

                string weapon = row.TryGetValue("build_weapon", out string w) ? w : string.Empty;
                int ringOuts = ParseRingOuts(row.TryGetValue("ringouts_by_weapon", out string ro) ? ro : null);
                if (weapon.IndexOf("hammer", StringComparison.OrdinalIgnoreCase) >= 0) { report.HammerRuns++; hammerRingOuts += ringOuts; }
                else if (weapon.IndexOf("dagger", StringComparison.OrdinalIgnoreCase) >= 0) { report.DaggersRuns++; daggersRingOuts += ringOuts; }
                else if (weapon.IndexOf("fist", StringComparison.OrdinalIgnoreCase) >= 0) { report.FistsRuns++; fistsRingOuts += ringOuts; }
            }

            if (!runHeaderSeen) report.Warnings.Add("The run file is empty.");

            report.HammerRingOutsPerRun = report.HammerRuns > 0 ? hammerRingOuts / report.HammerRuns : 0;
            report.DaggersRingOutsPerRun = report.DaggersRuns > 0 ? daggersRingOuts / report.DaggersRuns : 0;
            report.FistsRingOutsPerRun = report.FistsRuns > 0 ? fistsRingOuts / report.FistsRuns : 0;

            // ---------------- hit rows: G1 (Fighter attacker only) ----------------
            double hammerKb = 0, daggersKb = 0;
            var hammerBySeed = new Dictionary<string, KeyValuePair<double, int>>();
            var daggersBySeed = new Dictionary<string, KeyValuePair<double, int>>();
            if (hitLines != null)
            {
                foreach (Dictionary<string, string> row in ReadTable(hitLines, HitColumns, report, "hit_log.csv", out _))
                {
                    if (!row.TryGetValue("attacker_id", out string attacker) || attacker != FighterAttackerId)
                    {
                        report.EnemyHitRowsExcluded++;
                        continue;
                    }

                    if (!row.TryGetValue("weapon", out string weapon) ||
                        !row.TryGetValue("kb_distance", out string kbText) ||
                        !double.TryParse(kbText, NumberStyles.Float, ci, out double kb))
                        continue;

                    string seed = row.TryGetValue("seed", out string s) ? s : string.Empty;
                    bool isHammer = weapon.IndexOf("hammer", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool isDaggers = weapon.IndexOf("dagger", StringComparison.OrdinalIgnoreCase) >= 0;

                    if (isHammer) { hammerKb += kb; report.HammerHitRows++; Accumulate(hammerBySeed, seed, kb); }
                    else if (isDaggers) { daggersKb += kb; report.DaggersHitRows++; Accumulate(daggersBySeed, seed, kb); }
                }
            }

            // ---------------- decision rows ----------------
            var decisionSeconds = new List<double>();
            double decisionSecondsTotal = 0;
            if (decisionLines != null)
            {
                foreach (Dictionary<string, string> row in ReadTable(decisionLines, DecisionColumns, report, "decision_log.csv", out _))
                {
                    if (!row.TryGetValue("decision_time_s", out string secText) ||
                        !double.TryParse(secText, NumberStyles.Float, ci, out double sec))
                        continue;

                    decisionSeconds.Add(sec);
                    decisionSecondsTotal += sec;

                    if (row.TryGetValue("wave", out string wave) && wave == "1")
                    {
                        report.Wave1Decisions++;
                        if (row.TryGetValue("choice", out string choice) &&
                            string.Equals(choice, "Continue", StringComparison.OrdinalIgnoreCase))
                            report.Wave1Continues++;
                    }
                }
            }

            // ---------------- G1 ----------------
            report.HammerAvgPush = report.HammerHitRows > 0 ? hammerKb / report.HammerHitRows : 0;
            report.DaggersAvgPush = report.DaggersHitRows > 0 ? daggersKb / report.DaggersHitRows : 0;
            report.HammerMeanOfRuns = MeanOfRuns(hammerBySeed);
            report.DaggersMeanOfRuns = MeanOfRuns(daggersBySeed);

            if (report.HammerHitRows <= 0 || report.DaggersHitRows <= 0)
            {
                report.G1 = GateStatus.NoData;
                report.Warnings.Add("G1 needs Fighter hit rows for both Giant Hammer and Dual Daggers (hit_log.csv).");
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

            if (report.HammerHitRows > 0 || report.DaggersHitRows > 0)
                report.Warnings.Add("G1 = D-08 kb_distance per hit, pooled per weapon over hit rows whose attacker is the Fighter (UnitID " +
                                    FighterAttackerId + "); " + report.EnemyHitRowsExcluded + " enemy hit row(s) excluded.");

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

        /// <summary>Parses CSV lines into header-keyed rows. The first non-blank line is the header.</summary>
        private static List<Dictionary<string, string>> ReadTable(IEnumerable<string> lines, string[] required,
            GateReport report, string label, out bool headerSeen)
        {
            var rows = new List<Dictionary<string, string>>();
            headerSeen = false;
            if (lines == null) return rows;

            List<string> header = null;
            foreach (string raw in lines)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                List<string> cells = SplitCsvLine(raw);

                if (header == null)
                {
                    header = new List<string>();
                    foreach (string c in cells) header.Add(c.Trim());
                    headerSeen = true;
                    foreach (string need in required)
                    {
                        if (!header.Contains(need))
                            report.Warnings.Add("Missing column '" + need + "' in " + label + " - is this a P-06 log?");
                    }
                    continue;
                }

                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < header.Count && i < cells.Count; i++) row[header[i]] = cells[i].Trim();
                rows.Add(row);
            }
            return rows;
        }

        /// <summary>"Giant Hammer:3" -> 3 (0 when blank or unreadable).</summary>
        private static int ParseRingOuts(string cell)
        {
            if (string.IsNullOrEmpty(cell)) return 0;
            int colon = cell.LastIndexOf(':');
            return colon >= 0 && int.TryParse(cell.Substring(colon + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : 0;
        }

        private static void Accumulate(Dictionary<string, KeyValuePair<double, int>> bySeed, string seed, double kb)
        {
            bySeed.TryGetValue(seed, out KeyValuePair<double, int> cur);
            bySeed[seed] = new KeyValuePair<double, int>(cur.Key + kb, cur.Value + 1);
        }

        private static double MeanOfRuns(Dictionary<string, KeyValuePair<double, int>> bySeed)
        {
            if (bySeed.Count == 0) return 0;
            double sum = 0;
            foreach (KeyValuePair<double, int> v in bySeed.Values) sum += v.Key / v.Value;
            return sum / bySeed.Count;
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
