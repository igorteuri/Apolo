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

        private const int FrameBytes = 3840;            // 20 ms
        private const int PrimeBytes = FrameBytes * 3;  // 60 ms collected before the sound of a source is used
        private const int MaxBytes = FrameBytes * 20;   // 400 ms: more than that is dropped (the oldest part)
        private const int KeepBytes = FrameBytes * 6;

        // a queue of PCM data of one source
        private class Source
        {
            private readonly object m_Lock = new object();
            private readonly Queue<byte[]> m_Chunks = new Queue<byte[]>();
            private int m_Offset;
            private int m_Length;
            private bool m_Primed;
            public long TotalBytes;
            public bool Reported;

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
                    if (m_Length > MaxBytes)
                    {
                        while (m_Length > KeepBytes && m_Chunks.Count > 0)
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
                        if (m_Length >= PrimeBytes)
                            m_Primed = true;
                        else
                            return false;
                    }
                    if (m_Length < FrameBytes)
                    {
                        m_Primed = false;
                        return false;
                    }
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
                try { s.Capture.Dispose(); } catch (Exception) { }
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
            Stopwatch clock = Stopwatch.StartNew();
            long next = 0;
            byte[] frame = new byte[FrameBytes];
            byte[] output = new byte[FrameBytes];
            int[] sum = new int[FrameBytes / 2];
            long lastReport = 0;
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
                WebSource[] web;
                lock (m_Lock)
                {
                    web = new WebSource[m_Web.Count];
                    m_Web.Values.CopyTo(web, 0);
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
                        catch (Exception) { }
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
                    ProcessLoopbackCapture.Log("sound of Apolo: engine " + (m_Engine.TotalBytes / 1024) + " KB, web pages (" + count + " capture(s)) " + (webBytes / 1024) + " KB");
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
                try { g.Capture.Dispose(); } catch (Exception) { }
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
}
