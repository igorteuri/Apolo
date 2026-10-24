using Ares.Playing.Audio;
using Xunit;

namespace Apolo.Tests
{
    public class VolumeCurveTests
    {
        [Fact]
        public void EndsStayWhereTheyWere()
        {
            Assert.Equal(0f, VolumeCurve.ToGain(0));
            Assert.Equal(1f, VolumeCurve.ToGain(100));
        }

        [Fact]
        public void LowEndHasMoreRoomThanALine()
        {
            Assert.Equal(0.01f, VolumeCurve.ToGain(10), 4);
            Assert.True(VolumeCurve.ToGain(20) < 0.05f);
            // every step of the slider is audible, none is skipped
            for (int i = 1; i <= 100; i++)
                Assert.True(VolumeCurve.ToGain(i) > VolumeCurve.ToGain(i - 1));
        }

        [Fact]
        public void OutOfRangeIsClamped()
        {
            Assert.Equal(1f, VolumeCurve.ToGain(250));
            Assert.Equal(0f, VolumeCurve.ToGain(-5));
        }
    }
}
