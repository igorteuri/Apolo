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
                    && modesList.SelectedIndex >= 2 && modesList.SelectedIndex - 2 < m_Project.GetModes().Count)
                {
                    looseModeTitle = m_Project.GetModes()[modesList.SelectedIndex - 2].Title;
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

        private void SelectModeByTitle(string title)
        {
            if (title == null)
                return;
            try
            {
                int found = -1;
                for (int i = 2; i < modesList.Items.Count && found < 0; i++)
                {
                    if (string.Equals(modesList.Items[i] as string, title, StringComparison.Ordinal))
                        found = i;
                }
                for (int i = 2; i < modesList.Items.Count && found < 0; i++)
                {
                    string t = modesList.Items[i] as string;
                    if (t != null && t.StartsWith(title))
                        found = i;
                }
                if (found >= 0)
                    modesList.SelectedIndex = found;
            }
            catch (Exception)
            {
            }
        }

        private void ShowLibraryMessage(int lists, int files)
        {
            this.Text = this.Text + "  -  " + (IsPt ? lists + " pasta(s)/modo(s), " + files + " música(s) adicionadas"
                : lists + " mode(s), " + files + " track(s) added");
        }
    }
}
