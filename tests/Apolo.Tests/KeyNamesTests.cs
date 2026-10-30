using Ares.Player;
using Xunit;

namespace Apolo.Tests
{
    public class KeyNamesTests
    {
        private const int Shift = 0x10000, Ctrl = 0x20000, Alt = 0x40000;

        [Theory]
        [InlineData(0, "")]
        [InlineData(0x78, "F9")]
        [InlineData(Ctrl | 0xBC, "Ctrl+,")]
        [InlineData(Ctrl | 0x27, "Ctrl+Seta direita")]
        [InlineData(Alt | 0x25, "Alt+Seta esquerda")]
        [InlineData(Ctrl | Shift | 0x09, "Ctrl+Shift+Tab")]
        [InlineData(Ctrl | Alt | 0x50, "Ctrl+Alt+P")]
        [InlineData(0x31, "1")]
        [InlineData(0x63, "Num 3")]
        [InlineData(0x20, "Espaço")]
        public void Portuguese(int keys, string expected)
        {
            Assert.Equal(expected, KeyNames.Format(keys, true, null));
        }

        [Fact]
        public void EnglishNames()
        {
            Assert.Equal("Ctrl+Right arrow", KeyNames.Format(Ctrl | 0x27, false, null));
        }

        [Fact]
        public void KeyboardOfTheUserNamesPunctuation()
        {
            // a Brazilian keyboard: the key of ";" in the US layout gives "Ç"
            Assert.Equal("Ç", KeyNames.Format(0xBA, true, code => code == 0xBA ? "Ç" : null));
        }

        [Fact]
        public void UnknownKeyUsesTheFallback()
        {
            Assert.Equal("VolumeUp", KeyNames.Format(0xAF, true, code => "VolumeUp"));
            Assert.Equal("#175", KeyNames.Format(0xAF, true, null));
        }
    }
}
