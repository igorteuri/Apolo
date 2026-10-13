/*
 Apolo - a fork of Ares (Copyright (c) Jörg Ruedenauer), distributed under the GPL.
 A slider (volume control) which follows the colors of the current theme (accent color for the knob and the filled part).
*/
using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Ares.CommonGUI
{
    public class ModernSlider : Control, ISupportInitialize
    {
        private int m_Minimum;
        private int m_Maximum = 10;
        private int m_Value;
        private bool m_Dragging;
        private bool m_Hover;
        private EventHandler m_ThemeHandler;

        public event EventHandler Scroll;
        public event EventHandler ValueChanged;

        public ModernSlider()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor, true);
            TabStop = true;
            Size = new Size(104, 31);
            SmallChange = 1;
            LargeChange = 5;
            TickFrequency = 1;
            m_ThemeHandler = (s, e) => Invalidate();
            ThemeManager.Changed += m_ThemeHandler;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                ThemeManager.Changed -= m_ThemeHandler;
            base.Dispose(disposing);
        }

        public void BeginInit() { }
        public void EndInit() { }

        // TrackBar compatible members
        public int SmallChange { get; set; }
        public int LargeChange { get; set; }
        public int TickFrequency { get; set; }
        public TickStyle TickStyle { get; set; }

        public int Minimum
        {
            get { return m_Minimum; }
            set { m_Minimum = value; if (m_Value < value) Value = value; Invalidate(); }
        }

        public int Maximum
        {
            get { return m_Maximum; }
            set { m_Maximum = value; if (m_Value > value) Value = value; Invalidate(); }
        }

        public int Value
        {
            get { return m_Value; }
            set
            {
                int v = Math.Max(m_Minimum, Math.Min(m_Maximum, value));
                if (v == m_Value)
                    return;
                m_Value = v;
                Invalidate();
                EventHandler handler = ValueChanged;
                if (handler != null)
                    handler(this, EventArgs.Empty);
            }
        }

        private const int Pad = 11; // room for the knob at both ends

        private void SetFromMouse(int x)
        {
            double width = Math.Max(1, Width - 2 * Pad);
            double ratio = Math.Max(0, Math.Min(1, (x - Pad) / width));
            int v = m_Minimum + (int)Math.Round(ratio * (m_Maximum - m_Minimum));
            ChangeByUser(v);
        }

        private void ChangeByUser(int v)
        {
            int old = m_Value;
            Value = v;
            if (m_Value != old)
            {
                EventHandler handler = Scroll;
                if (handler != null)
                    handler(this, EventArgs.Empty);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                Focus();
                m_Dragging = true;
                Capture = true;
                SetFromMouse(e.X);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (m_Dragging)
                SetFromMouse(e.X);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            m_Dragging = false;
            Capture = false;
            Invalidate();
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

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            ChangeByUser(m_Value + (e.Delta > 0 ? SmallChange : -SmallChange));
        }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Left: case Keys.Right: case Keys.Home: case Keys.End: case Keys.PageUp: case Keys.PageDown:
                    return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            switch (e.KeyCode)
            {
                case Keys.Left: ChangeByUser(m_Value - SmallChange); e.Handled = true; break;
                case Keys.Right: ChangeByUser(m_Value + SmallChange); e.Handled = true; break;
                case Keys.PageDown: ChangeByUser(m_Value - LargeChange); e.Handled = true; break;
                case Keys.PageUp: ChangeByUser(m_Value + LargeChange); e.Handled = true; break;
                case Keys.Home: ChangeByUser(m_Minimum); e.Handled = true; break;
                case Keys.End: ChangeByUser(m_Maximum); e.Handled = true; break;
            }
        }

        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            ThemePalette p = ThemeManager.Current;
            Color back = ThemeManager.EffectiveBack(this);
            Color track = SystemColors.ControlDark;
            Color accent = SystemColors.Highlight;
            if (p != null && !p.IsClassic)
            {
                track = p.Hover;
                accent = p.Accent;
            }
            if (!Enabled)
            {
                accent = track;
            }
            Graphics g = e.Graphics;
            g.Clear(back);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int cy = Height / 2;
            int left = Pad, right = Width - Pad;
            int range = Math.Max(1, m_Maximum - m_Minimum);
            int knobX = left + (int)Math.Round((right - left) * (double)(m_Value - m_Minimum) / range);
            using (Pen pen = new Pen(track, 6f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawLine(pen, left, cy, right, cy);
            }
            if (knobX > left)
            {
                using (Pen pen = new Pen(accent, 6f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawLine(pen, left, cy, knobX, cy);
                }
            }
            int r = (m_Dragging || m_Hover) ? 9 : 8;
            using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 255, 255)))
                g.FillEllipse(b, knobX - r, cy - r, 2 * r, 2 * r);
            using (Pen ring = new Pen(accent, 3f))
                g.DrawEllipse(ring, knobX - r + 1, cy - r + 1, 2 * r - 2, 2 * r - 2);
            if (Focused && ShowFocusCues)
            {
                using (Pen f = new Pen(Color.FromArgb(120, accent), 1f))
                    g.DrawRectangle(f, 1, 1, Width - 3, Height - 3);
            }
        }
    }
}
