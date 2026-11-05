using System;
using System.Collections.Generic;
using System.IO;
using Ares.Player;
using Xunit;

namespace Apolo.Tests
{
    // Preparing waveforms ahead of time: what is skipped, what is analyzed, stopping, bad files, and the idle check.
    public class WaveformPrecomputeTests : IDisposable
    {
        private readonly string m_Dir = Path.Combine(Path.GetTempPath(), "apolo-precalc-" + Guid.NewGuid().ToString("N"));
        private readonly WaveformCache m_Cache;

        public WaveformPrecomputeTests()
        {
            Directory.CreateDirectory(m_Dir);
            m_Cache = new WaveformCache(Path.Combine(m_Dir, "cache"));
        }

        public void Dispose()
        {
            try { Directory.Delete(m_Dir, true); } catch (Exception) { }
        }

        private string MakeFile(string name)
        {
            string p = Path.Combine(m_Dir, name);
            File.WriteAllBytes(p, new byte[] { 1, 2, 3 });
            return p;
        }

        [Fact]
        public void AnalyzesMissingAndSkipsCached()
        {
            string a = MakeFile("música ação 🎵.mp3"), b = MakeFile("b.mp3");
            m_Cache.Put(a, new byte[] { 9, 9 });
            List<string> analyzed = new List<string>();
            WaveformPrecompute run = new WaveformPrecompute(m_Cache, p => { analyzed.Add(p); return new byte[] { 5 }; });
            WaveformPrecompute.Result r = run.Run(new[] { a, b }, null, null);
            Assert.Equal(2, r.Total);
            Assert.Equal(1, r.Cached);
            Assert.Equal(1, r.Analyzed);
            Assert.Equal(new[] { b }, analyzed);
            Assert.Equal(new byte[] { 5 }, m_Cache.Get(b));
            Assert.False(r.Stopped);
        }

        [Fact]
        public void SecondRunDoesNothing()
        {
            string a = MakeFile("a.mp3");
            int calls = 0;
            WaveformPrecompute run = new WaveformPrecompute(m_Cache, p => { calls++; return new byte[] { 1 }; });
            run.Run(new[] { a }, null, null);
            WaveformPrecompute.Result r = run.Run(new[] { a }, null, null);
            Assert.Equal(1, calls);
            Assert.Equal(1, r.Cached);
        }

        [Fact]
        public void StopsWhenAsked_AndKeepsWhatWasDone()
        {
            string a = MakeFile("a.mp3"), b = MakeFile("b.mp3"), c = MakeFile("c.mp3");
            int done = 0;
            WaveformPrecompute run = new WaveformPrecompute(m_Cache, p => { done++; return new byte[] { 1 }; });
            WaveformPrecompute.Result r = run.Run(new[] { a, b, c }, () => done >= 1, null);
            Assert.True(r.Stopped);
            Assert.Equal(1, r.Analyzed);
            Assert.NotNull(m_Cache.Get(a));
            Assert.Null(m_Cache.Get(b));
        }

        [Fact]
        public void BadFiles_CountAsFailed_AndTheRestGoesOn()
        {
            string a = MakeFile("a.mp3"), b = MakeFile("b.mp3"), c = MakeFile("c.mp3");
            WaveformPrecompute run = new WaveformPrecompute(m_Cache, p =>
            {
                if (p == a) return null;
                if (p == b) throw new InvalidOperationException("broken");
                return new byte[] { 7 };
            });
            WaveformPrecompute.Result r = run.Run(new[] { a, b, c }, null, null);
            Assert.Equal(2, r.Failed);
            Assert.Equal(1, r.Analyzed);
        }

        [Fact]
        public void EmptyNullAndRepeatedPaths_AreIgnored()
        {
            string a = MakeFile("a.mp3");
            int calls = 0;
            WaveformPrecompute run = new WaveformPrecompute(m_Cache, p => { calls++; return new byte[] { 1 }; });
            WaveformPrecompute.Result r = run.Run(new[] { null, "", a, a.ToUpperInvariant(), a }, null, null);
            Assert.Equal(1, r.Total);
            Assert.Equal(1, calls);
            Assert.Equal(0, run.Run(null, null, null).Total);
            Assert.Equal(0, run.Run(new string[0], null, null).Total);
        }

        [Fact]
        public void FileThatDoesNotExist_IsNotCachedAndCountsAsAnalyzedOrFailedWithoutCrash()
        {
            WaveformPrecompute run = new WaveformPrecompute(m_Cache, p => new byte[] { 1 });
            WaveformPrecompute.Result r = run.Run(new[] { Path.Combine(m_Dir, "nao-existe.mp3") }, null, null);
            Assert.Equal(1, r.Total);
        }

        [Fact]
        public void ReportsProgress()
        {
            string a = MakeFile("a.mp3"), b = MakeFile("b.mp3");
            List<string> seen = new List<string>();
            WaveformPrecompute run = new WaveformPrecompute(m_Cache, p => new byte[] { 1 });
            run.Run(new[] { a, b }, null, (done, total) => seen.Add(done + "/" + total));
            Assert.Equal(new[] { "1/2", "2/2" }, seen.ToArray());
        }

        [Fact]
        public void IsIdle_ComparesElapsedTime_AlsoOverTheTickWrapAround()
        {
            Assert.True(WaveformPrecompute.IsIdle(200000, 0, 180000));
            Assert.False(WaveformPrecompute.IsIdle(100000, 0, 180000));
            // Environment.TickCount wraps from int.MaxValue to int.MinValue
            Assert.False(WaveformPrecompute.IsIdle(int.MinValue + 1000, int.MaxValue - 1000, 180000));
            Assert.True(WaveformPrecompute.IsIdle(int.MinValue + 200000, int.MaxValue - 1000, 180000));
        }
    }
}
