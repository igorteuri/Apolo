using System;
using System.Threading;
using Ares.Playing.Audio;
using Xunit;

namespace Apolo.Tests
{
    // The sound output of Linux and macOS (OpenAL Soft). The tests use the "null" driver of the library: nothing is heard,
    // but the library is loaded, buffers are queued and the position of the playback is followed.
    public class OpenAlSinkTests
    {
        public OpenAlSinkTests()
        {
            Environment.SetEnvironmentVariable("ALSOFT_DRIVERS", "null");
        }

        private static byte[] Block(int frames)
        {
            byte[] data = new byte[frames * 8];
            for (int i = 0; i < frames * 2; i++)
                BitConverter.GetBytes((float)Math.Sin(i * 0.05) * 0.2f).CopyTo(data, i * 4);
            return data;
        }

        [Fact]
        public void PlaysTheQueuedBlocks_AndTheBufferedFramesFollowThePlayback()
        {
            OpenAlBackend backend = new OpenAlBackend();
            using (Sink sink = backend.Open(backend.DefaultDeviceId(), 2))
            {
                Assert.Equal(0, sink.BufferedFrames);
                byte[] block = Block(AudioEngine.BlockFrames);
                for (int i = 0; i < 20; i++)
                    sink.Write(block, block.Length);   // 20 blocks of 20 ms = 400 ms
                int right = sink.BufferedFrames;
                Assert.InRange(right, AudioEngine.BlockFrames * 15, AudioEngine.BlockFrames * 20);
                Thread.Sleep(250);
                int later = sink.BufferedFrames;
                Assert.True(later < right - AudioEngine.SampleRate / 10, "the playback should have used up at least 100 ms: " + right + " -> " + later);
                Assert.False(sink.Failed);
            }
        }

        [Fact]
        public void ListsTheCards_WithoutFailing()
        {
            var cards = new OpenAlBackend().ListCards();
            Assert.NotNull(cards);
        }
    }
}
