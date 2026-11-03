using System.IO;
using Ares.Player;
using Xunit;

namespace Apolo.Tests
{
    // The subfolders, colors and pictures find a folder by its name: two folders must never share one.
    public class FolderNamesTests
    {
        [Fact]
        public void FreeName_IsKept_WithoutTheSpacesAround()
        {
            Assert.Equal("Taverna", FolderNames.Unique("  Taverna ", new[] { "Masmorra" }));
            Assert.Equal("Taverna", FolderNames.Unique("Taverna", new string[0]));
        }

        [Fact]
        public void TakenName_GetsTheNextFreeNumber_IgnoringCaseAndAccentedCapitals()
        {
            Assert.Equal("Ação 2", FolderNames.Unique("Ação", new[] { "AÇÃO" }));
            Assert.Equal("taverna 3", FolderNames.Unique("taverna", new[] { "Taverna", "Taverna 2", null }));
            Assert.Equal("Dragões 🐉 2", FolderNames.Unique("Dragões 🐉", new[] { "Dragões 🐉" }));
        }

        [Fact]
        public void Rename_RefusesEmptyAndTakenNames_ButAcceptsChangingTheCase()
        {
            string[] others = { "Masmorra", "Floresta" };
            Assert.True(FolderNames.CanRename("Cidade", "Taverna", others));
            Assert.True(FolderNames.CanRename("TAVERNA", "Taverna", others));
            Assert.False(FolderNames.CanRename("masmorra ", "Taverna", others));
            Assert.False(FolderNames.CanRename("   ", "Taverna", others));
            Assert.False(FolderNames.CanRename(null, "Taverna", others));
        }

        [Fact]
        public void Suggestion_IsTheFolderOfTheFiles_OrTheFileOnARootDrive()
        {
            string folder = Path.Combine(Path.GetTempPath(), "Trilha da Masmorra");
            Assert.Equal("Trilha da Masmorra", FolderNames.SuggestFor(new[] { Path.Combine(folder, "a.mp3"), Path.Combine(folder, "b.mp3") }));
            Assert.Equal("chuva", FolderNames.SuggestFor(new[] { Path.Combine(Path.GetPathRoot(Path.GetTempPath()), "chuva.mp3") }));
            Assert.Equal("", FolderNames.SuggestFor(new string[0]));
            Assert.Equal("", FolderNames.SuggestFor(null));
        }
    }
}
