using System;
using UnityEngine;

namespace Unity.LiveCapture.Ltc
{
    /// <summary>
    /// A component that reads timecodes from an LTC audio stream.
    /// </summary>
    [ExecuteAlways]
    [CreateTimecodeSourceMenuItemAttribute("LTC Timecode Source")]
    [AddComponentMenu("Live Capture/Timecode/LTC Timecode Source")]
    [HelpURL(Documentation.baseURL + "ref-component-ltc-timecode-source" + Documentation.endURL)]
    public class LtcTimecodeSource : TimecodeSource
    {
        /// <summary>
        /// The sampling rate to try to use.
        /// </summary>
        const int k_TargetSampleRate = 44100;

        /// <summary>
        /// The number of seconds of audio to buffer.
        /// </summary>
        /// <remarks>
        /// Changes to this value do not impact latency, just the behaviour if too many samples are recorded before they can
        /// be processed. Since we only need the last sampleRate / frameRate samples to read the most recent timecode,
        /// this is easily enough for our purposes.
        /// </remarks>
        const int k_BufferSize = 1;

        // FDT patch (#16701): constant, so a failed start does not allocate.
        const string k_StartFailedMessage =
            "LTC Timecode Source did not start recording its audio input. The input may be missing, " +
            "in use by another component or app, or not permitted. The source stays idle and reports no timecode.";

        // FDT patch (#16701): constant, so a refused device switch does not allocate.
        const string k_DeviceBusyMessage =
            "LTC Timecode Source did not switch to the selected audio input because another component records it. " +
            "The source keeps recording its current input.";

        [SerializeField, Tooltip("The frame rate of the timecodes.")]
        [OnlyStandardFrameRates]
        FrameRate m_FrameRate = StandardFrameRate.FPS_24_00;
        
        [SerializeField, Tooltip("The LTC audio line in.")]
        string m_Device;

        [SerializeField, Tooltip("The audio channel containing the LTC audio.")]
        [Range(0, 15)]
        int m_Channel = 0;

        readonly LtcDecoder m_Decoder = new LtcDecoder();
        AudioClip m_Clip;
        int m_ClipPos;
        float[] m_Buffer;
        Timecode m_LastTimecode;

        // FDT patch (#16701): the input this source started, or null while it does not record.
        // Only this input is ever ended, so a stop never ends a recording that another component owns.
        string m_RecordingDevice;

        // FDT patch (#16701): the microphone session this source started (0 while it does not record).
        // A non-null clip and a matching device name do not prove ownership: another recorder can end
        // and restart the same input between polls. The session changes on every such restart.
        int m_RecordingSession;

        // FDT patch (#16701): true once the current recording decoded a frame. Until then no
        // timecode is reported (the vendor reported 00:00:00:00).
        bool m_HasDecodedFrame;

        // FDT patch (#16701): tests replace the microphone to reproduce failures without hardware.
        internal IMicrophoneInput m_Microphone = MicrophoneInput.Instance;

        /// <inheritdoc/>
        public override string FriendlyName => $"LTC ({name})";

        /// <inheritdoc />
        
        public override FrameRate FrameRate
        {
            get => m_FrameRate;
            set => m_FrameRate = value;
        }

        /// <summary>
        /// The selected audio input.
        /// </summary>
        // FDT patch (#16701)
        public string Device => m_Device;

        /// <summary>
        /// The audio input this source records, or <see langword="null"/> when it does not record.
        /// </summary>
        // FDT patch (#16701)
        public string RecordingDevice => m_RecordingDevice;

        /// <summary>
        /// Whether this source is recording. Only a confirmed recording start sets this, and it clears when
        /// the recording stops, fails, or is ended by something else.
        /// </summary>
        // FDT patch (#16701)
        public bool IsRecording => m_Clip != null;

        void Reset()
        {
            // FDT patch (#16701): read through the microphone seam.
            // Developer Edit. If no microphones found, reset to null. Otherwise it may try to load a microphone from another computer
            m_Device = m_Microphone.DefaultDevice;
        }

        void OnValidate()
        {
            // FDT patch (#16701): restart only a recording this source owns. An inspector edit no
            // longer starts an idle source, and the restart cannot end another component's recording.
            if (isActiveAndEnabled && IsRecording && !StartRecording())
            {
                Debug.LogWarning(k_StartFailedMessage, this);
            }
        }

        /// <inheritdoc />
        protected override void OnEnable()
        {
            base.OnEnable();

            m_Decoder.FrameDecoded += OnFrameDecoded;

            // Always reset device to ensure the correct microphone is selected at runtime,
            // as the device ID may differ across machines or platforms.
            Reset();

            // FDT patch (#16701): a failed start leaves the source enabled and idle instead of throwing.
            if (!StartRecording() && !string.IsNullOrEmpty(m_Device))
            {
                Debug.LogWarning(k_StartFailedMessage, this);
            }
        }

        /// <inheritdoc />
        protected override void OnDisable()
        {
            base.OnDisable();

            m_Decoder.FrameDecoded -= OnFrameDecoded;

            StopRecording();
        }

        /// <summary>
        /// Sets the audio device used as the LTC audio input.
        /// </summary>
        /// <remarks>
        /// You can query the available devices using <c>Microphone.devices</c>.
        /// </remarks>
        /// <param name="device">The device to use. If null or empty the default audio input will be used.</param>
        public void SetDevice(string device)
        {
            if (m_Device == device)
            {
                return;
            }

            var wasRecording = m_Clip != null;

            // FDT patch (#16701): refuse an input another component records before ending this
            // source's recording, so the refusal keeps the current input and its timecode.
            if (wasRecording && IsRecordedByAnother(device))
            {
                Debug.LogWarning(k_DeviceBusyMessage, this);
                return;
            }

            StopRecording();

            m_Device = device;

            // FDT patch (#16701): report a failed restart instead of throwing.
            if (wasRecording && !StartRecording())
            {
                Debug.LogWarning(k_StartFailedMessage, this);
            }
        }

        /// <summary>
        /// Selects <paramref name="device"/> and starts recording it, ending this source's own recording first.
        /// </summary>
        /// <remarks>
        /// The start fails when the source is not active and enabled, when the input is already recording in
        /// another component (this source then keeps its current recording), or when the microphone does not
        /// start (this source is then idle).
        /// </remarks>
        /// <param name="device">The audio input to record. You can query the available devices using <c>Microphone.devices</c>.</param>
        /// <returns><see langword="true"/> if the recording started; otherwise, <see langword="false"/>.</returns>
        // FDT patch (#16701)
        public bool TryStartRecording(string device)
        {
            if (!isActiveAndEnabled || string.IsNullOrEmpty(device))
                return false;

            m_Device = device;
            return StartRecording();
        }

        // FDT patch (#16701): returns whether the recording started. A failed Microphone.Start
        // (null clip) no longer throws a NullReferenceException.
        bool StartRecording()
        {
            if (string.IsNullOrEmpty(m_Device))
                return false;

            // FDT patch (#16701): Microphone.Start would restart, and so take over, an input that
            // another component records. Refuse it and keep this source's current recording.
            // The check uses the session, not the device name, so a lost recording does not count as ours.
            if (IsRecordedByAnother(m_Device))
                return false;

            // FDT patch (#16701): ends only this source's own recording.
            StopRecording();

            // FDT patch (#16701): the unused Microphone.GetDeviceCaps query is removed.
            // Don't clamp sample rate: when minFreq and maxFreq are 0, the device supports any sample rate
            var sampleRate = k_TargetSampleRate;

            // prepare the decoder
            m_Decoder.Initialize(sampleRate, m_FrameRate.AsFloat());

            // start recording audio samples
            var clip = m_Microphone.Start(m_Device, true, k_BufferSize, sampleRate, out var session);

            // FDT patch (#16701): Microphone.Start returns null when the input does not start.
            if (clip == null)
                return false;

            m_Clip = clip;
            m_RecordingDevice = m_Device;
            m_RecordingSession = session;
            m_ClipPos = 0;
            m_HasDecodedFrame = false;

            // create a buffer large enough to hold the buffered audio
            var bufferSize = k_BufferSize * sampleRate * m_Clip.channels;

            if (m_Buffer == null || m_Buffer.Length < bufferSize)
            {
                m_Buffer = new float[bufferSize];
            }

            return true;
        }

        /// <summary>
        /// Stops recording. The source stays enabled and reports no timecode until a recording starts again.
        /// </summary>
        /// <remarks>
        /// Only a recording that this source started, and that no other recorder has replaced, is ended.
        /// </remarks>
        // FDT patch (#16701): public. The vendor ended whatever input m_Device named, even one that
        // another component recorded, and kept serving the last frame.
        public void StopRecording()
        {
            if (OwnsRecording())
            {
                m_Microphone.End(m_RecordingDevice);
            }

            ReleaseRecording();
        }

        // FDT patch (#16701): true while this source's own session is the input's current recording.
        bool OwnsRecording() =>
            m_Clip != null && m_Microphone.IsCurrentSession(m_RecordingDevice, m_RecordingSession);

        // FDT patch (#16701): true when device records a session that this source did not start.
        bool IsRecordedByAnother(string device)
        {
            if (string.IsNullOrEmpty(device) || !m_Microphone.IsRecording(device))
                return false;

            return device != m_RecordingDevice || !OwnsRecording();
        }

        // FDT patch (#16701): forgets the recording and its timecode, so readers fall back.
        void ReleaseRecording()
        {
            m_Clip = null;
            m_RecordingDevice = null;
            m_RecordingSession = 0;
            m_HasDecodedFrame = false;
            m_LastTimecode = default;
            ClearCurrentTime();
        }

        /// <inheritdoc />
        protected override bool TryPollTimecode(out FrameRate frameRate, out Timecode timecode)
        {
            frameRate = m_FrameRate;

            // FDT patch (#16701): no timecode until this recording decodes a frame.
            if (ProcessSamples() && m_HasDecodedFrame)
            {
                timecode = m_LastTimecode;
                return true;
            }

            timecode = default;
            return false;
        }

        bool ProcessSamples()
        {
            if (m_Clip == null)
            {
                return false;
            }

            // FDT patch (#16701): the recording can end without this source (another app, a call,
            // or Microphone.End elsewhere), or another recorder can end and restart the same input.
            // Either way this clip is no longer the input's recording. Stop serving its last frame.
            if (!OwnsRecording())
            {
                ReleaseRecording();
                return false;
            }

            try
            {
                var clipPos = m_Microphone.GetPosition(m_RecordingDevice);

                if (clipPos == m_ClipPos)
                {
                    return true;
                }

                var channelCount = m_Clip.channels;
                var channel = Mathf.Clamp(m_Channel, 0, channelCount - 1);

                m_Clip.GetData(m_Buffer, m_ClipPos);

                var count = clipPos - m_ClipPos;

                if (count < 0)
                    count += m_Clip.samples;

                for (var i = 0; i < count; i++)
                {
                    var sample = m_Buffer[(i * channelCount) + channel];
                    m_Decoder.Decode(sample);
                }

                m_ClipPos = clipPos;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Error processing LTC samples: {e.Message}");
                return false;
            }
        }

        // FDT patch (#16701): internal so tests can deliver a decoded frame.
        internal void OnFrameDecoded(LtcDecoder.Frame frame)
        {
            m_LastTimecode = Timecode.FromHMSF(m_FrameRate, frame.hour, frame.minute, frame.second, frame.frame);
            m_HasDecodedFrame = true;
        }
    }
}
