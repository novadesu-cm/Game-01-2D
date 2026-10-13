using GotA.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace GotA.Client.UI
{
    /// <summary>
    /// Shown when a wave is cleared and more waves remain: Extract (bank Pot, end run) or Continue.
    /// The buttons call GameSessionManager.Extract/Continue, which call RunManager.Extract/Continue
    /// and update the session phase.
    /// </summary>
    public sealed class DecisionOverlay : SessionScreen
    {
        [SerializeField] private Button extractButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Text infoText;

        protected override bool IsVisibleIn(SessionPhase phase) => phase == SessionPhase.Decision;

        protected override void OnEnable()
        {
            if (extractButton != null) extractButton.onClick.AddListener(OnExtract);
            if (continueButton != null) continueButton.onClick.AddListener(OnContinue);
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (extractButton != null) extractButton.onClick.RemoveListener(OnExtract);
            if (continueButton != null) continueButton.onClick.RemoveListener(OnContinue);
        }

        protected override void OnShown()
        {
            RunManager run = session.ActiveRun;
            if (infoText == null || run == null || run.State != RunState.AwaitingDecision) return;

            infoText.text = "Wave " + run.WaveNumber + " cleared!  Pot: " + run.Pot +
                            "\nExtract to bank it, or Continue to Wave " + (run.WaveNumber + 1) + ".";
        }

        private void OnExtract() => session.Extract();

        private void OnContinue() => session.Continue();
    }
}
