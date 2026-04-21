using System.Collections.Generic;
using EggTest.Shared;
using UnityEngine;

namespace EggTest.Client
{
    /// <summary>
    /// Client-side coordinator for local prediction, remote interpolation, UI updates, and view creation.
    /// </summary>
    public sealed class ClientGameController
    {
        private struct PendingInputState
        {
            public int Sequence;
            public Vector2 Direction;
            public double LocalTime;
        }

        private readonly GameConfig _config;
        private readonly ArenaDefinition _arena;
        private readonly INetworkTransport _transport;
        private readonly Transform _playersRoot;
        private readonly Transform _eggsRoot;
        private readonly HudPresenter _hud;

        private readonly Dictionary<PlayerId, PlayerProfile> _profiles = new Dictionary<PlayerId, PlayerProfile>();
        private readonly Dictionary<PlayerId, PlayerView> _playerViews = new Dictionary<PlayerId, PlayerView>();
        private readonly Dictionary<EggId, EggView> _eggViews = new Dictionary<EggId, EggView>();
        private readonly Dictionary<PlayerId, int> _scores = new Dictionary<PlayerId, int>();
        private readonly Stack<EggView> _eggViewPool = new Stack<EggView>();
        private readonly List<PendingInputState> _pendingLocalInputs = new List<PendingInputState>();
        private readonly List<EggId> _eggsToRemoveBuffer = new List<EggId>();
        private readonly List<string> _winnerNamesBuffer = new List<string>();
        private readonly List<ScoreEntry> _standingsBuffer = new List<ScoreEntry>();
        private readonly HashSet<EggId> _seenEggsBuffer = new HashSet<EggId>();

        private Vector3 _predictedLocalPosition;
        private Vector2 _predictedLocalDirection;
        private Vector3 _authoritativeLocalPosition;
        private Vector2 _authoritativeLocalDirection;
        private double _authoritativeLocalServerTime;
        private int _localInputSequence;
        private float _inputHeartbeat;
        private Vector2 _lastSentDirection;
        private int _latestSnapshotSequence;
        private double _latestServerTime;
        private double _estimatedServerTime;
        private double _serverTimeOffset;
        private double _lastSnapshotReceiptLocalTime;
        private double _lastServerTimeAtReceipt;
        private float _smoothedSnapshotInterval = 0.30f;
        private float _currentInterpolationBackTime = 0.30f;
        private bool _hasReceivedLocalSnapshot;
        private float _remainingTime;
        private bool _matchEnded;
        private string _statusText = "Waiting for server...";
        private string _winnerText = string.Empty;

        private NetworkPresetProfile CurrentPresetProfile
        {
            get { return NetworkPresetProfiles.Get(_transport.CurrentPreset); }
        }

        public bool MatchEnded
        {
            get { return _matchEnded; }
        }

        public string WinnerSummary
        {
            get { return _winnerText; }
        }

        public ClientGameController(GameConfig config, ArenaDefinition arena, INetworkTransport transport, Transform playersRoot, Transform eggsRoot, HudPresenter hud)
        {
            _config = config;
            _arena = arena;
            _transport = transport;
            _playersRoot = playersRoot;
            _eggsRoot = eggsRoot;
            _hud = hud;
        }

        public void CaptureLocalInput(Vector2 rawInput, float deltaTime, double now)
        {
            if (_matchEnded || !_profiles.ContainsKey(new PlayerId(0)))
            {
                return;
            }

            Vector2 direction = GameMath.Cardinalize(rawInput);
            _predictedLocalDirection = direction;
            _predictedLocalPosition = GameMath.SimulateKinematicMove(_arena, _predictedLocalPosition, direction, _config.PlayerMoveSpeed, deltaTime, _config.PlayerRadius);

            PlayerView localView;
            if (_playerViews.TryGetValue(new PlayerId(0), out localView))
            {
                localView.SetLocalPredictedState(_predictedLocalPosition, _predictedLocalDirection);
            }

            _inputHeartbeat -= deltaTime;
            if (_inputHeartbeat > 0f && direction == _lastSentDirection)
            {
                return;
            }

            _inputHeartbeat = CurrentPresetProfile.InputSendInterval;
            _lastSentDirection = direction;
            _localInputSequence++;
            _transport.SendToServer(new PlayerInputMessage
            {
                SentTime = now,
                PlayerId = new PlayerId(0),
                Sequence = _localInputSequence,
                Direction = direction,
            }, now);
            _pendingLocalInputs.Add(new PendingInputState
            {
                Sequence = _localInputSequence,
                Direction = direction,
                LocalTime = now,
            });
            GameTrace.Verbose("Input", "Client sent input seq " + _localInputSequence + " dir=" + direction + ".");
        }

        public void Receive(IMessage message)
        {
            MatchStartedMessage started = message as MatchStartedMessage;
            if (started != null)
            {
                GameTrace.Verbose("Client", "Received MatchStartedMessage.");
                OnMatchStarted(started);
                return;
            }

            EggSpawnedMessage eggSpawned = message as EggSpawnedMessage;
            if (eggSpawned != null)
            {
                GameTrace.Verbose("Client", "Received EggSpawnedMessage for " + eggSpawned.Egg.Id + ".");
                OnEggSpawned(eggSpawned);
                return;
            }

            EggCollectedMessage eggCollected = message as EggCollectedMessage;
            if (eggCollected != null)
            {
                GameTrace.Verbose("Client", "Received EggCollectedMessage for " + eggCollected.EggId + ".");
                OnEggCollected(eggCollected);
                return;
            }

            WorldSnapshotMessage snapshot = message as WorldSnapshotMessage;
            if (snapshot != null)
            {
                GameTrace.Verbose("Client", "Received WorldSnapshotMessage #" + snapshot.SnapshotSequence + ".");
                OnWorldSnapshot(snapshot);
                return;
            }

            MatchEndedMessage ended = message as MatchEndedMessage;
            if (ended != null)
            {
                GameTrace.Verbose("Client", "Received MatchEndedMessage.");
                OnMatchEnded(ended);
            }
        }

        public void Tick(float deltaTime, double now)
        {
            if (!_matchEnded)
            {
                _remainingTime = Mathf.Max(0f, _remainingTime - deltaTime);
            }

            _estimatedServerTime = _lastServerTimeAtReceipt + Mathf.Max(0f, (float)(now - _lastSnapshotReceiptLocalTime));
            ApplyLocalReconciliation(deltaTime);
            UpdateRemotePlayers(now);
            UpdateEggAnimations(now);
            RefreshHud();
        }

        public void Dispose()
        {
            foreach (KeyValuePair<PlayerId, PlayerView> pair in _playerViews)
            {
                if (pair.Value != null)
                {
                    Object.Destroy(pair.Value.gameObject);
                }
            }

            foreach (KeyValuePair<EggId, EggView> pair in _eggViews)
            {
                if (pair.Value != null)
                {
                    Object.Destroy(pair.Value.gameObject);
                }
            }

            _playerViews.Clear();
            _eggViews.Clear();
            while (_eggViewPool.Count > 0)
            {
                EggView pooledView = _eggViewPool.Pop();
                if (pooledView != null)
                {
                    Object.Destroy(pooledView.gameObject);
                }
            }
            _profiles.Clear();
            _scores.Clear();
        }

        private void OnMatchStarted(MatchStartedMessage message)
        {
            GameTrace.Verbose("Client", "Match started on client with " + message.Players.Count + " player profiles.");
            _profiles.Clear();
            _scores.Clear();
            _matchEnded = false;
            _winnerText = string.Empty;
            _remainingTime = message.DurationSeconds;
            _statusText = "Collect more eggs than the bots before time runs out.";
            _localInputSequence = 0;
            _latestSnapshotSequence = 0;
            _latestServerTime = 0.0;
            _estimatedServerTime = 0.0;
            _serverTimeOffset = 0.0;
            _lastServerTimeAtReceipt = 0.0;
            _lastSnapshotReceiptLocalTime = 0.0;
            _smoothedSnapshotInterval = CurrentPresetProfile.RemoteInterpolationBackTime;
            _currentInterpolationBackTime = CurrentPresetProfile.RemoteInterpolationBackTime;
            _inputHeartbeat = 0f;
            _lastSentDirection = Vector2.zero;
            _predictedLocalDirection = Vector2.zero;
            _predictedLocalPosition = Vector3.zero;
            _authoritativeLocalPosition = Vector3.zero;
            _authoritativeLocalDirection = Vector2.zero;
            _authoritativeLocalServerTime = 0.0;
            _hasReceivedLocalSnapshot = false;
            _pendingLocalInputs.Clear();

            for (int i = 0; i < message.Players.Count; i++)
            {
                PlayerProfile profile = message.Players[i];
                _profiles[profile.Id] = profile;
                _scores[profile.Id] = 0;
                EnsurePlayerView(profile);
            }
        }

        private void OnEggSpawned(EggSpawnedMessage message)
        {
            EnsureEggView(message.Egg);
            _statusText = "A new egg spawned.";
            GameTrace.Verbose("Client", "Displayed " + message.Egg.Id + " at " + message.Egg.Position + ".");
        }

        private void OnEggCollected(EggCollectedMessage message)
        {
            ReleaseEggView(message.EggId);

            PlayerProfile profile;
            if (_profiles.TryGetValue(message.CollectorId, out profile))
            {
                _statusText = profile.DisplayName + " collected an egg.";
            }

            _scores[message.CollectorId] = message.NewScore;
            GameTrace.Verbose("Client", "Updated score for " + message.CollectorId + " to " + message.NewScore + ".");
        }

        private void OnWorldSnapshot(WorldSnapshotMessage message)
        {
            if (message.SnapshotSequence <= _latestSnapshotSequence)
            {
                return;
            }

            if (_latestSnapshotSequence > 0)
            {
                float rawSnapshotInterval = (float)(message.ServerTime - _latestServerTime);
                if (rawSnapshotInterval > 0.0001f)
                {
                    _smoothedSnapshotInterval = Mathf.Lerp(_smoothedSnapshotInterval, rawSnapshotInterval, 0.35f);
                }
            }

            _latestSnapshotSequence = message.SnapshotSequence;
            _latestServerTime = message.ServerTime;
            _lastServerTimeAtReceipt = message.ServerTime;
            _lastSnapshotReceiptLocalTime = Time.unscaledTimeAsDouble;
            _serverTimeOffset = message.ServerTime - _lastSnapshotReceiptLocalTime;
            _remainingTime = message.RemainingTime;
            GameTrace.LogEvery("Snapshot", "Accepted", 0.5f, "Client accepted snapshot #" + message.SnapshotSequence + " (players=" + message.Players.Count + ", eggs=" + message.Eggs.Count + ", dt≈" + _smoothedSnapshotInterval.ToString("F2") + "s).", verboseOnly: true);

            _seenEggsBuffer.Clear();
            for (int i = 0; i < message.Eggs.Count; i++)
            {
                EggSnapshot egg = message.Eggs[i];
                _seenEggsBuffer.Add(egg.Id);
                EnsureEggView(egg).ApplySnapshot(egg, _config.EggPalette);
            }

            _eggsToRemoveBuffer.Clear();
            foreach (KeyValuePair<EggId, EggView> pair in _eggViews)
            {
                if (!_seenEggsBuffer.Contains(pair.Key))
                {
                    _eggsToRemoveBuffer.Add(pair.Key);
                }
            }

            for (int i = 0; i < _eggsToRemoveBuffer.Count; i++)
            {
                ReleaseEggView(_eggsToRemoveBuffer[i]);
            }

            for (int i = 0; i < message.Players.Count; i++)
            {
                PlayerSnapshot player = message.Players[i];
                PlayerProfile profile;
                if (!_profiles.TryGetValue(player.Id, out profile))
                {
                    continue;
                }

                PlayerView view = EnsurePlayerView(profile);
                _scores[player.Id] = player.Score;
                view.SetScore(player.Score);

                if (profile.Kind == PlayerKind.LocalHuman)
                {
                    if (!_hasReceivedLocalSnapshot)
                    {
                        _predictedLocalPosition = player.Position;
                        _hasReceivedLocalSnapshot = true;
                        GameTrace.Verbose("Client", "Initialized local player from authoritative spawn at " + player.Position + ".");
                    }

                    _authoritativeLocalPosition = player.Position;
                    _authoritativeLocalDirection = player.MoveDirection;
                    _authoritativeLocalServerTime = message.ServerTime;
                    DropAcknowledgedInputs(player.LastInputSequence);
                    view.SetLocalPredictedState(_predictedLocalPosition, _predictedLocalDirection);
                }
                else
                {
                    view.PushRemoteSnapshot(player, message.ServerTime);
                }
            }
        }

        private void OnMatchEnded(MatchEndedMessage message)
        {
            _matchEnded = true;
            _statusText = "Match ended.";

            _winnerNamesBuffer.Clear();
            for (int i = 0; i < message.WinnerIds.Count; i++)
            {
                PlayerProfile profile;
                if (_profiles.TryGetValue(message.WinnerIds[i], out profile))
                {
                    _winnerNamesBuffer.Add(profile.DisplayName);
                }
            }

            _winnerText = _winnerNamesBuffer.Count > 1
                ? "Winners: " + string.Join(", ", _winnerNamesBuffer.ToArray())
                : "Winner: " + (_winnerNamesBuffer.Count == 1 ? _winnerNamesBuffer[0] : "Unknown");
            GameTrace.Log("Match", _winnerText);
        }

        private void ApplyLocalReconciliation(float deltaTime)
        {
            PlayerView localView;
            if (!_playerViews.TryGetValue(new PlayerId(0), out localView))
            {
                return;
            }

            NetworkPresetProfile profile = CurrentPresetProfile;

            Vector3 projectedAuthority = RebuildPredictedLocalPosition();

            Vector3 error = projectedAuthority - _predictedLocalPosition;
            float distance = error.magnitude;

            if (distance <= 0.001f)
            {
                localView.SetLocalPredictedState(_predictedLocalPosition, _predictedLocalDirection);
                return;
            }

            // Replay-based prediction gives us a better target than the last raw authoritative position.
            // We still blend toward it slightly so visual corrections stay smooth when network timing changes.
            float correctionSpeed = distance > profile.LocalCorrectionThreshold ? profile.LocalHardCorrection : profile.LocalSoftCorrection;
            _predictedLocalPosition = Vector3.MoveTowards(_predictedLocalPosition, projectedAuthority, correctionSpeed * deltaTime * 2f);
            _predictedLocalDirection = GetPredictedLocalDirection();
            localView.SetLocalPredictedState(_predictedLocalPosition, _predictedLocalDirection);
            GameTrace.LogEvery("Client", "LocalCorrection", 0.5f, "Local reconciliation correcting error of " + distance.ToString("F2") + " units.", verboseOnly: true);
        }

        private void UpdateRemotePlayers(double now)
        {
            NetworkPresetProfile presetProfile = CurrentPresetProfile;
            float interpolationBackTime = ComputeInterpolationBackTime();
            _currentInterpolationBackTime = interpolationBackTime;

            foreach (KeyValuePair<PlayerId, PlayerView> pair in _playerViews)
            {
                PlayerProfile playerProfile;
                if (!_profiles.TryGetValue(pair.Key, out playerProfile) || playerProfile.Kind == PlayerKind.LocalHuman)
                {
                    continue;
                }

                pair.Value.TickRemote(_estimatedServerTime, interpolationBackTime, _config.PlayerMoveSpeed, presetProfile.RemoteExtrapolationLimit);
            }

            GameTrace.LogEvery("Client", "RemoteSmoothing", 1.0f, "Estimated server time=" + _estimatedServerTime.ToString("F2") + ", interpolation back-time=" + interpolationBackTime.ToString("F2") + "s.", verboseOnly: true);
        }

        private void UpdateEggAnimations(double now)
        {
            foreach (KeyValuePair<EggId, EggView> pair in _eggViews)
            {
                pair.Value.Tick(now);
            }
        }

        private void RefreshHud()
        {
            _standingsBuffer.Clear();
            foreach (KeyValuePair<PlayerId, PlayerProfile> pair in _profiles)
            {
                int score;
                _scores.TryGetValue(pair.Key, out score);
                _standingsBuffer.Add(new ScoreEntry
                {
                    PlayerId = pair.Key,
                    DisplayName = pair.Value.DisplayName,
                    Score = score,
                });
            }

            _standingsBuffer.Sort((left, right) =>
            {
                int scoreCompare = right.Score.CompareTo(left.Score);
                return scoreCompare != 0 ? scoreCompare : left.PlayerId.Value.CompareTo(right.PlayerId.Value);
            });

            _hud.Render(
                _remainingTime,
                _standingsBuffer,
                _statusText,
                _winnerText,
                _config.PlayerCount,
                Mathf.RoundToInt(_config.MatchDurationSeconds),
                _config.TargetActiveEggCount,
                _transport.Settings,
                _config.SnapshotMinInterval,
                _config.SnapshotMaxInterval,
                _smoothedSnapshotInterval,
                _currentInterpolationBackTime);
        }

        private float ComputeInterpolationBackTime()
        {
            NetworkPresetProfile presetProfile = CurrentPresetProfile;
            return Mathf.Clamp(
                Mathf.Max(presetProfile.RemoteInterpolationBackTime, _smoothedSnapshotInterval + presetProfile.RemoteInterpolationSafetyMargin),
                presetProfile.RemoteInterpolationBackTime,
                _config.SnapshotMaxInterval);
        }

        private PlayerView EnsurePlayerView(PlayerProfile profile)
        {
            PlayerView existing;
            if (_playerViews.TryGetValue(profile.Id, out existing))
            {
                return existing;
            }

            GameObject viewObject = new GameObject(profile.DisplayName);
            viewObject.transform.SetParent(_playersRoot);
            PlayerView view = viewObject.AddComponent<PlayerView>();
            view.Initialize(profile);
            _playerViews.Add(profile.Id, view);
            GameTrace.Verbose("Client", "Created player view for " + profile.DisplayName + ".");
            return view;
        }

        private EggView EnsureEggView(EggSnapshot snapshot)
        {
            EggView existing;
            if (_eggViews.TryGetValue(snapshot.Id, out existing))
            {
                return existing;
            }

            EggView view = _eggViewPool.Count > 0 ? _eggViewPool.Pop() : CreateEggView();
            view.gameObject.name = "Egg_" + snapshot.Id.Value;
            view.gameObject.SetActive(true);
            view.ApplySnapshot(snapshot, _config.EggPalette);
            _eggViews.Add(snapshot.Id, view);
            GameTrace.Verbose("Client", "Created egg view for " + snapshot.Id + ".");
            return view;
        }

        private EggView CreateEggView()
        {
            GameObject eggObject = new GameObject("EggView_Pooled");
            eggObject.transform.SetParent(_eggsRoot);
            EggView view = eggObject.AddComponent<EggView>();
            view.Initialize();
            return view;
        }

        private void ReleaseEggView(EggId eggId)
        {
            EggView eggView;
            if (!_eggViews.TryGetValue(eggId, out eggView))
            {
                return;
            }

            _eggViews.Remove(eggId);
            eggView.gameObject.SetActive(false);
            _eggViewPool.Push(eggView);
        }

        private void DropAcknowledgedInputs(int lastAcknowledgedSequence)
        {
            for (int index = _pendingLocalInputs.Count - 1; index >= 0; index--)
            {
                if (_pendingLocalInputs[index].Sequence <= lastAcknowledgedSequence)
                {
                    _pendingLocalInputs.RemoveAt(index);
                }
            }
        }

        private Vector3 RebuildPredictedLocalPosition()
        {
            if (!_hasReceivedLocalSnapshot)
            {
                return _predictedLocalPosition;
            }

            Vector3 rebuiltPosition = _authoritativeLocalPosition;
            Vector2 activeDirection = _authoritativeLocalDirection;
            double segmentStartServerTime = _authoritativeLocalServerTime;

            for (int i = 0; i < _pendingLocalInputs.Count; i++)
            {
                PendingInputState pending = _pendingLocalInputs[i];
                double pendingServerTime = pending.LocalTime + _serverTimeOffset;

                if (pendingServerTime <= segmentStartServerTime)
                {
                    activeDirection = pending.Direction;
                    continue;
                }

                if (pendingServerTime >= _estimatedServerTime)
                {
                    break;
                }

                float segmentDuration = (float)(pendingServerTime - segmentStartServerTime);
                if (segmentDuration > 0f)
                {
                    rebuiltPosition = GameMath.SimulateKinematicMove(
                        _arena,
                        rebuiltPosition,
                        activeDirection,
                        _config.PlayerMoveSpeed,
                        segmentDuration,
                        _config.PlayerRadius);
                }

                activeDirection = pending.Direction;
                segmentStartServerTime = pendingServerTime;
            }

            float tailDuration = Mathf.Max(0f, (float)(_estimatedServerTime - segmentStartServerTime));
            if (tailDuration > 0f)
            {
                rebuiltPosition = GameMath.SimulateKinematicMove(
                    _arena,
                    rebuiltPosition,
                    activeDirection,
                    _config.PlayerMoveSpeed,
                    tailDuration,
                    _config.PlayerRadius);
            }

            return rebuiltPosition;
        }

        private Vector2 GetPredictedLocalDirection()
        {
            if (_pendingLocalInputs.Count > 0)
            {
                return _pendingLocalInputs[_pendingLocalInputs.Count - 1].Direction;
            }

            return _authoritativeLocalDirection;
        }
    }
}
