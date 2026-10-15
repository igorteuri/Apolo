using Ares.Player;
using Xunit;

namespace Apolo.Tests
{
    public class TrackTextTests
    {
        [Theory]
        [InlineData(0, "")]
        [InlineData(-5, "")]
        [InlineData(5, "0:05")]
        [InlineData(65, "1:05")]
        [InlineData(599.6, "10:00")]
        [InlineData(3600, "1:00:00")]
        [InlineData(3725, "1:02:05")]
        public void FormatDuration_ShowsMinutesAndSeconds(double seconds, string expected)
        {
            Assert.Equal(expected, TrackText.FormatDuration(seconds));
        }
    }
}
