/*
 Apolo - a fork of Ares (Copyright (c) Jörg Ruedenauer), distributed under the GPL.
 Draws the Apolo sun logo (same geometry as Branding/apolo.svg) for the window,
 the tray icon (idle / playing / paused) and the About dialog.
*/
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace Ares.CommonGUI
{
    public enum ApoloState { Idle, Playing, Paused }

    public static class ApoloBrand
    {
        private static readonly int[] s_Lens = { 31, 23, 28, 21, 27, 22, 31, 22, 27, 21, 28, 23 };
        private const float Inner = 19f;
        private const float Radius = 13f;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        /// <summary>Renders the logo on a transparent bitmap. phase (0/1) alternates the ray lengths (playing animation).</summary>
        public static Bitmap Render(int px, ApoloState state, int phase)
        {
            Bitmap bmp = new Bitmap(px, px, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                float k = px / 64f;
                float stroke = (px > 20 ? 3.6f : 5.2f) * k;
                Color rayColor, disc1, disc2;
                if (state == ApoloState.Idle)
                {
                    rayColor = Color.FromArgb(150, 160, 172);
                    disc1 = Color.FromArgb(176, 185, 195);
                    disc2 = Color.FromArgb(130, 140, 152);
                }
                else if (state == ApoloState.Paused)
                {
                    rayColor = Color.FromArgb(200, 255, 182, 39);
                    disc1 = Color.FromArgb(255, 213, 74);
                    disc2 = Color.FromArgb(255, 159, 28);
                }
                else
                {
                    rayColor = Color.FromArgb(255, 182, 39);
                    disc1 = Color.FromArgb(255, 213, 74);
                    disc2 = Color.FromArgb(255, 159, 28);
                }
                using (Pen pen = new Pen(rayColor, stroke))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    for (int i = 0; i < 12; i++)
                    {
                        double a = (-90 + 30 * i) * Math.PI / 180.0;
                        int len = s_Lens[(i + (phase != 0 ? 3 : 0)) % 12];
                        if (state == ApoloState.Idle)
                            len = 25;
                        float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
                        g.DrawLine(pen, (32 + Inner * c) * k, (32 + Inner * s) * k, (32 + len * c) * k, (32 + len * s) * k);
                    }
                }
                RectangleF disc = new RectangleF((32 - Radius) * k, (32 - Radius) * k, 2 * Radius * k, 2 * Radius * k);
                using (LinearGradientBrush br = new LinearGradientBrush(disc, disc1, disc2, 45f))
                    g.FillEllipse(br, disc);
                if (state == ApoloState.Paused)
                {
                    using (SolidBrush dark = new SolidBrush(Color.FromArgb(120, 60, 0)))
                    {
                        g.FillRectangle(dark, 27f * k, 25f * k, 3.6f * k, 14f * k);
                        g.FillRectangle(dark, 33.4f * k, 25f * k, 3.6f * k, 14f * k);
                    }
                }
            }
            return bmp;
        }

        /// <summary>Creates an icon (the caller owns it and must keep it alive while it is shown).</summary>
        public static Icon CreateIcon(int px, ApoloState state, int phase)
        {
            using (Bitmap bmp = Render(px, state, phase))
            {
                IntPtr h = bmp.GetHicon();
                try
                {
                    return (Icon)Icon.FromHandle(h).Clone();
                }
                finally
                {
                    DestroyIcon(h);
                }
            }
        }

        private static readonly Dictionary<string, Icon> s_Cache = new Dictionary<string, Icon>();

        public static Icon GetIcon(int px, ApoloState state, int phase)
        {
            string key = px + "/" + state + "/" + phase;
            Icon icon;
            if (!s_Cache.TryGetValue(key, out icon))
            {
                icon = CreateIcon(px, state, phase);
                s_Cache[key] = icon;
            }
            return icon;
        }
    }
}
