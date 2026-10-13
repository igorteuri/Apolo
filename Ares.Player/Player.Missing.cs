using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Ares.Data;

namespace Ares.Player
{
    // Tool for audio files which were moved, renamed or deleted: lists them and helps to find them again.
    public partial class Player
    {
        private class MissingEntry
        {
            public string FullPath;                       // where the project expects the file
            public string FileName;
            public List<FileRef> Refs = new List<FileRef>();
            public bool Remove;                           // to be taken out of the project
            public HashSet<string> Places = new HashSet<string>();
            public string NewPath;                        // where it was found (null = not yet)
            public ListViewItem Item;
        }

        // where a file element sits: in which container (null = it is the start element of the button itself)
        private class FileRef
        {
            public IFileElement File;
            public IGeneralElementContainer Parent;
            public IMode Mode;
            public IModeElement ModeElement;
            public string Place;
        }

        private class FileCollector : IElementVisitor
        {
            public readonly List<FileRef> Files = new List<FileRef>();
            public IMode Mode;
            public IModeElement ModeElement;
            public string Place = "";
            private IGeneralElementContainer m_Parent;

            private void VisitChildren(IGeneralElementContainer container, System.Collections.IEnumerable children)
            {
                IGeneralElementContainer old = m_Parent;
                m_Parent = container;
                foreach (IElement e in children)
                    e.Visit(this);
                m_Parent = old;
            }

            public void VisitFileElement(IFileElement fileElement)
            {
                Files.Add(new FileRef { File = fileElement, Parent = m_Parent, Mode = Mode, ModeElement = ModeElement, Place = Place });
            }
            public void VisitWebRadioElement(IWebRadioElement webRadioElement) { }
            public void VisitSequentialContainer(ISequentialContainer c) { VisitChildren(c, c.GetElements()); }
            public void VisitParallelContainer(IElementContainer<IParallelElement> c) { VisitChildren(c, c.GetElements()); }
            public void VisitChoiceContainer(IElementContainer<IChoiceElement> c) { VisitChildren(c, c.GetElements()); }
            public void VisitSequentialMusicList(ISequentialBackgroundMusicList l) { VisitChildren(l, l.GetElements()); }
            public void VisitRandomMusicList(IRandomBackgroundMusicList l) { VisitChildren(l, l.GetElements()); }
            public void VisitMacro(IMacro macro) { }
            public void VisitMacroCommand(IMacroCommand macroCommand) { }
            public void VisitReference(IReferenceElement reference) { }
            public void VisitMusicByTags(IMusicByTags musicByTags) { }
        }

        private static string BaseDirectory(IFileElement fe)
        {
            return fe.SoundFileType == SoundFileType.Music
                ? Ares.Settings.Settings.Instance.MusicDirectory : Ares.Settings.Settings.Instance.SoundDirectory;
        }

        private static string FullPathOf(IFileElement fe)
        {
            try
            {
                return Path.Combine(BaseDirectory(fe) ?? "", fe.FilePath ?? "");
            }
            catch (Exception)
            {
                return fe.FilePath ?? "";
            }
        }

        private List<MissingEntry> FindMissingFiles()
        {
            Dictionary<string, MissingEntry> byPath = new Dictionary<string, MissingEntry>(StringComparer.OrdinalIgnoreCase);
            if (m_Project == null)
                return byPath.Values.ToList();
            FileCollector collector = new FileCollector();
            foreach (IMode mode in m_Project.GetModes())
            {
                foreach (IModeElement me in mode.GetElements())
                {
                    collector.Mode = mode;
                    collector.ModeElement = me;
                    collector.Place = mode.Title + " / " + me.Title;
                    me.Visit(collector);
                }
            }
            Dictionary<string, bool> exists = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (FileRef pair in collector.Files)
            {
                string full = FullPathOf(pair.File);
                bool ok;
                if (!exists.TryGetValue(full, out ok))
                {
                    try { ok = File.Exists(full); } catch (Exception) { ok = false; }
                    exists[full] = ok;
                }
                if (ok)
                    continue;
                MissingEntry entry;
                if (!byPath.TryGetValue(full, out entry))
                {
                    entry = new MissingEntry { FullPath = full, FileName = Path.GetFileName(full) };
                    byPath[full] = entry;
                }
                entry.Refs.Add(pair);
                entry.Places.Add(pair.Place);
            }
            return byPath.Values.OrderBy(m => m.FileName, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        private void ShowMissingFiles()
        {
            if (m_Project == null)
            {
                MessageBox.Show(this, T("Abra um projeto primeiro.", "Open a project first."), StringResources.Ares,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Cursor old = Cursor;
            Cursor = Cursors.WaitCursor;
            List<MissingEntry> missing;
            try { missing = FindMissingFiles(); }
            finally { Cursor = old; }
            if (missing.Count == 0)
            {
                MessageBox.Show(this, T("Todos os arquivos de áudio do projeto foram encontrados.", "All audio files of the project were found."),
                    StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (MissingFilesDialog dlg = new MissingFilesDialog(missing))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;
            }
            List<MissingEntry> fixedEntries = missing.Where(m => !m.Remove && m.NewPath != null).ToList();
            List<MissingEntry> removedEntries = missing.Where(m => m.Remove).ToList();
            if (fixedEntries.Count == 0 && removedEntries.Count == 0)
                return;
            string keep = SelectedModeNode != null ? SelectedModeNode.Title : null;
            int relinked = 0, removed = 0, emptied = 0;
            ApplyProjectEdit(() =>
            {
                foreach (MissingEntry entry in fixedEntries)
                {
                    foreach (FileRef r in entry.Refs)
                    {
                        r.File.FilePath = ToStoredPath(r.File, entry.NewPath);
                        relinked++;
                    }
                }
                HashSet<IModeElement> touched = new HashSet<IModeElement>();
                foreach (MissingEntry entry in removedEntries)
                {
                    foreach (FileRef r in entry.Refs)
                    {
                        if (r.Parent != null)
                        {
                            r.Parent.RemoveElement(r.File.Id);
                            DataModule.ElementRepository.DeleteElement(r.File.Id);
                            touched.Add(r.ModeElement);
                        }
                        else if (r.ModeElement != null)
                        {
                            // a button which is only this file
                            r.Mode.RemoveElement(r.ModeElement);
                            DataModule.ElementRepository.DeleteElement(r.ModeElement.Id);
                        }
                        removed++;
                    }
                }
                // playlists which lost all their files are of no use
                foreach (IModeElement me in touched)
                {
                    IGeneralElementContainer container = me.StartElement as IGeneralElementContainer;
                    if (container != null && container.GetGeneralElements().Count == 0)
                    {
                        foreach (IMode mode in m_Project.GetModes())
                        {
                            if (mode.GetElements().Contains(me))
                            {
                                mode.RemoveElement(me);
                                DataModule.ElementRepository.DeleteElement(me.Id);
                                emptied++;
                                break;
                            }
                        }
                    }
                }
            }, keep);
            string note = "";
            if (relinked > 0)
                note += "  -  " + relinked + T(" arquivo(s) religado(s)", " file(s) relinked");
            if (removed > 0)
                note += "  -  " + removed + T(" arquivo(s) removido(s) do projeto", " file(s) removed from the project");
            if (emptied > 0)
                note += " (" + emptied + T(" playlist(s) vazia(s) removida(s))", " empty playlist(s) removed)");
            this.Text = this.Text + note;
        }

        // inside the music / sound folder the path is stored relative to it (like the Editor does), otherwise as it is
        private static string ToStoredPath(IFileElement fe, string newPath)
        {
            try
            {
                string baseDir = BaseDirectory(fe);
                if (!string.IsNullOrEmpty(baseDir))
                {
                    string b = Path.GetFullPath(baseDir).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
                    string f = Path.GetFullPath(newPath);
                    if (f.StartsWith(b, StringComparison.OrdinalIgnoreCase))
                        return f.Substring(b.Length);
                }
            }
            catch (Exception)
            {
            }
            return newPath;
        }

        private class MissingFilesDialog : Form
        {
            private readonly List<MissingEntry> m_Entries;
            private readonly ListView m_List;
            private readonly Label m_Summary;
            private readonly Button m_Search, m_Locate, m_Apply, m_RemoveButton;

            public MissingFilesDialog(List<MissingEntry> entries)
            {
                m_Entries = entries;
                Text = T("Arquivos de áudio ausentes", "Missing audio files");
                AutoScaleMode = AutoScaleMode.Font;
                ClientSize = new Size(820, 520);
                MinimumSize = new Size(640, 400);
                StartPosition = FormStartPosition.CenterParent;
                ShowInTaskbar = false;
                MinimizeBox = false;

                Label help = new Label();
                help.Text = T("Estes arquivos não estão mais onde o projeto espera. Se você os moveu para outra pasta, escolha a pasta nova e o Apolo procura pelo nome do arquivo. Depois clique em \"Corrigir\".",
                    "These files are no longer where the project expects them. If you moved them to another folder, choose the new folder and Apolo looks for them by file name. Then click \"Fix\".");
                help.SetBounds(12, 10, 796, 40);
                help.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                Controls.Add(help);

                m_List = new ListView();
                m_List.View = View.Details;
                m_List.FullRowSelect = true;
                m_List.HideSelection = false;
                m_List.MultiSelect = true;
                m_List.Columns.Add(T("Arquivo", "File"), 200);
                m_List.Columns.Add(T("Estava em", "Was in"), 280);
                m_List.Columns.Add(T("Usado em", "Used in"), 90);
                m_List.Columns.Add(T("Novo local", "New location"), 230);
                m_List.SetBounds(12, 54, 796, 380);
                m_List.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
                foreach (MissingEntry e in m_Entries)
                {
                    ListViewItem item = new ListViewItem(e.FileName);
                    item.SubItems.Add(Path.GetDirectoryName(e.FullPath) ?? e.FullPath);
                    item.SubItems.Add(e.Places.Count + (e.Places.Count == 1 ? T(" playlist", " playlist") : T(" playlists", " playlists")));
                    item.SubItems.Add("");
                    item.ToolTipText = string.Join("\n", e.Places.Take(12));
                    item.Tag = e;
                    e.Item = item;
                    m_List.Items.Add(item);
                }
                m_List.ShowItemToolTips = true;
                m_List.DoubleClick += (s, e) => LocateSelected();
                m_List.SelectedIndexChanged += (s, e) => UpdateButtons();
                Controls.Add(m_List);

                m_Summary = new Label();
                m_Summary.SetBounds(12, 440, 796, 22);
                m_Summary.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
                Controls.Add(m_Summary);

                m_Search = new Button { Text = T("Procurar numa pasta...", "Search in a folder...") };
                m_Locate = new Button { Text = T("Localizar este arquivo...", "Locate this file...") };
                m_RemoveButton = new Button { Text = T("Remover do projeto", "Remove from project") };
                m_Apply = new Button { Text = T("Corrigir", "Fix"), DialogResult = DialogResult.OK };
                Button cancel = new Button { Text = T("Fechar", "Close"), DialogResult = DialogResult.Cancel };
                m_Search.SetBounds(12, 474, 190, 32);
                m_Locate.SetBounds(210, 474, 190, 32);
                m_RemoveButton.SetBounds(408, 474, 170, 32);
                m_RemoveButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
                m_RemoveButton.Click += (s, e) => RemoveSelected();
                m_Apply.SetBounds(690, 474, 56, 32);
                cancel.SetBounds(750, 474, 58, 32);
                m_Search.Anchor = m_Locate.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
                m_Apply.Anchor = cancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
                m_Search.Click += (s, e) => SearchInFolder();
                m_Locate.Click += (s, e) => LocateSelected();
                Controls.AddRange(new Control[] { m_Search, m_Locate, m_RemoveButton, m_Apply, cancel });
                CancelButton = cancel;
                UpdateButtons();
                UpdateSummary();
            }

            private MissingEntry Selected()
            {
                return m_List.SelectedItems.Count == 0 ? null : (MissingEntry)m_List.SelectedItems[0].Tag;
            }

            private void UpdateButtons()
            {
                m_Locate.Enabled = m_List.SelectedItems.Count == 1;
                m_RemoveButton.Enabled = m_List.SelectedItems.Count > 0;
                m_Apply.Enabled = m_Entries.Any(m => m.Remove || m.NewPath != null);
            }

            private void UpdateSummary()
            {
                int found = m_Entries.Count(m => m.NewPath != null && !m.Remove);
                int removed = m_Entries.Count(m => m.Remove);
                m_Summary.Text = T(m_Entries.Count + " arquivo(s) ausente(s); " + found + " encontrado(s) em outro lugar; " + removed + " para remover do projeto.",
                    m_Entries.Count + " missing file(s); " + found + " found elsewhere; " + removed + " to remove from the project.");
                UpdateButtons();
            }

            // marks the selected files to be taken out of the project (they stop giving errors)
            private void RemoveSelected()
            {
                List<MissingEntry> chosen = m_List.SelectedItems.Cast<ListViewItem>().Select(i => (MissingEntry)i.Tag).ToList();
                if (chosen.Count == 0)
                    return;
                int uses = chosen.Sum(c => c.Refs.Count);
                if (MessageBox.Show(this,
                        T("Remover " + chosen.Count + " arquivo(s) do projeto? Eles saem de " + uses + " lugar(es) nas playlists e deixam de dar erro.\n(Nada é apagado do computador. Vale ao clicar em \"Corrigir\".)",
                          "Remove " + chosen.Count + " file(s) from the project? They are taken out of " + uses + " place(s) in the playlists and stop giving errors.\n(Nothing is deleted from the computer. Takes effect when you click \"Fix\".)"),
                        StringResources.Ares, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;
                foreach (MissingEntry entry in chosen)
                {
                    entry.Remove = true;
                    entry.NewPath = null;
                    entry.Item.SubItems[3].Text = T("(será removido do projeto)", "(will be removed from the project)");
                    entry.Item.ForeColor = Color.IndianRed;
                }
                UpdateSummary();
            }

            private void SetNewPath(MissingEntry entry, string path)
            {
                entry.Remove = false;
                entry.NewPath = path;
                entry.Item.SubItems[3].Text = path ?? "";
                entry.Item.ForeColor = path != null ? Color.ForestGreen : m_List.ForeColor;
            }

            private void SearchInFolder()
            {
                string root;
                using (FolderBrowserDialog dlg = new FolderBrowserDialog())
                {
                    dlg.Description = T("Escolha a pasta onde os arquivos podem estar (as subpastas também são verificadas).",
                        "Choose the folder where the files may be (subfolders are searched too).");
                    if (dlg.ShowDialog(this) != DialogResult.OK)
                        return;
                    root = dlg.SelectedPath;
                }
                Dictionary<string, List<MissingEntry>> wanted = new Dictionary<string, List<MissingEntry>>(StringComparer.OrdinalIgnoreCase);
                foreach (MissingEntry entry in m_Entries.Where(m => m.NewPath == null && !m.Remove))
                {
                    List<MissingEntry> list;
                    if (!wanted.TryGetValue(entry.FileName, out list))
                        wanted[entry.FileName] = list = new List<MissingEntry>();
                    list.Add(entry);
                }
                Cursor old = Cursor;
                Cursor = Cursors.WaitCursor;
                int count = 0;
                try
                {
                    Stack<string> pending = new Stack<string>();
                    pending.Push(root);
                    while (pending.Count > 0 && wanted.Count > 0)
                    {
                        string dir = pending.Pop();
                        try
                        {
                            foreach (string file in Directory.GetFiles(dir))
                            {
                                List<MissingEntry> list;
                                if (wanted.TryGetValue(Path.GetFileName(file), out list))
                                {
                                    foreach (MissingEntry entry in list)
                                    {
                                        SetNewPath(entry, file);
                                        count++;
                                    }
                                    wanted.Remove(Path.GetFileName(file));
                                }
                            }
                            foreach (string sub in Directory.GetDirectories(dir))
                                pending.Push(sub);
                        }
                        catch (Exception)
                        {
                            // folder without access: skip
                        }
                    }
                }
                finally
                {
                    Cursor = old;
                }
                UpdateSummary();
                if (count == 0)
                {
                    MessageBox.Show(this, T("Nenhum dos arquivos ausentes foi encontrado nessa pasta.", "None of the missing files was found in that folder."),
                        StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }

            private void LocateSelected()
            {
                MissingEntry entry = Selected();
                if (entry == null)
                    return;
                using (OpenFileDialog dlg = new OpenFileDialog())
                {
                    dlg.Title = T("Onde está o arquivo \"" + entry.FileName + "\"?", "Where is the file \"" + entry.FileName + "\"?");
                    dlg.FileName = entry.FileName;
                    if (dlg.ShowDialog(this) != DialogResult.OK)
                        return;
                    SetNewPath(entry, dlg.FileName);
                    // the same change of folder probably applies to the other files of that folder
                    string oldDir = Path.GetDirectoryName(entry.FullPath) ?? "";
                    string newDir = Path.GetDirectoryName(dlg.FileName) ?? "";
                    foreach (MissingEntry other in m_Entries)
                    {
                        if (other == entry || other.NewPath != null || other.Remove)
                            continue;
                        if (string.Equals(Path.GetDirectoryName(other.FullPath) ?? "", oldDir, StringComparison.OrdinalIgnoreCase))
                        {
                            string candidate = Path.Combine(newDir, other.FileName);
                            if (File.Exists(candidate))
                                SetNewPath(other, candidate);
                        }
                    }
                }
                UpdateSummary();
            }
        }
    }
}
