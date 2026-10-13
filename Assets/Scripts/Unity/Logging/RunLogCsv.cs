using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace GotA.Client
{
    /// <summary>
    /// P-06: CSV formatting and file output (System.IO only, no UnityEngine). Three files, all appended;
    /// each header is written when its file is new or empty:
    ///   run_log.csv      one row per run
    ///   hit_log.csv      one row per hit (both teams; attacker UnitID is a column)
    ///   decision_log.csv one row per Extract/Continue decision
    /// </summary>
    public static class RunLogCsv
    {
        public static readonly string[] Columns =
        {
            "seed",
            "build_weapon",
            "build_trait",
            "end_reason",
            "wave_results",
            "death_cause",             // None / RingOut / HP (the Fighter)
            "last_attacker_id",        // UnitID that last hit the Fighter; blank when the Fighter survived
            "ringouts_by_weapon",      // "Giant Hammer:3" (Fighter hits only)
            "run_duration_s",
            "final_pot",
            "best_pot"
        };

        public static readonly string[] HitColumns =
        {
            "seed", "tick", "attacker_id", "target_id", "weapon", "damage", "kb_distance", "target_died"
        };

        public static readonly string[] DecisionColumns =
        {
            "seed", "wave", "choice", "decision_time_s"
        };

        public static string Header() => string.Join(",", Columns);
        public static string HitHeader() => string.Join(",", HitColumns);
        public static string DecisionHeader() => string.Join(",", DecisionColumns);

        public static string ToRow(RunLogRecord r)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            CultureInfo ci = CultureInfo.InvariantCulture;

            string[] cells =
            {
                r.Seed.ToString(ci),
                r.Weapon,
                r.Trait,
                r.Reason.ToString(),
                r.WaveResults,
                r.FighterDeath.ToString(),
                r.FighterDeath == Simulation.HitTargetDeath.None ? string.Empty : r.LastAttackerId.ToString(ci),
                r.Weapon + ":" + r.FighterRingOuts.ToString(ci),
                r.DurationSeconds.ToString("0.00", ci),
                r.FinalPot.ToString(ci),
                r.BestPot.ToString(ci)
            };
            return Join(cells);
        }

        public static string ToHitRow(int seed, HitRow h)
        {
            CultureInfo ci = CultureInfo.InvariantCulture;
            return Join(new[]
            {
                seed.ToString(ci),
                h.Tick.ToString(ci),
                h.AttackerId.ToString(ci),
                h.TargetId.ToString(ci),
                h.Weapon,
                h.Damage.ToString(ci),
                h.KbDistance.ToString("0.000", ci),
                h.TargetDied.ToString()
            });
        }

        public static string ToDecisionRow(int seed, DecisionRecord d)
        {
            CultureInfo ci = CultureInfo.InvariantCulture;
            return Join(new[]
            {
                seed.ToString(ci),
                d.AfterWave.ToString(ci),
                d.Continued ? "Continue" : "Extract",
                d.Seconds.ToString("0.000", ci)
            });
        }

        private static string Join(string[] cells)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < cells.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(Escape(cells[i]));
            }
            return sb.ToString();
        }

        /// <summary>Quotes a cell when it has a comma, quote or line break; flattens line breaks to spaces.</summary>
        public static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            value = value.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');
            if (value.IndexOfAny(new[] { ',', '"' }) < 0) return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        /// <summary>Appends the run row (creating folder, file and header when needed). Throws on I/O errors.</summary>
        public static void Append(string path, RunLogRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            AppendLines(path, Header(), new[] { ToRow(record) });
        }

        /// <summary>Appends one row per hit of the run.</summary>
        public static void AppendHits(string path, RunLogRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            var lines = new List<string>(record.Hits.Count);
            for (int i = 0; i < record.Hits.Count; i++) lines.Add(ToHitRow(record.Seed, record.Hits[i]));
            AppendLines(path, HitHeader(), lines);
        }

        /// <summary>Appends one row per Extract/Continue decision of the run.</summary>
        public static void AppendDecisions(string path, RunLogRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            var lines = new List<string>(record.Decisions.Count);
            for (int i = 0; i < record.Decisions.Count; i++) lines.Add(ToDecisionRow(record.Seed, record.Decisions[i]));
            AppendLines(path, DecisionHeader(), lines);
        }

        private static void AppendLines(string path, string header, IReadOnlyList<string> lines)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("path is empty", nameof(path));

            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            bool needsHeader = !File.Exists(path) || new FileInfo(path).Length == 0;
            using (var writer = new StreamWriter(path, true, new UTF8Encoding(false)))
            {
                if (needsHeader) writer.Write(header + "\n");
                for (int i = 0; i < lines.Count; i++) writer.Write(lines[i] + "\n");
            }
        }
    }
}
