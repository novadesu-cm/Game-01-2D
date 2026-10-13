using System;
using System.Collections.Generic;
using GotA.Client;
using GotA.Client.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GotA.EditorTools
{
    /// <summary>
    /// Menu "GotA/Build Prototype Scene": (re)creates Assets/Scenes/Prototype.unity with camera,
    /// GameSessionManager (all configs assigned), the four screens, ArenaView and RunLogWriter,
    /// wires every reference and registers the scene as Build Settings scene 0.
    /// Setup only: no gameplay values are written here (configs come from CreateDefaultConfigs).
    /// </summary>
    public static class BuildPrototypeScene
    {
        public const string ScenePath = "Assets/Scenes/Prototype.unity";
        public const string OptionButtonPrefabPath = "Assets/Prefabs/UI/OptionButton.prefab";
        private const string ConfigFolder = "Assets/Data/Configs";

        // Spec section 1: ArenaRadius = 6.0. Ortho size is the half-height, so 7 shows the whole
        // radius with a 1 u margin on the short axis of a landscape window.
        public const float CameraOrthoSize = 7f;

        private static readonly Color PanelColor = new Color(0.05f, 0.05f, 0.08f, 0.85f);
        private static readonly Color ButtonColor = new Color(0.25f, 0.28f, 0.38f, 1f);
        private static Font _font;

        [MenuItem("GotA/Build Prototype Scene")]
        public static void Build()
        {
            // 1. Configs (never overwrites existing assets).
            CreateDefaultConfigs.Create();

            // 2. Scene. (Load assets AFTER NewScene: switching scenes unloads unreferenced assets.)
            EnsureFolder("Assets/Scenes");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var runSetup = Load<RunSetupSO>("RunSetup");
            var weapons = new[]
            {
                Load<WeaponConfigSO>("Weapon_Fists"),
                Load<WeaponConfigSO>("Weapon_GiantHammer"),
                Load<WeaponConfigSO>("Weapon_DualDaggers")
            };
            var traits = new[]
            {
                Load<TraitConfigSO>("Trait_None"),
                Load<TraitConfigSO>("Trait_Lifesteal"),
                Load<TraitConfigSO>("Trait_ThickSkin")
            };

            BuildCamera();

            GameSessionManager session = new GameObject("GameSessionManager").AddComponent<GameSessionManager>();
            SerializedObject sessionSo = new SerializedObject(session);
            SetRef(sessionSo, "runSetup", runSetup);
            SetRefArray(sessionSo, "weapons", weapons);
            SetRefArray(sessionSo, "traits", traits);
            sessionSo.ApplyModifiedPropertiesWithoutUndo();

            Button optionButtonPrefab = BuildOptionButtonPrefab();

            Canvas canvas = BuildCanvas();
            BuildHud(canvas.transform, session);
            BuildBuildSelection(canvas.transform, session, optionButtonPrefab);
            BuildDecisionOverlay(canvas.transform, session);
            BuildResultScreen(canvas.transform, session);
            BuildEventSystem();

            ArenaView arena = new GameObject("ArenaView").AddComponent<ArenaView>();
            var arenaSo = new SerializedObject(arena);
            SetRef(arenaSo, "session", session);
            arenaSo.ApplyModifiedPropertiesWithoutUndo();

            RunLogWriter logWriter = new GameObject("RunLogWriter").AddComponent<RunLogWriter>();
            var logSo = new SerializedObject(logWriter);
            SetRef(logSo, "session", session);
            logSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Could not save " + ScenePath);

            // 3. Build Settings: Prototype is scene 0.
            SetAsFirstBuildScene(ScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("GotA: built " + ScenePath + " and set it as Build Settings scene 0.");
        }

        // ------------------------------------------------------------------
        // Scene objects
        // ------------------------------------------------------------------

        private static void BuildCamera()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
            cam.orthographic = true;
            cam.orthographicSize = CameraOrthoSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.06f, 0.09f);
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 50f;
            go.transform.position = new Vector3(0f, 0f, -10f); // ArenaView maps sim (x, y) -> world (x, y, 0)
        }

        private static Canvas BuildCanvas()
        {
            var go = new GameObject("Canvas", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static void BuildEventSystem()
        {
            // Project uses the Input System package only (activeInputHandler = 1).
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        private static void BuildHud(Transform canvas, GameSessionManager session)
        {
            RectTransform screenRt = NewRect("HudView", canvas);
            Stretch(screenRt);
            RectTransform root = NewRect("Root", screenRt);
            Stretch(root);

            Text wave = NewText("WaveText", root, "Wave 1 / 3", 36, TextAnchor.UpperLeft);
            Anchor(wave.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -30), new Vector2(430, -90));
            Text hp = NewText("HpText", root, "HP 120 / 120", 36, TextAnchor.UpperLeft);
            Anchor(hp.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -90), new Vector2(530, -150));
            Text time = NewText("WaveTimerText", root, "Time 60s", 36, TextAnchor.UpperRight);
            Anchor(time.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-430, -30), new Vector2(-30, -90));
            Text pot = NewText("PotText", root, "Pot 0", 36, TextAnchor.UpperRight);
            Anchor(pot.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-430, -90), new Vector2(-30, -150));

            HudView hud = screenRt.gameObject.AddComponent<HudView>();
            var so = new SerializedObject(hud);
            SetRef(so, "session", session);
            SetRef(so, "root", root.gameObject);
            SetRef(so, "hpText", hp);
            SetRef(so, "waveTimerText", time);
            SetRef(so, "potText", pot);
            SetRef(so, "waveText", wave);
            so.ApplyModifiedPropertiesWithoutUndo();
            root.gameObject.SetActive(false);
        }

        private static void BuildBuildSelection(Transform canvas, GameSessionManager session, Button optionPrefab)
        {
            RectTransform screenRt = NewRect("BuildSelectionScreen", canvas);
            Stretch(screenRt);
            RectTransform root = NewPanel("Root", screenRt);
            Stretch(root);

            Text title = NewText("Title", root, "God of the Arena - Choose your build", 56, TextAnchor.MiddleCenter);
            Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(40, -170), new Vector2(-40, -60));
            Text best = NewText("BestPotText", root, "Best Pot: 0", 40, TextAnchor.MiddleCenter);
            Anchor(best.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(40, -250), new Vector2(-40, -180));

            Text weaponHeader = NewText("WeaponHeader", root, "Weapon", 40, TextAnchor.MiddleLeft);
            Anchor(weaponHeader.rectTransform, new Vector2(0.05f, 0.7f), new Vector2(0.48f, 0.76f), Vector2.zero, Vector2.zero);
            RectTransform weaponContainer = NewList("WeaponContainer", root);
            Anchor(weaponContainer, new Vector2(0.05f, 0.30f), new Vector2(0.48f, 0.70f), Vector2.zero, Vector2.zero);

            Text traitHeader = NewText("TraitHeader", root, "Trait", 40, TextAnchor.MiddleLeft);
            Anchor(traitHeader.rectTransform, new Vector2(0.52f, 0.7f), new Vector2(0.95f, 0.76f), Vector2.zero, Vector2.zero);
            RectTransform traitContainer = NewList("TraitContainer", root);
            Anchor(traitContainer, new Vector2(0.52f, 0.30f), new Vector2(0.95f, 0.70f), Vector2.zero, Vector2.zero);

            Button start = NewButton("StartButton", root, "Start Run", 44);
            Anchor((RectTransform)start.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-200, 60), new Vector2(200, 160));

            BuildSelectionScreen screen = screenRt.gameObject.AddComponent<BuildSelectionScreen>();
            var so = new SerializedObject(screen);
            SetRef(so, "session", session);
            SetRef(so, "root", root.gameObject);
            SetRef(so, "weaponContainer", weaponContainer);
            SetRef(so, "traitContainer", traitContainer);
            SetRef(so, "optionButtonPrefab", optionPrefab);
            SetRef(so, "startButton", start);
            SetRef(so, "bestPotText", best);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildDecisionOverlay(Transform canvas, GameSessionManager session)
        {
            RectTransform screenRt = NewRect("DecisionOverlay", canvas);
            Stretch(screenRt);
            RectTransform root = NewPanel("Root", screenRt);
            Anchor(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-450, -220), new Vector2(450, 220));

            Text info = NewText("InfoText", root, "Wave cleared!", 38, TextAnchor.MiddleCenter);
            Anchor(info.rectTransform, new Vector2(0, 0.35f), new Vector2(1, 1), new Vector2(30, 0), new Vector2(-30, -30));
            Button extract = NewButton("ExtractButton", root, "Extract", 40);
            Anchor((RectTransform)extract.transform, new Vector2(0.05f, 0f), new Vector2(0.48f, 0f), new Vector2(0, 40), new Vector2(0, 140));
            Button cont = NewButton("ContinueButton", root, "Continue", 40);
            Anchor((RectTransform)cont.transform, new Vector2(0.52f, 0f), new Vector2(0.95f, 0f), new Vector2(0, 40), new Vector2(0, 140));

            DecisionOverlay overlay = screenRt.gameObject.AddComponent<DecisionOverlay>();
            var so = new SerializedObject(overlay);
            SetRef(so, "session", session);
            SetRef(so, "root", root.gameObject);
            SetRef(so, "extractButton", extract);
            SetRef(so, "continueButton", cont);
            SetRef(so, "infoText", info);
            so.ApplyModifiedPropertiesWithoutUndo();
            root.gameObject.SetActive(false);
        }

        private static void BuildResultScreen(Transform canvas, GameSessionManager session)
        {
            RectTransform screenRt = NewRect("ResultScreen", canvas);
            Stretch(screenRt);
            RectTransform root = NewPanel("Root", screenRt);
            Stretch(root);

            Text title = NewText("Title", root, "Run Over", 64, TextAnchor.MiddleCenter);
            Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(40, -240), new Vector2(-40, -100));
            Text finalPot = NewText("FinalPotText", root, "Final Pot: 0", 52, TextAnchor.MiddleCenter);
            Anchor(finalPot.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(40, 40), new Vector2(-40, 140));
            Text bestPot = NewText("BestPotText", root, "Best Pot: 0", 44, TextAnchor.MiddleCenter);
            Anchor(bestPot.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(40, -40), new Vector2(-40, 40));
            Text cause = NewText("CauseText", root, "", 38, TextAnchor.MiddleCenter);
            Anchor(cause.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(40, -140), new Vector2(-40, -50));
            Button playAgain = NewButton("PlayAgainButton", root, "Play Again", 44);
            Anchor((RectTransform)playAgain.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-200, 60), new Vector2(200, 160));

            ResultScreen screen = screenRt.gameObject.AddComponent<ResultScreen>();
            var so = new SerializedObject(screen);
            SetRef(so, "session", session);
            SetRef(so, "root", root.gameObject);
            SetRef(so, "finalPotText", finalPot);
            SetRef(so, "bestPotText", bestPot);
            SetRef(so, "causeText", cause);
            SetRef(so, "playAgainButton", playAgain);
            so.ApplyModifiedPropertiesWithoutUndo();
            root.gameObject.SetActive(false);
        }

        /// <summary>Prefab cloned by BuildSelectionScreen once per weapon / trait (Button + child Text).</summary>
        private static Button BuildOptionButtonPrefab()
        {
            EnsureFolder("Assets/Prefabs");
            EnsureFolder("Assets/Prefabs/UI");

            var temp = new GameObject("OptionButton", typeof(RectTransform));
            var image = temp.AddComponent<Image>();
            image.color = ButtonColor;
            var button = temp.AddComponent<Button>();
            button.targetGraphic = image;
            var layout = temp.AddComponent<LayoutElement>();
            layout.preferredHeight = 90f;
            layout.minHeight = 90f;

            Text label = NewText("Text", temp.transform, "Option", 30, TextAnchor.MiddleLeft);
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(20f, 0f);
            label.rectTransform.offsetMax = new Vector2(-20f, 0f);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(temp, OptionButtonPrefabPath);
            UnityEngine.Object.DestroyImmediate(temp);
            if (saved == null) throw new InvalidOperationException("Could not save " + OptionButtonPrefabPath);
            return saved.GetComponent<Button>();
        }

        // ------------------------------------------------------------------
        // UI helpers
        // ------------------------------------------------------------------

        private static Font GetFont()
        {
            if (_font != null) return _font;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null) _font = Font.CreateDynamicFontFromOSFont("Arial", 16);
            return _font;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static RectTransform NewPanel(string name, Transform parent)
        {
            RectTransform rt = NewRect(name, parent);
            rt.gameObject.AddComponent<Image>().color = PanelColor;
            return rt;
        }

        private static RectTransform NewList(string name, Transform parent)
        {
            RectTransform rt = NewRect(name, parent);
            var group = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            group.spacing = 12f;
            group.childAlignment = TextAnchor.UpperCenter;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            return rt;
        }

        private static Text NewText(string name, Transform parent, string content, int size, TextAnchor anchor)
        {
            RectTransform rt = NewRect(name, parent);
            var text = rt.gameObject.AddComponent<Text>();
            text.font = GetFont();
            text.text = content;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = Color.white;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static Button NewButton(string name, Transform parent, string label, int fontSize)
        {
            RectTransform rt = NewRect(name, parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = ButtonColor;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            Text text = NewText("Text", rt, label, fontSize, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);
            return button;
        }

        private static void Stretch(RectTransform rt) =>
            Anchor(rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        private static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        // ------------------------------------------------------------------
        // Serialization / asset helpers
        // ------------------------------------------------------------------

        private static void SetRef(SerializedObject so, string field, UnityEngine.Object value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
                throw new InvalidOperationException(so.targetObject.GetType().Name + " has no serialized field '" + field + "'.");
            if (value == null)
                throw new InvalidOperationException(so.targetObject.GetType().Name + "." + field + ": value to assign is null.");
            p.objectReferenceValue = value;
        }

        private static void SetRefArray(SerializedObject so, string field, UnityEngine.Object[] values)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null || !p.isArray)
                throw new InvalidOperationException(so.targetObject.GetType().Name + " has no serialized array '" + field + "'.");
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == null)
                    throw new InvalidOperationException(so.targetObject.GetType().Name + "." + field + "[" + i + "]: asset is null.");
                p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        private static T Load<T>(string file) where T : UnityEngine.Object
        {
            string path = ConfigFolder + "/" + file + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException("Missing config asset: " + path);
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }

        private static void SetAsFirstBuildScene(string path)
        {
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(path, true) };
            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
                if (existing.path != path) scenes.Add(existing);
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
