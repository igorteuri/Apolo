using System;
using System.Collections.Generic;
using System.IO;

namespace Ares.Player
{
    /// <summary>
    /// The list of the quick project switch (click on the project name in the status bar): the recent projects without
    /// the one which is open, without files which are gone, without repetitions, newest first.
    /// </summary>
    internal static class RecentProjects
    {
        public sealed class Entry
        {
            public string Path;
            public string Title;
        }

        public static List<Entry> ForSwitch(IEnumerable<KeyValuePair<string, string>> recent, string currentPath, Func<string, bool> exists, int max)
        {
            List<Entry> result = new List<Entry>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string current = Normalize(currentPath);
            if (current != null)
                seen.Add(current);
            foreach (KeyValuePair<string, string> pair in recent)
            {
                string path = Normalize(pair.Key);
                if (path == null || !seen.Add(path))
                    continue;
                if (!exists(pair.Key))
                    continue;
                string title = string.IsNullOrWhiteSpace(pair.Value) ? System.IO.Path.GetFileNameWithoutExtension(pair.Key) : pair.Value.Trim();
                result.Add(new Entry { Path = pair.Key, Title = title });
                if (result.Count >= max)
                    break;
            }
            return result;
        }

        private static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;
            try { return System.IO.Path.GetFullPath(path).TrimEnd('\\', '/'); }
            catch (Exception) { return path.Trim(); }
        }
    }
}
