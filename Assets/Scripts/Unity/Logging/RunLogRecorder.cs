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

    /// <summary>One per-hit CSV row (D-08 knockback distance; target-died flag).</summary>
    public readonly struct HitRow
    {
        public readonly int Tick;
        public readonly int AttackerId;
        public readonly int TargetId;
        public readonly string Weapon;       // attacker's weapon; "Enemy" for enemies
        public readonly int Damage;
        public readonly float KbDistance;
        public readonly HitTargetDeath TargetDied;

        public HitRow(int tick, int attackerId, int targetId, string weapon, int damage,
                      float kbDistance, HitTargetDeath targetDied)
        {
            Tick = tick;
            AttackerId = attackerId;
            TargetId = targetId;
            Weapon = weapon;
            Damage = damage;
            KbDistance = kbDistance;
            TargetDied = targetDied;
        }
    }

    /// <summary>Everything P-06 logs for one finished run: one run row, N hit rows, N decision rows.</summary>
    public sealed class RunLogRecord
    {
        public int Seed;
        public string Weapon;
        public string Trait;
        public RunEndReason Reason;
        public int FinalPot;
        public int BestPot;
        public int TotalTicks;
        public float DurationSeconds;

        /// <summary>Per-wave outcome list, e.g. "W1:Cleared;W2:Defeat".</summary>
        public string WaveResults;

        /// <summary>Fighter death: None when the Fighter survived the run.</summary>
        public HitTargetDeath FighterDeath;

        /// <summary>UnitID of the last attacker that hit the Fighter (0 = none). Only meaningful with FighterDeath != None.</summary>
        public int LastAttackerId;

        /// <summary>Enemies that ring-outed within a window of a Fighter hit, each counted once (G1b, Fighter hits only).</summary>
        public int FighterRingOuts;

        public List<DecisionRecord> Decisions = new List<DecisionRecord>();
        public List<HitRow> Hits = new List<HitRow>();
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

            var waves = new List<string>();
            for (int i = 0; i < run.WaveResults.Count; i++)
            {
                WaveResult r = run.WaveResults[i];
                waves.Add("W" + r.WaveNumber + ":" + r.Outcome);
            }

            int fighterId = run.Config.FighterUnitId;

            // Rows are logged for both teams; G1/G1b filter on the attacker UnitID (Fighter = 1).
            var hits = new List<HitRow>(run.HitLog.Count);
            var ringOutVictims = new HashSet<long>();
            for (int i = 0; i < run.HitLog.Count; i++)
            {
                HitLogEntry e = run.HitLog[i];
                bool byFighter = e.AttackerId == fighterId;
                hits.Add(new HitRow(e.RunTick, e.AttackerId, e.TargetId, byFighter ? Weapon : "Enemy",
                                    e.Damage, e.KbDistance, e.TargetDied));

                if (byFighter && e.TargetDied == HitTargetDeath.RingOut)
                    ringOutVictims.Add(((long)e.WaveNumber << 32) | (uint)e.TargetId); // one per enemy, however many hits
            }

            HitTargetDeath fighterDeath = HitTargetDeath.None;
            int lastAttacker = 0;
            SimUnit fighter = run.Sim?.GetUnit(fighterId);
            if (fighter != null && !fighter.IsAlive)
            {
                fighterDeath = fighter.DeathCause == DeathCause.RingOut ? HitTargetDeath.RingOut : HitTargetDeath.HP;
                lastAttacker = fighter.LastAttackerId;
            }

            return new RunLogRecord
            {
                Seed = Seed,
                Weapon = Weapon,
                Trait = Trait,
                Reason = run.EndReason,
                FinalPot = summary.FinalPot,
                BestPot = bestPot,
                TotalTicks = run.TotalTicks,
                DurationSeconds = run.TotalTicks * run.Config.Arena.FixedDt,
                WaveResults = string.Join(";", waves),
                FighterDeath = fighterDeath,
                LastAttackerId = lastAttacker,
                FighterRingOuts = ringOutVictims.Count,
                Decisions = new List<DecisionRecord>(_decisions),
                Hits = hits
            };
        }
    }
}
