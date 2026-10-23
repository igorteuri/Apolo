using Avalonia.Input;

namespace Apolo.Desktop
{
    /// <summary>
    /// The keys of the buttons are saved as the key codes of Windows (the same in every version of the program),
    /// with the modifiers added: Shift 0x10000, Control 0x20000, Alt 0x40000.
    /// </summary>
    public static class KeyMap
    {
        /// <summary>The code of the key and its modifiers; 0 for a key which no button can have.</summary>
        public static int ToCode(Key key, KeyModifiers modifiers)
        {
            int code = VirtualKey(key);
            if (code == 0)
                return 0;
            if ((modifiers & KeyModifiers.Shift) != 0)
                code |= 0x10000;
            if ((modifiers & KeyModifiers.Control) != 0)
                code |= 0x20000;
            if ((modifiers & KeyModifiers.Alt) != 0)
                code |= 0x40000;
            return code;
        }

        public static int VirtualKey(Key key)
        {
            if (key >= Key.A && key <= Key.Z)
                return 'A' + (key - Key.A);
            if (key >= Key.D0 && key <= Key.D9)
                return '0' + (key - Key.D0);
            if (key >= Key.NumPad0 && key <= Key.NumPad9)
                return 0x60 + (key - Key.NumPad0);
            if (key >= Key.F1 && key <= Key.F12)
                return 0x70 + (key - Key.F1);
            switch (key)
            {
                case Key.Back: return 8;
                case Key.Tab: return 9;
                case Key.Return: return 13;
                case Key.Escape: return 27;
                case Key.Space: return 32;
                case Key.PageUp: return 33;
                case Key.PageDown: return 34;
                case Key.End: return 35;
                case Key.Home: return 36;
                case Key.Left: return 37;
                case Key.Up: return 38;
                case Key.Right: return 39;
                case Key.Down: return 40;
                case Key.Insert: return 45;
                case Key.Delete: return 46;
                case Key.Multiply: return 0x6A;
                case Key.Add: return 0x6B;
                case Key.Subtract: return 0x6D;
                case Key.Decimal: return 0x6E;
                case Key.Divide: return 0x6F;
                case Key.OemPlus: return 0xBB;
                case Key.OemComma: return 0xBC;
                case Key.OemMinus: return 0xBD;
                case Key.OemPeriod: return 0xBE;
                default: return 0;
            }
        }
    }
}
