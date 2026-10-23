/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 The audio engine of Apolo: mixes all playing sounds in software and sends the result to a sound card
 (WASAPI on Windows, OpenAL on Linux and macOS) or - for "do not listen on this PC" - nowhere. Replaces the BASS library.
*/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Ares.Playing.Audio
{
    internal sealed class AudioDeviceInfo
    {
        /// <summary>-1 = default device, 0 = no local output, 1 and more = sound cards.</summary>
        public int Index;
        public string Name;
        public string Id;
    }

    internal sealed class AudioEngine : IDisposable
    {
        public const int SampleRate = 48000;
        public const int BlockFrames = 960;               // 20 ms
        private const int TargetFrames = SampleRate / 12;  // about 80 ms are always mixed in advance

        public static AudioEngine Instance { get; private set; }

        // ------------------------------------------------------------------ state

        private readonly object m_ChannelLock = new object();
        private readonly List<AudioChannel> m_Channels = new List<AudioChannel>();
        private readonly Dictionary<int, AudioChannel> m_ByHandle = new Dictionary<int, AudioChannel>();
        private readonly object m_SinkLock = new object();
        private Sink m_Sink;
        private int m_DeviceIndex = -1;
        private int m_NextHandle;
        private volatile float m_Master = 1f;
        private float m_LastMaster = 1f;
        private volatile bool m_Stop;
        private readonly Thread m_MixerThread;
        private float[] m_Mix = new float[0];
        private byte[] m_Bytes = new byte[0];
        private DateTime m_LastDeviceCheck = DateTime.MinValue;
        private bool m_NoRealDevice;
        private readonly Action<string> m_Warning;

        // events (callbacks) are called on an own thread, never by the mixer
        private readonly Queue<Action> m_Events = new Queue<Action>();
        private readonly object m_EventLock = new object();
        private readonly Thread m_EventThread;

        // listeners for the mixed sound (virtual microphone, Discord, streaming)
        private readonly object m_TapLock = new object();
        private Action<byte[], int> m_Tap;
        private readonly Queue<byte[]> m_TapQueue = new Queue<byte[]>();
        private readonly Thread m_TapThread;

        public event Action OutputChanged;

        // ------------------------------------------------------------------ creation

        public static AudioEngine Create(int deviceIndex, Action<string> warning)
        {
            AudioEngine engine = new AudioEngine(deviceIndex, warning);
            Instance = engine;
            return engine;
        }

        private AudioEngine(int deviceIndex, Action<string> warning)
        {
            m_Warning = warning;
            m_DeviceIndex = deviceIndex;
            Sink sink = null;
            try
            {
                sink = OpenSink(deviceIndex);
            }
            catch (Exception)
            {
                // no usable sound card: carry on silently, a card may appear later
                m_NoRealDevice = true;
                sink = new NullSink();
                if (m_Warning != null)
                    m_Warning(AudioInit.Text("Nenhum dispositivo de áudio foi encontrado. O Apolo vai tocar sem som até que um apareça.",
                        "No audio output device was found. Apolo plays without sound until one appears.",
                        "Es wurde kein Audioausgabegerät gefunden. Apolo spielt ohne Ton, bis eines verfügbar ist."));
            }
            m_Sink = sink;

            m_EventThread = new Thread(EventLoop);
            m_EventThread.IsBackground = true;
            m_EventThread.Name = "Apolo audio events";
            m_EventThread.Start();

            m_TapThread = new Thread(TapLoop);
            m_TapThread.IsBackground = true;
            m_TapThread.Name = "Apolo audio tap";
            m_TapThread.Start();

            m_MixerThread = new Thread(MixerLoop);
            m_MixerThread.IsBackground = true;
            m_MixerThread.Priority = ThreadPriority.AboveNormal;
            m_MixerThread.Name = "Apolo audio mixer";
            m_MixerThread.Start();
        }

        public void Dispose()
        {
            m_Stop = true;
            try { m_MixerThread.Join(1000); } catch (Exception) { }
            AudioChannel[] channels;
            lock (m_ChannelLock)
            {
                channels = m_Channels.ToArray();
                m_Channels.Clear();
                m_ByHandle.Clear();
            }
            foreach (AudioChannel channel in channels)
            {
                try { channel.Stop(); } catch (Exception) { }
            }
            lock (m_SinkLock)
            {
                if (m_Sink != null)
                    m_Sink.Dispose();
                m_Sink = null;
            }
            lock (m_EventLock)
            {
                Monitor.PulseAll(m_EventLock);
            }
            lock (m_TapLock)
            {
                Monitor.PulseAll(m_TapLock);
            }
            if (Instance == this)
                Instance = null;
        }

        // ------------------------------------------------------------------ properties

        private readonly LevelCompressor m_Leveler = new LevelCompressor();

        public float MasterVolume
        {
            get { return m_Master; }
            set { m_Master = Math.Max(0f, Math.Min(1f, value)); }
        }

        /// <summary>Number of speakers of the output (2, or 6 / 8 for surround sound cards).</summary>
        public int Speakers
        {
            get
            {
                lock (m_SinkLock)
                {
                    return m_Sink != null ? m_Sink.Channels : 2;
                }
            }
        }

        public int DeviceIndex
        {
            get { return m_DeviceIndex; }
        }

        /// <summary>False if the sound is not played on this PC (but is still available for Discord, VDO.Ninja, ...).</summary>
        public bool LocalOutput
        {
            get { return m_DeviceIndex != 0; }
        }

        public int NextHandle()
        {
            return Interlocked.Increment(ref m_NextHandle);
        }

        // ------------------------------------------------------------------ channels

        public void AddChannel(AudioChannel channel)
        {
            lock (m_ChannelLock)
            {
                if (!m_ByHandle.ContainsKey(channel.Handle))
                {
                    m_Channels.Add(channel);
                    m_ByHandle[channel.Handle] = channel;
                }
            }
        }

        public void RemoveChannel(AudioChannel channel)
        {
            lock (m_ChannelLock)
            {
                m_Channels.Remove(channel);
                m_ByHandle.Remove(channel.Handle);
            }
        }

        public AudioChannel GetChannel(int handle)
        {
            lock (m_ChannelLock)
            {
                AudioChannel channel;
                return m_ByHandle.TryGetValue(handle, out channel) ? channel : null;
            }
        }

        public void Post(Action action)
        {
            lock (m_EventLock)
            {
                m_Events.Enqueue(action);
                Monitor.Pulse(m_EventLock);
            }
        }

        private void EventLoop()
        {
            while (!m_Stop)
            {
                Action action = null;
                lock (m_EventLock)
                {
                    while (m_Events.Count == 0 && !m_Stop)
                        Monitor.Wait(m_EventLock);
                    if (m_Events.Count > 0)
                        action = m_Events.Dequeue();
                }
                if (action == null)
                    continue;
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Audio event: " + ex.Message);
                }
            }
        }

        // ------------------------------------------------------------------ the tap (mixed sound as 48 kHz, stereo, 16 bit)

        public void AddTap(Action<byte[], int> handler)
        {
            lock (m_TapLock)
            {
                m_Tap = (Action<byte[], int>)Delegate.Combine(m_Tap, handler);
            }
        }

        public void RemoveTap(Action<byte[], int> handler)
        {
            lock (m_TapLock)
            {
                m_Tap = (Action<byte[], int>)Delegate.Remove(m_Tap, handler);
            }
        }

        private void TapLoop()
        {
            while (!m_Stop)
            {
                byte[] block = null;
                Action<byte[], int> handler = null;
                lock (m_TapLock)
                {
                    while (m_TapQueue.Count == 0 && !m_Stop)
                        Monitor.Wait(m_TapLock);
                    if (m_TapQueue.Count > 0)
                        block = m_TapQueue.Dequeue();
                    handler = m_Tap;
                }
                if (block == null || handler == null)
                    continue;
                try
                {
                    handler(block, block.Length);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Audio tap: " + ex.Message);
                }
            }
        }

        private void PushTap(byte[] block)
        {
            lock (m_TapLock)
            {
                if (m_Tap == null)
                {
                    m_TapQueue.Clear();
                    return;
                }
                while (m_TapQueue.Count > 100)
                    m_TapQueue.Dequeue();
                m_TapQueue.Enqueue(block);
                Monitor.Pulse(m_TapLock);
            }
        }

        private bool HasTap
        {
            get { lock (m_TapLock) { return m_Tap != null; } }
        }

        // ------------------------------------------------------------------ mixing

        private void MixerLoop()
        {
            while (!m_Stop)
            {
                try
                {
                    Sink sink;
                    int buffered;
                    lock (m_SinkLock)
                    {
                        sink = m_Sink;
                        buffered = sink != null ? sink.BufferedFrames : int.MaxValue;
                    }
                    if ((DateTime.UtcNow - m_LastDeviceCheck).TotalMilliseconds > 2000)
                    {
                        m_LastDeviceCheck = DateTime.UtcNow;
                        CheckDevice();
                    }
                    if (sink == null || buffered >= TargetFrames)
                    {
                        Thread.Sleep(4);
                        continue;
                    }
                    MixBlock(sink);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Audio mixer: " + ex.Message);
                    Thread.Sleep(10);
                }
            }
        }

        private void MixBlock(Sink sink)
        {
            int n = Math.Max(2, sink.Channels);
            if (m_Mix.Length != BlockFrames * n)
            {
                m_Mix = new float[BlockFrames * n];
                m_Bytes = new byte[BlockFrames * n * 4];
            }
            Array.Clear(m_Mix, 0, m_Mix.Length);
            AudioChannel[] channels;
            lock (m_ChannelLock)
            {
                channels = m_Channels.ToArray();
            }
            List<AudioChannel> finished = null;
            foreach (AudioChannel channel in channels)
            {
                bool alive;
                try
                {
                    alive = channel.Mix(m_Mix, BlockFrames, n);
                }
                catch (Exception)
                {
                    alive = false;
                }
                if (!alive)
                {
                    if (finished == null)
                        finished = new List<AudioChannel>();
                    finished.Add(channel);
                }
            }
            if (finished != null)
            {
                lock (m_ChannelLock)
                {
                    foreach (AudioChannel channel in finished)
                    {
                        m_Channels.Remove(channel);
                        m_ByHandle.Remove(channel.Handle);
                    }
                }
            }
            // leveler: brings quiet and loud material to about the same loudness (and limits the peaks)
            m_Leveler.Process(m_Mix, BlockFrames, n, SampleRate, Ares.Playing.AudioLeveler.Enabled);
            // master volume (smooth over the block), and keep the signal in range
            float v0 = m_LastMaster;
            float v1 = m_Master;
            m_LastMaster = v1;
            int total = BlockFrames * n;
            for (int f = 0; f < BlockFrames; f++)
            {
                float v = v0 + (v1 - v0) * (f + 1) / BlockFrames;
                int o = f * n;
                for (int c = 0; c < n; c++)
                {
                    float s = m_Mix[o + c] * v;
                    if (s > 1f) s = 1f; else if (s < -1f) s = -1f;
                    m_Mix[o + c] = s;
                }
            }
            if (HasTap)
            {
                byte[] pcm = new byte[BlockFrames * 4];
                for (int f = 0; f < BlockFrames; f++)
                {
                    short l = (short)(m_Mix[f * n] * 32767f);
                    short r = (short)(m_Mix[f * n + 1] * 32767f);
                    pcm[f * 4] = (byte)l;
                    pcm[f * 4 + 1] = (byte)(l >> 8);
                    pcm[f * 4 + 2] = (byte)r;
                    pcm[f * 4 + 3] = (byte)(r >> 8);
                }
                PushTap(pcm);
            }
            Buffer.BlockCopy(m_Mix, 0, m_Bytes, 0, total * 4);
            lock (m_SinkLock)
            {
                if (m_Sink == sink)
                    sink.Write(m_Bytes, total * 4);
            }
        }

        // ------------------------------------------------------------------ devices

        /// <summary>The sound cards (index 1 and more), sorted by name.</summary>
        public static List<AudioDeviceInfo> ListCards()
        {
            List<AudioDeviceInfo> result = new List<AudioDeviceInfo>();
            try
            {
                List<AudioDeviceInfo> cards = AudioBackend.Current.ListCards();
                cards.Sort(delegate (AudioDeviceInfo x, AudioDeviceInfo y)
                {
                    int c = string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);
                    return c != 0 ? c : string.CompareOrdinal(x.Id, y.Id);
                });
                for (int i = 0; i < cards.Count; i++)
                {
                    cards[i].Index = i + 1;
                    result.Add(cards[i]);
                }
            }
            catch (Exception)
            {
            }
            return result;
        }

        private static Sink OpenSink(int index)
        {
            if (index == 0)
                return new NullSink();
            AudioBackend backend = AudioBackend.Current;
            string id;
            if (index < 0)
            {
                id = backend.DefaultDeviceId();
            }
            else
            {
                id = null;
                foreach (AudioDeviceInfo card in ListCards())
                {
                    if (card.Index == index)
                        id = card.Id;
                }
            }
            if (id == null)
                throw new AudioInitException(StringResources.BassDeviceInvalid);
            int channels = backend.MixChannels(id);
            if (channels > 2)
            {
                try
                {
                    return backend.Open(id, channels);
                }
                catch (Exception)
                {
                    // surround output not possible: stereo
                }
            }
            try
            {
                return backend.Open(id, 2);
            }
            catch (Exception ex)
            {
                throw new AudioInitException(StringResources.BassDeviceFormat, ex);
            }
        }

        /// <summary>Switches to another output without interrupting the sounds. Throws if this isn't possible (nothing changes then).</summary>
        public void SwitchDevice(int index)
        {
            Sink sink = OpenSink(index);
            Sink old;
            lock (m_SinkLock)
            {
                old = m_Sink;
                m_Sink = sink;
                m_DeviceIndex = index;
                m_NoRealDevice = false;
            }
            if (old != null)
                old.Dispose();
            Action changed = OutputChanged;
            if (changed != null)
                changed();
        }

        // called by the mixer thread now and then: lost devices and changes of the default device
        private void CheckDevice()
        {
            try
            {
                Sink current;
                lock (m_SinkLock) { current = m_Sink; }
                if (current == null || m_DeviceIndex == 0)
                    return;
                bool reopen = false;
                if (m_NoRealDevice || current.Failed)
                    reopen = true;
                else if (m_DeviceIndex < 0)
                {
                    string id = AudioBackend.Current.DefaultDeviceId();
                    if (id != null && current.DeviceId != null && id != current.DeviceId)
                        reopen = true;
                }
                if (!reopen)
                    return;
                Sink sink = null;
                try
                {
                    sink = OpenSink(m_DeviceIndex);
                }
                catch (Exception)
                {
                    try { sink = OpenSink(-1); } catch (Exception) { }
                }
                if (sink == null)
                {
                    if (current.Failed)
                    {
                        // no device at all: keep time running
                        Sink nullSink = new NullSink();
                        lock (m_SinkLock) { m_Sink = nullSink; }
                        m_NoRealDevice = true;
                        current.Dispose();
                    }
                    return;
                }
                Sink old;
                lock (m_SinkLock)
                {
                    if (m_Sink != current)
                    {
                        // the user switched the device meanwhile
                        old = sink;
                    }
                    else
                    {
                        old = m_Sink;
                        m_Sink = sink;
                        m_NoRealDevice = false;
                    }
                }
                if (old != null)
                    old.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Audio device check: " + ex.Message);
            }
        }
    }
}
