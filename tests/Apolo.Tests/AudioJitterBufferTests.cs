using System;
using Ares.Player;
using Xunit;

namespace Apolo.Tests
{
    public class AudioJitterBufferTests
    {
        // a browser which sends a 440 Hz tone in blocks of 8192 samples, on a clock "speed" times the one of the program;
        // "stall" (in ms, at the given second) holds one block back, like a busy page thread
        private class Run
        {
            public int Gaps, Drops, Frames, MaxJump;
        }

        private static Run Simulate(int seconds, double speed, int marginMs, int stallAtSecond = -1, int stallMs = 0, int stalls = 1)
        {
            AudioJitterBuffer buffer = new AudioJitterBuffer(marginMs, 2000);
            const int block = 8192;
            double produced = 0, phase = 0;
            int pendingSince = -1;
            byte[] frame = new byte[AudioJitterBuffer.FrameBytes];
            Run run = new Run();
            short last = 0;
            bool lastValid = false;
            int ticks = seconds * 50;
            for (int t = 0; t < ticks; ++t)
            {
                produced += 960 * speed;
                int ms = t * 20;
                bool stalled = stallAtSecond >= 0 && Enumerable(stallAtSecond, stalls).Contains(ms / 1000) && ms % 1000 < stallMs;
                while (produced >= block && !stalled)
                {
                    produced -= block;
                    byte[] data = new byte[block * 4];
                    for (int i = 0; i < block; ++i)
                    {
                        short v = (short)(Math.Sin(phase) * 12000);
                        phase += 2 * Math.PI * 440 / 48000;
                        data[i * 4] = (byte)(v & 0xFF); data[i * 4 + 1] = (byte)(v >> 8);
                        data[i * 4 + 2] = (byte)(v & 0xFF); data[i * 4 + 3] = (byte)(v >> 8);
                    }
                    buffer.Add(data, data.Length);
                }
                if (buffer.TakeFrame(frame))
                {
                    run.Frames++;
                    for (int i = 0; i < AudioJitterBuffer.FrameSamples; ++i)
                    {
                        short v = (short)(frame[i * 4] | (frame[i * 4 + 1] << 8));
                        if (lastValid)
                            run.MaxJump = Math.Max(run.MaxJump, Math.Abs(v - last));
                        last = v;
                        lastValid = true;
                    }
                }
                else
                    lastValid = false;
            }
            run.Gaps = buffer.Underruns;
            run.Drops = buffer.Overflows;
            return run;
        }

        private static System.Collections.Generic.List<int> Enumerable(int first, int count)
        {
            var list = new System.Collections.Generic.List<int>();
            for (int i = 0; i < count; ++i)
                list.Add(first + i * 20);
            return list;
        }

        [Fact]
        public void SameClockInBurstsHasNoGaps()
        {
            Run run = Simulate(120, 1.0, 280);
            Assert.Equal(0, run.Gaps);
            Assert.Equal(0, run.Drops);
        }

        [Theory]
        [InlineData(0.997)]
        [InlineData(1.003)]
        public void SmallClockDifferenceIsAbsorbed(double speed)
        {
            // 10 minutes: without the speed adjustment 0.3 % slower would empty the margin every ~90 s, faster would overflow
            Run run = Simulate(600, speed, 280);
            Assert.True(run.Gaps <= 1, "gaps: " + run.Gaps);
            Assert.Equal(0, run.Drops);
        }

        [Fact]
        public void TheToneStaysSmooth()
        {
            // a 440 Hz tone at 12000 changes by at most ~700 per sample: the reading speed adjustment adds no clicks
            Run run = Simulate(60, 1.002, 280);
            Assert.True(run.MaxJump < 900, "largest jump: " + run.MaxJump);
        }

        [Fact]
        public void TheMarginGrowsSoGapsDoNotRepeat()
        {
            // the page thread stands still for 400 ms every 20 s: only the first stalls are heard, then the margin is large enough
            Run run = Simulate(300, 1.0, 280, 10, 400, 14);
            Assert.True(run.Gaps >= 1 && run.Gaps <= 4, "gaps: " + run.Gaps);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(20000)]
        [InlineData(-26000)]
        public void SoftClipKeepsNormalSound(int v)
        {
            Assert.Equal((short)v, AudioJitterBuffer.SoftClip(v));
        }

        [Fact]
        public void SoftClipRoundsOffPeaks()
        {
            Assert.True(AudioJitterBuffer.SoftClip(60000) <= short.MaxValue);
            Assert.True(AudioJitterBuffer.SoftClip(-60000) >= short.MinValue);
            Assert.True(AudioJitterBuffer.SoftClip(30000) < 30000);
            Assert.True(AudioJitterBuffer.SoftClip(30000) > AudioJitterBuffer.SoftClip(28000));
        }
    }
}
