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
        private static readonly string[] s_AudioExtensions =
            { ".mp3", ".wav", ".ogg", ".opus", ".flac", ".m4a", ".aac", ".wma", ".aif", ".aiff", ".mp2", ".mp1" };

        private const string LibraryModeTitle = "Biblioteca";

        private static bool IsPt
        {
            get { return System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt"
                || System.Globalization.CultureInfo.CurrentCulture.TwoLetterISOLanguageName == "pt"; }
        }

        private static string LibraryProjectPath
        {
            get
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Ares");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "Biblioteca.ares");
            }
        }

        private void InitLibraryDrop()
        {
            HookDrop(this);
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
            e.Effect = e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
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

        private static List<string> AudioFilesIn(string folder)
        {
            var result = new List<string>();
            try
            {
                result.AddRange(Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
                    .Where(f => s_AudioExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())));
            }
            catch (Exception)
            {
            }
            result.Sort(StringComparer.CurrentCultureIgnoreCase);
            return result;
        }

        private void ImportDroppedPaths(string[] paths)
        {
            try
            {
                // collect: one list per folder, one list for loose files
                var lists = new List<KeyValuePair<string, List<string>>>();
                var loose = new List<string>();
                foreach (string p in paths)
                {
                    if (Directory.Exists(p))
                    {
                        List<string> files = AudioFilesIn(p);
                        if (files.Count > 0)
                        {
                            string name = new DirectoryInfo(p).Name;
                            lists.Add(new KeyValuePair<string, List<string>>(name, files));
                        }
                    }
                    else if (File.Exists(p) && s_AudioExtensions.Contains(Path.GetExtension(p).ToLowerInvariant()))
                    {
                        loose.Add(p);
                    }
                }
                if (loose.Count > 0)
                {
                    string name = loose.Count == 1 ? Path.GetFileNameWithoutExtension(loose[0])
                        : (IsPt ? "Músicas soltas" : "Loose music");
                    lists.Add(new KeyValuePair<string, List<string>>(name, loose));
                }
                if (lists.Count == 0)
                {
                    MessageBox.Show(this, IsPt ? "Nenhum arquivo de áudio encontrado no que foi arrastado."
                        : "No audio files found in what was dropped.", StringResources.Ares,
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // choose target project
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

                bool oldWatch = fileSystemWatcher1.EnableRaisingEvents;
                fileSystemWatcher1.EnableRaisingEvents = false;
                try
                {
                    if (m_Project == null || !string.Equals(m_Project.FileName, target, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!File.Exists(target))
                        {
                            IProject created = DataModule.ProjectManager.CreateProject("Biblioteca");
                            created.FileName = target;
                            DataModule.ProjectManager.SaveProject(created);
                            DataModule.ProjectManager.UnloadProject(created);
                        }
                        OpenProject(target, false);
                    }
                    if (m_Project == null)
                        return;
                    StopAllPlaying();

                    IMode mode = m_Project.GetModes().FirstOrDefault(m => m.Title == LibraryModeTitle);
                    if (mode == null)
                        mode = m_Project.AddMode(LibraryModeTitle);

                    int totalFiles = 0;
                    foreach (var entry in lists)
                    {
                        IRandomBackgroundMusicList list = DataModule.ElementFactory.CreateRandomBackgroundMusicList(entry.Key);
                        foreach (string file in entry.Value)
                        {
                            IFileElement fe = DataModule.ElementFactory.CreateFileElement(file, SoundFileType.Music);
                            list.AddElement(fe);
                            totalFiles++;
                        }
                        IModeElement me = DataModule.ElementFactory.CreateModeElement(entry.Key, list);
                        mode.AddElement(me);
                    }
                    m_Project.Changed = true;
                    DataModule.ProjectManager.SaveProject(m_Project);
                    OpenProject(m_Project.FileName, false);
                    SelectLibraryMode();
                    ShowLibraryMessage(lists.Count, totalFiles);
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

        private void SelectLibraryMode()
        {
            try
            {
                for (int i = 0; i < modesList.Items.Count; i++)
                {
                    string t = modesList.Items[i] as string;
                    if (t != null && t.StartsWith(LibraryModeTitle))
                    {
                        modesList.SelectedIndex = i;
                        return;
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        private void ShowLibraryMessage(int lists, int files)
        {
            this.Text = this.Text + "  -  " + (IsPt ? lists + " lista(s), " + files + " música(s) adicionadas"
                : lists + " list(s), " + files + " track(s) added");
        }
    }
}
