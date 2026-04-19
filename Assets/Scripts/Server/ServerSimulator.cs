using System;
using System.Collections.Generic;
using EggTest.Shared;
using UnityEngine;

namespace EggTest.Server
{
    internal sealed class ServerPlayerState
    {
        public PlayerProfile Profile;
        public Vector3 Position;
        public Vector2 MoveDirection;
        public int Score;
        public int LastInputSequence;
        public BotBrain BotBrain;
    }

    internal sealed class ServerEggState
    {
        public EggId Id;
        public Vector3 Position;
        public int PaletteIndex;
    }

    internal sealed class BotBrain
    {
        public float ThinkDelay;
        public float RetargetTimer;
        public EggId? TargetEggId;
        public readonly List<GridCell> CurrentPath = new List<GridCell>();
        public int NextWaypointIndex;
    }

    public sealed class ServerSimulator
    {
        private readonly GameConfig _config;
        private readonly ArenaDefinition _arena;
        private readonly INetworkTransport _transport;
        private readonly IPathfinder _pathfinder;
        private readonly System.Random _random;

        private readonly Dictionary<PlayerId, ServerPlayerState> _players = new Dictionary<PlayerId, ServerPlayerState>();
        private readonly Dictionary<EggId, ServerEggState> _eggs = new Dictionary<EggId, ServerEggState>();
        private readonly List<PlayerProfile> _profiles = new List<PlayerProfile>();
        private readonly List<GridCell> _pathBuffer = new List<GridCell>();
        private readonly List<GridCell> _bestPathBuffer = new List<GridCell>();
        private readonly List<GridCell> _availableEggCellsBuffer = new List<GridCell>();
        private readonly List<EggId> _eggsToCollectBuffer = new List<EggId>();
        private readonly List<PlayerId> _collectorsBuffer = new List<PlayerId>();
        private readonly List<ScoreEntry> _finalScoresBuffer = new List<ScoreEntry>();
        private readonly List<PlayerId> _winnerIdsBuffer = new List<PlayerId>();

        private float _remainingTime;
        private float _simulationAccumulator;
        private float _snapshotCountdown;
        private float _eggSpawnCountdown;
        private double _serverTime;
        private double _currentTransportTime;
        private int _nextEggId;
        private int _snapshotSequence;
        private bool _isRunning;

        public ServerSimulator(GameConfig config, ArenaDefinition arena, INetworkTransport transport)
        {
            _config = config;
            _arena = arena;
            _transport = transport;
            _pathfinder = new GridPathfinder(arena);
            _random = new System.Random(config.RandomSeed);
            GameTrace.Log("Server", "Server simulator created.");
        }

        public IReadOnlyList<PlayerProfile> Profiles
        {
            get { return _profiles; }
        }

        public bool IsRunning
        {
            get { return _isRunning; }
        }

        public void StartMatch(double now)
        {
            GameTrace.Log("Match", "Starting match with " + _config.PlayerCount + " players for " + _config.MatchDurationSeconds + " seconds.");
            _players.Clear();
            _eggs.Clear();
            _profiles.Clear();
            _simulationAccumulator = 0f;
            _remainingTime = _config.MatchDurationSeconds;
            _serverTime = 0.0;
            _nextEggId = 1;
            _snapshotSequence = 0;
            _isRunning = true;
            _snapshotCountdown = RandomSnapshotInterval();
            _eggSpawnCountdown = 0f;

            for (int i = 0; i < _config.PlayerCount; i++)
            {
                PlayerId playerId = new PlayerId(i);
                PlayerProfile profile = new PlayerProfile
                {
                    Id = playerId,
                    DisplayName = i == 0 ? "You" : "Bot " + i,
                    Kind = i == 0 ? PlayerKind.LocalHuman : PlayerKind.RemoteBot,
                    Color = _config.PlayerPalette[i % _config.PlayerPalette.Length],
                };

                GridCell spawnCell = _arena.GetSpawnCell(i);
                ServerPlayerState player = new ServerPlayerState
                {
                    Profile = profile,
                    Position = _arena.CellToWorld(spawnCell),
                    MoveDirection = Vector2.zero,
                    Score = 0,
                    LastInputSequence = 0,
                    BotBrain = profile.Kind == PlayerKind.RemoteBot ? CreateBotBrain() : null,
                };

                _players.Add(playerId, player);
                _profiles.Add(profile);
                GameTrace.Verbose("Match", "Spawned " + profile.DisplayName + " at " + spawnCell + " (" + profile.Kind + ").");
            }

            MatchStartedMessage message = new MatchStartedMessage
            {
                SentTime = now,
                DurationSeconds = _config.MatchDurationSeconds,
                Players = CloneProfiles(),
            };
            _transport.SendToClient(message, now);

            // Initial snapshot gives the client enough state to render the world even before the first random publish interval.
            PublishSnapshot(now);
        }

        public void Receive(IMessage message)
        {
            PlayerInputMessage input = message as PlayerInputMessage;
            if (input == null)
            {
                return;
            }

            ServerPlayerState player;
            if (!_players.TryGetValue(input.PlayerId, out player))
            {
                return;
            }

            if (player.Profile.Kind != PlayerKind.LocalHuman)
            {
                return;
            }

            if (input.Sequence < player.LastInputSequence)
            {
                return;
            }

            player.LastInputSequence = input.Sequence;
            player.MoveDirection = GameMath.Cardinalize(input.Direction);
            GameTrace.Verbose("Input", "Server accepted input seq " + input.Sequence + " for " + input.PlayerId + " dir=" + input.Direction + ".");
        }

        public void Update(float deltaTime, double now)
        {
            if (!_isRunning)
            {
                return;
            }

            _currentTransportTime = now;
            _simulationAccumulator += deltaTime;
            while (_simulationAccumulator >= _config.ServerSimulationStep)
            {
                _simulationAccumulator -= _config.ServerSimulationStep;
                SimulateStep(_config.ServerSimulationStep);
            }

            GameTrace.LogEvery("Server", "Heartbeat", 1.0f, "Server time=" + _serverTime.ToString("F2") + " remaining=" + _remainingTime.ToString("F1") + " eggs=" + _eggs.Count + ".", verboseOnly: true);

            _snapshotCountdown -= deltaTime;
            if (_snapshotCountdown <= 0f && _isRunning)
            {
                PublishSnapshot(now);
                _snapshotCountdown = RandomSnapshotInterval();
            }
        }

        public void OnNetworkPresetChanged()
        {
            float nextInterval = RandomSnapshotInterval();
            if (_snapshotCountdown > nextInterval)
            {
                _snapshotCountdown = nextInterval;
            }

            GameTrace.Log("Server", "Server snapshot cadence refreshed for preset " + _transport.CurrentPreset + ".");
        }

        private void SimulateStep(float step)
        {
            _serverTime += step;
            _remainingTime = Mathf.Max(0f, _remainingTime - step);

            UpdateBots(step);
            MovePlayers(step);
            HandleEggSpawning(step);
            HandleEggCollection();

            if (_remainingTime <= 0f)
            {
                EndMatch();
            }
        }

        private void UpdateBots(float step)
        {
            foreach (KeyValuePair<PlayerId, ServerPlayerState> pair in _players)
            {
                ServerPlayerState player = pair.Value;
                if (player.Profile.Kind != PlayerKind.RemoteBot || player.BotBrain == null)
                {
                    continue;
                }

                BotBrain brain = player.BotBrain;
                brain.ThinkDelay -= step;
                brain.RetargetTimer -= step;

                bool shouldReconsider = brain.TargetEggId == null || brain.RetargetTimer <= 0f;
                if (brain.TargetEggId.HasValue && !_eggs.ContainsKey(brain.TargetEggId.Value))
                {
                    shouldReconsider = true;
                }

                if (shouldReconsider && brain.ThinkDelay <= 0f)
                {
                    RebuildBotPlan(player);
                    brain.RetargetTimer = _config.BotRetargetInterval;
                    brain.ThinkDelay = RandomRange(_config.BotDecisionMinDelay, _config.BotDecisionMaxDelay);
                }

                ApplyBotMoveDirection(player);
            }
        }

        private void RebuildBotPlan(ServerPlayerState player)
        {
            BotBrain brain = player.BotBrain;
            brain.TargetEggId = null;
            brain.CurrentPath.Clear();
            brain.NextWaypointIndex = 0;

            if (_eggs.Count == 0)
            {
                player.MoveDirection = Vector2.zero;
                GameTrace.Verbose("AI", player.Profile.DisplayName + " has no egg target because no eggs are active.");
                return;
            }

            GridCell startCell = _arena.WorldToCell(player.Position);
            float bestScore = float.MaxValue;
            EggId? bestEggId = null;
            _bestPathBuffer.Clear();

            foreach (KeyValuePair<EggId, ServerEggState> eggPair in _eggs)
            {
                ServerEggState egg = eggPair.Value;
                GridCell goalCell = _arena.WorldToCell(egg.Position);
                _pathBuffer.Clear();
                if (!_pathfinder.TryFindPath(startCell, goalCell, _pathBuffer))
                {
                    continue;
                }

                float pathLengthCost = _pathBuffer.Count;
                float directDistance = Vector3.Distance(player.Position, egg.Position) * 0.25f;
                float contestPenalty = EstimateContestPenalty(player.Profile.Id, egg.Position);
                float noise = RandomRange(0f, _config.BotRandomScoreNoise);
                float totalScore = pathLengthCost + directDistance + contestPenalty + noise;

                if (totalScore < bestScore)
                {
                    bestScore = totalScore;
                    bestEggId = egg.Id;
                    _bestPathBuffer.Clear();
                    _bestPathBuffer.AddRange(_pathBuffer);
                }
            }

            if (!bestEggId.HasValue || _bestPathBuffer.Count == 0)
            {
                player.MoveDirection = Vector2.zero;
                GameTrace.Verbose("AI", player.Profile.DisplayName + " could not find a reachable egg.");
                return;
            }

            brain.TargetEggId = bestEggId.Value;
            brain.CurrentPath.Clear();
            brain.CurrentPath.AddRange(_bestPathBuffer);
            brain.NextWaypointIndex = brain.CurrentPath.Count > 1 ? 1 : 0;
            GameTrace.Verbose("AI", player.Profile.DisplayName + " targeted " + bestEggId.Value + " with path length " + _bestPathBuffer.Count + ".");
        }

        private void ApplyBotMoveDirection(ServerPlayerState player)
        {
            BotBrain brain = player.BotBrain;
            if (brain.TargetEggId == null || brain.CurrentPath.Count == 0)
            {
                player.MoveDirection = Vector2.zero;
                return;
            }

            // A bot can finish all waypoints while the egg still exists for one more simulation step.
            // Guard the waypoint index before reading the list so the server never crashes on a stale path tail.
            if (brain.NextWaypointIndex < 0 || brain.NextWaypointIndex >= brain.CurrentPath.Count)
            {
                brain.TargetEggId = null;
                brain.CurrentPath.Clear();
                brain.NextWaypointIndex = 0;
                brain.RetargetTimer = 0f;
                player.MoveDirection = Vector2.zero;
                GameTrace.Verbose("AI", player.Profile.DisplayName + " exhausted its path and will retarget.");
                return;
            }

            Vector3 waypoint = _arena.CellToWorld(brain.CurrentPath[brain.NextWaypointIndex]);
            Vector3 toWaypoint = waypoint - player.Position;
            toWaypoint.y = 0f;

            if (toWaypoint.magnitude <= _config.BotWaypointTolerance)
            {
                brain.NextWaypointIndex++;
                if (brain.NextWaypointIndex >= brain.CurrentPath.Count)
                {
                    brain.TargetEggId = null;
                    brain.CurrentPath.Clear();
                    brain.NextWaypointIndex = 0;
                    brain.RetargetTimer = 0f;
                    player.MoveDirection = Vector2.zero;
                    return;
                }

                waypoint = _arena.CellToWorld(brain.CurrentPath[brain.NextWaypointIndex]);
                toWaypoint = waypoint - player.Position;
                toWaypoint.y = 0f;
            }

            player.MoveDirection = GameMath.Cardinalize(new Vector2(toWaypoint.x, toWaypoint.z));
        }

        private float EstimateContestPenalty(PlayerId currentBot, Vector3 eggPosition)
        {
            float penalty = 0f;
            foreach (KeyValuePair<PlayerId, ServerPlayerState> pair in _players)
            {
                if (pair.Key.Equals(currentBot))
                {
                    continue;
                }

                float distance = Vector3.Distance(pair.Value.Position, eggPosition);
                if (distance < 2.5f)
                {
                    penalty += 0.75f;
                }
            }

            return penalty;
        }

        private void MovePlayers(float step)
        {
            foreach (KeyValuePair<PlayerId, ServerPlayerState> pair in _players)
            {
                ServerPlayerState player = pair.Value;
                player.Position = GameMath.SimulateKinematicMove(
                    _arena,
                    player.Position,
                    player.MoveDirection,
                    _config.PlayerMoveSpeed,
                    step,
                    _config.PlayerRadius);
            }
        }

        private void HandleEggSpawning(float step)
        {
            if (_eggs.Count >= _config.TargetActiveEggCount)
            {
                return;
            }

            _eggSpawnCountdown -= step;
            if (_eggSpawnCountdown > 0f)
            {
                return;
            }

            SpawnEgg();
            _eggSpawnCountdown = RandomRange(_config.EggRespawnMinDelay, _config.EggRespawnMaxDelay);
        }

        private void SpawnEgg()
        {
            _availableEggCellsBuffer.Clear();
            for (int i = 0; i < _arena.CandidateEggCells.Count; i++)
            {
                GridCell cell = _arena.CandidateEggCells[i];
                if (IsCellOccupiedByEgg(cell))
                {
                    continue;
                }

                _availableEggCellsBuffer.Add(cell);
            }

            if (_availableEggCellsBuffer.Count == 0)
            {
                GameTrace.Warn("Spawn", "No available cells remain for egg spawning.");
                return;
            }

            GridCell spawnCell = _availableEggCellsBuffer[_random.Next(_availableEggCellsBuffer.Count)];
            ServerEggState egg = new ServerEggState
            {
                Id = new EggId(_nextEggId++),
                Position = _arena.CellToWorld(spawnCell) + Vector3.up * 0.35f,
                PaletteIndex = _random.Next(_config.EggPalette.Length),
            };

            _eggs.Add(egg.Id, egg);
            _transport.SendToClient(new EggSpawnedMessage
            {
                SentTime = _currentTransportTime,
                Egg = CloneEgg(egg),
            }, _currentTransportTime);
            GameTrace.Log("Spawn", "Spawned " + egg.Id + " at cell " + spawnCell + " with palette index " + egg.PaletteIndex + ".");
        }

        private bool IsCellOccupiedByEgg(GridCell cell)
        {
            foreach (KeyValuePair<EggId, ServerEggState> pair in _eggs)
            {
                if (_arena.WorldToCell(pair.Value.Position).Equals(cell))
                {
                    return true;
                }
            }

            return false;
        }

        private void HandleEggCollection()
        {
            if (_eggs.Count == 0)
            {
                return;
            }

            _eggsToCollectBuffer.Clear();
            _collectorsBuffer.Clear();

            foreach (KeyValuePair<EggId, ServerEggState> eggPair in _eggs)
            {
                ServerEggState egg = eggPair.Value;
                PlayerId? bestCollector = null;
                float bestDistance = float.MaxValue;

                foreach (KeyValuePair<PlayerId, ServerPlayerState> playerPair in _players)
                {
                    float distance = Vector3.Distance(playerPair.Value.Position + Vector3.up * 0.35f, egg.Position);
                    if (distance > _config.EggCollectRadius)
                    {
                        continue;
                    }

                    if (!bestCollector.HasValue || distance < bestDistance - 0.001f || (Mathf.Abs(distance - bestDistance) <= 0.001f && playerPair.Key.Value < bestCollector.Value.Value))
                    {
                        bestCollector = playerPair.Key;
                        bestDistance = distance;
                    }
                }

                if (bestCollector.HasValue)
                {
                    _eggsToCollectBuffer.Add(egg.Id);
                    _collectorsBuffer.Add(bestCollector.Value);
                }
            }

            for (int i = 0; i < _eggsToCollectBuffer.Count; i++)
            {
                ResolveEggCollection(_eggsToCollectBuffer[i], _collectorsBuffer[i]);
            }
        }

        private void ResolveEggCollection(EggId eggId, PlayerId collectorId)
        {
            ServerEggState egg;
            ServerPlayerState collector;
            if (!_eggs.TryGetValue(eggId, out egg) || !_players.TryGetValue(collectorId, out collector))
            {
                return;
            }

            _eggs.Remove(eggId);
            collector.Score++;

            _transport.SendToClient(new EggCollectedMessage
            {
                SentTime = _currentTransportTime,
                EggId = eggId,
                CollectorId = collectorId,
                NewScore = collector.Score,
            }, _currentTransportTime);
            GameTrace.Log("Score", collector.Profile.DisplayName + " collected " + eggId + " and now has score " + collector.Score + ".");

            // Force nearby bots to reconsider quickly after an egg disappears.
            foreach (KeyValuePair<PlayerId, ServerPlayerState> pair in _players)
            {
                if (pair.Value.BotBrain != null)
                {
                    pair.Value.BotBrain.RetargetTimer = 0f;
                }
            }
        }

        private void PublishSnapshot(double now)
        {
            WorldSnapshotMessage snapshot = new WorldSnapshotMessage
            {
                SentTime = now,
                SnapshotSequence = ++_snapshotSequence,
                ServerTime = _serverTime,
                RemainingTime = _remainingTime,
            };

            foreach (KeyValuePair<PlayerId, ServerPlayerState> pair in _players)
            {
                snapshot.Players.Add(new PlayerSnapshot
                {
                    Id = pair.Key,
                    Position = pair.Value.Position,
                    MoveDirection = pair.Value.MoveDirection,
                    Score = pair.Value.Score,
                    LastInputSequence = pair.Value.LastInputSequence,
                });
            }

            foreach (KeyValuePair<EggId, ServerEggState> pair in _eggs)
            {
                snapshot.Eggs.Add(CloneEgg(pair.Value));
            }

            _transport.SendToClient(snapshot, now);
            GameTrace.LogEvery("Snapshot", "Published", 0.5f, "Published snapshot #" + snapshot.SnapshotSequence + " with " + snapshot.Players.Count + " players and " + snapshot.Eggs.Count + " eggs.", verboseOnly: true);
        }

        private void EndMatch()
        {
            _isRunning = false;

            int bestScore = int.MinValue;
            _finalScoresBuffer.Clear();
            _winnerIdsBuffer.Clear();

            foreach (KeyValuePair<PlayerId, ServerPlayerState> pair in _players)
            {
                ScoreEntry entry = new ScoreEntry
                {
                    PlayerId = pair.Key,
                    DisplayName = pair.Value.Profile.DisplayName,
                    Score = pair.Value.Score,
                };
                _finalScoresBuffer.Add(entry);

                if (entry.Score > bestScore)
                {
                    bestScore = entry.Score;
                    _winnerIdsBuffer.Clear();
                    _winnerIdsBuffer.Add(pair.Key);
                }
                else if (entry.Score == bestScore)
                {
                    _winnerIdsBuffer.Add(pair.Key);
                }
            }

            _transport.SendToClient(new MatchEndedMessage
            {
                SentTime = _currentTransportTime,
                FinalScores = new List<ScoreEntry>(_finalScoresBuffer),
                WinnerIds = new List<PlayerId>(_winnerIdsBuffer),
            }, _currentTransportTime);
            GameTrace.Log("Match", "Match ended. Winners: " + string.Join(", ", _winnerIdsBuffer.ConvertAll(id => id.ToString()).ToArray()) + ".");
        }

        private BotBrain CreateBotBrain()
        {
            return new BotBrain
            {
                ThinkDelay = RandomRange(_config.BotDecisionMinDelay, _config.BotDecisionMaxDelay),
                RetargetTimer = 0f,
            };
        }

        private float RandomSnapshotInterval()
        {
            return NetworkPresetProfiles.SampleSnapshotInterval(_transport.CurrentPreset, _config.SnapshotMinInterval, _config.SnapshotMaxInterval, _random);
        }

        private float RandomRange(float min, float max)
        {
            return min + (float)_random.NextDouble() * (max - min);
        }

        private List<PlayerProfile> CloneProfiles()
        {
            List<PlayerProfile> players = new List<PlayerProfile>(_profiles.Count);
            for (int i = 0; i < _profiles.Count; i++)
            {
                PlayerProfile profile = _profiles[i];
                players.Add(new PlayerProfile
                {
                    Id = profile.Id,
                    DisplayName = profile.DisplayName,
                    Kind = profile.Kind,
                    Color = profile.Color,
                });
            }

            return players;
        }

        private static EggSnapshot CloneEgg(ServerEggState egg)
        {
            return new EggSnapshot
            {
                Id = egg.Id,
                Position = egg.Position,
                PaletteIndex = egg.PaletteIndex,
            };
        }
    }
}
