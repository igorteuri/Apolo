using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Ares.Player;
using Xunit;

namespace Apolo.Tests
{
    // The remote control page (phones), the Stream Deck and Foundry read this text with JSON.parse:
    // a name with quotes or a decimal comma would break all of them at once.
    public class RemoteStateJsonTests
    {
        private static RemoteState Sample()
        {
            RemoteState s = new RemoteState
            {
                Playing = true,
                Paused = false,
                Title = "Música \"épica\" 🐉",
                Artist = "Kevin MacLeod",
                List = "Taverna\\Noite",
                Position = 12.5,
                Length = 184.25,
                RepeatMode = 2,
                VolOverall = 80, VolMusic = 65, VolSound = 100,
                Mode = 0
            };
            s.Modes.AddRange(new[] { "Taverna à noite", "Masmorra" });
            s.Tree.Add(new[] { 0, -1, 0 });
            s.Tree.Add(new[] { 1, 0, 1 });
            s.Elements.Add(new RemoteButton { Id = 7, Title = "Ação\ntensa", Active = true, Image = "dragão.png", Color = "#FFB627" });
            s.Elements.Add(new RemoteButton { Id = 8, Title = "Chuva" });
            RemoteGroup g = new RemoteGroup { Title = "Porão" };
            g.Buttons.Add(new RemoteButton { Id = 9, Title = "Ratos" });
            s.Groups.Add(g);
            s.Active.AddRange(new[] { 7, 42 });
            return s;
        }

        [Fact]
        public void TheText_IsValidJson_WithTheRightValues_EvenWithQuotesAccentsEmojiAndNewLines()
        {
            using (JsonDocument doc = JsonDocument.Parse(RemoteStateJson.Build(Sample())))
            {
                JsonElement r = doc.RootElement;
                Assert.True(r.GetProperty("playing").GetBoolean());
                Assert.Equal("Música \"épica\" 🐉", r.GetProperty("title").GetString());
                Assert.Equal("Taverna\\Noite", r.GetProperty("list").GetString());
                Assert.Equal(12.5, r.GetProperty("position").GetDouble());
                Assert.True(r.GetProperty("repeat").GetBoolean());
                Assert.Equal(2, r.GetProperty("repeatMode").GetInt32());
                Assert.Equal(65, r.GetProperty("vol").GetProperty("music").GetInt32());
                Assert.Equal(new[] { "Taverna à noite", "Masmorra" }, r.GetProperty("modes").EnumerateArray().Select(m => m.GetString()).ToArray());
                Assert.Equal(-1, r.GetProperty("tree")[0].GetProperty("p").GetInt32());
                Assert.Equal(1, r.GetProperty("tree")[1].GetProperty("d").GetInt32());
                JsonElement first = r.GetProperty("elements")[0];
                Assert.Equal("Ação\ntensa", first.GetProperty("title").GetString());
                Assert.Equal("dragão.png", first.GetProperty("img").GetString());
                Assert.Equal("#FFB627", first.GetProperty("color").GetString());
                Assert.True(first.GetProperty("active").GetBoolean());
                Assert.False(r.GetProperty("elements")[1].TryGetProperty("img", out _));   // no picture: no field
                Assert.Equal("Ratos", r.GetProperty("groups")[0].GetProperty("elements")[0].GetProperty("title").GetString());
                Assert.Equal(new[] { 7, 42 }, r.GetProperty("active").EnumerateArray().Select(a => a.GetInt32()).ToArray());
            }
        }

        [Fact]
        public void Numbers_UseThePoint_AlsoOnAPortugueseWindows()
        {
            CultureInfo before = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("pt-BR");
                string text = RemoteStateJson.Build(Sample());
                Assert.Contains("\"position\":12.5", text);
                Assert.Contains("\"length\":184.3", text);
                using (JsonDocument.Parse(text)) { }
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = before;
            }
        }

        [Fact]
        public void NothingOpen_IsStillValid_AndNoFolderIsOpen()
        {
            using (JsonDocument doc = JsonDocument.Parse(RemoteStateJson.Build(new RemoteState())))
            {
                JsonElement r = doc.RootElement;
                Assert.False(r.GetProperty("playing").GetBoolean());
                Assert.Equal(-1, r.GetProperty("mode").GetInt32());
                Assert.Equal(0, r.GetProperty("elements").GetArrayLength());
                Assert.Equal(0, r.GetProperty("groups").GetArrayLength());
                Assert.Equal("", r.GetProperty("title").GetString());
            }
        }
    }
}
