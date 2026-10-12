using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace Ares.CommonGUI
{
    /// <summary>
    /// Images and icons of the element buttons in the Player. They are saved beside the project file
    /// (&lt;project&gt;.ares.icons), so that the .ares file itself is not changed.
    /// A setting is "lucide:name" (built-in icon) or "img:relative\\path.png" (a picture copied to the "ares_icons" folder).
    /// </summary>
    public static class ButtonImageStore
    {
        private static readonly Dictionary<string, Dictionary<string, string>> s_Cache =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Bitmap> s_Images = new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);

        private static string SidecarPath(string projectPath)
        {
            return projectPath + ".icons";
        }

        private static string Key(string mode, string element)
        {
            return (mode ?? "").Replace('\t', ' ') + "\t" + (element ?? "").Replace('\t', ' ');
        }

        private static Dictionary<string, string> Load(string projectPath)
        {
            Dictionary<string, string> map;
            if (s_Cache.TryGetValue(projectPath, out map))
                return map;
            map = new Dictionary<string, string>();
            try
            {
                string file = SidecarPath(projectPath);
                if (File.Exists(file))
                {
                    foreach (string line in File.ReadAllLines(file))
                    {
                        string[] parts = line.Split('\t');
                        if (parts.Length >= 3)
                            map[Key(parts[0], parts[1])] = parts[2];
                    }
                }
            }
            catch (Exception)
            {
            }
            s_Cache[projectPath] = map;
            return map;
        }

        private static void Save(string projectPath)
        {
            try
            {
                Dictionary<string, string> map = Load(projectPath);
                string file = SidecarPath(projectPath);
                if (map.Count == 0)
                {
                    if (File.Exists(file))
                        File.Delete(file);
                    return;
                }
                List<string> lines = new List<string>();
                foreach (KeyValuePair<string, string> pair in map)
                    lines.Add(pair.Key + "\t" + pair.Value);
                File.WriteAllLines(file, lines.ToArray());
            }
            catch (Exception)
            {
            }
        }

        public static string Get(string projectPath, string mode, string element)
        {
            if (string.IsNullOrEmpty(projectPath))
                return null;
            string spec;
            return Load(projectPath).TryGetValue(Key(mode, element), out spec) ? spec : null;
        }

        /// <summary>Sets the image/icon of a button (null or empty removes it).</summary>
        public static void Set(string projectPath, string mode, string element, string spec)
        {
            if (string.IsNullOrEmpty(projectPath))
                return;
            Dictionary<string, string> map = Load(projectPath);
            if (string.IsNullOrEmpty(spec))
                map.Remove(Key(mode, element));
            else
                map[Key(mode, element)] = spec;
            Save(projectPath);
        }

        public static void RenameElement(string projectPath, string mode, string oldTitle, string newTitle)
        {
            if (string.IsNullOrEmpty(projectPath))
                return;
            string spec = Get(projectPath, mode, oldTitle);
            if (spec == null)
                return;
            Dictionary<string, string> map = Load(projectPath);
            map.Remove(Key(mode, oldTitle));
            map[Key(mode, newTitle)] = spec;
            Save(projectPath);
        }

        public static void RenameMode(string projectPath, string oldMode, string newMode)
        {
            if (string.IsNullOrEmpty(projectPath))
                return;
            Dictionary<string, string> map = Load(projectPath);
            string prefix = Key(oldMode, "");
            List<KeyValuePair<string, string>> moved = new List<KeyValuePair<string, string>>();
            foreach (KeyValuePair<string, string> pair in map)
            {
                if (pair.Key.StartsWith(prefix))
                    moved.Add(pair);
            }
            if (moved.Count == 0)
                return;
            foreach (KeyValuePair<string, string> pair in moved)
            {
                map.Remove(pair.Key);
                map[Key(newMode, pair.Key.Substring(prefix.Length))] = pair.Value;
            }
            Save(projectPath);
        }

        /// <summary>Copies a picture (scaled down) into the "ares_icons" folder beside the project; returns the setting for it.</summary>
        public static string ImportImage(string projectPath, string sourceFile)
        {
            string dir = Path.Combine(Path.GetDirectoryName(projectPath), "ares_icons");
            Directory.CreateDirectory(dir);
            string name = Guid.NewGuid().ToString("N").Substring(0, 12) + ".png";
            using (FileStream fs = File.OpenRead(sourceFile))
            using (Image src = Image.FromStream(fs))
            {
                int max = 320;
                float scale = Math.Min(1f, (float)max / Math.Max(src.Width, src.Height));
                int w = Math.Max(1, (int)(src.Width * scale));
                int h = Math.Max(1, (int)(src.Height * scale));
                using (Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.DrawImage(src, 0, 0, w, h);
                    }
                    bmp.Save(Path.Combine(dir, name), ImageFormat.Png);
                }
            }
            return "img:ares_icons\\" + name;
        }

        /// <summary>The picture of an "img:" setting (cached, shared - do not dispose); null if missing.</summary>
        public static Bitmap LoadImage(string projectPath, string spec)
        {
            if (string.IsNullOrEmpty(projectPath) || spec == null || !spec.StartsWith("img:"))
                return null;
            try
            {
                string full = Path.Combine(Path.GetDirectoryName(projectPath), spec.Substring(4));
                Bitmap bmp;
                if (s_Images.TryGetValue(full, out bmp))
                    return bmp;
                if (!File.Exists(full))
                    return null;
                using (FileStream fs = File.OpenRead(full))
                using (Image img = Image.FromStream(fs))
                {
                    bmp = new Bitmap(img);
                }
                s_Images[full] = bmp;
                return bmp;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
