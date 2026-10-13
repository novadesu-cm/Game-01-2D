using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace GotA.Client
{
    /// <summary>
    /// P-06: CSV formatting and file output (System.IO only, no UnityEngine).
    /// One row per run, appended; the header is written when the file is new or empty.
    /// </summary>
    public static class RunLogCsv
    {
        public static readonly string[] Columns =
        {
            "seed",
            "build_weapon",
            "build_trait",
            "end_reason",
            "waves_cleared",
            "wave_reached",
            "wave_results",
            "decision_time_total_s",
            "decision_times",          // "W1:Continue:3.420;W2:Extract:1.100"
            "continue_rate",
            "cause_of_death",
            "hits",
            "push_total",
            "avg_push_distance",
            "run_duration_s",
            "total_ticks",
            "final_pot",
            "best_pot"
        };

        public static string Header() => string.Join(",", Columns);

        public static string ToRow(RunLogRecord r)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            CultureInfo ci = CultureInfo.InvariantCulture;

            var decisions = new StringBuilder();
            for (int i = 0; i < r.Decisions.Count; i++)
            {
                DecisionRecord d = r.Decisions[i];
                if (i > 0) decisions.Append(';');
                decisions.Append('W').Append(d.AfterWave).Append(':')
                         .Append(d.Continued ? "Continue" : "Extract").Append(':')
                         .Append(d.Seconds.ToString("0.000", ci));
            }

            string[] cells =
            {
                r.Seed.ToString(ci),
                r.Weapon,
                r.Trait,
                r.Reason.ToString(),
                r.WavesCleared.ToString(ci),
                r.WaveReached.ToString(ci),
                r.WaveResults,
                r.DecisionSecondsTotal.ToString("0.000", ci),
                decisions.ToString(),
                float.IsNaN(r.ContinueRate) ? string.Empty : r.ContinueRate.ToString("0.000", ci),
                r.CauseOfDeath,
                r.TotalHits.ToString(ci),
                r.TotalPushDistance.ToString("0.000", ci),
                r.AveragePushDistance.ToString("0.000", ci),
                r.DurationSeconds.ToString("0.00", ci),
                r.TotalTicks.ToString(ci),
                r.FinalPot.ToString(ci),
                r.BestPot.ToString(ci)
            };

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

        /// <summary>Appends one row (creating folder, file and header when needed). Throws on I/O errors.</summary>
        public static void Append(string path, RunLogRecord record)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("path is empty", nameof(path));

            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            bool needsHeader = !File.Exists(path) || new FileInfo(path).Length == 0;
            using (var writer = new StreamWriter(path, true, new UTF8Encoding(false)))
            {
                if (needsHeader) writer.Write(Header() + "\n");
                writer.Write(ToRow(record) + "\n");
            }
        }
    }
}
