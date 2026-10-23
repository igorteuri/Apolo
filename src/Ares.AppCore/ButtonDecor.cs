/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 What decorates the buttons and folders of a project: icon, picture / emoji and color. It is kept beside the project
 file ("[project].icons", lines "folder TAB button TAB setting"), so that the .apolo file itself does not change. This is
 the reader and writer without any picture code, shared by every version of the program; the Windows version also
 has its own reader with pictures (Ares.CommonGUI.ButtonImageStore) for the same file.

 A setting is "lucide:name" (a built-in icon), "lib:file.png" (a picture or emoji in the "icones" folder of the Apolo data
 folder) or "img:relative\path.png" (a picture beside the project). Colors are lines whose button name starts with "cor:".
*/
using System;
using System.Collections.Generic;
using System.IO;

namespace Ares.AppCore
{
    public static class ButtonDecor
    {
        private const string ColorPrefix = "cor:";

        private sealed class Entry
        {
            public DateTime Stamp;
            public Dictionary<string, string> Map;
        }

        private static readonly Dictionary<string, Entry> s_Cache = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);

        public static string SidecarFile(string projectPath)
        {
            return projectPath + ".icons";
        }

        /// <summary>The folder where the pictures and emojis of the buttons are kept.</summary>
        public static string IconsFolder
        {
            get { return Path.Combine(Ares.Settings.AppFolders.Root, "icones"); }
        }

        private static string Key(string folder, string button)
        {
            return (folder ?? "").Replace('\t', ' ') + "\t" + (button ?? "").Replace('\t', ' ');
        }

        private static Dictionary<string, string> Load(string projectPath)
        {
            string file = SidecarFile(projectPath);
            DateTime stamp = File.Exists(file) ? File.GetLastWriteTimeUtc(file) : DateTime.MinValue;
            Entry entry;
            if (s_Cache.TryGetValue(projectPath, out entry) && entry.Stamp == stamp)
                return entry.Map;
            Dictionary<string, string> map = new Dictionary<string, string>();
            try
            {
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
            s_Cache[projectPath] = new Entry { Stamp = stamp, Map = map };
            return map;
        }

        /// <summary>The setting of the icon / picture of a button, or of a folder when the button is empty; null if none.</summary>
        public static string Get(string projectPath, string folder, string button)
        {
            if (string.IsNullOrEmpty(projectPath))
                return null;
            string spec;
            return Load(projectPath).TryGetValue(Key(folder, button), out spec) ? spec : null;
        }

        /// <summary>The highlight color "#RRGGBB" of a button (or of a folder when the button is empty); null if none.</summary>
        public static string GetColor(string projectPath, string folder, string button)
        {
            if (string.IsNullOrEmpty(projectPath))
                return null;
            string spec;
            if (!Load(projectPath).TryGetValue(Key(folder, ColorPrefix + (button ?? "")), out spec) || spec == null)
                return null;
            return spec.Length == 7 && spec[0] == '#' ? spec : null;
        }

        /// <summary>The file of a "lib:" or "img:" setting (it may not exist), null for other settings.</summary>
        public static string ImagePath(string projectPath, string spec)
        {
            if (string.IsNullOrEmpty(spec) || spec.Length < 5)
                return null;
            if (spec.StartsWith("lib:", StringComparison.Ordinal))
                return Path.Combine(IconsFolder, spec.Substring(4));
            if (spec.StartsWith("img:", StringComparison.Ordinal) && !string.IsNullOrEmpty(projectPath))
            {
                // the older way wrote the folder separator of Windows
                string relative = spec.Substring(4).Replace('\\', Path.DirectorySeparatorChar);
                return Path.Combine(Path.GetDirectoryName(projectPath) ?? "", relative);
            }
            return null;
        }

        /// <summary>Name of the built-in icon of a "lucide:" setting, null for other settings.</summary>
        public static string IconName(string spec)
        {
            return spec != null && spec.StartsWith("lucide:", StringComparison.Ordinal) ? spec.Substring(7) : null;
        }

        private static void Save(string projectPath, Dictionary<string, string> map)
        {
            string file = SidecarFile(projectPath);
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

        /// <summary>Sets (or, with null, removes) the icon / picture setting of a button or folder.</summary>
        public static void Set(string projectPath, string folder, string button, string spec)
        {
            Dictionary<string, string> map = Load(projectPath);
            if (spec == null)
                map.Remove(Key(folder, button));
            else
                map[Key(folder, button)] = spec;
            Save(projectPath, map);
        }

        /// <summary>Sets (or, with null, removes) the highlight color "#RRGGBB".</summary>
        public static void SetColor(string projectPath, string folder, string button, string color)
        {
            Dictionary<string, string> map = Load(projectPath);
            string key = Key(folder, ColorPrefix + (button ?? ""));
            if (color == null)
                map.Remove(key);
            else
                map[key] = color;
            Save(projectPath, map);
        }
    }
}
