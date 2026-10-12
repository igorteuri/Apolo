using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Ares.CommonGUI;

namespace Ares.Player
{
    /// <summary>
    /// Button for an element of the Player, drawn as a card: optional picture or icon, and the whole name
    /// (wrapped over several lines, never cut). It is a CheckBox, so that the rest of the Player works unchanged.
    /// </summary>
    public class ElementButton : CheckBox
    {
        private string m_IconName;
        private Bitmap m_Picture;
        private bool m_Hover;
        private bool m_Down;
        private Bitmap m_IconCache;
        private string m_IconCacheKey;

        public ElementButton()
        {
            Appearance = Appearance.Button;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            Margin = new Padding(4);
            TextAlign = ContentAlignment.MiddleCenter;
            ThemeManager.Changed += OnThemeChanged;
        }

        /// <summary>Name of a built-in icon, or null.</summary>
        public string IconName
        {
            get { return m_IconName; }
            set { m_IconName = value; Invalidate(); }
        }

        /// <summary>Own picture (shared bitmap, not owned by the button), or null.</summary>
        public Bitmap Picture
        {
            get { return m_Picture; }
            set { m_Picture = value; Invalidate(); }
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            if (IsDisposed)
                return;
            DisposeIconCache();
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThemeManager.Changed -= OnThemeChanged;
                DisposeIconCache();
            }
            base.Dispose(disposing);
        }

        private void DisposeIconCache()
        {
            if (m_IconCache != null)
            {
                m_IconCache.Dispose();
                m_IconCache = null;
                m_IconCacheKey = null;
            }
        }

        // ------------------------------------------------------------------ sizes (shared by all cards of a mode)

        private static float Scale
        {
            get { return Math.Max(80, ThemeManager.ScalePercent) / 110f; }
        }

        private static int Pad { get { return (int)Math.Round(10 * Scale); } }
        private static int IconSize { get { return (int)Math.Round(30 * Scale); } }
        private static int PictureHeight { get { return (int)Math.Round(70 * Scale); } }

        private const TextFormatFlags TextFlags = TextFormatFlags.WordBreak | TextFormatFlags.HorizontalCenter
            | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;

        /// <summary>
        /// Size of the cards for a list of buttons: the same for all, wide and tall enough to show every text completely.
        /// </summary>
        public static Size ComputeCardSize(IList<string> texts, IList<int> kinds, Font font)
        {
            // kinds: 0 = text only, 1 = icon, 2 = picture
            int pad = Pad;
            int width = (int)Math.Round(150 * Scale);
            // a single long word must fit as well
            foreach (string text in texts)
            {
                foreach (string word in text.Split(' '))
                {
                    int w = TextRenderer.MeasureText(word, font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
                    width = Math.Max(width, w + 2 * pad + 4);
                }
            }
            int height = (int)Math.Round(54 * Scale);
            for (int i = 0; i < texts.Count; ++i)
            {
                int textHeight = TextRenderer.MeasureText(texts[i], font, new Size(width - 2 * pad, int.MaxValue), TextFlags).Height;
                int h = textHeight + 2 * pad;
                if (kinds[i] == 1)
                    h += IconSize + 4;
                else if (kinds[i] == 2)
                    h = PictureHeight + 4 + textHeight + pad;
                height = Math.Max(height, h);
            }
            return new Size(width, height);
        }

        // ------------------------------------------------------------------ painting

        private static GraphicsPath Rounded(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = Math.Max(2, radius * 2);
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void OnMouseEnter(EventArgs eventargs)
        {
            m_Hover = true;
            Invalidate();
            base.OnMouseEnter(eventargs);
        }

        protected override void OnMouseLeave(EventArgs eventargs)
        {
            m_Hover = false;
            m_Down = false;
            Invalidate();
            base.OnMouseLeave(eventargs);
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            m_Down = true;
            Invalidate();
            base.OnMouseDown(mevent);
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            m_Down = false;
            Invalidate();
            base.OnMouseUp(mevent);
        }

        protected override void OnCheckedChanged(EventArgs e)
        {
            Invalidate();
            base.OnCheckedChanged(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            ThemePalette p = ThemeManager.Current;
            Graphics g = e.Graphics;
            Color window = p != null && !p.IsClassic ? p.Window : SystemColors.Control;
            Color surface = p != null && !p.IsClassic ? p.Surface : SystemColors.ControlLight;
            Color hover = p != null && !p.IsClassic ? p.Hover : SystemColors.ControlLightLight;
            Color selection = p != null && !p.IsClassic ? p.Selection : SystemColors.Highlight;
            Color accent = p != null && !p.IsClassic ? p.Accent : SystemColors.Highlight;
            Color border = p != null && !p.IsClassic ? p.Border : SystemColors.ControlDark;
            Color text = p != null && !p.IsClassic ? p.Text : SystemColors.ControlText;

            Color parentBack = Parent != null ? Parent.BackColor : window;
            g.Clear(parentBack);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color back = Checked ? selection : (m_Down ? hover : (m_Hover ? hover : surface));
            int radius = (int)Math.Round(8 * Scale);
            int pad = Pad;
            int textTop = pad;
            int textBottom = Height - pad;

            using (GraphicsPath path = Rounded(r, radius))
            {
                using (SolidBrush b = new SolidBrush(back))
                    g.FillPath(b, path);

                if (m_Picture != null)
                {
                    int ph = PictureHeight;
                    Rectangle area = new Rectangle(1, 1, Width - 2, ph);
                    Region oldClip = g.Clip;
                    g.SetClip(path, CombineMode.Intersect);
                    g.SetClip(area, CombineMode.Intersect);
                    // cover: fill the area, cut what does not fit
                    float scale = Math.Max((float)area.Width / m_Picture.Width, (float)area.Height / m_Picture.Height);
                    int w = (int)Math.Ceiling(m_Picture.Width * scale);
                    int h = (int)Math.Ceiling(m_Picture.Height * scale);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(m_Picture, area.X + (area.Width - w) / 2, area.Y + (area.Height - h) / 2, w, h);
                    g.Clip = oldClip;
                    oldClip.Dispose();
                    textTop = ph + 4;
                }
                else if (!string.IsNullOrEmpty(m_IconName))
                {
                    int size = IconSize;
                    string key = m_IconName + "|" + size + "|" + accent.ToArgb();
                    if (m_IconCache == null || m_IconCacheKey != key)
                    {
                        DisposeIconCache();
                        m_IconCache = IconLibrary.Get(m_IconName, new Size(size, size), Checked ? text : accent);
                        m_IconCacheKey = key;
                    }
                    if (m_IconCache != null)
                        g.DrawImage(m_IconCache, (Width - size) / 2, pad, size, size);
                    textTop = pad + size + 4;
                }

                Rectangle textRect = new Rectangle(pad, textTop, Width - 2 * pad, Math.Max(10, textBottom - textTop));
                TextRenderer.DrawText(g, Text, Font, textRect, text, TextFlags | TextFormatFlags.VerticalCenter);

                Color line = Checked ? accent : (m_Hover ? accent : border);
                using (Pen pen = new Pen(line, Checked ? 2f : 1f))
                    g.DrawPath(pen, path);
            }
        }
    }
}
