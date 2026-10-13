using System;
using GotA.Simulation;

namespace GotA.Client
{
    /// <summary>Snapshot of a finished run for the Result screen (and later the P-06 log).</summary>
    public sealed class RunSummary
    {
        public RunEndReason Reason;
        public int FinalPot;
        public int WavesCleared;
        public int WaveReached;

        /// <summary>One line: why the run ended (death cause + last attacker, extract, timeout, clear).</summary>
        public string CauseLine;

        public bool IsNewBest;

        /// <summary>
        /// Builds a summary from an ended run. <paramref name="unitName"/> maps a UnitId to a display
        /// name for the "killed by" text (may be null).
        /// </summary>
        public static RunSummary Build(RunManager run, Func<int, string> unitName)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (run.State != RunState.Ended)
                throw new InvalidOperationException("Run has not ended.");

            int cleared = 0;
            for (int i = 0; i < run.WaveResults.Count; i++)
                if (run.WaveResults[i].Outcome == CombatOutcome.Cleared) cleared++;

            return new RunSummary
            {
                Reason = run.EndReason,
                FinalPot = run.FinalPot,
                WavesCleared = cleared,
                WaveReached = run.WaveNumber,
                CauseLine = BuildCauseLine(run, unitName)
            };
        }

        private static string BuildCauseLine(RunManager run, Func<int, string> unitName)
        {
            int wave = run.WaveNumber;
            switch (run.EndReason)
            {
                case RunEndReason.Extracted:
                    return "Extracted after Wave " + wave + " with " + run.Pot + " Pot.";

                case RunEndReason.ClearedAllWaves:
                    return "Cleared all " + run.WaveCount + " waves.";

                case RunEndReason.Timeout:
                    return "Wave " + wave + " timed out: Pot " + run.Pot + " halved to " + run.FinalPot + ".";

                case RunEndReason.Defeat:
                    return DefeatLine(run, wave, unitName);

                default:
                    return string.Empty;
            }
        }

        private static string DefeatLine(RunManager run, int wave, Func<int, string> unitName)
        {
            SimUnit fighter = run.Sim?.GetUnit(run.Config.FighterUnitId);
            if (fighter == null) return "Defeated in Wave " + wave + ".";

            string attacker = null;
            if (fighter.LastAttackerId != 0)
                attacker = unitName?.Invoke(fighter.LastAttackerId) ?? ("enemy " + fighter.LastAttackerId);

            switch (fighter.DeathCause)
            {
                case DeathCause.RingOut:
                    return attacker == null
                        ? "Ring-out in Wave " + wave + "."
                        : "Ring-out in Wave " + wave + " (last hit by " + attacker + ").";

                case DeathCause.HP:
                    return attacker == null
                        ? "Killed in Wave " + wave + "."
                        : "Killed by " + attacker + " in Wave " + wave + ".";

                default:
                    return "Defeated in Wave " + wave + ".";
            }
        }
    }
}
