/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 "Tocando agora": one slim strip below the header with a small chip for every sound effect which is playing
 (title, progress, repeat / pause / stop) and for every browser tab which makes or sends sound (with a mute button).
 It replaces the big status column on the left.
*/
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Ares.Playing;

namespace Ares.Player
{
    internal class NowPlayingStrip : Control
    {
        public const int StripHeight = 34;
        private const int ChipMin = 190;
        private const int ChipMax = 300;
        private const int Gap = 6;
        private const int Btn = 22;
        // the bottom strip of a sound chip is its progress line: a click anywhere along it (also under the buttons) seeks
        private const int SeekZone = 7;

        private enum Part { None, Body, Repeat, Pause, Stop, Mute }

        private class Chip
        {
            public bool Web;
            public int Handle;
            public string Title;
            public double Position;
            public double Length;
            public bool Paused;
            public bool Repeat;
            public bool Muted;
            public bool Vdo;
            public Action Toggle;
            public Rectangle Rect;
        }

        private readonly List<Chip> m_Sounds = new List<Chip>();
        private readonly List<Chip> m_Web = new List<Chip>();
        private readonly List<Chip> m_Shown = new List<Chip>();
        private int m_Overflow;
        private Rectangle m_OverflowRect = Rectangle.Empty;
        private Chip m_HoverChip;
        private Part m_HoverPart = Part.None;

        public event Action<int> StopRequested;
        public event Action<int> PauseRequested;
        public event Action<int, bool> RepeatRequested;
        public event Action<int, double> SeekRequested;

        public NowPlayingStrip()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
            Height = StripHeight;
            Visible = false;
            Ares.CommonGUI.ThemeManager.Changed += (s, e) => Invalidate();
        }

        private static string T(string pt, string en)
        {
            return System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt" ? pt : en;
        }

        public bool HasContent
        {
            get { return m_Sounds.Count > 0 || m_Web.Count > 0; }
        }

        /// <summary>true when the strip needs to be shown or hidden because of the new sounds</summary>
        public bool SetSounds(IList<PlayingFileInfo> sounds)
        {
            bool had = HasContent;
            m_Sounds.Clear();
            foreach (PlayingFileInfo s in sounds)
            {
                Chip c = new Chip();
                c.Handle = s.Handle;
                c.Title = s.Title;
                c.Position = s.Position;
                c.Length = s.Length;
                c.Paused = s.Paused;
                c.Repeat = s.Repeat;
                m_Sounds.Add(c);
            }
            Invalidate();
            return had != HasContent;
        }

        public bool SetWeb(IList<WebAudioInfo> infos)
        {
            bool had = HasContent;
            m_Web.Clear();
            foreach (WebAudioInfo i in infos)
            {
                Chip c = new Chip();
                c.Web = true;
                c.Title = i.Name;
                c.Vdo = i.Vdo;
                c.Muted = i.Muted;
                c.Toggle = i.Toggle;
                m_Web.Add(c);
            }
            Invalidate();
            return had != HasContent;
        }

        // ------------------------------------------------------------ layout

        private void LayoutChips()
        {
            m_Shown.Clear();
            m_Overflow = 0;
            m_OverflowRect = Rectangle.Empty;
            if (m_Sounds.Count == 0 && m_Web.Count == 0)
                return;
            int room = Width - 2;

            // browser tabs: at the right end
            int webW = 230;
            int webFit = 0;
            if (m_Web.Count > 0)
            {
                webFit = Math.Max(1, Math.Min(m_Web.Count, (room / 2 + Gap) / (webW + Gap)));
                webW = Math.Min(webW, Math.Max(120, (room / 2 - (webFit - 1) * Gap) / webFit));
                int x0 = Width - 1 - webFit * webW - (webFit - 1) * Gap;
                for (int i = 0; i < webFit; i++)
                {
                    m_Web[i].Rect = new Rectangle(x0 + i * (webW + Gap), 2, webW, Height - 4);
                    m_Shown.Add(m_Web[i]);
                }
            }
            int webArea = webFit > 0 ? webFit * webW + (webFit - 1) * Gap + Gap * 2 : 0;

            // sound effects: from the left
            int soundRoom = room - webArea;
            int count = m_Sounds.Count;
            if (count == 0)
                return;
            int fit = Math.Max(1, (soundRoom + Gap) / (ChipMin + Gap));
            int reserve = 0;
            if (count > fit)
            {
                reserve = 46;
                fit = Math.Max(1, (soundRoom - reserve + Gap) / (ChipMin + Gap));
                count = Math.Min(count, fit);
                m_Overflow = m_Sounds.Count - count;
            }
            int w = Math.Max(100, Math.Min(ChipMax, (soundRoom - reserve - (count - 1) * Gap) / count));
            int x = 1;
            for (int i = 0; i < count; i++)
            {
                m_Sounds[i].Rect = new Rectangle(x, 2, w, Height - 4);
                m_Shown.Add(m_Sounds[i]);
                x += w + Gap;
            }
            if (m_Overflow > 0)
                m_OverflowRect = new Rectangle(x, 2, 40, Height - 4);
        }

        private static Color MixColors(Color a, Color b, double t)
        {
            return Color.FromArgb(255,
                (int)Math.Round(a.R + (b.R - a.R) * t),
                (int)Math.Round(a.G + (b.G - a.G) * t),
                (int)Math.Round(a.B + (b.B - a.B) * t));
        }

        private static Rectangle StopRect(Rectangle r)
        {
            return new Rectangle(r.Right - Btn - 3, r.Y + (r.Height - SeekZone - Btn) / 2, Btn, Btn);
        }

        private static Rectangle PauseRect(Rectangle r)
        {
            return new Rectangle(r.Right - 2 * Btn - 3, r.Y + (r.Height - SeekZone - Btn) / 2, Btn, Btn);
        }

        private static Rectangle RepeatRect(Rectangle r)
        {
            return new Rectangle(r.Right - 3 * Btn - 3, r.Y + (r.Height - SeekZone - Btn) / 2, Btn, Btn);
        }

        private static Rectangle MuteRect(Rectangle r)
        {
            return new Rectangle(r.Right - Btn - 3, r.Y + (r.Height - Btn) / 2, Btn, Btn);
        }

        private Part HitPart(Chip c, Point p)
        {
            if (!c.Rect.Contains(p))
                return Part.None;
            if (c.Web)
                return MuteRect(c.Rect).Contains(p) ? Part.Mute : Part.Body;
            if (p.Y >= c.Rect.Bottom - SeekZone)
                return Part.Body;
            if (StopRect(c.Rect).Contains(p))
                return Part.Stop;
            if (PauseRect(c.Rect).Contains(p))
                return Part.Pause;
            if (RepeatRect(c.Rect).Contains(p))
                return Part.Repeat;
            return Part.Body;
        }

        // ------------------------------------------------------------ mouse

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Chip hc = null;
            Part hp = Part.None;
            foreach (Chip c in m_Shown)
            {
                Part p = HitPart(c, e.Location);
                if (p != Part.None)
                {
                    hc = c;
                    hp = p;
                    break;
                }
            }
            if (hc != m_HoverChip || hp != m_HoverPart)
            {
                m_HoverChip = hc;
                m_HoverPart = hp;
                Cursor = hp == Part.None ? Cursors.Default : Cursors.Hand;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            m_HoverChip = null;
            m_HoverPart = Part.None;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left)
                return;
            foreach (Chip c in m_Shown)
            {
                Part p = HitPart(c, e.Location);
                if (p == Part.None)
                    continue;
                if (c.Web)
                {
                    if (p == Part.Mute && c.Toggle != null)
                        c.Toggle();
                    return;
                }
                if (p == Part.Stop && StopRequested != null)
                    StopRequested(c.Handle);
                else if (p == Part.Pause && PauseRequested != null)
                    PauseRequested(c.Handle);
                else if (p == Part.Repeat && RepeatRequested != null)
                    RepeatRequested(c.Handle, !c.Repeat);
                else if (p == Part.Body && SeekRequested != null && c.Length > 0)
                {
                    int left = c.Rect.X + 6;
                    int width = Math.Max(1, c.Rect.Width - 12);
                    double f = Math.Max(0, Math.Min(1, (e.X - left) / (double)width));
                    SeekRequested(c.Handle, f * c.Length);
                }
                return;
            }
        }

        // ------------------------------------------------------------ painting

        private static GraphicsPath Round(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d - 1, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d - 1, r.Bottom - d - 1, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d - 1, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static string Time(double s)
        {
            if (s < 0)
                s = 0;
            int t = (int)s;
            return (t / 60) + ":" + (t % 60).ToString("00");
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Ares.CommonGUI.ThemePalette p = Ares.CommonGUI.ThemeManager.Current;
            bool themed = p != null && !p.IsClassic;
            Color back = themed ? p.Window : SystemColors.Control;
            Color chipBack = themed ? p.Surface : SystemColors.ControlLight;
            Color border = themed ? p.Border : SystemColors.ControlDark;
            Color text = themed ? p.Text : SystemColors.ControlText;
            Color dim = themed ? p.TextDim : SystemColors.GrayText;
            Color accent = themed ? p.Accent : SystemColors.Highlight;
            Color hover = themed ? p.Hover : SystemColors.ControlLightLight;
            Graphics g = e.Graphics;
            g.Clear(back);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            LayoutChips();

            if (m_Shown.Count == 0)
            {
                TextRenderer.DrawText(g, T("Tocando agora: nada. Efeitos sonoros e abas com som aparecem aqui.",
                    "Playing now: nothing. Sound effects and tabs with sound show up here."), Font,
                    new Rectangle(8, 0, Math.Max(10, Width - 16), Height), dim,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.Left | TextFormatFlags.NoPadding);
            }

            foreach (Chip c in m_Shown)
            {
                Rectangle r = c.Rect;
                using (GraphicsPath path = Round(r, 8))
                {
                    using (SolidBrush b = new SolidBrush(c.Web ? MixColors(chipBack, accent, 0.22) : chipBack))
                        g.FillPath(b, path);
                    if (!c.Web && c.Length > 0)
                    {
                        // progress fill
                        int fillW = (int)(r.Width * Math.Max(0, Math.Min(1, c.Position / c.Length)));
                        if (fillW > 2)
                        {
                            Region old = g.Clip;
                            g.SetClip(path, CombineMode.Intersect);
                            using (SolidBrush fb = new SolidBrush(Color.FromArgb(70, accent)))
                                g.FillRectangle(fb, r.X, r.Y, fillW, r.Height);
                            g.Clip = old;
                        }
                    }
                    using (Pen pen = new Pen(c.Web && !c.Muted ? accent : border))
                        g.DrawPath(pen, path);
                }

                string icon;
                Color iconColor;
                if (c.Web)
                {
                    icon = c.Muted ? "🔇" : (c.Vdo ? "⇧" : "♪");
                    iconColor = c.Muted ? dim : accent;
                }
                else
                {
                    icon = "⚡"; // sound effect
                    iconColor = accent;
                }
                int bodyHeight = c.Web ? r.Height : r.Height - SeekZone;
                if (!c.Web && c.Length > 0)
                {
                    // thin progress line along the bottom edge
                    Rectangle track = new Rectangle(r.X + 8, r.Bottom - 6, Math.Max(4, r.Width - 16), 3);
                    using (SolidBrush tb = new SolidBrush(Color.FromArgb(80, dim)))
                        g.FillRectangle(tb, track);
                    int lineW = (int)(track.Width * Math.Max(0, Math.Min(1, c.Position / c.Length)));
                    if (lineW > 0)
                    {
                        using (SolidBrush lb = new SolidBrush(accent))
                            g.FillRectangle(lb, track.X, track.Y, lineW, track.Height);
                    }
                }
                Rectangle iconRect = new Rectangle(r.X + 6, r.Y, 18, bodyHeight);
                TextRenderer.DrawText(g, icon, Font, iconRect, iconColor,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);

                int buttonsWidth = c.Web ? Btn + 6 : 3 * Btn + 6;
                string title = c.Title ?? "";
                if (c.Web)
                    title = (c.Vdo ? T("Enviando: ", "Sending: ") : "") + title + (c.Muted ? T(" (mudo)", " (muted)") : "");
                else if (c.Length > 0)
                    title += "  " + Time(c.Position) + "/" + Time(c.Length);
                Rectangle textRect = new Rectangle(r.X + 26, r.Y, Math.Max(10, r.Width - 26 - buttonsWidth), bodyHeight);
                TextRenderer.DrawText(g, title, Font, textRect, c.Muted ? dim : text,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.Left | TextFormatFlags.NoPadding);

                if (c.Web)
                {
                    DrawButton(g, MuteRect(r), c.Muted ? "🔇" : "🔊", c, Part.Mute, hover, c.Muted ? accent : dim);
                }
                else
                {
                    DrawButton(g, RepeatRect(r), "↻", c, Part.Repeat, hover, c.Repeat ? accent : dim);
                    DrawButton(g, PauseRect(r), c.Paused ? "▶" : "❚❚", c, Part.Pause, hover, text);
                    DrawButton(g, StopRect(r), "■", c, Part.Stop, hover, text);
                }
            }

            if (m_Overflow > 0)
            {
                using (GraphicsPath path = Round(m_OverflowRect, 8))
                using (Pen pen = new Pen(border))
                    g.DrawPath(pen, path);
                TextRenderer.DrawText(g, "+" + m_Overflow, Font, m_OverflowRect, dim,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
            }
        }

        private void DrawButton(Graphics g, Rectangle r, string glyph, Chip chip, Part part, Color hover, Color color)
        {
            if (chip == m_HoverChip && part == m_HoverPart)
            {
                using (GraphicsPath path = Round(r, 5))
                using (SolidBrush b = new SolidBrush(hover))
                    g.FillPath(b, path);
            }
            TextRenderer.DrawText(g, glyph, Font, r, color,
                TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
        }
    }
}
