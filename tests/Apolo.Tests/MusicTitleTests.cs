using Ares.Players;
using Xunit;

namespace Apolo.Tests
{
    // The title above the seek bar: a file with an empty tag must still show a name.
    public class MusicTitleTests
    {
        [Fact]
        public void EmptyTag_UsesTheFallbackName()
        {
            Assert.Equal("Fuga", MusicInfo.BuildLongTitle("", null, "  ", "Fuga"));
            Assert.Equal("Fuga", MusicInfo.BuildLongTitle(null, null, null, "Fuga"));
        }

        [Fact]
        public void OnlyThePartsWhichExist_AreJoined()
        {
            Assert.Equal("Banda - Álbum - Música 🐉", MusicInfo.BuildLongTitle("Banda", "Álbum", "Música 🐉", "x"));
            Assert.Equal("Banda - Música", MusicInfo.BuildLongTitle("Banda", "", "Música", "x"));
            Assert.Equal("Música", MusicInfo.BuildLongTitle("", "", " Música ", "x"));
        }

        [Fact]
        public void NoTagAndNoFallback_IsEmptyNotNull()
        {
            Assert.Equal("", MusicInfo.BuildLongTitle(null, null, null, null));
        }
    }
}
