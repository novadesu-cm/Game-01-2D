using System;
using System.IO;
using UnityEngine;

namespace GotA.Client
{
    /// <summary>
    /// P-06: listens to GameSessionManager and appends one CSV row when a run ends.
    /// Decision time = real seconds between the Decision overlay opening and the player's choice.
    /// Never writes to the simulation. Keep this on an always-active object (like the session).
    /// File: &lt;persistentDataPath&gt;/Logs/run_log.csv unless overridden.
    /// </summary>
    public sealed class RunLogWriter : MonoBehaviour
    {
        [SerializeField] private GameSessionManager session;
        [Tooltip("Empty = Application.persistentDataPath/Logs/<fileName>.")]
        [SerializeField] private string overridePath = "";
        [SerializeField] private string fileName = "run_log.csv";

        private RunLogRecorder _recorder;
        private SessionPhase _previousPhase = SessionPhase.BuildSelect;

        public string LogPath =>
            !string.IsNullOrEmpty(overridePath)
                ? overridePath
                : Path.Combine(Application.persistentDataPath, "Logs", fileName);

        private void OnEnable()
        {
            if (session == null) session = FindAnyObjectByType<GameSessionManager>();
            if (session == null)
            {
                Debug.LogError("RunLogWriter: no GameSessionManager found.", this);
                return;
            }
            _previousPhase = session.Phase;
            session.PhaseChanged += HandlePhaseChanged;
            session.RunEnded += HandleRunEnded;
        }

        private void OnDisable()
        {
            if (session == null) return;
            session.PhaseChanged -= HandlePhaseChanged;
            session.RunEnded -= HandleRunEnded;
        }

        private void HandlePhaseChanged(SessionPhase phase)
        {
            float now = Time.realtimeSinceStartup;

            switch (phase)
            {
                case SessionPhase.InWave:
                    if (_previousPhase == SessionPhase.BuildSelect)
                    {
                        _recorder = new RunLogRecorder(
                            session.LastSeed,
                            session.SelectedWeapon != null ? session.SelectedWeapon.DisplayName : null,
                            session.SelectedTrait != null ? session.SelectedTrait.DisplayName : null);
                    }
                    else if (_previousPhase == SessionPhase.Decision)
                    {
                        _recorder?.EndDecision(true, now); // Continue
                    }
                    break;

                case SessionPhase.Decision:
                    _recorder?.BeginDecision(session.ActiveRun != null ? session.ActiveRun.WaveNumber : 0, now);
                    break;

                case SessionPhase.Result:
                    if (_previousPhase == SessionPhase.Decision)
                        _recorder?.EndDecision(false, now); // Extract
                    break;
            }

            _previousPhase = phase;
        }

        private void HandleRunEnded(RunSummary summary)
        {
            if (_recorder == null || session.ActiveRun == null) return;

            try
            {
                RunLogRecord record = _recorder.Finish(session.ActiveRun, summary, session.BestPot);
                RunLogCsv.Append(LogPath, record);
                Debug.Log("Run log written: " + LogPath + " (seed " + record.Seed + ")");
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning("RunLogWriter: could not write " + LogPath + ": " + e.Message, this);
            }
            finally
            {
                _recorder = null;
            }
        }
    }
}
