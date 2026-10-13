/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 Decoders of the audio engine. Uses NAudio (MIT), NLayer (MP3), NVorbis (Ogg Vorbis) and Concentus (Opus);
 everything else (AAC, M4A, WMA, FLAC, ...) is decoded by Windows (Media Foundation).
*/
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace Ares.Playing.Audio
{
    /// <summary>A WaveStream of NAudio as audio source (wav, and everything Media Foundation can decode).</summary>
    internal sealed class WaveStreamSource : IAudioSource
    {
        private readonly NAudio.Wave.WaveStream m_Stream;
        private readonly NAudio.Wave.ISampleProvider m_Samples;

        public WaveStreamSource(NAudio.Wave.WaveStream stream)
        {
            m_Stream = stream;
            m_Samples = NAudio.Wave.WaveExtensionMethods.ToSampleProvider(stream);
        }

        public int Channels { get { return m_Samples.WaveFormat.Channels; } }
        public int SampleRate { get { return m_Samples.WaveFormat.SampleRate; } }
        public double Length { get { return m_Stream.TotalTime.TotalSeconds; } }
        public double Position { get { return m_Stream.CurrentTime.TotalSeconds; } }
        public bool CanSeek { get { return m_Stream.CanSeek; } }

        public void Seek(double seconds)
        {
            m_Stream.CurrentTime = TimeSpan.FromSeconds(Math.Max(0, seconds));
        }

        public int Read(float[] buffer, int offset, int count)
        {
            return m_Samples.Read(buffer, offset, count);
        }

        public void Dispose()
        {
            m_Stream.Dispose();
        }
    }

    /// <summary>MP3 (NLayer).</summary>
    internal sealed class MpegSource : IAudioSource
    {
        private readonly NLayer.MpegFile m_File;
        private readonly bool m_Seekable;
        private double m_Length;
        private long m_Frames;

        public MpegSource(string path)
        {
            m_File = new NLayer.MpegFile(path);
            m_Seekable = true;
            try { m_Length = m_File.Duration.TotalSeconds; }
            catch (Exception) { m_Length = 0; }
        }

        public MpegSource(Stream stream)
        {
            m_File = new NLayer.MpegFile(stream, true);
            m_Seekable = false;
            m_Length = 0;
        }

        public int Channels { get { return m_File.Channels; } }
        public int SampleRate { get { return m_File.SampleRate; } }
        public double Length { get { return m_Length; } }
        public double Position { get { return (double)m_Frames / Math.Max(1, m_File.SampleRate); } }
        public bool CanSeek { get { return m_Seekable; } }

        public void Seek(double seconds)
        {
            m_File.Time = TimeSpan.FromSeconds(Math.Max(0, seconds));
            m_Frames = (long)(Math.Max(0, seconds) * m_File.SampleRate);
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int got = m_File.ReadSamples(buffer, offset, count);
            if (got > 0)
                m_Frames += got / Math.Max(1, m_File.Channels);
            return got;
        }

        public void Dispose()
        {
            m_File.Dispose();
        }
    }

    /// <summary>Ogg Vorbis (NVorbis).</summary>
    internal sealed class VorbisSource : IAudioSource
    {
        private readonly NVorbis.VorbisReader m_Reader;
        private readonly bool m_Seekable;
        private long m_Frames;

        public VorbisSource(string path)
        {
            m_Reader = new NVorbis.VorbisReader(path);
            m_Seekable = true;
        }

        public VorbisSource(Stream stream)
        {
            m_Reader = new NVorbis.VorbisReader(stream, true);
            m_Seekable = false;
        }

        public int Channels { get { return m_Reader.Channels; } }
        public int SampleRate { get { return m_Reader.SampleRate; } }

        public double Length
        {
            get
            {
                if (!m_Seekable)
                    return 0;
                try { return m_Reader.TotalTime.TotalSeconds; }
                catch (Exception) { return 0; }
            }
        }

        public double Position { get { return (double)m_Frames / Math.Max(1, m_Reader.SampleRate); } }
        public bool CanSeek { get { return m_Seekable; } }

        public void Seek(double seconds)
        {
            m_Reader.TimePosition = TimeSpan.FromSeconds(Math.Max(0, seconds));
            m_Frames = (long)(Math.Max(0, seconds) * m_Reader.SampleRate);
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int got = m_Reader.ReadSamples(buffer, offset, count);
            if (got > 0)
                m_Frames += got / Math.Max(1, m_Reader.Channels);
            return got;
        }

        public void Dispose()
        {
            m_Reader.Dispose();
        }
    }

    /// <summary>Opus in an Ogg file (Concentus). Always delivers stereo at 48 kHz.</summary>
    internal sealed class OpusSource : IAudioSource
    {
        private readonly string m_Path;
        private FileStream m_File;
        private Concentus.Structs.OpusDecoder m_Decoder;
        private Concentus.Oggfile.OpusOggReadStream m_Reader;
        private float[] m_Pending = new float[0];
        private int m_PendingStart, m_PendingCount;
        private long m_Frames;
        private double m_Length;

        public OpusSource(string path)
        {
            m_Path = path;
            m_Length = ReadLength(path);
            try
            {
                Open();
                // the first packet tells whether it really is Opus
                if (!Fill())
                    throw new InvalidDataException("no opus data");
            }
            catch (Exception)
            {
                Close();
                throw;
            }
        }

        private void Open()
        {
            Close();
            m_File = new FileStream(m_Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            m_Decoder = Concentus.Structs.OpusDecoder.Create(48000, 2);
            m_Reader = new Concentus.Oggfile.OpusOggReadStream(m_Decoder, m_File);
            m_PendingStart = 0;
            m_PendingCount = 0;
            m_Frames = 0;
        }

        private void Close()
        {
            if (m_File != null)
            {
                m_File.Dispose();
                m_File = null;
            }
            m_Reader = null;
            m_Decoder = null;
        }

        // length from the granule position of the last Ogg page (48000 Hz, minus the pre-skip)
        private static double ReadLength(string path)
        {
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    byte[] head = new byte[Math.Min(512, (int)fs.Length)];
                    fs.Read(head, 0, head.Length);
                    int preSkip = 0;
                    for (int i = 0; i + 12 < head.Length; i++)
                    {
                        if (head[i] == 'O' && head[i + 1] == 'p' && head[i + 2] == 'u' && head[i + 3] == 's' && head[i + 4] == 'H')
                        {
                            preSkip = head[i + 10] | (head[i + 11] << 8);
                            break;
                        }
                    }
                    long tail = Math.Min(fs.Length, 65536);
                    fs.Seek(-tail, SeekOrigin.End);
                    byte[] data = new byte[tail];
                    int read = fs.Read(data, 0, data.Length);
                    for (int i = read - 14; i >= 0; i--)
                    {
                        if (data[i] == 'O' && data[i + 1] == 'g' && data[i + 2] == 'g' && data[i + 3] == 'S')
                        {
                            long granule = BitConverter.ToInt64(data, i + 6);
                            if (granule > 0)
                                return Math.Max(0, (granule - preSkip)) / 48000.0;
                            break;
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
            return 0;
        }

        private bool Fill()
        {
            while (m_PendingCount == 0)
            {
                if (m_Reader == null || !m_Reader.HasNextPacket)
                    return false;
                short[] packet = m_Reader.DecodeNextPacket();
                if (packet == null || packet.Length == 0)
                    continue;
                if (m_Pending.Length < packet.Length)
                    m_Pending = new float[packet.Length];
                for (int i = 0; i < packet.Length; i++)
                    m_Pending[i] = packet[i] / 32768f;
                m_PendingStart = 0;
                m_PendingCount = packet.Length;
            }
            return true;
        }

        public int Channels { get { return 2; } }
        public int SampleRate { get { return 48000; } }
        public double Length { get { return m_Length; } }
        public double Position { get { return m_Frames / 48000.0; } }
        public bool CanSeek { get { return true; } }

        public void Seek(double seconds)
        {
            // Opus decodes much faster than real time: start again and skip
            Open();
            long skip = (long)(Math.Max(0, seconds) * 48000);
            float[] discard = new float[8192];
            while (skip > 0)
            {
                int want = (int)Math.Min(skip, discard.Length / 2) * 2;
                int got = Read(discard, 0, want);
                if (got <= 0)
                    break;
                skip -= got / 2;
            }
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int total = 0;
            while (total < count)
            {
                if (m_PendingCount == 0 && !Fill())
                    break;
                int n = Math.Min(count - total, m_PendingCount);
                Array.Copy(m_Pending, m_PendingStart, buffer, offset + total, n);
                m_PendingStart += n;
                m_PendingCount -= n;
                total += n;
            }
            m_Frames += total / 2;
            return total;
        }

        public void Dispose()
        {
            Close();
        }
    }

    /// <summary>The stream of a web radio without the title information (ICY) which is mixed into the data.</summary>
    internal sealed class IcyStream : Stream
    {
        private readonly Stream m_Net;
        private readonly int m_MetaInt;
        private int m_Remaining;

        public IcyStream(Stream net, int metaInt)
        {
            m_Net = net;
            m_MetaInt = metaInt;
            m_Remaining = metaInt;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (m_MetaInt <= 0)
                return m_Net.Read(buffer, offset, count);
            if (m_Remaining == 0)
            {
                int lengthByte = m_Net.ReadByte();
                if (lengthByte < 0)
                    return 0;
                int skip = lengthByte * 16;
                byte[] meta = new byte[skip];
                int done = 0;
                while (done < skip)
                {
                    int n = m_Net.Read(meta, done, skip - done);
                    if (n <= 0)
                        return 0;
                    done += n;
                }
                m_Remaining = m_MetaInt;
            }
            int got = m_Net.Read(buffer, offset, Math.Min(count, m_Remaining));
            if (got > 0)
                m_Remaining -= got;
            return got;
        }

        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return false; } }
        public override bool CanWrite { get { return false; } }
        public override long Length { get { throw new NotSupportedException(); } }
        public override long Position { get { throw new NotSupportedException(); } set { throw new NotSupportedException(); } }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
        public override void SetLength(long value) { throw new NotSupportedException(); }
        public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                m_Net.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>Decodes in a thread of its own so that a slow network never stops the other sounds.</summary>
    internal sealed class ThreadedSource : IAudioSource
    {
        private readonly IAudioSource m_Inner;
        private readonly Thread m_Thread;
        private readonly object m_Lock = new object();
        private readonly float[] m_Ring;
        private int m_Head, m_Count;           // in samples
        private long m_Delivered;              // frames given to the reader
        private volatile bool m_Ended;
        private volatile bool m_Stop;
        private readonly int m_Channels;
        private readonly int m_Rate;

        public ThreadedSource(IAudioSource inner)
        {
            m_Inner = inner;
            m_Channels = Math.Max(1, inner.Channels);
            m_Rate = inner.SampleRate;
            m_Ring = new float[m_Rate * m_Channels * 6];
            m_Thread = new Thread(Run);
            m_Thread.IsBackground = true;
            m_Thread.Name = "Apolo radio decoder";
            m_Thread.Start();
        }

        private void Run()
        {
            float[] chunk = new float[4096 * m_Channels];
            try
            {
                while (!m_Stop)
                {
                    bool full;
                    lock (m_Lock)
                    {
                        full = m_Ring.Length - m_Count < chunk.Length;
                    }
                    if (full)
                    {
                        Thread.Sleep(20);
                        continue;
                    }
                    int got = m_Inner.Read(chunk, 0, chunk.Length);
                    if (got < 0)
                    {
                        Thread.Sleep(10);
                        continue;
                    }
                    if (got == 0)
                        break;
                    lock (m_Lock)
                    {
                        for (int i = 0; i < got; i++)
                            m_Ring[(m_Head + m_Count + i) % m_Ring.Length] = chunk[i];
                        m_Count += got;
                    }
                }
            }
            catch (Exception)
            {
            }
            m_Ended = true;
        }

        public int Channels { get { return m_Channels; } }
        public int SampleRate { get { return m_Rate; } }
        public double Length { get { return 0; } }
        public double Position { get { return (double)Interlocked.Read(ref m_Delivered) / m_Rate; } }
        public bool CanSeek { get { return false; } }

        public void Seek(double seconds)
        {
            throw new NotSupportedException();
        }

        public int Read(float[] buffer, int offset, int count)
        {
            lock (m_Lock)
            {
                if (m_Count == 0)
                    return m_Ended ? 0 : -1;
                int n = Math.Min(count, m_Count);
                for (int i = 0; i < n; i++)
                    buffer[offset + i] = m_Ring[(m_Head + i) % m_Ring.Length];
                m_Head = (m_Head + n) % m_Ring.Length;
                m_Count -= n;
                Interlocked.Add(ref m_Delivered, n / m_Channels);
                return n;
            }
        }

        public void Dispose()
        {
            m_Stop = true;
            m_Thread.Join(1500);
            try { m_Inner.Dispose(); } catch (Exception) { }
        }
    }

    /// <summary>Opens files and web radios.</summary>
    internal static class AudioSources
    {
        public static IAudioSource OpenFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                AudioErrors.Last = AudioError.FileOpen;
                return null;
            }
            string ext = Path.GetExtension(path).ToLowerInvariant();
            List<Func<IAudioSource>> attempts = new List<Func<IAudioSource>>();
            Func<IAudioSource> mpeg = () => new MpegSource(path);
            Func<IAudioSource> vorbis = () => new VorbisSource(path);
            Func<IAudioSource> opus = () => new OpusSource(path);
            Func<IAudioSource> wav = () => new WaveStreamSource(new NAudio.Wave.WaveFileReader(path));
            Func<IAudioSource> media = () => new WaveStreamSource(new NAudio.Wave.MediaFoundationReader(path));
            switch (ext)
            {
                case ".mp3":
                case ".mp2":
                case ".mp1":
                    attempts.AddRange(new[] { mpeg, media });
                    break;
                case ".ogg":
                case ".oga":
                    attempts.AddRange(new[] { vorbis, opus, media });
                    break;
                case ".opus":
                    attempts.AddRange(new[] { opus, vorbis, media });
                    break;
                case ".wav":
                case ".wave":
                    attempts.AddRange(new[] { wav, media });
                    break;
                default:
                    attempts.AddRange(new[] { media, mpeg, vorbis, opus, wav });
                    break;
            }
            foreach (Func<IAudioSource> attempt in attempts)
            {
                try
                {
                    IAudioSource source = attempt();
                    if (source != null && source.SampleRate > 0 && source.Channels > 0)
                        return source;
                    if (source != null)
                        source.Dispose();
                }
                catch (Exception)
                {
                }
            }
            AudioErrors.Last = AudioError.Format;
            return null;
        }

        // ---------------------------------------------------------------- web radio

        public static IAudioSource OpenRadio(string url)
        {
            try
            {
                for (int depth = 0; depth < 3; depth++)
                {
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                    request.UserAgent = "Apolo";
                    request.Headers.Add("Icy-MetaData", "1");
                    request.Timeout = 15000;
                    request.ReadWriteTimeout = 30000;
                    request.AllowAutoRedirect = true;
                    HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                    string type = (response.ContentType ?? "").ToLowerInvariant();
                    string lowerUrl = url.ToLowerInvariant();
                    bool playlist = type.Contains("scpls") || type.Contains("mpegurl") || type.Contains("x-pls")
                        || lowerUrl.EndsWith(".pls") || lowerUrl.EndsWith(".m3u");
                    if (playlist)
                    {
                        string next = null;
                        using (response)
                        using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                        {
                            char[] buf = new char[65536];
                            int n = reader.Read(buf, 0, buf.Length);
                            next = FirstUrlOfPlaylist(new string(buf, 0, Math.Max(0, n)));
                        }
                        if (string.IsNullOrEmpty(next))
                        {
                            AudioErrors.Last = AudioError.Format;
                            return null;
                        }
                        url = next;
                        continue;
                    }
                    int metaInt = 0;
                    string metaHeader = response.Headers["icy-metaint"];
                    if (!string.IsNullOrEmpty(metaHeader))
                        int.TryParse(metaHeader, out metaInt);
                    Stream net = new IcyStream(response.GetResponseStream(), metaInt);
                    IAudioSource inner = null;
                    if (type.Contains("ogg") || type.Contains("vorbis"))
                    {
                        inner = new VorbisSource(net);
                    }
                    else if (type.Contains("aac") || type.Contains("mp4") || type.Contains("x-m4a"))
                    {
                        net.Dispose();
                        inner = new WaveStreamSource(new NAudio.Wave.MediaFoundationReader(url));
                    }
                    else
                    {
                        inner = new MpegSource(net);
                    }
                    return new ThreadedSource(inner);
                }
            }
            catch (WebException)
            {
                AudioErrors.Last = AudioError.Network;
                return null;
            }
            catch (Exception)
            {
                AudioErrors.Last = AudioError.Format;
                return null;
            }
            AudioErrors.Last = AudioError.Network;
            return null;
        }

        private static string FirstUrlOfPlaylist(string text)
        {
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                    continue;
                if (line.StartsWith("File", StringComparison.OrdinalIgnoreCase) && line.Contains("="))
                    line = line.Substring(line.IndexOf('=') + 1).Trim();
                if (line.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    return line;
            }
            return null;
        }
    }
}
