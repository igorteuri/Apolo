using System;
using System.Drawing;
using System.Windows.Forms;
using Ares.CommonGUI;
using WeifenLuo.WinFormsUI.Docking;

namespace Ares.Editor
{
    /// <summary>
    /// Title bar of the docked panels of the Editor: just the title and a close button.
    /// No "pin" / auto-hide button (panels do not collapse), no dragging out of the window.
    /// </summary>
    internal class ApoloDockCaption : DockPaneCaptionBase
    {
        private bool m_HoverClose;

        public ApoloDockCaption(DockPane pane) : base(pane)
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
        }

        protected override int MeasureHeight()
        {
            return Math.Max(24, Font.Height + 10);
        }

        private bool CanClose()
        {
            ToolWindow window = DockPane.ActiveContent as ToolWindow;
            return window == null || window.Closable;
        }

        private Rectangle CloseRect()
        {
            if (!CanClose())
                return new Rectangle(Width, 0, 0, 0);
            int size = Math.Max(16, Height - 8);
            return new Rectangle(Width - size - 6, (Height - size) / 2, size, size);
        }

        private string TitleText()
        {
            IDockContent content = DockPane.ActiveContent;
            if (content == null || content.DockHandler == null)
                return string.Empty;
            return content.DockHandler.TabText ?? string.Empty;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            ThemePalette p = ThemeManager.Current;
            Color back = p != null && !p.IsClassic ? p.Window : SystemColors.Control;
            Color text = p != null && !p.IsClassic ? p.Text : SystemColors.ControlText;
            Color dim = p != null && !p.IsClassic ? p.TextDim : SystemColors.GrayText;
            Color line = p != null && !p.IsClassic ? p.Border : SystemColors.ControlDark;
            Color accent = p != null && !p.IsClassic ? p.Accent : SystemColors.Highlight;
            Graphics g = e.Graphics;
            g.Clear(back);
            // thin accent line on top of the panel that has the focus, quiet line otherwise
            bool active = DockPane.ContainsFocus;
            using (Pen pen = new Pen(active ? accent : line))
                g.DrawLine(pen, 0, 0, Width, 0);
            Rectangle close = CloseRect();
            Rectangle textRect = new Rectangle(8, 0, Math.Max(10, close.Left - 12), Height);
            TextRenderer.DrawText(g, TitleText(), Font, textRect, active ? text : dim,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            if (m_HoverClose)
            {
                using (Brush b = new SolidBrush(p != null && !p.IsClassic ? p.Hover : SystemColors.ControlLight))
                    g.FillRectangle(b, close);
            }
            if (!CanClose())
                return;
            using (Bitmap bmp = IconLibrary.Get("x", close.Size, active || m_HoverClose ? text : dim))
            {
                if (bmp != null)
                    g.DrawImageUnscaled(bmp, close.Location);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool hover = CloseRect().Contains(e.Location);
            if (hover != m_HoverClose)
            {
                m_HoverClose = hover;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (m_HoverClose)
            {
                m_HoverClose = false;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left && CanClose() && CloseRect().Contains(e.Location))
                DockPane.CloseActiveContent();
        }
    }

    internal class ApoloDockCaptionFactory : DockPanelExtender.IDockPaneCaptionFactory
    {
        public DockPaneCaptionBase CreateDockPaneCaption(DockPane pane)
        {
            return new ApoloDockCaption(pane);
        }
    }
}
