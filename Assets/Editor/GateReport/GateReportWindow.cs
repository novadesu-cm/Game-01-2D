using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GotA.EditorTools
{
    /// <summary>
    /// Menu "GotA/P-07 Gate Report": reads run_log.csv (P-06) and shows the draft Prototype Gate report.
    /// Read-only with respect to the log and to gameplay code.
    /// </summary>
    public sealed class GateReportWindow : EditorWindow
    {
        private const string PathPrefKey = "GotA.GateReport.LogPath";

        private string _path;
        private string _g2Matching = "";
        private string _g2Checked = "";
        private string _g5Players = "";
        private string _g5Total = "5";

        private GateReport _report;
        private string _error;
        private Vector2 _scroll;
        private GUIStyle _monoStyle;

        [MenuItem("GotA/P-07 Gate Report")]
        public static void Open()
        {
            var window = GetWindow<GateReportWindow>("P-07 Gate Report");
            window.minSize = new Vector2(560f, 520f);
            window.Show();
        }

        private static string DefaultPath =>
            Path.Combine(Application.persistentDataPath, "Logs", "run_log.csv");

        private void OnEnable()
        {
            _path = EditorPrefs.GetString(PathPrefKey, DefaultPath);
            Recalculate();
        }

        private void OnGUI()
        {
            DrawSource();
            EditorGUILayout.Space();
            DrawManualInputs();
            EditorGUILayout.Space();
            DrawReport();
        }

        // ------------------------------------------------------------------

        private void DrawSource()
        {
            EditorGUILayout.LabelField("run_log.csv", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _path = EditorGUILayout.TextField(_path);
            if (EditorGUI.EndChangeCheck()) EditorPrefs.SetString(PathPrefKey, _path);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reload / Analyze")) Recalculate();

                if (GUILayout.Button("Browse..."))
                {
                    string dir = File.Exists(_path) ? Path.GetDirectoryName(_path) : Application.persistentDataPath;
                    string picked = EditorUtility.OpenFilePanel("Select run_log.csv", dir, "csv");
                    if (!string.IsNullOrEmpty(picked))
                    {
                        _path = picked;
                        EditorPrefs.SetString(PathPrefKey, _path);
                        Recalculate();
                    }
                }

                if (GUILayout.Button("Default path"))
                {
                    _path = DefaultPath;
                    EditorPrefs.SetString(PathPrefKey, _path);
                    Recalculate();
                }

                if (GUILayout.Button("Open folder"))
                {
                    string dir = Path.GetDirectoryName(_path);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) EditorUtility.RevealInFinder(_path);
                    else EditorUtility.RevealInFinder(Application.persistentDataPath);
                }
            }
        }

        private void DrawManualInputs()
        {
            EditorGUILayout.LabelField("Manual survey answers (leave blank = PENDING)", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("G2  losses explained correctly", GUILayout.Width(200f));
                _g2Matching = EditorGUILayout.TextField(_g2Matching, GUILayout.Width(50f));
                EditorGUILayout.LabelField("of", GUILayout.Width(18f));
                _g2Checked = EditorGUILayout.TextField(_g2Checked, GUILayout.Width(50f));
                EditorGUILayout.LabelField("losses checked", GUILayout.Width(100f));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("G5  pressed Play Again unprompted", GUILayout.Width(200f));
                _g5Players = EditorGUILayout.TextField(_g5Players, GUILayout.Width(50f));
                EditorGUILayout.LabelField("of", GUILayout.Width(18f));
                _g5Total = EditorGUILayout.TextField(_g5Total, GUILayout.Width(50f));
                EditorGUILayout.LabelField("players", GUILayout.Width(100f));
            }

            if (EditorGUI.EndChangeCheck()) Recalculate();
        }

        private void DrawReport()
        {
            if (!string.IsNullOrEmpty(_error))
                EditorGUILayout.HelpBox(_error, MessageType.Warning);

            if (_report == null) return;

            using (new EditorGUILayout.HorizontalScope())
            {
                GateRow("G1", _report.G1);
                GateRow("G2", _report.G2);
                GateRow("G3", _report.G3);
                GateRow("G4", _report.G4);
                GateRow("G5", _report.G5);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Log to Console")) Debug.Log(_report.ToText());
                if (GUILayout.Button("Copy to clipboard")) EditorGUIUtility.systemCopyBuffer = _report.ToText();
                if (GUILayout.Button("Save report (.txt)")) SaveReport();
            }

            if (_monoStyle == null)
            {
                _monoStyle = new GUIStyle(EditorStyles.textArea) { richText = false, wordWrap = false };
                Font mono = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Courier New", "Menlo" }, 12);
                if (mono != null) _monoStyle.font = mono;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.SelectableLabel(_report.ToText(), _monoStyle,
                GUILayout.ExpandHeight(true), GUILayout.MinHeight(CountLines(_report.ToText()) * 16f));
            EditorGUILayout.EndScrollView();
        }

        private static void GateRow(string name, GateStatus status)
        {
            Color old = GUI.backgroundColor;
            switch (status)
            {
                case GateStatus.Pass: GUI.backgroundColor = new Color(0.45f, 0.85f, 0.45f); break;
                case GateStatus.Fail: GUI.backgroundColor = new Color(0.95f, 0.45f, 0.45f); break;
                default: GUI.backgroundColor = new Color(0.85f, 0.85f, 0.45f); break;
            }
            GUILayout.Box(name + "\n" + GateReport.Mark(status), GUILayout.Height(40f), GUILayout.ExpandWidth(true));
            GUI.backgroundColor = old;
        }

        private static float CountLines(string text)
        {
            int n = 1;
            for (int i = 0; i < text.Length; i++) if (text[i] == '\n') n++;
            return n;
        }

        // ------------------------------------------------------------------

        private void Recalculate()
        {
            _error = null;
            _report = null;

            if (string.IsNullOrEmpty(_path) || !File.Exists(_path))
            {
                _error = "No log found at:\n" + _path +
                         "\nPlay a run first (RunLogWriter writes the file when a run ends), or use Browse...";
                // Still show an empty report so G2/G5 placeholders are visible.
                _report = GateAnalyzer.Analyze(new string[0], ReadManual(), _path);
                return;
            }

            try
            {
                // hit_log.csv and decision_log.csv sit next to the run log.
                string dir = Path.GetDirectoryName(_path) ?? string.Empty;
                string[] runLines = ReadLines(_path);
                string[] hitLines = ReadLines(Path.Combine(dir, "hit_log.csv"));
                string[] decisionLines = ReadLines(Path.Combine(dir, "decision_log.csv"));
                if (hitLines == null) _error = "hit_log.csv not found next to the run log: G1 has no data.";
                else if (decisionLines == null) _error = "decision_log.csv not found next to the run log: G3/G4 have no decision data.";

                _report = GateAnalyzer.Analyze(runLines, ReadManual(), _path, hitLines, decisionLines);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                _error = "Could not read " + _path + ": " + e.Message;
            }
        }

        /// <summary>Share-read so the log can be analysed while the game is still running. Null when missing.</summary>
        private static string[] ReadLines(string path)
        {
            if (!File.Exists(path)) return null;
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(fs))
                return reader.ReadToEnd().Split('\n');
        }

        private ManualGateInputs ReadManual()
        {
            return new ManualGateInputs
            {
                G2MatchingLosses = ParseInt(_g2Matching),
                G2CheckedLosses = ParseInt(_g2Checked),
                G5PlayAgainPlayers = ParseInt(_g5Players),
                G5TotalPlayers = ParseInt(_g5Total) ?? 5
            };
        }

        private static int? ParseInt(string s) =>
            int.TryParse(s, out int v) && v >= 0 ? v : (int?)null;

        private void SaveReport()
        {
            string suggested = "P07_GateReport.txt";
            string target = EditorUtility.SaveFilePanel("Save gate report", Path.GetDirectoryName(Application.dataPath), suggested, "txt");
            if (string.IsNullOrEmpty(target)) return;
            File.WriteAllText(target, _report.ToText());
            Debug.Log("Gate report saved: " + target);
        }
    }
}
