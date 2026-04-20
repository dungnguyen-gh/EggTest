using System;
using System.Collections.Generic;
using UnityEngine;

namespace EggTest.Shared
{
    public enum PlayerKind
    {
        LocalHuman,
        RemoteBot,
    }

    public enum NetworkSimulationPreset
    {
        Stable,
        Low,
        Medium,
        High,
    }

    [Serializable]
    public struct PlayerId : IEquatable<PlayerId>
    {
        public int Value;

        public PlayerId(int value)
        {
            Value = value;
        }

        public bool Equals(PlayerId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is PlayerId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return "P" + Value;
        }
    }

    [Serializable]
    public struct EggId : IEquatable<EggId>
    {
        public int Value;

        public EggId(int value)
        {
            Value = value;
        }

        public bool Equals(EggId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is EggId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return "E" + Value;
        }
    }

    [Serializable]
    public struct GridCell : IEquatable<GridCell>
    {
        public int X;
        public int Y;

        public GridCell(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(GridCell other)
        {
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj)
        {
            return obj is GridCell other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (X * 397) ^ Y;
            }
        }

        public override string ToString()
        {
            return "(" + X + ", " + Y + ")";
        }
    }

    [Serializable]
    public sealed class PlayerProfile
    {
        public PlayerId Id;
        public string DisplayName;
        public PlayerKind Kind;
        public Color Color;
    }

    [Serializable]
    public sealed class PlayerSnapshot
    {
        public PlayerId Id;
        public Vector3 Position;
        public Vector2 MoveDirection;
        public int Score;
        public int LastInputSequence;
    }

    [Serializable]
    public sealed class EggSnapshot
    {
        public EggId Id;
        public Vector3 Position;
        public int PaletteIndex;
    }

    [Serializable]
    public sealed class ScoreEntry
    {
        public PlayerId PlayerId;
        public string DisplayName;
        public int Score;
    }

    /// <summary>
    /// Simple logical arena definition shared by server logic and client rendering.
    /// Using a grid keeps pathfinding custom and deterministic.
    /// </summary>
    public sealed class ArenaDefinition
    {
        private readonly HashSet<GridCell> _blockedCells;
        private readonly HashSet<GridCell> _clearanceBlockedCells;
        private readonly HashSet<GridCell> _primaryBotSafeRegionCells;
        private readonly List<GridCell> _spawnCells;
        private readonly List<GridCell> _botSafeSpawnCells;
        private readonly List<GridCell> _candidateEggCells;
        private readonly List<GridCell> _botSafeEggCells;
        private readonly List<GridCell> _walkableCells;

        public int Width { get; private set; }
        public int Height { get; private set; }
        public float CellSize { get; private set; }

        public IReadOnlyCollection<GridCell> BlockedCells
        {
            get { return _blockedCells; }
        }

        public IReadOnlyList<GridCell> SpawnCells
        {
            get { return _spawnCells; }
        }

        public IReadOnlyList<GridCell> BotSafeSpawnCells
        {
            get { return _botSafeSpawnCells; }
        }

        public IReadOnlyCollection<GridCell> ClearanceBlockedCells
        {
            get { return _clearanceBlockedCells; }
        }

        public IReadOnlyCollection<GridCell> PrimaryBotSafeRegionCells
        {
            get { return _primaryBotSafeRegionCells; }
        }

        public IReadOnlyList<GridCell> CandidateEggCells
        {
            get { return _candidateEggCells; }
        }

        public IReadOnlyList<GridCell> BotSafeEggCells
        {
            get { return _botSafeEggCells; }
        }

        public IReadOnlyList<GridCell> WalkableCells
        {
            get { return _walkableCells; }
        }

        public int MaxSupportedPlayerCount
        {
            get
            {
                int preferredCapacity = _primaryBotSafeRegionCells.Count > 0 ? _primaryBotSafeRegionCells.Count : _walkableCells.Count;
                return Mathf.Max(2, preferredCapacity);
            }
        }

        public ArenaDefinition(int width, int height, float cellSize, IEnumerable<GridCell> blockedCells, IEnumerable<GridCell> spawnCells)
        {
            Width = width;
            Height = height;
            CellSize = cellSize;
            _blockedCells = new HashSet<GridCell>(blockedCells);
            _spawnCells = new List<GridCell>(spawnCells);
            _botSafeSpawnCells = new List<GridCell>();
            _primaryBotSafeRegionCells = new HashSet<GridCell>();
            _clearanceBlockedCells = BuildClearanceBlockedCells(width, height, _blockedCells, configRadiusCells: 1);
            _walkableCells = BuildWalkableCells();
            _candidateEggCells = BuildCandidateEggCells();
            _botSafeEggCells = new List<GridCell>();
        }

        public bool IsInside(GridCell cell)
        {
            return cell.X >= 0 && cell.X < Width && cell.Y >= 0 && cell.Y < Height;
        }

        public bool IsWalkable(GridCell cell)
        {
            return IsInside(cell) && !_blockedCells.Contains(cell);
        }

        public bool IsClearForBot(GridCell cell)
        {
            return IsInside(cell) && !_clearanceBlockedCells.Contains(cell);
        }

        public Vector3 CellToWorld(GridCell cell)
        {
            float x = (cell.X - (Width * 0.5f) + 0.5f) * CellSize;
            float z = (cell.Y - (Height * 0.5f) + 0.5f) * CellSize;
            return new Vector3(x, 0f, z);
        }

        public GridCell WorldToCell(Vector3 world)
        {
            float normalizedX = (world.x / CellSize) + (Width * 0.5f);
            float normalizedY = (world.z / CellSize) + (Height * 0.5f);
            return new GridCell(Mathf.FloorToInt(normalizedX), Mathf.FloorToInt(normalizedY));
        }

        public GridCell GetSpawnCell(int index)
        {
            if (_spawnCells.Count == 0)
            {
                return new GridCell(0, 0);
            }

            return _spawnCells[index % _spawnCells.Count];
        }

        public GridCell GetBotSafeSpawnCell(int index)
        {
            if (_botSafeSpawnCells.Count == 0)
            {
                return GetSpawnCell(index);
            }

            return _botSafeSpawnCells[index % _botSafeSpawnCells.Count];
        }

        public bool TryFindNearestBotSafeCell(GridCell origin, out GridCell resolved)
        {
            if (IsClearForBot(origin))
            {
                resolved = origin;
                return true;
            }

            int maxRadius = Mathf.Max(Width, Height);
            for (int radius = 1; radius <= maxRadius; radius++)
            {
                for (int offsetX = -radius; offsetX <= radius; offsetX++)
                {
                    for (int offsetY = -radius; offsetY <= radius; offsetY++)
                    {
                        if (Mathf.Max(Mathf.Abs(offsetX), Mathf.Abs(offsetY)) != radius)
                        {
                            continue;
                        }

                        GridCell candidate = new GridCell(origin.X + offsetX, origin.Y + offsetY);
                        if (IsClearForBot(candidate))
                        {
                            resolved = candidate;
                            return true;
                        }
                    }
                }
            }

            resolved = origin;
            return false;
        }

        public static ArenaDefinition CreateDefault(GameConfig config)
        {
            List<GridCell> blocked = new List<GridCell>();

            // A plus-shaped central blocker.
            for (int x = 8; x <= 11; x++)
            {
                blocked.Add(new GridCell(x, 5));
                blocked.Add(new GridCell(x, 6));
            }

            for (int y = 3; y <= 8; y++)
            {
                blocked.Add(new GridCell(9, y));
                blocked.Add(new GridCell(10, y));
            }

            // Side walls that create interesting path splits for the bots.
            for (int y = 1; y <= 3; y++)
            {
                blocked.Add(new GridCell(4, y));
                blocked.Add(new GridCell(15, y + 5));
            }

            for (int y = 8; y <= 10; y++)
            {
                blocked.Add(new GridCell(4, y));
                blocked.Add(new GridCell(15, y - 5));
            }

            List<GridCell> spawns = BuildSpawnCells(config, blocked);

            ArenaDefinition arena = new ArenaDefinition(config.GridWidth, config.GridHeight, config.CellSize, blocked, spawns);
            arena.RebuildClearanceBlockedCells(config);
            return arena;
        }

        private void RebuildClearanceBlockedCells(GameConfig config)
        {
            _clearanceBlockedCells.Clear();

            HashSet<GridCell> rebuilt = BuildClearanceBlockedCells(
                Width,
                Height,
                _blockedCells,
                Mathf.Max(0, config.BotClearanceInflationRadiusCells));

            foreach (GridCell cell in rebuilt)
            {
                _clearanceBlockedCells.Add(cell);
            }

            if (config.BotUseCornerSafetyInflation)
            {
                AddCornerSafetyInflation(_clearanceBlockedCells);
            }

            RebuildBotSafeCaches(config);
        }

        private static List<GridCell> BuildSpawnCells(GameConfig config, List<GridCell> blocked)
        {
            HashSet<GridCell> blockedSet = new HashSet<GridCell>(blocked);
            List<GridCell> candidates = new List<GridCell>();

            for (int x = 0; x < config.GridWidth; x++)
            {
                for (int y = 0; y < config.GridHeight; y++)
                {
                    GridCell cell = new GridCell(x, y);
                    if (blockedSet.Contains(cell))
                    {
                        continue;
                    }

                    candidates.Add(cell);
                }
            }

            List<GridCell> result = new List<GridCell>();
            int desiredCount = Mathf.Clamp(config.PlayerCount, 2, candidates.Count);

            while (result.Count < desiredCount && candidates.Count > 0)
            {
                int bestIndex = 0;
                float bestScore = float.MinValue;

                for (int i = 0; i < candidates.Count; i++)
                {
                    GridCell candidate = candidates[i];
                    float nearestDistance = float.MaxValue;

                    if (result.Count == 0)
                    {
                        nearestDistance = DistanceFromCenter(config, candidate);
                    }
                    else
                    {
                        for (int j = 0; j < result.Count; j++)
                        {
                            GridCell chosen = result[j];
                            float distance = Mathf.Abs(candidate.X - chosen.X) + Mathf.Abs(candidate.Y - chosen.Y);
                            if (distance < nearestDistance)
                            {
                                nearestDistance = distance;
                            }
                        }
                    }

                    float edgeBonus = DistanceFromCenter(config, candidate);
                    float score = nearestDistance + edgeBonus * 0.35f;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestIndex = i;
                    }
                }

                result.Add(candidates[bestIndex]);
                candidates.RemoveAt(bestIndex);
            }

            return result;
        }

        private static float DistanceFromCenter(GameConfig config, GridCell cell)
        {
            float centerX = (config.GridWidth - 1) * 0.5f;
            float centerY = (config.GridHeight - 1) * 0.5f;
            return Mathf.Abs(cell.X - centerX) + Mathf.Abs(cell.Y - centerY);
        }

        private void RebuildBotSafeCaches(GameConfig config)
        {
            _botSafeSpawnCells.Clear();
            _botSafeEggCells.Clear();
            _primaryBotSafeRegionCells.Clear();

            List<GridCell> botSafeCandidates = new List<GridCell>();
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    GridCell cell = new GridCell(x, y);
                    if (IsClearForBot(cell))
                    {
                        botSafeCandidates.Add(cell);
                    }
                }
            }

            List<GridCell> primaryRegion = BuildLargestConnectedRegion(botSafeCandidates);
            for (int i = 0; i < primaryRegion.Count; i++)
            {
                _primaryBotSafeRegionCells.Add(primaryRegion[i]);
            }

            _botSafeSpawnCells.AddRange(SelectDistributedCells(config, primaryRegion, Mathf.Clamp(config.PlayerCount, 0, primaryRegion.Count)));
            _botSafeEggCells.AddRange(BuildEggCandidateCells(_botSafeSpawnCells, useBotClearance: true, allowedCells: _primaryBotSafeRegionCells));
        }

        private List<GridCell> BuildWalkableCells()
        {
            List<GridCell> result = new List<GridCell>();
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    GridCell cell = new GridCell(x, y);
                    if (IsWalkable(cell))
                    {
                        result.Add(cell);
                    }
                }
            }

            return result;
        }

        private List<GridCell> BuildCandidateEggCells()
        {
            return BuildEggCandidateCells(_spawnCells, useBotClearance: false, allowedCells: null);
        }

        private List<GridCell> BuildEggCandidateCells(IReadOnlyList<GridCell> referenceSpawns, bool useBotClearance, HashSet<GridCell> allowedCells)
        {
            List<GridCell> result = new List<GridCell>();

            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    GridCell cell = new GridCell(x, y);
                    bool validCell = useBotClearance ? IsClearForBot(cell) : IsWalkable(cell);
                    if (!validCell)
                    {
                        continue;
                    }

                    if (allowedCells != null && !allowedCells.Contains(cell))
                    {
                        continue;
                    }

                    bool tooCloseToSpawn = false;
                    for (int i = 0; i < referenceSpawns.Count; i++)
                    {
                        GridCell spawn = referenceSpawns[i];
                        int manhattanDistance = Mathf.Abs(spawn.X - cell.X) + Mathf.Abs(spawn.Y - cell.Y);
                        if (manhattanDistance < 3)
                        {
                            tooCloseToSpawn = true;
                            break;
                        }
                    }

                    if (!tooCloseToSpawn)
                    {
                        result.Add(cell);
                    }
                }
            }

            return result;
        }

        private List<GridCell> BuildLargestConnectedRegion(List<GridCell> candidates)
        {
            List<GridCell> largest = new List<GridCell>();
            if (candidates.Count == 0)
            {
                return largest;
            }

            HashSet<GridCell> candidateSet = new HashSet<GridCell>(candidates);
            HashSet<GridCell> visited = new HashSet<GridCell>();
            Queue<GridCell> queue = new Queue<GridCell>();
            List<GridCell> componentBuffer = new List<GridCell>();

            for (int i = 0; i < candidates.Count; i++)
            {
                GridCell start = candidates[i];
                if (!visited.Add(start))
                {
                    continue;
                }

                componentBuffer.Clear();
                queue.Enqueue(start);

                while (queue.Count > 0)
                {
                    GridCell current = queue.Dequeue();
                    componentBuffer.Add(current);

                    EnqueueNeighbor(current.X + 1, current.Y, candidateSet, visited, queue);
                    EnqueueNeighbor(current.X - 1, current.Y, candidateSet, visited, queue);
                    EnqueueNeighbor(current.X, current.Y + 1, candidateSet, visited, queue);
                    EnqueueNeighbor(current.X, current.Y - 1, candidateSet, visited, queue);
                }

                if (componentBuffer.Count > largest.Count)
                {
                    largest.Clear();
                    largest.AddRange(componentBuffer);
                }
            }

            return largest;
        }

        private static void EnqueueNeighbor(int x, int y, HashSet<GridCell> candidateSet, HashSet<GridCell> visited, Queue<GridCell> queue)
        {
            GridCell neighbor = new GridCell(x, y);
            if (!candidateSet.Contains(neighbor) || !visited.Add(neighbor))
            {
                return;
            }

            queue.Enqueue(neighbor);
        }

        private static List<GridCell> SelectDistributedCells(GameConfig config, List<GridCell> candidates, int desiredCount)
        {
            List<GridCell> available = new List<GridCell>(candidates);
            List<GridCell> result = new List<GridCell>();

            while (result.Count < desiredCount && available.Count > 0)
            {
                int bestIndex = 0;
                float bestScore = float.MinValue;

                for (int i = 0; i < available.Count; i++)
                {
                    GridCell candidate = available[i];
                    float nearestDistance = float.MaxValue;

                    if (result.Count == 0)
                    {
                        nearestDistance = DistanceFromCenter(config, candidate);
                    }
                    else
                    {
                        for (int j = 0; j < result.Count; j++)
                        {
                            GridCell chosen = result[j];
                            float distance = Mathf.Abs(candidate.X - chosen.X) + Mathf.Abs(candidate.Y - chosen.Y);
                            if (distance < nearestDistance)
                            {
                                nearestDistance = distance;
                            }
                        }
                    }

                    float edgeBonus = DistanceFromCenter(config, candidate);
                    float score = nearestDistance + edgeBonus * 0.35f;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestIndex = i;
                    }
                }

                result.Add(available[bestIndex]);
                available.RemoveAt(bestIndex);
            }

            return result;
        }

        private static HashSet<GridCell> BuildClearanceBlockedCells(int width, int height, HashSet<GridCell> blockedCells, int configRadiusCells)
        {
            HashSet<GridCell> result = new HashSet<GridCell>(blockedCells);
            if (configRadiusCells <= 0)
            {
                return result;
            }

            foreach (GridCell blockedCell in blockedCells)
            {
                for (int offsetX = -configRadiusCells; offsetX <= configRadiusCells; offsetX++)
                {
                    for (int offsetY = -configRadiusCells; offsetY <= configRadiusCells; offsetY++)
                    {
                        GridCell candidate = new GridCell(blockedCell.X + offsetX, blockedCell.Y + offsetY);
                        if (candidate.X < 0 || candidate.X >= width || candidate.Y < 0 || candidate.Y >= height)
                        {
                            continue;
                        }

                        result.Add(candidate);
                    }
                }
            }

            return result;
        }

        private void AddCornerSafetyInflation(HashSet<GridCell> clearanceBlocked)
        {
            for (int x = 0; x < Width - 1; x++)
            {
                for (int y = 0; y < Height - 1; y++)
                {
                    GridCell bottomLeft = new GridCell(x, y);
                    GridCell bottomRight = new GridCell(x + 1, y);
                    GridCell topLeft = new GridCell(x, y + 1);
                    GridCell topRight = new GridCell(x + 1, y + 1);

                    bool diagonalA = _blockedCells.Contains(bottomLeft) && _blockedCells.Contains(topRight);
                    bool diagonalB = _blockedCells.Contains(bottomRight) && _blockedCells.Contains(topLeft);

                    if (diagonalA || diagonalB)
                    {
                        clearanceBlocked.Add(bottomLeft);
                        clearanceBlocked.Add(bottomRight);
                        clearanceBlocked.Add(topLeft);
                        clearanceBlocked.Add(topRight);
                    }
                }
            }
        }
    }
}
