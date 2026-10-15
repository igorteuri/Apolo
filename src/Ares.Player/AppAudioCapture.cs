/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.
*/
using System;

namespace Ares.Player
{
    /// <summary>
    /// The sound of Apolo for Discord / the virtual microphone, as 48 kHz, stereo, 16 bit PCM.
    /// When Apolo plays on this PC the sound of the whole program (including the web pages, like YouTube) is captured
    /// by Windows (process loopback). When Apolo is set to "don't listen on this PC" - or when Windows can't capture a
    /// single program - the sound which the Apolo audio engine mixes is used (web pages are not part of it then).
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

        private readonly int m_ProcessId;
        private readonly object m_Lock = new object();
        private ProcessLoopbackCapture m_Loopback;
        private bool m_EngineListening;
        private bool m_Started;
        private bool m_Disposed;

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
                System.Threading.Interlocked.Increment(ref s_Active);
            }
            Ares.Playing.AudioInit.OutputChanged += OnOutputChanged;
            Apply();
            lock (m_Lock)
            {
                if (m_Loopback == null && !m_EngineListening)
                    return "no audio engine";
            }
            return null;
        }

        public void Stop()
        {
            Ares.Playing.AudioInit.OutputChanged -= OnOutputChanged;
            ProcessLoopbackCapture loopback;
            bool listening;
            lock (m_Lock)
            {
                if (m_Started)
                    System.Threading.Interlocked.Decrement(ref s_Active);
                m_Started = false;
                loopback = m_Loopback;
                m_Loopback = null;
                listening = m_EngineListening;
                m_EngineListening = false;
            }
            if (listening)
                Ares.Playing.AudioInit.RemovePcmListener(OnEngineData);
            if (loopback != null)
                loopback.Dispose();
        }

        public void Dispose()
        {
            lock (m_Lock) { m_Disposed = true; }
            Stop();
        }

        private void OnOutputChanged()
        {
            System.Threading.ThreadPool.QueueUserWorkItem(delegate { Apply(); });
        }

        private void OnEngineData(byte[] buffer, int count)
        {
            Action<byte[], int> handler = DataAvailable;
            if (handler != null)
                handler(buffer, count);
        }

        private void OnLoopbackData(byte[] buffer, int count)
        {
            Action<byte[], int> handler = DataAvailable;
            if (handler != null)
                handler(buffer, count);
        }

        // chooses where the sound comes from, depending on whether Apolo plays on this PC
        private void Apply()
        {
            lock (m_Lock)
            {
                if (!m_Started || m_Disposed)
                    return;
                bool wantLoopback = Ares.Playing.AudioInit.LocalOutputEnabled;
                if (wantLoopback)
                {
                    if (m_Loopback == null)
                    {
                        ProcessLoopbackCapture loopback = new ProcessLoopbackCapture(m_ProcessId);
                        loopback.DataAvailable += OnLoopbackData;
                        string error = loopback.Start();
                        if (error == null)
                        {
                            m_Loopback = loopback;
                        }
                        else
                        {
                            ProcessLoopbackCapture.Log("the sound of the program could not be captured (" + error + "): only the sound of the Apolo engine is sent, without web pages");
                            loopback.Dispose();
                        }
                    }
                    if (m_Loopback != null)
                    {
                        if (m_EngineListening)
                        {
                            Ares.Playing.AudioInit.RemovePcmListener(OnEngineData);
                            m_EngineListening = false;
                        }
                        return;
                    }
                }
                else if (m_Loopback != null)
                {
                    ProcessLoopbackCapture old = m_Loopback;
                    m_Loopback = null;
                    old.Dispose();
                }
                if (!m_EngineListening)
                {
                    m_EngineListening = Ares.Playing.AudioInit.AddPcmListener(OnEngineData);
                }
            }
        }
    }
}
