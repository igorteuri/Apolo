using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Ares.Player;
using Xunit;

namespace Apolo.Tests
{
    // The sound of the web tabs sent through VDO/Discord crackled for a long time (pieces lost when the page was busy).
    // These tests run the real scripts of CaptureScripts.cs in node, against a fake browser (capture-harness.js):
    // the sound which comes out must be whole, in order, with left and right in their places.
    public class CaptureScriptTests : IDisposable
    {
        private readonly string m_Dir = Path.Combine(Path.GetTempPath(), "apolo-capture-" + Guid.NewGuid().ToString("N"));
        private static JsonElement? s_Result;
        private static readonly object s_Lock = new object();

        public CaptureScriptTests()
        {
            Directory.CreateDirectory(m_Dir);
        }

        public void Dispose()
        {
            try { Directory.Delete(m_Dir, true); } catch (Exception) { }
        }

        private JsonElement Run()
        {
            lock (s_Lock)
            {
                if (s_Result.HasValue)
                    return s_Result.Value;
                string page = Path.Combine(m_Dir, "page.js"), worklet = Path.Combine(m_Dir, "worklet.js");
                File.WriteAllText(page, CaptureScripts.Page);
                File.WriteAllText(worklet, CaptureScripts.Worklet);
                string harness = Path.Combine(AppContext.BaseDirectory, "capture-harness.js");
                ProcessStartInfo psi = new ProcessStartInfo("node")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                };
                psi.ArgumentList.Add(harness);
                psi.ArgumentList.Add(page);
                psi.ArgumentList.Add(worklet);
                Process p;
                try { p = Process.Start(psi); }
                catch (Exception ex) { throw new Exception("node (Node.js) is needed for this test and was not found: " + ex.Message); }
                var outputTask = p.StandardOutput.ReadToEndAsync();
                var errorTask = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(30000))
                {
                    try { p.Kill(true); } catch (Exception) { }
                    Assert.Fail("node did not finish in 30 s");
                }
                string output = outputTask.Result;
                string errors = errorTask.Result;
                JsonElement result = JsonDocument.Parse(output.Trim().Length > 0 ? output : "{\"error\":" + JsonSerializer.Serialize(errors) + "}").RootElement;
                if (result.TryGetProperty("error", out JsonElement error))
                    Assert.Fail("the capture script failed: " + error.GetString());
                s_Result = result;
                return result;
            }
        }

        [Fact]
        public void PageThreadRecorder_GivesTheWholeSound_InOrder()
        {
            JsonElement r = Run().GetProperty("script");
            Assert.InRange(r.GetProperty("samples").GetInt32(), 6 * 8192 - 2, 6 * 8192);   // all of it (one sample may wait for the next block)
            Assert.True(r.GetProperty("maxJump").GetInt32() < 100, "the sound jumps: " + r.GetProperty("maxJump"));
            Assert.True(r.GetProperty("lrOk").GetBoolean(), "left and right are mixed up");
            Assert.Contains("page thread", r.GetProperty("capmode").GetString());
            Assert.Equal(0, r.GetProperty("seams").GetInt32());
        }

        [Fact]
        public void ALostBlock_IsSmoothed_InsteadOfAClick()
        {
            JsonElement r = Run().GetProperty("lost");
            Assert.Equal(1, r.GetProperty("seams").GetInt32());
            // a cut 440 Hz wave jumps by ~4400 at the border; smoothed it stays far below
            Assert.True(r.GetProperty("maxJump").GetInt32() < 1500, "the lost block is a click: " + r.GetProperty("maxJump"));
        }

        [Fact]
        public void AudioThreadRecorder_IsLoadedFromTheSiteItself_AndTakesOver()
        {
            JsonElement r = Run().GetProperty("worklet");
            Assert.Equal("https://www.youtube.com" + CaptureScripts.WorkletPath, r.GetProperty("url").GetString());
            Assert.Contains("audio thread", r.GetProperty("capmode").GetString());
            Assert.InRange(r.GetProperty("samples").GetInt32(), 6 * 8192 - 2, 6 * 8192);
            Assert.True(r.GetProperty("maxJump").GetInt32() < 100, "the sound jumps: " + r.GetProperty("maxJump"));
            Assert.True(r.GetProperty("lrOk").GetBoolean(), "left and right are mixed up");
            Assert.Equal(0, r.GetProperty("scriptAfter").GetInt32());   // the old recorder is quiet: no sound twice
        }
    }
}
