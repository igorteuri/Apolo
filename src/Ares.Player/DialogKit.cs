/*
 Small helpers so that the settings windows built in code (Discord, OBS, remote control ...) look alike:
 the same margins, one height for fields and buttons, a thin rule between the parts, secondary text dimmed,
 the main button in the highlight color and no text selected (blue) when the window opens.
*/
using System;
using System.Drawing;
using System.Windows.Forms;
using Ares.CommonGUI;

namespace Ares.Player
{
    internal static class DialogKit
    {
        public const int Margin = 24;
        public const int FieldHeight = 32;
        public const int ButtonHeight = 36;

        /// <summary>A thin line between two parts of the window.</summary>
        public sealed class Rule : Control
        {
            public Rule()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
                TabStop = false;
                Height = 1;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                ThemePalette p = ThemeManager.Current;
                Color back = Parent != null ? ThemeManager.EffectiveBack(this) : SystemColors.Control;
                e.Graphics.Clear(back);
                using (Pen pen = new Pen(p != null && !p.IsClassic ? p.Border : SystemColors.ControlDark))
                    e.Graphics.DrawLine(pen, 0, 0, Width, 0);
            }
        }

        public static Rule MakeRule(int y, int width)
        {
            Rule r = new Rule();
            r.SetBounds(Margin, y, width, 1);
            return r;
        }

        /// <summary>Puts a field (text box, combo, numeric box) in the middle of a row, whatever height it wants to have.</summary>
        public static void InRow(Control c, int x, int rowTop, int width, int rowHeight)
        {
            c.Width = width;
            c.Left = x;
            c.Top = rowTop + Math.Max(0, (rowHeight - c.Height) / 2);
        }

        public static void Primary(Button b)
        {
            b.Tag = "Primary";
        }

        public static void Muted(Label l)
        {
            l.Tag = "Muted";
        }

        /// <summary>The closing touches: the Enter key, the first focus (on a button, so that no text is selected) and the theme.</summary>
        public static void Finish(Form form, Button close, Control focus)
        {
            form.AcceptButton = close;
            ThemeManager.Apply(form);
            form.Shown += (s, e) =>
            {
                foreach (Control c in form.Controls)
                {
                    TextBox tb = c as TextBox;
                    if (tb != null)
                        tb.SelectionLength = 0;
                }
                if (focus != null && focus.CanFocus)
                    form.ActiveControl = focus;
            };
        }
    }
}
