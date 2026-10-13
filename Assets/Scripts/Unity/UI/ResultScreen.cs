using UnityEngine;
using UnityEngine.UI;

namespace GotA.Client.UI
{
    /// <summary>Result: final Pot, Best Pot, one line with the cause of death / end, Play Again.</summary>
    public sealed class ResultScreen : SessionScreen
    {
        [SerializeField] private Text finalPotText;
        [SerializeField] private Text bestPotText;
        [SerializeField] private Text causeText;
        [SerializeField] private Button playAgainButton;

        protected override bool IsVisibleIn(SessionPhase phase) => phase == SessionPhase.Result;

        protected override void OnEnable()
        {
            if (playAgainButton != null) playAgainButton.onClick.AddListener(OnPlayAgain);
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (playAgainButton != null) playAgainButton.onClick.RemoveListener(OnPlayAgain);
        }

        protected override void OnShown()
        {
            RunSummary summary = session.LastSummary;
            if (summary == null) return;

            if (finalPotText != null) finalPotText.text = "Final Pot: " + summary.FinalPot;
            if (bestPotText != null)
                bestPotText.text = "Best Pot: " + session.BestPot + (summary.IsNewBest ? "  (new best!)" : string.Empty);
            if (causeText != null) causeText.text = summary.CauseLine;
        }

        private void OnPlayAgain() => session.PlayAgain();
    }
}
