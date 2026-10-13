/*
 Apolo - a fork of Ares (Copyright (c) Jörg Ruedenauer), distributed under the GPL.
*/
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Ares.Player
{
    /// <summary>
    /// A slim bar at the edge of the window which shows / hides a side panel when clicked
    /// (a chevron appears when the mouse is over it).
    /// </summary>
    public class EdgeHandle : Control
    {
        private bool m_Hover;
        private bool m_PointsLeft;
        private bool m_Horizontal;

        public EdgeHandle()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.Selectable, false);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            TabStop = false;
            Width = 17;
        }

        /// <summary>True for a bar lying at the bottom edge (the chevron then points up / down).</summary>
        public bool Horizontal
        {
            get { return m_Horizontal; }
            set
            {
                if (m_Horizontal != value)
                {
                    m_Horizontal = value;
                    Invalidate();
                }
            }
        }

        /// <summary>Direction of the chevron: true points to the left (up for a horizontal bar).</summary>
        public bool PointsLeft
        {
            get { return m_PointsLeft; }
            set
            {
                if (m_PointsLeft != value)
                {
                    m_PointsLeft = value;
                    Invalidate();
                }
            }
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

        protected override void OnPaint(PaintEventArgs e)
        {
            Ares.CommonGUI.ThemePalette p = Ares.CommonGUI.ThemeManager.Current;
            Color back = Parent != null ? Parent.BackColor : SystemColors.Control;
            Color bar = SystemColors.ControlDark;
            Color glyph = SystemColors.ControlDarkDark;
            Color accent = SystemColors.Highlight;
            if (p != null && !p.IsClassic)
            {
                bar = p.Border;
                glyph = p.TextDim;
                accent = p.Accent;
            }
            Graphics g = e.Graphics;
            g.Clear(back);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int w = Math.Max(4, Width - 3);
            Rectangle r = new Rectangle((Width - w) / 2, 0, w, Height - 1);
            if (m_Horizontal)
            {
                int hh = Math.Max(4, Height - 3);
                r = new Rectangle(0, (Height - hh) / 2, Width - 1, hh);
                w = hh;
            }
            using (GraphicsPath path = RoundRect(r, w))
            using (SolidBrush b = new SolidBrush(m_Hover ? Color.FromArgb(70, accent) : Color.FromArgb(90, bar)))
                g.FillPath(b, path);
            // chevron in the middle of the bar
            float cx = Width / 2f;
            float cy = Height / 2f;
            float dx = 2.5f * (m_PointsLeft ? -1 : 1);
            using (Pen pen = new Pen(m_Hover ? accent : glyph, 2f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                if (m_Horizontal)
                    g.DrawLines(pen, new PointF[] { new PointF(cx - 6, cy - dx), new PointF(cx, cy + dx), new PointF(cx + 6, cy - dx) });
                else
                    g.DrawLines(pen, new PointF[] { new PointF(cx - dx, cy - 5), new PointF(cx + dx, cy), new PointF(cx - dx, cy + 5) });
            }
        }

        private static GraphicsPath RoundRect(Rectangle r, int d)
        {
            GraphicsPath path = new GraphicsPath();
            d = Math.Max(2, Math.Min(d, Math.Min(r.Width, r.Height)));
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
