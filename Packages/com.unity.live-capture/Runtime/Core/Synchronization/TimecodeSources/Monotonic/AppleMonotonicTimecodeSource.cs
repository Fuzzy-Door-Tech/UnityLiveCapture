using System;
using Unity.LiveCapture;
using UnityEngine;

namespace Unity.LiveCapture.Monotonic
{
    [ExecuteAlways]
    [CreateTimecodeSourceMenuItem("iOS/macOS Monotonic Timecode Source")]
    [AddComponentMenu("Live Capture/Timecode/iOS/macOS Monotonic Timecode Source")]
    public partial class AppleMonotonicTimecodeSource : MonotonicTimecodeSourceBase
    {
        public override string FriendlyName => $"Apple Monotonic ({name})";

        private double _startMonotonicTime;

        protected override void OnEnable()
        {
            base.OnEnable();
            _startMonotonicTime = GetMonotonicTimeInternal();
            ResetSession(DateTime.Now, _startMonotonicTime); // Use local time for consistency
            Debug.Log($"Apple: WallClock={DateTime.Now}, MonotonicStart={_startMonotonicTime}");
        }

        protected override double GetMonotonicTimeInternal()
        {
#if (UNITY_IOS || UNITY_VISIONOS) && !UNITY_EDITOR
            return _GetMonotonicTimeSeconds();
#elif (UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX) && !UNITY_IOS
            return _GetMonotonicTimeSeconds();
#else
            return Time.realtimeSinceStartupAsDouble; // Fallback
#endif
        }

#if (UNITY_IOS || UNITY_VISIONOS) && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern double _GetMonotonicTimeSeconds();
#endif

#if (UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX) && !UNITY_IOS
        [System.Runtime.InteropServices.DllImport("MonotonicClock")]
        private static extern double _GetMonotonicTimeSeconds();
#endif
    }
}