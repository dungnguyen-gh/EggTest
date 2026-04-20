using UnityEngine;

namespace EggTest.Client
{
    /// <summary>
    /// Explicit scene object contract consumed by runtime composition and editor authoring paths.
    /// </summary>
    public sealed class SceneContract
    {
        public Transform WorldRoot { get; private set; }
        public Transform ArenaRoot { get; private set; }
        public Transform ObstaclesRoot { get; private set; }
        public Transform PlayersRoot { get; private set; }
        public Transform EggsRoot { get; private set; }
        public HudPresenter Hud { get; private set; }

        public SceneContract(
            Transform worldRoot,
            Transform arenaRoot,
            Transform obstaclesRoot,
            Transform playersRoot,
            Transform eggsRoot,
            HudPresenter hud)
        {
            WorldRoot = worldRoot;
            ArenaRoot = arenaRoot;
            ObstaclesRoot = obstaclesRoot;
            PlayersRoot = playersRoot;
            EggsRoot = eggsRoot;
            Hud = hud;
        }
    }
}
