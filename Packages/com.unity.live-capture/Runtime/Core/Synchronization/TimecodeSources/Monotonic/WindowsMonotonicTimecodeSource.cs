using System;
using Unity.LiveCapture;
using UnityEngine;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace Unity.LiveCapture.Monotonic
{
    [ExecuteAlways]
    [CreateTimecodeSourceMenuItem("Windows Monotonic Timecode Source")]
    [AddComponentMenu("Live Capture/Timecode/Windows Monotonic Timecode Source")]
    public class WindowsMonotonicTimecodeSource : MonotonicTimecodeSourceBase
    {
        private long _startTicks;
        private long _frequency;

        public override string FriendlyName => $"Windows Monotonic ({name})";

        protected override void OnEnable()
        {
            base.OnEnable();
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            QueryPerformanceFrequency(out _frequency);
            QueryPerformanceCounter(out _startTicks);
            ResetSession(DateTime.Now, (double)_startTicks / _frequency);
            Debug.Log($"Windows: WallClock={DateTime.Now}, MonotonicStart={(double)_startTicks / _frequency}");
#else
            // In Editor, use Unity's time since startup for consistency
            _startTicks = 0; // Reset to align with wall clock
            _frequency = 1; // Not used with Time.realtimeSinceStartupAsDouble
            double monotonicStart = Time.realtimeSinceStartupAsDouble;
            ResetSession(DateTime.Now, monotonicStart);
            Debug.Log($"Editor: WallClock={DateTime.Now}, MonotonicStart={monotonicStart}, frame Rate={m_FrameRate}");
#endif
        }

        protected override double GetMonotonicTimeInternal()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            QueryPerformanceCounter(out long currentTicks);
            return (double)currentTicks / _frequency;
#else
            return Time.realtimeSinceStartupAsDouble; // Consistent Editor fallback
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [DllImport("Kernel32.dll")]
        private static extern bool QueryPerformanceCounter(out long ticks);

        [DllImport("Kernel32.dll")]
        private static extern bool QueryPerformanceFrequency(out long frequency);
#endif
    }
}