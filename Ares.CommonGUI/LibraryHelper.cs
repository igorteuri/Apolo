using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ares.Data;

namespace Ares.CommonGUI
{
    /// <summary>One group of audio files (a folder, or loose files) found in a drop.</summary>
    public class DroppedGroup
    {
        public string Name;
        public bool IsLoose;
        public List<string> Files = new List<string>();
    }

    /// <summary>Helpers shared by Player and Editor for drag &amp; drop of folders/files and the "Library" folder.</summary>
    public static class LibraryHelper
    {
        private static readonly string[] s_Extensions =
            { ".mp3", ".wav", ".ogg", ".opus", ".flac", ".m4a", ".aac", ".wma", ".aif", ".aiff", ".mp2", ".mp1" };

        public static bool IsAudioFile(string path)
        {
            return s_Extensions.Contains(Path.GetExtension(path).ToLowerInvariant());
        }

        public static string TitleOf(string file)
        {
            return Path.GetFileNameWithoutExtension(file);
        }

        /// <summary>
        /// Folder -> one group per folder that directly contains audio (subfolders become groups of their own).
        /// Loose audio files -> one group flagged IsLoose.
        /// </summary>
        public static List<DroppedGroup> Collect(IEnumerable<string> paths)
        {
            var result = new List<DroppedGroup>();
            var loose = new DroppedGroup { IsLoose = true };
            foreach (string p in paths)
            {
                try
                {
                    if (Directory.Exists(p))
                    {
                        Walk(p, new DirectoryInfo(p).Name, result);
                    }
                    else if (File.Exists(p) && IsAudioFile(p))
                    {
                        loose.Files.Add(p);
                    }
                }
                catch (Exception)
                {
                }
            }
            if (loose.Files.Count > 0)
            {
                loose.Files.Sort(StringComparer.CurrentCultureIgnoreCase);
                result.Add(loose);
            }
            return result;
        }

        private static void Walk(string dir, string name, List<DroppedGroup> result)
        {
            var files = new List<string>();
            try
            {
                files.AddRange(Directory.GetFiles(dir).Where(IsAudioFile));
            }
            catch (Exception)
            {
                return;
            }
            if (files.Count > 0)
            {
                files.Sort(StringComparer.CurrentCultureIgnoreCase);
                result.Add(new DroppedGroup { Name = name, Files = files });
            }
            string[] subs;
            try
            {
                subs = Directory.GetDirectories(dir);
            }
            catch (Exception)
            {
                return;
            }
            Array.Sort(subs, StringComparer.CurrentCultureIgnoreCase);
            foreach (string sub in subs)
            {
                Walk(sub, name + " - " + new DirectoryInfo(sub).Name, result);
            }
        }

        /// <summary>
        /// Creates the mode elements for one group: one per file, or a single playlist / random-sound container.
        /// </summary>
        public static List<IModeElement> BuildModeElements(DroppedGroup group, IEnumerable<string> files, DropOptions options)
        {
            var result = new List<IModeElement>();
            var list = files.ToList();
            if (list.Count == 0)
                return result;
            SoundFileType type = options.Sounds ? SoundFileType.SoundEffect : SoundFileType.Music;
            string name = group.IsLoose ? TitleOf(list[0]) : group.Name;
            if (options.OneElement && !(group.IsLoose && list.Count == 1))
            {
                if (group.IsLoose)
                    name = DropOptionsDialog.IsPt ? "Músicas soltas" : "Loose files";
                IElement container;
                if (options.Sounds)
                {
                    var choice = DataModule.ElementFactory.CreateChoiceContainer(name);
                    foreach (string f in list)
                        choice.AddElement(DataModule.ElementFactory.CreateFileElement(f, type));
                    container = choice;
                }
                else
                {
                    var playlist = DataModule.ElementFactory.CreateRandomBackgroundMusicList(name);
                    foreach (string f in list)
                        playlist.AddElement(DataModule.ElementFactory.CreateFileElement(f, type));
                    container = playlist;
                }
                result.Add(DataModule.ElementFactory.CreateModeElement(name, container));
            }
            else
            {
                foreach (string f in list)
                {
                    IFileElement fe = DataModule.ElementFactory.CreateFileElement(f, type);
                    result.Add(DataModule.ElementFactory.CreateModeElement(TitleOf(f), fe));
                }
            }
            return result;
        }

        // ---- Library folder (where Biblioteca.ares lives) ----

        private static string ConfigFile
        {
            get
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Ares");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "library.cfg");
            }
        }

        public static string DefaultLibraryFolder
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Ares Biblioteca"); }
        }

        public static string LibraryFolder
        {
            get
            {
                string folder = null;
                try
                {
                    if (File.Exists(ConfigFile))
                        folder = File.ReadAllText(ConfigFile).Trim();
                }
                catch (Exception)
                {
                }
                if (string.IsNullOrEmpty(folder))
                    folder = DefaultLibraryFolder;
                try
                {
                    Directory.CreateDirectory(folder);
                }
                catch (Exception)
                {
                    folder = DefaultLibraryFolder;
                    Directory.CreateDirectory(folder);
                }
                return folder;
            }
            set
            {
                Directory.CreateDirectory(value);
                File.WriteAllText(ConfigFile, value);
            }
        }

        public static string LibraryProjectPath
        {
            get { return Path.Combine(LibraryFolder, "Biblioteca.ares"); }
        }
    }
}
