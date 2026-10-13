/*
 Apolo - a fork of Ares (Copyright (c) Jörg Ruedenauer), distributed under the GPL.
*/
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Ares.Player
{
    public class AppTabItem
    {
        public string Text;
        public bool Closable;
        public bool Home;
        public bool Sound;   // the page plays (or sends) sound
        public bool Muted;
    }

    /// <summary>
    /// Tab bar at the top of the window (like Kenku FM): the first tab is Apolo itself, the others are web pages
    /// which play in the background when another tab is in front.
    /// </summary>
    public class AppTabBar : Control
    {
        public const int BarHeight = 36;
        private const int TabMinWidth = 70;
        private const int TabMaxWidth = 210;
        private const int HomeWidth = 118;
        private const int AddWidth = 34;
        private const int CloseSize = 16;

        private readonly List<AppTabItem> m_Items = new List<AppTabItem>();
        private readonly List<Rectangle> m_Rects = new List<Rectangle>();
        private Rectangle m_AddRect = Rectangle.Empty;
        private int m_Selected;
        private int m_Hover = -1;
        private bool m_HoverClose;
        private bool m_HoverAdd;
        private Icon m_HomeIcon;

        public event Action<int> TabSelected;
        public event Action<int> TabClosed;
        public event Action<Point> AddClicked;
        public event Action<int, Point> TabContext;

        public AppTabBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            Height = BarHeight;
            Dock = DockStyle.Top;
            TabStop = false;
            Ares.CommonGUI.ThemeManager.Changed += (s, e) => Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && m_HomeIcon != null)
                m_HomeIcon.Dispose();
            base.Dispose(disposing);
        }

        public int SelectedIndex
        {
            get { return m_Selected; }
            set
            {
                if (m_Selected != value)
                {
                    m_Selected = value;
                    Invalidate();
                }
            }
        }

        public void SetItems(IList<AppTabItem> items, int selected)
        {
            m_Items.Clear();
            m_Items.AddRange(items);
            m_Selected = Math.Max(0, Math.Min(selected, m_Items.Count - 1));
            m_Hover = -1;
            Invalidate();
        }

        // ------------------------------------------------------------ layout

        private void Measure()
        {
            m_Rects.Clear();
            int x = 8;
            int top = 6;
            int h = Height - top;
            int count = m_Items.Count;
            int available = Width - 8 - AddWidth - 10;
            int homeW = (count > 0 && m_Items[0].Home) ? HomeWidth : 0;
            List<int> widths = new List<int>();
            int total = 0;
            using (Graphics g = CreateGraphics())
            {
                for (int i = 0; i < count; i++)
                {
                    int w;
                    if (m_Items[i].Home)
                        w = homeW;
                    else
                    {
                        Size size = TextRenderer.MeasureText(g, m_Items[i].Text ?? "", Font, new Size(int.MaxValue, int.MaxValue),
                            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                        w = Math.Max(TabMinWidth, Math.Min(TabMaxWidth, size.Width + 20 + CloseSize + 14));
                    }
                    widths.Add(w);
                    total += w + 2;
                }
            }
            if (total > available && count > 1)
            {
                // too many tabs: all web tabs get narrower
                int others = total - homeW - 2;
                int room = Math.Max(TabMinWidth * (count - 1), available - homeW - 2);
                for (int i = 0; i < count; i++)
                {
                    if (!m_Items[i].Home)
                        widths[i] = Math.Max(48, (int)((long)widths[i] * room / Math.Max(1, others)) - 2);
                }
            }
            for (int i = 0; i < count; i++)
            {
                m_Rects.Add(new Rectangle(x, top, widths[i], h));
                x += widths[i] + 2;
            }
            m_AddRect = new Rectangle(x + 2, top + 3, AddWidth - 4, h - 6);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Invalidate();
        }

        private Rectangle CloseRect(Rectangle tab)
        {
            return new Rectangle(tab.Right - CloseSize - 8, tab.Y + (tab.Height - CloseSize) / 2 + 1, CloseSize, CloseSize);
        }

        private int HitTab(Point p, out bool onClose)
        {
            onClose = false;
            for (int i = 0; i < m_Rects.Count; i++)
            {
                if (m_Rects[i].Contains(p))
                {
                    onClose = m_Items[i].Closable && CloseRect(m_Rects[i]).Contains(p);
                    return i;
                }
            }
            return -1;
        }

        // ------------------------------------------------------------ mouse

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool close;
            int hit = HitTab(e.Location, out close);
            bool add = m_AddRect.Contains(e.Location);
            if (hit != m_Hover || close != m_HoverClose || add != m_HoverAdd)
            {
                m_Hover = hit;
                m_HoverClose = close;
                m_HoverAdd = add;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (m_Hover != -1 || m_HoverAdd)
            {
                m_Hover = -1;
                m_HoverClose = false;
                m_HoverAdd = false;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            bool close;
            int hit = HitTab(e.Location, out close);
            if (e.Button == MouseButtons.Left)
            {
                if (m_AddRect.Contains(e.Location))
                {
                    if (AddClicked != null)
                        AddClicked(PointToScreen(new Point(m_AddRect.Left, m_AddRect.Bottom)));
                    return;
                }
                if (hit < 0)
                    return;
                if (close)
                {
                    if (TabClosed != null)
                        TabClosed(hit);
                    return;
                }
                if (TabSelected != null)
                    TabSelected(hit);
            }
            else if (e.Button == MouseButtons.Middle)
            {
                if (hit >= 0 && m_Items[hit].Closable && TabClosed != null)
                    TabClosed(hit);
            }
            else if (e.Button == MouseButtons.Right)
            {
                if (hit >= 0 && TabContext != null)
                    TabContext(hit, PointToScreen(e.Location));
            }
        }

        // ------------------------------------------------------------ painting

        protected override void OnPaint(PaintEventArgs e)
        {
            Ares.CommonGUI.ThemePalette p = Ares.CommonGUI.ThemeManager.Current;
            bool themed = p != null && !p.IsClassic;
            Color barColor = themed ? p.Surface : SystemColors.ControlDark;
            Color pageColor = themed ? p.Window : SystemColors.Control;
            Color text = themed ? p.Text : SystemColors.ControlText;
            Color dim = themed ? p.TextDim : SystemColors.GrayText;
            Color accent = themed ? p.Accent : SystemColors.Highlight;
            Color hover = themed ? p.Hover : SystemColors.ControlLight;
            Graphics g = e.Graphics;
            g.Clear(barColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Measure();

            for (int i = 0; i < m_Items.Count; i++)
            {
                AppTabItem item = m_Items[i];
                Rectangle r = m_Rects[i];
                bool selected = i == m_Selected;
                bool over = i == m_Hover;
                Color back = selected ? pageColor : (over ? hover : barColor);
                using (GraphicsPath path = TabPath(r))
                using (SolidBrush b = new SolidBrush(back))
                    g.FillPath(b, path);
                if (selected)
                {
                    using (SolidBrush line = new SolidBrush(accent))
                        g.FillRectangle(line, r.X + 8, r.Y, r.Width - 16, 2);
                }
                int textLeft = r.X + 12;
                if (item.Home)
                {
                    if (m_HomeIcon == null)
                        m_HomeIcon = Ares.CommonGUI.ApoloBrand.GetIcon(16, Ares.CommonGUI.ApoloState.Playing, 0);
                    g.DrawIcon(m_HomeIcon, new Rectangle(r.X + 10, r.Y + (r.Height - 16) / 2 + 1, 16, 16));
                    textLeft = r.X + 32;
                }
                else if (item.Sound)
                {
                    TextRenderer.DrawText(g, item.Muted ? "🔇" : "♪", Font, new Rectangle(r.X + 8, r.Y, 18, r.Height),
                        item.Muted ? dim : accent, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
                    textLeft = r.X + 26;
                }
                int textRight = r.Right - 8 - (item.Closable ? CloseSize + 2 : 0);
                Rectangle textRect = new Rectangle(textLeft, r.Y + 1, Math.Max(10, textRight - textLeft), r.Height);
                TextRenderer.DrawText(g, item.Text ?? "", Font, textRect, selected ? text : (over ? text : dim),
                    TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.Left | TextFormatFlags.NoPadding);
                if (item.Closable && (selected || over))
                {
                    Rectangle c = CloseRect(r);
                    if (over && m_HoverClose)
                    {
                        using (SolidBrush hb = new SolidBrush(themed ? p.Border : Color.LightGray))
                            g.FillEllipse(hb, c);
                    }
                    using (Pen pen = new Pen(over && m_HoverClose ? text : dim, 1.6f))
                    {
                        pen.StartCap = LineCap.Round;
                        pen.EndCap = LineCap.Round;
                        g.DrawLine(pen, c.X + 5, c.Y + 5, c.Right - 5, c.Bottom - 5);
                        g.DrawLine(pen, c.Right - 5, c.Y + 5, c.X + 5, c.Bottom - 5);
                    }
                }
            }

            // "+" button
            if (m_HoverAdd)
            {
                using (GraphicsPath path = RoundRect(m_AddRect, 7))
                using (SolidBrush b = new SolidBrush(hover))
                    g.FillPath(b, path);
            }
            using (Pen pen = new Pen(m_HoverAdd ? text : dim, 1.8f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                float cx = m_AddRect.X + m_AddRect.Width / 2f;
                float cy = m_AddRect.Y + m_AddRect.Height / 2f;
                g.DrawLine(pen, cx - 5, cy, cx + 5, cy);
                g.DrawLine(pen, cx, cy - 5, cx, cy + 5);
            }
            // thin line under the bar
            using (Pen line = new Pen(themed ? p.Border : SystemColors.ControlDarkDark))
                g.DrawLine(line, 0, Height - 1, Width, Height - 1);
            if (m_Selected >= 0 && m_Selected < m_Rects.Count)
            {
                Rectangle r = m_Rects[m_Selected];
                using (SolidBrush b = new SolidBrush(pageColor))
                    g.FillRectangle(b, r.X, Height - 1, r.Width, 1);
            }
        }

        private static GraphicsPath TabPath(Rectangle r)
        {
            GraphicsPath path = new GraphicsPath();
            int d = 10;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddLine(r.Right, r.Y + d / 2, r.Right, r.Bottom);
            path.AddLine(r.Right, r.Bottom, r.X, r.Bottom);
            path.CloseFigure();
            return path;
        }

        private static GraphicsPath RoundRect(Rectangle r, int d)
        {
            GraphicsPath path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
