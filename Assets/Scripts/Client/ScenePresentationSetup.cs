using EggTest.Shared;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace EggTest.Client
{
    /// <summary>
    /// Ensures the camera, light, EventSystem, and HUD wiring required by the current scene contract.
    /// </summary>
    public sealed class ScenePresentationSetup
    {
        public void EnsureRuntimePresentation(Transform gameRoot, HudPresenter hud, InputActionAsset actionsAsset, NetworkSimulationPreset preset, GameSceneController controller)
        {
            EnsurePresentation(gameRoot, hud, actionsAsset, preset, controller);
        }

        public void EnsureEditorPresentation(Transform gameRoot, HudPresenter hud, InputActionAsset actionsAsset, NetworkSimulationPreset preset, GameSceneController controller)
        {
            EnsurePresentation(gameRoot, hud, actionsAsset, preset, controller);
        }

        private void EnsurePresentation(Transform gameRoot, HudPresenter hud, InputActionAsset actionsAsset, NetworkSimulationPreset preset, GameSceneController controller)
        {
            EnsureCameraAndLight();
            EnsureEventSystem(gameRoot, actionsAsset);
            BindHud(hud, controller, preset);
        }

        private void EnsureCameraAndLight()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }

            camera.transform.position = new Vector3(0f, 16f, -4f);
            camera.transform.rotation = Quaternion.Euler(75f, 0f, 0f);
            camera.fieldOfView = 55f;
            camera.clearFlags = CameraClearFlags.Skybox;

            Light light = Object.FindObjectOfType<Light>();
            if (light == null)
            {
                GameObject lightObject = new GameObject("Directional Light");
                light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
            }

            light.transform.position = new Vector3(0f, 3f, 0f);
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private void EnsureEventSystem(Transform gameRoot, InputActionAsset actionsAsset)
        {
            EventSystem eventSystem = Object.FindObjectOfType<EventSystem>();
            GameObject eventSystemObject;
            if (eventSystem == null)
            {
                eventSystemObject = new GameObject("EventSystem");
                eventSystemObject.transform.SetParent(gameRoot, false);
                eventSystem = eventSystemObject.AddComponent<EventSystem>();
            }
            else
            {
                eventSystemObject = eventSystem.gameObject;
            }

            StandaloneInputModule legacyModule = eventSystemObject.GetComponent<StandaloneInputModule>();
            if (legacyModule != null)
            {
                legacyModule.enabled = false;
                DestroyComponent(legacyModule);
            }

            InputSystemUIInputModule uiInputModule = eventSystemObject.GetComponent<InputSystemUIInputModule>();
            if (uiInputModule == null)
            {
                uiInputModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();
            }

            ConfigureUiInputModule(uiInputModule, actionsAsset);
        }

        private void ConfigureUiInputModule(InputSystemUIInputModule uiInputModule, InputActionAsset actionsAsset)
        {
            if (uiInputModule == null || actionsAsset == null)
            {
                return;
            }

            uiInputModule.UnassignActions();
            uiInputModule.actionsAsset = actionsAsset;
            uiInputModule.move = CreateActionReference(actionsAsset, "UI/Navigate");
            uiInputModule.submit = CreateActionReference(actionsAsset, "UI/Submit");
            uiInputModule.cancel = CreateActionReference(actionsAsset, "UI/Cancel");
            uiInputModule.point = CreateActionReference(actionsAsset, "UI/Point");
            uiInputModule.leftClick = CreateActionReference(actionsAsset, "UI/Click");
            uiInputModule.rightClick = CreateActionReference(actionsAsset, "UI/RightClick");
            uiInputModule.middleClick = CreateActionReference(actionsAsset, "UI/MiddleClick");
            uiInputModule.scrollWheel = CreateActionReference(actionsAsset, "UI/ScrollWheel");
        }

        private InputActionReference CreateActionReference(InputActionAsset actionsAsset, string actionPath)
        {
            InputAction action = actionsAsset.FindAction(actionPath, true);
            return action != null ? InputActionReference.Create(action) : null;
        }

        private void BindHud(HudPresenter hud, GameSceneController controller, NetworkSimulationPreset preset)
        {
            if (hud == null)
            {
                return;
            }

            hud.Bind(controller);
            hud.SetNetworkPreset(preset);
        }

        private void DestroyComponent(Component component)
        {
            if (component == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(component);
                return;
            }

            Object.DestroyImmediate(component);
        }
    }
}
