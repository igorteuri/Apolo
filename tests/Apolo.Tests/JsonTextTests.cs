using Ares.Player;
using Xunit;

namespace Apolo.Tests
{
    public class JsonTextTests
    {
        [Fact]
        public void Null_BecomesEmptyString()
        {
            Assert.Equal("\"\"", JsonText.Quote(null));
        }

        [Fact]
        public void PlainText_IsOnlyQuoted()
        {
            Assert.Equal("\"Taverna do Dragão\"", JsonText.Quote("Taverna do Dragão"));
        }

        [Fact]
        public void QuotesAndBackslashes_AreEscaped()
        {
            Assert.Equal("\"a\\\"b\\\\c\"", JsonText.Quote("a\"b\\c"));
        }

        [Fact]
        public void NewLineAndTab_AreEscaped_AndCarriageReturnDropped()
        {
            Assert.Equal("\"a\\nb\\tc\"", JsonText.Quote("a\r\nb\tc"));
        }

        [Fact]
        public void OtherControlCharacters_UseUnicodeEscape()
        {
            Assert.Equal("\"\\u0001\"", JsonText.Quote("\u0001"));
        }
    }
}
