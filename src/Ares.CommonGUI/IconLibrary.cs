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
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, object> s_Managed =
            new System.Runtime.CompilerServices.ConditionalWeakTable<object, object>();

        /// <summary>True for toolbars whose icon size is controlled by this class (the theme must not resize them).</summary>
        public static bool IsManaged(ToolStrip strip)
        {
            object o;
            return strip != null && s_Managed.TryGetValue(strip, out o);
        }

        /// <summary>Icon size for toolbars: 24 px at the default interface size (110%), scaled with the interface size.</summary>
        public static Size ToolbarIconSize()
        {
            int px = (int)Math.Round(24 * Math.Max(80, ThemeManager.ScalePercent) / 110f);
            return new Size(px, px);
        }

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ToolStrip, object> s_Factors =
            new System.Runtime.CompilerServices.ConditionalWeakTable<ToolStrip, object>();

        /// <summary>Makes the icons of a managed toolbar bigger (or smaller) than the standard toolbar size, e.g. 1.4 for the player buttons.</summary>
        public static void SetSizeFactor(ToolStrip strip, double factor)
        {
            try { s_Factors.Remove(strip); } catch (Exception) { }
            s_Factors.Add(strip, factor);
        }

        private static Size SizeFor(ToolStrip strip)
        {
            Size size = ToolbarIconSize();
            object f;
            if (s_Factors.TryGetValue(strip, out f))
            {
                int px = (int)Math.Round(size.Width * (double)f);
                size = new Size(px, px);
            }
            return size;
        }

        /// <summary>Icon size for menus: 20 px at the default interface size.</summary>
        public static Size MenuIconSize()
        {
            int px = (int)Math.Round(20 * Math.Max(80, ThemeManager.ScalePercent) / 110f);
            return new Size(px, px);
        }

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

        /// <summary>Names of all embedded icons (for the icon picker), sorted.</summary>
        public static List<string> AllNames()
        {
            List<string> result = new List<string>();
            foreach (string resource in Assembly.GetExecutingAssembly().GetManifestResourceNames())
            {
                if (resource.StartsWith("icon.") && resource.EndsWith(".png"))
                    result.Add(resource.Substring(5, resource.Length - 9));
            }
            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
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
            try { s_Managed.Add(strip, strip); } catch (ArgumentException) { }
            Action apply = () =>
            {
                Size size = SizeFor(strip);
                if (strip.ImageScalingSize != size)
                    strip.ImageScalingSize = size;
                lastSize = size;
                lastColor = ThemeColor();
                int applied = 0;
                Color color = ThemeColor();
                foreach (KeyValuePair<ToolStripItem, string> pair in items)
                {
                    Bitmap bmp = Get(pair.Value, size, color);
                    if (bmp == null)
                    {
                        ThemeManager.Log("icon missing: " + pair.Value, null);
                        continue;
                    }
                    Image old = pair.Key.Image;
                    pair.Key.ImageScaling = ToolStripItemImageScaling.None;
                    pair.Key.ImageTransparentColor = Color.Empty;
                    pair.Key.Image = bmp;
                    applied++;
                    if (old != null)
                        old.Dispose();
                }
                ThemeManager.Log("icons toolbar '" + strip.Name + "' size=" + size.Width + " applied=" + applied + "/" + items.Count, null);
                if (extra != null)
                    extra();
            };
            apply();
            // the toolbar size can change without a theme event (text scale applied at startup)
            EventHandler idle = (s, e) =>
            {
                try
                {
                    if (!strip.IsDisposed && (SizeFor(strip) != lastSize || strip.ImageScalingSize != lastSize || ThemeColor() != lastColor))
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
            BindMenu(menu, byName, false);
        }

        /// <summary>Same, for a context menu (<paramref name="topLevelItems"/> = true: the icons go on the menu's own items).</summary>
        public static void BindMenu(ToolStrip menu, IDictionary<string, string> byName, bool topLevelItems)
        {
            Size lastSize = Size.Empty;
            Color lastColor = Color.Empty;
            Action apply = () =>
            {
                Size size = MenuIconSize();
                Color color = ThemeColor();
                lastSize = size;
                lastColor = color;
                int applied = 0;
                try
                {
                    if (topLevelItems)
                    {
                        applied += ApplyMenuIcons(menu.Items, byName, size, color);
                    }
                    else
                    {
                        foreach (ToolStripItem top in menu.Items)
                        {
                            ToolStripDropDownItem dd = top as ToolStripDropDownItem;
                            if (dd != null)
                                applied += ApplyMenuIcons(dd.DropDownItems, byName, size, color);
                        }
                    }
                }
                catch (Exception ex)
                {
                    ThemeManager.Log("menu icons failed", ex);
                }
                ThemeManager.Log("icons menu '" + menu.Name + "' size=" + size.Width + " applied=" + applied + "/" + byName.Count, null);
            };
            apply();
            EventHandler idle = (s, e) =>
            {
                try
                {
                    if (!menu.IsDisposed && (MenuIconSize() != lastSize || ThemeColor() != lastColor))
                        apply();
                }
                catch (Exception)
                {
                }
            };
            Application.Idle += idle;
            menu.Disposed += (s, e) => Application.Idle -= idle;
        }

        private static int ApplyMenuIcons(ToolStripItemCollection items, IDictionary<string, string> byName, Size size, Color color)
        {
            int count = 0;
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
                        count++;
                        if (old != null)
                            old.Dispose();
                    }
                }
                ToolStripDropDownItem dd = item as ToolStripDropDownItem;
                if (dd != null && dd.HasDropDownItems)
                    count += ApplyMenuIcons(dd.DropDownItems, byName, size, color);
            }
            return count;
        }
    }
}
