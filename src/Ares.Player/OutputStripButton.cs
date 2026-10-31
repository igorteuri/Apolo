/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 The button of the audio output in the status bar. It draws itself in the colors of the theme, in every state (hover, pressed,
 list open): the standard drawing of a tool strip button left a light blue fill and frame of the system, which showed while
 the list of devices was open.
*/
using System;
using System.Drawing;
using System.Windows.Forms;
using Ares.CommonGUI;

namespace Ares.Player
{
    internal class OutputStripButton : ToolStripButton
    {
        private readonly Func<bool> m_IsOpen;

        public OutputStripButton(Func<bool> isOpen)
        {
            m_IsOpen = isOpen;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            ThemePalette p = ThemeManager.Current;
            if (p == null || p.IsClassic)
            {
                base.OnPaint(e);
                return;
            }
            Graphics g = e.Graphics;
            bool open = m_IsOpen != null && m_IsOpen();
            Rectangle r = new Rectangle(1, 1, Width - 3, Height - 3);
            if (Enabled && (open || Pressed))
            {
                using (SolidBrush fill = new SolidBrush(p.Selection))
                    g.FillRectangle(fill, r);
                using (Pen border = new Pen(p.Accent))
                    g.DrawRectangle(border, r);
            }
            else if (Enabled && Selected)
            {
                using (SolidBrush fill = new SolidBrush(p.Hover))
                    g.FillRectangle(fill, r);
                using (Pen border = new Pen(p.Border))
                    g.DrawRectangle(border, r);
            }
            Color color = Enabled ? p.Accent : p.TextDim;
            int x = 6;
            if (Image != null)
            {
                g.DrawImage(Image, x, (Height - Image.Height) / 2, Image.Width, Image.Height);
                x += Image.Width + 4;
            }
            if (!string.IsNullOrEmpty(Text))
            {
                Rectangle text = new Rectangle(x, 0, Math.Max(10, Width - x - 4), Height);
                TextRenderer.DrawText(g, Text, Font, text, color,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
        }
    }
}
