using Ares.Player;
using Xunit;

namespace Apolo.Tests
{
    public class PackCatalogTests
    {
        private const string Url = "https://github.com/igorteuri/Apolo/releases/download/pacotes/pacotes.ini";

        [Fact]
        public void ReadsPacksAndBuildsTheAddressBesideTheList()
        {
            string text = "# lista\r\n[inicial]\r\ntitulo = Pacote inicial\r\ndescricao = Pastas prontas\r\narquivo = pacote inicial.apkg\r\nbytes = 2097152\r\nsha256 = ABCD\r\n\r\n[outro]\r\ntitulo = Outro\r\narquivo = https://exemplo.com/outro.apkg\r\n";
            var packs = PackCatalog.Parse(text, Url);
            Assert.Equal(2, packs.Count);
            Assert.Equal("inicial", packs[0].Id);
            Assert.Equal("Pacote inicial", packs[0].Title);
            Assert.Equal("https://github.com/igorteuri/Apolo/releases/download/pacotes/pacote%20inicial.apkg", packs[0].Url);
            Assert.Equal(2097152, packs[0].Bytes);
            Assert.Equal("ABCD", packs[0].Sha256);
            Assert.Equal("https://exemplo.com/outro.apkg", packs[1].Url);
        }

        [Fact]
        public void LeavesOutPacksWithoutTitleOrWithUnsafeFiles()
        {
            string text = "[a]\narquivo = a.apkg\n[b]\ntitulo = B\narquivo = ..\b.apkg\n[c]\ntitulo = C\narquivo = http://inseguro.com/c.apkg\n[d]\ntitulo = D\narquivo = pasta/d.apkg\n[e]\ntitulo = E\narquivo = e.apkg\n";
            var packs = PackCatalog.Parse(text, Url);
            Assert.Single(packs);
            Assert.Equal("e", packs[0].Id);
        }

        [Fact]
        public void EmptyTextGivesNoPacks()
        {
            Assert.Empty(PackCatalog.Parse("", Url));
            Assert.Empty(PackCatalog.Parse(null, Url));
        }

        [Theory]
        [InlineData(0, "")]
        [InlineData(500 * 1024, "500 KB")]
        [InlineData(5L * 1024 * 1024, "5.0 MB")]
        [InlineData(120L * 1024 * 1024, "120 MB")]
        [InlineData(3L * 1024 * 1024 * 1024, "3.0 GB")]
        public void FormatsTheSize(long bytes, string expected)
        {
            Assert.Equal(expected, PackCatalog.FormatSize(bytes));
        }

        [Fact]
        public void SafeNameKeepsOnlyFileNameCharacters()
        {
            Assert.Equal("pacote-inicial", PackCatalog.SafeName("pacote inicial"));
            Assert.Equal("pacote", PackCatalog.SafeName("///"));
        }
    }

    public class ProjectCreditsTests
    {
        [Fact]
        public void ListsSoundsWithAuthorAndLicenceAndLeavesOutUntaggedOnes()
        {
            var entries = new[]
            {
                new CreditEntry { File = "Kevin/Heroic Age.mp3", Title = "Heroic Age", Artist = "Kevin MacLeod", Copyright = "CC BY 4.0" },
                new CreditEntry { File = "x/sem-tag.mp3" },
                new CreditEntry { File = "Kevin/Heroic Age.mp3", Title = "Heroic Age", Artist = "Kevin MacLeod", Copyright = "CC BY 4.0" },
            };
            string text = ProjectCredits.Build("Músicas de Kevin MacLeod", entries);
            Assert.StartsWith("Músicas de Kevin MacLeod", text);
            Assert.Contains("  - Heroic Age - Kevin MacLeod [CC BY 4.0]", text);
            Assert.DoesNotContain("sem-tag", text);
            Assert.Equal(1, System.Text.RegularExpressions.Regex.Matches(text, "Heroic Age").Count);
        }

        [Fact]
        public void NothingToSayGivesEmptyText()
        {
            Assert.Equal("", ProjectCredits.Build("  ", new CreditEntry[0]));
            Assert.Equal("", ProjectCredits.Build(null, null));
        }

        [Fact]
        public void CreditsFileSitsBesideTheProject()
        {
            Assert.Equal(@"C:\p\jogo.apolo.creditos.txt", ProjectCredits.FileFor(@"C:\p\jogo.apolo"));
        }
    }
}
