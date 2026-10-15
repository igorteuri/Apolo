/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 A drop-down list drawn with the colors of the theme (the native one has a white frame and a light arrow button).
*/
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Ares.CommonGUI
{
    public class ThemedComboBox : ComboBox
    {
        private const int WM_PAINT = 0x000F;
        private bool m_Hover;

        public ThemedComboBox()
        {
            DropDownStyle = ComboBoxStyle.DropDownList;
            DrawMode = DrawMode.OwnerDrawFixed;
            FlatStyle = FlatStyle.Flat;
        }

        private static bool Themed
        {
            get { return ThemeManager.Current != null && !ThemeManager.Current.IsClassic; }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            m_Hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            m_Hover = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (!Themed || e.Index < 0)
            {
                base.OnDrawItem(e);
                return;
            }
            ThemePalette p = ThemeManager.Current;
            bool inList = (e.State & DrawItemState.ComboBoxEdit) == 0;
            bool selected = inList && (e.State & DrawItemState.Selected) != 0;
            using (SolidBrush back = new SolidBrush(selected ? p.Selection : p.Surface))
                e.Graphics.FillRectangle(back, e.Bounds);
            Rectangle r = new Rectangle(e.Bounds.X + 4, e.Bounds.Y, e.Bounds.Width - 6, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]), Font, r, p.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.Left);
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WM_PAINT && Themed && IsHandleCreated)
            {
                try
                {
                    PaintBox();
                }
                catch (Exception)
                {
                }
            }
        }

        // the closed box (frame, text and arrow) is painted over the native one
        private void PaintBox()
        {
            ThemePalette p = ThemeManager.Current;
            using (Graphics g = Graphics.FromHwnd(Handle))
            {
                Rectangle all = new Rectangle(0, 0, Width, Height);
                bool hot = Focused || m_Hover || DroppedDown;
                using (SolidBrush back = new SolidBrush(p.Surface))
                    g.FillRectangle(back, all);
                using (Pen frame = new Pen(hot ? p.Accent : p.Border))
                    g.DrawRectangle(frame, 0, 0, Width - 1, Height - 1);
                int arrowW = 22;
                Rectangle text = new Rectangle(5, 0, Width - arrowW - 6, Height);
                string s = SelectedIndex >= 0 ? GetItemText(SelectedItem) : Text;
                TextRenderer.DrawText(g, s, Font, text, Enabled ? p.Text : p.TextDim,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.Left);
                int cx = Width - arrowW / 2 - 2, cy = Height / 2;
                Point[] tri = { new Point(cx - 4, cy - 2), new Point(cx + 4, cy - 2), new Point(cx, cy + 3) };
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (SolidBrush arrow = new SolidBrush(Enabled ? p.Text : p.TextDim))
                    g.FillPolygon(arrow, tri);
            }
        }
    }
}
