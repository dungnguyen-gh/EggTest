using System.Collections.Generic;
using EggTest.Server;
using EggTest.Shared;
using NUnit.Framework;
using UnityEngine;

namespace EggTest.Tests.EditMode
{
    public sealed class GameMathTests
    {
        [Test]
        public void Cardinalize_PrefersDominantAxis_AndZeroesTheOther()
        {
            Assert.AreEqual(Vector2.zero, GameMath.Cardinalize(Vector2.zero));
            Assert.AreEqual(Vector2.right, GameMath.Cardinalize(new Vector2(0.9f, 0.2f)));
            Assert.AreEqual(Vector2.left, GameMath.Cardinalize(new Vector2(-0.9f, 0.1f)));
            Assert.AreEqual(Vector2.up, GameMath.Cardinalize(new Vector2(0.2f, 0.9f)));
            Assert.AreEqual(Vector2.down, GameMath.Cardinalize(new Vector2(0.1f, -0.9f)));
        }
    }

    public sealed class ArenaDefinitionTests
    {
        [Test]
        public void CreateDefault_BuildsUniqueWalkableSpawnCells_AndEggCellsStayAwayFromSpawns()
        {
            GameConfig config = new GameConfig
            {
                PlayerCount = 4,
            };

            ArenaDefinition arena = ArenaDefinition.CreateDefault(config);

            Assert.AreEqual(config.PlayerCount, arena.SpawnCells.Count);

            HashSet<GridCell> uniqueSpawns = new HashSet<GridCell>();
            for (int i = 0; i < arena.SpawnCells.Count; i++)
            {
                GridCell spawn = arena.SpawnCells[i];
                Assert.IsTrue(arena.IsWalkable(spawn), "Spawn cell must be walkable.");
                Assert.IsTrue(uniqueSpawns.Add(spawn), "Spawn cells should be unique for this player count.");
            }

            for (int i = 0; i < arena.CandidateEggCells.Count; i++)
            {
                GridCell eggCell = arena.CandidateEggCells[i];
                Assert.IsTrue(arena.IsWalkable(eggCell), "Egg cell must be walkable.");

                for (int j = 0; j < arena.SpawnCells.Count; j++)
                {
                    GridCell spawn = arena.SpawnCells[j];
                    int manhattanDistance = Mathf.Abs(spawn.X - eggCell.X) + Mathf.Abs(spawn.Y - eggCell.Y);
                    Assert.GreaterOrEqual(manhattanDistance, 3, "Egg cell should not be too close to a spawn cell.");
                }
            }
        }
    }

    public sealed class GridPathfinderTests
    {
        [Test]
        public void TryFindPath_FindsDetourAroundBlockers()
        {
            ArenaDefinition arena = new ArenaDefinition(
                5,
                5,
                1f,
                new[]
                {
                    new GridCell(1, 0),
                    new GridCell(1, 1),
                    new GridCell(1, 2),
                },
                new[] { new GridCell(0, 0) });

            GridPathfinder pathfinder = new GridPathfinder(arena);
            List<GridCell> path = new List<GridCell>();

            bool found = pathfinder.TryFindPath(new GridCell(0, 0), new GridCell(2, 0), path);

            Assert.IsTrue(found);
            Assert.AreEqual(new GridCell(0, 0), path[0]);
            Assert.AreEqual(new GridCell(2, 0), path[path.Count - 1]);
            Assert.AreEqual(9, path.Count);

            for (int i = 0; i < path.Count; i++)
            {
                Assert.IsTrue(arena.IsWalkable(path[i]));
            }
        }

        [Test]
        public void TryFindPath_ReturnsFalse_WhenGoalIsUnreachable()
        {
            ArenaDefinition arena = new ArenaDefinition(
                3,
                3,
                1f,
                new[]
                {
                    new GridCell(1, 0),
                    new GridCell(1, 1),
                    new GridCell(1, 2),
                },
                new[] { new GridCell(0, 0) });

            GridPathfinder pathfinder = new GridPathfinder(arena);
            List<GridCell> path = new List<GridCell>();

            bool found = pathfinder.TryFindPath(new GridCell(0, 1), new GridCell(2, 1), path);

            Assert.IsFalse(found);
            Assert.AreEqual(0, path.Count);
        }
    }
}
