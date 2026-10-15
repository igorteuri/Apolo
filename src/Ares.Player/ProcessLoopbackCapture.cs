/*
 Apolo - a fork of Ares (Copyright (c) Jörg Ruedenauer), distributed under the GPL.
*/
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Ares.Player
{
    /// <summary>
    /// Captures the sound which this program (and its child processes, like the web browser parts)
    /// plays - nothing else of the computer. Windows 10 (build 20348) / Windows 11 and newer.
    /// The sound is delivered as 48 kHz, stereo, 16 bit PCM, exactly what Discord needs.
    /// Playing on the speakers is not touched.
    /// </summary>
    public class ProcessLoopbackCapture : IDisposable
    {
        public const int BytesPerFrame = 4;
        public const int SampleRate = 48000;

        /// <summary>Called on the capture thread with PCM data (buffer, length in bytes).</summary>
        public event Action<byte[], int> DataAvailable;

        private Thread m_Thread;
        private volatile bool m_Stop;
        private readonly int m_ProcessId;
        private string m_Error;
        private readonly ManualResetEvent m_Started = new ManualResetEvent(false);

        public ProcessLoopbackCapture(int processId)
        {
            m_ProcessId = processId;
        }

        /// <summary>Starts the capture; returns null if it works, or the reason why not.</summary>
        public string Start()
        {
            m_Stop = false;
            m_Error = null;
            m_Started.Reset();
            m_Thread = new Thread(CaptureLoop);
            m_Thread.IsBackground = true;
            m_Thread.Name = "Apolo process loopback";
            m_Thread.SetApartmentState(ApartmentState.MTA);
            m_Thread.Start();
            if (!m_Started.WaitOne(10000))
                return "timeout";
            return m_Error;
        }

        public void Stop()
        {
            m_Stop = true;
            Thread t = m_Thread;
            m_Thread = null;
            if (t != null && !t.Join(3000))
            {
                try { t.Abort(); } catch (Exception) { }
            }
        }

        public void Dispose()
        {
            Stop();
        }

        /// <summary>Small diagnostic log (folder of the settings, file audio-capture.log): what Windows answered at each step.</summary>
        internal static void Log(string text)
        {
            try
            {
                string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Apolo");
                System.IO.Directory.CreateDirectory(dir);
                string file = System.IO.Path.Combine(dir, "audio-capture.log");
                if (System.IO.File.Exists(file) && new System.IO.FileInfo(file).Length > 200000)
                    System.IO.File.Delete(file);
                System.IO.File.AppendAllText(file, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + text + "\r\n");
            }
            catch (Exception)
            {
            }
        }

        // ---------------- COM interop ----------------

        private static readonly Guid IID_IAudioClient = new Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2");
        private static readonly Guid IID_IAudioCaptureClient = new Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317");

        [ComImport, Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioClient
        {
            [PreserveSig] int Initialize(int shareMode, int streamFlags, long hnsBufferDuration, long hnsPeriodicity, IntPtr pFormat, IntPtr audioSessionGuid);
            [PreserveSig] int GetBufferSize(out uint numBufferFrames);
            [PreserveSig] int GetStreamLatency(out long latency);
            [PreserveSig] int GetCurrentPadding(out uint numPaddingFrames);
            [PreserveSig] int IsFormatSupported(int shareMode, IntPtr pFormat, out IntPtr closestMatch);
            [PreserveSig] int GetMixFormat(out IntPtr deviceFormat);
            [PreserveSig] int GetDevicePeriod(out long defaultPeriod, out long minimumPeriod);
            [PreserveSig] int Start();
            [PreserveSig] int Stop();
            [PreserveSig] int Reset();
            [PreserveSig] int SetEventHandle(IntPtr eventHandle);
            [PreserveSig] int GetService(ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object service);
        }

        [ComImport, Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioCaptureClient
        {
            [PreserveSig] int GetBuffer(out IntPtr data, out uint numFramesToRead, out uint flags, out ulong devicePosition, out ulong qpcPosition);
            [PreserveSig] int ReleaseBuffer(uint numFramesRead);
            [PreserveSig] int GetNextPacketSize(out uint numFramesInNextPacket);
        }

        [ComImport, Guid("72A22D78-CDE4-431D-B8CC-843A71199B6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IActivateAudioInterfaceAsyncOperation
        {
            [PreserveSig] int GetActivateResult(out int activateResult, [MarshalAs(UnmanagedType.IUnknown)] out object activatedInterface);
        }

        [ComImport, Guid("41D949AB-9862-444A-80F6-C261334DA5EB"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IActivateAudioInterfaceCompletionHandler
        {
            [PreserveSig] int ActivateCompleted(IActivateAudioInterfaceAsyncOperation activateOperation);
        }

        // marker: the handler may be called on any thread
        [ComImport, Guid("94EA2B94-E9CC-49E0-C0FF-EE64CA8F5B90"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAgileObject
        {
        }

        [DllImport("Mmdevapi.dll", ExactSpelling = true, PreserveSig = true)]
        private static extern int ActivateAudioInterfaceAsync(
            [MarshalAs(UnmanagedType.LPWStr)] string deviceInterfacePath,
            ref Guid riid,
            IntPtr activationParams,
            IActivateAudioInterfaceCompletionHandler completionHandler,
            out IActivateAudioInterfaceAsyncOperation activationOperation);

        private class CompletionHandler : IActivateAudioInterfaceCompletionHandler, IAgileObject
        {
            public readonly ManualResetEvent Done = new ManualResetEvent(false);
            public IActivateAudioInterfaceAsyncOperation Operation;

            public int ActivateCompleted(IActivateAudioInterfaceAsyncOperation activateOperation)
            {
                Operation = activateOperation;
                Done.Set();
                return 0;
            }
        }

        private const int AUDCLNT_STREAMFLAGS_LOOPBACK = 0x00020000;
        private const int AUDCLNT_STREAMFLAGS_EVENTCALLBACK = 0x00040000;
        private const int AUDCLNT_STREAMFLAGS_AUTOCONVERTPCM = unchecked((int)0x80000000);
        private const int AUDCLNT_STREAMFLAGS_SRC_DEFAULT_QUALITY = 0x08000000;
        private const uint AUDCLNT_BUFFERFLAGS_SILENT = 0x2;
        private const ushort VT_BLOB = 0x41;

        private void CaptureLoop()
        {
            IntPtr paramsPtr = IntPtr.Zero;
            IntPtr variant = IntPtr.Zero;
            IntPtr format = IntPtr.Zero;
            IAudioClient client = null;
            IAudioCaptureClient capture = null;
            AutoResetEvent audioEvent = new AutoResetEvent(false);
            bool started = false;
            try
            {
                // AUDIOCLIENT_ACTIVATION_PARAMS: type = process loopback (1), process id, mode = include the process tree (0)
                paramsPtr = Marshal.AllocHGlobal(12);
                Marshal.WriteInt32(paramsPtr, 0, 1);
                Marshal.WriteInt32(paramsPtr, 4, m_ProcessId);
                Marshal.WriteInt32(paramsPtr, 8, 0);

                // PROPVARIANT of type VT_BLOB which points to the parameters
                int variantSize = IntPtr.Size == 8 ? 24 : 16;
                variant = Marshal.AllocHGlobal(variantSize);
                for (int i = 0; i < variantSize; ++i)
                    Marshal.WriteByte(variant, i, 0);
                Marshal.WriteInt16(variant, 0, unchecked((short)VT_BLOB));
                Marshal.WriteInt32(variant, 8, 12);
                Marshal.WriteIntPtr(variant, IntPtr.Size == 8 ? 16 : 12, paramsPtr);

                Log("process loopback: start, process " + m_ProcessId + ", " + (IntPtr.Size * 8) + " bit, " + Environment.OSVersion.Version);
                CompletionHandler handler = new CompletionHandler();
                Guid iid = IID_IAudioClient;
                IActivateAudioInterfaceAsyncOperation op;
                int hr = ActivateAudioInterfaceAsync("VAD\\Process_Loopback", ref iid, variant, handler, out op);
                if (hr != 0)
                    throw new InvalidOperationException("ActivateAudioInterfaceAsync 0x" + hr.ToString("X8"));
                if (!handler.Done.WaitOne(10000))
                    throw new TimeoutException("activation");
                int activateResult;
                object unk;
                hr = handler.Operation.GetActivateResult(out activateResult, out unk);
                if (hr != 0 || activateResult != 0)
                    throw new InvalidOperationException("GetActivateResult 0x" + (hr != 0 ? hr : activateResult).ToString("X8"));
                client = (IAudioClient)unk;
                Log("process loopback: activated");

                // WAVEFORMATEX: PCM, 2 channels, 48000 Hz, 16 bit
                format = Marshal.AllocHGlobal(18);
                Marshal.WriteInt16(format, 0, 1);
                Marshal.WriteInt16(format, 2, 2);
                Marshal.WriteInt32(format, 4, SampleRate);
                Marshal.WriteInt32(format, 8, SampleRate * BytesPerFrame);
                Marshal.WriteInt16(format, 12, BytesPerFrame);
                Marshal.WriteInt16(format, 14, 16);
                Marshal.WriteInt16(format, 16, 0);

                hr = client.Initialize(0, AUDCLNT_STREAMFLAGS_LOOPBACK | AUDCLNT_STREAMFLAGS_EVENTCALLBACK
                    | AUDCLNT_STREAMFLAGS_AUTOCONVERTPCM | AUDCLNT_STREAMFLAGS_SRC_DEFAULT_QUALITY, 0, 0, format, IntPtr.Zero);
                if (hr != 0)
                    throw new InvalidOperationException("Initialize 0x" + hr.ToString("X8"));
                hr = client.SetEventHandle(audioEvent.SafeWaitHandle.DangerousGetHandle());
                if (hr != 0)
                    throw new InvalidOperationException("SetEventHandle 0x" + hr.ToString("X8"));
                Guid captureIid = IID_IAudioCaptureClient;
                object cap;
                hr = client.GetService(ref captureIid, out cap);
                if (hr != 0)
                    throw new InvalidOperationException("GetService 0x" + hr.ToString("X8"));
                capture = (IAudioCaptureClient)cap;
                hr = client.Start();
                if (hr != 0)
                    throw new InvalidOperationException("Start 0x" + hr.ToString("X8"));
                started = true;
                Log("process loopback: capturing");
                m_Started.Set();

                byte[] buffer = new byte[65536];
                while (!m_Stop)
                {
                    audioEvent.WaitOne(100);
                    uint packet;
                    while (!m_Stop && capture.GetNextPacketSize(out packet) == 0 && packet > 0)
                    {
                        IntPtr data;
                        uint frames, flags;
                        ulong devPos, qpcPos;
                        if (capture.GetBuffer(out data, out frames, out flags, out devPos, out qpcPos) != 0)
                            break;
                        int bytes = (int)frames * BytesPerFrame;
                        if (bytes > buffer.Length)
                            buffer = new byte[bytes];
                        if ((flags & AUDCLNT_BUFFERFLAGS_SILENT) != 0)
                            Array.Clear(buffer, 0, bytes);
                        else
                            Marshal.Copy(data, buffer, 0, bytes);
                        capture.ReleaseBuffer(frames);
                        Action<byte[], int> handlerCopy = DataAvailable;
                        if (handlerCopy != null && bytes > 0)
                            handlerCopy(buffer, bytes);
                    }
                }
            }
            catch (Exception ex)
            {
                Log("process loopback: " + (started ? "stopped by error: " : "FAILED: ") + ex);
                if (!started)
                    m_Error = ex.Message;
            }
            finally
            {
                try
                {
                    if (started && client != null)
                        client.Stop();
                }
                catch (Exception) { }
                if (capture != null)
                {
                    try { Marshal.ReleaseComObject(capture); } catch (Exception) { }
                }
                if (client != null)
                {
                    try { Marshal.ReleaseComObject(client); } catch (Exception) { }
                }
                if (format != IntPtr.Zero)
                    Marshal.FreeHGlobal(format);
                if (variant != IntPtr.Zero)
                    Marshal.FreeHGlobal(variant);
                if (paramsPtr != IntPtr.Zero)
                    Marshal.FreeHGlobal(paramsPtr);
                audioEvent.Close();
                m_Started.Set(); // in case of an error before the start
            }
        }
    }
}
