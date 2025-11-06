using Unity.LiveCapture;
using UnityEngine;
using System;

namespace Unity.LiveCapture.Monotonic
{
    public abstract class MonotonicTimecodeSourceBase : TimecodeSource
    {
        [SerializeField, Tooltip("The frame rate to output timecodes.")]
        [OnlyStandardFrameRates]
        protected FrameRate m_FrameRate = StandardFrameRate.FPS_24_00;

        protected DateTime BaseWallClockTime;
        protected double StartMonotonicTime;

        public override FrameRate FrameRate
        {
            get => m_FrameRate;
            set => m_FrameRate = value;
        }

        protected void ResetSession(DateTime wallClockTime, double monotonicTime)
        {
            BaseWallClockTime = wallClockTime;
            StartMonotonicTime = monotonicTime;

            Debug.Log($"ResetSession: WallClock={wallClockTime}, FrameRate={m_FrameRate}");
        }

        protected override bool TryPollTimecode(out FrameRate frameRate, out Timecode timecode)
        {
            frameRate = m_FrameRate;
            timecode = GetTimecodeWithWallClockAnchor(GetMonotonicTimeInternal());
            return true;
        }

        public Timecode GetUncachedTimecode()
        {
            return GetTimecodeWithWallClockAnchor(GetMonotonicTimeInternal());
        }

        protected Timecode GetTimecodeWithWallClockAnchor(double currentMonotonicTime)
        {
            // How long since ResetSession()
            double monotonicElapsed = currentMonotonicTime - StartMonotonicTime;

            // Convert anchor wall clock to "seconds since midnight" and add elapsed
            double secondsSinceMidnight = BaseWallClockTime.TimeOfDay.TotalSeconds + monotonicElapsed;

            // Now map directly into frames at *current* framerate
            double frameRateValue = m_FrameRate.AsDouble();
            double exactFrame = secondsSinceMidnight * frameRateValue;
            int totalFrameCount = (int)Math.Floor(exactFrame);

            double fractional = exactFrame - totalFrameCount;
            const double epsilon = 1e-6;
            if (fractional >= 1.0 - epsilon)
            {
                fractional = 0.0;
                totalFrameCount++;
            }

            var subframe = Subframe.FromFloat((float)fractional, Subframe.DefaultResolution);

            int framesPerSecond = (int)Math.Round(frameRateValue);
            int frameInSecond = totalFrameCount % framesPerSecond;
            int totalSeconds = totalFrameCount / framesPerSecond;
            int correctedSeconds = totalSeconds % 60;
            int correctedMinutes = (totalSeconds / 60) % 60;
            int correctedHours = (totalSeconds / 3600) % 24;

            return Timecode.FromHMSF(
                m_FrameRate,
                correctedHours,
                correctedMinutes,
                correctedSeconds,
                frameInSecond,
                subframe,
                isDropFrame: false
            );
        }

        protected abstract double GetMonotonicTimeInternal();
    }
}
