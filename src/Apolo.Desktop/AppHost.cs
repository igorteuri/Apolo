using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ares.AppCore;
using Ares.CommonGUI;
using Ares.Data;
using Ares.Players;
using Ares.Player;

namespace Apolo.Desktop
{
    /// <summary>
    /// The engine of the program without any window: settings, sound output, the open project and what plays.
    /// The windows only look at it and ask it to do things.
    /// </summary>
    public sealed class AppHost : IDisposable
    {
        private Ares.Playing.AudioInit m_Audio;
        private readonly Ares.Settings.BasicSettings m_Basic = new Ares.Settings.BasicSettings();

        public PlayingControl Control { get; private set; }
        public IProject Project { get; private set; }
        public string ProjectPath { get; private set; }
        public ModeTree Tree { get; private set; }
        public string Warning { get; private set; }

        public static bool IsPt
        {
            get { return System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt"; }
        }

        public static string T(string pt, string en)
        {
            return IsPt ? pt : en;
        }

        /// <summary>Reads the settings and opens the sound output. noDevice: without a sound card (tests, screenshots).</summary>
        public void Start(bool noDevice)
        {
            bool found = m_Basic.ReadFromFile();
            Ares.Settings.Settings.Instance.Initialize(Ares.Settings.Settings.PlayerID, found ? m_Basic.GetSettingsDir() : null);
            m_Audio = new Ares.Playing.AudioInit(noDevice ? 0 : -1, s => Warning = s);
            Control = new PlayingControl();
            ApplySettings();
            if (!noDevice)
                UseSavedOutput();
            Ares.Playing.AudioLeveler.Enabled = Ares.CommonGUI.Preferences.GetBool("LevelerOn", true);
            Ares.Playing.SoundRepeat.CrossfadeEnabled = Ares.CommonGUI.Preferences.GetBool("SfxLoopCrossfade", true);
            Tree = new ModeTree();
        }

        // sound cards are saved with their name, because their numbers change when devices are plugged in or removed
        private void UseSavedOutput()
        {
            try
            {
                Ares.Settings.Settings s = Ares.Settings.Settings.Instance;
                int index = Ares.Playing.AudioInit.ResolveIndex(s.OutputDeviceIndex, s.OutputDeviceName);
                if (index != -1)
                    m_Audio.SwitchDevice(index);
                s.OutputDeviceIndex = index;
                s.OutputDeviceName = Ares.Playing.AudioInit.NameOf(index);
            }
            catch (Exception)
            {
                Ares.Settings.Settings.Instance.OutputDeviceIndex = -1;
                Ares.Settings.Settings.Instance.OutputDeviceName = string.Empty;
            }
        }

        /// <summary>Uses the folders of the settings and switches to the chosen sound card; null if all went well, else a message.</summary>
        public string ApplySettings(int outputDevice)
        {
            Control.UpdateDirectories();
            string problem = null;
            Ares.Settings.Settings s = Ares.Settings.Settings.Instance;
            if (outputDevice != s.OutputDeviceIndex)
            {
                try
                {
                    m_Audio.SwitchDeviceKeepPlaying(outputDevice);
                    s.OutputDeviceIndex = outputDevice;
                    s.OutputDeviceName = Ares.Playing.AudioInit.NameOf(outputDevice);
                }
                catch (Ares.Playing.AudioInitException ex)
                {
                    problem = ex.Message;
                }
            }
            SaveSettings();
            return problem;
        }

        private void ApplySettings()
        {
            Ares.Settings.Settings s = Ares.Settings.Settings.Instance;
            Control.UpdateDirectories();
            Control.SetPlayMusicOnAllSpeakers(s.PlayMusicOnAllSpeakers);
            Control.SetFadingOnPreviousNext(s.ButtonMusicFadeMode != 0, s.ButtonMusicFadeMode == 2, s.ButtonMusicFadeTime);
            Control.SetMusicTagFading(s.TagMusicFadeTime, s.TagMusicFadeOnlyOnChange);
            Control.GlobalVolume = s.GlobalVolume;
            Control.MusicVolume = s.MusicVolume;
            Control.SoundVolume = s.SoundVolume;
        }

        public IList<Ares.Settings.RecentFiles.ProjectEntry> Recent
        {
            get { return Ares.Settings.Settings.Instance.RecentFiles.GetFiles(); }
        }

        public void StopAll()
        {
            Ares.Playing.PlayingModule.ProjectPlayer.StopAll();
        }

        /// <summary>Opens a project file (.apolo or .ares). Throws with a message if it cannot be read.</summary>
        public void Open(string path)
        {
            StopAll();
            IProject loaded = DataModule.ProjectManager.LoadProject(path);
            if (Project != null)
                DataModule.ProjectManager.UnloadProject(Project);
            Project = loaded;
            ProjectPath = path;
            Tree = ModeTree.Load(path);
            Tree.Sync(Project.GetModes().Select(m => m.Title).ToList());
            Ares.Playing.PlayingModule.ProjectPlayer.SetProject(Project);
            Ares.Settings.Settings.Instance.RecentFiles.AddFile(new Ares.Settings.RecentFiles.ProjectEntry(path, Project.Title));
            Ares.CommonGUI.Preferences.Set("LastProject", path);
            SaveSettings();
        }

        public void SaveSettings()
        {
            try
            {
                Ares.Settings.Settings s = Ares.Settings.Settings.Instance;
                s.GlobalVolume = Control.GlobalVolume;
                s.MusicVolume = Control.MusicVolume;
                s.SoundVolume = Control.SoundVolume;
                s.WriteToFile(m_Basic.GetSettingsDir());
                m_Basic.WriteToFile();
            }
            catch (Exception)
            {
            }
        }

        /// <summary>The project file made by the last import of a pack.</summary>
        public string ImportedProject { get; private set; }

        private sealed class QuietMonitor : Ares.ModelInfo.IProgressMonitor
        {
            public bool Canceled { get { return false; } }
            public void IncreaseProgress(double percent) { }
            public void IncreaseProgress(double percent, string text) { }
            public void SetProgress(int percent, string text) { }
            public void SetIndeterminate(string text) { }
        }

        private sealed class YesBoxes : Ares.ModelInfo.IMessageBoxProvider
        {
            public Ares.ModelInfo.MessageBoxResult ShowYesNoCancelBox(string prompt) { return Ares.ModelInfo.MessageBoxResult.Yes; }
        }

        /// <summary>Unpacks a pack (.apkg) into Documents/Apolo/Pacotes; "done" is called when it ends (error = null if all went well).</summary>
        public void Import(string packFile, Action<Exception> done)
        {
            string forced = Environment.GetEnvironmentVariable("APOLO_DATA_DIR");
            string folder = !string.IsNullOrEmpty(forced) ? Path.Combine(forced, "Pacotes")
                : Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Apolo"), "Pacotes");
            Directory.CreateDirectory(folder);
            string name = Path.GetFileNameWithoutExtension(packFile);
            string target = Path.Combine(folder, name + ".apolo");
            for (int n = 2; File.Exists(target); n++)
                target = Path.Combine(folder, name + " " + n + ".apolo");
            StopAll();
            Ares.ModelInfo.Importer.Import(new QuietMonitor(), packFile, target, true, new YesBoxes(), (error, cancelled) =>
            {
                ImportedProject = error == null && !cancelled && File.Exists(target) ? target : null;
                done(error);
            });
        }

        // ---- editing: new project, import of files and folders

        /// <summary>Makes an empty project file (and opens it).</summary>
        public void CreateProject(string path, string title)
        {
            StopAll();
            IProject created = DataModule.ProjectManager.CreateProject(title);
            created.FileName = path;
            DataModule.ProjectManager.SaveProject(created);
            DataModule.ProjectManager.UnloadProject(created);
            Open(path);
        }

        private void EnsureProjectFile(string path)
        {
            if (File.Exists(path))
                return;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            IProject created = DataModule.ProjectManager.CreateProject("Biblioteca");
            created.FileName = path;
            DataModule.ProjectManager.SaveProject(created);
            DataModule.ProjectManager.UnloadProject(created);
        }

        public sealed class ImportSummary
        {
            public int NewFolders;
            public int Buttons;
            /// <summary>The folder to show after the import.</summary>
            public string ShowTitle;
        }

        /// <summary>
        /// Puts what was dropped in the open project (the Library is used when no project is open): the same plan as the
        /// Windows program. destination = title of the folder which receives the audio (null = new folders).
        /// </summary>
        public ImportSummary ImportDropped(DropScan scan, DropOptions options, string destination)
        {
            if (Project == null)
            {
                string library = LibraryHelper.LibraryProjectPath;
                EnsureProjectFile(library);
                Open(library);
            }
            PushUndo(destination);
            StopAll();
            if (destination != null && ModeByTitle(destination) == null)
                destination = null;
            string loose = IsPt ? "Músicas soltas" : "Loose music";
            DropPlan plan = DropPlan.Build(scan, options.Layout, destination, Project.GetModes().Select(m => m.Title).ToList(), loose);
            PlannedFolder firstNew = plan.Folders.FirstOrDefault(f => !f.Existing);
            ImportSummary summary = new ImportSummary { ShowTitle = firstNew != null ? firstNew.Title : destination };
            summary.NewFolders = plan.Folders.Count(f => !f.Existing);

            foreach (PlannedFolder planned in plan.Folders)
            {
                IMode mode = ModeByTitle(planned.Title);
                if (mode == null)
                {
                    if (planned.Existing)
                        continue;
                    mode = Project.AddMode(planned.Title);
                }
                // what is in the folder already is not added twice
                HashSet<string> present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (planned.Existing)
                {
                    foreach (IModeElement old in mode.GetElements())
                    {
                        IFileElement oldFile = old.StartElement as IFileElement;
                        if (oldFile != null)
                            present.Add(oldFile.FilePath);
                    }
                }
                foreach (DroppedGroup group in planned.Groups)
                {
                    List<string> files = group.Files.Where(f => !present.Contains(f)).ToList();
                    foreach (IModeElement element in LibraryHelper.BuildModeElements(group, files, options))
                    {
                        mode.AddElement(element);
                        summary.Buttons++;
                    }
                    foreach (string file in files)
                        present.Add(file);
                }
            }

            // nest the new folders: inside their parent, or (top level) at the end
            Tree.Sync(Project.GetModes().Select(m => m.Title).ToList());
            foreach (PlannedFolder planned in plan.Folders)
            {
                if (planned.Existing)
                    continue;
                ModeNode node = Tree.Find(planned.Title);
                ModeNode parent = planned.Parent != null ? Tree.Find(planned.Parent) : null;
                if (node != null && parent != null && parent != node)
                {
                    Tree.Attach(node, parent, -1);
                    parent.Collapsed = false;
                }
            }
            SaveProjectNow();
            return summary;
        }

        // ---- undo / redo: before every change a copy of the project files is kept (project, pictures / colors, folder tree)

        private sealed class Snapshot
        {
            public byte[] Project;
            public byte[] Icons;
            public byte[] Tree;
            public string FolderTitle;
        }

        private readonly List<Snapshot> m_Undo = new List<Snapshot>();
        private readonly List<Snapshot> m_Redo = new List<Snapshot>();
        private string m_UndoProject = "";
        private DateTime m_LastUndo = DateTime.MinValue;
        private const int MaxUndo = 30;

        public bool CanUndo { get { return Project != null && m_UndoProject == ProjectPath && m_Undo.Count > 0; } }
        public bool CanRedo { get { return Project != null && m_UndoProject == ProjectPath && m_Redo.Count > 0; } }

        private static byte[] ReadOrNull(string path)
        {
            try { return File.Exists(path) ? File.ReadAllBytes(path) : null; }
            catch (Exception) { return null; }
        }

        private static void WriteOrDelete(string path, byte[] data)
        {
            try
            {
                if (data == null)
                {
                    if (File.Exists(path))
                        File.Delete(path);
                }
                else
                {
                    File.WriteAllBytes(path, data);
                }
            }
            catch (Exception)
            {
            }
        }

        private Snapshot Capture(string folderTitle)
        {
            if (Project == null || string.IsNullOrEmpty(ProjectPath))
                return null;
            Snapshot snap = new Snapshot { Project = ReadOrNull(ProjectPath), FolderTitle = folderTitle };
            if (snap.Project == null)
                return null;
            snap.Icons = ReadOrNull(ButtonDecor.SidecarFile(ProjectPath));
            snap.Tree = ReadOrNull(ModeTree.FileFor(ProjectPath));
            return snap;
        }

        /// <summary>Called before every change of the project; changes within two seconds count as one step.</summary>
        public void PushUndo(string folderTitle)
        {
            try
            {
                if (Project == null)
                    return;
                DateTime now = DateTime.UtcNow;
                bool quick = (now - m_LastUndo).TotalSeconds < 2;
                m_LastUndo = now;
                if (m_UndoProject != ProjectPath)
                {
                    m_Undo.Clear();
                    m_Redo.Clear();
                    m_UndoProject = ProjectPath;
                    quick = false;
                }
                if (quick)
                    return;
                Snapshot snap = Capture(folderTitle);
                if (snap == null)
                    return;
                m_Undo.Add(snap);
                if (m_Undo.Count > MaxUndo)
                    m_Undo.RemoveAt(0);
                m_Redo.Clear();
            }
            catch (Exception)
            {
            }
        }

        /// <summary>Goes one step back (or forward); returns the title of the folder to show, or null when there was nothing to do.</summary>
        public string Travel(bool redo, string currentFolder)
        {
            List<Snapshot> from = redo ? m_Redo : m_Undo;
            List<Snapshot> to = redo ? m_Undo : m_Redo;
            if (Project == null || from.Count == 0 || m_UndoProject != ProjectPath)
                return null;
            string path = ProjectPath;
            Snapshot target = from[from.Count - 1];
            from.RemoveAt(from.Count - 1);
            Snapshot now = Capture(currentFolder);
            if (now != null)
                to.Add(now);
            StopAll();
            WriteOrDelete(path, target.Project);
            WriteOrDelete(ButtonDecor.SidecarFile(path), target.Icons);
            WriteOrDelete(ModeTree.FileFor(path), target.Tree);
            Open(path);
            m_UndoProject = path;
            m_LastUndo = DateTime.MinValue;
            string title = currentFolder != null && ModeByTitle(currentFolder) != null ? currentFolder : target.FolderTitle;
            return title ?? "";
        }

        // ---- one new button from files (a music playlist or a sound effect)

        /// <summary>Makes one button of the files in the given folder (made if needed); returns the title of the folder.</summary>
        public string AddButton(IList<string> files, string name, bool sfx, string folder)
        {
            if (Project == null)
            {
                string library = LibraryHelper.LibraryProjectPath;
                EnsureProjectFile(library);
                Open(library);
            }
            PushUndo(folder);
            string wanted = !string.IsNullOrEmpty(folder) ? folder : T("Botões", "Buttons");
            DropOptions options = new DropOptions
            {
                Sounds = sfx,
                // one sound effect is just the file; several are one button which picks one at random
                OneElement = !(sfx && files.Count == 1),
                Kind = PlaylistKind.Random,
                Repeat = !sfx
            };
            DroppedGroup group = new DroppedGroup { Name = name, Files = files.ToList() };
            List<IModeElement> elements = LibraryHelper.BuildModeElements(group, files.ToList(), options);
            foreach (IModeElement element in elements)
                element.Title = name;
            IMode mode = ModeByTitle(wanted) ?? Project.AddMode(wanted);
            foreach (IModeElement element in elements)
                mode.AddElement(element);
            Tree.Sync(Project.GetModes().Select(m => m.Title).ToList());
            SaveProjectNow();
            return wanted;
        }

        private static IFileElement CopyFileAs(IFileElement file, SoundFileType type)
        {
            IFileElement copy = DataModule.ElementFactory.CreateFileElement(file.FilePath, type);
            copy.Effects.Volume = file.Effects.Volume;
            copy.Effects.FadeInTime = file.Effects.FadeInTime;
            copy.Effects.FadeOutTime = file.Effects.FadeOutTime;
            copy.Effects.CrossFading = type == SoundFileType.Music && file.Effects.CrossFading;
            return copy;
        }

        /// <summary>What switching a button between music and sound effect needs: 0 = cannot be switched, 1 = can, 2 = can but the files change how they play (ask first).</summary>
        public int KindSwitch(int id, out bool toSfx, out int fileCount)
        {
            toSfx = false;
            fileCount = 0;
            IModeElement me;
            IMode owner = OwnerOf(id, out me);
            if (owner == null)
                return 0;
            IElement start = me.StartElement;
            int kinds = ElementKinds.Of(start);
            List<IFileElement> files = ShiftableFiles(start);
            if ((kinds != ElementKinds.Music && kinds != ElementKinds.Effect) || files == null || files.Count == 0)
                return 0;
            toSfx = kinds == ElementKinds.Music;
            fileCount = files.Count;
            if (toSfx && files.Count > 1)
                return 2;
            if (!toSfx && files.Count > 1 && start is IElementContainer<IParallelElement>)
                return 2;
            return 1;
        }

        private static List<IFileElement> ShiftableFiles(IElement start)
        {
            IFileElement single = start as IFileElement;
            if (single != null)
                return new List<IFileElement> { single };
            IGeneralElementContainer container = start as IGeneralElementContainer;
            if (container == null)
                return null;
            List<IFileElement> files = new List<IFileElement>();
            foreach (IContainerElement inner in container.GetGeneralElements())
            {
                IFileElement file = inner.InnerElement as IFileElement;
                if (file == null)
                    return null;
                files.Add(file);
            }
            return files;
        }

        public void ToggleButtonKind(int id)
        {
            IModeElement target;
            IMode owner = OwnerOf(id, out target);
            if (owner == null)
                return;
            bool toSfx;
            int count;
            if (KindSwitch(id, out toSfx, out count) == 0)
                return;
            PushUndo(owner.Title);
            string title = target.Title;
            IElement old = target.StartElement;
            SoundFileType type = toSfx ? SoundFileType.SoundEffect : SoundFileType.Music;
            IFileElement oldFile = old as IFileElement;
            if (oldFile != null)
            {
                oldFile.SoundFileType = type;
                if (toSfx)
                    oldFile.Effects.CrossFading = false;
                SaveProjectNow();
                return;
            }
            IGeneralElementContainer oldContainer = (IGeneralElementContainer)old;
            List<IFileElement> copies = new List<IFileElement>();
            foreach (IContainerElement inner in oldContainer.GetGeneralElements())
                copies.Add(CopyFileAs((IFileElement)inner.InnerElement, type));
            IElement made;
            if (copies.Count == 1)
            {
                made = copies[0];
            }
            else if (toSfx)
            {
                IElementContainer<IParallelElement> together = DataModule.ElementFactory.CreateParallelContainer(title);
                foreach (IFileElement copy in copies)
                    together.AddElement(copy);
                made = together;
            }
            else if (old is ISequentialContainer)
            {
                ISequentialBackgroundMusicList list = DataModule.ElementFactory.CreateSequentialBackgroundMusicList(title);
                foreach (IFileElement copy in copies)
                    list.AddElement(copy);
                list.RepeatCount = -1;
                made = list;
            }
            else
            {
                IRandomBackgroundMusicList list = DataModule.ElementFactory.CreateRandomBackgroundMusicList(title);
                foreach (IFileElement copy in copies)
                    list.AddElement(copy);
                list.RepeatCount = -1;
                made = list;
            }
            target.StartElement = made;
            DataModule.ElementRepository.DeleteElement(old.Id);
            SaveProjectNow();
        }

        private string FolderOf(int id)
        {
            IModeElement element;
            IMode mode = OwnerOf(id, out element);
            return mode != null ? mode.Title : null;
        }

        private IMode OwnerOf(int elementId, out IModeElement element)
        {
            element = null;
            if (Project == null)
                return null;
            foreach (IMode mode in Project.GetModes())
            {
                foreach (IModeElement me in mode.GetElements())
                {
                    if (me.Id == elementId)
                    {
                        element = me;
                        return mode;
                    }
                }
            }
            return null;
        }

        public void RenameButton(int id, string newTitle)
        {
            PushUndo(FolderOf(id));
            IModeElement element;
            IMode mode = OwnerOf(id, out element);
            if (mode == null || string.IsNullOrWhiteSpace(newTitle))
                return;
            string old = element.Title;
            ButtonDecor.RenameButton(ProjectPath, mode.Title, old, newTitle);
            element.Title = newTitle;
            SaveProjectNow();
        }

        public void DeleteButton(int id)
        {
            PushUndo(FolderOf(id));
            IModeElement element;
            IMode mode = OwnerOf(id, out element);
            if (mode == null)
                return;
            StopAll();
            ButtonDecor.RemoveButton(ProjectPath, mode.Title, element.Title);
            mode.RemoveElement(element);
            DataModule.ElementRepository.DeleteElement(element.Id);
            SaveProjectNow();
        }

        /// <summary>Moves a button one place inside its folder (delta -1 = earlier, +1 = later).</summary>
        public void MoveButton(int id, int delta)
        {
            PushUndo(FolderOf(id));
            IModeElement element;
            IMode mode = OwnerOf(id, out element);
            if (mode == null)
                return;
            IList<IModeElement> list = mode.GetElements();
            int index = list.IndexOf(element);
            int target = index + delta;
            if (index < 0 || target < 0 || target >= list.Count)
                return;
            mode.RemoveElement(element);
            mode.InsertElement(target, element);
            SaveProjectNow();
        }

        public void SetButtonIcon(int id, string spec)
        {
            PushUndo(FolderOf(id));
            IModeElement element;
            IMode mode = OwnerOf(id, out element);
            if (mode != null)
                ButtonDecor.Set(ProjectPath, mode.Title, element.Title, spec);
        }

        public void SetButtonColor(int id, string hex)
        {
            PushUndo(FolderOf(id));
            IModeElement element;
            IMode mode = OwnerOf(id, out element);
            if (mode != null)
                ButtonDecor.SetColor(ProjectPath, mode.Title, element.Title, hex);
        }

        public void SetFolderIcon(string folder, string spec)
        {
            PushUndo(folder);
            ButtonDecor.Set(ProjectPath, folder, "", spec);
        }

        public void SetFolderColor(string folder, string hex)
        {
            PushUndo(folder);
            ButtonDecor.SetColor(ProjectPath, folder, "", hex);
        }

        /// <summary>A new empty folder (inside "parent", or at the top level); returns its title (made unique if needed).</summary>
        public string NewFolder(string title, string parent)
        {
            PushUndo(parent);
            string unique = title;
            for (int n = 2; ModeByTitle(unique) != null; n++)
                unique = title + " " + n;
            Project.AddMode(unique);
            Tree.Sync(Project.GetModes().Select(m => m.Title).ToList());
            ModeNode node = Tree.Find(unique);
            ModeNode parentNode = parent != null ? Tree.Find(parent) : null;
            if (node != null && parentNode != null)
            {
                Tree.Attach(node, parentNode, -1);
                parentNode.Collapsed = false;
            }
            SaveProjectNow();
            return unique;
        }

        public string RenameFolder(string oldTitle, string newTitle)
        {
            PushUndo(oldTitle);
            IMode mode = ModeByTitle(oldTitle);
            if (mode == null || string.IsNullOrWhiteSpace(newTitle) || newTitle == oldTitle)
                return oldTitle;
            if (ModeByTitle(newTitle) != null)
                return oldTitle;
            ButtonDecor.RenameFolder(ProjectPath, oldTitle, newTitle);
            mode.Title = newTitle;
            ModeNode node = Tree.Find(oldTitle);
            if (node != null)
                node.Title = newTitle;
            SaveProjectNow();
            return newTitle;
        }

        public void DeleteFolder(string title)
        {
            PushUndo(null);
            IMode mode = ModeByTitle(title);
            if (mode == null)
                return;
            StopAll();
            foreach (IModeElement element in mode.GetElements().ToList())
                DataModule.ElementRepository.DeleteElement(element.Id);
            ButtonDecor.RemoveFolder(ProjectPath, title);
            Project.RemoveMode(mode);
            // its subfolders move up one level
            Tree.Sync(Project.GetModes().Select(m => m.Title).ToList());
            SaveProjectNow();
        }

        /// <summary>Writes the project and its folder tree.</summary>
        public void SaveProjectNow()
        {
            if (Project == null)
                return;
            DataModule.ProjectManager.SaveProject(Project);
            Tree.Save(ProjectPath);
        }

        public IMode ModeByTitle(string title)
        {
            return Project == null ? null : Project.GetModes().FirstOrDefault(m => m.Title == title);
        }

        public void Dispose()
        {
            try { StopAll(); } catch (Exception) { }
            SaveSettings();
            if (Control != null)
                Control.Dispose();
            if (m_Audio != null)
                m_Audio.Dispose();
        }
    }
}
