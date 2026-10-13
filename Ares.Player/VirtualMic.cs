/*
 Apolo - a fork of Ares (Copyright (c) Jörg Ruedenauer), distributed under the GPL.
*/
using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Web.WebView2.Core;

namespace Ares.Player
{
    /// <summary>
    /// The native "virtual microphone" of Apolo: the sound which Apolo itself plays is captured
    /// (process loopback) and handed to the VDO.Ninja page, which shows it as an input device
    /// called "Apolo Audio". No virtual cable has to be installed.
    /// </summary>
    internal class VirtualMic : IDisposable
    {
        public const string DeviceName = "Apolo Audio";

        private readonly System.Windows.Forms.Control m_Ui;
        private readonly CoreWebView2 m_Core;
        private ProcessLoopbackCapture m_Capture;
        private readonly MemoryStream m_Pending = new MemoryStream();
        private readonly object m_Lock = new object();
        private bool m_Disposed;

        /// <summary>Null if the capture works, otherwise the reason.</summary>
        public string Error { get; private set; }

        public VirtualMic(System.Windows.Forms.Control ui, CoreWebView2 core)
        {
            m_Ui = ui;
            m_Core = core;
        }

        private bool m_Starting;

        /// <summary>Starts the capture without blocking the window.</summary>
        public void StartAsync()
        {
            lock (m_Lock)
            {
                if (m_Capture != null || m_Starting || m_Disposed)
                    return;
                m_Starting = true;
            }
            System.Threading.ThreadPool.QueueUserWorkItem(delegate { Start(); });
        }

        private void Start()
        {
            try
            {
                StartCore();
            }
            finally
            {
                lock (m_Lock) { m_Starting = false; }
            }
        }

        private void StartCore()
        {
            lock (m_Lock)
            {
                if (m_Capture != null || m_Disposed)
                    return;
            }
            ProcessLoopbackCapture capture = new ProcessLoopbackCapture(Process.GetCurrentProcess().Id);
            capture.DataAvailable += OnData;
            string error = capture.Start();
            if (error != null)
            {
                Error = error;
                capture.Dispose();
                Report("fail");
                return;
            }
            Error = null;
            lock (m_Lock)
            {
                if (m_Disposed)
                {
                    capture.Dispose();
                    return;
                }
                m_Capture = capture;
            }
        }

        public void Stop()
        {
            ProcessLoopbackCapture capture;
            lock (m_Lock)
            {
                capture = m_Capture;
                m_Capture = null;
                m_Pending.SetLength(0);
            }
            if (capture != null)
                capture.Dispose();
        }

        public void Dispose()
        {
            lock (m_Lock) { m_Disposed = true; }
            Stop();
        }

        // about 20 ms of sound per message (48000 Hz * 4 bytes * 0.02)
        private const int ChunkBytes = 3840;

        private void OnData(byte[] buffer, int count)
        {
            byte[] chunk = null;
            lock (m_Lock)
            {
                if (m_Capture == null)
                    return;
                m_Pending.Write(buffer, 0, count);
                if (m_Pending.Length >= ChunkBytes)
                {
                    chunk = m_Pending.ToArray();
                    m_Pending.SetLength(0);
                }
            }
            if (chunk != null)
                Post(Convert.ToBase64String(chunk));
        }

        private void Post(string text)
        {
            try
            {
                if (m_Ui.IsHandleCreated && !m_Ui.IsDisposed)
                    m_Ui.BeginInvoke((Action)(() =>
                    {
                        try { m_Core.PostWebMessageAsString(text); }
                        catch (Exception) { }
                    }));
            }
            catch (Exception)
            {
            }
        }

        private void Report(string what)
        {
            Post("!" + what);
        }
    }
}
