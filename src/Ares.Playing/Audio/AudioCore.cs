/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 The audio engine of Apolo (replaces the BASS library): basic types, resampler, time stretcher and reverb.
*/
using System;

namespace Ares.Playing.Audio
{
    /// <summary>The reasons why something in the audio engine can fail (like BASS_ErrorGetCode).</summary>
    internal enum AudioError
    {
        None,
        FileOpen,
        Format,
        Codec,
        Memory,
        Device,
        Driver,
        Handle,
        Position,
        NotAudio,
        Ended,
        Speaker,
        Network,
        Unknown
    }

    internal static class AudioErrors
    {
        [ThreadStatic]
        private static AudioError s_Last;

        public static AudioError Last
        {
            get { return s_Last; }
            set { s_Last = value; }
        }

        public static bool Fail(AudioError error)
        {
            s_Last = error;
            return false;
        }
    }

    /// <summary>Decoded audio, interleaved 32 bit float samples.</summary>
    internal interface IAudioSource : IDisposable
    {
        int Channels { get; }
        int SampleRate { get; }
        /// <summary>Length in seconds, 0 or less if unknown (web radio).</summary>
        double Length { get; }
        /// <summary>Position in seconds.</summary>
        double Position { get; }
        bool CanSeek { get; }
        void Seek(double seconds);
        /// <summary>Reads samples (not frames); returns 0 at the end and -1 if there is no data yet (network).</summary>
        int Read(float[] buffer, int offset, int count);
    }

    /// <summary>Stereo float audio, interleaved (left, right).</summary>
    internal interface IStereoStream
    {
        /// <summary>Writes up to <paramref name="frames"/> frames at the frame offset; returns the number of frames (0 = end).</summary>
        int Read(float[] buffer, int frameOffset, int frames);
        void Reset();
    }

    /// <summary>Converts the audio of a source to stereo.</summary>
    internal sealed class SourceStream : IStereoStream
    {
        private readonly IAudioSource m_Source;
        private readonly int m_Channels;
        private readonly bool m_Mono;
        private float[] m_Temp = new float[0];

        public SourceStream(IAudioSource source, bool mono)
        {
            m_Source = source;
            m_Channels = Math.Max(1, source.Channels);
            m_Mono = mono;
        }

        public int Read(float[] buffer, int frameOffset, int frames)
        {
            if (m_Channels == 2 && !m_Mono)
            {
                int got = ReadFully(buffer, frameOffset * 2, frames * 2);
                return got / 2;
            }
            int need = frames * m_Channels;
            if (m_Temp.Length < need)
                m_Temp = new float[need];
            int read = ReadFully(m_Temp, 0, need);
            int n = read / m_Channels;
            for (int i = 0; i < n; i++)
            {
                int s = i * m_Channels;
                int d = (frameOffset + i) * 2;
                if (m_Channels == 1)
                {
                    buffer[d] = m_Temp[s];
                    buffer[d + 1] = m_Temp[s];
                }
                else if (m_Mono)
                {
                    float sum = 0;
                    for (int c = 0; c < m_Channels; c++)
                        sum += m_Temp[s + c];
                    sum /= m_Channels;
                    buffer[d] = sum;
                    buffer[d + 1] = sum;
                }
                else if (m_Channels >= 6)
                {
                    // 5.1 / 7.1: front left/right, centre, (lfe ignored), rear left/right
                    float c = m_Temp[s + 2] * 0.7071f;
                    float l = m_Temp[s] + c + m_Temp[s + 4] * 0.7071f;
                    float r = m_Temp[s + 1] + c + m_Temp[s + 5] * 0.7071f;
                    if (m_Channels >= 8)
                    {
                        l += m_Temp[s + 6] * 0.7071f;
                        r += m_Temp[s + 7] * 0.7071f;
                    }
                    buffer[d] = l * 0.6f;
                    buffer[d + 1] = r * 0.6f;
                }
                else
                {
                    buffer[d] = m_Temp[s];
                    buffer[d + 1] = m_Temp[s + 1];
                }
            }
            return n;
        }

        // sources may deliver less than asked without being at the end
        private int ReadFully(float[] buffer, int offset, int count)
        {
            int total = 0;
            int zeroReads = 0;
            while (total < count)
            {
                int got = m_Source.Read(buffer, offset + total, count - total);
                if (got < 0)
                {
                    // no data yet (buffering): play silence for the rest of this block
                    Array.Clear(buffer, offset + total, count - total);
                    total = count;
                    break;
                }
                if (got == 0)
                {
                    if (++zeroReads >= 2)
                        break;
                    continue;
                }
                zeroReads = 0;
                total += got;
            }
            return total;
        }

        public void Reset()
        {
        }
    }

    /// <summary>Changes the sample rate (and with it the pitch) with a cubic interpolation.</summary>
    internal sealed class ResampleStream : IStereoStream
    {
        private readonly IStereoStream m_Source;
        private float[] m_Buf = new float[8192 * 2];
        private int m_Len;          // frames in the buffer
        private double m_Pos;       // position of the next output frame in the buffer
        private bool m_Ended;

        /// <summary>Input frames used per output frame.</summary>
        public double Ratio { get; set; }

        public ResampleStream(IStereoStream source, double ratio)
        {
            m_Source = source;
            Ratio = ratio;
        }

        public void Reset()
        {
            m_Len = 0;
            m_Pos = 0;
            m_Ended = false;
            m_Source.Reset();
        }

        public int Read(float[] dst, int frameOffset, int frames)
        {
            int produced = 0;
            int capacity = m_Buf.Length / 2;
            double ratio = Ratio;
            if (ratio < 0.01)
                ratio = 0.01;
            while (produced < frames)
            {
                int i = (int)m_Pos;
                if (i + 2 >= m_Len)
                {
                    if (m_Ended)
                        break;
                    int drop = Math.Max(0, i - 1);
                    if (drop > 0)
                    {
                        Array.Copy(m_Buf, drop * 2, m_Buf, 0, (m_Len - drop) * 2);
                        m_Len -= drop;
                        m_Pos -= drop;
                    }
                    int space = capacity - m_Len;
                    int got = space > 0 ? m_Source.Read(m_Buf, m_Len, space) : 0;
                    if (got <= 0)
                    {
                        m_Ended = true;
                        break;
                    }
                    m_Len += got;
                    continue;
                }
                double t = m_Pos - i;
                int i0 = i > 0 ? i - 1 : 0;
                int d = (frameOffset + produced) * 2;
                for (int ch = 0; ch < 2; ch++)
                {
                    float y0 = m_Buf[i0 * 2 + ch];
                    float y1 = m_Buf[i * 2 + ch];
                    float y2 = m_Buf[(i + 1) * 2 + ch];
                    float y3 = m_Buf[(i + 2) * 2 + ch];
                    double c1 = 0.5 * (y2 - y0);
                    double c2 = y0 - 2.5 * y1 + 2.0 * y2 - 0.5 * y3;
                    double c3 = 0.5 * (y3 - y0) + 1.5 * (y1 - y2);
                    dst[d + ch] = (float)(((c3 * t + c2) * t + c1) * t + y1);
                }
                m_Pos += ratio;
                produced++;
            }
            return produced;
        }
    }

    /// <summary>
    /// Changes the speed without changing the pitch (WSOLA: overlap-add with the best matching position).
    /// </summary>
    internal sealed class StretchStream : IStereoStream
    {
        private readonly IStereoStream m_Source;
        private readonly int m_Hop;        // synthesis hop (half of the frame)
        private readonly int m_Frame;
        private readonly int m_Search;
        private readonly float[] m_Window;
        private float[] m_In = new float[0];   // interleaved stereo input
        private int m_InBase;                  // absolute frame index of the first buffered input frame
        private int m_InLen;                   // buffered frames
        private bool m_InEnded;
        private double m_Nominal;              // nominal absolute start of the next frame
        private int m_PrevStart;
        private bool m_First = true;
        private readonly float[] m_Tail;       // second half of the previous frame (windowed), stereo
        private float[] m_Out = new float[0];  // interleaved stereo output waiting to be read
        private int m_OutStart, m_OutLen;      // in frames
        private bool m_Finished;

        public double Tempo { get; set; }

        public StretchStream(IStereoStream source, int sampleRate)
        {
            m_Source = source;
            Tempo = 1.0;
            int frame = (int)(0.040 * sampleRate);
            frame -= frame % 4;
            if (frame < 256)
                frame = 256;
            m_Frame = frame;
            m_Hop = frame / 2;
            m_Search = Math.Max(32, (int)(0.012 * sampleRate));
            m_Window = new float[frame];
            for (int i = 0; i < frame; i++)
                m_Window[i] = (float)(0.5 - 0.5 * Math.Cos(2.0 * Math.PI * i / frame));
            m_Tail = new float[m_Hop * 2];
            m_Out = new float[m_Hop * 2 * 8];
        }

        public void Reset()
        {
            m_InBase = 0;
            m_InLen = 0;
            m_InEnded = false;
            m_Nominal = 0;
            m_PrevStart = 0;
            m_First = true;
            Array.Clear(m_Tail, 0, m_Tail.Length);
            m_OutStart = 0;
            m_OutLen = 0;
            m_Finished = false;
            m_Source.Reset();
        }

        private void EnsureInput(int absEnd)
        {
            // make sure the buffer holds frames up to absEnd (exclusive) or the source has ended
            while (!m_InEnded && m_InBase + m_InLen < absEnd)
            {
                int want = Math.Max(4096, absEnd - (m_InBase + m_InLen));
                int needFrames = m_InLen + want;
                if (m_In.Length < needFrames * 2)
                {
                    float[] bigger = new float[Math.Max(needFrames * 2, m_In.Length * 2)];
                    Array.Copy(m_In, bigger, m_InLen * 2);
                    m_In = bigger;
                }
                int got = m_Source.Read(m_In, m_InLen, want);
                if (got <= 0)
                {
                    m_InEnded = true;
                    break;
                }
                m_InLen += got;
            }
        }

        private void DropBefore(int absFrame)
        {
            int drop = absFrame - m_InBase;
            if (drop <= 0)
                return;
            if (drop > m_InLen)
                drop = m_InLen;
            Array.Copy(m_In, drop * 2, m_In, 0, (m_InLen - drop) * 2);
            m_InLen -= drop;
            m_InBase += drop;
        }

        private float MonoAt(int absFrame)
        {
            int i = absFrame - m_InBase;
            if (i < 0 || i >= m_InLen)
                return 0f;
            return m_In[i * 2] + m_In[i * 2 + 1];
        }

        // creates the next block of m_Hop frames in the output queue; false at the end
        private bool MakeBlock()
        {
            if (m_Finished)
                return false;
            double tempo = Math.Max(0.2, Math.Min(6.0, Tempo));
            int hop = m_Hop;
            int frame = m_Frame;
            int nominal = (int)Math.Round(m_Nominal);
            int search = m_First ? 0 : m_Search;
            EnsureInput(nominal + search + frame);
            int available = m_InBase + m_InLen;
            if (nominal + frame > available && m_InEnded)
            {
                // not enough input for a whole frame: flush what is left
                EnqueueTail();
                m_Finished = true;
                return true;
            }
            int start = nominal;
            if (!m_First)
            {
                int templateStart = m_PrevStart + hop;
                int bestStart = nominal;
                double bestScore = double.NegativeInfinity;
                int lo = Math.Max(m_InBase - 0, nominal - search);
                int hi = nominal + search;
                if (hi + frame > available)
                    hi = available - frame;
                if (hi < lo)
                    hi = lo;
                for (int cand = lo; cand <= hi; cand += 2)
                {
                    double dot = 0, energy = 1e-9;
                    for (int k = 0; k < hop; k += 2)
                    {
                        float a = MonoAt(templateStart + k);
                        float b = MonoAt(cand + k);
                        dot += a * b;
                        energy += b * b;
                    }
                    double score = dot / Math.Sqrt(energy);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestStart = cand;
                    }
                }
                start = bestStart;
            }
            if (start < m_InBase)
                start = m_InBase;
            // overlap-add
            EnsureOut(hop);
            int rel = start - m_InBase;
            int outPos = (m_OutStart + m_OutLen) * 2;
            for (int i = 0; i < hop; i++)
            {
                int s = (rel + i);
                float w1 = m_Window[i];
                float inL = s < m_InLen ? m_In[s * 2] : 0f;
                float inR = s < m_InLen ? m_In[s * 2 + 1] : 0f;
                m_Out[outPos + i * 2] = m_Tail[i * 2] + w1 * inL;
                m_Out[outPos + i * 2 + 1] = m_Tail[i * 2 + 1] + w1 * inR;
            }
            for (int i = 0; i < hop; i++)
            {
                int s = rel + hop + i;
                float w2 = m_Window[hop + i];
                float inL = s < m_InLen ? m_In[s * 2] : 0f;
                float inR = s < m_InLen ? m_In[s * 2 + 1] : 0f;
                m_Tail[i * 2] = w2 * inL;
                m_Tail[i * 2 + 1] = w2 * inR;
            }
            m_OutLen += hop;
            m_PrevStart = start;
            if (m_First)
            {
                m_First = false;
                m_Nominal = nominal;
            }
            m_Nominal += hop * tempo;
            // keep the input we still need
            int keepFrom = Math.Min(m_PrevStart + hop, (int)m_Nominal - m_Search) - 4;
            if (keepFrom > m_InBase + 8192)
                DropBefore(keepFrom);
            return true;
        }

        private void EnqueueTail()
        {
            EnsureOut(m_Hop);
            int outPos = (m_OutStart + m_OutLen) * 2;
            Array.Copy(m_Tail, 0, m_Out, outPos, m_Hop * 2);
            Array.Clear(m_Tail, 0, m_Tail.Length);
            m_OutLen += m_Hop;
        }

        private void EnsureOut(int extraFrames)
        {
            if (m_OutStart > 0 && (m_OutStart + m_OutLen + extraFrames) * 2 > m_Out.Length)
            {
                Array.Copy(m_Out, m_OutStart * 2, m_Out, 0, m_OutLen * 2);
                m_OutStart = 0;
            }
            if ((m_OutStart + m_OutLen + extraFrames) * 2 > m_Out.Length)
            {
                float[] bigger = new float[(m_OutStart + m_OutLen + extraFrames) * 2 * 2];
                Array.Copy(m_Out, bigger, (m_OutStart + m_OutLen) * 2);
                m_Out = bigger;
            }
        }

        public int Read(float[] dst, int frameOffset, int frames)
        {
            int produced = 0;
            while (produced < frames)
            {
                if (m_OutLen == 0)
                {
                    if (!MakeBlock() || m_OutLen == 0)
                        break;
                }
                int n = Math.Min(frames - produced, m_OutLen);
                Array.Copy(m_Out, m_OutStart * 2, dst, (frameOffset + produced) * 2, n * 2);
                m_OutStart += n;
                m_OutLen -= n;
                if (m_OutLen == 0)
                    m_OutStart = 0;
                produced += n;
            }
            return produced;
        }
    }

    /// <summary>A simple reverb: a few feedback delays (level and delay in ms).</summary>
    internal sealed class SimpleReverb
    {
        private readonly float[][] m_Buf;
        private readonly int[] m_Pos;
        private readonly float m_Level;
        private const float Feedback = 0.45f;
        private static readonly double[] Scale = { 1.0, 0.77, 0.61, 0.47 };

        public int SampleRate { get; private set; }

        public SimpleReverb(float level, int delayMs, int sampleRate)
        {
            SampleRate = sampleRate;
            m_Level = Math.Max(0f, Math.Min(2f, level));
            if (delayMs < 10)
                delayMs = 10;
            if (delayMs > 5000)
                delayMs = 5000;
            m_Buf = new float[Scale.Length][];
            m_Pos = new int[Scale.Length];
            for (int i = 0; i < Scale.Length; i++)
            {
                int frames = Math.Max(16, (int)(delayMs * 0.001 * sampleRate * Scale[i]));
                m_Buf[i] = new float[frames * 2];
            }
        }

        /// <summary>Processes interleaved stereo frames in place.</summary>
        public void Process(float[] buffer, int frames)
        {
            for (int f = 0; f < frames; f++)
            {
                float inL = buffer[f * 2];
                float inR = buffer[f * 2 + 1];
                float wetL = 0, wetR = 0;
                for (int c = 0; c < m_Buf.Length; c++)
                {
                    float[] b = m_Buf[c];
                    int p = m_Pos[c] * 2;
                    float oL = b[p];
                    float oR = b[p + 1];
                    b[p] = inL + oL * Feedback;
                    b[p + 1] = inR + oR * Feedback;
                    wetL += oL;
                    wetR += oR;
                    int np = m_Pos[c] + 1;
                    m_Pos[c] = np * 2 >= b.Length ? 0 : np;
                }
                buffer[f * 2] = inL + wetL * m_Level * 0.5f;
                buffer[f * 2 + 1] = inR + wetR * m_Level * 0.5f;
            }
        }
    }
}
