using System;
using System.Collections.Generic;

namespace GotA.Simulation
{
    public enum RunState
    {
        NotStarted = 0,
        /// <summary>A wave is being simulated; call Tick().</summary>
        InWave = 1,
        /// <summary>Wave cleared, more waves remain: call Extract() or Continue().</summary>
        AwaitingDecision = 2,
        Ended = 3
    }

    public enum RunEndReason
    {
        None = 0,
        /// <summary>Player took the Pot between waves. Full Pot.</summary>
        Extracted = 1,
        /// <summary>Last wave cleared. Full Pot.</summary>
        ClearedAllWaves = 2,
        /// <summary>A wave hit its time limit. floor(Pot * multiplier).</summary>
        Timeout = 3,
        /// <summary>Fighter died. Pot 0.</summary>
        Defeat = 4
    }

    /// <summary>What happened in one finished wave.</summary>
    public readonly struct WaveResult
    {
        public readonly int WaveNumber;      // 1-based
        public readonly CombatOutcome Outcome;
        public readonly int Ticks;
        public readonly int FighterHpAtEnd;
        public readonly int PotAfterWave;    // Pot after this wave's reward (if any) was added

        public WaveResult(int waveNumber, CombatOutcome outcome, int ticks, int fighterHpAtEnd, int potAfterWave)
        {
            WaveNumber = waveNumber;
            Outcome = outcome;
            Ticks = ticks;
            FighterHpAtEnd = fighterHpAtEnd;
            PotAfterWave = potAfterWave;
        }
    }

    /// <summary>
    /// P-04: run flow (up to N waves, Extract/Continue between waves), Pot, Timeout and Defeat.
    /// Pure C#, deterministic, driven only by Tick() and the two player decisions.
    /// Each wave is a fresh ArenaSim; the fighter's HP is carried over (no regen between waves).
    /// </summary>
    public sealed class RunManager
    {
        private readonly RunConfig _config;
        private readonly List<WaveResult> _results = new List<WaveResult>();
        private ArenaSim _sim;
        private int _fighterHp;

        public RunManager(RunConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.Validate();
            _config = config;
        }

        public RunConfig Config => _config;
        public RunState State { get; private set; }
        public RunEndReason EndReason { get; private set; }

        /// <summary>1-based number of the current (or last played) wave. 0 before Start().</summary>
        public int WaveNumber { get; private set; }

        public int WaveCount => _config.Waves.Count;

        /// <summary>Pot earned so far (sum of cleared wave rewards). Not yet banked.</summary>
        public int Pot { get; private set; }

        /// <summary>Pot awarded when the run ended. 0 until State == Ended.</summary>
        public int FinalPot { get; private set; }

        /// <summary>Ticks simulated in the current wave.</summary>
        public int WaveTicks { get; private set; }

        /// <summary>Total ticks simulated in the run.</summary>
        public int TotalTicks { get; private set; }

        /// <summary>Wave limit in ticks (WaveTimeLimitSeconds / FixedDt, 60 / 0.02 = 3000).</summary>
        public int WaveTimeLimitTicks =>
            (int)Math.Round(_config.WaveTimeLimitSeconds / _config.Arena.FixedDt);

        /// <summary>Fighter HP as it will be at the start of the next wave (never regenerates).</summary>
        public int FighterHp => _fighterHp;

        /// <summary>Simulation of the current wave (null before Start()). For views/debug only.</summary>
        public ArenaSim Sim => _sim;

        public IReadOnlyList<WaveResult> WaveResults => _results;

        /// <summary>P-06 telemetry: knockback displacement of all units over all finished waves.</summary>
        public float TotalPushDistance { get; private set; }

        /// <summary>P-06 telemetry: hits landed (either team) over all finished waves.</summary>
        public int TotalHits { get; private set; }

        /// <summary>P-06 telemetry: TotalPushDistance / TotalHits, 0 when nothing was hit.</summary>
        public float AveragePushDistance => TotalHits > 0 ? TotalPushDistance / TotalHits : 0f;

        /// <summary>Begins the run: Wave 1, fighter at MaxHP.</summary>
        public void Start()
        {
            if (State != RunState.NotStarted)
                throw new InvalidOperationException("Run already started.");
            _fighterHp = _config.Fighter.Stats.MaxHP;
            StartWave(1);
        }

        /// <summary>Advances the current wave by one tick. No-op unless State == InWave.</summary>
        public void Tick()
        {
            if (State != RunState.InWave) return;

            _sim.Tick();
            WaveTicks++;
            TotalTicks++;

            if (_sim.Phase == CombatPhase.Ended)
                ResolveWave(_sim.Outcome);
        }

        /// <summary>Between waves: bank the current Pot in full and end the run.</summary>
        public void Extract()
        {
            RequireDecision();
            EndRun(RunEndReason.Extracted, Pot);
        }

        /// <summary>Between waves: proceed to the next wave (HP is kept as is).</summary>
        public void Continue()
        {
            RequireDecision();
            StartWave(WaveNumber + 1);
        }

        private void RequireDecision()
        {
            if (State != RunState.AwaitingDecision)
                throw new InvalidOperationException("No Extract/Continue decision pending (state " + State + ").");
        }

        private void StartWave(int waveNumber)
        {
            WaveNumber = waveNumber;
            WaveTicks = 0;
            WaveConfig wave = _config.Waves[waveNumber - 1];
            FighterConfig f = _config.Fighter;

            _sim = new ArenaSim(_config.Arena, _config.HitRules, _config.AiRules);

            SimUnit fighter = _sim.AddUnit(_config.FighterUnitId, ArenaSim.FighterTeam,
                f.SpawnPosition, f.Stats, f.Weapon, f.Trait);
            fighter.HP = _fighterHp; // carried over, no regen

            for (int i = 0; i < wave.Enemies.Count; i++)
            {
                EnemySpawnConfig e = wave.Enemies[i];
                _sim.AddUnit(_config.FirstEnemyUnitId + i, ArenaSim.EnemyTeam, e.Position, e.Stats, e.Weapon);
            }

            _sim.StartCombat(_config.WaveTimeLimitSeconds);
            State = RunState.InWave;
        }

        private void ResolveWave(CombatOutcome outcome)
        {
            SimUnit fighter = _sim.GetUnit(_config.FighterUnitId);
            _fighterHp = Math.Max(0, fighter.HP);

            for (int i = 0; i < _sim.Units.Count; i++)
            {
                TotalPushDistance += _sim.Units[i].KnockbackDistance;
                TotalHits += _sim.Units[i].HitsTaken;
            }

            switch (outcome)
            {
                case CombatOutcome.Cleared:
                    Pot += _config.Waves[WaveNumber - 1].PotReward;
                    Record(outcome);
                    if (WaveNumber >= _config.Waves.Count)
                        EndRun(RunEndReason.ClearedAllWaves, Pot);
                    else
                        State = RunState.AwaitingDecision;
                    break;

                case CombatOutcome.Timeout:
                    Record(outcome);
                    EndRun(RunEndReason.Timeout, (int)Math.Floor(Pot * _config.TimeoutPotMultiplier));
                    break;

                default: // Defeat (ring-out or HP <= 0)
                    Record(outcome);
                    EndRun(RunEndReason.Defeat, 0);
                    break;
            }
        }

        private void Record(CombatOutcome outcome)
        {
            _results.Add(new WaveResult(WaveNumber, outcome, WaveTicks, _fighterHp, Pot));
        }

        private void EndRun(RunEndReason reason, int finalPot)
        {
            EndReason = reason;
            FinalPot = finalPot;
            State = RunState.Ended;
        }
    }
}
