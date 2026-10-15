/*
 Copyright (c) 2026 [Ares fork]

 This file is part of Ares.

 Ares is free software; you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation; either version 2 of the License, or
 (at your option) any later version.

 Ares is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 GNU General Public License for more details.
*/
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Ares.Player
{
    /// <summary>
    /// A seek bar like the one of a video player: shows the position in the current music title
    /// and allows to jump to another position by clicking or dragging.
    /// </summary>
    public class MusicProgressBar : Control
    {
        public class SeekEventArgs : EventArgs
        {
            public SeekEventArgs(double seconds)
            {
                Seconds = seconds;
            }
            public double Seconds { get; private set; }
        }

        /// <summary>
        /// Raised when the user wants to jump to another position.
        /// </summary>
        public event EventHandler<SeekEventArgs> SeekRequested;

        private double m_Position;
        private double m_Length;
        private bool m_Seekable;
        private bool m_Dragging;
        private double m_DragPosition;
        private bool m_Hover;
        private int m_HoverX;

        private const int Padding_ = 10;
        private string m_Title = "";

        /// <summary>Name of the music which is playing, shown between the two times.</summary>
        public string Title
        {
            get { return m_Title; }
            set
            {
                string v = value ?? "";
                if (v != m_Title)
                {
                    m_Title = v;
                    Invalidate();
                }
            }
        }

        /// <summary>
        /// Compact mode (for the small bars of sound effects): no hover bubble, less height.
        /// </summary>
        public bool Compact { get; set; }

        public MusicProgressBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                | ControlStyles.Opaque, true);
            TabStop = false;
            Size = new Size(300, 52);
        }

        /// <summary>
        /// Sets the current position and length of the music, in seconds.
        /// </summary>
        public void SetProgress(double position, double length)
        {
            bool wasSeekable = m_Seekable;
            double oldLength = m_Length;
            m_Length = length;
            m_Seekable = length > 0;
            if (!m_Dragging)
            {
                double oldPosition = m_Position;
                m_Position = position;
                // only repaint when something visible changed
                if (wasSeekable == m_Seekable && Math.Abs(oldLength - length) < 0.5
                    && (int)oldPosition == (int)position && PixelFor(oldPosition) == PixelFor(position))
                    return;
            }
            Invalidate();
        }

        /// <summary>
        /// Resets the bar (nothing is playing).
        /// </summary>
        public void Clear()
        {
            if (!m_Seekable && m_Position == 0 && m_Length == 0 && !m_Dragging)
                return;
            m_Seekable = false;
            m_Position = 0;
            m_Length = 0;
            m_Dragging = false;
            Invalidate();
        }

        private Rectangle TrackRectangle
        {
            get
            {
                int h = (m_Hover || m_Dragging) && m_Seekable ? 8 : 6;
                int y = TrackCenterY - h / 2;
                return new Rectangle(Padding_, y, Math.Max(1, Width - 2 * Padding_), h);
            }
        }

        private int TrackCenterY
        {
            get { return Compact ? 8 : 24; }
        }

        private int PixelFor(double seconds)
        {
            if (m_Length <= 0)
                return 0;
            double ratio = Math.Max(0, Math.Min(1, seconds / m_Length));
            return (int)Math.Round(ratio * (Width - 2 * Padding_));
        }

        private double SecondsFor(int x)
        {
            int width = Width - 2 * Padding_;
            if (width <= 0 || m_Length <= 0)
                return 0;
            double ratio = (double)(x - Padding_) / width;
            ratio = Math.Max(0, Math.Min(1, ratio));
            return ratio * m_Length;
        }

        private static string FormatTime(double seconds)
        {
            if (seconds < 0 || double.IsNaN(seconds) || double.IsInfinity(seconds))
                return "--:--";
            TimeSpan t = TimeSpan.FromSeconds(Math.Floor(seconds));
            if (t.TotalHours >= 1)
                return string.Format("{0}:{1:00}:{2:00}", (int)t.TotalHours, t.Minutes, t.Seconds);
            return string.Format("{0:00}:{1:00}", t.Minutes, t.Seconds);
        }

        // ------------------------------------------------------------------------
        // mouse
        // ------------------------------------------------------------------------

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            m_Hover = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            m_Hover = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && m_Seekable)
            {
                m_Dragging = true;
                m_DragPosition = SecondsFor(e.X);
                m_HoverX = e.X;
                Capture = true;
                Invalidate();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            m_HoverX = e.X;
            if (m_Dragging)
                m_DragPosition = SecondsFor(e.X);
            if (m_Seekable)
                Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (m_Dragging && e.Button == MouseButtons.Left)
            {
                m_Dragging = false;
                Capture = false;
                m_Position = m_DragPosition;
                Invalidate();
                EventHandler<SeekEventArgs> handler = SeekRequested;
                if (handler != null)
                    handler(this, new SeekEventArgs(m_DragPosition));
            }
        }

        // ------------------------------------------------------------------------
        // painting
        // ------------------------------------------------------------------------

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Ares.CommonGUI.ThemePalette p = Ares.CommonGUI.ThemeManager.Current;
            bool themed = p != null && !p.IsClassic;

            Color back = themed ? p.Window : SystemColors.Control;
            Color track = themed ? p.Border : SystemColors.ControlDark;
            Color fill = themed ? p.Accent : SystemColors.Highlight;
            Color text = themed ? p.Text : SystemColors.ControlText;
            Color dim = themed ? p.TextDim : SystemColors.GrayText;

            using (SolidBrush b = new SolidBrush(back))
                g.FillRectangle(b, ClientRectangle);

            g.SmoothingMode = SmoothingMode.AntiAlias;

            double shown = m_Dragging ? m_DragPosition : m_Position;
            Rectangle tr = TrackRectangle;

            // track
            using (GraphicsPath path = RoundedRect(tr, tr.Height / 2))
            using (SolidBrush b = new SolidBrush(m_Seekable ? track : Blend(track, back, 0.5)))
            {
                g.FillPath(b, path);
            }

            if (m_Seekable)
            {
                int fillWidth = PixelFor(shown);
                if (fillWidth > 0)
                {
                    Rectangle fr = new Rectangle(tr.X, tr.Y, Math.Max(tr.Height, fillWidth), tr.Height);
                    using (GraphicsPath path = RoundedRect(fr, tr.Height / 2))
                    using (SolidBrush b = new SolidBrush(fill))
                    {
                        g.FillPath(b, path);
                    }
                }
                // thumb
                int d = (m_Hover || m_Dragging) ? 14 : 10;
                int cx = tr.X + fillWidth;
                using (SolidBrush b = new SolidBrush(fill))
                {
                    g.FillEllipse(b, cx - d / 2, TrackCenterY - d / 2, d, d);
                }

                // hover time bubble
                if ((m_Hover || m_Dragging) && !Compact)
                {
                    double hoverSeconds = m_Dragging ? m_DragPosition : SecondsFor(m_HoverX);
                    string bubble = FormatTime(hoverSeconds);
                    Size bs = TextRenderer.MeasureText(g, bubble, Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
                    int bw = bs.Width + 12;
                    int bh = bs.Height + 4;
                    int bx = Math.Max(0, Math.Min(Width - bw, (m_Dragging ? cx : m_HoverX) - bw / 2));
                    Rectangle br = new Rectangle(bx, 0, bw, bh);
                    using (GraphicsPath path = RoundedRect(br, 4))
                    using (SolidBrush b = new SolidBrush(themed ? p.Surface : SystemColors.Info))
                    using (Pen pen = new Pen(track))
                    {
                        g.FillPath(b, path);
                        g.DrawPath(pen, path);
                    }
                    TextRenderer.DrawText(g, bubble, Font, br, text,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                }
            }

            // times
            string left = m_Seekable ? FormatTime(shown) : "--:--";
            string right = m_Seekable ? FormatTime(m_Length) : "--:--";
            int textY = TrackCenterY + (Compact ? 8 : 10);
            Rectangle leftRect = new Rectangle(Padding_ - 2, textY, Width / 2, Font.Height + 2);
            Rectangle rightRect = new Rectangle(Width / 2 - Padding_ + 2, textY, Width / 2, Font.Height + 2);
            TextRenderer.DrawText(g, left, Font, leftRect, m_Seekable ? text : dim,
                TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(g, right, Font, rightRect, dim,
                TextFormatFlags.Right | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            if (!Compact && m_Title.Length > 0)
            {
                int timeW = Math.Max(
                    TextRenderer.MeasureText(g, left, Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width,
                    TextRenderer.MeasureText(g, right, Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width);
                int tx = Padding_ + timeW + 10;
                int tw = Width - 2 * (Padding_ + timeW + 10);
                if (tw > 40)
                {
                    TextRenderer.DrawText(g, m_Title, Font, new Rectangle(tx, textY, tw, Font.Height + 2), text,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
                }
            }
        }

        private static Color Blend(Color a, Color b, double amountOfB)
        {
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * amountOfB),
                (int)(a.G + (b.G - a.G) * amountOfB),
                (int)(a.B + (b.B - a.B) * amountOfB));
        }

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = Math.Max(1, radius * 2);
            d = Math.Min(d, Math.Min(r.Width, r.Height));
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
