using GotA.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace GotA.Client.UI
{
    /// <summary>
    /// Minimal HUD: fighter HP, wave timer, current Pot. Polls RunManager / ArenaSim in Update()
    /// (read-only; never writes to the simulation).
    /// </summary>
    public sealed class HudView : SessionScreen
    {
        [SerializeField] private Text hpText;
        [SerializeField] private Text waveTimerText;
        [SerializeField] private Text potText;
        [SerializeField] private Text waveText;

        // Shown during the wave and while the Decision overlay is up (so HP/Pot stay visible).
        protected override bool IsVisibleIn(SessionPhase phase) =>
            phase == SessionPhase.InWave || phase == SessionPhase.Decision;

        protected override void OnShown() => Poll();

        private void Update()
        {
            if (IsShown) Poll();
        }

        private void Poll()
        {
            RunManager run = session.ActiveRun;
            ArenaSim sim = run?.Sim;
            if (run == null || sim == null) return;

            SimUnit fighter = sim.GetUnit(run.Config.FighterUnitId);
            if (hpText != null && fighter != null)
                hpText.text = "HP " + Mathf.Max(0, fighter.HP) + " / " + fighter.MaxHP;

            if (waveTimerText != null)
                waveTimerText.text = "Time " + Mathf.CeilToInt(Mathf.Max(0f, sim.WaveTimer)) + "s";

            if (potText != null) potText.text = "Pot " + run.Pot;

            if (waveText != null) waveText.text = "Wave " + run.WaveNumber + " / " + run.WaveCount;
        }
    }
}
