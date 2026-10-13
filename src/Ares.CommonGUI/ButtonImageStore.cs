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
    /// (&lt;project&gt;.apolo.icons), so that the .apolo file itself is not changed.
    /// A setting is "lucide:name" (built-in icon), "lib:name.png" (a picture or emoji kept in the "icones" folder of
    /// Apolo, %APPDATA%\\Apolo\\icones, so that it is not lost when a project is moved) or "img:relative\\path.png"
    /// (the older way: a picture in the "apolo_icons" folder beside the project; it is moved to the Apolo folder when
    /// the project is opened).
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

        /// <summary>The folder of Apolo where the pictures and emojis of the buttons and folders are kept.</summary>
        public static string IconsFolder
        {
            get
            {
                string dir = Path.Combine(Ares.Settings.AppFolders.Root, "icones");
                try { Directory.CreateDirectory(dir); } catch (Exception) { }
                return dir;
            }
        }

        // the file of an "img:" or "lib:" setting (not checked for existence)
        private static string ImagePath(string projectPath, string spec)
        {
            if (spec == null || spec.Length < 5)
                return null;
            if (spec.StartsWith("lib:"))
                return Path.Combine(IconsFolder, spec.Substring(4));
            if (spec.StartsWith("img:") && !string.IsNullOrEmpty(projectPath))
                return Path.Combine(Path.GetDirectoryName(projectPath), spec.Substring(4));
            return null;
        }

        // moves the pictures of the older "img:" settings to the Apolo folder; true if something changed
        private static bool MigrateToLibrary(string projectPath, Dictionary<string, string> map)
        {
            List<string> keys = new List<string>();
            foreach (KeyValuePair<string, string> pair in map)
            {
                if (pair.Value != null && pair.Value.StartsWith("img:"))
                    keys.Add(pair.Key);
            }
            bool changed = false;
            foreach (string key in keys)
            {
                try
                {
                    string source = ImagePath(projectPath, map[key]);
                    if (source == null || !File.Exists(source))
                        continue;
                    string name = Path.GetFileName(source);
                    string target = Path.Combine(IconsFolder, name);
                    if (File.Exists(target) && new FileInfo(target).Length != new FileInfo(source).Length)
                    {
                        name = Guid.NewGuid().ToString("N").Substring(0, 8) + "-" + name;
                        target = Path.Combine(IconsFolder, name);
                    }
                    if (!File.Exists(target))
                        File.Copy(source, target);
                    map[key] = "lib:" + name;
                    changed = true;
                }
                catch (Exception)
                {
                }
            }
            return changed;
        }

        /// <summary>
        /// For the export of a project: the settings of the pictures in the portable form (the pictures in an
        /// "apolo_icons" folder beside the project) and the pictures to put into the package (zip entry name, file).
        /// </summary>
        public static string BuildPortable(string projectPath, Dictionary<string, string> files)
        {
            if (string.IsNullOrEmpty(projectPath))
                return null;
            Dictionary<string, string> map = Load(projectPath);
            if (map.Count == 0)
                return null;
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (KeyValuePair<string, string> pair in map)
            {
                string spec = pair.Value;
                if (spec.StartsWith("lib:") || spec.StartsWith("img:"))
                {
                    string source = ImagePath(projectPath, spec);
                    if (source == null || !File.Exists(source))
                        continue;
                    string name = Path.GetFileName(source);
                    files["Icons/apolo_icons/" + name] = source;
                    spec = "img:apolo_icons\\" + name;
                }
                sb.Append(pair.Key).Append('\t').Append(spec).Append("\r\n");
            }
            return sb.ToString();
        }

        /// <summary>Copies the files of a folder (what is already there stays).</summary>
        public static void CopyFolder(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (string file in Directory.GetFiles(from))
            {
                string target = Path.Combine(to, Path.GetFileName(file));
                if (!File.Exists(target))
                    File.Copy(file, target);
            }
        }

        /// <summary>Copies the button pictures of a project to the place of another project (for the export of a project).</summary>
        public static void CopyTo(string projectPath, string newProjectPath)
        {
            if (string.IsNullOrEmpty(projectPath) || string.IsNullOrEmpty(newProjectPath))
                return;
            try
            {
                Dictionary<string, string> map = Load(projectPath);
                if (map.Count == 0)
                    return;
                string from = Path.GetDirectoryName(projectPath);
                string to = Path.GetDirectoryName(newProjectPath);
                List<string> lines = new List<string>();
                foreach (KeyValuePair<string, string> pair in map)
                {
                    string spec = pair.Value;
                    if (spec.StartsWith("lib:"))
                    {
                        // the pictures live in the Apolo folder: the copy gets them in "apolo_icons" beside it
                        string source = ImagePath(projectPath, spec);
                        string name = spec.Substring(4);
                        string target = Path.Combine(Path.Combine(to, "apolo_icons"), name);
                        if (source != null && File.Exists(source))
                        {
                            if (!File.Exists(target))
                            {
                                Directory.CreateDirectory(Path.GetDirectoryName(target));
                                File.Copy(source, target);
                            }
                            spec = "img:apolo_icons\\" + name;
                        }
                    }
                    else if (spec.StartsWith("img:"))
                    {
                        string source = Path.Combine(from, spec.Substring(4));
                        string target = Path.Combine(to, spec.Substring(4));
                        if (File.Exists(source) && !File.Exists(target))
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(target));
                            File.Copy(source, target);
                        }
                    }
                    lines.Add(pair.Key + "\t" + spec);
                }
                File.WriteAllLines(SidecarPath(newProjectPath), lines.ToArray());
            }
            catch (Exception)
            {
            }
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
            bool upgraded = false;
            try
            {
                string file = SidecarPath(projectPath);
                if (!File.Exists(file))
                {
                    // project of the old Ares names: <project>.ares.icons and the folder "ares_icons"
                    string legacy = SidecarPath(Ares.Settings.ProjectFiles.ToLegacy(projectPath));
                    if (legacy != file && File.Exists(legacy))
                    {
                        file = legacy;
                        upgraded = true;
                    }
                }
                if (File.Exists(file))
                {
                    foreach (string line in File.ReadAllLines(file))
                    {
                        string[] parts = line.Split('\t');
                        if (parts.Length >= 3)
                        {
                            string spec = parts[2];
                            if (upgraded && spec.StartsWith("img:ares_icons\\", StringComparison.OrdinalIgnoreCase))
                                spec = "img:apolo_icons\\" + spec.Substring("img:ares_icons\\".Length);
                            map[Key(parts[0], parts[1])] = spec;
                        }
                    }
                }
                if (upgraded)
                {
                    string dir = Path.GetDirectoryName(projectPath);
                    string old = Path.Combine(dir, "ares_icons");
                    if (Directory.Exists(old))
                        CopyFolder(old, Path.Combine(dir, "apolo_icons"));
                }
            }
            catch (Exception)
            {
            }
            s_Cache[projectPath] = map;
            bool moved = MigrateToLibrary(projectPath, map);
            if (upgraded || moved)
                Save(projectPath);
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
            Dictionary<string, string> map = Load(projectPath);
            bool changed = false;
            string spec = Get(projectPath, mode, oldTitle);
            if (spec != null)
            {
                map.Remove(Key(mode, oldTitle));
                map[Key(mode, newTitle)] = spec;
                changed = true;
            }
            string color;
            if (map.TryGetValue(Key(mode, ColorPrefix + oldTitle), out color))
            {
                map.Remove(Key(mode, ColorPrefix + oldTitle));
                map[Key(mode, ColorPrefix + newTitle)] = color;
                changed = true;
            }
            if (changed)
                Save(projectPath);
        }

        // ---- background colors of buttons and folders ("#RRGGBB"), kept in the same sidecar file ----

        private const string ColorPrefix = "cor:";

        /// <summary>Highlight color of a button (element given) or of a folder (element null); Color.Empty if none.</summary>
        public static Color GetColor(string projectPath, string mode, string element)
        {
            if (string.IsNullOrEmpty(projectPath))
                return Color.Empty;
            string spec;
            if (!Load(projectPath).TryGetValue(Key(mode, ColorPrefix + (element ?? "")), out spec) || spec == null)
                return Color.Empty;
            if (spec.Length == 7 && spec[0] == '#')
            {
                try
                {
                    return Color.FromArgb(255, Convert.ToInt32(spec.Substring(1, 2), 16), Convert.ToInt32(spec.Substring(3, 2), 16),
                        Convert.ToInt32(spec.Substring(5, 2), 16));
                }
                catch (Exception)
                {
                }
            }
            return Color.Empty;
        }

        /// <summary>Sets the color of a button or folder (Color.Empty removes it).</summary>
        public static void SetColor(string projectPath, string mode, string element, Color color)
        {
            if (string.IsNullOrEmpty(projectPath))
                return;
            Dictionary<string, string> map = Load(projectPath);
            string key = Key(mode, ColorPrefix + (element ?? ""));
            if (color.IsEmpty)
                map.Remove(key);
            else
                map[key] = "#" + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");
            Save(projectPath);
        }

        /// <summary>Copies the picture of an emoji into the icons folder of Apolo; returns the setting for it.</summary>
        public static string ImportEmoji(string projectPath, string emojiFile, string code)
        {
            string dir = IconsFolder;
            string name = "emoji-" + code + ".png";
            string target = Path.Combine(dir, name);
            if (!File.Exists(target))
                File.Copy(emojiFile, target);
            return "lib:" + name;
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

        /// <summary>Copies a picture (scaled down) into the icons folder of Apolo; returns the setting for it.</summary>
        public static string ImportImage(string projectPath, string sourceFile)
        {
            string dir = IconsFolder;
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
            return "lib:" + name;
        }

        /// <summary>The picture of an "img:" or "lib:" setting (cached, shared - do not dispose); null if missing.</summary>
        public static Bitmap LoadImage(string projectPath, string spec)
        {
            if (spec == null || !(spec.StartsWith("img:") || spec.StartsWith("lib:")))
                return null;
            try
            {
                string full = ImagePath(projectPath, spec);
                if (full == null)
                    return null;
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
