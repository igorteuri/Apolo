/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 The sound output of Linux and macOS: OpenAL Soft (the library comes with the program, through the Silk.NET packages;
 it talks to PulseAudio / PipeWire / ALSA on Linux and to CoreAudio on macOS). The mixed blocks are queued as buffers
 of one streaming source. With the environment variable ALSOFT_DRIVERS=null the library makes no sound (used by the tests).
*/
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Silk.NET.Core.Contexts;
using Silk.NET.OpenAL;
using Silk.NET.OpenAL.Extensions.Creative;

namespace Ares.Playing.Audio
{
    internal sealed unsafe class OpenAlBackend : AudioBackend
    {
        private const int FormatStereoFloat32 = 0x10011;   // AL_FORMAT_STEREO_FLOAT32 (AL_EXT_FLOAT32)

        private static readonly object s_ApiLock = new object();
        private static ALContext s_Alc;
        private static AL s_Al;

        internal static ALContext Alc
        {
            get { EnsureApi(); return s_Alc; }
        }

        internal static AL Al
        {
            get { EnsureApi(); return s_Al; }
        }

        private static string[] NativeNames()
        {
            string file = OperatingSystem.IsWindows() ? "soft_oal.dll" : (OperatingSystem.IsMacOS() ? "libopenal.dylib" : "libopenal.so");
            string os = OperatingSystem.IsWindows() ? "win" : (OperatingSystem.IsMacOS() ? "osx" : "linux");
            string cpu;
            switch (RuntimeInformation.ProcessArchitecture)
            {
                case Architecture.Arm64: cpu = "arm64"; break;
                case Architecture.Arm: cpu = "arm"; break;
                case Architecture.X86: cpu = "x86"; break;
                default: cpu = "x64"; break;
            }
            List<string> names = new List<string>();
            string baseDir = AppContext.BaseDirectory;
            foreach (string dir in new[] { baseDir, Path.Combine(baseDir, "runtimes", os + "-" + cpu, "native") })
            {
                string path = Path.Combine(dir, file);
                if (File.Exists(path))
                    names.Add(path);
            }
            // the library of the system, if there is one
            if (OperatingSystem.IsLinux())
            {
                names.Add("libopenal.so.1");
                names.Add("libopenal.so");
            }
            else if (OperatingSystem.IsMacOS())
            {
                names.Add("/System/Library/Frameworks/OpenAL.framework/OpenAL");
            }
            return names.ToArray();
        }

        private static void EnsureApi()
        {
            lock (s_ApiLock)
            {
                if (s_Alc == null)
                {
                    // the library is loaded from the exact file which comes with the program (the default search of the
                    // packages does not look in the "runtimes" folder when the program is not published)
                    string[] names = NativeNames();
                    s_Alc = new ALContext(new DefaultNativeContext(names));
                    s_Al = new AL(new DefaultNativeContext(names));
                }
            }
        }

        private sealed class OpenAlSink : Sink
        {
            private readonly ALContext m_Alc;
            private readonly AL m_Al;
            private Device* m_Device;
            private Context* m_Context;
            private uint m_Source;
            private readonly Queue<KeyValuePair<uint, int>> m_Queued = new Queue<KeyValuePair<uint, int>>();
            private readonly Stack<uint> m_Free = new Stack<uint>();
            private long m_QueuedFrames;
            private readonly bool m_Float;
            private short[] m_Short = new short[0];
            private bool m_Disposed;

            public OpenAlSink(string deviceId)
            {
                DeviceId = deviceId;
                Channels = 2;
                m_Alc = Alc;
                m_Al = Al;
                m_Device = m_Alc.OpenDevice(string.IsNullOrEmpty(deviceId) ? null : deviceId);
                if (m_Device == null)
                    throw new InvalidOperationException("OpenAL: the sound card could not be opened.");
                m_Context = m_Alc.CreateContext(m_Device, null);
                if (m_Context == null)
                {
                    m_Alc.CloseDevice(m_Device);
                    m_Device = null;
                    throw new InvalidOperationException("OpenAL: no context.");
                }
                m_Alc.MakeContextCurrent(m_Context);
                m_Float = m_Al.IsExtensionPresent("AL_EXT_FLOAT32");
                m_Source = m_Al.GenSource();
                m_Al.SetSourceProperty(m_Source, SourceBoolean.Looping, false);
                m_Al.SetSourceProperty(m_Source, SourceBoolean.SourceRelative, true);
            }

            // there is one current context for the whole process: make it ours before every call
            private void Current()
            {
                m_Alc.MakeContextCurrent(m_Context);
            }

            // gives back the buffers which were played
            private void Recycle()
            {
                m_Al.GetSourceProperty(m_Source, GetSourceInteger.BuffersProcessed, out int processed);
                while (processed-- > 0 && m_Queued.Count > 0)
                {
                    uint buffer = 0;
                    m_Al.SourceUnqueueBuffers(m_Source, 1, &buffer);
                    KeyValuePair<uint, int> first = m_Queued.Dequeue();
                    m_QueuedFrames -= first.Value;
                    m_Free.Push(first.Key);
                }
            }

            public override int BufferedFrames
            {
                get
                {
                    if (m_Disposed)
                        return 0;
                    Current();
                    Recycle();
                    m_Al.GetSourceProperty(m_Source, GetSourceInteger.SampleOffset, out int offset);
                    long frames = m_QueuedFrames - Math.Max(0, offset);
                    return (int)Math.Max(0, frames);
                }
            }

            public override void Write(byte[] data, int count)
            {
                if (m_Disposed || count <= 0)
                    return;
                Current();
                Recycle();
                uint buffer = m_Free.Count > 0 ? m_Free.Pop() : m_Al.GenBuffer();
                int frames = count / 8;
                if (m_Float)
                {
                    fixed (byte* p = data)
                        m_Al.BufferData(buffer, (BufferFormat)FormatStereoFloat32, p, count, AudioEngine.SampleRate);
                }
                else
                {
                    // 16 bit stereo for a library without the float extension
                    int samples = frames * 2;
                    if (m_Short.Length < samples)
                        m_Short = new short[samples];
                    for (int i = 0; i < samples; i++)
                    {
                        float v = BitConverter.ToSingle(data, i * 4);
                        m_Short[i] = (short)(Math.Max(-1f, Math.Min(1f, v)) * 32767f);
                    }
                    fixed (short* p = m_Short)
                        m_Al.BufferData(buffer, BufferFormat.Stereo16, p, samples * 2, AudioEngine.SampleRate);
                }
                m_Al.SourceQueueBuffers(m_Source, 1, &buffer);
                m_Queued.Enqueue(new KeyValuePair<uint, int>(buffer, frames));
                m_QueuedFrames += frames;
                // starts the source, and starts it again after an underrun
                m_Al.GetSourceProperty(m_Source, GetSourceInteger.SourceState, out int state);
                if ((SourceState)state != SourceState.Playing)
                    m_Al.SourcePlay(m_Source);
                if (m_Alc.GetError(m_Device) != ContextError.NoError)
                    Failed = true;
            }

            public override void Dispose()
            {
                if (m_Disposed)
                    return;
                m_Disposed = true;
                try
                {
                    Current();
                    m_Al.SourceStop(m_Source);
                    m_Al.SetSourceProperty(m_Source, SourceInteger.Buffer, 0);
                    foreach (KeyValuePair<uint, int> item in m_Queued)
                        m_Al.DeleteBuffer(item.Key);
                    foreach (uint buffer in m_Free)
                        m_Al.DeleteBuffer(buffer);
                    m_Queued.Clear();
                    m_Free.Clear();
                    m_Al.DeleteSource(m_Source);
                    m_Alc.MakeContextCurrent(null);
                    m_Alc.DestroyContext(m_Context);
                    m_Alc.CloseDevice(m_Device);
                }
                catch (Exception)
                {
                }
                m_Context = null;
                m_Device = null;
            }
        }

        public override List<AudioDeviceInfo> ListCards()
        {
            List<AudioDeviceInfo> cards = new List<AudioDeviceInfo>();
            ALContext alc = Alc;
            EnumerateAll all;
            if (alc.TryGetExtension(null, out all))
            {
                foreach (string name in all.GetStringList(GetEnumerateAllContextStringList.AllDevicesSpecifier))
                {
                    if (!string.IsNullOrEmpty(name))
                        cards.Add(new AudioDeviceInfo { Name = name, Id = name });
                }
            }
            return cards;
        }

        public override string DefaultDeviceId()
        {
            // an empty name opens the default sound card of the system
            return string.Empty;
        }

        public override int MixChannels(string deviceId)
        {
            return 2;
        }

        public override Sink Open(string deviceId, int channels)
        {
            return new OpenAlSink(deviceId);
        }
    }
}
