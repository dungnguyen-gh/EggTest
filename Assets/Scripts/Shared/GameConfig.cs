using UnityEngine;

namespace EggTest.Shared
{
    /// <summary>
    /// Centralized gameplay and networking knobs.
    /// Keeping these values in one place makes the recruitment-test prototype easy to review and easy to retune.
    /// </summary>
    public sealed class GameConfig
    {
        public int PlayerCount = 4;
        public float MatchDurationSeconds = 90f;

        public int GridWidth = 20;
        public int GridHeight = 12;
        public float CellSize = 1f;

        public float PlayerMoveSpeed = 4.1f;
        public float PlayerRadius = 0.28f;
        public float EggCollectRadius = 0.55f;
        public float InputSendInterval = 0.05f;

        public float ServerSimulationStep = 0.05f; // 20 Hz simulation.
        public float SnapshotMinInterval = 0.10f;
        public float SnapshotMaxInterval = 0.50f;

        public int TargetActiveEggCount = 3;
        public float EggRespawnMinDelay = 0.50f;
        public float EggRespawnMaxDelay = 1.50f;

        public float RemoteInterpolationBackTime = 0.30f;
        public float RemoteExtrapolationLimit = 0.20f;
        public float RemoteInterpolationSafetyMargin = 0.06f;

        public float LocalCorrectionThreshold = 0.50f;
        public float LocalSoftCorrection = 6f;
        public float LocalHardCorrection = 10f;

        public float BotDecisionMinDelay = 0.15f;
        public float BotDecisionMaxDelay = 0.35f;
        public float BotRetargetInterval = 0.50f;
        public float BotWaypointTolerance = 0.18f;
        public float BotRandomScoreNoise = 0.35f;
        public float BotSeparationRadius = 0.95f;
        public float BotSeparationStrength = 0.70f;
        public float BotTargetClaimPenalty = 1.10f;
        public float BotTargetClaimPenaltyDistance = 3.00f;
        public float BotMinimumPersonalSpace = 0.62f;
        public int BotClearanceInflationRadiusCells = 1;
        public bool BotUseCornerSafetyInflation = true;

        public int RandomSeed = 12345;
        public bool EnableDebugLogs = false;
        public bool EnableVerboseDebugLogs = false;

        public Color[] PlayerPalette =
        {
            new Color(0.25f, 0.80f, 0.35f),
            new Color(0.25f, 0.60f, 0.95f),
            new Color(0.95f, 0.55f, 0.20f),
            new Color(0.80f, 0.30f, 0.90f),
            new Color(0.95f, 0.85f, 0.20f),
            new Color(0.20f, 0.85f, 0.85f),
            new Color(0.95f, 0.40f, 0.45f),
            new Color(0.55f, 0.95f, 0.35f),
        };

        public Color[] EggPalette =
        {
            new Color(1.00f, 0.45f, 0.40f),
            new Color(1.00f, 0.85f, 0.25f),
            new Color(0.35f, 0.95f, 0.45f),
            new Color(0.35f, 0.65f, 1.00f),
            new Color(0.95f, 0.45f, 1.00f),
        };

        public NetworkSimulationPreset DefaultNetworkPreset = NetworkSimulationPreset.Stable;
    }
}
