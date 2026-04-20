using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EggTest.Server;
using EggTest.Shared;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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

        [Test]
        public void CreateDefault_BuildsClearanceBlockedCellsBeyondBaseBlockedCells()
        {
            GameConfig config = new GameConfig();
            ArenaDefinition arena = ArenaDefinition.CreateDefault(config);

            Assert.Greater(arena.ClearanceBlockedCells.Count, arena.BlockedCells.Count);

            foreach (GridCell blocked in arena.BlockedCells)
            {
                Assert.IsTrue(arena.ClearanceBlockedCells.Contains(blocked));
            }
        }

        [Test]
        public void CreateDefault_BuildsBotSafeSpawnAndEggCaches_WhenClearanceIsEnabled()
        {
            GameConfig config = new GameConfig
            {
                PlayerCount = 4,
                BotClearanceInflationRadiusCells = 1,
                BotUseCornerSafetyInflation = true,
            };

            ArenaDefinition arena = ArenaDefinition.CreateDefault(config);

            Assert.GreaterOrEqual(arena.BotSafeSpawnCells.Count, config.PlayerCount, "Bot-safe spawns should cover all runtime players.");
            Assert.Greater(arena.BotSafeEggCells.Count, 0, "Bot-safe egg cells should exist for server spawning.");

            for (int i = 0; i < arena.BotSafeSpawnCells.Count; i++)
            {
                GridCell spawnCell = arena.BotSafeSpawnCells[i];
                Assert.IsTrue(arena.IsClearForBot(spawnCell), "Bot-safe spawn must be clear for bots.");
                Assert.IsTrue(arena.PrimaryBotSafeRegionCells.Contains(spawnCell), "Bot-safe spawn should belong to the primary bot-safe region.");
            }

            for (int i = 0; i < arena.BotSafeEggCells.Count; i++)
            {
                GridCell eggCell = arena.BotSafeEggCells[i];
                Assert.IsTrue(arena.IsClearForBot(eggCell), "Bot-safe egg must be clear for bots.");
                Assert.IsTrue(arena.PrimaryBotSafeRegionCells.Contains(eggCell), "Bot-safe egg should belong to the primary bot-safe region.");

                for (int j = 0; j < arena.BotSafeSpawnCells.Count; j++)
                {
                    GridCell spawn = arena.BotSafeSpawnCells[j];
                    int manhattanDistance = Mathf.Abs(spawn.X - eggCell.X) + Mathf.Abs(spawn.Y - eggCell.Y);
                    Assert.GreaterOrEqual(manhattanDistance, 3, "Bot-safe egg cell should stay away from bot-safe spawns.");
                }
            }
        }

        [Test]
        public void CreateDefault_BotSafeEggsRemainReachableFromPrimaryBotSpawn()
        {
            GameConfig config = new GameConfig
            {
                PlayerCount = 4,
                BotClearanceInflationRadiusCells = 1,
                BotUseCornerSafetyInflation = true,
            };

            ArenaDefinition arena = ArenaDefinition.CreateDefault(config);
            GridPathfinder safePathfinder = new GridPathfinder(arena, useBotClearance: true);
            List<GridCell> path = new List<GridCell>();
            GridCell start = arena.BotSafeSpawnCells[0];

            for (int i = 0; i < arena.BotSafeEggCells.Count; i++)
            {
                bool found = safePathfinder.TryFindPath(start, arena.BotSafeEggCells[i], path);
                Assert.IsTrue(found, "Primary bot-safe spawn should be able to reach every bot-safe egg.");
            }
        }

        [Test]
        public void CreateDefault_MaxSupportedPlayerCount_UsesPrimaryBotSafeCapacity()
        {
            GameConfig config = new GameConfig
            {
                PlayerCount = 4,
            };

            ArenaDefinition arena = ArenaDefinition.CreateDefault(config);

            Assert.GreaterOrEqual(arena.MaxSupportedPlayerCount, config.PlayerCount);
            Assert.AreEqual(arena.PrimaryBotSafeRegionCells.Count, arena.MaxSupportedPlayerCount);
        }
    }

    public sealed class GameTraceTests
    {
        [Test]
        public void ResetThrottleState_AllowsLogEveryToEmitAgainForSameKey()
        {
            GameTrace.Configure(true, false);
            GameTrace.ResetThrottleState();

            LogAssert.Expect(LogType.Log, "[EggTest][Test] First");
            GameTrace.LogEvery("Test", "ThrottleKey", 999f, "First");

            GameTrace.ResetThrottleState();

            LogAssert.Expect(LogType.Log, "[EggTest][Test] Second");
            GameTrace.LogEvery("Test", "ThrottleKey", 999f, "Second");

            GameTrace.Configure(false, false);
            GameTrace.ResetThrottleState();
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

        [Test]
        public void TryFindPath_BotSafeMode_RejectsTightCorridor()
        {
            ArenaDefinition arena = new ArenaDefinition(
                5,
                5,
                1f,
                new[]
                {
                    new GridCell(1, 1),
                    new GridCell(1, 2),
                    new GridCell(3, 2),
                    new GridCell(3, 3),
                },
                new[] { new GridCell(0, 0) });

            GridPathfinder safePathfinder = new GridPathfinder(arena, useBotClearance: true);
            List<GridCell> path = new List<GridCell>();

            bool found = safePathfinder.TryFindPath(new GridCell(0, 2), new GridCell(4, 2), path);

            Assert.IsFalse(found);
        }
    }

    public sealed class ServerSoftAvoidanceTests
    {
        [Test]
        public void BlendPathWithSeparation_PreservesForwardProgress_WhileAllowingSideBias()
        {
            MethodInfo method = typeof(ServerSimulator).GetMethod("BlendPathWithSeparation", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);

            Vector2 pathDirection = Vector2.right;
            Vector2 separation = Vector2.up * 0.75f;
            Vector2 combined = (Vector2)method.Invoke(null, new object[] { pathDirection, separation });

            Assert.Greater(combined.x, 0f, "Combined steering should still preserve path progress.");
            Assert.Greater(combined.y, 0f, "Combined steering should allow side bias away from nearby bots.");
        }

        [Test]
        public void ComputeTargetClaimPenalty_IsHigherWhenAnotherBotIsCloserToTheEgg()
        {
            MethodInfo method = typeof(ServerSimulator).GetMethod("ComputeTargetClaimPenalty", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);

            GameConfig config = new GameConfig();
            float farPenalty = (float)method.Invoke(null, new object[] { config, 5f });
            float nearPenalty = (float)method.Invoke(null, new object[] { config, 0.5f });

            Assert.Greater(nearPenalty, farPenalty);
            Assert.GreaterOrEqual(farPenalty, config.BotTargetClaimPenalty);
        }
    }
}
