using System;
using System.IO;
using Ares.Playing.Audio;
using Xunit;

namespace Apolo.Tests
{
    // The decoder which uses ffmpeg (for M4A, FLAC... on Linux and macOS). Without ffmpeg on the machine the test has nothing to check.
    public class FfmpegSourceTests : IDisposable
    {
        private readonly string m_Dir = Path.Combine(Path.GetTempPath(), "apolo-ffmpeg-" + Guid.NewGuid().ToString("N"));

        public FfmpegSourceTests()
        {
            Directory.CreateDirectory(m_Dir);
        }

        public void Dispose()
        {
            try { Directory.Delete(m_Dir, true); } catch (Exception) { }
        }

        private string MakeWave()
        {
            string path = Path.Combine(m_Dir, "tone.wav");
            int rate = 44100, frames = rate * 2;
            using (BinaryWriter w = new BinaryWriter(File.Create(path)))
            {
                w.Write(new[] { 'R', 'I', 'F', 'F' });
                w.Write(36 + frames * 2);
                w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
                w.Write(new[] { 'd', 'a', 't', 'a' });
                w.Write(frames * 2);
                for (int i = 0; i < frames; i++)
                    w.Write((short)(Math.Sin(i * 2 * Math.PI * 440 / rate) * 12000));
            }
            return path;
        }

        [Fact]
        public void ReadsAFile_WithTheRightLength_AndSeeks()
        {
            if (FfmpegSource.Executable == null)
                return;
            using (FfmpegSource source = FfmpegSource.TryOpen(MakeWave()))
            {
                Assert.NotNull(source);
                Assert.InRange(source.Length, 1.9, 2.1);
                Assert.Equal(2, source.Channels);
                Assert.Equal(AudioEngine.SampleRate, source.SampleRate);
                float[] buffer = new float[4096];
                int total = 0;
                float peak = 0;
                int n;
                while ((n = source.Read(buffer, 0, buffer.Length)) > 0)
                {
                    total += n;
                    for (int i = 0; i < n; i++)
                        peak = Math.Max(peak, Math.Abs(buffer[i]));
                }
                double seconds = total / 2.0 / AudioEngine.SampleRate;
                Assert.InRange(seconds, 1.9, 2.1);
                Assert.InRange(peak, 0.33, 0.40);   // 12000 of 32768: a mono file keeps its level

                // the position after a jump
                source.Seek(1.5);
                int rest = 0;
                while ((n = source.Read(buffer, 0, buffer.Length)) > 0)
                    rest += n;
                Assert.InRange(rest / 2.0 / AudioEngine.SampleRate, 0.4, 0.6);
            }
        }

        [Fact]
        public void AFileWhichIsNotAudio_IsNotOpened()
        {
            if (FfmpegSource.Executable == null)
                return;
            string path = Path.Combine(m_Dir, "not-audio.txt");
            File.WriteAllText(path, "hello");
            Assert.Null(FfmpegSource.TryOpen(path));
        }
    }
}
