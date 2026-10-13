using System.Collections.Generic;
using System.Linq;
using GotA.Client;
using GotA.Client.UI;
using GotA.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GotA.Tests
{
    /// <summary>
    /// P-07 setup: opens Assets/Scenes/Prototype.unity (no Play mode) and checks that every serialized
    /// object reference on the session / UI / view / log objects is assigned, and that the wiring is consistent.
    /// If this fails because the scene is missing or stale, run the menu GotA/Build Prototype Scene.
    /// </summary>
    public class PrototypeSceneTests
    {
        private SceneSetup[] _previousSetup;
        private Scene _scene;

        [OneTimeSetUp]
        public void OpenPrototypeScene()
        {
            _previousSetup = EditorSceneManager.GetSceneManagerSetup();
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(BuildPrototypeScene.ScenePath),
                BuildPrototypeScene.ScenePath + " does not exist. Run GotA/Build Prototype Scene.");
            _scene = EditorSceneManager.OpenScene(BuildPrototypeScene.ScenePath, OpenSceneMode.Single);
            Assert.IsTrue(_scene.isLoaded);
        }

        [OneTimeTearDown]
        public void RestoreScenes()
        {
            if (_previousSetup != null && _previousSetup.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(_previousSetup);
            else
                EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        }

        private static T Single<T>() where T : Component
        {
            T[] found = Object.FindObjectsByType<T>(FindObjectsInactive.Include);
            Assert.AreEqual(1, found.Length, "Expected exactly one " + typeof(T).Name + " in Prototype.unity");
            return found[0];
        }

        private static IEnumerable<Component> ObjectsToCheck()
        {
            yield return Single<GameSessionManager>();
            yield return Single<BuildSelectionScreen>();
            yield return Single<HudView>();
            yield return Single<DecisionOverlay>();
            yield return Single<ResultScreen>();
            yield return Single<ArenaView>();
            yield return Single<RunLogWriter>();
        }

        [Test]
        public void EveryObjectReference_OnPrototypeObjects_IsAssigned()
        {
            int checkedReferences = 0;

            foreach (Component component in ObjectsToCheck())
            {
                string owner = component.GetType().Name + " on '" + component.gameObject.name + "'";
                Assert.AreEqual(0, GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(component.gameObject),
                    owner + ": GameObject has a missing script.");

                var so = new SerializedObject(component);
                SerializedProperty it = so.GetIterator();
                // NextVisible skips Unity's hidden prefab bookkeeping refs (m_CorrespondingSourceObject etc.,
                // legitimately null on scene objects) but still covers m_Script and every [SerializeField].
                while (it.NextVisible(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                    checkedReferences++;
                    Assert.IsNotNull(it.objectReferenceValue, owner + ": '" + it.propertyPath + "' is null/missing.");
                }
            }

            // GameSessionManager 1 + 3 + 3, BuildSelection 2 + 5, Hud 2 + 4, Decision 2 + 3, Result 2 + 4,
            // ArenaView 1, RunLogWriter 1 (+ m_Script on each) -> guard against a silently empty check.
            Assert.GreaterOrEqual(checkedReferences, 30, "Suspiciously few references were inspected.");
        }

        [Test]
        public void GameSessionManager_HasAllConfigs()
        {
            GameSessionManager session = Single<GameSessionManager>();

            Assert.IsNotNull(session.RunSetup);
            Assert.AreEqual(new[] { "Fists", "Giant Hammer", "Dual Daggers" },
                session.Weapons.Select(w => w.DisplayName).ToArray());
            Assert.AreEqual(new[] { "None", "Lifesteal", "Thick Skin" },
                session.Traits.Select(t => t.DisplayName).ToArray());
        }

        [Test]
        public void AllScreens_ReferenceTheSameSession_AndRootIsAChild()
        {
            GameSessionManager session = Single<GameSessionManager>();

            var screens = new SessionScreen[]
            {
                Single<BuildSelectionScreen>(), Single<HudView>(), Single<DecisionOverlay>(), Single<ResultScreen>()
            };

            foreach (SessionScreen screen in screens)
            {
                var so = new SerializedObject(screen);
                Assert.AreSame(session, so.FindProperty("session").objectReferenceValue, screen.GetType().Name + ".session");

                var root = (GameObject)so.FindProperty("root").objectReferenceValue;
                Assert.AreNotSame(screen.gameObject, root, screen.GetType().Name + ".root must not be the screen object");
                Assert.IsTrue(root.transform.IsChildOf(screen.transform), screen.GetType().Name + ".root must be a child of the screen");
            }

            Assert.AreSame(session, new SerializedObject(Single<ArenaView>()).FindProperty("session").objectReferenceValue);
            Assert.AreSame(session, new SerializedObject(Single<RunLogWriter>()).FindProperty("session").objectReferenceValue);
        }

        [Test]
        public void MainCamera_IsOrthographic_AndShowsArenaRadius()
        {
            Camera cam = Camera.main;
            Assert.IsNotNull(cam, "No camera tagged MainCamera");
            Assert.IsTrue(cam.orthographic);

            const float arenaRadius = 6.0f; // spec section 1
            // Ortho size = half the visible height; width is larger in any landscape window.
            Assert.GreaterOrEqual(cam.orthographicSize, arenaRadius, "Arena radius must fit vertically");
            Assert.AreEqual(0f, cam.transform.position.x, 0.001f);
            Assert.AreEqual(0f, cam.transform.position.y, 0.001f);
            Assert.Less(cam.transform.position.z, 0f);
        }

        [Test]
        public void CanvasHasEventSystem()
        {
            Assert.IsNotNull(Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>(),
                "Buttons need an EventSystem");
        }

        [Test]
        public void PrototypeIsBuildSettingsSceneZero()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            Assert.IsTrue(scenes.Length > 0);
            Assert.AreEqual(BuildPrototypeScene.ScenePath, scenes[0].path);
            Assert.IsTrue(scenes[0].enabled);
        }

        [Test]
        public void DefaultConfigAssets_Exist_ForSpecSections2And6()
        {
            string[] names =
            {
                "Weapon_Fists", "Weapon_GiantHammer", "Weapon_DualDaggers", "Weapon_GruntAttack", "Weapon_BruteAttack",
                "Trait_None", "Trait_Lifesteal", "Trait_ThickSkin",
                "Enemy_Grunt", "Enemy_Brute", "RunSetup"
            };
            foreach (string n in names)
                Assert.IsNotNull(AssetDatabase.LoadMainAssetAtPath("Assets/Data/Configs/" + n + ".asset"), n + ".asset is missing");

            var setup = AssetDatabase.LoadAssetAtPath<RunSetupSO>("Assets/Data/Configs/RunSetup.asset");
            Assert.AreEqual(3, setup.waves.Count);
            Assert.AreEqual(new[] { 2, 2, 3 }, setup.waves.Select(w => w.enemies.Count).ToArray());
            foreach (WaveSetup wave in setup.waves)
                foreach (EnemySpawnSetup spawn in wave.enemies)
                {
                    Assert.IsNotNull(spawn.enemy, "wave enemy reference is null");
                    Assert.IsNotNull(spawn.enemy.weapon, spawn.enemy.name + " has no weapon");
                }
        }
    }
}
