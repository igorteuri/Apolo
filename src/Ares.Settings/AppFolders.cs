/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 Where Apolo keeps its data (%APPDATA%\Apolo) and the extension of the project files (.apolo).
 Data and projects of the old Ares names are copied over once, the old ones are never deleted.
*/
using System;
using System.IO;
using System.Threading;

namespace Ares.Settings
{
    public static class AppFolders
    {
        public const string FolderName = "Apolo";
        public const string LegacyFolderName = "Ares";

        private static string s_Root;
        private static readonly object s_Lock = new object();

        private static string AppData
        {
            get { return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData); }
        }

        /// <summary>%APPDATA%\Apolo (created and filled from %APPDATA%\Ares on the first call).</summary>
        public static string Root
        {
            get
            {
                lock (s_Lock)
                {
                    if (s_Root != null)
                        return s_Root;
                    // APOLO_DATA_DIR puts all the data of the program in another folder (a portable copy, the tests)
                    string forced = Environment.GetEnvironmentVariable("APOLO_DATA_DIR");
                    if (!string.IsNullOrEmpty(forced))
                    {
                        try { Directory.CreateDirectory(forced); } catch (Exception) { }
                        s_Root = forced;
                        return s_Root;
                    }
                    string root = Path.Combine(AppData, FolderName);
                    string legacy = Path.Combine(AppData, LegacyFolderName);
                    try
                    {
                        if (!Directory.Exists(root) && Directory.Exists(legacy))
                            Migrate(legacy, root);
                    }
                    catch (Exception)
                    {
                    }
                    try { Directory.CreateDirectory(root); } catch (Exception) { }
                    s_Root = root;
                    return root;
                }
            }
        }

        private static void Migrate(string from, string to)
        {
            // two programs (Player, Editor) can start together: only one copies
            bool created;
            using (Mutex mutex = new Mutex(false, "Apolo.FolderMigration"))
            {
                bool owned = false;
                try
                {
                    try { owned = mutex.WaitOne(15000); }
                    catch (AbandonedMutexException) { owned = true; }
                    if (Directory.Exists(to))
                        return;
                    created = true;
                    string temp = to + ".tmp";
                    if (Directory.Exists(temp))
                        Directory.Delete(temp, true);
                    CopyTree(from, temp);
                    if (created)
                        Directory.Move(temp, to);
                }
                finally
                {
                    if (owned)
                        mutex.ReleaseMutex();
                }
            }
        }

        private static void CopyTree(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (string file in Directory.GetFiles(from))
            {
                try { File.Copy(file, Path.Combine(to, Path.GetFileName(file)), true); }
                catch (Exception) { }
            }
            foreach (string dir in Directory.GetDirectories(from))
            {
                CopyTree(dir, Path.Combine(to, Path.GetFileName(dir)));
            }
        }

        public static string Combine(params string[] parts)
        {
            string result = Root;
            foreach (string part in parts)
                result = Path.Combine(result, part);
            return result;
        }
    }

    public static class ProjectFiles
    {
        public const string Extension = ".apolo";
        public const string LegacyExtension = ".ares";

        public static bool IsProjectFile(string path)
        {
            return path != null && (path.EndsWith(Extension, StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(LegacyExtension, StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsLegacy(string path)
        {
            return path != null && path.EndsWith(LegacyExtension, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>The project files (.apolo and .ares) of a folder; an old .ares is left out when its .apolo copy exists.</summary>
        public static string[] GetProjectFiles(string directory)
        {
            System.Collections.Generic.List<string> result = new System.Collections.Generic.List<string>(Directory.GetFiles(directory, "*" + Extension));
            foreach (string legacy in Directory.GetFiles(directory, "*" + LegacyExtension))
            {
                if (!legacy.EndsWith(LegacyExtension, StringComparison.OrdinalIgnoreCase))
                    continue; // (the search pattern also finds longer extensions)
                if (!File.Exists(ToCurrent(legacy)))
                    result.Add(legacy);
            }
            return result.ToArray();
        }

        /// <summary>The path of a project with the current extension (the same for projects which already have it).</summary>
        public static string ToCurrent(string path)
        {
            if (!IsLegacy(path))
                return path;
            return path.Substring(0, path.Length - LegacyExtension.Length) + Extension;
        }

        public static string ToLegacy(string path)
        {
            if (path == null || !path.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
                return path;
            return path.Substring(0, path.Length - Extension.Length) + LegacyExtension;
        }

        /// <summary>
        /// For an old .ares project: makes a copy with the .apolo extension (if there is none yet) and returns its path.
        /// The old file stays untouched. Returns the given path if nothing can be done.
        /// </summary>
        public static string Upgrade(string path)
        {
            if (!IsLegacy(path) || !File.Exists(path))
                return path;
            string current = ToCurrent(path);
            try
            {
                if (!File.Exists(current))
                    File.Copy(path, current, false);
                return current;
            }
            catch (Exception)
            {
                return path;
            }
        }
    }
}
