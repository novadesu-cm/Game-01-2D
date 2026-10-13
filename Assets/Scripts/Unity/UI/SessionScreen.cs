using UnityEngine;

namespace GotA.Client.UI
{
    /// <summary>
    /// Base for the placeholder screens. Listens to GameSessionManager.PhaseChanged and shows/hides
    /// <see cref="root"/>. This script must sit on an object that stays active (a parent of root),
    /// otherwise hiding root would also stop the event subscription.
    /// </summary>
    public abstract class SessionScreen : MonoBehaviour
    {
        [SerializeField] protected GameSessionManager session;
        [Tooltip("Child object toggled by this screen. Must NOT be the object this script is on.")]
        [SerializeField] protected GameObject root;

        protected bool IsShown { get; private set; }

        protected abstract bool IsVisibleIn(SessionPhase phase);

        /// <summary>Called every time the screen becomes visible (refresh texts here).</summary>
        protected virtual void OnShown() { }

        protected virtual void OnEnable()
        {
            if (session == null)
            {
                Debug.LogError(GetType().Name + ": session is not assigned.", this);
                return;
            }
            session.PhaseChanged += HandlePhaseChanged;
            HandlePhaseChanged(session.Phase);
        }

        protected virtual void OnDisable()
        {
            if (session != null) session.PhaseChanged -= HandlePhaseChanged;
        }

        private void HandlePhaseChanged(SessionPhase phase)
        {
            IsShown = IsVisibleIn(phase);
            if (root != null) root.SetActive(IsShown);
            if (IsShown) OnShown();
        }
    }
}
