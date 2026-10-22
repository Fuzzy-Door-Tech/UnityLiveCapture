using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.LiveCapture.Ltc
{
    // FDT patch (#16701): the microphone calls LtcTimecodeSource makes, behind a seam so tests
    // can reproduce a failed start, another owner's recording, and a lost recording without hardware.
    interface IMicrophoneInput
    {
        string DefaultDevice { get; }
        bool IsRecording(string device);

        // FDT patch (#16701): session identifies this start. Every start through the seam gets a new
        // session, so a recorder that ended and restarted the input is a different session.
        AudioClip Start(string device, bool loop, int lengthSec, int frequency, out int session);
        void End(string device);
        int GetPosition(string device);

        // FDT patch (#16701): true while device records and its current recording is session.
        bool IsCurrentSession(string device, int session);
    }

    // FDT patch (#16701): the production IMicrophoneInput over UnityEngine.Microphone. Main thread only.
    //
    // Handoff contract: every recorder that starts or ends an input through this class (all
    // LtcTimecodeSource instances share Instance) replaces the input's session, so the previous
    // owner sees the replacement on its next poll and stops reading or ending that input.
    // Limit: UnityEngine.Microphone exposes only a device-wide IsRecording flag. Code that calls
    // UnityEngine.Microphone.End and then Start for the same input directly, outside this class,
    // cannot be detected; the previous session then still looks current.
    sealed class MicrophoneInput : IMicrophoneInput
    {
        public static readonly MicrophoneInput Instance = new MicrophoneInput();

        // Current session for each input started through this class. One entry per audio input name,
        // so the dictionary grows only when an input is first started (bounded by the input count).
        readonly Dictionary<string, int> m_Sessions = new Dictionary<string, int>(StringComparer.Ordinal);
        int m_NextSession;

        public string DefaultDevice
        {
            get
            {
                // Microphone.devices returns a new array; Unity has no non-allocating form.
                // It runs only from Reset and OnEnable, never per frame.
                var devices = Microphone.devices;
                return devices.Length > 0 ? devices[0] : null;
            }
        }

        public bool IsRecording(string device) => Microphone.IsRecording(device);

        public AudioClip Start(string device, bool loop, int lengthSec, int frequency, out int session)
        {
            var clip = Microphone.Start(device, loop, lengthSec, frequency);
            var key = device ?? string.Empty;

            if (clip == null)
            {
                // Whatever recorded the input before this failed start is no longer a known session.
                m_Sessions.Remove(key);
                session = 0;
                return null;
            }

            // Session 0 means "none", so a wrapped counter skips it.
            if (++m_NextSession == 0)
                m_NextSession = 1;

            session = m_NextSession;
            m_Sessions[key] = session;
            return clip;
        }

        public void End(string device)
        {
            Microphone.End(device);
            m_Sessions.Remove(device ?? string.Empty);
        }

        public int GetPosition(string device) => Microphone.GetPosition(device);

        public bool IsCurrentSession(string device, int session) =>
            session != 0
            && Microphone.IsRecording(device)
            && m_Sessions.TryGetValue(device ?? string.Empty, out var current)
            && current == session;
    }
}
