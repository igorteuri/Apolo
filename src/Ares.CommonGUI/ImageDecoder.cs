using System;
using System.Drawing;
using System.IO;
#if NET8_0_OR_GREATER
using SkiaSharp;
#endif

namespace Ares.CommonGUI
{
    /// <summary>
    /// Pictures which Windows (GDI+) does not read itself: WebP. They are decoded with SkiaSharp and handed on
    /// as PNG, so that the rest of the program only has to know the usual formats.
    /// </summary>
    public static class ImageDecoder
    {
        public static bool IsWebp(byte[] d)
        {
            return d != null && d.Length > 12 && d[0] == 'R' && d[1] == 'I' && d[2] == 'F' && d[3] == 'F'
                && d[8] == 'W' && d[9] == 'E' && d[10] == 'B' && d[11] == 'P';
        }

        /// <summary>Bytes which GDI+ can read: the same bytes, or the PNG made from a WebP. Null when it cannot be read.</summary>
        public static byte[] ToReadable(byte[] data)
        {
            if (data == null || data.Length == 0)
                return null;
            if (!IsWebp(data))
                return data;
#if NET8_0_OR_GREATER
            try
            {
                using (SKBitmap bmp = SKBitmap.Decode(data))
                {
                    if (bmp == null)
                        return null;
                    using (SKImage image = SKImage.FromBitmap(bmp))
                    using (SKData png = image.Encode(SKEncodedImageFormat.Png, 100))
                        return png.ToArray();
                }
            }
            catch (Exception)
            {
                return null;
            }
#else
            return null;
#endif
        }

        /// <summary>Reads a picture file (also WebP) into a bitmap which does not need the file any more; null if it fails.</summary>
        public static Bitmap LoadBitmap(string path)
        {
            try
            {
                byte[] data = ToReadable(File.ReadAllBytes(path));
                if (data == null)
                    return null;
                using (MemoryStream ms = new MemoryStream(data))
                using (Image img = Image.FromStream(ms))
                    return new Bitmap(img);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
