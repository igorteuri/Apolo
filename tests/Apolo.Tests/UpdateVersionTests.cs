using Ares.Player;
using Xunit;

namespace Apolo.Tests
{
    public class UpdateVersionTests
    {
        [Theory]
        [InlineData("0.85", "0.81", true)]
        [InlineData("v0.85", "0.81.124.0", true)]
        [InlineData("Apolo 1.0", "0.99", true)]
        [InlineData("0.81", "0.81.200.0", false)]
        [InlineData("0.80", "0.81", false)]
        [InlineData("0.100", "0.99", true)]
        [InlineData("lixo", "0.81", false)]
        [InlineData("", "0.81", false)]
        [InlineData("0.90", "sem versao", false)]
        public void IsNewer_ComparesReleases(string latest, string current, bool expected)
        {
            Assert.Equal(expected, UpdateVersion.IsNewer(latest, current));
        }

        [Fact]
        public void ReadInfo_VersionAndUrl()
        {
            string version, url;
            Assert.True(UpdateVersion.TryReadInfo("{ \"version\": \"0.85\", \"url\": \"https:\/\/exemplo.com\/apolo\" }", out version, out url));
            Assert.Equal("0.85", version);
            Assert.Equal("https://exemplo.com/apolo", url);
        }

        [Fact]
        public void ReadInfo_UrlIsOptional_AndGarbageIsRejected()
        {
            string version, url;
            Assert.True(UpdateVersion.TryReadInfo("{\"version\":\"0.9\"}", out version, out url));
            Assert.Null(url);
            Assert.False(UpdateVersion.TryReadInfo("<html>nada</html>", out version, out url));
            Assert.False(UpdateVersion.TryReadInfo(null, out version, out url));
        }
    }
}
