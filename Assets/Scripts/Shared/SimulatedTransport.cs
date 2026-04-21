using System;
using System.Collections.Generic;
using UnityEngine;

namespace EggTest.Shared
{
    public interface INetworkTransport
    {
        NetworkSimulationPreset CurrentPreset { get; }
        NetworkSimulationSettings Settings { get; }
        void SendToServer(IMessage message, double now);
        void SendToClient(IMessage message, double now);
        void Pump(double now, Action<IMessage> onServerMessage, Action<IMessage> onClientMessage);
        void ApplyPreset(NetworkSimulationPreset preset);
        void SetSpikeEnabled(bool enabled);
        void Clear();
    }

    [Serializable]
    public sealed class NetworkSimulationSettings
    {
        public float BaseLatencyMs;
        public float JitterMs;
        public bool SpikeEnabled;
        public float SpikeAdditionalLatencyMs;

        public NetworkSimulationSettings Clone()
        {
            return new NetworkSimulationSettings
            {
                BaseLatencyMs = BaseLatencyMs,
                JitterMs = JitterMs,
                SpikeEnabled = SpikeEnabled,
                SpikeAdditionalLatencyMs = SpikeAdditionalLatencyMs,
            };
        }
    }

    /// <summary>
    /// Local fake transport. It is intentionally queue-based so gameplay code never talks directly across layers.
    /// </summary>
    public sealed class SimulatedTransport : INetworkTransport
    {
        private sealed class ScheduledMessage
        {
            public IMessage Message;
            public double DeliveryTime;
            public bool DeliverToServer;
            public int SortOrder;
        }

        private readonly List<ScheduledMessage> _scheduled = new List<ScheduledMessage>();
        private readonly System.Random _random;
        private int _sortCounter;

        public NetworkSimulationPreset CurrentPreset { get; private set; }
        public NetworkSimulationSettings Settings { get; private set; }

        public SimulatedTransport(int seed, NetworkSimulationPreset preset)
        {
            _random = new System.Random(seed);
            CurrentPreset = preset;
            Settings = BuildPreset(preset);
            GameTrace.Verbose("Transport", "Initialized simulated transport with preset " + preset + ".");
        }

        public void SendToServer(IMessage message, double now)
        {
            Schedule(message, now, true);
        }

        public void SendToClient(IMessage message, double now)
        {
            Schedule(message, now, false);
        }

        public void Pump(double now, Action<IMessage> onServerMessage, Action<IMessage> onClientMessage)
        {
            while (_scheduled.Count > 0)
            {
                ScheduledMessage scheduled = _scheduled[0];
                if (scheduled.DeliveryTime > now)
                {
                    break;
                }

                _scheduled.RemoveAt(0);
                if (scheduled.DeliverToServer)
                {
                    onServerMessage(scheduled.Message);
                }
                else
                {
                    onClientMessage(scheduled.Message);
                }
            }
        }

        public void ApplyPreset(NetworkSimulationPreset preset)
        {
            CurrentPreset = preset;
            NetworkSimulationSettings next = BuildPreset(preset);
            next.SpikeEnabled = Settings.SpikeEnabled;
            Settings = next;
            GameTrace.Verbose("Transport", "Applied network preset " + preset + " (latency=" + Settings.BaseLatencyMs + "ms, jitter=" + Settings.JitterMs + "ms).");
        }

        public void SetSpikeEnabled(bool enabled)
        {
            Settings.SpikeEnabled = enabled;
            GameTrace.Verbose("Transport", "Latency spike simulation set to " + enabled + ".");
        }

        public void Clear()
        {
            _scheduled.Clear();
            GameTrace.Verbose("Transport", "Cleared pending transport queues.");
        }

        private void Schedule(IMessage message, double now, bool deliverToServer)
        {
            float jitter = RandomRange(-Settings.JitterMs, Settings.JitterMs);
            float latency = Mathf.Max(0f, Settings.BaseLatencyMs + jitter);

            if (Settings.SpikeEnabled)
            {
                latency += Settings.SpikeAdditionalLatencyMs;
            }

            ScheduledMessage scheduledMessage = new ScheduledMessage
            {
                Message = message,
                DeliveryTime = now + (latency / 1000.0),
                DeliverToServer = deliverToServer,
                SortOrder = _sortCounter++,
            };
            InsertScheduledMessage(scheduledMessage);

            GameTrace.Verbose(
                "Transport",
                "Scheduled " + message.GetType().Name + " to " + (deliverToServer ? "server" : "client") + " in " + latency.ToString("F0") + "ms.");
        }

        private float RandomRange(float min, float max)
        {
            double value = _random.NextDouble();
            return min + (float)value * (max - min);
        }

        private static int CompareMessages(ScheduledMessage left, ScheduledMessage right)
        {
            int timeCompare = left.DeliveryTime.CompareTo(right.DeliveryTime);
            if (timeCompare != 0)
            {
                return timeCompare;
            }

            return left.SortOrder.CompareTo(right.SortOrder);
        }

        private void InsertScheduledMessage(ScheduledMessage message)
        {
            int low = 0;
            int high = _scheduled.Count;

            while (low < high)
            {
                int mid = (low + high) / 2;
                if (CompareMessages(_scheduled[mid], message) <= 0)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid;
                }
            }

            _scheduled.Insert(low, message);
        }

        private static NetworkSimulationSettings BuildPreset(NetworkSimulationPreset preset)
        {
            NetworkPresetProfile profile = NetworkPresetProfiles.Get(preset);
            return new NetworkSimulationSettings
            {
                BaseLatencyMs = profile.BaseLatencyMs,
                JitterMs = profile.JitterMs,
                SpikeAdditionalLatencyMs = profile.SpikeAdditionalLatencyMs,
            };
        }
    }
}
