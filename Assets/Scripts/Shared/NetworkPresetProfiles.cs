using UnityEngine;

namespace EggTest.Shared
{
    /// <summary>
    /// Centralized preset tuning so transport, client smoothing, and server publish cadence stay in sync.
    /// Stable mode is intentionally the smoothest requirement-compliant option.
    /// </summary>
    public readonly struct NetworkPresetProfile
    {
        public readonly NetworkSimulationPreset Preset;
        public readonly float BaseLatencyMs;
        public readonly float JitterMs;
        public readonly float SpikeAdditionalLatencyMs;
        public readonly float InputSendInterval;
        public readonly float RemoteInterpolationBackTime;
        public readonly float RemoteInterpolationSafetyMargin;
        public readonly float RemoteExtrapolationLimit;
        public readonly float LocalCorrectionThreshold;
        public readonly float LocalSoftCorrection;
        public readonly float LocalHardCorrection;
        public readonly float SnapshotBiasExponent;

        public NetworkPresetProfile(
            NetworkSimulationPreset preset,
            float baseLatencyMs,
            float jitterMs,
            float spikeAdditionalLatencyMs,
            float inputSendInterval,
            float remoteInterpolationBackTime,
            float remoteInterpolationSafetyMargin,
            float remoteExtrapolationLimit,
            float localCorrectionThreshold,
            float localSoftCorrection,
            float localHardCorrection,
            float snapshotBiasExponent)
        {
            Preset = preset;
            BaseLatencyMs = baseLatencyMs;
            JitterMs = jitterMs;
            SpikeAdditionalLatencyMs = spikeAdditionalLatencyMs;
            InputSendInterval = inputSendInterval;
            RemoteInterpolationBackTime = remoteInterpolationBackTime;
            RemoteInterpolationSafetyMargin = remoteInterpolationSafetyMargin;
            RemoteExtrapolationLimit = remoteExtrapolationLimit;
            LocalCorrectionThreshold = localCorrectionThreshold;
            LocalSoftCorrection = localSoftCorrection;
            LocalHardCorrection = localHardCorrection;
            SnapshotBiasExponent = snapshotBiasExponent;
        }
    }

    public static class NetworkPresetProfiles
    {
        public static NetworkPresetProfile Get(NetworkSimulationPreset preset)
        {
            switch (preset)
            {
                case NetworkSimulationPreset.Stable:
                    return new NetworkPresetProfile(
                        preset,
                        baseLatencyMs: 25f,
                        jitterMs: 5f,
                        spikeAdditionalLatencyMs: 250f,
                        inputSendInterval: 0.04f,
                        remoteInterpolationBackTime: 0.18f,
                        remoteInterpolationSafetyMargin: 0.03f,
                        remoteExtrapolationLimit: 0.10f,
                        localCorrectionThreshold: 0.65f,
                        localSoftCorrection: 4.5f,
                        localHardCorrection: 8f,
                        snapshotBiasExponent: 2.6f);
                case NetworkSimulationPreset.Low:
                    return new NetworkPresetProfile(
                        preset,
                        baseLatencyMs: 50f,
                        jitterMs: 10f,
                        spikeAdditionalLatencyMs: 250f,
                        inputSendInterval: 0.05f,
                        remoteInterpolationBackTime: 0.26f,
                        remoteInterpolationSafetyMargin: 0.05f,
                        remoteExtrapolationLimit: 0.16f,
                        localCorrectionThreshold: 0.55f,
                        localSoftCorrection: 5.5f,
                        localHardCorrection: 9f,
                        snapshotBiasExponent: 1.3f);
                case NetworkSimulationPreset.High:
                    return new NetworkPresetProfile(
                        preset,
                        baseLatencyMs: 300f,
                        jitterMs: 100f,
                        spikeAdditionalLatencyMs: 250f,
                        inputSendInterval: 0.06f,
                        remoteInterpolationBackTime: 0.34f,
                        remoteInterpolationSafetyMargin: 0.08f,
                        remoteExtrapolationLimit: 0.22f,
                        localCorrectionThreshold: 0.50f,
                        localSoftCorrection: 6f,
                        localHardCorrection: 10f,
                        snapshotBiasExponent: 1.0f);
                default:
                    return new NetworkPresetProfile(
                        preset,
                        baseLatencyMs: 150f,
                        jitterMs: 40f,
                        spikeAdditionalLatencyMs: 250f,
                        inputSendInterval: 0.055f,
                        remoteInterpolationBackTime: 0.30f,
                        remoteInterpolationSafetyMargin: 0.06f,
                        remoteExtrapolationLimit: 0.20f,
                        localCorrectionThreshold: 0.50f,
                        localSoftCorrection: 6f,
                        localHardCorrection: 10f,
                        snapshotBiasExponent: 1.0f);
            }
        }

        public static float SampleSnapshotInterval(NetworkSimulationPreset preset, float minInterval, float maxInterval, System.Random random)
        {
            NetworkPresetProfile profile = Get(preset);
            float normalized = (float)random.NextDouble();

            if (profile.SnapshotBiasExponent > 1.01f)
            {
                normalized = Mathf.Pow(normalized, profile.SnapshotBiasExponent);
            }

            return Mathf.Lerp(minInterval, maxInterval, normalized);
        }
    }
}
