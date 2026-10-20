using Ares.Player;
using Xunit;

namespace Apolo.Tests
{
    public class ImageBankIndexTests
    {
        private static readonly ImageBankIndex Index = ImageBankIndex.Parse(
            "# comentario\n" +
            "floresta|floresta,bosque,pinheiro,mata,forest\n" +
            "sol|sol,dia,ensolarado,sun,day\n" +
            "dragao|dragao,dragoes,dragon\n" +
            "ovni|ovni,disco voador,ufo\n" +
            "rato|rato,ratos,esgoto,rat\n" +
            "mar|mar,oceano,onda,sea\n" +
            "musica|musica,melodia,trilha,music\n" +
            "caveira|caveira,morte,esqueleto,skull\n");

        [Fact]
        public void Parse_ReadsEntriesAndSkipsComments()
        {
            Assert.Equal(8, Index.Entries.Count);
            Assert.Equal("floresta", Index.Entries[0].Id);
            Assert.Equal("floresta", Index.Entries[0].Label);
        }

        [Theory]
        [InlineData("Floresta", "floresta")]
        [InlineData("FLORESTA SOMBRIA", "floresta")]
        [InlineData("Dragão", "dragao")]
        [InlineData("Dragões de fogo", "dragao")]
        [InlineData("Os ratos do esgoto", "rato")]
        [InlineData("Disco Voador", "ovni")]
        [InlineData("Música de fundo", "musica")]
        [InlineData("Esqueleto 3", "caveira")]
        [InlineData("forest ambience", "floresta")]
        public void Match_FindsThePictureByTheWords(string title, string expected)
        {
            BankEntry entry = Index.Match(title);
            Assert.NotNull(entry);
            Assert.Equal(expected, entry.Id);
        }

        [Theory]
        [InlineData("Combate final")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("Marte")]
        public void Match_NothingFitsGivesNull(string title)
        {
            Assert.Null(Index.Match(title));
        }

        [Fact]
        public void Match_TheEarlierWordOfTheTitleWins()
        {
            Assert.Equal("floresta", Index.Match("Floresta Dia").Id);
            Assert.Equal("sol", Index.Match("Dia na Floresta").Id);
        }

        [Fact]
        public void Search_ByBeginningOfAWord_AndEmptyGivesAll()
        {
            Assert.Equal(8, Index.Search("").Count);
            Assert.Single(Index.Search("drag"));
            Assert.Single(Index.Search("OCEANO"));
            Assert.Empty(Index.Search("xyz"));
        }
    }
}
