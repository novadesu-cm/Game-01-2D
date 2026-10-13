using System;
using System.Collections.Generic;
using GotA.Simulation;

namespace GotA.Client
{
    /// <summary>One Extract/Continue choice and how long the player took (real seconds).</summary>
    public readonly struct DecisionRecord
    {
        public readonly int AfterWave;
        public readonly bool Continued;
        public readonly float Seconds;

        public DecisionRecord(int afterWave, bool continued, float seconds)
        {
            AfterWave = afterWave;
            Continued = continued;
            Seconds = seconds;
        }
    }

    /// <summary>Everything P-06 logs for one finished run (one CSV row).</summary>
    public sealed class RunLogRecord
    {
        public int Seed;
        public string Weapon;
        public string Trait;
        public RunEndReason Reason;
        public int WavesCleared;
        public int WaveReached;
        public int FinalPot;
        public int BestPot;
        public int TotalTicks;
        public float DurationSeconds;

        public List<DecisionRecord> Decisions = new List<DecisionRecord>();

        /// <summary>1. Total seconds the player spent on Extract/Continue screens.</summary>
        public float DecisionSecondsTotal;

        /// <summary>2. Continue choices / decisions. NaN when the run had no decision (died in Wave 1).</summary>
        public float ContinueRate;

        /// <summary>3. One-line cause (ring-out / HP + last attacker, extract, timeout, clear).</summary>
        public string CauseOfDeath;

        /// <summary>4. Total knockback displacement / hits landed (units of arena length).</summary>
        public float AveragePushDistance;
        public float TotalPushDistance;
        public int TotalHits;

        /// <summary>Per-wave outcome list, e.g. "W1:Cleared;W2:Defeat".</summary>
        public string WaveResults;
    }

    /// <summary>
    /// P-06: collects the per-run debug data. Pure C# (no UnityEngine, no file I/O): the Unity layer
    /// feeds it timestamps, then hands the record to <see cref="RunLogCsv"/>.
    /// Read-only with respect to the simulation.
    /// </summary>
    public sealed class RunLogRecorder
    {
        private readonly List<DecisionRecord> _decisions = new List<DecisionRecord>();
        private bool _decisionOpen;
        private int _decisionWave;
        private float _decisionStart;

        public RunLogRecorder(int seed, string weapon, string trait)
        {
            Seed = seed;
            Weapon = weapon ?? "None";
            Trait = trait ?? "None";
        }

        public int Seed { get; }
        public string Weapon { get; }
        public string Trait { get; }
        public IReadOnlyList<DecisionRecord> Decisions => _decisions;

        /// <summary>The Extract/Continue overlay appeared. <paramref name="now"/> is real seconds.</summary>
        public void BeginDecision(int afterWave, float now)
        {
            _decisionOpen = true;
            _decisionWave = afterWave;
            _decisionStart = now;
        }

        /// <summary>The player chose. No-op when no decision is open.</summary>
        public void EndDecision(bool continued, float now)
        {
            if (!_decisionOpen) return;
            _decisionOpen = false;
            _decisions.Add(new DecisionRecord(_decisionWave, continued, Math.Max(0f, now - _decisionStart)));
        }

        /// <summary>Builds the record of an ended run.</summary>
        public RunLogRecord Finish(RunManager run, RunSummary summary, int bestPot)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (summary == null) throw new ArgumentNullException(nameof(summary));
            if (run.State != RunState.Ended)
                throw new InvalidOperationException("Run has not ended.");

            float decisionTotal = 0f;
            int continues = 0;
            for (int i = 0; i < _decisions.Count; i++)
            {
                decisionTotal += _decisions[i].Seconds;
                if (_decisions[i].Continued) continues++;
            }

            var waves = new List<string>();
            for (int i = 0; i < run.WaveResults.Count; i++)
            {
                WaveResult r = run.WaveResults[i];
                waves.Add("W" + r.WaveNumber + ":" + r.Outcome);
            }

            return new RunLogRecord
            {
                Seed = Seed,
                Weapon = Weapon,
                Trait = Trait,
                Reason = run.EndReason,
                WavesCleared = summary.WavesCleared,
                WaveReached = summary.WaveReached,
                FinalPot = summary.FinalPot,
                BestPot = bestPot,
                TotalTicks = run.TotalTicks,
                DurationSeconds = run.TotalTicks * run.Config.Arena.FixedDt,
                Decisions = new List<DecisionRecord>(_decisions),
                DecisionSecondsTotal = decisionTotal,
                ContinueRate = _decisions.Count > 0 ? (float)continues / _decisions.Count : float.NaN,
                CauseOfDeath = summary.CauseLine ?? string.Empty,
                AveragePushDistance = run.AveragePushDistance,
                TotalPushDistance = run.TotalPushDistance,
                TotalHits = run.TotalHits,
                WaveResults = string.Join(";", waves)
            };
        }
    }
}
