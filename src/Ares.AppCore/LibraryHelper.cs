using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ares.Data;

namespace Ares.CommonGUI
{
    /// <summary>Helpers shared by Player and Editor for drag &amp; drop of folders/files and the "Library" folder.</summary>
    public static class LibraryHelper
    {
        public static bool IsAudioFile(string path)
        {
            return DropScan.IsAudioFile(path);
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
            Func<string, IFileElement> makeFile = f =>
            {
                IFileElement fe = DataModule.ElementFactory.CreateFileElement(f, type);
                fe.Effects.Volume = Math.Max(0, Math.Min(100, options.Volume));
                fe.Effects.FadeInTime = Math.Max(0, options.FadeInMs);
                fe.Effects.FadeOutTime = Math.Max(0, options.FadeOutMs);
                fe.Effects.CrossFading = !options.Sounds && options.CrossFade;
                return fe;
            };
            string name = group.IsLoose ? TitleOf(list[0]) : group.Name;
            if (options.OneElement && !(group.IsLoose && list.Count == 1))
            {
                if (group.IsLoose)
                    name = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt" ? "Músicas soltas" : "Loose files";
                IElement container;
                if (options.Sounds)
                {
                    if (options.Kind == PlaylistKind.Sequential)
                    {
                        var seq = DataModule.ElementFactory.CreateSequentialContainer(name);
                        foreach (string f in list)
                            seq.AddElement(makeFile(f));
                        container = seq;
                    }
                    else if (options.Kind == PlaylistKind.Parallel)
                    {
                        var par = DataModule.ElementFactory.CreateParallelContainer(name);
                        foreach (string f in list)
                            par.AddElement(makeFile(f));
                        container = par;
                    }
                    else
                    {
                        var choice = DataModule.ElementFactory.CreateChoiceContainer(name);
                        foreach (string f in list)
                            choice.AddElement(makeFile(f));
                        container = choice;
                    }
                }
                else if (options.Kind == PlaylistKind.Sequential)
                {
                    var playlist = DataModule.ElementFactory.CreateSequentialBackgroundMusicList(name);
                    foreach (string f in list)
                        playlist.AddElement(makeFile(f));
                    playlist.RepeatCount = options.Repeat ? -1 : 1;
                    container = playlist;
                }
                else
                {
                    var playlist = DataModule.ElementFactory.CreateRandomBackgroundMusicList(name);
                    foreach (string f in list)
                        playlist.AddElement(makeFile(f));
                    playlist.RepeatCount = options.Repeat ? -1 : 1;
                    container = playlist;
                }
                result.Add(DataModule.ElementFactory.CreateModeElement(name, container));
            }
            else
            {
                foreach (string f in list)
                {
                    IFileElement fe = makeFile(f);
                    result.Add(DataModule.ElementFactory.CreateModeElement(TitleOf(f), fe));
                }
            }
            return result;
        }

        /// <summary>The Windows program sets this to the reader of the old sidecar files (it knows the pictures); null elsewhere.</summary>
        public static Action<string> ReadIcons;

        private static void CopyFolder(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (string file in Directory.GetFiles(from))
            {
                string target = Path.Combine(to, Path.GetFileName(file));
                if (!File.Exists(target))
                    File.Copy(file, target);
            }
        }

        // ---- Library folder (where Biblioteca.apolo lives) ----

        private static string ConfigFile
        {
            get
            {
                string dir = Ares.Settings.AppFolders.Root;
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "library.cfg");
            }
        }

        public static string DefaultLibraryFolder
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Apolo Biblioteca"); }
        }

        private static string LegacyLibraryFolder
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
                if (string.IsNullOrEmpty(folder) ||
                    string.Equals(folder.TrimEnd('\\'), LegacyLibraryFolder, StringComparison.OrdinalIgnoreCase))
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
            get
            {
                string folder = LibraryFolder;
                string current = Path.Combine(folder, "Biblioteca" + Ares.Settings.ProjectFiles.Extension);
                if (!File.Exists(current))
                    CopyLegacyLibrary(folder, current);
                return current;
            }
        }

        // the library of the old Ares names (Biblioteca.ares, "Ares Biblioteca") is copied once; the old one stays
        private static void CopyLegacyLibrary(string folder, string current)
        {
            try
            {
                string legacyName = "Biblioteca" + Ares.Settings.ProjectFiles.LegacyExtension;
                string legacyFolder = File.Exists(Path.Combine(folder, legacyName)) ? folder : LegacyLibraryFolder;
                string legacy = Path.Combine(legacyFolder, legacyName);
                if (!File.Exists(legacy))
                    return;
                File.Copy(legacy, current, false);
                if (!string.Equals(legacyFolder, folder, StringComparison.OrdinalIgnoreCase))
                {
                    // the pictures of the buttons (they are found by the old name of the project)
                    string sidecar = legacy + ".icons";
                    string tempSidecar = Path.Combine(folder, legacyName + ".icons");
                    string pictures = Path.Combine(legacyFolder, "ares_icons");
                    string tempPictures = Path.Combine(folder, "ares_icons");
                    bool hadPictures = Directory.Exists(tempPictures);
                    if (File.Exists(sidecar) && !File.Exists(tempSidecar))
                    {
                        File.Copy(sidecar, tempSidecar, false);
                        if (Directory.Exists(pictures))
                            CopyFolder(pictures, tempPictures);
                        if (ReadIcons != null)
                        {
                            // the Windows program reads them: that makes the new files (<project>.apolo.icons, "apolo_icons")
                            ReadIcons(current);
                            File.Delete(tempSidecar);
                            if (!hadPictures && Directory.Exists(tempPictures))
                                Directory.Delete(tempPictures, true);
                        }
                        else
                        {
                            File.Move(tempSidecar, current + ".icons");
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
