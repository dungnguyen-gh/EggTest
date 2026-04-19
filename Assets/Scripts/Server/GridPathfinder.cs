using System.Collections.Generic;
using EggTest.Shared;

namespace EggTest.Server
{
    public interface IPathfinder
    {
        bool TryFindPath(GridCell start, GridCell goal, List<GridCell> outputPath);
    }

    /// <summary>
    /// Custom A* pathfinder over the authored grid.
    /// The map is small, so a straightforward implementation is clearer for reviewers than an over-optimized one.
    /// </summary>
    public sealed class GridPathfinder : IPathfinder
    {
        private struct OpenNode
        {
            public GridCell Cell;
            public int Priority;
        }

        private readonly ArenaDefinition _arena;
        private readonly List<GridCell> _neighbors = new List<GridCell>(4);
        private readonly Dictionary<GridCell, GridCell> _cameFrom = new Dictionary<GridCell, GridCell>();
        private readonly Dictionary<GridCell, int> _costSoFar = new Dictionary<GridCell, int>();
        private readonly HashSet<GridCell> _closed = new HashSet<GridCell>();
        private readonly List<OpenNode> _openHeap = new List<OpenNode>(32);

        public GridPathfinder(ArenaDefinition arena)
        {
            _arena = arena;
        }

        public bool TryFindPath(GridCell start, GridCell goal, List<GridCell> outputPath)
        {
            outputPath.Clear();

            if (!_arena.IsWalkable(start) || !_arena.IsWalkable(goal))
            {
                GameTrace.Verbose("Pathfinding", "Rejected path from " + start + " to " + goal + " because one endpoint is blocked.");
                return false;
            }

            if (start.Equals(goal))
            {
                outputPath.Add(goal);
                return true;
            }

            ResetSearchState();
            _costSoFar[start] = 0;
            PushOpen(start, Heuristic(start, goal));

            while (_openHeap.Count > 0)
            {
                OpenNode currentNode = PopOpen();
                GridCell current = currentNode.Cell;

                if (_closed.Contains(current))
                {
                    continue;
                }

                if (current.Equals(goal))
                {
                    ReconstructPath(_cameFrom, current, outputPath);
                    GameTrace.Verbose("Pathfinding", "Found path from " + start + " to " + goal + " with " + outputPath.Count + " cells.");
                    return true;
                }

                _closed.Add(current);
                FillNeighbors(current);
                for (int i = 0; i < _neighbors.Count; i++)
                {
                    GridCell neighbor = _neighbors[i];
                    if (!_arena.IsWalkable(neighbor) || _closed.Contains(neighbor))
                    {
                        continue;
                    }

                    int newCost = _costSoFar[current] + 1;
                    int oldCost;
                    if (!_costSoFar.TryGetValue(neighbor, out oldCost) || newCost < oldCost)
                    {
                        _costSoFar[neighbor] = newCost;
                        _cameFrom[neighbor] = current;
                        PushOpen(neighbor, newCost + Heuristic(neighbor, goal));
                    }
                }
            }

            GameTrace.Verbose("Pathfinding", "No path found from " + start + " to " + goal + ".");
            return false;
        }

        private void FillNeighbors(GridCell cell)
        {
            _neighbors.Clear();
            _neighbors.Add(new GridCell(cell.X + 1, cell.Y));
            _neighbors.Add(new GridCell(cell.X - 1, cell.Y));
            _neighbors.Add(new GridCell(cell.X, cell.Y + 1));
            _neighbors.Add(new GridCell(cell.X, cell.Y - 1));
        }

        private static int Heuristic(GridCell from, GridCell to)
        {
            return System.Math.Abs(from.X - to.X) + System.Math.Abs(from.Y - to.Y);
        }

        private static void ReconstructPath(Dictionary<GridCell, GridCell> cameFrom, GridCell current, List<GridCell> outputPath)
        {
            outputPath.Add(current);
            while (cameFrom.ContainsKey(current))
            {
                current = cameFrom[current];
                outputPath.Add(current);
            }

            outputPath.Reverse();
        }

        private void ResetSearchState()
        {
            _cameFrom.Clear();
            _costSoFar.Clear();
            _closed.Clear();
            _openHeap.Clear();
        }

        private void PushOpen(GridCell cell, int priority)
        {
            OpenNode node = new OpenNode
            {
                Cell = cell,
                Priority = priority,
            };

            _openHeap.Add(node);
            int index = _openHeap.Count - 1;
            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (_openHeap[parent].Priority <= _openHeap[index].Priority)
                {
                    break;
                }

                Swap(index, parent);
                index = parent;
            }
        }

        private OpenNode PopOpen()
        {
            OpenNode root = _openHeap[0];
            int lastIndex = _openHeap.Count - 1;
            _openHeap[0] = _openHeap[lastIndex];
            _openHeap.RemoveAt(lastIndex);

            int index = 0;
            while (index < _openHeap.Count)
            {
                int left = (index * 2) + 1;
                int right = left + 1;
                if (left >= _openHeap.Count)
                {
                    break;
                }

                int best = left;
                if (right < _openHeap.Count && _openHeap[right].Priority < _openHeap[left].Priority)
                {
                    best = right;
                }

                if (_openHeap[index].Priority <= _openHeap[best].Priority)
                {
                    break;
                }

                Swap(index, best);
                index = best;
            }

            return root;
        }

        private void Swap(int left, int right)
        {
            OpenNode temp = _openHeap[left];
            _openHeap[left] = _openHeap[right];
            _openHeap[right] = temp;
        }
    }
}
