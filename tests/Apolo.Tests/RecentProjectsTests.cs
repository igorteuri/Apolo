using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ares.Player;
using Xunit;

namespace Apolo.Tests
{
    // The quick project switch in the status bar lists the recent projects which can really be opened.
    public class RecentProjectsTests
    {
        private static KeyValuePair<string, string> P(string path, string title)
        {
            return new KeyValuePair<string, string>(path, title);
        }

        private static readonly string Dir = Path.Combine(Path.GetTempPath(), "Campanhas");

        [Fact]
        public void TheOpenProject_TheMissingOnes_AndRepetitions_AreLeftOut()
        {
            string mesa = Path.Combine(Dir, "Mesa Pessoal.apolo"), ose = Path.Combine(Dir, "OSE.apolo"), gone = Path.Combine(Dir, "Apagado.apolo");
            var recent = new[] { P(mesa, "Mesa Pessoal"), P(ose, "OSE 🐉"), P(gone, "Apagado"), P(ose.ToUpperInvariant(), "OSE de novo") };
            var list = RecentProjects.ForSwitch(recent, mesa, p => !p.Contains("Apagado"), 10);
            Assert.Equal(new[] { "OSE 🐉" }, list.Select(e => e.Title).ToArray());
            Assert.Equal(ose, list[0].Path);
        }

        [Fact]
        public void WithoutTitle_TheFileNameIsShown_AndTheListIsLimited()
        {
            var recent = Enumerable.Range(1, 15).Select(i => P(Path.Combine(Dir, "Aventura " + i + ".apolo"), i == 1 ? "  " : "Aventura " + i)).ToList();
            var list = RecentProjects.ForSwitch(recent, null, p => true, 10);
            Assert.Equal(10, list.Count);
            Assert.Equal("Aventura 1", list[0].Title);
        }

        [Fact]
        public void EmptyAndBrokenEntries_DoNotBreakTheList()
        {
            var recent = new[] { P(null, "x"), P("   ", "y"), P(Path.Combine(Dir, "Ação.apolo"), "Ação") };
            var list = RecentProjects.ForSwitch(recent, "", p => true, 10);
            Assert.Single(list);
            Assert.Equal("Ação", list[0].Title);
        }
    }
}
