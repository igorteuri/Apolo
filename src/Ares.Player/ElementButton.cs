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
        private bool m_ListMode;
        private bool m_MultiSelected;
        private int m_Kind;

        /// <summary>What the button plays, as flags: 1 = music, 2 = sound effects (small badges in the corner).</summary>
        public int Kind
        {
            get { return m_Kind; }
            set { if (m_Kind != value) { m_Kind = value; Invalidate(); } }
        }

        /// <summary>Selected (with Ctrl / Shift) in edit mode, for deleting several buttons at once.</summary>
        public bool MultiSelected
        {
            get { return m_MultiSelected; }
            set { if (m_MultiSelected != value) { m_MultiSelected = value; Invalidate(); } }
        }

        /// <summary>When set, the next click only changes the selection and does not play the playlist.</summary>
        public bool SwallowClick;

        /// <summary>When set (edit mode), a click on the small music / effect badge asks to switch the kind instead of playing.</summary>
        public bool BadgeClickable;

        /// <summary>The badge (music / sound effect) was clicked.</summary>
        public event EventHandler KindBadgeClicked;

        private int BadgeCount()
        {
            return ((m_Kind & 1) != 0 ? 1 : 0) + ((m_Kind & 2) != 0 ? 1 : 0);
        }

        // where the small badges are drawn (the same place for painting and for the click)
        private Rectangle BadgeRect(int index, int count)
        {
            int bd = Math.Max(22, (int)Math.Round(25 * Scale));
            return m_ListMode
                ? new Rectangle(Width - (bd + 4) * (count - index) - 6, (Height - bd) / 2, bd, bd)
                : new Rectangle(6 + index * (bd + 3), 6, bd, bd);
        }

        private bool BadgeHit(Point p)
        {
            int count = BadgeCount();
            for (int i = 0; i < count; i++)
            {
                if (BadgeRect(i, count).Contains(p))
                    return true;
            }
            return false;
        }

        protected override void OnClick(EventArgs e)
        {
            if (SwallowClick)
            {
                SwallowClick = false;
                return;
            }
            if (BadgeClickable && m_Kind != 0 && BadgeHit(PointToClient(Cursor.Position)))
            {
                EventHandler handler = KindBadgeClicked;
                if (handler != null)
                    handler(this, EventArgs.Empty);
                return;
            }
            base.OnClick(e);
        }

        public ElementButton()
        {
            Appearance = Appearance.Button;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            Margin = new Padding(4);
            TextAlign = ContentAlignment.MiddleCenter;
            Cursor = Cursors.Hand;
            ThemeManager.Changed += OnThemeChanged;
        }

        /// <summary>Draws the button as one row of a list instead of a card.</summary>
        public bool ListMode
        {
            get { return m_ListMode; }
            set { m_ListMode = value; Invalidate(); }
        }

        public static int RowHeight(Font font)
        {
            return Math.Max((int)Math.Round(34 * Scale), TextRenderer.MeasureText("Ag", font).Height + 12);
        }

        /// <summary>Name of a built-in icon, or null.</summary>
        public string IconName
        {
            get { return m_IconName; }
            set { m_IconName = value; Invalidate(); }
        }

        private Color m_Tint = Color.Empty;
        private bool m_PictureContain;

        /// <summary>Highlight color chosen for this button (Color.Empty = the color of the theme).</summary>
        public Color TintColor
        {
            get { return m_Tint; }
            set { m_Tint = value; Invalidate(); }
        }

        /// <summary>The picture is shown whole and centered (emojis) instead of filling the area (photos).</summary>
        public bool PictureContain
        {
            get { return m_PictureContain; }
            set { m_PictureContain = value; Invalidate(); }
        }

        private static Color Mix(Color a, Color b, double t)
        {
            return Color.FromArgb(255,
                (int)Math.Round(a.R + (b.R - a.R) * t),
                (int)Math.Round(a.G + (b.G - a.G) * t),
                (int)Math.Round(a.B + (b.B - a.B) * t));
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
        private static int BadgeSize { get { return Math.Max(22, (int)Math.Round(25 * Scale)); } }

        // a text-only card whose name is long: the name goes below the corner marks (kind badges, "playing" mark) instead of under them
        private static bool TextCrowdsMarks(string text, Font font, int width)
        {
            int w = TextRenderer.MeasureText(text, font, new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix).Width;
            return w > width - 2 * (BadgeSize + 12);
        }

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
                if (kinds[i] == 0 && TextCrowdsMarks(texts[i], font, width))
                    h = textHeight + pad + BadgeSize + 12;
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

        // a soft fade into the color of the button at the edges of the picture, so it does not end abruptly
        private static void FadeEdges(Graphics g, Rectangle area, Color back)
        {
            int f = Math.Max(6, (int)(Math.Min(area.Width, area.Height) * 0.2));
            Color opaque = Color.FromArgb(255, back);
            Color clear = Color.FromArgb(0, back);
            Rectangle left = new Rectangle(area.X, area.Y, f, area.Height);
            Rectangle right = new Rectangle(area.Right - f, area.Y, f, area.Height);
            Rectangle top = new Rectangle(area.X, area.Y, area.Width, f);
            Rectangle bottom = new Rectangle(area.X, area.Bottom - f, area.Width, f);
            using (LinearGradientBrush b = new LinearGradientBrush(Rectangle.Inflate(left, 1, 1), opaque, clear, LinearGradientMode.Horizontal))
                g.FillRectangle(b, left);
            using (LinearGradientBrush b = new LinearGradientBrush(Rectangle.Inflate(right, 1, 1), clear, opaque, LinearGradientMode.Horizontal))
                g.FillRectangle(b, right);
            using (LinearGradientBrush b = new LinearGradientBrush(Rectangle.Inflate(top, 1, 1), opaque, clear, LinearGradientMode.Vertical))
                g.FillRectangle(b, top);
            using (LinearGradientBrush b = new LinearGradientBrush(Rectangle.Inflate(bottom, 1, 1), clear, opaque, LinearGradientMode.Vertical))
                g.FillRectangle(b, bottom);
        }

        private void PaintRow(Graphics g, Color text, Color accent)
        {
            int pad = (int)Math.Round(8 * Scale);
            int x = pad;
            int thumb = Height - 8;
            if (m_Picture != null)
            {
                Rectangle area = new Rectangle(x, 4, thumb, thumb);
                Region oldClip = g.Clip;
                g.SetClip(area, CombineMode.Intersect);
                float scale = m_PictureContain
                    ? Math.Min((float)area.Width / m_Picture.Width, (float)area.Height / m_Picture.Height)
                    : Math.Max((float)area.Width / m_Picture.Width, (float)area.Height / m_Picture.Height);
                int w = (int)Math.Ceiling(m_Picture.Width * scale);
                int h = (int)Math.Ceiling(m_Picture.Height * scale);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(m_Picture, area.X + (area.Width - w) / 2, area.Y + (area.Height - h) / 2, w, h);
                g.Clip = oldClip;
                oldClip.Dispose();
                x += thumb + pad;
            }
            else if (!string.IsNullOrEmpty(m_IconName))
            {
                int size = Math.Min(thumb, IconSize);
                string key = m_IconName + "|" + size + "|" + accent.ToArgb();
                if (m_IconCache == null || m_IconCacheKey != key)
                {
                    DisposeIconCache();
                    m_IconCache = IconLibrary.Get(m_IconName, new Size(size, size), Checked ? text : accent);
                    m_IconCacheKey = key;
                }
                if (m_IconCache != null)
                    g.DrawImage(m_IconCache, x, (Height - size) / 2, size, size);
                x += size + pad;
            }
            int right = Width - pad;
            if (m_Kind != 0)
                right = BadgeRect(0, BadgeCount()).X - 4;
            Rectangle textRect = new Rectangle(x, 0, Math.Max(10, right - x), Height);
            TextRenderer.DrawText(g, Text, Font, textRect, text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
                | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        }

        // the button which plays now: a "play" triangle in a pill at the top right, besides the fill and the border
        private void PaintPlayingMark(Graphics g, Color accent, Color text, Color window)
        {
            int d = Math.Max(22, (int)Math.Round(25 * Scale));
            Rectangle pill = new Rectangle(Width - d - 8, 6, d, d);
            using (GraphicsPath pp = Rounded(pill, d / 3))
            {
                using (SolidBrush pb = new SolidBrush(Color.FromArgb(235, accent)))
                    g.FillPath(pb, pp);
            }
            // dark symbol on a light color, light symbol on a dark one
            Color mark = ContrastRatio(accent, Color.White) >= ContrastRatio(accent, Color.Black) ? Color.White : Color.Black;
            // the triangle is a little to the right of the center so that it looks centered
            float th = d * 0.46f;
            float tw = th * 0.9f;
            float tx = pill.X + (d - tw) / 2f + d * 0.04f;
            float ty = pill.Y + (d - th) / 2f;
            using (SolidBrush bb = new SolidBrush(mark))
            {
                g.FillPolygon(bb, new PointF[] { new PointF(tx, ty), new PointF(tx + tw, ty + th / 2f), new PointF(tx, ty + th) });
            }
        }

        private static double Luminance(Color c)
        {
            Func<byte, double> f = v => { double x = v / 255.0; return x <= 0.03928 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4); };
            return 0.2126 * f(c.R) + 0.7152 * f(c.G) + 0.0722 * f(c.B);
        }

        private static double ContrastRatio(Color a, Color b)
        {
            double la = Luminance(a), lb = Luminance(b);
            return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
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
            Color border = p != null && !p.IsClassic ? p.ControlBorder : SystemColors.ControlDark;
            Color text = p != null && !p.IsClassic ? p.Text : SystemColors.ControlText;
            if (!m_Tint.IsEmpty)
            {
                surface = Mix(surface, m_Tint, 0.30);
                hover = Mix(hover, m_Tint, 0.45);
                selection = Mix(selection, m_Tint, 0.65);
                accent = m_Tint;
                border = Mix(border, m_Tint, 0.5);
            }

            Color parentBack = Parent != null ? ThemeManager.EffectiveBack(this) : window;
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

                if (m_ListMode)
                {
                    PaintRow(g, text, accent);
                }
                else if (m_Picture != null)
                {
                    int ph = PictureHeight;
                    Rectangle area = new Rectangle(1, 1, Width - 2, ph);
                    Region oldClip = g.Clip;
                    g.SetClip(path, CombineMode.Intersect);
                    g.SetClip(area, CombineMode.Intersect);
                    // cover: fill the area, cut what does not fit
                    float scale;
                    if (m_PictureContain)
                    {
                        // emoji: whole picture with a little room around it
                        Rectangle inner = Rectangle.Inflate(area, -6, -4);
                        scale = Math.Min((float)inner.Width / m_Picture.Width, (float)inner.Height / m_Picture.Height);
                    }
                    else
                    {
                        scale = Math.Max((float)area.Width / m_Picture.Width, (float)area.Height / m_Picture.Height);
                    }
                    int w = (int)Math.Ceiling(m_Picture.Width * scale);
                    int h = (int)Math.Ceiling(m_Picture.Height * scale);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(m_Picture, area.X + (area.Width - w) / 2, area.Y + (area.Height - h) / 2, w, h);
                    if (!m_PictureContain)
                        FadeEdges(g, area, back);
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
                else if (!m_ListMode && (m_Kind != 0 || Checked) && TextCrowdsMarks(Text, Font, Width))
                {
                    textTop = Math.Max(pad, BadgeSize + 12);
                }

                if (!m_ListMode)
                {
                Rectangle textRect = new Rectangle(pad, textTop, Width - 2 * pad, Math.Max(10, textBottom - textTop));
                TextRenderer.DrawText(g, Text, Font, textRect, text, TextFlags | TextFormatFlags.VerticalCenter);
                }

                Color line = Checked ? accent : (m_Hover ? accent : border);
                using (Pen pen = new Pen(line, Checked ? 2f : 1f))
                    g.DrawPath(pen, path);
                // keyboard focus: a visible ring inside the button (it must be seen on every theme and every color of button)
                if (Focused && ShowFocusCues)
                {
                    Rectangle ring = Rectangle.Inflate(r, -4, -4);
                    if (ring.Width > 8 && ring.Height > 8)
                    {
                        using (GraphicsPath rp = Rounded(ring, Math.Max(2, radius - 3)))
                        using (Pen fp = new Pen(text, 2f))
                            g.DrawPath(fp, rp);
                    }
                }

                if (m_Kind != 0)
                {
                    // one small badge for each kind: music (1) and / or sound effects (2)
                    List<string> glyphs = new List<string>();
                    if ((m_Kind & 1) != 0)
                        glyphs.Add("\u266B");
                    if ((m_Kind & 2) != 0)
                        glyphs.Add("\u26A1");
                    int bd = Math.Max(22, (int)Math.Round(25 * Scale));
                    using (Font gf = new Font("Segoe UI Symbol", Math.Max(10f, Font.Size + 2f), FontStyle.Bold))
                    {
                        for (int gi = 0; gi < glyphs.Count; gi++)
                        {
                            Rectangle pill = BadgeRect(gi, glyphs.Count);
                            using (GraphicsPath pp = Rounded(pill, bd / 3))
                            {
                                using (SolidBrush pb = new SolidBrush(Color.FromArgb(m_Picture != null && !m_ListMode ? 225 : 200, window)))
                                    g.FillPath(pb, pp);
                                using (Pen pbd = new Pen(Color.FromArgb(150, accent)))
                                    g.DrawPath(pbd, pp);
                            }
                            TextRenderer.DrawText(g, glyphs[gi], gf, pill, text,
                                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                        }
                    }
                }

                if (Checked && !m_ListMode)
                    PaintPlayingMark(g, accent, text, window);

                if (m_MultiSelected)
                {
                    using (Pen pen = new Pen(accent, 3f))
                    {
                        pen.DashStyle = DashStyle.Dash;
                        g.DrawPath(pen, path);
                    }
                    int d = Math.Max(14, (int)Math.Round(16 * Scale));
                    Rectangle badge = new Rectangle(Width - d - 6, 6, d, d);
                    using (SolidBrush b = new SolidBrush(accent))
                        g.FillEllipse(b, badge);
                    using (Pen tick = new Pen(Color.White, 2f))
                    {
                        g.DrawLines(tick, new Point[]
                        {
                            new Point(badge.X + d / 4, badge.Y + d / 2),
                            new Point(badge.X + d * 2 / 5, badge.Y + d * 2 / 3 + 1),
                            new Point(badge.X + d * 3 / 4, badge.Y + d / 3)
                        });
                    }
                }
            }
        }
    }
}
