using System.Collections.Generic;
using UnityEngine;

namespace EggTest.Shared
{
    /// <summary>
    /// Centralized debug logger for the prototype.
    /// Logging is routed through one place so it can be throttled or disabled when profiling performance.
    /// </summary>
    public static class GameTrace
    {
        private static readonly Dictionary<string, double> LastLogTimes = new Dictionary<string, double>();

        public static bool Enabled { get; private set; } = true;
        public static bool VerboseEnabled { get; private set; }

        public static void Configure(bool enabled, bool verboseEnabled)
        {
            Enabled = enabled;
            VerboseEnabled = verboseEnabled;
        }

        public static void Log(string category, string message)
        {
            if (!Enabled)
            {
                return;
            }

            Debug.Log(Format(category, message));
        }

        public static void Warn(string category, string message)
        {
            if (!Enabled)
            {
                return;
            }

            Debug.LogWarning(Format(category, message));
        }

        public static void Verbose(string category, string message)
        {
            if (!Enabled || !VerboseEnabled)
            {
                return;
            }

            Debug.Log(Format(category, message));
        }

        public static void LogEvery(string category, string key, float intervalSeconds, string message, bool verboseOnly = false)
        {
            if (!Enabled || (verboseOnly && !VerboseEnabled))
            {
                return;
            }

            string combinedKey = category + ":" + key;
            double now = Time.realtimeSinceStartupAsDouble;

            double lastTime;
            if (LastLogTimes.TryGetValue(combinedKey, out lastTime) && now - lastTime < intervalSeconds)
            {
                return;
            }

            LastLogTimes[combinedKey] = now;
            Debug.Log(Format(category, message));
        }

        private static string Format(string category, string message)
        {
            return "[EggTest][" + category + "] " + message;
        }
    }
}
