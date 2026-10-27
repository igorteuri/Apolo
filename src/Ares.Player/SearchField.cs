/*
 The search field of the program: a rounded box which fills the height of its bar, a magnifier at the left and the real
 TextBox (without its own border) inside. The border follows the theme and turns to the highlight color on focus.
*/
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Ares.CommonGUI;

namespace Ares.Player
{
    internal class SearchField : Control
    {
        /// <summary>The text box inside (text, events and cue banner are used through it).</summary>
        public readonly TextBox Box = new TextBox();

        private const int IconWidth = 26;

        public SearchField()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.ContainerControl, true);
            Box.BorderStyle = BorderStyle.None;
            Box.Tag = "NoThemeBorder"; // the theme must not put its own frame on it
            Box.GotFocus += (s, e) => Invalidate();
            Box.LostFocus += (s, e) => Invalidate();
            Controls.Add(Box);
            ThemeManager.Changed += OnThemeChanged;
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            if (IsDisposed)
                return;
            ApplyColors();
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                ThemeManager.Changed -= OnThemeChanged;
            base.Dispose(disposing);
        }

        private void ApplyColors()
        {
            ThemePalette p = ThemeManager.Current;
            Box.BackColor = p != null && !p.IsClassic ? p.Surface : SystemColors.Window;
            Box.ForeColor = p != null && !p.IsClassic ? p.Text : SystemColors.WindowText;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyColors();
            LayoutBox();
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            LayoutBox();
        }

        private void LayoutBox()
        {
            int h = Box.PreferredHeight;
            Box.SetBounds(IconWidth, Math.Max(0, (Height - h) / 2), Math.Max(10, Width - IconWidth - 8), h);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            ThemePalette p = ThemeManager.Current;
            bool themed = p != null && !p.IsClassic;
            Color surface = themed ? p.Surface : SystemColors.Window;
            Color border = themed ? p.ControlBorder : SystemColors.ControlDark;
            Color accent = themed ? p.Accent : SystemColors.Highlight;
            Color dim = themed ? p.TextDim : SystemColors.GrayText;
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? ThemeManager.EffectiveBack(this) : SystemColors.Control);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            bool focus = Box.Focused;
            using (GraphicsPath path = Rounded(r, 6))
            {
                using (SolidBrush b = new SolidBrush(surface))
                    g.FillPath(b, path);
                using (Pen pen = new Pen(focus ? accent : border, focus ? 2f : 1f))
                    g.DrawPath(pen, path);
            }
            // magnifier
            float cy = Height / 2f;
            using (Pen pen = new Pen(focus ? accent : dim, 1.6f))
            {
                pen.StartCap = pen.EndCap = LineCap.Round;
                g.DrawEllipse(pen, 9f, cy - 6f, 8f, 8f);
                g.DrawLine(pen, 16f, cy + 1.5f, 19.5f, cy + 5f);
            }
        }

        private static GraphicsPath Rounded(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Box.Focus();
        }
    }
}
