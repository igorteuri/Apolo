using System;
using Ares.Playing.Audio;
using Xunit;

namespace Apolo.Tests
{
    // The loop of a sound effect blends its end into its beginning. The sound here has three parts of constant level
    // (head 0.2, body 0.5, tail 0.8), so the level tells where the playback is inside the blend.
    public class LoopCrossfadeTests
    {
        private const int Rate = AudioEngine.SampleRate;

        private sealed class StepSource : IAudioSource
        {
            private readonly double m_Seconds, m_PartSeconds;
            private long m_Frame;

            public StepSource(double seconds, double partSeconds)
            {
                m_Seconds = seconds;
                m_PartSeconds = partSeconds;
            }

            public int Channels { get { return 2; } }
            public int SampleRate { get { return Rate; } }
            public double Length { get { return m_Seconds; } }
            public double Position { get { return (double)m_Frame / Rate; } }
            public bool CanSeek { get { return true; } }
            public void Seek(double seconds) { m_Frame = (long)Math.Round(seconds * Rate); }
            public void Dispose() { }

            public int Read(float[] buffer, int offset, int count)
            {
                long total = (long)(m_Seconds * Rate);
                int frames = (int)Math.Min(count / 2, total - m_Frame);
                for (int i = 0; i < frames; i++)
                {
                    double t = (double)(m_Frame + i) / Rate;
                    float v = t < m_PartSeconds ? 0.2f : (t >= m_Seconds - m_PartSeconds ? 0.8f : 0.5f);
                    buffer[offset + 2 * i] = v;
                    buffer[offset + 2 * i + 1] = v;
                }
                m_Frame += frames;
                return frames * 2;
            }
        }

        // plays "seconds" of output (silent engine, nothing is heard) and returns the left channel
        private static float[] Render(AudioChannel channel, double seconds)
        {
            int total = (int)(seconds * Rate);
            float[] left = new float[total];
            float[] block = new float[1024 * 2];
            int done = 0;
            while (done < total)
            {
                int n = Math.Min(1024, total - done);
                Array.Clear(block, 0, block.Length);
                channel.Mix(block, n, 2);
                for (int i = 0; i < n; i++)
                    left[done + i] = block[2 * i];
                done += n;
            }
            return left;
        }

        private static AudioChannel NewChannel(int crossfadeMs, bool loop)
        {
            AudioEngine engine = AudioEngine.Instance ?? AudioEngine.Create(0, null);
            AudioChannel channel = new AudioChannel(engine, new StepSource(6.0, 1.0), false);
            channel.Loop = loop;
            channel.LoopCrossfadeMs = crossfadeMs;
            channel.SetVolume(1f);
            // the mixer thread of the engine must not read this channel: it is mixed by hand here
            channel.MarkPlayingForTest();
            return channel;
        }

        private static float At(float[] data, double seconds)
        {
            return data[(int)(seconds * Rate)];
        }

        [Fact]
        public void Loop_BlendsEndWithStart_AndRepeatsEveryLengthMinusCrossfade()
        {
            AudioChannel channel = NewChannel(1000, true);
            float[] output = Render(channel, 16.0);
            // first pass: head 0.2, body 0.5
            Assert.InRange(At(output, 0.5), 0.19f, 0.21f);
            Assert.InRange(At(output, 3.0), 0.49f, 0.51f);
            // the blend of the tail (0.8) with the head (0.2) from second 5 to 6: equal power
            Assert.InRange(At(output, 5.01), 0.77f, 0.81f);
            Assert.InRange(At(output, 5.5), 0.68f, 0.74f);
            Assert.InRange(At(output, 5.99), 0.19f, 0.26f);
            // then the body goes on from second 1 of the file
            Assert.InRange(At(output, 7.0), 0.49f, 0.51f);
            // the next blend comes 5 seconds later (length 6 s minus the crossfade of 1 s)
            Assert.InRange(At(output, 10.5), 0.68f, 0.74f);
            Assert.InRange(At(output, 15.5), 0.68f, 0.74f);
        }

        [Fact]
        public void Loop_WithoutCrossfade_JumpsBackAfterTheWholeLength()
        {
            AudioChannel channel = NewChannel(0, true);
            float[] output = Render(channel, 14.0);
            Assert.InRange(At(output, 5.5), 0.79f, 0.81f);   // the tail, not blended
            Assert.InRange(At(output, 6.5), 0.19f, 0.21f);   // back at the head after 6 s
            Assert.InRange(At(output, 12.5), 0.19f, 0.21f);
        }

        [Fact]
        public void NoLoop_PlaysOnceAndEnds()
        {
            AudioChannel channel = NewChannel(1000, false);
            float[] output = Render(channel, 8.0);
            Assert.InRange(At(output, 5.5), 0.79f, 0.81f);   // plain tail, no blend
            Assert.Equal(0f, At(output, 7.0));
        }
    }
}
