/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 Names of keys for people: "Ctrl+," instead of "Ctrl+Oemcomma", "Ctrl+Seta direita" instead of "Ctrl+Right".
 Works on the numbers of System.Windows.Forms.Keys, without any window code, so that it can be tested automatically.
*/
using System;
using System.Collections.Generic;

namespace Ares.Player
{
    internal static class KeyNames
    {
        private const int KeyCodeMask = 0xFFFF, ShiftBit = 0x10000, ControlBit = 0x20000, AltBit = 0x40000;

        // key code -> Portuguese name, English name
        private static readonly Dictionary<int, string[]> s_Names = new Dictionary<int, string[]>
        {
            { 0x08, new[] { "Backspace", "Backspace" } },
            { 0x09, new[] { "Tab", "Tab" } },
            { 0x0D, new[] { "Enter", "Enter" } },
            { 0x13, new[] { "Pause", "Pause" } },
            { 0x1B, new[] { "Esc", "Esc" } },
            { 0x20, new[] { "Espaço", "Space" } },
            { 0x21, new[] { "Page Up", "Page Up" } },
            { 0x22, new[] { "Page Down", "Page Down" } },
            { 0x23, new[] { "End", "End" } },
            { 0x24, new[] { "Home", "Home" } },
            { 0x25, new[] { "Seta esquerda", "Left arrow" } },
            { 0x26, new[] { "Seta para cima", "Up arrow" } },
            { 0x27, new[] { "Seta direita", "Right arrow" } },
            { 0x28, new[] { "Seta para baixo", "Down arrow" } },
            { 0x2D, new[] { "Insert", "Insert" } },
            { 0x2E, new[] { "Delete", "Delete" } },
            { 0x6A, new[] { "Num *", "Num *" } },
            { 0x6B, new[] { "Num +", "Num +" } },
            { 0x6D, new[] { "Num -", "Num -" } },
            { 0x6E, new[] { "Num ,", "Num ." } },
            { 0x6F, new[] { "Num /", "Num /" } },
            // punctuation (US layout); the keyboard of the user decides first, see Name
            { 0xBA, new[] { ";", ";" } },
            { 0xBB, new[] { "=", "=" } },
            { 0xBC, new[] { ",", "," } },
            { 0xBD, new[] { "-", "-" } },
            { 0xBE, new[] { ".", "." } },
            { 0xBF, new[] { "/", "/" } },
            { 0xC0, new[] { "'", "`" } },
            { 0xDB, new[] { "[", "[" } },
            { 0xDC, new[] { "\\", "\\" } },
            { 0xDD, new[] { "]", "]" } },
            { 0xDE, new[] { "'", "'" } },
        };

        /// <summary>The text of a key with its modifiers ("Ctrl+Shift+F5"); "" for none. "fallback" gives the character of a key on the keyboard of the user (or null).</summary>
        public static string Format(int keys, bool portuguese, Func<int, string> fallback)
        {
            int code = keys & KeyCodeMask;
            if (code == 0)
                return "";
            string name = Name(code, portuguese, fallback);
            string text = "";
            if ((keys & ControlBit) != 0) text += "Ctrl+";
            if ((keys & ShiftBit) != 0) text += "Shift+";
            if ((keys & AltBit) != 0) text += "Alt+";
            return text + name;
        }

        private static string Name(int code, bool portuguese, Func<int, string> fallback)
        {
            // punctuation keys differ between keyboards (Ç, ´, ~ in Brazil): the character of the real layout wins
            bool punctuation = code >= 0xBA && code <= 0xE2;
            if (punctuation && fallback != null)
            {
                string typed = fallback(code);
                if (!string.IsNullOrEmpty(typed))
                    return typed;
            }
            string[] names;
            if (s_Names.TryGetValue(code, out names))
                return portuguese ? names[0] : names[1];
            if (code >= 0x30 && code <= 0x39)                // D0..D9
                return ((char)code).ToString();
            if (code >= 0x41 && code <= 0x5A)                // A..Z
                return ((char)code).ToString();
            if (code >= 0x60 && code <= 0x69)                // NumPad0..NumPad9
                return "Num " + (code - 0x60);
            if (code >= 0x70 && code <= 0x87)                // F1..F24
                return "F" + (code - 0x6F);
            string other = fallback != null && !punctuation ? fallback(code) : null;
            return string.IsNullOrEmpty(other) ? "#" + code : other;
        }
    }
}
