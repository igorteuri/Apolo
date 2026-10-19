using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ares.CommonGUI
{
    /// <summary>One group of audio files (a folder, or loose files) found in a drop.</summary>
    public class DroppedGroup
    {
        public string Name;
        public bool IsLoose;
        public List<string> Files = new List<string>();
    }

    /// <summary>A folder of the computer which holds audio (directly or in its subfolders). Folders without audio are left out.</summary>
    public class AudioFolder
    {
        public string Name;
        public List<string> Files = new List<string>();
        public List<AudioFolder> Subfolders = new List<AudioFolder>();

        public bool IsLeaf
        {
            get { return Subfolders.Count == 0; }
        }

        /// <summary>Audio files in this folder and below.</summary>
        public int TotalFiles
        {
            get { return Files.Count + Subfolders.Sum(s => s.TotalFiles); }
        }

        /// <summary>Folders which directly hold audio, this one and below.</summary>
        public int AudioFolderCount
        {
            get { return (Files.Count > 0 ? 1 : 0) + Subfolders.Sum(s => s.AudioFolderCount); }
        }

        /// <summary>All files: first the ones of this folder, then the ones of the subfolders.</summary>
        public List<string> AllFiles()
        {
            List<string> result = new List<string>(Files);
            foreach (AudioFolder sub in Subfolders)
                result.AddRange(sub.AllFiles());
            return result;
        }

        public static string NameOf(string dir)
        {
            string trimmed = dir.TrimEnd('\\', '/');
            string name = Path.GetFileName(trimmed);
            if (string.IsNullOrEmpty(name))
                name = trimmed.Replace(":", "");
            return string.IsNullOrEmpty(name) ? "Pasta" : name;
        }

        /// <summary>Reads a folder; null when there is no audio in it or below it.</summary>
        public static AudioFolder Read(string dir)
        {
            AudioFolder result = new AudioFolder { Name = NameOf(dir) };
            try
            {
                result.Files.AddRange(Directory.GetFiles(dir).Where(DropScan.IsAudioFile));
                result.Files.Sort(StringComparer.CurrentCultureIgnoreCase);
                string[] subs = Directory.GetDirectories(dir);
                Array.Sort(subs, StringComparer.CurrentCultureIgnoreCase);
                foreach (string sub in subs)
                {
                    AudioFolder child = Read(sub);
                    if (child != null)
                        result.Subfolders.Add(child);
                }
            }
            catch (Exception)
            {
            }
            if (result.Files.Count == 0 && result.Subfolders.Count == 0)
                return null;
            return result;
        }
    }

    /// <summary>What the user dragged, seen from the point of view of "what can be asked".</summary>
    public enum DropShape
    {
        None = 0,
        /// <summary>A single audio file.</summary>
        OneFile = 1,
        /// <summary>Several loose audio files.</summary>
        Files = 2,
        /// <summary>One folder with audio only (no subfolders).</summary>
        Folder = 3,
        /// <summary>One folder which has subfolders.</summary>
        NestedFolder = 4,
        /// <summary>Several folders and / or files together.</summary>
        Several = 5
    }

    /// <summary>Where the dropped audio goes.</summary>
    public enum DropLayout
    {
        /// <summary>Into the folder which receives the drop: one button per folder of music (or per file).</summary>
        Here = 0,
        /// <summary>New folders like the ones of the computer: subfolders become folders with their own buttons.</summary>
        Tree = 1,
        /// <summary>A new folder per dropped folder; its subfolders become buttons inside it.</summary>
        Compact = 2,
        /// <summary>Into the receiving folder: one button per dropped folder, with all the music below it.</summary>
        Merge = 3
    }

    /// <summary>The result of reading what was dropped: loose files and folder trees.</summary>
    public class DropScan
    {
        private static readonly string[] s_Extensions =
            { ".mp3", ".wav", ".ogg", ".opus", ".flac", ".m4a", ".aac", ".wma", ".aif", ".aiff", ".mp2", ".mp1" };

        public readonly List<string> LooseFiles = new List<string>();
        public readonly List<AudioFolder> Folders = new List<AudioFolder>();

        public static bool IsAudioFile(string path)
        {
            return s_Extensions.Contains(Path.GetExtension(path).ToLowerInvariant());
        }

        public static DropScan Read(IEnumerable<string> paths)
        {
            DropScan scan = new DropScan();
            foreach (string p in paths)
            {
                try
                {
                    if (Directory.Exists(p))
                    {
                        AudioFolder folder = AudioFolder.Read(p);
                        if (folder != null)
                            scan.Folders.Add(folder);
                    }
                    else if (File.Exists(p) && IsAudioFile(p))
                    {
                        scan.LooseFiles.Add(p);
                    }
                }
                catch (Exception)
                {
                }
            }
            scan.LooseFiles.Sort(StringComparer.CurrentCultureIgnoreCase);
            return scan;
        }

        public int FileCount
        {
            get { return LooseFiles.Count + Folders.Sum(f => f.TotalFiles); }
        }

        /// <summary>Folders which directly hold audio.</summary>
        public int AudioFolderCount
        {
            get { return Folders.Sum(f => f.AudioFolderCount); }
        }

        public bool HasSubfolders
        {
            get { return Folders.Any(f => !f.IsLeaf); }
        }

        public bool IsEmpty
        {
            get { return LooseFiles.Count == 0 && Folders.Count == 0; }
        }

        public DropShape Shape
        {
            get
            {
                if (IsEmpty)
                    return DropShape.None;
                if (Folders.Count == 0)
                    return LooseFiles.Count == 1 ? DropShape.OneFile : DropShape.Files;
                if (Folders.Count == 1 && LooseFiles.Count == 0)
                    return Folders[0].IsLeaf ? DropShape.Folder : DropShape.NestedFolder;
                return DropShape.Several;
            }
        }

        /// <summary>The ways to place this drop. "hasDestination": the drop hit a folder / the buttons of an open folder.</summary>
        public List<DropLayout> AvailableLayouts(bool hasDestination)
        {
            List<DropLayout> result = new List<DropLayout>();
            if (IsEmpty)
                return result;
            if (hasDestination)
                result.Add(DropLayout.Here);
            if (Folders.Count > 0 || !hasDestination)
                result.Add(DropLayout.Tree);
            if (HasSubfolders)
                result.Add(DropLayout.Compact);
            if (hasDestination && HasSubfolders)
                result.Add(DropLayout.Merge);
            return result;
        }

        public DropLayout DefaultLayout(bool hasDestination)
        {
            if (!hasDestination)
                return DropLayout.Tree;
            if (Folders.Count == 0)
                return DropLayout.Here;
            return HasSubfolders ? DropLayout.Tree : DropLayout.Here;
        }
    }

    /// <summary>A folder (mode) of Apolo which receives groups of audio.</summary>
    public class PlannedFolder
    {
        public string Title;
        /// <summary>Title of the folder it goes into; null = top level.</summary>
        public string Parent;
        /// <summary>true = the folder is there already (only buttons are added), false = it is created.</summary>
        public bool Existing;
        public List<DroppedGroup> Groups = new List<DroppedGroup>();
    }

    /// <summary>The folders and groups a drop turns into (nothing is created here: it only decides).</summary>
    public class DropPlan
    {
        public readonly List<PlannedFolder> Folders = new List<PlannedFolder>();

        private readonly HashSet<string> m_Used;

        private DropPlan(IEnumerable<string> existingTitles)
        {
            m_Used = new HashSet<string>(existingTitles, StringComparer.Ordinal);
        }

        /// <param name="destination">Title of the existing folder which receives the drop; null = none.</param>
        /// <param name="existingTitles">Titles of the folders of the project (new folders get titles which are free).</param>
        /// <param name="looseTitle">Folder for loose files when there is no destination.</param>
        public static DropPlan Build(DropScan scan, DropLayout layout, string destination, IEnumerable<string> existingTitles, string looseTitle)
        {
            List<string> existing = existingTitles != null ? existingTitles.ToList() : new List<string>();
            DropPlan plan = new DropPlan(existing);
            bool hasDestination = destination != null && existing.Contains(destination);
            if (!hasDestination)
                destination = null;

            if (!hasDestination && (layout == DropLayout.Here || layout == DropLayout.Merge))
                layout = DropLayout.Tree;
            if (layout == DropLayout.Compact && !scan.HasSubfolders)
                layout = DropLayout.Tree;
            if (layout == DropLayout.Merge && scan.Folders.Count == 0)
                layout = DropLayout.Here;

            if (layout == DropLayout.Here || layout == DropLayout.Merge)
            {
                PlannedFolder target = new PlannedFolder { Title = destination, Existing = true };
                foreach (AudioFolder f in scan.Folders)
                {
                    if (layout == DropLayout.Merge)
                        target.Groups.Add(new DroppedGroup { Name = f.Name, Files = f.AllFiles() });
                    else
                        Flatten(f, f.Name, target.Groups);
                }
                AddLoose(scan, target.Groups);
                plan.Folders.Add(target);
                return plan;
            }

            bool compact = layout == DropLayout.Compact;
            foreach (AudioFolder f in scan.Folders)
                plan.AddTree(f, destination, compact);
            if (scan.LooseFiles.Count > 0)
            {
                PlannedFolder loose;
                if (hasDestination)
                {
                    loose = new PlannedFolder { Title = destination, Existing = true };
                }
                else if (existing.Contains(looseTitle))
                {
                    loose = new PlannedFolder { Title = looseTitle, Existing = true };
                }
                else
                {
                    loose = new PlannedFolder { Title = plan.Unique(looseTitle, null) };
                }
                AddLoose(scan, loose.Groups);
                plan.Folders.Add(loose);
            }
            return plan;
        }

        /// <summary>Groups of audio in all the planned folders.</summary>
        public int GroupCount
        {
            get { return Folders.Sum(f => f.Groups.Count); }
        }

        private static void AddLoose(DropScan scan, List<DroppedGroup> groups)
        {
            if (scan.LooseFiles.Count > 0)
                groups.Add(new DroppedGroup { IsLoose = true, Files = new List<string>(scan.LooseFiles) });
        }

        // every folder which holds audio becomes a group named "Parent - Child"
        private static void Flatten(AudioFolder folder, string name, List<DroppedGroup> groups)
        {
            if (folder.Files.Count > 0)
                groups.Add(new DroppedGroup { Name = name, Files = new List<string>(folder.Files) });
            foreach (AudioFolder sub in folder.Subfolders)
                Flatten(sub, name + " - " + sub.Name, groups);
        }

        private void AddTree(AudioFolder folder, string parent, bool compact)
        {
            PlannedFolder planned = new PlannedFolder { Title = Unique(folder.Name, parent), Parent = parent };
            Folders.Add(planned);
            if (folder.Files.Count > 0)
                planned.Groups.Add(new DroppedGroup { Name = folder.Name, Files = new List<string>(folder.Files) });
            foreach (AudioFolder sub in folder.Subfolders)
            {
                if (compact && sub.IsLeaf)
                    planned.Groups.Add(new DroppedGroup { Name = sub.Name, Files = new List<string>(sub.Files) });
                else
                    AddTree(sub, planned.Title, compact);
            }
        }

        // "Dia", else "Floresta - Dia", else "Floresta - Dia (2)" ...
        private string Unique(string wanted, string parent)
        {
            string title = wanted;
            if (m_Used.Contains(title) && parent != null)
                title = parent + " - " + wanted;
            string baseTitle = title;
            int n = 2;
            while (m_Used.Contains(title))
                title = baseTitle + " (" + n++ + ")";
            m_Used.Add(title);
            return title;
        }
    }
}
