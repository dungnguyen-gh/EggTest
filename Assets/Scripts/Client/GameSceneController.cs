using EggTest.Server;
using EggTest.Shared;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace EggTest.Client
{
    /// <summary>
    /// Scene-resident runtime controller.
    /// Unlike the old bootstrap flow, this component does not mutate the scene automatically in edit mode.
    /// </summary>
    public sealed class GameSceneController : MonoBehaviour
    {
        private const string WorldRootName = "World";
        private const string ArenaRootName = "Arena";
        private const string ObstaclesRootName = "Obstacles";
        private const string PlayersRootName = "Players";
        private const string EggsRootName = "Eggs";
        private const string HudObjectName = "HUD";

        private static GameSceneController _instance;

        [SerializeField] private Transform _worldRoot;
        [SerializeField] private Transform _arenaRoot;
        [SerializeField] private Transform _obstaclesRoot;
        [SerializeField] private Transform _playersRoot;
        [SerializeField] private Transform _eggsRoot;
        [SerializeField] private HudPresenter _hud;

        private GameConfig _config;
        private ArenaDefinition _arena;
        private SimulatedTransport _transport;
        private ServerSimulator _server;
        private ClientGameController _client;
        private LocalPlayerInput _localInput;
        private NetworkSimulationPreset _selectedPreset;
        private bool _spikeEnabled;

        private LocalPlayerInput LocalInput
        {
            get
            {
                if (_localInput == null)
                {
                    _localInput = new LocalPlayerInput(LocalPlayerInput.LoadConfiguredAsset());
                }

                return _localInput;
            }
        }

        private void Awake()
        {
            if (!TryBecomeSingleton())
            {
                return;
            }

            InitializeRuntimeState();
            ResolveSceneContract(createMissing: true);
            EnsurePresentationInfrastructure();
            GameTrace.Configure(_config.EnableDebugLogs, _config.EnableVerboseDebugLogs);
            RebuildRuntimeMatch();
        }

        private void OnDestroy()
        {
            if (_client != null)
            {
                _client.Dispose();
                _client = null;
            }

            if (_localInput != null)
            {
                _localInput.Dispose();
                _localInput = null;
            }

            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void Update()
        {
            if (!HasRuntimeMatch())
            {
                return;
            }

            TickRuntimeMatch();
        }

        public void RestartMatch()
        {
            RebuildRuntimeMatch();
        }

        public void ChangePlayerCount(int delta)
        {
            int maxPlayers = Mathf.Max(2, (_config.GridWidth * _config.GridHeight) - ArenaDefinition.CreateDefault(_config).BlockedCells.Count);
            _config.PlayerCount = Mathf.Clamp(_config.PlayerCount + delta, 2, maxPlayers);
            RebuildRuntimeMatch();
        }

        public void ChangeMatchDuration(int deltaSeconds)
        {
            _config.MatchDurationSeconds = Mathf.Clamp(_config.MatchDurationSeconds + deltaSeconds, 30f, 300f);
            RebuildRuntimeMatch();
        }

        public void SetNetworkPreset(NetworkSimulationPreset preset)
        {
            if (_transport != null)
            {
                _transport.ApplyPreset(preset);
            }

            if (_server != null)
            {
                _server.OnNetworkPresetChanged();
            }

            _selectedPreset = preset;
            if (_hud != null)
            {
                _hud.SetNetworkPresetLabel(preset.ToString());
            }
        }

        public void SetSpikeEnabled(bool enabled)
        {
            if (_transport != null)
            {
                _transport.SetSpikeEnabled(enabled);
            }

            _spikeEnabled = enabled;
        }

        public NetworkSimulationSettings NetworkSettings
        {
            get { return _transport != null ? _transport.Settings : null; }
        }

        private bool TryBecomeSingleton()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return false;
            }

            _instance = this;
            return true;
        }

        private void InitializeRuntimeState()
        {
            _config = new GameConfig();
            _arena = ArenaDefinition.CreateDefault(_config);
            _selectedPreset = _config.DefaultNetworkPreset;
        }

        private bool HasRuntimeMatch()
        {
            return _client != null && _server != null && _transport != null;
        }

        private void TickRuntimeMatch()
        {
            double now = Time.unscaledTimeAsDouble;
            float deltaTime = Time.unscaledDeltaTime;
            Vector2 rawMove = LocalInput.ReadMove();

            _client.CaptureLocalInput(rawMove, deltaTime, now);
            _transport.Pump(now, _server.Receive, _client.Receive);
            _server.Update(deltaTime, now);
            _transport.Pump(now, _server.Receive, _client.Receive);
            _client.Tick(deltaTime, now);
        }

        private void RebuildRuntimeMatch()
        {
            TeardownRuntimeMatch();
            _arena = ArenaDefinition.CreateDefault(_config);
            EnsureWorldVisualsForRuntime();

            _transport = new SimulatedTransport(_config.RandomSeed, _selectedPreset);
            _transport.SetSpikeEnabled(_spikeEnabled);
            _server = new ServerSimulator(_config, _arena, _transport);
            _client = new ClientGameController(_config, _arena, _transport, _playersRoot, _eggsRoot, _hud);

            BindHud();
            _server.StartMatch(Time.unscaledTimeAsDouble);
        }

        private void TeardownRuntimeMatch()
        {
            if (_client != null)
            {
                _client.Dispose();
                _client = null;
            }

            if (_transport != null)
            {
                _transport.Clear();
            }

            ClearDynamicChildren(_playersRoot);
            ClearDynamicChildren(_eggsRoot);
        }

        private void ResolveSceneContract(bool createMissing)
        {
            _worldRoot = ResolveChild(transform, WorldRootName, createMissing);
            _arenaRoot = ResolveChild(_worldRoot, ArenaRootName, createMissing);
            _obstaclesRoot = ResolveChild(_arenaRoot, ObstaclesRootName, createMissing);
            _playersRoot = ResolveChild(_worldRoot, PlayersRootName, createMissing);
            _eggsRoot = ResolveChild(_worldRoot, EggsRootName, createMissing);

            if (_hud == null)
            {
                Transform existingHud = transform.Find(HudObjectName);
                if (existingHud != null)
                {
                    _hud = existingHud.GetComponent<HudPresenter>();
                }

                if (_hud == null && createMissing)
                {
                    GameObject hudObject = new GameObject(HudObjectName);
                    hudObject.transform.SetParent(transform, false);
                    _hud = hudObject.AddComponent<HudPresenter>();
                }
            }
        }

        private void EnsurePresentationInfrastructure()
        {
            EnsureCameraAndLight();
            EnsureEventSystem();
            EnsureWorldVisualsForRuntime();
            if (_hud != null)
            {
                _hud.Bind(this);
            }
        }

        private void BindHud()
        {
            if (_hud == null)
            {
                return;
            }

            _hud.Bind(this);
            _hud.SetNetworkPresetLabel(_selectedPreset.ToString());
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
            camera.transform.rotation = Quaternion.Euler(70f, 0f, 0f);
            camera.fieldOfView = 55f;
            camera.clearFlags = CameraClearFlags.Skybox;

            Light light = FindObjectOfType<Light>();
            if (light == null)
            {
                GameObject lightObject = new GameObject("Directional Light");
                light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
            }

            light.transform.position = new Vector3(0f, 3f, 0f);
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private void EnsureEventSystem()
        {
            EventSystem eventSystem = FindObjectOfType<EventSystem>();
            GameObject eventSystemObject;
            if (eventSystem == null)
            {
                eventSystemObject = new GameObject("EventSystem");
                eventSystemObject.transform.SetParent(transform, false);
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

            ConfigureUiInputModule(uiInputModule);
        }

        private void EnsureWorldVisualsForRuntime()
        {
            if (_arenaRoot == null || _obstaclesRoot == null)
            {
                return;
            }

            if (!HasAuthoredArenaVisuals())
            {
                EnsureArenaVisuals(rebuildObstacles: true);
            }
            else
            {
                EnsureArenaBorderOnly();
            }
        }

        private bool HasAuthoredArenaVisuals()
        {
            return _arenaRoot.Find("Floor") != null && _obstaclesRoot.childCount > 0;
        }

        private void EnsureArenaBorderOnly()
        {
            float arenaWidth = _arena.Width * _arena.CellSize;
            float arenaHeight = _arena.Height * _arena.CellSize;
            EnsureBorderWall("NorthBorder", new Vector3(0f, 0.55f, arenaHeight * 0.5f + 0.25f), new Vector3(arenaWidth + 1f, 1.1f, 0.5f));
            EnsureBorderWall("SouthBorder", new Vector3(0f, 0.55f, -arenaHeight * 0.5f - 0.25f), new Vector3(arenaWidth + 1f, 1.1f, 0.5f));
            EnsureBorderWall("WestBorder", new Vector3(-arenaWidth * 0.5f - 0.25f, 0.55f, 0f), new Vector3(0.5f, 1.1f, arenaHeight + 1f));
            EnsureBorderWall("EastBorder", new Vector3(arenaWidth * 0.5f + 0.25f, 0.55f, 0f), new Vector3(0.5f, 1.1f, arenaHeight + 1f));
        }

        private void EnsureArenaVisuals(bool rebuildObstacles)
        {
            if (_arenaRoot == null || _obstaclesRoot == null)
            {
                return;
            }

            float arenaWidth = _arena.Width * _arena.CellSize;
            float arenaHeight = _arena.Height * _arena.CellSize;

            GameObject floor = GetOrCreatePrimitive(_arenaRoot, "Floor", PrimitiveType.Cube);
            floor.transform.localPosition = new Vector3(0f, -0.25f, 0f);
            floor.transform.localScale = new Vector3(arenaWidth, 0.5f, arenaHeight);
            ApplyRendererColor(floor, new Color(0.18f, 0.22f, 0.26f));

            if (rebuildObstacles)
            {
                ClearStaticChildren(_obstaclesRoot);
                foreach (GridCell blockedCell in _arena.BlockedCells)
                {
                    GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wall.name = "Wall_" + blockedCell.X + "_" + blockedCell.Y;
                    wall.transform.SetParent(_obstaclesRoot, false);
                    wall.transform.position = _arena.CellToWorld(blockedCell) + new Vector3(0f, 0.5f, 0f);
                    wall.transform.localScale = new Vector3(_arena.CellSize, 1.0f, _arena.CellSize);
                    ApplyRendererColor(wall, new Color(0.35f, 0.36f, 0.42f));
                }
            }

            EnsureArenaBorderOnly();
        }

        private void EnsureBorderWall(string name, Vector3 position, Vector3 scale)
        {
            GameObject wall = GetOrCreatePrimitive(_arenaRoot, name, PrimitiveType.Cube);
            wall.transform.localPosition = position;
            wall.transform.localScale = scale;
            ApplyRendererColor(wall, new Color(0.12f, 0.12f, 0.15f));
        }

        private static Transform ResolveChild(Transform parent, string name, bool createMissing)
        {
            if (parent == null)
            {
                return null;
            }

            Transform existing = parent.Find(name);
            if (existing != null || !createMissing)
            {
                return existing;
            }

            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static GameObject GetOrCreatePrimitive(Transform parent, string name, PrimitiveType primitiveType)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                return existing.gameObject;
            }

            GameObject created = GameObject.CreatePrimitive(primitiveType);
            created.name = name;
            created.transform.SetParent(parent, false);
            return created;
        }

        private static void ApplyRendererColor(GameObject target, Color color)
        {
            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer == null)
            {
                return;
            }

            Material material = renderer.sharedMaterial;
            if (material == null || material.shader == null || material.shader.name != "Standard")
            {
                material = new Material(Shader.Find("Standard"));
            }

            material.color = color;
            renderer.sharedMaterial = material;
        }

        private void ConfigureUiInputModule(InputSystemUIInputModule uiInputModule)
        {
            if (uiInputModule == null)
            {
                return;
            }

            uiInputModule.UnassignActions();
            uiInputModule.actionsAsset = LocalInput.ActionsAsset;
            uiInputModule.move = CreateActionReference("UI/Navigate");
            uiInputModule.submit = CreateActionReference("UI/Submit");
            uiInputModule.cancel = CreateActionReference("UI/Cancel");
            uiInputModule.point = CreateActionReference("UI/Point");
            uiInputModule.leftClick = CreateActionReference("UI/Click");
            uiInputModule.rightClick = CreateActionReference("UI/RightClick");
            uiInputModule.middleClick = CreateActionReference("UI/MiddleClick");
            uiInputModule.scrollWheel = CreateActionReference("UI/ScrollWheel");
        }

        private InputActionReference CreateActionReference(string actionPath)
        {
            InputAction action = LocalInput.ActionsAsset.FindAction(actionPath, true);
            return action != null ? InputActionReference.Create(action) : null;
        }

        private static void DestroyComponent(Component component)
        {
            if (component == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(component);
                return;
            }

            Object.DestroyImmediate(component);
        }

        private static void ClearDynamicChildren(Transform root)
        {
            if (root == null)
            {
                return;
            }

            for (int index = root.childCount - 1; index >= 0; index--)
            {
                Destroy(root.GetChild(index).gameObject);
            }
        }

        private static void ClearStaticChildren(Transform root)
        {
            if (root == null)
            {
                return;
            }

            for (int index = root.childCount - 1; index >= 0; index--)
            {
                Destroy(root.GetChild(index).gameObject);
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Rebuild Scene Authoring Objects")]
        public void RebuildSceneAuthoringObjects()
        {
            ResolveSceneContract(createMissing: true);
            AuthorSceneContract();
        }

        [ContextMenu("Rebuild Scene Authoring Objects From Scratch")]
        public void RebuildSceneAuthoringObjectsFromScratch()
        {
            DestroyExistingSceneAuthoringObjectsImmediate();
            _worldRoot = null;
            _arenaRoot = null;
            _obstaclesRoot = null;
            _playersRoot = null;
            _eggsRoot = null;
            _hud = null;
            RebuildSceneAuthoringObjects();
        }

        private void AuthorSceneContract()
        {
            EnsureCameraAndLight();
            EnsureEventSystem();

            GameConfig previewConfig = _config ?? new GameConfig();
            _arena = ArenaDefinition.CreateDefault(previewConfig);
            EnsureArenaVisualsEditor();

            if (_hud != null)
            {
                _hud.EnsureAuthoredUi();
            }

            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }

        public void EnsureArenaVisualsEditor()
        {
            if (_arena == null)
            {
                _arena = ArenaDefinition.CreateDefault(_config ?? new GameConfig());
            }

            if (_arenaRoot == null || _obstaclesRoot == null)
            {
                ResolveSceneContract(createMissing: true);
            }

            float arenaWidth = _arena.Width * _arena.CellSize;
            float arenaHeight = _arena.Height * _arena.CellSize;

            GameObject floor = GetOrCreatePrimitive(_arenaRoot, "Floor", PrimitiveType.Cube);
            floor.transform.localPosition = new Vector3(0f, -0.25f, 0f);
            floor.transform.localScale = new Vector3(arenaWidth, 0.5f, arenaHeight);
            ApplyRendererColor(floor, new Color(0.18f, 0.22f, 0.26f));

            ClearStaticChildrenImmediate(_obstaclesRoot);
            foreach (GridCell blockedCell in _arena.BlockedCells)
            {
                GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "Wall_" + blockedCell.X + "_" + blockedCell.Y;
                wall.transform.SetParent(_obstaclesRoot, false);
                wall.transform.position = _arena.CellToWorld(blockedCell) + new Vector3(0f, 0.5f, 0f);
                wall.transform.localScale = new Vector3(_arena.CellSize, 1.0f, _arena.CellSize);
                ApplyRendererColor(wall, new Color(0.35f, 0.36f, 0.42f));
            }

            EnsureBorderWall("NorthBorder", new Vector3(0f, 0.55f, arenaHeight * 0.5f + 0.25f), new Vector3(arenaWidth + 1f, 1.1f, 0.5f));
            EnsureBorderWall("SouthBorder", new Vector3(0f, 0.55f, -arenaHeight * 0.5f - 0.25f), new Vector3(arenaWidth + 1f, 1.1f, 0.5f));
            EnsureBorderWall("WestBorder", new Vector3(-arenaWidth * 0.5f - 0.25f, 0.55f, 0f), new Vector3(0.5f, 1.1f, arenaHeight + 1f));
            EnsureBorderWall("EastBorder", new Vector3(arenaWidth * 0.5f + 0.25f, 0.55f, 0f), new Vector3(0.5f, 1.1f, arenaHeight + 1f));
        }

        private static void ClearStaticChildrenImmediate(Transform root)
        {
            if (root == null)
            {
                return;
            }

            for (int index = root.childCount - 1; index >= 0; index--)
            {
                Object.DestroyImmediate(root.GetChild(index).gameObject);
            }
        }

        private void DestroyExistingSceneAuthoringObjectsImmediate()
        {
            for (int index = transform.childCount - 1; index >= 0; index--)
            {
                Object.DestroyImmediate(transform.GetChild(index).gameObject);
            }

            EventSystem[] eventSystems = FindObjectsOfType<EventSystem>(true);
            for (int i = 0; i < eventSystems.Length; i++)
            {
                if (eventSystems[i] != null)
                {
                    Object.DestroyImmediate(eventSystems[i].gameObject);
                }
            }
        }
#endif
    }
}
