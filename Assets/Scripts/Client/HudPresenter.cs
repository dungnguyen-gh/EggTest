using System.Collections.Generic;
using System.Text;
using EggTest.Shared;
using UnityEngine;
using UnityEngine.UI;

namespace EggTest.Client
{
    public sealed class HudPresenter : MonoBehaviour
    {
        private enum HudScreen
        {
            MainMenu,
            Countdown,
            Gameplay,
            GameOver,
        }

        private static readonly Color PanelColor = new Color(0.06f, 0.09f, 0.14f, 0.86f);
        private static readonly Color PanelBorderColor = new Color(0.20f, 0.32f, 0.47f, 0.95f);
        private static readonly Color OverlayColor = new Color(0.02f, 0.03f, 0.05f, 0.72f);
        private static readonly Color AccentColor = new Color(0.28f, 0.72f, 1.00f, 1f);
        private static readonly Color SuccessColor = new Color(0.38f, 0.90f, 0.52f, 1f);
        private static readonly Color WarningColor = new Color(1.00f, 0.84f, 0.28f, 1f);
        private static readonly Color SecondaryTextColor = new Color(0.82f, 0.88f, 0.96f, 0.96f);
        private static readonly Color DebugTextColor = new Color(0.72f, 0.82f, 0.95f, 0.92f);
        private static readonly Color NeutralButtonColor = new Color(0.18f, 0.24f, 0.32f, 0.96f);
        private static readonly Color ActiveButtonColor = new Color(0.20f, 0.54f, 0.88f, 1f);
        private static readonly Color PrimaryButtonColor = new Color(0.18f, 0.58f, 0.38f, 0.98f);
        private static readonly Color DangerButtonColor = new Color(0.65f, 0.24f, 0.26f, 0.98f);

        private Font _font;

        private Text _timerText;
        private Text _scoreHeaderLeftText;
        private Text _scoreHeaderRightText;
        private Text _scoreNamesText;
        private Text _scoreValuesText;
        private Text _statusText;
        private Text _winnerText;
        private Text _debugText;
        private Text _networkPresetText;
        private Text _menuTitleText;
        private Text _menuSubtitleText;
        private Text _countdownValueText;
        private Text _countdownSubtitleText;
        private Text _gameOverTitleText;
        private Text _gameOverWinnerText;

        private GameObject _leftPanelObject;
        private GameObject _rightPanelObject;
        private GameObject _menuBackdropObject;
        private GameObject _menuPanelObject;
        private GameObject _countdownBackdropObject;
        private GameObject _countdownPanelObject;
        private GameObject _gameOverBackdropObject;
        private GameObject _gameOverPanelObject;

        private Button _playersMinusButton;
        private Button _playersPlusButton;
        private Button _timeMinusButton;
        private Button _timePlusButton;
        private Button _presetStableButton;
        private Button _presetLowButton;
        private Button _presetMediumButton;
        private Button _presetHighButton;
        private Button _spikeToggleButton;
        private Button _restartButton;
        private Button _gameplayExitButton;
        private Button _startButton;
        private Button _menuExitButton;
        private Button _gameOverRestartButton;
        private Button _gameOverExitButton;

        private readonly StringBuilder _scoreboardBuilder = new StringBuilder(192);
        private readonly StringBuilder _debugBuilder = new StringBuilder(192);
        private float _nextRenderTime;
        private HudScreen _currentScreen = HudScreen.MainMenu;

        private GameSceneController _controller;

        private void Awake()
        {
            EnsureUiStructure();
            CacheReferences();
            ApplyFontToAllTexts();
        }

        public void Bind(GameSceneController controller)
        {
            _controller = controller;
            EnsureUiStructure();
            CacheReferences();
            ApplyFontToAllTexts();
            SetNetworkPreset(controller != null ? controller.CurrentPreset : NetworkSimulationPreset.Stable);

            if (Application.isPlaying)
            {
                WireButtons();
            }
        }

        public void SetNetworkPreset(NetworkSimulationPreset preset)
        {
            CacheReferences();
            if (_networkPresetText != null)
            {
                _networkPresetText.text = "Preset: " + preset;
            }

            UpdatePresetButtonVisuals(preset);
        }

        public void SetNetworkPresetLabel(string label)
        {
            CacheReferences();
            if (_networkPresetText != null)
            {
                _networkPresetText.text = "Preset: " + label;
            }
        }

        public void ShowMainMenu()
        {
            CacheReferences();
            _currentScreen = HudScreen.MainMenu;
            SetActive(_leftPanelObject, false);
            SetActive(_rightPanelObject, false);
            SetActive(_menuBackdropObject, true);
            SetActive(_menuPanelObject, true);
            SetActive(_countdownBackdropObject, false);
            SetActive(_countdownPanelObject, false);
            SetActive(_gameOverBackdropObject, false);
            SetActive(_gameOverPanelObject, false);
        }

        public void ShowCountdown(int secondsLeft)
        {
            CacheReferences();
            _currentScreen = HudScreen.Countdown;
            SetActive(_leftPanelObject, false);
            SetActive(_rightPanelObject, false);
            SetActive(_menuBackdropObject, false);
            SetActive(_menuPanelObject, false);
            SetActive(_countdownBackdropObject, true);
            SetActive(_countdownPanelObject, true);
            SetActive(_gameOverBackdropObject, false);
            SetActive(_gameOverPanelObject, false);

            if (_countdownValueText != null)
            {
                _countdownValueText.text = secondsLeft.ToString();
            }
        }

        public void ShowGameplayHud()
        {
            CacheReferences();
            _currentScreen = HudScreen.Gameplay;
            SetActive(_leftPanelObject, true);
            SetActive(_rightPanelObject, true);
            SetActive(_menuBackdropObject, false);
            SetActive(_menuPanelObject, false);
            SetActive(_countdownBackdropObject, false);
            SetActive(_countdownPanelObject, false);
            SetActive(_gameOverBackdropObject, false);
            SetActive(_gameOverPanelObject, false);
        }

        public void ShowGameOver(string winnerSummary)
        {
            CacheReferences();
            _currentScreen = HudScreen.GameOver;
            SetActive(_leftPanelObject, true);
            SetActive(_rightPanelObject, true);
            SetActive(_menuBackdropObject, false);
            SetActive(_menuPanelObject, false);
            SetActive(_countdownBackdropObject, false);
            SetActive(_countdownPanelObject, false);
            SetActive(_gameOverBackdropObject, true);
            SetActive(_gameOverPanelObject, true);

            if (_gameOverWinnerText != null)
            {
                _gameOverWinnerText.text = winnerSummary;
            }
        }

        public void Render(
            float remainingTime,
            List<ScoreEntry> standings,
            string status,
            string winners,
            int playerCount,
            int durationSeconds,
            NetworkSimulationSettings networkSettings,
            float snapshotMinInterval,
            float snapshotMaxInterval,
            float smoothedSnapshotInterval,
            float interpolationBackTime)
        {
            CacheReferences();
            if ((_currentScreen != HudScreen.Gameplay && _currentScreen != HudScreen.GameOver) || _timerText == null)
            {
                return;
            }

            if (Time.unscaledTime < _nextRenderTime)
            {
                return;
            }

            _nextRenderTime = Time.unscaledTime + 0.10f;

            _timerText.text = "Time Left  " + Mathf.CeilToInt(remainingTime) + "s";

            _scoreboardBuilder.Length = 0;
            _debugBuilder.Length = 0;
            for (int i = 0; i < standings.Count; i++)
            {
                ScoreEntry entry = standings[i];
                _scoreboardBuilder
                    .Append(i + 1)
                    .Append(".  ")
                    .Append(entry.DisplayName)
                    .AppendLine();
                _debugBuilder
                    .Append(entry.Score)
                    .AppendLine();
            }

            if (_scoreHeaderLeftText != null)
            {
                _scoreHeaderLeftText.text = "Rank  Player";
            }

            if (_scoreHeaderRightText != null)
            {
                _scoreHeaderRightText.text = "Score";
            }

            if (_scoreNamesText != null)
            {
                _scoreNamesText.text = _scoreboardBuilder.ToString();
            }

            if (_scoreValuesText != null)
            {
                _scoreValuesText.text = _debugBuilder.ToString();
            }

            _statusText.text = "Status  " + status;
            _winnerText.text = winners;
            if (_gameOverWinnerText != null && _currentScreen == HudScreen.GameOver)
            {
                _gameOverWinnerText.text = winners;
            }

            if (networkSettings != null && _debugText != null)
            {
                _scoreboardBuilder.Length = 0;
                _scoreboardBuilder
                    .Append("Players: ").Append(playerCount).Append('\n')
                    .Append("Duration: ").Append(durationSeconds).Append("s\n")
                    .Append("Latency: ").Append(Mathf.RoundToInt(networkSettings.BaseLatencyMs)).Append(" ms\n")
                    .Append("Jitter: ").Append(Mathf.RoundToInt(networkSettings.JitterMs)).Append(" ms\n")
                    .Append("Spike: ").Append(networkSettings.SpikeEnabled ? "ON" : "OFF").Append('\n')
                    .Append("Snapshots: ").Append(snapshotMinInterval.ToString("F2")).Append(" - ").Append(snapshotMaxInterval.ToString("F2")).Append(" s\n")
                    .Append("Observed dt: ").Append(smoothedSnapshotInterval.ToString("F2")).Append(" s\n")
                    .Append("Interp back-time: ").Append(interpolationBackTime.ToString("F2")).Append(" s");
                _debugText.text = _scoreboardBuilder.ToString();
            }
        }

        public void EnsureAuthoredUi()
        {
            EnsureUiStructure();
            CacheReferences();
            ApplyFontToAllTexts();
        }

        private void CacheReferences()
        {
            if (_font == null)
            {
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            if (_leftPanelObject == null)
            {
                _leftPanelObject = FindObject("CanvasRoot/LeftPanel");
                _rightPanelObject = FindObject("CanvasRoot/RightPanel");
                _menuBackdropObject = FindObject("CanvasRoot/MenuBackdrop");
                _menuPanelObject = FindObject("CanvasRoot/MenuPanel");
                _countdownBackdropObject = FindObject("CanvasRoot/CountdownBackdrop");
                _countdownPanelObject = FindObject("CanvasRoot/CountdownPanel");
                _gameOverBackdropObject = FindObject("CanvasRoot/GameOverBackdrop");
                _gameOverPanelObject = FindObject("CanvasRoot/GameOverPanel");
            }

            if (_timerText == null)
            {
                _timerText = FindText("CanvasRoot/LeftPanel/TimerText");
                _scoreHeaderLeftText = FindText("CanvasRoot/LeftPanel/ScoreHeaderLeftText");
                _scoreHeaderRightText = FindText("CanvasRoot/LeftPanel/ScoreHeaderRightText");
                _scoreNamesText = FindText("CanvasRoot/LeftPanel/ScoreNamesText");
                _scoreValuesText = FindText("CanvasRoot/LeftPanel/ScoreValuesText");
                _statusText = FindText("CanvasRoot/LeftPanel/StatusText");
                _winnerText = FindText("CanvasRoot/LeftPanel/WinnerText");
                _networkPresetText = FindText("CanvasRoot/RightPanel/PresetText");
                _debugText = FindText("CanvasRoot/RightPanel/DebugText");
                _menuTitleText = FindText("CanvasRoot/MenuPanel/MenuTitle");
                _menuSubtitleText = FindText("CanvasRoot/MenuPanel/MenuSubtitle");
                _countdownValueText = FindText("CanvasRoot/CountdownPanel/CountdownValue");
                _countdownSubtitleText = FindText("CanvasRoot/CountdownPanel/CountdownSubtitle");
                _gameOverTitleText = FindText("CanvasRoot/GameOverPanel/GameOverTitle");
                _gameOverWinnerText = FindText("CanvasRoot/GameOverPanel/GameOverWinner");
            }

            if (_playersMinusButton == null)
            {
                _playersMinusButton = FindButton("CanvasRoot/RightPanel/PlayersMinus");
                _playersPlusButton = FindButton("CanvasRoot/RightPanel/PlayersPlus");
                _timeMinusButton = FindButton("CanvasRoot/RightPanel/TimeMinus");
                _timePlusButton = FindButton("CanvasRoot/RightPanel/TimePlus");
                _presetStableButton = FindButton("CanvasRoot/RightPanel/PresetStable");
                _presetLowButton = FindButton("CanvasRoot/RightPanel/PresetLow");
                _presetMediumButton = FindButton("CanvasRoot/RightPanel/PresetMedium");
                _presetHighButton = FindButton("CanvasRoot/RightPanel/PresetHigh");
                _spikeToggleButton = FindButton("CanvasRoot/RightPanel/SpikeToggle");
                _restartButton = FindButton("CanvasRoot/RightPanel/Restart");
                _gameplayExitButton = FindButton("CanvasRoot/RightPanel/Exit");
                _startButton = FindButton("CanvasRoot/MenuPanel/Start");
                _menuExitButton = FindButton("CanvasRoot/MenuPanel/Exit");
                _gameOverRestartButton = FindButton("CanvasRoot/GameOverPanel/Restart");
                _gameOverExitButton = FindButton("CanvasRoot/GameOverPanel/Exit");
            }
        }

        private void ApplyFontToAllTexts()
        {
            if (_font == null)
            {
                return;
            }

            Text[] texts = GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null)
                {
                    texts[i].font = _font;
                }
            }
        }

        private void WireButtons()
        {
            if (_controller == null)
            {
                return;
            }

            BindButton(_playersMinusButton, delegate { _controller.ChangePlayerCount(-1); });
            BindButton(_playersPlusButton, delegate { _controller.ChangePlayerCount(1); });
            BindButton(_timeMinusButton, delegate { _controller.ChangeMatchDuration(-30); });
            BindButton(_timePlusButton, delegate { _controller.ChangeMatchDuration(30); });
            BindButton(_presetStableButton, delegate { _controller.SetNetworkPreset(NetworkSimulationPreset.Stable); });
            BindButton(_presetLowButton, delegate { _controller.SetNetworkPreset(NetworkSimulationPreset.Low); });
            BindButton(_presetMediumButton, delegate { _controller.SetNetworkPreset(NetworkSimulationPreset.Medium); });
            BindButton(_presetHighButton, delegate { _controller.SetNetworkPreset(NetworkSimulationPreset.High); });
            BindButton(_spikeToggleButton, ToggleSpike);
            BindButton(_restartButton, delegate { _controller.RestartMatch(); });
            BindButton(_gameplayExitButton, delegate { _controller.ExitGame(); });
            BindButton(_startButton, delegate { _controller.StartGame(); });
            BindButton(_menuExitButton, delegate { _controller.ExitGame(); });
            BindButton(_gameOverRestartButton, delegate { _controller.RestartMatch(); });
            BindButton(_gameOverExitButton, delegate { _controller.ExitGame(); });
        }

        private void ToggleSpike()
        {
            if (_controller == null)
            {
                return;
            }

            NetworkSimulationSettings settings = _controller.NetworkSettings;
            if (settings == null)
            {
                return;
            }

            _controller.SetSpikeEnabled(!settings.SpikeEnabled);
        }

        private void UpdatePresetButtonVisuals(NetworkSimulationPreset preset)
        {
            SetButtonSelected(_presetStableButton, preset == NetworkSimulationPreset.Stable);
            SetButtonSelected(_presetLowButton, preset == NetworkSimulationPreset.Low);
            SetButtonSelected(_presetMediumButton, preset == NetworkSimulationPreset.Medium);
            SetButtonSelected(_presetHighButton, preset == NetworkSimulationPreset.High);
        }

        private static void SetButtonSelected(Button button, bool selected)
        {
            if (button == null || button.image == null)
            {
                return;
            }

            button.image.color = selected ? ActiveButtonColor : NeutralButtonColor;
        }

        private void BindButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private Text FindText(string relativePath)
        {
            Transform target = transform.Find(relativePath);
            return target != null ? target.GetComponent<Text>() : null;
        }

        private Button FindButton(string relativePath)
        {
            Transform target = transform.Find(relativePath);
            return target != null ? target.GetComponent<Button>() : null;
        }

        private GameObject FindObject(string relativePath)
        {
            Transform target = transform.Find(relativePath);
            return target != null ? target.gameObject : null;
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

        private void EnsureUiStructure()
        {
            RectTransform canvasRect = EnsureCanvasRoot();
            if (canvasRect == null)
            {
                return;
            }

            RectTransform leftPanel = EnsurePanel(canvasRect, "LeftPanel", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -16f), new Vector2(300f, 400f), PanelColor);
            RectTransform rightPanel = EnsurePanel(canvasRect, "RightPanel", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(340f, 510f), PanelColor);
            RectTransform menuBackdrop = EnsureStretchPanel(canvasRect, "MenuBackdrop", OverlayColor);
            RectTransform menuPanel = EnsurePanel(canvasRect, "MenuPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(540f, 360f), new Color(0.05f, 0.08f, 0.12f, 0.94f));
            RectTransform countdownBackdrop = EnsureStretchPanel(canvasRect, "CountdownBackdrop", OverlayColor);
            RectTransform countdownPanel = EnsurePanel(canvasRect, "CountdownPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320f, 220f), new Color(0.05f, 0.08f, 0.12f, 0.94f));
            RectTransform gameOverBackdrop = EnsureStretchPanel(canvasRect, "GameOverBackdrop", OverlayColor);
            RectTransform gameOverPanel = EnsurePanel(canvasRect, "GameOverPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500f, 300f), new Color(0.05f, 0.08f, 0.12f, 0.94f));

            EnsureText(leftPanel, "SectionTitle", new Vector2(16f, -16f), new Vector2(260f, 24f), 18, FontStyle.Bold, "Match Overview", AccentColor, TextAnchor.UpperLeft);
            EnsureText(leftPanel, "TimerText", new Vector2(16f, -44f), new Vector2(268f, 40f), 34, FontStyle.Bold, string.Empty, WarningColor, TextAnchor.UpperLeft);
            EnsureText(leftPanel, "ScoreTitleText", new Vector2(16f, -96f), new Vector2(260f, 24f), 17, FontStyle.Bold, "Scoreboard", SecondaryTextColor, TextAnchor.UpperLeft);
            EnsureText(leftPanel, "ScoreHeaderLeftText", new Vector2(16f, -126f), new Vector2(176f, 22f), 15, FontStyle.Bold, "Rank  Player", SecondaryTextColor, TextAnchor.UpperLeft);
            EnsureText(leftPanel, "ScoreHeaderRightText", new Vector2(198f, -126f), new Vector2(86f, 22f), 15, FontStyle.Bold, "Score", SecondaryTextColor, TextAnchor.UpperRight);
            EnsureText(leftPanel, "ScoreNamesText", new Vector2(16f, -152f), new Vector2(182f, 152f), 18, FontStyle.Normal, string.Empty, Color.white, TextAnchor.UpperLeft);
            EnsureText(leftPanel, "ScoreValuesText", new Vector2(198f, -152f), new Vector2(86f, 152f), 18, FontStyle.Normal, string.Empty, Color.white, TextAnchor.UpperRight);
            EnsureText(leftPanel, "StatusText", new Vector2(16f, -314f), new Vector2(268f, 34f), 15, FontStyle.Normal, string.Empty, SecondaryTextColor, TextAnchor.UpperLeft);
            EnsureText(leftPanel, "WinnerText", new Vector2(16f, -350f), new Vector2(268f, 40f), 20, FontStyle.Bold, string.Empty, SuccessColor, TextAnchor.UpperLeft);

            EnsureText(rightPanel, "DebugTitle", new Vector2(16f, -16f), new Vector2(300f, 24f), 20, FontStyle.Bold, "Controls & Network", AccentColor, TextAnchor.UpperLeft);
            EnsureText(rightPanel, "PresetText", new Vector2(16f, -46f), new Vector2(300f, 24f), 16, FontStyle.Bold, "Preset: Stable", SecondaryTextColor, TextAnchor.UpperLeft);
            EnsureText(rightPanel, "DebugText", new Vector2(16f, -78f), new Vector2(308f, 150f), 16, FontStyle.Normal, "Waiting for Play mode...", DebugTextColor, TextAnchor.UpperLeft);
            EnsureButton(rightPanel, "PlayersMinus", new Vector2(16f, -240f), new Vector2(86f, 34f), "Players -", NeutralButtonColor);
            EnsureButton(rightPanel, "PlayersPlus", new Vector2(108f, -240f), new Vector2(86f, 34f), "Players +", NeutralButtonColor);
            EnsureButton(rightPanel, "TimeMinus", new Vector2(16f, -284f), new Vector2(86f, 34f), "Time -", NeutralButtonColor);
            EnsureButton(rightPanel, "TimePlus", new Vector2(108f, -284f), new Vector2(86f, 34f), "Time +", NeutralButtonColor);
            EnsureButton(rightPanel, "PresetStable", new Vector2(16f, -336f), new Vector2(72f, 34f), "Stable", NeutralButtonColor);
            EnsureButton(rightPanel, "PresetLow", new Vector2(94f, -336f), new Vector2(62f, 34f), "Low", NeutralButtonColor);
            EnsureButton(rightPanel, "PresetMedium", new Vector2(162f, -336f), new Vector2(82f, 34f), "Medium", NeutralButtonColor);
            EnsureButton(rightPanel, "PresetHigh", new Vector2(250f, -336f), new Vector2(62f, 34f), "High", NeutralButtonColor);
            EnsureButton(rightPanel, "SpikeToggle", new Vector2(16f, -386f), new Vector2(136f, 38f), "Toggle Spike", NeutralButtonColor);
            EnsureButton(rightPanel, "Restart", new Vector2(16f, -438f), new Vector2(136f, 38f), "Restart Match", PrimaryButtonColor);
            EnsureButton(rightPanel, "Exit", new Vector2(164f, -438f), new Vector2(136f, 38f), "Exit", DangerButtonColor);

            EnsureText(menuPanel, "MenuTitle", new Vector2(24f, -28f), new Vector2(492f, 60f), 42, FontStyle.Bold, "Egg Test", Color.white, TextAnchor.UpperCenter);
            EnsureText(menuPanel, "MenuSubtitle", new Vector2(32f, -104f), new Vector2(476f, 96f), 18, FontStyle.Normal, "Collect eggs before the timer ends.\nOutscore the bots and test the network presets.", SecondaryTextColor, TextAnchor.UpperCenter);
            EnsureButton(menuPanel, "Start", new Vector2(145f, -226f), new Vector2(250f, 46f), "Start Game", PrimaryButtonColor);
            EnsureButton(menuPanel, "Exit", new Vector2(145f, -284f), new Vector2(250f, 42f), "Exit", DangerButtonColor);

            EnsureText(countdownPanel, "CountdownValue", new Vector2(0f, -34f), new Vector2(320f, 90f), 64, FontStyle.Bold, "3", WarningColor, TextAnchor.UpperCenter);
            EnsureText(countdownPanel, "CountdownSubtitle", new Vector2(28f, -132f), new Vector2(264f, 48f), 18, FontStyle.Normal, "Get ready! Match starts soon.", SecondaryTextColor, TextAnchor.UpperCenter);

            EnsureText(gameOverPanel, "GameOverTitle", new Vector2(24f, -28f), new Vector2(452f, 48f), 36, FontStyle.Bold, "Game Over", Color.white, TextAnchor.UpperCenter);
            EnsureText(gameOverPanel, "GameOverWinner", new Vector2(32f, -96f), new Vector2(436f, 84f), 22, FontStyle.Bold, "Winner: Unknown", SuccessColor, TextAnchor.UpperCenter);
            EnsureButton(gameOverPanel, "Restart", new Vector2(70f, -210f), new Vector2(160f, 42f), "Restart", PrimaryButtonColor);
            EnsureButton(gameOverPanel, "Exit", new Vector2(270f, -210f), new Vector2(160f, 42f), "Exit", DangerButtonColor);

            StylePanel(leftPanel, PanelColor);
            StylePanel(rightPanel, PanelColor);
            StylePanel(menuPanel, new Color(0.05f, 0.08f, 0.12f, 0.94f));
            StylePanel(countdownPanel, new Color(0.05f, 0.08f, 0.12f, 0.94f));
            StylePanel(gameOverPanel, new Color(0.05f, 0.08f, 0.12f, 0.94f));
            StyleStretchPanel(menuBackdrop, OverlayColor);
            StyleStretchPanel(countdownBackdrop, OverlayColor);
            StyleStretchPanel(gameOverBackdrop, OverlayColor);
        }

        private RectTransform EnsureCanvasRoot()
        {
            Transform canvasTransform = transform.Find("CanvasRoot");
            if (canvasTransform == null)
            {
                GameObject canvasObject = new GameObject("CanvasRoot", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvasTransform = canvasObject.transform;
                canvasTransform.SetParent(transform, false);
            }

            Canvas canvas = canvasTransform.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = canvasTransform.gameObject.AddComponent<Canvas>();
            }
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasTransform.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = canvasTransform.gameObject.AddComponent<CanvasScaler>();
            }
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            if (canvasTransform.GetComponent<GraphicRaycaster>() == null)
            {
                canvasTransform.gameObject.AddComponent<GraphicRaycaster>();
            }

            RectTransform canvasRect = canvasTransform as RectTransform;
            if (canvasRect == null)
            {
                return null;
            }

            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.offsetMin = Vector2.zero;
            canvasRect.offsetMax = Vector2.zero;
            return canvasRect;
        }

        private static RectTransform EnsurePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size, Color color)
        {
            GameObject panelObject = GetOrCreateUiObject(parent, name);
            Image panelImage = panelObject.GetComponent<Image>();
            if (panelImage == null)
            {
                panelImage = panelObject.AddComponent<Image>();
            }
            panelImage.color = color;

            Outline outline = panelObject.GetComponent<Outline>();
            if (outline == null)
            {
                outline = panelObject.AddComponent<Outline>();
            }
            outline.effectColor = PanelBorderColor;
            outline.effectDistance = new Vector2(1f, -1f);

            RectTransform rect = panelObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(
                anchorMin.x == anchorMax.x ? anchorMin.x : 0.5f,
                anchorMin.y == anchorMax.y ? anchorMin.y : 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return rect;
        }

        private static RectTransform EnsureStretchPanel(Transform parent, string name, Color color)
        {
            GameObject panelObject = GetOrCreateUiObject(parent, name);
            Image panelImage = panelObject.GetComponent<Image>();
            if (panelImage == null)
            {
                panelImage = panelObject.AddComponent<Image>();
            }
            panelImage.color = color;

            RectTransform rect = panelObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static void StylePanel(RectTransform panelRect, Color color)
        {
            if (panelRect == null)
            {
                return;
            }

            Image image = panelRect.GetComponent<Image>();
            if (image != null)
            {
                image.color = color;
            }
        }

        private static void StyleStretchPanel(RectTransform panelRect, Color color)
        {
            if (panelRect == null)
            {
                return;
            }

            Image image = panelRect.GetComponent<Image>();
            if (image != null)
            {
                image.color = color;
            }
        }

        private static Text EnsureText(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, int fontSize, FontStyle fontStyle, string textValue, Color color, TextAnchor alignment)
        {
            GameObject textObject = GetOrCreateUiObject(parent, name);
            if (textObject.GetComponent<CanvasRenderer>() == null)
            {
                textObject.AddComponent<CanvasRenderer>();
            }

            Text text = textObject.GetComponent<Text>();
            if (text == null)
            {
                text = textObject.AddComponent<Text>();
            }

            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.text = textValue;

            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return text;
        }

        private static Button EnsureButton(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, string label, Color fillColor)
        {
            GameObject buttonObject = GetOrCreateUiObject(parent, name);
            if (buttonObject.GetComponent<CanvasRenderer>() == null)
            {
                buttonObject.AddComponent<CanvasRenderer>();
            }

            Image image = buttonObject.GetComponent<Image>();
            if (image == null)
            {
                image = buttonObject.AddComponent<Image>();
            }
            image.color = fillColor;

            Outline outline = buttonObject.GetComponent<Outline>();
            if (outline == null)
            {
                outline = buttonObject.AddComponent<Outline>();
            }
            outline.effectColor = new Color(0f, 0f, 0f, 0.45f);
            outline.effectDistance = new Vector2(1f, -1f);

            Button button = buttonObject.GetComponent<Button>();
            if (button == null)
            {
                button = buttonObject.AddComponent<Button>();
            }
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = BuildButtonColors(fillColor);

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Text labelText = EnsureText(buttonObject.transform, "Label", Vector2.zero, Vector2.zero, 16, FontStyle.Bold, label, Color.white, TextAnchor.MiddleCenter);
            RectTransform labelRect = labelText.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            return button;
        }

        private static ColorBlock BuildButtonColors(Color baseColor)
        {
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = baseColor;
            colors.highlightedColor = Color.Lerp(baseColor, Color.white, 0.14f);
            colors.pressedColor = Color.Lerp(baseColor, Color.black, 0.18f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(baseColor.r * 0.6f, baseColor.g * 0.6f, baseColor.b * 0.6f, 0.6f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            return colors;
        }

        private static GameObject GetOrCreateUiObject(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                return existing.gameObject;
            }

            GameObject created = new GameObject(name, typeof(RectTransform));
            created.transform.SetParent(parent, false);
            return created;
        }
    }
}
