/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 Pictures, emojis, icons and colors of the buttons and folders (kept beside the project, see ButtonImageStore):
 they go into exported packages and follow the project when it is saved under another name.
*/
using System;
using System.Collections.Generic;
using System.IO;

namespace Ares.Editor
{
    static class ProjectIcons
    {
        /// <summary>The extra files of a package; tempFile must be deleted by the caller when the export is finished.</summary>
        public static Dictionary<string, string> PrepareExport(string projectFileName, out string tempFile)
        {
            Dictionary<string, string> extras = new Dictionary<string, string>();
            tempFile = null;
            if (string.IsNullOrEmpty(projectFileName))
                return extras;
            try
            {
                string sidecar = Ares.CommonGUI.ButtonImageStore.BuildPortable(projectFileName, extras);
                if (!string.IsNullOrEmpty(sidecar))
                {
                    tempFile = Path.Combine(Path.GetTempPath(), "apolo-icons-" + Guid.NewGuid().ToString("N") + ".txt");
                    File.WriteAllText(tempFile, sidecar, new System.Text.UTF8Encoding(false));
                    extras["Icons/icons.txt"] = tempFile;
                }
            }
            catch (Exception)
            {
                extras.Clear();
                tempFile = null;
            }
            return extras;
        }

        public static void Cleanup(string tempFile)
        {
            if (tempFile == null)
                return;
            try { File.Delete(tempFile); } catch (Exception) { }
        }

        public static void CopyForSaveAs(string oldPath, string newPath)
        {
            try
            {
                if (string.IsNullOrEmpty(oldPath) || string.IsNullOrEmpty(newPath))
                    return;
                if (string.Equals(Path.GetFullPath(oldPath), Path.GetFullPath(newPath), StringComparison.OrdinalIgnoreCase))
                    return;
                Ares.CommonGUI.ButtonImageStore.CopyTo(oldPath, newPath);
            }
            catch (Exception)
            {
            }
        }
    }
}
