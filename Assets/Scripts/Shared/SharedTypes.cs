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
        private readonly List<GridCell> _spawnCells;
        private readonly List<GridCell> _candidateEggCells;
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

        public IReadOnlyList<GridCell> CandidateEggCells
        {
            get { return _candidateEggCells; }
        }

        public IReadOnlyList<GridCell> WalkableCells
        {
            get { return _walkableCells; }
        }

        public ArenaDefinition(int width, int height, float cellSize, IEnumerable<GridCell> blockedCells, IEnumerable<GridCell> spawnCells)
        {
            Width = width;
            Height = height;
            CellSize = cellSize;
            _blockedCells = new HashSet<GridCell>(blockedCells);
            _spawnCells = new List<GridCell>(spawnCells);
            _walkableCells = BuildWalkableCells();
            _candidateEggCells = BuildCandidateEggCells();
        }

        public bool IsInside(GridCell cell)
        {
            return cell.X >= 0 && cell.X < Width && cell.Y >= 0 && cell.Y < Height;
        }

        public bool IsWalkable(GridCell cell)
        {
            return IsInside(cell) && !_blockedCells.Contains(cell);
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
            for (int y = 1; y <= 4; y++)
            {
                blocked.Add(new GridCell(4, y));
                blocked.Add(new GridCell(15, y + 5));
            }

            for (int y = 7; y <= 10; y++)
            {
                blocked.Add(new GridCell(4, y));
                blocked.Add(new GridCell(15, y - 5));
            }

            List<GridCell> spawns = BuildSpawnCells(config, blocked);

            return new ArenaDefinition(config.GridWidth, config.GridHeight, config.CellSize, blocked, spawns);
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
            List<GridCell> result = new List<GridCell>();

            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    GridCell cell = new GridCell(x, y);
                    if (!IsWalkable(cell))
                    {
                        continue;
                    }

                    bool tooCloseToSpawn = false;
                    for (int i = 0; i < _spawnCells.Count; i++)
                    {
                        GridCell spawn = _spawnCells[i];
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
    }
}
