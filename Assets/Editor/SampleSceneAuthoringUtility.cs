using EggTest.Client;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace EggTest.EditorTools
{
    [InitializeOnLoad]
    public static class SampleSceneAuthoringUtility
    {
        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
        private static bool _queued;

        static SampleSceneAuthoringUtility()
        {
            EditorApplication.delayCall += TryAuthorOpenSampleScene;
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        [MenuItem("Tools/EggTest/Author Sample Scene")]
        public static void AuthorSampleSceneMenu()
        {
            Scene scene = OpenSampleScene();
            AuthorScene(scene);
        }

        [MenuItem("Tools/EggTest/Rebuild Sample Scene From Scratch")]
        public static void RebuildSampleSceneMenu()
        {
            Scene scene = OpenSampleScene();
            RebuildScene(scene);
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (scene.path == SampleScenePath)
            {
                QueueAuthoring();
            }
        }

        private static void TryAuthorOpenSampleScene()
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            if (scene.path == SampleScenePath)
            {
                QueueAuthoring();
            }
        }

        private static void QueueAuthoring()
        {
            if (_queued)
            {
                return;
            }

            _queued = true;
            EditorApplication.delayCall += RunQueuedAuthoring;
        }

        private static void RunQueuedAuthoring()
        {
            _queued = false;
            Scene scene = EditorSceneManager.GetActiveScene();
            if (scene.path == SampleScenePath)
            {
                AuthorScene(scene);
            }
        }

        private static void AuthorScene(Scene scene)
        {
            GameSceneController controller = GetOrCreateController();

            if (!NeedsAuthoring(controller))
            {
                return;
            }

            controller.RebuildSceneAuthoringObjects();
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void RebuildScene(Scene scene)
        {
            GameSceneController controller = GetOrCreateController();
            controller.RebuildSceneAuthoringObjectsFromScratch();
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static Scene OpenSampleScene()
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            return scene.path == SampleScenePath ? scene : EditorSceneManager.OpenScene(SampleScenePath);
        }

        private static GameSceneController GetOrCreateController()
        {
            GameSceneController controller = Object.FindObjectOfType<GameSceneController>();
            if (controller != null)
            {
                return controller;
            }

            GameObject root = new GameObject("GameRoot");
            return root.AddComponent<GameSceneController>();
        }

        private static bool NeedsAuthoring(GameSceneController controller)
        {
            Transform world = controller.transform.Find("World");
            Transform obstacles = controller.transform.Find("World/Arena/Obstacles");
            Transform hudCanvas = controller.transform.Find("HUD/CanvasRoot");
            EventSystem eventSystem = Object.FindObjectOfType<EventSystem>();
            bool missingInputSystemUiModule = eventSystem == null || eventSystem.GetComponent<InputSystemUIInputModule>() == null;
            bool hasLegacyStandaloneModule = eventSystem != null && eventSystem.GetComponent<StandaloneInputModule>() != null;
            return world == null
                || obstacles == null
                || obstacles.childCount == 0
                || hudCanvas == null
                || missingInputSystemUiModule
                || hasLegacyStandaloneModule
                || HasArenaVisualDrift(controller.transform);
        }

        private static bool HasArenaVisualDrift(Transform root)
        {
            Transform floor = root.Find("World/Arena/Floor");
            Transform obstacles = root.Find("World/Arena/Obstacles");
            Transform northBorder = root.Find("World/Arena/NorthBorder");

            return HasUnexpectedColor(floor, ArenaSceneBuilder.FloorColor)
                || HasUnexpectedColor(northBorder, ArenaSceneBuilder.BorderColor)
                || HasUnexpectedObstacleColor(obstacles);
        }

        private static bool HasUnexpectedObstacleColor(Transform obstaclesRoot)
        {
            if (obstaclesRoot == null || obstaclesRoot.childCount == 0)
            {
                return true;
            }

            return HasUnexpectedColor(obstaclesRoot.GetChild(0), ArenaSceneBuilder.ObstacleColor);
        }

        private static bool HasUnexpectedColor(Transform target, Color expectedColor)
        {
            if (target == null)
            {
                return true;
            }

            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer == null || renderer.sharedMaterial == null)
            {
                return true;
            }

            Color actual = renderer.sharedMaterial.color;
            return Mathf.Abs(actual.r - expectedColor.r) > 0.01f
                || Mathf.Abs(actual.g - expectedColor.g) > 0.01f
                || Mathf.Abs(actual.b - expectedColor.b) > 0.01f;
        }
    }
}
