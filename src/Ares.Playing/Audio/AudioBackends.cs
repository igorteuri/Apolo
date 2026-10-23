/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 Where the mixed sound of the audio engine goes. A "sink" takes the mixed blocks (32 bit float, interleaved); the "backend"
 of the system lists the sound cards and opens a sink: WASAPI (through NAudio) on Windows, OpenAL Soft on Linux and macOS.
*/
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Ares.Playing.Audio
{
    internal abstract class Sink : IDisposable
    {
        public int Channels = 2;
        public string DeviceId;
        public volatile bool Failed;
        public abstract int BufferedFrames { get; }
        public abstract void Write(byte[] data, int count);
        public abstract void Dispose();
    }

    /// <summary>No sound card: only a clock, so that the mixer runs in real time.</summary>
    internal sealed class NullSink : Sink
    {
        private readonly Stopwatch m_Clock = Stopwatch.StartNew();
        private long m_Produced;

        public override int BufferedFrames
        {
            get
            {
                long played = (long)(m_Clock.Elapsed.TotalSeconds * AudioEngine.SampleRate);
                if (m_Produced < played)
                    m_Produced = played;
                return (int)(m_Produced - played);
            }
        }

        public override void Write(byte[] data, int count)
        {
            m_Produced += count / (4 * Channels);
        }

        public override void Dispose()
        {
        }
    }

    /// <summary>The way a system plays sound.</summary>
    internal abstract class AudioBackend
    {
        private static AudioBackend s_Current;

        public static AudioBackend Current
        {
            get
            {
                if (s_Current == null)
                {
                    string forced = Environment.GetEnvironmentVariable("APOLO_AUDIO_BACKEND");
                    if (string.Equals(forced, "openal", StringComparison.OrdinalIgnoreCase))
                        s_Current = new OpenAlBackend();
                    else
                        s_Current = OperatingSystem.IsWindows() ? (AudioBackend)new WasapiBackend() : new OpenAlBackend();
                }
                return s_Current;
            }
        }

        /// <summary>The sound cards (Index is set by the caller).</summary>
        public abstract List<AudioDeviceInfo> ListCards();
        /// <summary>Id of the default sound card (empty text for "the system's default" if the backend has no ids), null if none.</summary>
        public abstract string DefaultDeviceId();
        /// <summary>Number of speakers to use for the card: 2, 6 or 8.</summary>
        public abstract int MixChannels(string deviceId);
        public abstract Sink Open(string deviceId, int channels);
    }
}
