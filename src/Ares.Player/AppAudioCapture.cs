/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.
*/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Ares.Player
{
    /// <summary>
    /// The sound of Apolo for Discord / the virtual microphone, as 48 kHz, stereo, 16 bit PCM.
    /// Two sources are mixed:
    ///  - the sound which the Apolo audio engine plays (music, effects) - always, whatever the output device is;
    ///  - the sound of the web pages (YouTube etc.): Windows captures what the browser parts of Apolo (the WebView2
    ///    processes which belong to Apolo) play - nothing else of the computer.
    /// </summary>
    public class AppAudioCapture : IDisposable
    {
        /// <summary>Called on a capture thread with PCM data (buffer, length in bytes).</summary>
        public event Action<byte[], int> DataAvailable;

        private static int s_Active;

        /// <summary>true while the sound of Apolo is being sent somewhere (Discord, virtual microphone, VDO).</summary>
        public static bool AnyActive
        {
            get { return System.Threading.Thread.VolatileRead(ref s_Active) > 0; }
        }

        public const int MinMarginMs = 100;
        public const int MaxMarginMs = 3000;
        public const int DefaultMarginMs = 300;

        /// <summary>
        /// Safety margin (setting "AudioSendMargin"): how much web page sound is collected before it is sent. More margin means
        /// a longer delay but no stutter when the PC is busy (a YouTube tab, the encoder of VDO.Ninja).
        /// </summary>
        public static int MarginMs
        {
            get
            {
                int ms;
                if (!int.TryParse(Ares.CommonGUI.Preferences.Get("AudioSendMargin", DefaultMarginMs.ToString()), out ms))
                    ms = DefaultMarginMs;
                return Math.Max(MinMarginMs, Math.Min(MaxMarginMs, ms));
            }
        }

        private const int FrameBytes = 3840;            // 20 ms
        private const int PrimeBytes = FrameBytes * 3;  // 60 ms collected before the sound of a source is used
        private const int MaxBytes = FrameBytes * 20;   // 400 ms: more than that is dropped (the oldest part)
        private const int KeepBytes = FrameBytes * 6;

        private static int MarginFrames()
        {
            return Math.Max(2, MarginMs / 20);
        }

        // a queue of PCM data of one source
        private class Source
        {
            private readonly object m_Lock = new object();
            private readonly Queue<byte[]> m_Chunks = new Queue<byte[]>();
            private int m_Offset;
            private int m_Length;
            private bool m_Primed;
            private bool m_Flowed;
            private readonly int m_Prime, m_Reprime, m_Max, m_Keep;
            public long TotalBytes;
            public bool Reported;
            /// <summary>Times the source ran dry while sound was flowing (heard as a gap), and times the oldest sound was dropped.</summary>
            public int Underruns;
            public int Overflows;

            public Source() : this(PrimeBytes, MaxBytes, KeepBytes)
            {
            }

            // sources which arrive in bursts (the web pages) get a deeper buffer: a smooth sound is better than a short delay
            public Source(int prime, int max, int keep) : this(prime, max, keep, prime)
            {
            }

            // "reprime": how much sound is collected again after the source ran dry (less than at the start: the gap stays short)
            public Source(int prime, int max, int keep, int reprime)
            {
                m_Prime = prime;
                m_Reprime = Math.Min(prime, reprime);
                m_Max = max;
                m_Keep = keep;
            }

            public void Add(byte[] buffer, int count)
            {
                if (count <= 0)
                    return;
                byte[] copy = new byte[count];
                Buffer.BlockCopy(buffer, 0, copy, 0, count);
                lock (m_Lock)
                {
                    m_Chunks.Enqueue(copy);
                    m_Length += count;
                    TotalBytes += count;
                    if (m_Length > m_Max)
                    {
                        Overflows++;
                        while (m_Length > m_Keep && m_Chunks.Count > 0)
                        {
                            byte[] old = m_Chunks.Dequeue();
                            m_Length -= old.Length - m_Offset;
                            m_Offset = 0;
                        }
                    }
                }
            }

            // takes one frame (FrameBytes) if the source has enough sound
            public bool TakeFrame(byte[] frame)
            {
                lock (m_Lock)
                {
                    if (!m_Primed)
                    {
                        if (m_Length >= (m_Flowed ? m_Reprime : m_Prime))
                            m_Primed = true;
                        else
                            return false;
                    }
                    if (m_Length < FrameBytes)
                    {
                        m_Primed = false;
                        Underruns++;
                        return false;
                    }
                    m_Flowed = true;
                    int got = 0;
                    while (got < FrameBytes)
                    {
                        byte[] chunk = m_Chunks.Peek();
                        int n = Math.Min(chunk.Length - m_Offset, FrameBytes - got);
                        Buffer.BlockCopy(chunk, m_Offset, frame, got, n);
                        got += n;
                        m_Offset += n;
                        if (m_Offset >= chunk.Length)
                        {
                            m_Chunks.Dequeue();
                            m_Offset = 0;
                        }
                    }
                    m_Length -= FrameBytes;
                    return true;
                }
            }
        }

        private class WebSource : Source
        {
            public ProcessLoopbackCapture Capture;

            // the sound of the web pages arrives in bursts (the browser, the encoder of VDO.Ninja and the other tabs share the PC):
            // 100 ms before it starts, up to 600 ms kept, and after a gap only 40 ms are collected again
            public WebSource() : this(MarginFrames())
            {
            }

            private WebSource(int frames) : base(FrameBytes * frames, FrameBytes * (frames + 30), FrameBytes * (frames + 8), FrameBytes * Math.Max(2, frames * 2 / 3))
            {
            }
        }

        // sound which the web pages send from inside (see WebWindow: used when the PC does not play the pages)
        private static readonly List<AppAudioCapture> s_Instances = new List<AppAudioCapture>();

        /// <summary>Gives PCM data (48 kHz, stereo, 16 bit) of a web page to everything which sends the sound of Apolo.</summary>
        public static void PushPageAudio(object key, byte[] buffer, int count)
        {
            AppAudioCapture[] list;
            lock (s_Instances)
            {
                if (s_Instances.Count == 0)
                    return;
                list = s_Instances.ToArray();
            }
            foreach (AppAudioCapture c in list)
                c.AddPage(key, buffer, count);
        }

        private readonly Dictionary<object, Source> m_Pages = new Dictionary<object, Source>();

        private void AddPage(object key, byte[] buffer, int count)
        {
            Source source;
            lock (m_Lock)
            {
                if (!m_Started)
                    return;
                if (!m_Pages.TryGetValue(key, out source))
                {
                    // the pages send blocks of about 170 ms: the buffer must hold more than one block
                    int frames = Math.Max(MarginFrames(), 14);
                    source = new Source(FrameBytes * frames, FrameBytes * (frames + 30), FrameBytes * (frames + 8), FrameBytes * Math.Max(2, frames * 2 / 3));
                    m_Pages[key] = source;
                    ProcessLoopbackCapture.Log("sound of Apolo: web page sound from inside the page started");
                }
            }
            source.Add(buffer, count);
        }

        private readonly int m_ProcessId;
        private readonly object m_Lock = new object();
        private readonly Source m_Engine = new Source();
        private readonly Dictionary<int, WebSource> m_Web = new Dictionary<int, WebSource>();
        private readonly Dictionary<int, DateTime> m_Failed = new Dictionary<int, DateTime>();
        private bool m_EngineListening;
        private bool m_Started;
        private bool m_Disposed;
        private volatile bool m_Stop;
        private Thread m_MixThread;
        private Thread m_ScanThread;
        private string m_LastProcessList = "";

        public AppAudioCapture(int processId)
        {
            m_ProcessId = processId;
        }

        /// <summary>Starts the capture; returns null if it works, or the reason why not.</summary>
        public string Start()
        {
            lock (m_Lock)
            {
                if (m_Started || m_Disposed)
                    return null;
                m_Started = true;
                m_Stop = false;
                System.Threading.Interlocked.Increment(ref s_Active);
            }
            lock (s_Instances) { s_Instances.Add(this); }
            m_EngineListening = Ares.Playing.AudioInit.AddPcmListener(OnEngineData);
            ProcessLoopbackCapture.Log("sound of Apolo: started (engine " + (m_EngineListening ? "listening" : "NOT available") + ")");
            m_MixThread = new Thread(MixLoop);
            m_MixThread.IsBackground = true;
            m_MixThread.Name = "Apolo audio mix";
            m_MixThread.Start();
            m_ScanThread = new Thread(ScanLoop);
            m_ScanThread.IsBackground = true;
            m_ScanThread.Name = "Apolo web audio";
            m_ScanThread.Start();
            return null;
        }

        public void Stop()
        {
            bool wasStarted;
            lock (m_Lock)
            {
                wasStarted = m_Started;
                if (m_Started)
                    System.Threading.Interlocked.Decrement(ref s_Active);
                m_Started = false;
            }
            m_Stop = true;
            lock (s_Instances) { s_Instances.Remove(this); }
            lock (m_Lock) { m_Pages.Clear(); }
            if (m_EngineListening)
            {
                Ares.Playing.AudioInit.RemovePcmListener(OnEngineData);
                m_EngineListening = false;
            }
            if (!wasStarted)
                return;
            List<WebSource> sources;
            lock (m_Lock)
            {
                sources = new List<WebSource>(m_Web.Values);
                m_Web.Clear();
            }
            foreach (WebSource s in sources)
            {
                try { s.Capture.Dispose(); } catch (Exception swallowed) { Ares.Settings.ErrorLog.Note(swallowed); }
            }
        }

        public void Dispose()
        {
            lock (m_Lock) { m_Disposed = true; }
            Stop();
        }

        private void OnEngineData(byte[] buffer, int count)
        {
            m_Engine.Add(buffer, count);
        }

        // ---------------- mixing: one frame of 20 ms at a time ----------------

        private void MixLoop()
        {
            // Windows normally wakes a sleeping thread only every 15 ms: ask for 1 ms so that the frames come evenly
            try { timeBeginPeriod(1); } catch (Exception swallowed) { Ares.Settings.ErrorLog.Note(swallowed); }
            // an audio thread: Windows serves it before the ordinary work (the window, the browser) of the PC
            try { Thread.CurrentThread.Priority = ThreadPriority.Highest; } catch (Exception swallowed) { Ares.Settings.ErrorLog.Note(swallowed); }
            IntPtr boost = AudioThread.Boost();
            try { MixLoopCore(); }
            finally
            {
                AudioThread.Release(boost);
                try { timeEndPeriod(1); } catch (Exception swallowed) { Ares.Settings.ErrorLog.Note(swallowed); }
            }
        }

        [DllImport("winmm.dll")]
        private static extern uint timeBeginPeriod(uint period);

        [DllImport("winmm.dll")]
        private static extern uint timeEndPeriod(uint period);

        private void MixLoopCore()
        {
            Stopwatch clock = Stopwatch.StartNew();
            long next = 0;
            byte[] frame = new byte[FrameBytes];
            byte[] output = new byte[FrameBytes];
            int[] sum = new int[FrameBytes / 2];
            long lastReport = 0;
            long lastSound = long.MinValue;
            while (!m_Stop)
            {
                long now = clock.ElapsedMilliseconds;
                if (now < next)
                {
                    Thread.Sleep((int)Math.Max(1, Math.Min(5, next - now)));
                    continue;
                }
                next += 20;
                if (now - next > 200)
                    next = now + 20; // stalled for a long time: don't catch up
                Array.Clear(sum, 0, sum.Length);
                bool any = false;
                if (m_Engine.TakeFrame(frame))
                {
                    Accumulate(sum, frame);
                    any = true;
                    if (!m_Engine.Reported)
                    {
                        m_Engine.Reported = true;
                        ProcessLoopbackCapture.Log("sound of Apolo: first sound from the audio engine");
                    }
                }
                Source[] pages;
                WebSource[] web;
                lock (m_Lock)
                {
                    web = new WebSource[m_Web.Count];
                    m_Web.Values.CopyTo(web, 0);
                    pages = new Source[m_Pages.Count];
                    m_Pages.Values.CopyTo(pages, 0);
                }
                foreach (Source pg in pages)
                {
                    if (pg.TakeFrame(frame))
                    {
                        Accumulate(sum, frame);
                        any = true;
                    }
                }
                foreach (WebSource w in web)
                {
                    if (w.TakeFrame(frame))
                    {
                        Accumulate(sum, frame);
                        any = true;
                        if (!w.Reported)
                        {
                            w.Reported = true;
                            ProcessLoopbackCapture.Log("sound of Apolo: first sound from a web page");
                        }
                    }
                }
                if (any)
                    lastSound = now;
                // the stream goes on without a break for 3 s after the last sound: a short gap of one source is then a short
                // silence, and not a hole which makes the receivers (VDO.Ninja, Discord) start their buffers again
                if (any || (lastSound != long.MinValue && now - lastSound < 3000))
                {
                    for (int i = 0; i < sum.Length; ++i)
                    {
                        int v = sum[i];
                        if (v > short.MaxValue) v = short.MaxValue;
                        else if (v < short.MinValue) v = short.MinValue;
                        output[i * 2] = (byte)(v & 0xFF);
                        output[i * 2 + 1] = (byte)((v >> 8) & 0xFF);
                    }
                    Action<byte[], int> handler = DataAvailable;
                    if (handler != null)
                    {
                        try { handler(output, FrameBytes); }
                        catch (Exception swallowed) { Ares.Settings.ErrorLog.Note(swallowed); }
                    }
                }
                if (now - lastReport > 30000)
                {
                    lastReport = now;
                    int count;
                    long webBytes = 0;
                    lock (m_Lock)
                    {
                        count = m_Web.Count;
                        foreach (WebSource w in m_Web.Values)
                            webBytes += w.TotalBytes;
                    }
                    int webGaps = 0, webOverflows = 0;
                    lock (m_Lock)
                    {
                        foreach (WebSource w in m_Web.Values)
                        {
                            webGaps += w.Underruns;
                            webOverflows += w.Overflows;
                        }
                        foreach (Source pg in m_Pages.Values)
                        {
                            webGaps += pg.Underruns;
                            webOverflows += pg.Overflows;
                        }
                    }
                    ProcessLoopbackCapture.Log("sound of Apolo: engine " + (m_Engine.TotalBytes / 1024) + " KB (gaps " + m_Engine.Underruns + "), web pages ("
                        + count + " capture(s)) " + (webBytes / 1024) + " KB (gaps " + webGaps + ", dropped bursts " + webOverflows + ")");
                }
            }
        }

        private static void Accumulate(int[] sum, byte[] frame)
        {
            for (int i = 0; i < sum.Length; ++i)
                sum[i] += (short)(frame[i * 2] | (frame[i * 2 + 1] << 8));
        }

        // ---------------- the web pages (WebView2 processes of Apolo) ----------------

        private void ScanLoop()
        {
            while (!m_Stop)
            {
                try
                {
                    ScanOnce();
                }
                catch (Exception ex)
                {
                    ProcessLoopbackCapture.Log("web audio scan: " + ex.Message);
                }
                for (int i = 0; i < 20 && !m_Stop; ++i)
                    Thread.Sleep(100);
            }
        }

        private void ScanOnce()
        {
            List<int> roots = FindWebViewRoots(m_ProcessId);
            string list = string.Join(",", roots.ConvertAll<string>(delegate (int p) { return p.ToString(); }).ToArray());
            if (list != m_LastProcessList)
            {
                m_LastProcessList = list;
                ProcessLoopbackCapture.Log("web page processes of Apolo: " + (list.Length == 0 ? "none" : list));
            }
            // stop the captures of processes which are gone
            List<WebSource> gone = new List<WebSource>();
            lock (m_Lock)
            {
                List<int> remove = new List<int>();
                foreach (KeyValuePair<int, WebSource> pair in m_Web)
                {
                    if (!roots.Contains(pair.Key))
                        remove.Add(pair.Key);
                }
                foreach (int pid in remove)
                {
                    gone.Add(m_Web[pid]);
                    m_Web.Remove(pid);
                }
            }
            foreach (WebSource g in gone)
            {
                try { g.Capture.Dispose(); } catch (Exception swallowed) { Ares.Settings.ErrorLog.Note(swallowed); }
            }
            foreach (int pid in roots)
            {
                if (m_Stop)
                    return;
                lock (m_Lock)
                {
                    if (m_Web.ContainsKey(pid))
                        continue;
                    DateTime failedAt;
                    if (m_Failed.TryGetValue(pid, out failedAt) && (DateTime.UtcNow - failedAt).TotalSeconds < 15)
                        continue;
                }
                WebSource source = new WebSource();
                ProcessLoopbackCapture capture = new ProcessLoopbackCapture(pid);
                capture.DataAvailable += delegate (byte[] buffer, int count) { source.Add(buffer, count); };
                string error = capture.Start();
                if (error != null)
                {
                    ProcessLoopbackCapture.Log("web page process " + pid + " could not be captured: " + error);
                    capture.Dispose();
                    lock (m_Lock) { m_Failed[pid] = DateTime.UtcNow; }
                    continue;
                }
                source.Capture = capture;
                bool keep;
                lock (m_Lock)
                {
                    keep = !m_Stop && m_Started;
                    if (keep)
                        m_Web[pid] = source;
                }
                if (!keep)
                    capture.Dispose();
            }
        }

        // the browser processes (msedgewebview2.exe) which belong to this program: the ones whose parent is not
        // itself a browser process of this program (Windows captures the child processes with them)
        private static List<int> FindWebViewRoots(int ownerPid)
        {
            Dictionary<int, int> parent = new Dictionary<int, int>();
            Dictionary<int, string> names = new Dictionary<int, string>();
            IntPtr snap = CreateToolhelp32Snapshot(2, 0);
            if (snap == IntPtr.Zero || snap == new IntPtr(-1))
                return new List<int>();
            try
            {
                PROCESSENTRY32 entry = new PROCESSENTRY32();
                entry.dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32));
                if (Process32First(snap, ref entry))
                {
                    do
                    {
                        parent[(int)entry.th32ProcessID] = (int)entry.th32ParentProcessID;
                        names[(int)entry.th32ProcessID] = entry.szExeFile;
                    }
                    while (Process32Next(snap, ref entry));
                }
            }
            finally
            {
                CloseHandle(snap);
            }
            List<int> candidates = new List<int>();
            foreach (KeyValuePair<int, string> pair in names)
            {
                if (!pair.Value.Equals("msedgewebview2.exe", StringComparison.OrdinalIgnoreCase))
                    continue;
                int p = pair.Key;
                bool owned = false;
                for (int depth = 0; depth < 8; ++depth)
                {
                    int par;
                    if (!parent.TryGetValue(p, out par) || par == 0)
                        break;
                    if (par == ownerPid)
                    {
                        owned = true;
                        break;
                    }
                    p = par;
                }
                if (owned)
                    candidates.Add(pair.Key);
            }
            List<int> roots = new List<int>();
            foreach (int c in candidates)
            {
                int par;
                if (!parent.TryGetValue(c, out par) || !candidates.Contains(par))
                    roots.Add(c);
            }
            return roots;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct PROCESSENTRY32
        {
            public uint dwSize;
            public uint cntUsage;
            public uint th32ProcessID;
            public UIntPtr th32DefaultHeapID;
            public uint th32ModuleID;
            public uint cntThreads;
            public uint th32ParentProcessID;
            public int pcPriClassBase;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szExeFile;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool Process32First(IntPtr snapshot, ref PROCESSENTRY32 entry);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool Process32Next(IntPtr snapshot, ref PROCESSENTRY32 entry);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);
    }

    /// <summary>Puts a thread in the class "Pro Audio" of Windows (the way the sound programs do), so that it is not starved.</summary>
    internal static class AudioThread
    {
        [System.Runtime.InteropServices.DllImport("avrt.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern IntPtr AvSetMmThreadCharacteristicsW(string task, ref uint index);

        [System.Runtime.InteropServices.DllImport("avrt.dll")]
        private static extern bool AvRevertMmThreadCharacteristics(IntPtr handle);

        public static IntPtr Boost()
        {
            try
            {
                uint index = 0;
                return AvSetMmThreadCharacteristicsW("Pro Audio", ref index);
            }
            catch (Exception)
            {
                return IntPtr.Zero;
            }
        }

        public static void Release(IntPtr handle)
        {
            try
            {
                if (handle != IntPtr.Zero)
                    AvRevertMmThreadCharacteristics(handle);
            }
            catch (Exception swallowed)
            {
                Ares.Settings.ErrorLog.Note(swallowed);
            }
        }
    }
}
