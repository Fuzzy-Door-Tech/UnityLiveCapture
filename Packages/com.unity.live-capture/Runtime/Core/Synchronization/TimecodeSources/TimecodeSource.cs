using System;
using UnityEngine;

namespace Unity.LiveCapture
{
    /// <summary>
    /// A base class that includes functionality suitable for most <see cref="ITimecodeSource"/> implementations.
    /// </summary>
    public abstract class TimecodeSource : MonoBehaviour, ITimecodeSource
    {
        [SerializeField, HideInInspector]
        string m_Guid;

        TimecodeSourceState m_State;

        /// <inheritdoc/>
        public string Id => m_Guid;

        /// <inheritdoc/>
        public virtual string FriendlyName => $"{GetType().Name} ({name})";

        /// <inheritdoc/>
        public abstract FrameRate FrameRate { get; set; }

        /// <inheritdoc/>
        public FrameTimeWithRate? CurrentTime => m_State?.CurrentTime;

        /// <summary>
        /// This method is called by Unity when the component becomes enabled and active.
        /// </summary>
        protected virtual void OnEnable()
        {
            TimecodeSourceManager.Instance.EnsureIdIsValid(ref m_Guid);
            TimecodeSourceManager.Instance.Register(this);

            m_State = new TimecodeSourceState(PollTimecode);
        }

        /// <summary>
        /// This method is called by Unity when the component becomes disabled.
        /// </summary>
        protected virtual void OnDisable()
        {
            TimecodeSourceManager.Instance.Unregister(this);

            if (m_State != null)
            {
                m_State.Dispose();
                m_State = null;
            }
        }

        /// <summary>
        /// Clears <see cref="CurrentTime"/> until the next successful poll.
        /// </summary>
        /// <remarks>
        /// Call this when the source stops producing timecode, so readers fall back instead of reading the last frame.
        /// </remarks>
        // FDT patch (#16701)
        protected void ClearCurrentTime()
        {
            m_State?.Clear();
        }

        // FDT patch (#16701): lets tests run one state update without the player loop.
        internal TimecodeSourceState State => m_State;

        /// <summary>
        /// Gets the latest timecode from the timecode source.
        /// </summary>
        /// <param name="frameRate">The rate at which the timecode is incremented.</param>
        /// <param name="timecode">The most recent timecode.</param>
        /// <returns><see langword="true"/> if a valid timecode was available; otherwise, <see langword="false"/>.</returns>
        protected abstract bool TryPollTimecode(out FrameRate frameRate, out Timecode timecode);

        FrameTimeWithRate? PollTimecode()
        {
            if (TryPollTimecode(out var frameRate, out var timecode))
            {
                return new FrameTimeWithRate(frameRate, timecode.ToFrameTime(frameRate));
            }

            return default;
        }
    }
}
