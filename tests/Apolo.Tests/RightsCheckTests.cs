using Ares.Player;
using Ares.Tags;
using Xunit;

namespace Apolo.Tests
{
    public class RightsCheckTests
    {
        private static char Quick(string title, string copyright, string path)
        {
            AudioTagInfo tag = new AudioTagInfo();
            tag.title = title;
            tag.copyright = copyright;
            char code;
            string why;
            RightsCheck.Quick(tag, path, out code, out why);
            return code;
        }

        [Fact]
        public void FreeLicence_IsGreen()
        {
            Assert.Equal('G', Quick("Song", "Creative Commons BY 4.0", "song.mp3"));
        }

        [Fact]
        public void FreeLicenceInFileName_IsGreen()
        {
            Assert.Equal('G', Quick("Song", "", "Kevin MacLeod - Song.mp3"));
        }

        [Fact]
        public void NonCommercialLicence_IsYellow()
        {
            Assert.Equal('Y', Quick("Song", "CC BY-NC 4.0 Creative Commons", "song.mp3"));
        }

        [Fact]
        public void NothingMentioned_IsUnknown()
        {
            Assert.Equal('\0', Quick("Some Song", "(c) Some Label", "some song.mp3"));
        }

        [Fact]
        public void Rank_DistinguishesTheThreeColours()
        {
            Assert.NotEqual(RightsCheck.Rank('G'), RightsCheck.Rank('Y'));
            Assert.NotEqual(RightsCheck.Rank('Y'), RightsCheck.Rank('R'));
            Assert.NotEqual(RightsCheck.Rank('G'), RightsCheck.Rank('R'));
        }
    }
}
