using System;
using System.Collections.Generic;
using System.IO;

namespace Ares.CommonGUI
{
    /// <summary>Small key=value preference file (%APPDATA%\Ares\prefs.ini) for options added by this fork.</summary>
    public static class Preferences
    {
        private static Dictionary<string, string> s_Values;

        /// <summary>The data folder of Apolo (%APPDATA%\\Apolo).</summary>
        public static string DataFolder
        {
            get { return Ares.Settings.AppFolders.Root; }
        }

        private static string FilePath
        {
            get
            {
                string dir = Ares.Settings.AppFolders.Root;
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "prefs.ini");
            }
        }

        private static void Load()
        {
            if (s_Values != null)
                return;
            s_Values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (File.Exists(FilePath))
                {
                    foreach (string line in File.ReadAllLines(FilePath))
                    {
                        int i = line.IndexOf('=');
                        if (i > 0)
                            s_Values[line.Substring(0, i).Trim()] = line.Substring(i + 1).Trim();
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        public static string Get(string key, string defaultValue)
        {
            Load();
            string v;
            return s_Values.TryGetValue(key, out v) ? v : defaultValue;
        }

        public static bool GetBool(string key, bool defaultValue)
        {
            string v = Get(key, null);
            if (v == null)
                return defaultValue;
            return v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        // a line break in a value would start a new "key=value" line in the file
        private static string OneLine(string text)
        {
            return (text ?? "").Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');
        }

        public static void Set(string key, string value)
        {
            Load();
            s_Values[OneLine(key).Replace("=", "")] = OneLine(value);
            try
            {
                var lines = new List<string>();
                foreach (var pair in s_Values)
                    lines.Add(pair.Key + "=" + pair.Value);
                // written beside and then swapped: a crash while writing never leaves a half file (all options lost)
                string target = FilePath;
                string temp = target + ".tmp";
                File.WriteAllLines(temp, lines.ToArray());
                if (File.Exists(target))
                    File.Replace(temp, target, null);
                else
                    File.Move(temp, target);
            }
            catch (Exception swallowed)
            {
                Ares.Settings.ErrorLog.Note(swallowed);
            }
        }

        public static void SetBool(string key, bool value)
        {
            Set(key, value ? "1" : "0");
        }
    }
}
