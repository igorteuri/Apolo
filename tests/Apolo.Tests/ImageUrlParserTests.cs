using System.Linq;
using System.Text;
using Ares.Player;
using Xunit;

namespace Apolo.Tests
{
    public class ImageUrlParserTests
    {
        private static string First(string text)
        {
            return ImageUrlParser.Candidates(text).FirstOrDefault();
        }

        [Theory]
        [InlineData("https://exemplo.com/img/dragao.png", "https://exemplo.com/img/dragao.png")]
        [InlineData("   https://exemplo.com/a.jpg   ", "https://exemplo.com/a.jpg")]
        [InlineData("Olha essa imagem linda: https://exemplo.com/a.webp, achei ontem!", "https://exemplo.com/a.webp")]
        [InlineData("https:// exemplo.com / pasta / a.png", "https://exemplo.com/pasta/a.png")]
        [InlineData("www.exemplo.com/a.png", "https://www.exemplo.com/a.png")]
        [InlineData("i.imgur.com/AbC123.jpg", "https://i.imgur.com/AbC123.jpg")]
        [InlineData("htps://exemplo.com/a.png", "https://exemplo.com/a.png")]
        [InlineData("http:/exemplo.com/a.png", "http://exemplo.com/a.png")]
        [InlineData("![dragao](https://exemplo.com/d.png) legenda", "https://exemplo.com/d.png")]
        [InlineData("<img src=\"https://exemplo.com/d.gif\" alt=\"x\">", "https://exemplo.com/d.gif")]
        [InlineData("https://exemplo.com/a.png?x=1&amp;y=2", "https://exemplo.com/a.png?x=1&y=2")]
        public void FindsTheLink(string pasted, string expected)
        {
            Assert.Equal(expected, First(pasted));
        }

        [Fact]
        public void LinkWithoutExtension_IsKept()
        {
            Assert.Equal("https://i.imgur.com/AbC123", First("https://i.imgur.com/AbC123"));
        }

        [Fact]
        public void SpaceInTheNameOfTheFile_GivesTheEncodedAndTheJoinedVersion()
        {
            var found = ImageUrlParser.Candidates("https://exemplo.com/minha imagem.png");
            Assert.Equal("https://exemplo.com/minha%20imagem.png", found[0]);
            Assert.Contains("https://exemplo.com/minhaimagem.png", found);
        }

        [Fact]
        public void SearchPageLink_GivesTheRealPictureFirst()
        {
            string pasted = "https://www.google.com/imgres?imgurl=https%3A%2F%2Fexemplo.com%2Ffoto.jpg&imgrefurl=https%3A%2F%2Fpagina.com&tbnid=1";
            Assert.Equal("https://exemplo.com/foto.jpg", First(pasted));
        }

        [Fact]
        public void SeveralLinks_PictureComesBeforeOther()
        {
            var found = ImageUrlParser.Candidates("primeiro https://b.com/y e depois https://a.com/x.png");
            Assert.Equal("https://a.com/x.png", found[0]);
            Assert.Equal("https://b.com/y", found[1]);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("nenhum link aqui, so texto 3.5")]
        public void NoLink_GivesNothing(string pasted)
        {
            Assert.Empty(ImageUrlParser.Candidates(pasted));
        }

        [Fact]
        public void PageImage_OgImage_RelativeAndAbsolute_AnyOrderOfAttributes()
        {
            Assert.Equal("https://site.com/img/capa.jpg",
                ImageUrlParser.FindPageImage("<html><head><meta property=\"og:image\" content=\"/img/capa.jpg\"></head></html>", "https://site.com/p/1"));
            Assert.Equal("https://s.com/c.png",
                ImageUrlParser.FindPageImage("<meta content=\"https://s.com/c.png\" property='og:image'>", "https://site.com/p/1"));
            Assert.Null(ImageUrlParser.FindPageImage("<html></html>", "https://site.com"));
        }

        [Fact]
        public void Detect_RecognizesTheFormatsByTheirFirstBytes()
        {
            Assert.Equal("png", ImageBytes.Detect(new byte[] { 0x89, 0x50, 0x4E, 0x47, 1, 2 }));
            Assert.Equal("jpg", ImageBytes.Detect(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 1 }));
            Assert.Equal("gif", ImageBytes.Detect(Encoding.ASCII.GetBytes("GIF89a...")));
            Assert.Equal("webp", ImageBytes.Detect(Encoding.ASCII.GetBytes("RIFF1234WEBPVP8 ")));
            Assert.Equal("html", ImageBytes.Detect(Encoding.UTF8.GetBytes("<!DOCTYPE html><html>")));
            Assert.Equal("svg", ImageBytes.Detect(Encoding.UTF8.GetBytes("<svg xmlns=\"x\">")));
            Assert.Null(ImageBytes.Detect(new byte[] { 1, 2, 3 }));
        }
    }
}
