using System;
using Ares.Playing.Audio;
using Xunit;

namespace Apolo.Tests
{
    public class LevelCompressorTests
    {
        private const int Rate = 48000;
        private const int Frames = 960;

        // runs a sine wave of the given amplitude through the leveler for some seconds; returns the RMS (dB) of the last second
        private static float Run(LevelCompressor c, float amplitude, double seconds, bool enabled, out float maxPeak)
        {
            float[] block = new float[Frames * 2];
            double phase = 0;
            int blocks = (int)(seconds * Rate / Frames);
            double sum = 0;
            long count = 0;
            maxPeak = 0;
            for (int b = 0; b < blocks; b++)
            {
                for (int f = 0; f < Frames; f++)
                {
                    float v = amplitude * (float)Math.Sin(phase);
                    phase += 2 * Math.PI * 440 / Rate;
                    block[f * 2] = v;
                    block[f * 2 + 1] = v;
                }
                c.Process(block, Frames, 2, Rate, enabled);
                for (int i = 0; i < block.Length; i++)
                    maxPeak = Math.Max(maxPeak, Math.Abs(block[i]));
                if (b >= blocks - Rate / Frames)
                {
                    for (int i = 0; i < block.Length; i++)
                        sum += block[i] * block[i];
                    count += block.Length;
                }
            }
            return 10f * (float)Math.Log10(sum / count + 1e-12);
        }

        [Fact]
        public void QuietAndLoudMaterialEndUpCloserTogether()
        {
            float peak;
            float quiet = Run(new LevelCompressor(), 0.05f, 12, true, out peak);
            float loud = Run(new LevelCompressor(), 0.7f, 12, true, out peak);
            float quietIn = 20f * (float)Math.Log10(0.05f / Math.Sqrt(2));
            float loudIn = 20f * (float)Math.Log10(0.7f / Math.Sqrt(2));
            Assert.True(loud - quiet < (loudIn - quietIn) / 2, "spread " + (loud - quiet) + " dB, input spread " + (loudIn - quietIn) + " dB");
            Assert.True(quiet > quietIn + 3, "the quiet piece is raised");
            Assert.True(loud < loudIn - 3, "the loud piece is lowered");
        }

        [Fact]
        public void PeaksStayUnderTheCeiling()
        {
            float peak;
            Run(new LevelCompressor(), 0.99f, 5, true, out peak);
            Assert.True(peak <= LevelCompressor.Ceiling + 0.001f, "peak " + peak);
        }

        [Fact]
        public void SilenceIsLeftAlone()
        {
            LevelCompressor c = new LevelCompressor();
            float peak;
            Run(c, 0.0001f, 5, true, out peak); // about -80 dB: under the gate
            Assert.True(Math.Abs(c.GainDb) < 0.5f, "gain " + c.GainDb);
        }

        [Fact]
        public void WhenOffTheSoundIsUntouchedAndTheGainFadesBackToZero()
        {
            LevelCompressor c = new LevelCompressor();
            float peak;
            Run(c, 0.05f, 10, true, out peak);
            Assert.True(c.GainDb > 3f);
            Run(c, 0.05f, 20, false, out peak);
            Assert.True(Math.Abs(c.GainDb) < 0.01f);
            float[] block = { 0.1f, -0.2f, 0.3f, -0.4f };
            c.Process(block, 2, 2, Rate, false);
            Assert.Equal(new float[] { 0.1f, -0.2f, 0.3f, -0.4f }, block);
        }
    }
}
