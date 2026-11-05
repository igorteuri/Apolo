using System.Linq;
using Ares.Player;
using Xunit;

namespace Apolo.Tests
{
    // The buttons which play a YouTube playlist: what the user pastes, and what the page answers.
    public class YouTubePlaylistTests
    {
        private const string Id = "PLrAXtmErZgOeiKm4sgNOknGvNjby9efdf";

        [Theory]
        [InlineData("https://www.youtube.com/playlist?list=" + Id)]
        [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ&list=" + Id + "&index=3")]
        [InlineData("https://youtu.be/dQw4w9WgXcQ?list=" + Id)]
        [InlineData("https://music.youtube.com/playlist?list=" + Id)]
        [InlineData("  www.youtube.com/playlist?list=" + Id + "  ")]
        [InlineData("m.youtube.com/playlist?list=" + Id)]
        [InlineData(Id)]
        public void ExtractId_FindsThePlaylist(string text)
        {
            Assert.Equal(Id, YouTubePlaylist.ExtractId(text));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("música épica 🎵")]
        [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ")]       // a video without a playlist
        [InlineData("https://example.com/playlist?list=" + Id)]            // another site
        [InlineData("https://evilyoutube.com/playlist?list=" + Id)]        // a look-alike site
        [InlineData("https://www.youtube.com/playlist?list=curto")]        // too short to be an id
        [InlineData("PLcurto")]
        public void ExtractId_RefusesWhatIsNotAPlaylist(string text)
        {
            Assert.Null(YouTubePlaylist.ExtractId(text));
        }

        [Fact]
        public void UrlFor_RoundTrips()
        {
            Assert.Equal(Id, YouTubePlaylist.IdOfButtonUrl(YouTubePlaylist.UrlFor(Id)));
            Assert.StartsWith("https://www.youtube.com/", YouTubePlaylist.UrlFor(Id));
        }

        [Theory]
        [InlineData("Minha lista - YouTube", "Minha lista")]
        [InlineData("(3) Minha lista - YouTube", "Minha lista")]
        [InlineData("Ação 🎵 épica", "Ação 🎵 épica")]
        [InlineData("", "")]
        [InlineData(null, "")]
        public void CleanTitle_RemovesTheSiteName(string page, string expected)
        {
            Assert.Equal(expected, YouTubePlaylist.CleanTitle(page));
        }

        [Fact]
        public void Snapshot_ReadsThePageAnswer_PlainOrWrappedAsAString()
        {
            string json = "{\"page\":\"watch\",\"list\":\"" + Id + "\",\"items\":[\"Um\",\"Dois ação 🎵\",\"Três\"],\"index\":1,\"paused\":false,\"ended\":false,\"title\":\"Dois ação 🎵 - YouTube\"}";
            string wrapped = System.Text.Json.JsonSerializer.Serialize(json);   // ExecuteScriptAsync gives the string of the script as JSON
            foreach (string raw in new[] { json, wrapped })
            {
                YouTubePlaylist.Snapshot s = YouTubePlaylist.Snapshot.Parse(raw);
                Assert.NotNull(s);
                Assert.Equal(new[] { "Um", "Dois ação 🎵", "Três" }, s.Items.ToArray());
                Assert.Equal(1, s.Index);
                Assert.False(s.Paused);
                Assert.Equal("Dois ação 🎵", s.Title);
                Assert.True(s.IsPlaying(Id));
                Assert.False(s.IsPlaying("outra"));
            }
        }

        [Fact]
        public void Snapshot_IndexOutsideTheListIsUnknown_AndMissingFieldsAreSafe()
        {
            YouTubePlaylist.Snapshot s = YouTubePlaylist.Snapshot.Parse("{\"page\":\"watch\",\"items\":[\"a\"],\"index\":5}");
            Assert.Equal(-1, s.Index);
            Assert.True(s.Paused);   // when unsure it counts as not playing
            Assert.False(s.Ended);
            Assert.False(s.IsPlaying(Id));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("null")]
        [InlineData("não é json")]
        [InlineData("[1,2]")]
        [InlineData("\"texto solto\"")]
        public void Snapshot_RefusesGarbage(string raw)
        {
            Assert.Null(YouTubePlaylist.Snapshot.Parse(raw));
        }

        [Fact]
        public void Snapshot_ReadsPositionLengthAndVolume_AndSurvivesLiveStreams()
        {
            YouTubePlaylist.Snapshot s = YouTubePlaylist.Snapshot.Parse("{\"page\":\"watch\",\"items\":[\"a\"],\"index\":0,\"pos\":12.5,\"len\":200,\"vol\":0.4}");
            Assert.Equal(12.5, s.Position);
            Assert.Equal(200, s.Duration);
            Assert.Equal(0.4, s.Volume, 3);
            YouTubePlaylist.Snapshot live = YouTubePlaylist.Snapshot.Parse("{\"page\":\"watch\",\"pos\":-3,\"len\":0}");
            Assert.Equal(0, live.Position);
            Assert.Equal(0, live.Duration);
            Assert.Equal(-1, live.Volume);   // unknown
        }

        [Fact]
        public void SeekAndVolumeScripts_AreSafe()
        {
            Assert.Contains("currentTime=12.5;", YouTubePlaylist.SeekScript(12.5));
            Assert.Contains("volume=0.35;", YouTubePlaylist.VolumeScript(35));
            Assert.Contains("volume=1;", YouTubePlaylist.VolumeScript(500));
            Assert.Contains("volume=0;", YouTubePlaylist.VolumeScript(-9));
        }

        [Fact]
        public void AllScripts_AreValidJavaScript()
        {
            // a typo in a script only shows in the real page: node at least checks the syntax
            string[] scripts =
            {
                YouTubePlaylist.StartScript, YouTubePlaylist.ReadScript, YouTubePlaylist.PauseScript, YouTubePlaylist.PlayScript,
                YouTubePlaylist.NextScript, YouTubePlaylist.PreviousScript, YouTubePlaylist.JumpScript(3),
                YouTubePlaylist.SeekScript(1.5), YouTubePlaylist.VolumeScript(50)
            };
            string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "apolo-ytjs-" + System.Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(dir);
            try
            {
                for (int i = 0; i < scripts.Length; i++)
                {
                    string file = System.IO.Path.Combine(dir, "s" + i + ".js");
                    System.IO.File.WriteAllText(file, "new Function(" + System.Text.Json.JsonSerializer.Serialize(scripts[i]) + ");");
                    System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo("node") { RedirectStandardError = true, UseShellExecute = false };
                    psi.ArgumentList.Add(file);
                    using (System.Diagnostics.Process p = System.Diagnostics.Process.Start(psi))
                    {
                        string err = p.StandardError.ReadToEnd();
                        p.WaitForExit(20000);
                        Assert.True(p.ExitCode == 0, "script " + i + " is not valid JavaScript: " + err);
                    }
                }
            }
            finally
            {
                try { System.IO.Directory.Delete(dir, true); } catch (System.Exception) { }
            }
        }

        [Fact]
        public void JumpScript_UsesTheNumber()
        {
            Assert.Contains("n[7]", YouTubePlaylist.JumpScript(7));
        }
    }
}
