using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Ares.Data;

namespace Ares.Player
{
    // Drag & drop of folders / audio files straight into the Player.
    // Each dropped folder becomes a music list ("playlist") in the mode "Biblioteca".
    public partial class Player
    {
        private const string LibraryModeTitle = "Biblioteca";

        private static bool IsPt
        {
            get { return System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt"
                || System.Globalization.CultureInfo.CurrentCulture.TwoLetterISOLanguageName == "pt"; }
        }

        private static string LibraryProjectPath
        {
            get { return Ares.CommonGUI.LibraryHelper.LibraryProjectPath; }
        }

        private void InitLibraryDrop()
        {
            HookDrop(this);
            try
            {
                var lib = new ToolStripMenuItem(IsPt ? "Biblioteca" : "Library");
                var open = new ToolStripMenuItem(IsPt ? "Abrir Biblioteca" : "Open library");
                open.Click += (s, e) =>
                {
                    string path = LibraryProjectPath;
                    EnsureProjectFile(path);
                    OpenProject(path, false);
                };
                var show = new ToolStripMenuItem(IsPt ? "Abrir pasta da Biblioteca no Explorer" : "Show library folder in Explorer");
                show.Click += (s, e) =>
                {
                    System.Diagnostics.Process.Start("explorer.exe", "\"" + Ares.CommonGUI.LibraryHelper.LibraryFolder + "\"");
                };
                var choose = new ToolStripMenuItem(IsPt ? "Escolher pasta da Biblioteca..." : "Choose library folder...");
                choose.Click += (s, e) => ChooseLibraryFolder();
                lib.DropDownItems.AddRange(new ToolStripItem[] { open, show, choose });
                extrasToolStripMenuItem.DropDownItems.Insert(0, lib);
                extrasToolStripMenuItem.DropDownItems.Insert(1, new ToolStripSeparator());
            }
            catch (Exception)
            {
            }
        }

        private void ChooseLibraryFolder()
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = IsPt ? "Pasta onde a Biblioteca (projeto com suas pastas e músicas) será guardada"
                    : "Folder where the library project will be stored";
                dlg.SelectedPath = Ares.CommonGUI.LibraryHelper.LibraryFolder;
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;
                string oldPath = LibraryProjectPath;
                bool wasOpen = m_Project != null && string.Equals(m_Project.FileName, oldPath, StringComparison.OrdinalIgnoreCase);
                Ares.CommonGUI.LibraryHelper.LibraryFolder = dlg.SelectedPath;
                string newPath = LibraryProjectPath;
                try
                {
                    if (File.Exists(oldPath) && !File.Exists(newPath))
                        File.Copy(oldPath, newPath);
                }
                catch (Exception)
                {
                }
                if (wasOpen && File.Exists(newPath))
                    OpenProject(newPath, false);
            }
        }

        private void HookDrop(Control control)
        {
            try
            {
                control.AllowDrop = true;
                control.DragEnter += LibraryDragEnter;
                control.DragOver += LibraryDragEnter;
                control.DragDrop += LibraryDragDrop;
                control.ControlAdded += (s, e) => HookDrop(e.Control);
                foreach (Control child in control.Controls)
                {
                    HookDrop(child);
                }
            }
            catch (Exception)
            {
            }
        }

        private void LibraryDragEnter(object sender, DragEventArgs e)
        {
            // only file drops are handled here; other (internal) drags are left to their own handlers
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        private void LibraryDragDrop(object sender, DragEventArgs e)
        {
            if (e.Data == null || !e.Data.GetDataPresent(DataFormats.FileDrop))
                return;
            string[] paths = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (paths == null || paths.Length == 0)
                return;
            // do not block the explorer window that started the drag
            BeginInvoke(new MethodInvoker(() => ImportDroppedPaths(paths)));
        }

        private void ImportDroppedPaths(string[] paths)
        {
            try
            {
                List<Ares.CommonGUI.DroppedGroup> groups = Ares.CommonGUI.LibraryHelper.Collect(paths);
                if (groups.Count == 0)
                {
                    MessageBox.Show(this, IsPt ? "Nenhum arquivo de áudio encontrado no que foi arrastado."
                        : "No audio files found in what was dropped.", StringResources.Ares,
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                int fileCount = groups.Sum(g => g.Files.Count);
                Ares.CommonGUI.DropOptions options = Ares.CommonGUI.DropOptionsDialog.Ask(this, groups.Count, fileCount);
                if (options == null)
                    return;

                string libPath = LibraryProjectPath;
                string target = libPath;
                if (m_Project != null && !string.Equals(m_Project.FileName, libPath, StringComparison.OrdinalIgnoreCase))
                {
                    DialogResult r = MessageBox.Show(this,
                        IsPt ? "Adicionar ao projeto aberto \"" + m_Project.Title + "\"?\r\n\r\nSim = projeto aberto\r\nNão = Biblioteca (padrão)"
                             : "Add to the open project \"" + m_Project.Title + "\"?\r\n\r\nYes = open project\r\nNo = Library (default)",
                        StringResources.Ares, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                    if (r == DialogResult.Cancel)
                        return;
                    if (r == DialogResult.Yes)
                        target = m_Project.FileName;
                }

                // loose files go into the mode that is currently selected (if it is a real mode)
                string looseModeTitle = null;
                if (m_Project != null && string.Equals(m_Project.FileName, target, StringComparison.OrdinalIgnoreCase)
                    && SelectedModeIndex >= 0 && SelectedModeIndex < m_Project.GetModes().Count)
                {
                    looseModeTitle = m_Project.GetModes()[SelectedModeIndex].Title;
                }
                if (looseModeTitle == null)
                    looseModeTitle = IsPt ? "Músicas soltas" : "Loose music";

                bool oldWatch = fileSystemWatcher1.EnableRaisingEvents;
                fileSystemWatcher1.EnableRaisingEvents = false;
                try
                {
                    if (m_Project == null || !string.Equals(m_Project.FileName, target, StringComparison.OrdinalIgnoreCase))
                    {
                        EnsureProjectFile(target);
                        OpenProject(target, false);
                    }
                    if (m_Project == null)
                        return;
                    StopAllPlaying();

                    int added = 0;
                    string lastMode = null;
                    foreach (Ares.CommonGUI.DroppedGroup g in groups)
                    {
                        string modeTitle = g.IsLoose ? looseModeTitle : g.Name;
                        IMode mode = m_Project.GetModes().FirstOrDefault(m => m.Title == modeTitle);
                        if (mode == null)
                            mode = m_Project.AddMode(modeTitle);
                        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        foreach (IModeElement old in mode.GetElements())
                        {
                            IFileElement oldFile = old.StartElement as IFileElement;
                            if (oldFile != null)
                                existing.Add(oldFile.FilePath);
                        }
                        List<string> newFiles = g.Files.Where(f => !existing.Contains(f)).ToList();
                        foreach (IModeElement me in Ares.CommonGUI.LibraryHelper.BuildModeElements(g, newFiles, options))
                        {
                            mode.AddElement(me);
                            added++;
                        }
                        lastMode = modeTitle;
                    }
                    m_Project.Changed = true;
                    DataModule.ProjectManager.SaveProject(m_Project);
                    OpenProject(m_Project.FileName, false);
                    SelectModeByTitle(lastMode);
                    ShowLibraryMessage(groups.Count, added);
                }
                finally
                {
                    fileSystemWatcher1.EnableRaisingEvents = oldWatch;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, (IsPt ? "Erro ao importar: " : "Import error: ") + ex.Message,
                    StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void EnsureProjectFile(string path)
        {
            if (File.Exists(path))
                return;
            IProject created = DataModule.ProjectManager.CreateProject("Biblioteca");
            created.FileName = path;
            DataModule.ProjectManager.SaveProject(created);
            DataModule.ProjectManager.UnloadProject(created);
        }

        // ---- a folder of the computer becomes a folder of Apolo, with its subfolders and a playlist per audio folder ----

        private class DiskFolder
        {
            public string Name;
            public List<string> Files = new List<string>();
            public List<DiskFolder> Subfolders = new List<DiskFolder>();
            public bool IsLeaf { get { return Subfolders.Count == 0; } }
        }

        // reads the folder; folders without any audio (also below them) are left out
        private static DiskFolder ReadDiskFolder(string dir, ref int folders, ref int files)
        {
            DiskFolder result = new DiskFolder { Name = new DirectoryInfo(dir).Name };
            try
            {
                result.Files.AddRange(Directory.GetFiles(dir).Where(Ares.CommonGUI.LibraryHelper.IsAudioFile));
                result.Files.Sort(StringComparer.CurrentCultureIgnoreCase);
                string[] subs = Directory.GetDirectories(dir);
                Array.Sort(subs, StringComparer.CurrentCultureIgnoreCase);
                foreach (string sub in subs)
                {
                    DiskFolder child = ReadDiskFolder(sub, ref folders, ref files);
                    if (child != null)
                        result.Subfolders.Add(child);
                }
            }
            catch (Exception)
            {
            }
            if (result.Files.Count == 0 && result.Subfolders.Count == 0)
                return null;
            folders++;
            files += result.Files.Count;
            return result;
        }

        private string UniqueModeTitle(string wanted, HashSet<string> used)
        {
            string title = wanted;
            int n = 2;
            while (used.Contains(title))
                title = wanted + " (" + n++ + ")";
            used.Add(title);
            return title;
        }

        // creates the Apolo folder (mode) for a disk folder; "leaf" folders inside it become playlists, not folders
        private void BuildFolderFromDisk(DiskFolder disk, string title, string parentTitle, Ares.CommonGUI.DropOptions options,
            HashSet<string> used, List<KeyValuePair<string, string>> placement, ref int elements)
        {
            IMode mode = m_Project.AddMode(title);
            placement.Add(new KeyValuePair<string, string>(title, parentTitle));
            if (disk.Files.Count > 0)
            {
                Ares.CommonGUI.DroppedGroup group = new Ares.CommonGUI.DroppedGroup { Name = disk.Name, Files = disk.Files };
                foreach (IModeElement me in Ares.CommonGUI.LibraryHelper.BuildModeElements(group, disk.Files, options))
                {
                    mode.AddElement(me);
                    elements++;
                }
            }
            foreach (DiskFolder child in disk.Subfolders)
            {
                if (child.IsLeaf)
                {
                    Ares.CommonGUI.DroppedGroup group = new Ares.CommonGUI.DroppedGroup { Name = child.Name, Files = child.Files };
                    foreach (IModeElement me in Ares.CommonGUI.LibraryHelper.BuildModeElements(group, child.Files, options))
                    {
                        mode.AddElement(me);
                        elements++;
                    }
                }
                else
                {
                    BuildFolderFromDisk(child, UniqueModeTitle(child.Name, used), title, options, used, placement, ref elements);
                }
            }
        }

        /// <summary>Asks for a folder of the computer and creates the matching folders and playlists.</summary>
        private void ImportFolderAsFolders(bool asChild)
        {
            string path;
            using (FolderBrowserDialog dlg = new FolderBrowserDialog())
            {
                dlg.Description = IsPt ? "Escolha a pasta com os áudios. Cada subpasta vira uma playlist (ou uma pasta, se tiver outras subpastas)."
                    : "Choose the folder with the audio. Each subfolder becomes a playlist (or a folder if it has subfolders of its own).";
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;
                path = dlg.SelectedPath;
            }
            int folders = 0, files = 0;
            Cursor old = Cursor;
            Cursor = Cursors.WaitCursor;
            DiskFolder disk;
            try { disk = ReadDiskFolder(path, ref folders, ref files); }
            finally { Cursor = old; }
            if (disk == null)
            {
                MessageBox.Show(this, IsPt ? "Nenhum arquivo de áudio encontrado nessa pasta." : "No audio files found in that folder.",
                    StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Ares.CommonGUI.DropOptions options = Ares.CommonGUI.DropOptionsDialog.Ask(this, folders, files);
            if (options == null)
                return;
            if (m_Project == null)
            {
                string libPath = LibraryProjectPath;
                EnsureProjectFile(libPath);
                OpenProject(libPath, false);
            }
            if (m_Project == null)
                return;
            ModeNode selected = SelectedModeNode;
            string anchorTitle = selected != null ? selected.Title : null;
            string firstTitle = null;
            int created = 0;
            ApplyProjectEdit(() =>
            {
                HashSet<string> used = new HashSet<string>(m_Project.GetModes().Select(m => m.Title), StringComparer.Ordinal);
                List<KeyValuePair<string, string>> placement = new List<KeyValuePair<string, string>>();
                firstTitle = UniqueModeTitle(disk.Name, used);
                int elements = 0;
                BuildFolderFromDisk(disk, firstTitle, null, options, used, placement, ref elements);
                created = elements;
                EnsureModeTree();
                ModeNode anchor = anchorTitle != null ? m_ModeTree.Find(anchorTitle) : null;
                foreach (KeyValuePair<string, string> p in placement)
                {
                    ModeNode node = m_ModeTree.Find(p.Key);
                    if (node == null)
                        continue;
                    if (p.Value != null)
                    {
                        ModeNode parent = m_ModeTree.Find(p.Value);
                        if (parent != null)
                            m_ModeTree.Attach(node, parent, -1);
                    }
                    else if (asChild && anchor != null)
                    {
                        m_ModeTree.Attach(node, anchor, -1);
                        anchor.Collapsed = false;
                    }
                    else if (anchor != null && anchor.Parent != null)
                    {
                        m_ModeTree.Attach(node, anchor.Parent, anchor.Parent.Children.IndexOf(anchor) + 1);
                    }
                }
                SaveModeTree();
            }, null);
            SelectModeByTitle(firstTitle);
            this.Text = this.Text + "  -  " + (IsPt ? folders + " pasta(s), " + created + " item(ns) criados" : folders + " folder(s), " + created + " item(s) created");
        }

        private void ShowLibraryMessage(int lists, int files)
        {
            this.Text = this.Text + "  -  " + (IsPt ? lists + " pasta(s), " + files + " música(s) adicionadas"
                : lists + " folder(s), " + files + " track(s) added");
        }
    }
}
