using System.Collections.Generic;
using System.Text;
using EggTest.Shared;
using UnityEngine;
using UnityEngine.UI;

namespace EggTest.Client
{
    public sealed class HudPresenter : MonoBehaviour
    {
        private Font _font;
        private Text _timerText;
        private Text _scoreboardText;
        private Text _statusText;
        private Text _winnerText;
        private Text _debugText;
        private Text _networkPresetText;
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
        private readonly StringBuilder _scoreboardBuilder = new StringBuilder(128);
        private readonly StringBuilder _debugBuilder = new StringBuilder(160);
        private float _nextRenderTime;

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
            if (Application.isPlaying)
            {
                WireButtons();
            }
        }

        public void SetNetworkPresetLabel(string label)
        {
            CacheReferences();
            if (_networkPresetText != null)
            {
                _networkPresetText.text = "Preset: " + label;
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
            if (_timerText == null)
            {
                return;
            }

            if (Time.unscaledTime < _nextRenderTime)
            {
                return;
            }

            _nextRenderTime = Time.unscaledTime + 0.10f;

            _timerText.text = "Time: " + Mathf.CeilToInt(remainingTime) + "s";

            _scoreboardBuilder.Length = 0;
            _scoreboardBuilder.AppendLine("Scores");
            for (int i = 0; i < standings.Count; i++)
            {
                ScoreEntry entry = standings[i];
                _scoreboardBuilder
                    .Append(i + 1)
                    .Append(". ")
                    .Append(entry.DisplayName)
                    .Append(" - ")
                    .Append(entry.Score)
                    .AppendLine();
            }

            _scoreboardText.text = _scoreboardBuilder.ToString();
            _statusText.text = "Status: " + status;
            _winnerText.text = winners;

            if (networkSettings != null && _debugText != null)
            {
                _debugBuilder.Length = 0;
                _debugBuilder
                    .Append("Players: ").Append(playerCount).Append('\n')
                    .Append("Duration: ").Append(durationSeconds).Append("s\n")
                    .Append("Latency: ").Append(Mathf.RoundToInt(networkSettings.BaseLatencyMs)).Append(" ms\n")
                    .Append("Jitter: ").Append(Mathf.RoundToInt(networkSettings.JitterMs)).Append(" ms\n")
                    .Append("Spike: ").Append(networkSettings.SpikeEnabled ? "ON" : "OFF").Append('\n')
                    .Append("Snapshots: ").Append(snapshotMinInterval.ToString("F2")).Append('-').Append(snapshotMaxInterval.ToString("F2")).Append("s\n")
                    .Append("Observed dt: ").Append(smoothedSnapshotInterval.ToString("F2")).Append("s\n")
                    .Append("Interp back-time: ").Append(interpolationBackTime.ToString("F2")).Append('s');
                _debugText.text = _debugBuilder.ToString();
            }
        }

        private void CacheReferences()
        {
            if (_font == null)
            {
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            if (_timerText == null)
            {
                _timerText = FindText("CanvasRoot/LeftPanel/TimerText");
                _scoreboardText = FindText("CanvasRoot/LeftPanel/ScoreboardText");
                _statusText = FindText("CanvasRoot/LeftPanel/StatusText");
                _winnerText = FindText("CanvasRoot/LeftPanel/WinnerText");
                _networkPresetText = FindText("CanvasRoot/RightPanel/PresetText");
                _debugText = FindText("CanvasRoot/RightPanel/DebugText");
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
                if (texts[i] != null && texts[i].font == null)
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

        public void EnsureAuthoredUi()
        {
            EnsureUiStructure();
            CacheReferences();
            ApplyFontToAllTexts();
        }

        private void EnsureUiStructure()
        {
            Transform canvasTransform = transform.Find("CanvasRoot");
            if (canvasTransform == null)
            {
                GameObject canvasObject = new GameObject("CanvasRoot", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvasTransform = canvasObject.transform;
                canvasTransform.SetParent(transform, false);
                Canvas canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
            }

            RectTransform canvasRect = canvasTransform as RectTransform;
            if (canvasRect == null)
            {
                return;
            }

            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.offsetMin = Vector2.zero;
            canvasRect.offsetMax = Vector2.zero;

            RectTransform leftPanel = EnsurePanel(canvasRect, "LeftPanel", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -12f), new Vector2(250f, 360f));
            EnsureText(leftPanel, "TimerText", new Vector2(10f, -10f), new Vector2(230f, 32f), 24, FontStyle.Bold, string.Empty);
            EnsureText(leftPanel, "ScoreboardText", new Vector2(10f, -50f), new Vector2(230f, 180f), 18, FontStyle.Normal, string.Empty);
            EnsureText(leftPanel, "StatusText", new Vector2(10f, -240f), new Vector2(230f, 45f), 16, FontStyle.Normal, string.Empty);
            EnsureText(leftPanel, "WinnerText", new Vector2(10f, -290f), new Vector2(230f, 50f), 18, FontStyle.Bold, string.Empty);

            RectTransform rightPanel = EnsurePanel(canvasRect, "RightPanel", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -12f), new Vector2(300f, 430f));
            EnsureText(rightPanel, "DebugTitle", new Vector2(10f, -10f), new Vector2(240f, 24f), 20, FontStyle.Bold, "Debug Controls");
            EnsureText(rightPanel, "PresetText", new Vector2(10f, -40f), new Vector2(240f, 24f), 16, FontStyle.Bold, "Preset: Stable");
            EnsureText(rightPanel, "DebugText", new Vector2(10f, -70f), new Vector2(280f, 140f), 16, FontStyle.Normal, "Waiting for Play mode...");
            EnsureButton(rightPanel, "PlayersMinus", new Vector2(10f, -220f), new Vector2(75f, 30f), "Players -");
            EnsureButton(rightPanel, "PlayersPlus", new Vector2(95f, -220f), new Vector2(75f, 30f), "Players +");
            EnsureButton(rightPanel, "TimeMinus", new Vector2(10f, -260f), new Vector2(75f, 30f), "Time -");
            EnsureButton(rightPanel, "TimePlus", new Vector2(95f, -260f), new Vector2(75f, 30f), "Time +");
            EnsureButton(rightPanel, "PresetStable", new Vector2(10f, -310f), new Vector2(65f, 30f), "Stable");
            EnsureButton(rightPanel, "PresetLow", new Vector2(80f, -310f), new Vector2(55f, 30f), "Low");
            EnsureButton(rightPanel, "PresetMedium", new Vector2(140f, -310f), new Vector2(75f, 30f), "Medium");
            EnsureButton(rightPanel, "PresetHigh", new Vector2(220f, -310f), new Vector2(55f, 30f), "High");
            EnsureButton(rightPanel, "SpikeToggle", new Vector2(10f, -350f), new Vector2(110f, 30f), "Toggle Spike");
            EnsureButton(rightPanel, "Restart", new Vector2(130f, -350f), new Vector2(110f, 30f), "Restart");
        }

        private static RectTransform EnsurePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size)
        {
            GameObject panelObject = GetOrCreateUiObject(parent, name);
            Image panelImage = panelObject.GetComponent<Image>();
            if (panelImage == null)
            {
                panelImage = panelObject.AddComponent<Image>();
            }

            panelImage.color = new Color(0f, 0f, 0f, 0.45f);

            RectTransform rect = panelObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(anchorMin.x == 0f ? 0f : 1f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return rect;
        }

        private static Text EnsureText(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, int fontSize, FontStyle fontStyle, string textValue)
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
            text.color = Color.white;
            text.alignment = TextAnchor.UpperLeft;
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

        private static Button EnsureButton(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, string label)
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

            image.color = new Color(0.2f, 0.2f, 0.25f, 0.95f);

            Button button = buttonObject.GetComponent<Button>();
            if (button == null)
            {
                button = buttonObject.AddComponent<Button>();
            }

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Text labelText = EnsureText(buttonObject.transform, "Label", Vector2.zero, Vector2.zero, 14, FontStyle.Bold, label);
            labelText.alignment = TextAnchor.MiddleCenter;
            RectTransform labelRect = labelText.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            return button;
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
