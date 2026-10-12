using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;

namespace Ares.CommonGUI
{
    /// <summary>
    /// Modern line icons from the open-source "Lucide" set (https://lucide.dev, ISC license).
    /// The embedded images are white on transparent; they are recolored and scaled when requested.
    /// </summary>
    public static class IconLibrary
    {
        private static readonly Dictionary<string, Bitmap> s_Sources = new Dictionary<string, Bitmap>();

        private static Bitmap LoadSource(string name)
        {
            Bitmap bmp;
            if (s_Sources.TryGetValue(name, out bmp))
                return bmp;
            try
            {
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("icon." + name + ".png"))
                {
                    if (stream != null)
                        bmp = new Bitmap(stream);
                }
            }
            catch (Exception)
            {
                bmp = null;
            }
            s_Sources[name] = bmp;
            return bmp;
        }

        public static Color ThemeColor()
        {
            ThemePalette p = ThemeManager.Current;
            return (p != null && !p.IsClassic) ? p.Text : SystemColors.ControlText;
        }

        /// <summary>Returns the icon recolored and scaled to the given size (null if the icon does not exist).</summary>
        public static Bitmap Get(string name, Size size, Color color)
        {
            Bitmap src = LoadSource(name);
            if (src == null || size.Width < 4 || size.Height < 4)
                return null;
            Bitmap result = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(result))
            using (ImageAttributes attrs = new ImageAttributes())
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.SmoothingMode = SmoothingMode.HighQuality;
                float[][] matrix =
                {
                    new float[] { 0, 0, 0, 0, 0 },
                    new float[] { 0, 0, 0, 0, 0 },
                    new float[] { 0, 0, 0, 0, 0 },
                    new float[] { 0, 0, 0, color.A / 255f, 0 },
                    new float[] { color.R / 255f, color.G / 255f, color.B / 255f, 0, 1 }
                };
                // keep the source alpha, replace the color
                matrix[3] = new float[] { 0, 0, 0, 1, 0 };
                attrs.SetColorMatrix(new ColorMatrix(matrix));
                // small padding so strokes are not clipped
                int pad = Math.Max(1, size.Width / 10);
                Rectangle dest = new Rectangle(pad, pad, size.Width - 2 * pad, size.Height - 2 * pad);
                g.DrawImage(src, dest, 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, attrs);
            }
            return result;
        }

        public static Bitmap Get(string name, Size size)
        {
            return Get(name, size, ThemeColor());
        }

        /// <summary>
        /// Puts icons on toolbar items and keeps them updated when the theme or the scale changes.
        /// </summary>
        public static void Bind(ToolStrip strip, IDictionary<ToolStripItem, string> items, Action extra = null)
        {
            Size lastSize = Size.Empty;
            Color lastColor = Color.Empty;
            Action apply = () =>
            {
                Size size = strip.ImageScalingSize;
                lastSize = size;
                lastColor = ThemeColor();
                Color color = ThemeColor();
                foreach (KeyValuePair<ToolStripItem, string> pair in items)
                {
                    Bitmap bmp = Get(pair.Value, size, color);
                    if (bmp == null)
                        continue;
                    Image old = pair.Key.Image;
                    pair.Key.ImageScaling = ToolStripItemImageScaling.None;
                    pair.Key.ImageTransparentColor = Color.Empty;
                    pair.Key.Image = bmp;
                    if (old != null)
                        old.Dispose();
                }
                if (extra != null)
                    extra();
            };
            apply();
            // the toolbar size can change without a theme event (text scale applied at startup)
            EventHandler idle = (s, e) =>
            {
                try
                {
                    if (!strip.IsDisposed && (strip.ImageScalingSize != lastSize || ThemeColor() != lastColor))
                        apply();
                }
                catch (Exception)
                {
                }
            };
            Application.Idle += idle;
            strip.Disposed += (s, e) => Application.Idle -= idle;
            ThemeManager.Changed += (s, e) =>
            {
                try
                {
                    if (strip.IsDisposed)
                        return;
                    if (strip.IsHandleCreated && strip.InvokeRequired)
                        strip.BeginInvoke(apply);
                    else
                        apply();
                }
                catch (Exception)
                {
                }
            };
        }

        /// <summary>
        /// Puts icons on menu items (looked up by item name, sub-menus included; top-level headers get none).
        /// </summary>
        public static void BindMenu(ToolStrip menu, IDictionary<string, string> byName)
        {
            Size lastSize = Size.Empty;
            Color lastColor = Color.Empty;
            Action apply = () =>
            {
                int px = (int)Math.Round(18 * Math.Max(100, ThemeManager.ScalePercent) / 100f);
                Size size = new Size(px, px);
                Color color = ThemeColor();
                lastSize = size;
                lastColor = color;
                foreach (ToolStripItem top in menu.Items)
                {
                    ToolStripDropDownItem dd = top as ToolStripDropDownItem;
                    if (dd != null)
                        ApplyMenuIcons(dd.DropDownItems, byName, size, color);
                }
            };
            apply();
            EventHandler idle = (s, e) =>
            {
                try
                {
                    int px = (int)Math.Round(18 * Math.Max(100, ThemeManager.ScalePercent) / 100f);
                    if (!menu.IsDisposed && (new Size(px, px) != lastSize || ThemeColor() != lastColor))
                        apply();
                }
                catch (Exception)
                {
                }
            };
            Application.Idle += idle;
            menu.Disposed += (s, e) => Application.Idle -= idle;
        }

        private static void ApplyMenuIcons(ToolStripItemCollection items, IDictionary<string, string> byName, Size size, Color color)
        {
            foreach (ToolStripItem item in items)
            {
                string icon;
                if (item.Name != null && byName.TryGetValue(item.Name, out icon))
                {
                    Bitmap bmp = Get(icon, size, color);
                    if (bmp != null)
                    {
                        Image old = item.Image;
                        item.ImageScaling = ToolStripItemImageScaling.None;
                        item.ImageTransparentColor = Color.Empty;
                        item.Image = bmp;
                        if (old != null)
                            old.Dispose();
                    }
                }
                ToolStripDropDownItem dd = item as ToolStripDropDownItem;
                if (dd != null && dd.HasDropDownItems)
                    ApplyMenuIcons(dd.DropDownItems, byName, size, color);
            }
        }
    }
}
