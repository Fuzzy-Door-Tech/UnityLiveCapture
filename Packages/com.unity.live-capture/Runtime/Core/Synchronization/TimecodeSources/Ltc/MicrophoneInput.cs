using UnityEngine;

namespace Unity.LiveCapture.Ltc
{
    // FDT patch (#16701): the microphone calls LtcTimecodeSource makes, behind a seam so tests
    // can reproduce a failed start, another owner's recording, and a lost recording without hardware.
    interface IMicrophoneInput
    {
        string DefaultDevice { get; }
        bool IsRecording(string device);
        AudioClip Start(string device, bool loop, int lengthSec, int frequency);
        void End(string device);
        int GetPosition(string device);
    }

    // FDT patch (#16701): the production IMicrophoneInput over UnityEngine.Microphone. Main thread only.
    sealed class MicrophoneInput : IMicrophoneInput
    {
        public static readonly MicrophoneInput Instance = new MicrophoneInput();

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

        public AudioClip Start(string device, bool loop, int lengthSec, int frequency) =>
            Microphone.Start(device, loop, lengthSec, frequency);

        public void End(string device) => Microphone.End(device);

        public int GetPosition(string device) => Microphone.GetPosition(device);
    }
}
