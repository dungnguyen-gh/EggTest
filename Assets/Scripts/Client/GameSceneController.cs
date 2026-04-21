using EggTest.Server;
using EggTest.Shared;
using UnityEngine;
using UnityEngine.InputSystem;

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
        [System.Serializable]
        private sealed class GameplaySettings
        {
            public int PlayerCount = 4;
            public float MatchDurationSeconds = 90f;
            public int TargetActiveEggCount = 3;
        }

        private enum GameFlowState
        {
            MainMenu,
            Countdown,
            Playing,
            GameOver,
        }

        private const float CountdownDurationSeconds = 3f;
        private const int MinPlayerCount = 2;
        private const float MinMatchDurationSeconds = 30f;
        private const float MaxMatchDurationSeconds = 300f;
        private const int MinTargetActiveEggCount = 1;

        private static GameSceneController _instance;

        [SerializeField] private Transform _worldRoot;
        [SerializeField] private Transform _arenaRoot;
        [SerializeField] private Transform _obstaclesRoot;
        [SerializeField] private Transform _playersRoot;
        [SerializeField] private Transform _eggsRoot;
        [SerializeField] private HudPresenter _hud;
        [Header("Gameplay Settings")]
        [SerializeField] private GameplaySettings _gameplaySettings = new GameplaySettings();

        private GameConfig _config;
        private ArenaDefinition _arena;
        private SimulatedTransport _transport;
        private ServerSimulator _server;
        private ClientGameController _client;
        private LocalPlayerInput _localInput;
        private SceneContract _sceneContract;
        private ArenaSceneBuilder _arenaSceneBuilder;
        private ScenePresentationSetup _presentationSetup;
        private NetworkSimulationPreset _selectedPreset;
        private bool _spikeEnabled;
        private GameFlowState _flowState;
        private float _countdownRemaining;

        private void OnValidate()
        {
            ClampSerializedGameplaySettings();
        }

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
            GameTrace.ResetThrottleState();
            ShowMainMenu();
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
            switch (_flowState)
            {
                case GameFlowState.Countdown:
                    TickCountdown();
                    break;
                case GameFlowState.Playing:
                    if (!HasRuntimeMatch())
                    {
                        return;
                    }

                    TickRuntimeMatch();
                    if (_client != null && _client.MatchEnded)
                    {
                        ShowGameOver();
                    }
                    break;
            }
        }

        public void RestartMatch()
        {
            StartGameWithCountdown();
        }

        public void ChangePlayerCount(int delta)
        {
            EnsureRuntimeConfig();
            _config.PlayerCount = Mathf.Clamp(_config.PlayerCount + delta, MinPlayerCount, GetMaxSupportedPlayerCount());
            SyncSerializedGameplaySettingsFromRuntimeConfig();
            if (_flowState != GameFlowState.MainMenu)
            {
                RestartMatchImmediateForConfigChange();
            }
        }

        public void ChangeMatchDuration(int deltaSeconds)
        {
            EnsureRuntimeConfig();
            _config.MatchDurationSeconds = Mathf.Clamp(_config.MatchDurationSeconds + deltaSeconds, MinMatchDurationSeconds, MaxMatchDurationSeconds);
            SyncSerializedGameplaySettingsFromRuntimeConfig();
            if (_flowState != GameFlowState.MainMenu)
            {
                RestartMatchImmediateForConfigChange();
            }
        }

        public void ChangeEggCount(int delta)
        {
            EnsureRuntimeConfig();
            _config.TargetActiveEggCount = Mathf.Clamp(_config.TargetActiveEggCount + delta, MinTargetActiveEggCount, GetMaxSupportedEggCount());
            SyncSerializedGameplaySettingsFromRuntimeConfig();
            if (_flowState != GameFlowState.MainMenu)
            {
                RestartMatchImmediateForConfigChange();
            }
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
                _hud.SetNetworkPreset(preset);
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

        public NetworkSimulationPreset CurrentPreset
        {
            get { return _selectedPreset; }
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
            ClampSerializedGameplaySettings();
            _config = BuildRuntimeConfigFromSerializedSettings();
            _arena = ArenaDefinition.CreateDefault(_config);
            ClampRuntimeConfigAgainstArena(_config, _arena);
            SyncSerializedGameplaySettingsFromRuntimeConfig();
            _arenaSceneBuilder = new ArenaSceneBuilder();
            _presentationSetup = new ScenePresentationSetup();
            _selectedPreset = _config.DefaultNetworkPreset;
            _flowState = GameFlowState.MainMenu;
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

        public void StartGame()
        {
            StartGameWithCountdown();
        }

        private void StartGameWithCountdown()
        {
            PrepareRuntimeMatch();
            BeginCountdown();
        }

        private void RestartMatchImmediateForConfigChange()
        {
            PrepareRuntimeMatch();
            BeginPlaying();
        }

        public void ExitGame()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void PrepareRuntimeMatch()
        {
            TeardownRuntimeMatch();
            _config = BuildRuntimeConfigFromSerializedSettings();
            _arena = ArenaDefinition.CreateDefault(_config);
            ClampRuntimeConfigAgainstArena(_config, _arena);
            SyncSerializedGameplaySettingsFromRuntimeConfig();
            GameTrace.ResetThrottleState();
            _arenaSceneBuilder.EnsureRuntimeArenaVisuals(_sceneContract, _arena);

            _transport = new SimulatedTransport(_config.RandomSeed, _selectedPreset);
            _transport.SetSpikeEnabled(_spikeEnabled);
            _server = new ServerSimulator(_config, _arena, _transport);
            _client = new ClientGameController(_config, _arena, _transport, _sceneContract.PlayersRoot, _sceneContract.EggsRoot, _sceneContract.Hud);

            _presentationSetup.EnsureRuntimePresentation(transform, _sceneContract.Hud, LocalInput.ActionsAsset, _selectedPreset, this);
        }

        private void BeginCountdown()
        {
            _countdownRemaining = CountdownDurationSeconds;
            _flowState = GameFlowState.Countdown;
            if (_hud != null)
            {
                _hud.ShowCountdown(Mathf.CeilToInt(_countdownRemaining));
            }
        }

        private void TickCountdown()
        {
            _countdownRemaining -= Time.unscaledDeltaTime;
            int secondsLeft = Mathf.Max(1, Mathf.CeilToInt(_countdownRemaining));
            if (_hud != null)
            {
                _hud.ShowCountdown(secondsLeft);
            }

            if (_countdownRemaining > 0f)
            {
                return;
            }

            BeginPlaying();
        }

        private void BeginPlaying()
        {
            if (!HasRuntimeMatch())
            {
                return;
            }

            _server.StartMatch(Time.unscaledTimeAsDouble);
            _flowState = GameFlowState.Playing;
            if (_hud != null)
            {
                _hud.ShowGameplayHud();
            }
        }

        private void ShowGameOver()
        {
            _flowState = GameFlowState.GameOver;
            if (_hud != null)
            {
                _hud.ShowGameOver(_client != null ? _client.WinnerSummary : "Winner: Unknown");
            }
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
                _transport = null;
            }

            _server = null;
            _flowState = GameFlowState.MainMenu;

            _arenaSceneBuilder.ClearRuntimeDynamicObjects(_sceneContract);
        }

        private void ResolveSceneContract(bool createMissing)
        {
            if (_arenaSceneBuilder == null)
            {
                _arenaSceneBuilder = new ArenaSceneBuilder();
            }

            _sceneContract = _arenaSceneBuilder.ResolveSceneContract(transform, _hud, createMissing);
            _worldRoot = _sceneContract.WorldRoot;
            _arenaRoot = _sceneContract.ArenaRoot;
            _obstaclesRoot = _sceneContract.ObstaclesRoot;
            _playersRoot = _sceneContract.PlayersRoot;
            _eggsRoot = _sceneContract.EggsRoot;
            _hud = _sceneContract.Hud;
        }

        private void EnsurePresentationInfrastructure()
        {
            if (_presentationSetup == null)
            {
                _presentationSetup = new ScenePresentationSetup();
            }

            _presentationSetup.EnsureRuntimePresentation(transform, _hud, LocalInput.ActionsAsset, _selectedPreset, this);
            _arenaSceneBuilder.EnsureRuntimeArenaVisuals(_sceneContract, _arena);
        }

        private void ShowMainMenu()
        {
            _flowState = GameFlowState.MainMenu;
            if (_hud != null)
            {
                _hud.ShowMainMenu();
            }
        }

        private void EnsureRuntimeConfig()
        {
            if (_config == null)
            {
                _config = BuildRuntimeConfigFromSerializedSettings();
            }
        }

        private GameConfig BuildRuntimeConfigFromSerializedSettings()
        {
            ClampSerializedGameplaySettings();

            GameConfig config = new GameConfig
            {
                PlayerCount = _gameplaySettings != null ? _gameplaySettings.PlayerCount : 4,
                MatchDurationSeconds = _gameplaySettings != null ? _gameplaySettings.MatchDurationSeconds : 90f,
                TargetActiveEggCount = _gameplaySettings != null ? _gameplaySettings.TargetActiveEggCount : 3,
            };

            return config;
        }

        private void SyncSerializedGameplaySettingsFromRuntimeConfig()
        {
            if (_config == null)
            {
                return;
            }

            if (_gameplaySettings == null)
            {
                _gameplaySettings = new GameplaySettings();
            }

            _gameplaySettings.PlayerCount = _config.PlayerCount;
            _gameplaySettings.MatchDurationSeconds = _config.MatchDurationSeconds;
            _gameplaySettings.TargetActiveEggCount = _config.TargetActiveEggCount;
        }

        private void ClampSerializedGameplaySettings()
        {
            if (_gameplaySettings == null)
            {
                _gameplaySettings = new GameplaySettings();
            }

            _gameplaySettings.PlayerCount = Mathf.Max(MinPlayerCount, _gameplaySettings.PlayerCount);
            _gameplaySettings.MatchDurationSeconds = Mathf.Clamp(_gameplaySettings.MatchDurationSeconds, MinMatchDurationSeconds, MaxMatchDurationSeconds);
            _gameplaySettings.TargetActiveEggCount = Mathf.Max(MinTargetActiveEggCount, _gameplaySettings.TargetActiveEggCount);

            GameConfig previewConfig = new GameConfig
            {
                PlayerCount = _gameplaySettings.PlayerCount,
                MatchDurationSeconds = _gameplaySettings.MatchDurationSeconds,
                TargetActiveEggCount = _gameplaySettings.TargetActiveEggCount,
            };

            ArenaDefinition previewArena = ArenaDefinition.CreateDefault(previewConfig);
            _gameplaySettings.PlayerCount = Mathf.Clamp(_gameplaySettings.PlayerCount, MinPlayerCount, previewArena.MaxSupportedPlayerCount);
            _gameplaySettings.TargetActiveEggCount = Mathf.Clamp(_gameplaySettings.TargetActiveEggCount, MinTargetActiveEggCount, previewArena.MaxSupportedActiveEggCount);
        }

        private int GetMaxSupportedPlayerCount()
        {
            EnsureRuntimeConfig();
            ArenaDefinition previewArena = ArenaDefinition.CreateDefault(_config);
            return previewArena.MaxSupportedPlayerCount;
        }

        private int GetMaxSupportedEggCount()
        {
            EnsureRuntimeConfig();
            ArenaDefinition previewArena = ArenaDefinition.CreateDefault(_config);
            return previewArena.MaxSupportedActiveEggCount;
        }

        private void ClampRuntimeConfigAgainstArena(GameConfig config, ArenaDefinition arena)
        {
            if (config == null || arena == null)
            {
                return;
            }

            config.PlayerCount = Mathf.Clamp(config.PlayerCount, MinPlayerCount, arena.MaxSupportedPlayerCount);
            config.MatchDurationSeconds = Mathf.Clamp(config.MatchDurationSeconds, MinMatchDurationSeconds, MaxMatchDurationSeconds);
            config.TargetActiveEggCount = Mathf.Clamp(config.TargetActiveEggCount, MinTargetActiveEggCount, arena.MaxSupportedActiveEggCount);
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
            if (_presentationSetup == null)
            {
                _presentationSetup = new ScenePresentationSetup();
            }

            if (_arenaSceneBuilder == null)
            {
                _arenaSceneBuilder = new ArenaSceneBuilder();
            }

            _presentationSetup.EnsureEditorPresentation(transform, _hud, LocalInput.ActionsAsset, _selectedPreset, this);

            GameConfig previewConfig = _config ?? BuildRuntimeConfigFromSerializedSettings();
            _arena = ArenaDefinition.CreateDefault(previewConfig);
            _arenaSceneBuilder.EnsureEditorArenaVisuals(_sceneContract, _arena);

            if (_hud != null)
            {
                _hud.EnsureAuthoredUi();
            }

            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }

        private void DestroyExistingSceneAuthoringObjectsImmediate()
        {
            if (_arenaSceneBuilder == null)
            {
                _arenaSceneBuilder = new ArenaSceneBuilder();
            }

            _arenaSceneBuilder.DestroySceneChildrenImmediate(transform);
            _arenaSceneBuilder.DestroyEventSystemsImmediate();
        }
#endif
    }
}
