using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Apolo.Desktop
{
    /// <summary>The pictures of the buttons: the line icons of the program and the pictures / emojis of the project.</summary>
    internal static class IconCache
    {
        private static readonly Dictionary<string, Bitmap> s_Lucide = new Dictionary<string, Bitmap>();
        private static readonly Dictionary<string, KeyValuePair<DateTime, Bitmap>> s_Files = new Dictionary<string, KeyValuePair<DateTime, Bitmap>>();

        /// <summary>A built-in line icon (black with transparency, to be painted with a color through a mask); null if there is none.</summary>
        public static Bitmap Lucide(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            Bitmap bitmap;
            if (s_Lucide.TryGetValue(name, out bitmap))
                return bitmap;
            try
            {
                using (Stream stream = AssetLoader.Open(new Uri("avares://Apolo.Desktop/Icons/" + name + ".png")))
                    bitmap = new Bitmap(stream);
            }
            catch (Exception)
            {
                bitmap = null;
            }
            s_Lucide[name] = bitmap;
            return bitmap;
        }

        /// <summary>The brush which paints the shape of an icon with any color: use it as the OpacityMask of a filled rectangle.</summary>
        public static IBrush Mask(string name)
        {
            Bitmap bitmap = Lucide(name);
            return bitmap == null ? null : new ImageBrush(bitmap) { Stretch = Stretch.Uniform };
        }

        /// <summary>A picture file (a button picture or an emoji); null if it is missing or cannot be read.</summary>
        public static Bitmap File(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;
            try
            {
                if (!System.IO.File.Exists(path))
                    return null;
                DateTime stamp = System.IO.File.GetLastWriteTimeUtc(path);
                KeyValuePair<DateTime, Bitmap> cached;
                if (s_Files.TryGetValue(path, out cached) && cached.Key == stamp)
                    return cached.Value;
                Bitmap bitmap;
                using (FileStream stream = System.IO.File.OpenRead(path))
                    bitmap = new Bitmap(stream);
                s_Files[path] = new KeyValuePair<DateTime, Bitmap>(stamp, bitmap);
                return bitmap;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static IBrush Brush(string hex, byte alpha = 255)
        {
            if (hex == null || hex.Length != 7 || hex[0] != '#')
                return null;
            try
            {
                Color c = Color.Parse(hex);
                return new SolidColorBrush(Color.FromArgb(alpha, c.R, c.G, c.B));
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
