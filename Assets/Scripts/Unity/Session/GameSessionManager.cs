using System;
using GotA.Simulation;
using UnityEngine;

namespace GotA.Client
{
    public enum SessionPhase
    {
        BuildSelect = 0,
        InWave = 1,
        Decision = 2,
        Result = 3
    }

    /// <summary>
    /// Unity-side owner of the play session: build choice, the active RunManager, the fixed-step
    /// tick loop, phase changes for the UI, and Best Pot across runs.
    /// Depends on GotA.Simulation, never the other way round.
    /// Flow: BuildSelect -> InWave -> (Decision -> InWave)* -> Result -> BuildSelect.
    /// </summary>
    public sealed class GameSessionManager : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private RunSetupSO runSetup;
        [SerializeField] private WeaponConfigSO[] weapons;
        [SerializeField] private TraitConfigSO[] traits;

        [Header("Seed (D-03)")]
        [SerializeField] private bool useFixedSeed;
        [SerializeField] private int fixedSeed = 12345;

        [Header("Tick loop")]
        [Tooltip("Safety cap so a long frame cannot trigger a spiral of catch-up ticks.")]
        [SerializeField, Min(1)] private int maxTicksPerFrame = 10;

        private readonly BestPotTracker _bestPot = new BestPotTracker();
        private float _accumulator;
        private int _weaponIndex;
        private int _traitIndex;

        /// <summary>Raised after every Phase change (Decision -> InWave on Continue included).</summary>
        public event Action<SessionPhase> PhaseChanged;

        /// <summary>Raised once when a run ends, after Best Pot was updated.</summary>
        public event Action<RunSummary> RunEnded;

        public SessionPhase Phase { get; private set; } = SessionPhase.BuildSelect;

        /// <summary>The run in progress (or just finished). Null in BuildSelect. Poll ActiveRun.Sim for HUD data.</summary>
        public RunManager ActiveRun { get; private set; }

        public ArenaSim ActiveSim => ActiveRun?.Sim;

        public RunSummary LastSummary { get; private set; }

        public int BestPot => _bestPot.BestPot;

        /// <summary>Seed of the current / last run (log it for D-03).</summary>
        public int LastSeed { get; private set; }

        public WeaponConfigSO[] Weapons => weapons;
        public TraitConfigSO[] Traits => traits;
        public RunSetupSO RunSetup => runSetup;

        public int WeaponIndex => _weaponIndex;
        public int TraitIndex => _traitIndex;

        public WeaponConfigSO SelectedWeapon =>
            weapons != null && _weaponIndex < weapons.Length ? weapons[_weaponIndex] : null;

        public TraitConfigSO SelectedTrait =>
            traits != null && _traitIndex < traits.Length ? traits[_traitIndex] : null;

        // ------------------------------------------------------------------
        // Build selection (free choice, no budget)
        // ------------------------------------------------------------------

        public void SelectWeapon(int index)
        {
            if (Phase != SessionPhase.BuildSelect) return;
            if (weapons == null || index < 0 || index >= weapons.Length) return;
            _weaponIndex = index;
        }

        public void SelectTrait(int index)
        {
            if (Phase != SessionPhase.BuildSelect) return;
            if (traits == null || index < 0 || index >= traits.Length) return;
            _traitIndex = index;
        }

        /// <summary>Builds the RunConfig from the selected Weapon + Trait and starts Wave 1.</summary>
        public void StartRun()
        {
            if (Phase != SessionPhase.BuildSelect) return;
            if (runSetup == null || SelectedWeapon == null)
            {
                Debug.LogError("GameSessionManager: assign a RunSetup and at least one Weapon.", this);
                return;
            }

            LastSeed = useFixedSeed ? fixedSeed : UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            Debug.Log("Run seed: " + LastSeed + " | build: " + SelectedWeapon.DisplayName + " + " +
                      (SelectedTrait != null ? SelectedTrait.DisplayName : "None"));

            RunConfig config = DtoConverter.BuildRunConfig(runSetup, SelectedWeapon, SelectedTrait, LastSeed);
            ActiveRun = new RunManager(config);
            ActiveRun.Start();
            LastSummary = null;
            _accumulator = 0f;
            SetPhase(SessionPhase.InWave);
        }

        // ------------------------------------------------------------------
        // Between waves
        // ------------------------------------------------------------------

        /// <summary>Decision overlay: bank the Pot and end the run.</summary>
        public void Extract()
        {
            if (Phase != SessionPhase.Decision) return;
            ActiveRun.Extract();
            SyncPhaseWithRun();
        }

        /// <summary>Decision overlay: start the next wave.</summary>
        public void Continue()
        {
            if (Phase != SessionPhase.Decision) return;
            ActiveRun.Continue();
            _accumulator = 0f;
            SetPhase(SessionPhase.InWave);
        }

        /// <summary>Result screen: back to build selection (Best Pot is kept).</summary>
        public void PlayAgain()
        {
            if (Phase != SessionPhase.Result) return;
            ActiveRun = null;
            SetPhase(SessionPhase.BuildSelect);
        }

        // ------------------------------------------------------------------
        // Tick loop
        // ------------------------------------------------------------------

        private void Update()
        {
            if (Phase != SessionPhase.InWave || ActiveRun == null) return;

            float dt = ActiveRun.Config.Arena.FixedDt;
            _accumulator += Time.deltaTime;

            int steps = 0;
            while (_accumulator >= dt && ActiveRun.State == RunState.InWave)
            {
                if (steps >= maxTicksPerFrame)
                {
                    _accumulator = 0f; // drop the backlog instead of spiralling
                    break;
                }
                ActiveRun.Tick();
                _accumulator -= dt;
                steps++;
            }

            SyncPhaseWithRun();
        }

        private void SyncPhaseWithRun()
        {
            if (ActiveRun == null) return;

            switch (ActiveRun.State)
            {
                case RunState.AwaitingDecision:
                    if (Phase != SessionPhase.Decision) SetPhase(SessionPhase.Decision);
                    break;

                case RunState.Ended:
                    if (Phase != SessionPhase.Result) FinishRun();
                    break;
            }
        }

        private void FinishRun()
        {
            RunSummary summary = RunSummary.Build(ActiveRun, UnitDisplayName);
            summary.IsNewBest = _bestPot.Submit(summary.FinalPot);
            LastSummary = summary;

            Debug.Log("Run ended: " + summary.Reason + " | Pot " + summary.FinalPot +
                      " | Best " + _bestPot.BestPot + " | " + summary.CauseLine);

            SetPhase(SessionPhase.Result);
            RunEnded?.Invoke(summary);
        }

        private void SetPhase(SessionPhase phase)
        {
            Phase = phase;
            PhaseChanged?.Invoke(phase);
        }

        /// <summary>Maps an enemy UnitId of the current wave back to its EnemyConfigSO name.</summary>
        private string UnitDisplayName(int unitId)
        {
            if (runSetup == null || ActiveRun == null) return null;

            int waveIndex = ActiveRun.WaveNumber - 1;
            int enemyIndex = unitId - ActiveRun.Config.FirstEnemyUnitId;
            if (waveIndex < 0 || waveIndex >= runSetup.waves.Count) return null;

            var enemies = runSetup.waves[waveIndex].enemies;
            if (enemyIndex < 0 || enemyIndex >= enemies.Count || enemies[enemyIndex].enemy == null) return null;
            return enemies[enemyIndex].enemy.DisplayName;
        }
    }
}
