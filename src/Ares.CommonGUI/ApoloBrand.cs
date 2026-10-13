/*
 Apolo - a fork of Ares (Copyright (c) Jörg Ruedenauer), distributed under the GPL.
 Draws the Apolo sun logo (same geometry as Branding/apolo-logo.svg) for the window,
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
        // ray lengths (distance of the tip from the centre) of Branding/apolo-logo.svg: 12 rays, 30 degrees apart
        private static readonly float[] s_Lens = { 24.52f, 27.64f, 22.96f, 26.86f, 25.30f, 28.42f, 23.74f, 27.64f, 22.18f, 26.86f, 24.52f, 26.08f };
        // the playing animation of Branding/apolo-logo-tocando.svg: the tips go between 30 and 21.5
        private const float TipLong = 30f;
        private const float TipShort = 21.5f;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        /// <summary>
        /// Renders the logo on a transparent bitmap. phase (0..3) is the step of the playing animation
        /// (0 and 2 = the rays at rest, 1 and 3 = the rays move out / in; the middle dot is bigger at 1 and 3).
        /// Small sizes use the sturdier logo with 8 thick rays (Branding/apolo-logo-icone-pequeno.svg).
        /// </summary>
        public static Bitmap Render(int px, ApoloState state, int phase)
        {
            Bitmap bmp = new Bitmap(px, px, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                float k = px / 64f;
                bool small = px <= 32;
                Color rayColor, disc1, disc2, dark;
                if (state == ApoloState.Idle)
                {
                    rayColor = Color.FromArgb(150, 160, 172);
                    disc1 = Color.FromArgb(176, 185, 195);
                    disc2 = Color.FromArgb(130, 140, 152);
                    dark = Color.FromArgb(70, 78, 90);
                }
                else if (state == ApoloState.Paused)
                {
                    rayColor = Color.FromArgb(200, 255, 182, 39);
                    disc1 = Color.FromArgb(255, 213, 74);
                    disc2 = Color.FromArgb(255, 159, 28);
                    dark = Color.FromArgb(58, 34, 0);
                }
                else
                {
                    rayColor = Color.FromArgb(255, 182, 39);
                    disc1 = Color.FromArgb(255, 213, 74);
                    disc2 = Color.FromArgb(255, 159, 28);
                    dark = Color.FromArgb(58, 34, 0);
                }
                int rays = small ? 8 : 12;
                float inner = small ? 18f : 17.5f;
                float stroke = (small ? 6f : 4f) * k;
                float radius = small ? 14f : 12f;
                bool wave = state == ApoloState.Playing && (phase & 1) == 1;
                using (Pen pen = new Pen(rayColor, stroke))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    for (int i = 0; i < rays; i++)
                    {
                        double a = (-90 + (360.0 / rays) * i) * Math.PI / 180.0;
                        float len;
                        if (small)
                            len = (i % 2 == 0) ? 26.1f : 29.7f;
                        else
                            len = s_Lens[i];
                        if (state == ApoloState.Idle)
                            len = small ? 28f : 25.5f;
                        if (wave)
                        {
                            // neighbouring rays go in opposite directions; the second half of the cycle swaps them
                            bool outward = ((i % 2) == 0) == (phase == 1);
                            len = outward ? TipLong : TipShort;
                        }
                        float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
                        g.DrawLine(pen, (32 + inner * c) * k, (32 + inner * s) * k, (32 + len * c) * k, (32 + len * s) * k);
                    }
                }
                RectangleF disc = new RectangleF((32 - radius) * k, (32 - radius) * k, 2 * radius * k, 2 * radius * k);
                using (LinearGradientBrush br = new LinearGradientBrush(disc, disc1, disc2, 45f))
                    g.FillEllipse(br, disc);
                if (state == ApoloState.Paused)
                {
                    using (SolidBrush darkBrush = new SolidBrush(dark))
                    {
                        g.FillRectangle(darkBrush, 27f * k, 25f * k, 3.6f * k, 14f * k);
                        g.FillRectangle(darkBrush, 33.4f * k, 25f * k, 3.6f * k, 14f * k);
                    }
                }
                else
                {
                    // the eye of the sun: a ring and a dot (the dot pulses while playing)
                    float ringR = small ? 8f : 7f;
                    float ringW = (small ? 3f : 1.8f) * k;
                    float dotR = small ? 3f : 2.7f;
                    if (wave)
                        dotR = small ? 3.9f : 3.6f;
                    using (Pen ring = new Pen(dark, ringW))
                        g.DrawEllipse(ring, (32 - ringR) * k, (32 - ringR) * k, 2 * ringR * k, 2 * ringR * k);
                    using (SolidBrush dot = new SolidBrush(dark))
                        g.FillEllipse(dot, (32 - dotR) * k, (32 - dotR) * k, 2 * dotR * k, 2 * dotR * k);
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
