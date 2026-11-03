using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ares.Player
{
    /// <summary>
    /// Names of the folders (modes) of a project. The subfolders, colors and pictures find a folder by its name,
    /// so two folders must never share one (the comparison ignores upper/lower case, also of accented letters).
    /// </summary>
    internal static class FolderNames
    {
        /// <summary>"name", or "name 2", "name 3" ... when it is already taken. Spaces around the name are removed.</summary>
        public static string Unique(string name, IEnumerable<string> taken)
        {
            name = (name ?? "").Trim();
            HashSet<string> used = new HashSet<string>(taken.Where(t => t != null).Select(t => t.Trim()), StringComparer.OrdinalIgnoreCase);
            if (!used.Contains(name))
                return name;
            for (int i = 2; ; ++i)
            {
                string candidate = name + " " + i;
                if (!used.Contains(candidate))
                    return candidate;
            }
        }

        /// <summary>True when "name" may become the new name of a folder: not empty and no other folder has it.</summary>
        public static bool CanRename(string name, string current, IEnumerable<string> others)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0)
                return false;
            if (string.Equals(name, current, StringComparison.OrdinalIgnoreCase))
                return true;
            return !others.Any(t => string.Equals((t ?? "").Trim(), name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>A name to suggest for a new folder made of chosen files: the name of the folder they are in.</summary>
        public static string SuggestFor(IList<string> files)
        {
            if (files == null || files.Count == 0)
                return "";
            string dir = Path.GetDirectoryName(files[0]);
            string name = string.IsNullOrEmpty(dir) ? "" : Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            // a drive (C:\) has no folder name: use the first file instead
            if (string.IsNullOrEmpty(name) || name.EndsWith(":"))
                name = Path.GetFileNameWithoutExtension(files[0]);
            return name ?? "";
        }
    }
}
