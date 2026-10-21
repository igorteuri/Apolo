/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 The "leveler" of the output: a slow automatic gain (a gentle compressor) that brings the loudness of the whole mix
 towards one level, so that a quiet piece and a loud piece sound about equally loud, plus a limiter that keeps the
 peaks in range. It works on the final mix (what goes to the speakers and to the capture for streams), before the
 master volume. No window code and no other dependencies, so that it can be tested automatically.
*/
using System;

namespace Ares.Playing
{
    /// <summary>The switch of the leveler (the Player turns it on and off; the audio engine reads it).</summary>
    public static class AudioLeveler
    {
        private static volatile bool s_Enabled = true;

        public static bool Enabled
        {
            get { return s_Enabled; }
            set { s_Enabled = value; }
        }
    }
}

namespace Ares.Playing.Audio
{
    internal sealed class LevelCompressor
    {
        /// <summary>The loudness (RMS, in dB below full scale) the mix is led to.</summary>
        public const float TargetDb = -20f;
        /// <summary>The most it makes quiet material louder / loud material quieter.</summary>
        public const float MaxBoostDb = 9f;
        public const float MaxCutDb = 12f;
        /// <summary>Quieter than this counts as silence: the level estimate and the gain stay as they are.</summary>
        public const float GateDb = -50f;
        /// <summary>How much of the difference to the target is corrected (1 = all of it, a flat level).</summary>
        public const float Strength = 0.8f;
        /// <summary>Peaks never go above this after the gain.</summary>
        public const float Ceiling = 0.95f;

        private float m_LevelDb = TargetDb;   // slow estimate of the loudness of the mix
        private float m_GainDb;               // gain in force at the end of the last block
        private float m_Gain = 1f;            // the same as a factor

        /// <summary>The gain in dB at the end of the last block (for tests and display).</summary>
        public float GainDb { get { return m_GainDb; } }

        public void Reset()
        {
            m_LevelDb = TargetDb;
            m_GainDb = 0f;
            m_Gain = 1f;
        }

        private static float Coefficient(float seconds, float tau)
        {
            return 1f - (float)Math.Exp(-seconds / tau);
        }

        /// <summary>Processes one block of interleaved samples in place.</summary>
        public void Process(float[] buffer, int frames, int channels, int sampleRate, bool enabled)
        {
            if (!enabled && Math.Abs(m_GainDb) < 0.01f)
            {
                m_Gain = 1f;
                m_GainDb = 0f;
                return; // off and settled: nothing to do
            }
            int total = frames * channels;
            if (total <= 0 || total > buffer.Length)
                return;
            float seconds = frames / (float)sampleRate;

            double sum = 0;
            float peak = 0f;
            for (int i = 0; i < total; i++)
            {
                float s = buffer[i];
                sum += s * s;
                float a = s < 0 ? -s : s;
                if (a > peak) peak = a;
            }
            float powerDb = 10f * (float)Math.Log10(sum / total + 1e-12);

            float desiredDb = 0f;
            if (enabled)
            {
                if (powerDb > GateDb)
                {
                    float tau = powerDb > m_LevelDb ? 0.5f : 2.0f;
                    m_LevelDb += (powerDb - m_LevelDb) * Coefficient(seconds, tau);
                }
                desiredDb = (TargetDb - m_LevelDb) * Strength;
                if (desiredDb > MaxBoostDb) desiredDb = MaxBoostDb;
                else if (desiredDb < -MaxCutDb) desiredDb = -MaxCutDb;
                // no boost while nothing is playing, so that the noise between pieces is not raised
                if (powerDb <= GateDb && desiredDb > m_GainDb)
                    desiredDb = m_GainDb;
            }
            // down fast, up slowly
            float rate = desiredDb < m_GainDb ? Coefficient(seconds, 0.3f) : Coefficient(seconds, 2.0f);
            m_GainDb += (desiredDb - m_GainDb) * rate;

            float start = m_Gain;
            float end = (float)Math.Pow(10.0, m_GainDb / 20.0);
            // limiter: the loudest sample of the block, after the gain, stays under the ceiling
            if (peak > 0f)
            {
                float room = Ceiling / peak;
                if (start > room) start = room;
                if (end > room)
                {
                    end = room;
                    m_GainDb = 20f * (float)Math.Log10(end);
                }
            }
            for (int f = 0; f < frames; f++)
            {
                float g = start + (end - start) * (f + 1) / frames;
                int o = f * channels;
                for (int c = 0; c < channels; c++)
                    buffer[o + c] *= g;
            }
            m_Gain = end;
        }
    }
}
