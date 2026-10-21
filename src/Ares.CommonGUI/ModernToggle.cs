/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.
 An on/off switch that follows the colors of the current theme (accent color when on). Space or click toggles it.
*/
using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Ares.CommonGUI
{
    public class ModernToggle : Control
    {
        private bool m_Checked;
        private bool m_Hover;
        private EventHandler m_ThemeHandler;

        public event EventHandler CheckedChanged;

        public ModernToggle()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor, true);
            TabStop = true;
            Size = new Size(48, 26);
            Cursor = Cursors.Hand;
            m_ThemeHandler = (s, e) => Invalidate();
            ThemeManager.Changed += m_ThemeHandler;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                ThemeManager.Changed -= m_ThemeHandler;
            base.Dispose(disposing);
        }

        [DefaultValue(false)]
        public bool Checked
        {
            get { return m_Checked; }
            set
            {
                if (m_Checked == value)
                    return;
                m_Checked = value;
                Invalidate();
                EventHandler handler = CheckedChanged;
                if (handler != null)
                    handler(this, EventArgs.Empty);
            }
        }

        protected override void OnClick(EventArgs e)
        {
            Focus();
            Checked = !Checked;
            base.OnClick(e);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            return keyData == Keys.Space || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space)
            {
                Checked = !Checked;
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnMouseEnter(EventArgs e) { m_Hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { m_Hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            ThemePalette p = ThemeManager.Current;
            Color back = ThemeManager.EffectiveBack(this);
            Color off = SystemColors.ControlDark;
            Color accent = SystemColors.Highlight;
            Color text = SystemColors.ControlText;
            if (p != null && !p.IsClassic)
            {
                off = m_Hover ? p.ControlBorder : p.Hover;
                accent = p.Accent;
                text = p.Text;
            }
            Graphics g = e.Graphics;
            g.Clear(back);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int h = Math.Min(Height - 6, 22);
            int w = Math.Min(Width - 4, 42);
            Rectangle track = new Rectangle((Width - w) / 2, (Height - h) / 2, w, h);
            using (GraphicsPath path = Pill(track))
            using (SolidBrush fill = new SolidBrush(m_Checked ? accent : off))
                g.FillPath(fill, path);
            int r = h - 6;
            int knobX = m_Checked ? track.Right - r - 3 : track.Left + 3;
            using (SolidBrush knob = new SolidBrush(Color.White))
                g.FillEllipse(knob, knobX, track.Top + 3, r, r);
            if (Focused && ShowFocusCues)
            {
                using (Pen f = new Pen(text, 2f))
                    g.DrawRectangle(f, 1, 1, Width - 3, Height - 3);
            }
        }

        private static GraphicsPath Pill(Rectangle r)
        {
            GraphicsPath path = new GraphicsPath();
            int d = r.Height;
            path.AddArc(r.Left, r.Top, d, d, 90, 180);
            path.AddArc(r.Right - d, r.Top, d, d, 270, 180);
            path.CloseFigure();
            return path;
        }
    }
}
