using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Ares.Data;

namespace Ares.Player
{
    // Drag & drop of folders / audio files straight into the Player.
    // What was dropped is analysed (DropScan) and the dialog asks only what is open; DropPlan decides the folders and buttons.
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
            // the folder under the mouse (or the open one) receives the playlists; empty area of the list = new folders
            string into = DropTargetFolder(new Point(e.X, e.Y));
            // do not block the explorer window that started the drag
            BeginInvoke(new MethodInvoker(() => ImportDroppedPaths(paths, into)));
        }

        // title of the Apolo folder which receives what is dropped at the screen point; null = create new folders
        private string DropTargetFolder(Point screen)
        {
            if (m_Project == null)
                return null;
            Point pt = modesList.PointToClient(screen);
            if (modesList.ClientRectangle.Contains(pt))
            {
                int row = modesList.IndexFromPoint(pt);
                return row >= 0 && row < m_ModeRows.Count ? m_ModeRows[row].Title : null;
            }
            return OpenFolderTitle();
        }

        // title of the folder which is open now (null when none)
        private string OpenFolderTitle()
        {
            IMode open = m_Project != null ? CurrentMode() : null;
            return open != null ? open.Title : null;
        }

        /// <summary>
        /// Puts dropped / chosen audio in the project. The dropped things are looked at first: the dialog then asks only
        /// what is open (where it goes, one button per file or one playlist per folder).
        /// "intoFolder" = the folder which receives the drop (null = none: new folders are created).
        /// "anchorTitle" = new top level folders are put right after this folder.
        /// </summary>
        private void ImportDroppedPaths(string[] paths, string intoFolder = null, string anchorTitle = null)
        {
            try
            {
                Ares.CommonGUI.DropScan scan = Ares.CommonGUI.DropScan.Read(paths);
                if (scan.IsEmpty)
                {
                    MessageBox.Show(this, IsPt ? "Nenhum arquivo de áudio encontrado no que foi arrastado."
                        : "No audio files found in what was dropped.", StringResources.Ares,
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                string destination = intoFolder != null && ModeIndexByTitle(intoFolder) >= 0 ? intoFolder : null;
                Ares.CommonGUI.DropOptions options = Ares.CommonGUI.DropOptionsDialog.Ask(this, scan, destination);
                if (options == null)
                    return;

                // the Library is only used when no project is open
                if (m_Project == null)
                {
                    string libPath = LibraryProjectPath;
                    EnsureProjectFile(libPath);
                    OpenProject(libPath, false);
                }
                if (m_Project == null)
                    return;

                Ares.CommonGUI.DropPlan plan = Ares.CommonGUI.DropPlan.Build(scan, options.Layout, destination,
                    m_Project.GetModes().Select(m => m.Title).ToList(), IsPt ? "Músicas soltas" : "Loose music");
                Ares.CommonGUI.PlannedFolder firstNew = plan.Folders.FirstOrDefault(f => !f.Existing);
                string showTitle = firstNew != null ? firstNew.Title : destination;

                int added = 0;
                ApplyProjectEdit(() =>
                {
                    foreach (Ares.CommonGUI.PlannedFolder planned in plan.Folders)
                    {
                        IMode mode = m_Project.GetModes().FirstOrDefault(m => m.Title == planned.Title);
                        if (mode == null)
                        {
                            if (planned.Existing)
                                continue;
                            mode = m_Project.AddMode(planned.Title);
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
                        foreach (Ares.CommonGUI.DroppedGroup group in planned.Groups)
                        {
                            List<string> files = group.Files.Where(f => !present.Contains(f)).ToList();
                            foreach (IModeElement me in Ares.CommonGUI.LibraryHelper.BuildModeElements(group, files, options))
                            {
                                mode.AddElement(me);
                                added++;
                            }
                            foreach (string file in files)
                                present.Add(file);
                        }
                    }

                    // nest the new folders: inside their parent, or (top level) right after the anchor
                    EnsureModeTree();
                    ModeNode anchor = anchorTitle != null ? m_ModeTree.Find(anchorTitle) : null;
                    foreach (Ares.CommonGUI.PlannedFolder planned in plan.Folders)
                    {
                        if (planned.Existing)
                            continue;
                        ModeNode node = m_ModeTree.Find(planned.Title);
                        if (node == null)
                            continue;
                        ModeNode parent = planned.Parent != null ? m_ModeTree.Find(planned.Parent) : null;
                        if (parent != null && parent != node)
                        {
                            m_ModeTree.Attach(node, parent, -1);
                            parent.Collapsed = false;
                        }
                        else if (planned.Parent == null && anchor != null && anchor.Parent != null)
                        {
                            m_ModeTree.Attach(node, anchor.Parent, anchor.Parent.Children.IndexOf(anchor) + 1);
                            anchor = node;
                        }
                    }
                    SaveModeTree();
                }, showTitle);
                ShowLibraryMessage(plan.Folders.Count(f => !f.Existing), added);
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

        /// <summary>Asks for a folder of the computer and adds it like a dropped folder (the dialog asks how).</summary>
        private void ImportFolderAsFolders(bool asChild)
        {
            string path;
            using (FolderBrowserDialog dlg = new FolderBrowserDialog())
            {
                dlg.Description = IsPt ? "Escolha a pasta com os áudios."
                    : "Choose the folder with the audio.";
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;
                path = dlg.SelectedPath;
            }
            ModeNode selected = m_Project != null ? SelectedModeNode : null;
            string title = selected != null ? selected.Title : null;
            ImportDroppedPaths(new[] { path }, asChild ? title : null, asChild ? null : title);
        }

        private void ShowLibraryMessage(int lists, int files)
        {
            this.Text = this.Text + "  -  " + (IsPt ? lists + " pasta(s) nova(s), " + files + " botão(ões) criado(s)"
                : lists + " new folder(s), " + files + " button(s) created");
        }
    }
}
