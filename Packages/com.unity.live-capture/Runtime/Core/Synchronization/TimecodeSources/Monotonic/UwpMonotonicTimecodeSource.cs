using System;
using Unity.LiveCapture;
using UnityEngine;

#if ENABLE_WINMD_SUPPORT && !UNITY_EDITOR
using Windows.Perception;
using static Windows.Perception.PerceptionTimestampHelper;
#endif

namespace Unity.LiveCapture.Monotonic
{
    [ExecuteAlways]
    [CreateTimecodeSourceMenuItem("Windows UWP Monotonic Timecode Source")]
    [AddComponentMenu("Live Capture/Timecode/Windows UWP Monotonic Timecode Source")]
    public class UwpMonotonicTimecodeSource : MonotonicTimecodeSourceBase
    {
        public override string FriendlyName => $"UWP Monotonic ({name})";

        protected override void OnEnable()
        {
            base.OnEnable();
#if ENABLE_WINMD_SUPPORT && !UNITY_EDITOR
            double monotonicStart = GetMonotonicTimeInternal();
            ResetSession(DateTime.UtcNow, monotonicStart);
            Debug.Log($"UWP: WallClock={DateTime.UtcNow}, MonotonicStart={monotonicStart}");
#else
            double monotonicStart = Time.realtimeSinceStartupAsDouble;
            ResetSession(DateTime.Now, monotonicStart);
            Debug.Log($"UWP (Editor): WallClock={DateTime.Now}, MonotonicStart={monotonicStart}");
#endif
        }

#if ENABLE_WINMD_SUPPORT && !UNITY_EDITOR
        protected override double GetMonotonicTimeInternal()
        {
            var perceptionTimestamp =
 Windows.Perception.PerceptionTimestampHelper.FromHistoricalTargetTime(DateTimeOffset.UtcNow);
            var targetTime = perceptionTimestamp.TargetTime;
            return targetTime.TimeOfDay.TotalSeconds;
        }
#else
        protected override double GetMonotonicTimeInternal()
        {
            return Time.realtimeSinceStartupAsDouble;
        }
#endif
    }
}