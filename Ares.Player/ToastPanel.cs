using System;
using System.Drawing;
using System.Windows.Forms;

namespace Ares.Player
{
    /// <summary>
    /// Small, non-intrusive notification shown in a corner of the Player window instead of a pop-up window.
    /// Disappears by itself; a click opens the full message list.
    /// </summary>
    public class ToastPanel : Control
    {
        private readonly Timer m_Timer = new Timer();
        private string m_Text = string.Empty;
        private bool m_IsError;
        private int m_Count;

        public event EventHandler Activated;

        public ToastPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Visible = false;
            Cursor = Cursors.Hand;
            Size = new Size(380, 52);
            m_Timer.Interval = 6000;
            m_Timer.Tick += (s, e) => { m_Timer.Stop(); Visible = false; m_Count = 0; };
        }

        public void ShowToast(string text, bool isError)
        {
            m_Count = Visible ? m_Count + 1 : 1;
            m_Text = text ?? string.Empty;
            m_IsError = isError;
            PositionInParent();
            Visible = true;
            BringToFront();
            Invalidate();
            m_Timer.Stop();
            m_Timer.Start();
        }

        public void PositionInParent()
        {
            if (Parent == null)
                return;
            float scale = Math.Max(1f, DeviceDpi / 96f);
            int w = Math.Min((int)(380 * scale), Math.Max(200, Parent.ClientSize.Width - 24));
            Size = new Size(w, (int)(52 * scale));
            Location = new Point(Parent.ClientSize.Width - Width - 12, Parent.ClientSize.Height - Height - 12);
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            m_Timer.Stop();
            Visible = false;
            m_Count = 0;
            if (Activated != null)
                Activated(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var theme = Ares.CommonGUI.ThemeManager.Current;
            bool dark = theme != null && !theme.IsClassic && theme.IsDark;
            Color back = dark ? Color.FromArgb(45, 48, 58) : Color.FromArgb(255, 252, 235);
            Color fore = dark ? Color.FromArgb(235, 235, 240) : Color.FromArgb(40, 40, 40);
            Color accent = m_IsError ? Color.FromArgb(220, 80, 80) : Color.FromArgb(230, 170, 50);
            Graphics g = e.Graphics;
            g.Clear(back);
            using (var accentBrush = new SolidBrush(accent))
                g.FillRectangle(accentBrush, 0, 0, 5, Height);
            using (var pen = new Pen(dark ? Color.FromArgb(90, 95, 110) : Color.FromArgb(210, 200, 160)))
                g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            string text = m_Count > 1 ? m_Text + "  (+" + (m_Count - 1) + ")" : m_Text;
            var rect = new Rectangle(14, 4, Width - 22, Height - 8);
            TextRenderer.DrawText(g, text, Font, rect, fore,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                m_Timer.Dispose();
            base.Dispose(disposing);
        }
    }
}
