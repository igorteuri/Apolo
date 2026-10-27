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
    /// <summary>
    /// The switch and the settings of the leveler (the Player and the settings window write them; the audio engine reads them
    /// on every block), and what it is doing now (for the display in the settings window).
    /// </summary>
    public static class AudioLeveler
    {
        public const float DefaultTargetDb = -20f;
        public const float DefaultStrength = 0.8f;
        public const float DefaultMaxBoostDb = 9f;
        public const float DefaultMaxCutDb = 12f;

        private static volatile bool s_Enabled = true;
        private static volatile float s_TargetDb = DefaultTargetDb;
        private static volatile float s_Strength = DefaultStrength;
        private static volatile float s_MaxBoostDb = DefaultMaxBoostDb;
        private static volatile float s_MaxCutDb = DefaultMaxCutDb;

        /// <summary>Raised when the switch changes (so that the button in the window and the settings page follow each other).</summary>
        public static event EventHandler EnabledChanged;

        public static bool Enabled
        {
            get { return s_Enabled; }
            set
            {
                if (s_Enabled == value)
                    return;
                s_Enabled = value;
                EventHandler h = EnabledChanged;
                if (h != null)
                    h(null, EventArgs.Empty);
            }
        }

        /// <summary>The loudness (RMS, dB below full scale) the mix is led to.</summary>
        public static float TargetDb { get { return s_TargetDb; } set { s_TargetDb = Math.Max(-40f, Math.Min(-6f, value)); } }
        /// <summary>How much of the difference to the target is corrected, 0 to 1 (1 = a flat level).</summary>
        public static float Strength { get { return s_Strength; } set { s_Strength = Math.Max(0f, Math.Min(1f, value)); } }
        /// <summary>The most it makes quiet material louder (dB).</summary>
        public static float MaxBoostDb { get { return s_MaxBoostDb; } set { s_MaxBoostDb = Math.Max(0f, Math.Min(24f, value)); } }
        /// <summary>The most it makes loud material quieter (dB).</summary>
        public static float MaxCutDb { get { return s_MaxCutDb; } set { s_MaxCutDb = Math.Max(0f, Math.Min(30f, value)); } }

        private static volatile int s_ActiveTick;
        /// <summary>Environment.TickCount of the last block with sound above the gate (to tell "playing" from "silent").</summary>
        public static int ActiveTick { get { return s_ActiveTick; } internal set { s_ActiveTick = value; } }

        private static volatile float s_GainDb;
        private static volatile float s_LevelDb = -60f;

        /// <summary>The gain in force now (dB, + = louder, - = quieter). Written by the audio engine.</summary>
        public static float GainDb { get { return s_GainDb; } internal set { s_GainDb = value; } }
        /// <summary>The loudness of the mix before the gain (RMS, dB). Written by the audio engine.</summary>
        public static float LevelDb { get { return s_LevelDb; } internal set { s_LevelDb = value; } }

        public static void ResetToDefaults()
        {
            TargetDb = DefaultTargetDb;
            Strength = DefaultStrength;
            MaxBoostDb = DefaultMaxBoostDb;
            MaxCutDb = DefaultMaxCutDb;
        }
    }
}

namespace Ares.Playing.Audio
{
    internal sealed class LevelCompressor
    {
        // target, strength and the limits of the gain are in AudioLeveler (the user can change them)
        /// <summary>Quieter than this counts as silence: the level estimate and the gain stay as they are.</summary>
        public const float GateDb = -50f;
        /// <summary>Peaks never go above this after the gain.</summary>
        public const float Ceiling = 0.95f;

        private float m_LevelDb = AudioLeveler.DefaultTargetDb;   // slow estimate of the loudness of the mix
        private float m_GainDb;               // gain in force at the end of the last block
        private float m_Gain = 1f;            // the same as a factor

        /// <summary>The gain in dB at the end of the last block (for tests and display).</summary>
        public float GainDb { get { return m_GainDb; } }

        public void Reset()
        {
            m_LevelDb = AudioLeveler.TargetDb;
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
                    AudioLeveler.ActiveTick = Environment.TickCount;
                    float tau = powerDb > m_LevelDb ? 0.5f : 2.0f;
                    m_LevelDb += (powerDb - m_LevelDb) * Coefficient(seconds, tau);
                }
                desiredDb = (AudioLeveler.TargetDb - m_LevelDb) * AudioLeveler.Strength;
                if (desiredDb > AudioLeveler.MaxBoostDb) desiredDb = AudioLeveler.MaxBoostDb;
                else if (desiredDb < -AudioLeveler.MaxCutDb) desiredDb = -AudioLeveler.MaxCutDb;
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
            if (enabled)
            {
                AudioLeveler.GainDb = m_GainDb;
                AudioLeveler.LevelDb = m_LevelDb;
            }
            else
            {
                AudioLeveler.GainDb = 0f;
            }
        }
    }
}
