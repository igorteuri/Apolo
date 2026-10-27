/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 errors.log: where the errors that the program swallows on purpose are noted (a missing icon, a device which went away...).
 They do not bother the user, but when something odd happens this file tells what was going on.
 Each different error is written once per run; the file is kept small.
*/
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace Ares.Settings
{
    public static class ErrorLog
    {
        private const int MaxDistinct = 200;
        private const long MaxBytes = 256 * 1024;
        private static readonly object s_Lock = new object();
        private static readonly HashSet<string> s_Seen = new HashSet<string>();

        /// <summary>Notes an error that was caught and ignored. Never throws.</summary>
        public static void Note(Exception ex, [CallerMemberName] string member = null, [CallerFilePath] string file = null)
        {
            try
            {
                if (ex == null)
                    return;
                string where = (string.IsNullOrEmpty(file) ? "" : Path.GetFileName(file)) + "/" + member;
                string first = ex.StackTrace == null ? "" : ex.StackTrace.Split((char)10)[0];
                string key = where + "|" + ex.GetType().Name + "|" + ex.Message + "|" + first;
                lock (s_Lock)
                {
                    if (s_Seen.Count >= MaxDistinct || !s_Seen.Add(key))
                        return;
                    string path = Path.Combine(AppFolders.Root, "errors.log");
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    if (File.Exists(path) && new FileInfo(path).Length > MaxBytes)
                        File.Delete(path);
                    File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  [" + where + "]  " + ex.GetType().Name + ": " + ex.Message
                        + Environment.NewLine + "    " + first.Trim() + Environment.NewLine);
                }
            }
            catch (Exception)
            {
                // the log is the last resort: nothing more to do
            }
        }
    }
}
