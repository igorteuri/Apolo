using Avalonia.Input;
using Xunit;

namespace Apolo.Desktop.Tests
{
    public class KeyMapTests
    {
        [Fact]
        public void KeysBecomeTheCodesOfWindows()
        {
            Assert.Equal(0x41, KeyMap.ToCode(Key.A, KeyModifiers.None));
            Assert.Equal(0x5A, KeyMap.ToCode(Key.Z, KeyModifiers.None));
            Assert.Equal(0x30, KeyMap.ToCode(Key.D0, KeyModifiers.None));
            Assert.Equal(0x39, KeyMap.ToCode(Key.D9, KeyModifiers.None));
            Assert.Equal(0x70, KeyMap.ToCode(Key.F1, KeyModifiers.None));
            Assert.Equal(0x7B, KeyMap.ToCode(Key.F12, KeyModifiers.None));
            Assert.Equal(27, KeyMap.ToCode(Key.Escape, KeyModifiers.None));
            Assert.Equal(37, KeyMap.ToCode(Key.Left, KeyModifiers.None));
            Assert.Equal(39, KeyMap.ToCode(Key.Right, KeyModifiers.None));
            Assert.Equal(0x60, KeyMap.ToCode(Key.NumPad0, KeyModifiers.None));
        }

        [Fact]
        public void ModifiersAreAddedLikeInWindows()
        {
            Assert.Equal(0x41 | 0x20000, KeyMap.ToCode(Key.A, KeyModifiers.Control));
            Assert.Equal(0x70 | 0x10000 | 0x40000, KeyMap.ToCode(Key.F1, KeyModifiers.Shift | KeyModifiers.Alt));
        }

        [Fact]
        public void KeysWithoutACode_GiveZero()
        {
            Assert.Equal(0, KeyMap.ToCode(Key.LeftShift, KeyModifiers.Shift));
            Assert.Equal(0, KeyMap.ToCode(Key.None, KeyModifiers.None));
        }
    }
}
