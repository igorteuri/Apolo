using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Ares.Player;
using Xunit;

namespace Apolo.Tests
{
    // The leveler also works on the sound of the web tabs (YouTube...): the worklet of the page evens out the volume like the
    // leveler of the music, and the page script sends the sound of the page through it. Runs the real scripts in node.
    public class LevelWebScriptTests : IDisposable
    {
        private readonly string m_Dir = Path.Combine(Path.GetTempPath(), "apolo-levelweb-" + Guid.NewGuid().ToString("N"));
        private static JsonElement? s_Result;
        private static readonly object s_Lock = new object();

        public LevelWebScriptTests()
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
                ProcessStartInfo psi = new ProcessStartInfo("node")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                };
                psi.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "level-harness.js"));
                psi.ArgumentList.Add(page);
                psi.ArgumentList.Add(worklet);
                Process p;
                try { p = Process.Start(psi); }
                catch (Exception ex) { throw new Exception("node (Node.js) is needed for this test and was not found: " + ex.Message); }
                var outputTask = p.StandardOutput.ReadToEndAsync();
                var errorTask = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(60000))
                {
                    try { p.Kill(true); } catch (Exception) { }
                    Assert.Fail("node did not finish in 60 s");
                }
                string output = outputTask.Result;
                JsonElement result = JsonDocument.Parse(output.Trim().Length > 0 ? output : "{\"error\":" + JsonSerializer.Serialize(errorTask.Result) + "}").RootElement;
                if (result.TryGetProperty("error", out JsonElement error))
                    Assert.Fail("the level script failed: " + error.GetString());
                s_Result = result;
                return result;
            }
        }

        [Fact]
        public void QuietPage_IsRaised_ButNotMoreThanTheMaximum()
        {
            double gain = Run().GetProperty("quiet").GetProperty("gainDb").GetDouble();
            Assert.InRange(gain, 5.0, 9.6);
        }

        [Fact]
        public void LoudPage_IsLowered_AndNeverOverTheCeiling()
        {
            JsonElement r = Run().GetProperty("loud");
            Assert.InRange(r.GetProperty("gainDb").GetDouble(), -12.5, -3.0);
            Assert.True(r.GetProperty("peak").GetDouble() <= 0.951, "peak " + r.GetProperty("peak"));
        }

        [Fact]
        public void WhenOffOrNotConfigured_TheSoundIsUntouched()
        {
            foreach (string name in new[] { "off", "unconfigured" })
            {
                JsonElement r = Run().GetProperty(name);
                Assert.True(Math.Abs(r.GetProperty("gainDb").GetDouble()) < 0.2, name + " gain " + r.GetProperty("gainDb"));
            }
        }

        [Fact]
        public void PageScript_SendsTheSoundThroughTheLeveler_OnlyWhenOn()
        {
            JsonElement r = Run().GetProperty("page");
            Assert.True(r.GetProperty("offDirect").GetBoolean());
            Assert.False(r.GetProperty("onDirect").GetBoolean());
            Assert.True(r.GetProperty("toLevel").GetBoolean());
            Assert.True(r.GetProperty("worklet").GetBoolean());
            Assert.True(r.GetProperty("workletOutGoesOn").GetBoolean());
        }

        [Fact]
        public void ValuesAndSwitch_ReachTheWorklet()
        {
            JsonElement r = Run().GetProperty("page");
            Assert.True(r.GetProperty("sentOn").GetBoolean());
            Assert.True(r.GetProperty("offTold").GetBoolean());
        }

        [Fact]
        public void ProtectedForeignOrUnknownMedia_IsNotRouted()
        {
            JsonElement r = Run().GetProperty("page");
            Assert.False(r.GetProperty("drmHooked").GetBoolean());
            Assert.False(r.GetProperty("foreignHooked").GetBoolean());
            Assert.False(r.GetProperty("noSrcHooked").GetBoolean());
        }
    }
}
