using System;
using System.IO;
using Ares.Player;
using Ares.Playing.Audio;
using Xunit;

namespace Apolo.Tests
{
    // The rough waveform of the seek bar: loud and quiet parts, bad input, and the cache.
    public class WaveformTests : IDisposable
    {
        private readonly string m_Dir = Path.Combine(Path.GetTempPath(), "apolo-wave-" + Guid.NewGuid().ToString("N"));

        public WaveformTests()
        {
            Directory.CreateDirectory(m_Dir);
        }

        public void Dispose()
        {
            try { Directory.Delete(m_Dir, true); } catch (Exception) { }
        }

        private sealed class FakeSource : IAudioSource
        {
            private readonly float[] m_Samples;
            private int m_Pos;
            public FakeSource(float[] samples, int channels, int rate, double length)
            {
                m_Samples = samples; Channels = channels; SampleRate = rate; Length = length;
            }
            public int Channels { get; private set; }
            public int SampleRate { get; private set; }
            public double Length { get; private set; }
            public double Position { get { return 0; } }
            public bool CanSeek { get { return false; } }
            public void Seek(double seconds) { }
            public int Read(float[] buffer, int offset, int count)
            {
                int n = Math.Min(count, m_Samples.Length - m_Pos);
                Array.Copy(m_Samples, m_Pos, buffer, offset, n);
                m_Pos += n;
                return n;
            }
            public void Dispose() { }
        }

        private static float[] Tone(double seconds, int rate, float amplitude)
        {
            float[] s = new float[(int)(seconds * rate)];
            for (int i = 0; i < s.Length; i++)
                s[i] = amplitude * (float)Math.Sin(2 * Math.PI * 440 * i / rate);
            return s;
        }

        [Fact]
        public void LoudThenQuiet_GivesHighThenLowSlices()
        {
            float[] loud = Tone(5, 8000, 0.8f);
            float[] quiet = Tone(5, 8000, 0.08f);
            float[] all = new float[loud.Length + quiet.Length];
            loud.CopyTo(all, 0);
            quiet.CopyTo(all, loud.Length);
            byte[] w = WaveformAnalyzer.Summarize(new FakeSource(all, 1, 8000, 10), 100);
            Assert.Equal(100, w.Length);
            Assert.True(w[10] > 240);
            Assert.True(w[90] < w[10] / 2);
            Assert.True(w[90] > 0);
        }

        [Fact]
        public void Silence_IsAllZero_AndStereoIsAccepted()
        {
            byte[] w = WaveformAnalyzer.Summarize(new FakeSource(new float[8000 * 2 * 3], 2, 8000, 3), 30);
            Assert.Equal(30, w.Length);
            Assert.All(w, b => Assert.Equal(0, b));
        }

        [Fact]
        public void UnknownLength_BadFile_AndMissingFile_GiveNull()
        {
            Assert.Null(WaveformAnalyzer.Summarize(new FakeSource(new float[100], 1, 8000, 0), 50));
            Assert.Null(WaveformAnalyzer.Analyze(Path.Combine(m_Dir, "nao existe.mp3")));
            string bad = Path.Combine(m_Dir, "lixo.mp3");
            File.WriteAllText(bad, "isto não é áudio");
            Assert.Null(WaveformAnalyzer.Analyze(bad));
        }

        [Fact]
        public void ARealWavFile_WithAccentsInTheName_IsAnalyzed()
        {
            string path = Path.Combine(m_Dir, "Música épica 🐉.wav");
            float[] tone = Tone(2, 8000, 0.5f);
            using (var writer = new NAudio.Wave.WaveFileWriter(path, new NAudio.Wave.WaveFormat(8000, 16, 1)))
                writer.WriteSamples(tone, 0, tone.Length);
            byte[] w = WaveformAnalyzer.Analyze(path, 50);
            Assert.NotNull(w);
            Assert.Equal(50, w.Length);
            Assert.True(w[25] > 200);
        }

        [Fact]
        public void Cache_ReturnsWhatWasSaved_AndForgetsAChangedFile()
        {
            string music = Path.Combine(m_Dir, "faixa.mp3");
            File.WriteAllText(music, "aaaa");
            WaveformCache cache = new WaveformCache(Path.Combine(m_Dir, "cache"));
            Assert.Null(cache.Get(music));
            cache.Put(music, new byte[] { 1, 2, 3 });
            Assert.Equal(new byte[] { 1, 2, 3 }, cache.Get(music));
            File.WriteAllText(music, "bbbbbbbb"); // other size: it is another file
            Assert.Null(cache.Get(music));
        }

        [Fact]
        public void Cache_IgnoresNullEmptyAndMissingFiles()
        {
            WaveformCache cache = new WaveformCache(Path.Combine(m_Dir, "cache2"));
            cache.Put(null, new byte[] { 1 });
            cache.Put(Path.Combine(m_Dir, "x.mp3"), new byte[] { 1 });
            cache.Put(Path.Combine(m_Dir, "y.mp3"), null);
            Assert.Null(cache.Get(null));
            Assert.Null(cache.Get(""));
            Assert.Null(cache.Get(Path.Combine(m_Dir, "x.mp3")));
            Assert.False(Directory.Exists(Path.Combine(m_Dir, "cache2")));
        }
    }
}
