using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.LiveCapture.Ltc;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.LiveCapture.Tests.Editor
{
    // FDT patch (#16701): the LTC source's recording lifecycle, driven through a fake microphone.
    public class LtcTimecodeSourceTests
    {
        const string k_InputA = "LTC input A";
        const string k_InputB = "LTC input B";

        sealed class FakeMicrophone : IMicrophoneInput
        {
            public readonly HashSet<string> Recording = new HashSet<string>();
            public readonly List<string> Ended = new List<string>();
            readonly Dictionary<string, int> m_Sessions = new Dictionary<string, int>();
            int m_NextSession;
            public AudioClip Clip;
            public bool FailStart;
            public int StartCalls;

            public string DefaultDevice { get; set; }

            public bool IsRecording(string device) => device != null && Recording.Contains(device);

            public AudioClip Start(string device, bool loop, int lengthSec, int frequency, out int session)
            {
                StartCalls++;
                if (FailStart)
                {
                    session = 0;
                    return null;
                }
                Recording.Add(device);
                session = ++m_NextSession;
                m_Sessions[device] = session;
                return Clip;
            }

            public void End(string device)
            {
                Ended.Add(device);
                Recording.Remove(device);
                m_Sessions.Remove(device);
            }

            public int GetPosition(string device) => 0;

            public bool IsCurrentSession(string device, int session) =>
                session != 0 && IsRecording(device) && m_Sessions.TryGetValue(device, out var current) && current == session;

            // Another recorder that follows the handoff contract ends the input and starts it again.
            public void ReplaceRecording(string device)
            {
                End(device);
                Start(device, true, 1, 44100, out _);
            }
        }

        static readonly Regex k_StartFailed = new Regex("did not start recording");

        GameObject m_Host;
        FakeMicrophone m_Microphone;
        LtcTimecodeSource m_Source;

        [SetUp]
        public void SetUp()
        {
            m_Microphone = new FakeMicrophone
            {
                DefaultDevice = k_InputA,
                Clip = AudioClip.Create("LtcTimecodeSourceTests", 44100, 1, 44100, false),
            };
            m_Host = new GameObject("LtcTimecodeSourceTests");
            m_Host.SetActive(false);
            m_Source = m_Host.AddComponent<LtcTimecodeSource>();
            m_Source.m_Microphone = m_Microphone;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(m_Host);
            Object.DestroyImmediate(m_Microphone.Clip);
        }

        void Enable() => m_Host.SetActive(true);

        void DecodeFrame(byte second)
        {
            m_Source.OnFrameDecoded(new LtcDecoder.Frame { hour = 1, minute = 2, second = second, frame = 3 });
            m_Source.State.Update();
        }

        [Test]
        public void FailedStartOnEnable_LeavesSourceIdleWithoutThrowing()
        {
            m_Microphone.FailStart = true;
            LogAssert.Expect(LogType.Warning, k_StartFailed);

            Enable();

            Assert.That(m_Source.isActiveAndEnabled, Is.True);
            Assert.That(m_Source.IsRecording, Is.False);
            Assert.That(m_Source.RecordingDevice, Is.Null);
            m_Source.State.Update();
            Assert.That(m_Source.CurrentTime, Is.Null);
        }

        [Test]
        public void TryStartRecording_ReturnsFalseWhenMicrophoneDoesNotStart()
        {
            Enable();
            m_Microphone.FailStart = true;

            Assert.That(m_Source.TryStartRecording(k_InputB), Is.False);
            Assert.That(m_Source.IsRecording, Is.False);
            Assert.That(m_Microphone.Recording, Does.Not.Contain(k_InputA), "the source's own recording ends before the new start");
        }

        [Test]
        public void TryStartRecording_RefusesInputAnotherComponentRecords_AndKeepsCurrentRecording()
        {
            Enable();
            m_Microphone.Recording.Add(k_InputB);
            var startsBefore = m_Microphone.StartCalls;

            Assert.That(m_Source.TryStartRecording(k_InputB), Is.False);

            Assert.That(m_Microphone.StartCalls, Is.EqualTo(startsBefore));
            Assert.That(m_Microphone.Ended, Is.Empty);
            Assert.That(m_Source.RecordingDevice, Is.EqualTo(k_InputA));
        }

        [Test]
        public void TryStartRecording_MovesOwnRecordingToNewInput()
        {
            Enable();

            Assert.That(m_Source.TryStartRecording(k_InputB), Is.True);

            Assert.That(m_Microphone.Ended, Is.EqualTo(new[] { k_InputA }));
            Assert.That(m_Source.RecordingDevice, Is.EqualTo(k_InputB));
            Assert.That(m_Source.Device, Is.EqualTo(k_InputB));
        }

        [Test]
        public void OnEnable_RefusesDefaultInputAnotherComponentRecords()
        {
            m_Microphone.Recording.Add(k_InputA);
            LogAssert.Expect(LogType.Warning, k_StartFailed);

            Enable();

            Assert.That(m_Source.IsRecording, Is.False);
            Assert.That(m_Microphone.StartCalls, Is.Zero);
        }

        [Test]
        public void StopAndDisable_DoNotEndAnInputAnotherComponentRecords()
        {
            Enable();
            m_Source.StopRecording();
            Assert.That(m_Microphone.Ended, Is.EqualTo(new[] { k_InputA }));

            // Another component now records the input this source used last.
            m_Microphone.Recording.Add(k_InputA);
            m_Source.StopRecording();
            m_Host.SetActive(false);

            Assert.That(m_Microphone.Ended, Has.Count.EqualTo(1));
            Assert.That(m_Microphone.Recording, Does.Contain(k_InputA));
        }

        [Test]
        public void RecordingReplacedByAnotherRecorder_StopsReportingTimecode_AndIsNotEnded()
        {
            Enable();
            DecodeFrame(4);
            Assert.That(m_Source.CurrentTime, Is.Not.Null);

            // Another recorder ends input A and starts it again before the next poll.
            // The device-wide recording flag stays true for the replacement.
            m_Microphone.ReplaceRecording(k_InputA);
            Assert.That(m_Microphone.IsRecording(k_InputA), Is.True);
            m_Source.State.Update();

            Assert.That(m_Source.IsRecording, Is.False);
            Assert.That(m_Source.RecordingDevice, Is.Null);
            Assert.That(m_Source.CurrentTime, Is.Null);

            // Stopping or disabling must not end the replacement recording.
            var endsBefore = m_Microphone.Ended.Count;
            m_Source.StopRecording();
            m_Host.SetActive(false);
            Assert.That(m_Microphone.Ended, Has.Count.EqualTo(endsBefore));
            Assert.That(m_Microphone.Recording, Does.Contain(k_InputA));
        }

        [Test]
        public void RecordingReplacedBeforePoll_IsNotEndedOrRestartedByTheSource()
        {
            Enable();

            // Replaced before any poll: the source still holds its clip and the device name matches.
            m_Microphone.ReplaceRecording(k_InputA);
            var startsBefore = m_Microphone.StartCalls;
            var endsBefore = m_Microphone.Ended.Count;

            Assert.That(m_Source.TryStartRecording(k_InputA), Is.False, "input A now belongs to another recorder");
            m_Source.StopRecording();

            Assert.That(m_Microphone.StartCalls, Is.EqualTo(startsBefore));
            Assert.That(m_Microphone.Ended, Has.Count.EqualTo(endsBefore));
            Assert.That(m_Microphone.Recording, Does.Contain(k_InputA));
        }

        [Test]
        public void SetDevice_ToInputAnotherComponentRecords_KeepsCurrentRecordingAndTimecode()
        {
            Enable();
            DecodeFrame(4);
            m_Microphone.Recording.Add(k_InputB);
            var startsBefore = m_Microphone.StartCalls;
            LogAssert.Expect(LogType.Warning, new Regex("did not switch to the selected audio input"));

            m_Source.SetDevice(k_InputB);

            Assert.That(m_Microphone.StartCalls, Is.EqualTo(startsBefore));
            Assert.That(m_Microphone.Ended, Is.Empty);
            Assert.That(m_Source.IsRecording, Is.True);
            Assert.That(m_Source.RecordingDevice, Is.EqualTo(k_InputA));
            Assert.That(m_Source.Device, Is.EqualTo(k_InputA));
            Assert.That(m_Source.CurrentTime, Is.Not.Null);
            m_Source.State.Update();
            Assert.That(m_Source.CurrentTime, Is.Not.Null);
        }

        [Test]
        public void SetDevice_ToFreeInput_MovesRecording()
        {
            Enable();

            m_Source.SetDevice(k_InputB);

            Assert.That(m_Microphone.Ended, Is.EqualTo(new[] { k_InputA }));
            Assert.That(m_Source.RecordingDevice, Is.EqualTo(k_InputB));
            Assert.That(m_Source.Device, Is.EqualTo(k_InputB));
        }

        [Test]
        public void NoTimecodeIsReportedBeforeTheFirstDecodedFrame()
        {
            Enable();
            m_Source.State.Update();

            Assert.That(m_Source.IsRecording, Is.True);
            Assert.That(m_Source.CurrentTime, Is.Null);
        }

        [Test]
        public void StopRecording_ClearsCurrentTime()
        {
            Enable();
            DecodeFrame(4);
            Assert.That(m_Source.CurrentTime, Is.Not.Null);

            m_Source.StopRecording();

            Assert.That(m_Source.CurrentTime, Is.Null);
            m_Source.State.Update();
            Assert.That(m_Source.CurrentTime, Is.Null);
        }

        [Test]
        public void RecordingEndedElsewhere_StopsReportingTimecode()
        {
            Enable();
            DecodeFrame(4);
            Assert.That(m_Source.CurrentTime, Is.Not.Null);

            // A call or another app ends the recording.
            m_Microphone.Recording.Remove(k_InputA);
            m_Source.State.Update();

            Assert.That(m_Source.IsRecording, Is.False);
            Assert.That(m_Source.CurrentTime, Is.Null);
        }

        [Test]
        public void NewRecording_DoesNotReportPreviousRecordingsFrame()
        {
            Enable();
            DecodeFrame(4);
            m_Source.StopRecording();

            Assert.That(m_Source.TryStartRecording(k_InputA), Is.True);
            m_Source.State.Update();

            Assert.That(m_Source.CurrentTime, Is.Null);
            DecodeFrame(5);
            Assert.That(m_Source.CurrentTime, Is.Not.Null);
        }

        [Test]
        public void OnValidate_DoesNotStartAnIdleSource()
        {
            m_Microphone.FailStart = true;
            LogAssert.Expect(LogType.Warning, k_StartFailed);
            Enable();
            m_Microphone.FailStart = false;
            var startsBefore = m_Microphone.StartCalls;

            InvokeOnValidate();

            Assert.That(m_Microphone.StartCalls, Is.EqualTo(startsBefore));
            Assert.That(m_Source.IsRecording, Is.False);
        }

        [Test]
        public void OnValidate_RestartsOwnRecording()
        {
            Enable();

            InvokeOnValidate();

            Assert.That(m_Source.RecordingDevice, Is.EqualTo(k_InputA));
            Assert.That(m_Microphone.Ended, Is.EqualTo(new[] { k_InputA }));
        }

        [Test]
        public void TimecodeSourceStateClear_ForgetsFrameUntilNextPoll()
        {
            FrameTimeWithRate? polled = new FrameTimeWithRate(StandardFrameRate.FPS_24_00, new FrameTime(10));
            var state = new TimecodeSourceState(() => polled);
            try
            {
                state.Update();
                Assert.That(state.CurrentTime, Is.EqualTo(polled));

                state.Clear();
                Assert.That(state.CurrentTime, Is.Null);

                polled = null;
                state.Update();
                Assert.That(state.CurrentTime, Is.Null);
            }
            finally
            {
                state.Dispose();
            }
        }

        void InvokeOnValidate()
        {
            typeof(LtcTimecodeSource)
                .GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(m_Source, null);
        }
    }
}
