/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 The waiting room of one sound source which is sent out (VDO.Ninja, Discord): 48 kHz, stereo, 16 bit, taken 20 ms at a time.
 The web pages deliver their sound in bursts and on the clock of the browser, while the frames are taken on the clock of the
 program (the clock of a page was measured ~1 % slow). Two things keep the sound whole:
  - the margin grows by half when the source ran dry (and shrinks slowly again after a long calm time), so that a gap
    does not come back every few seconds;
  - the speed at which the samples are read is adjusted by at most 2 % (about 1 % is not noticed) so that the filling stays around the
    margin: no gaps when the source is a little slower, nothing dropped when it is a little faster.
 Kept free of any window code so that it can be tested automatically.
*/
using System;

namespace Ares.Player
{
    internal class AudioJitterBuffer
    {
        public const int FrameSamples = 960;              // stereo samples of 20 ms
        public const int FrameBytes = FrameSamples * 4;
        private const int Rate = 48000;
        private const double MaxAdjust = 0.02;            // 2 % faster or slower at most (a page clock ~1 % slow was measured)
        private const double Gain = 0.1;                 // 10 % too empty / too full asks for 1 % of speed change
        private const int GrowSamples = Rate * 40 / 1000; // after a gap the margin grows by half (at least 40 ms)
        private const int CalmFrames = 3000;              // 60 s without a gap: the margin shrinks by 20 ms

        private readonly object m_Lock = new object();
        private readonly short[] m_Ring;                  // interleaved left/right
        private readonly int m_Capacity;                  // stereo samples
        private readonly int m_StartTarget, m_MaxTarget;
        private int m_Read, m_Count, m_Target;
        private double m_Frac, m_Level = -1;
        private bool m_Primed, m_Ends, m_FadeIn;
        private short m_LastL, m_LastR;
        private int m_FramesSinceGap;

        public long TotalBytes;
        public bool Reported;
        /// <summary>Times the source ran dry while sound was flowing, and times old sound had to be dropped.</summary>
        public int Underruns, Overflows;

        public AudioJitterBuffer(int targetMs, int maxTargetMs)
        {
            m_StartTarget = Math.Max(FrameSamples, targetMs * Rate / 1000);
            m_MaxTarget = Math.Max(m_StartTarget, maxTargetMs * Rate / 1000);
            m_Target = m_StartTarget;
            m_Capacity = m_MaxTarget * 2 + Rate;
            m_Ring = new short[m_Capacity * 2];
        }

        /// <summary>The margin now, in milliseconds.</summary>
        public int TargetMs
        {
            get { lock (m_Lock) return m_Target * 1000 / Rate; }
        }

        public void Add(byte[] buffer, int count)
        {
            int samples = count / 4;
            if (samples <= 0)
                return;
            lock (m_Lock)
            {
                TotalBytes += count;
                if (m_Count + samples > m_Capacity)
                {
                    // far too much (the program stood still): the oldest part goes, the rest starts softly
                    Overflows++;
                    int keep = Math.Max(0, Math.Min(m_Target, m_Capacity - samples));
                    int drop = m_Count - keep;
                    m_Read = (m_Read + drop) % m_Capacity;
                    m_Count = keep;
                    m_Frac = 0;
                    m_FadeIn = true;
                }
                int write = (m_Read + m_Count) % m_Capacity;
                for (int i = 0; i < samples; ++i)
                {
                    int o = i * 4;
                    m_Ring[write * 2] = (short)(buffer[o] | (buffer[o + 1] << 8));
                    m_Ring[write * 2 + 1] = (short)(buffer[o + 2] | (buffer[o + 3] << 8));
                    if (++write == m_Capacity)
                        write = 0;
                }
                m_Count += samples;
            }
        }

        /// <summary>Takes 20 ms (FrameBytes) into "frame"; false when the source has nothing to give now.</summary>
        public bool TakeFrame(byte[] frame)
        {
            lock (m_Lock)
            {
                if (!m_Primed)
                {
                    if (m_Count < m_Target)
                        return false;
                    m_Primed = true;
                    m_Level = m_Count;
                }
                // the filling, smoothed over about a second (the bursts make it jump), steers the reading speed
                m_Level += (m_Count - m_Level) * 0.02;
                double error = (m_Level - m_Target) / m_Target;
                double ratio = 1 + Math.Max(-MaxAdjust, Math.Min(MaxAdjust, error * Gain));
                double end = m_Frac + FrameSamples * ratio;
                if ((int)end + 1 >= m_Count)
                    return RanDry(frame);
                double pos = m_Frac;
                for (int i = 0; i < FrameSamples; ++i, pos += ratio)
                {
                    int k = (int)pos;
                    double f = pos - k;
                    int a = (m_Read + k) % m_Capacity, b = (a + 1) % m_Capacity;
                    int l = (int)Math.Round(m_Ring[a * 2] + (m_Ring[b * 2] - m_Ring[a * 2]) * f);
                    int r = (int)Math.Round(m_Ring[a * 2 + 1] + (m_Ring[b * 2 + 1] - m_Ring[a * 2 + 1]) * f);
                    if (m_FadeIn)
                    {
                        // the first frame after a gap starts from silence
                        double k2 = (i + 1) / (double)FrameSamples;
                        l = (int)(l * k2);
                        r = (int)(r * k2);
                    }
                    Put(frame, i, l, r);
                }
                int advance = (int)end;
                m_Frac = end - advance;
                m_Read = (m_Read + advance) % m_Capacity;
                m_Count -= advance;
                m_FadeIn = false;
                m_Ends = true;
                m_LastL = (short)(frame[FrameBytes - 4] | (frame[FrameBytes - 3] << 8));
                m_LastR = (short)(frame[FrameBytes - 2] | (frame[FrameBytes - 1] << 8));
                if (++m_FramesSinceGap >= CalmFrames)
                {
                    m_FramesSinceGap = 0;
                    if (m_Target > m_StartTarget)
                        m_Target = Math.Max(m_StartTarget, m_Target - FrameSamples);
                }
                return true;
            }
        }

        // the source ran dry: one frame which fades from the last sample to silence (true: it is in "frame"), then the
        // sound waits for a margin half again as large as before
        private bool RanDry(byte[] frame)
        {
            m_Primed = false;
            m_FramesSinceGap = 0;
            if (!m_Ends)
                return false;
            m_Ends = false;
            Underruns++;
            m_Target = Math.Min(m_MaxTarget, m_Target + Math.Max(GrowSamples, m_Target / 2));
            m_FadeIn = true;
            for (int i = 0; i < FrameSamples; ++i)
            {
                double k = 1 - (i + 1) / (double)FrameSamples;
                Put(frame, i, (int)(m_LastL * k), (int)(m_LastR * k));
            }
            return true;
        }

        private static void Put(byte[] frame, int i, int l, int r)
        {
            if (l > short.MaxValue) l = short.MaxValue; else if (l < short.MinValue) l = short.MinValue;
            if (r > short.MaxValue) r = short.MaxValue; else if (r < short.MinValue) r = short.MinValue;
            int o = i * 4;
            frame[o] = (byte)(l & 0xFF);
            frame[o + 1] = (byte)((l >> 8) & 0xFF);
            frame[o + 2] = (byte)(r & 0xFF);
            frame[o + 3] = (byte)((r >> 8) & 0xFF);
        }

        /// <summary>
        /// A soft limit for the mixed sound: up to 80 % nothing changes, above it the peaks are rounded off instead of
        /// being cut flat (a flat cut of loud peaks is heard as a faint crackle).
        /// </summary>
        public static short SoftClip(int v)
        {
            const double knee = 0.8;
            double x = v / 32768.0;
            double a = Math.Abs(x);
            if (a <= knee)
                return (short)v;
            double y = knee + (1 - knee) * Math.Tanh((a - knee) / (1 - knee));
            int s = (int)Math.Round((x < 0 ? -y : y) * 32767);
            return (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, s));
        }
    }
}
