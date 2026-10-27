/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 How many times each music file was played; shared by the Windows program and by Apolo Desktop.
*/
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Ares.Player
{
    /// <summary>How many times each music file was played (kept in %APPDATA%\Apolo\playcounts.txt).</summary>
    internal static class PlayCounts
    {
        private static readonly Dictionary<string, int> s_Counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private static bool s_Loaded;

        private static string FilePath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Path.Combine("Apolo", "playcounts.txt")); }
        }

        private static void Load()
        {
            if (s_Loaded)
                return;
            s_Loaded = true;
            try
            {
                if (!File.Exists(FilePath))
                    return;
                foreach (string line in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    int tab = line.LastIndexOf('\t');
                    int n;
                    if (tab > 0 && int.TryParse(line.Substring(tab + 1), out n))
                        s_Counts[line.Substring(0, tab)] = n;
                }
            }
            catch (Exception swallowed)
            {
                Ares.Settings.ErrorLog.Note(swallowed);
            }
        }

        public static int Get(string fullPath)
        {
            lock (s_Counts)
            {
                Load();
                int n;
                return !string.IsNullOrEmpty(fullPath) && s_Counts.TryGetValue(fullPath, out n) ? n : 0;
            }
        }

        public static int Add(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath))
                return 0;
            lock (s_Counts)
            {
                Load();
                int n;
                s_Counts.TryGetValue(fullPath, out n);
                n++;
                s_Counts[fullPath] = n;
                Save();
                return n;
            }
        }

        private static void Save()
        {
            try
            {
                string path = FilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                List<string> lines = new List<string>();
                foreach (KeyValuePair<string, int> pair in s_Counts)
                    lines.Add(pair.Key + "\t" + pair.Value);
                File.WriteAllLines(path, lines.ToArray(), Encoding.UTF8);
            }
            catch (Exception swallowed)
            {
                Ares.Settings.ErrorLog.Note(swallowed);
            }
        }
    }
}
