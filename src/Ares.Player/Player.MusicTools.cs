/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 Music tools in the menu "Extras":
   - Music renamer: cleans file names, looks up title / artist / album on the internet (AcoustID + MusicBrainz),
     writes the tags and renames the files by a pattern. With a preview and an undo of the last change.
   - Copyright check: identifies a song and gives a traffic light for the risk of a claim / block on streams and videos.
     It is an indication, not a guarantee.
*/
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Ares.Data;
using Ares.Tags;
using Ares.TagsImport;

namespace Ares.Player
{
    // ---- the rules for names (no windows, easy to read and to test) ----

    internal static class MusicNames
    {
        private static readonly Regex s_Noise = new Regex(
            @"[\[\(\{][^\]\)\}]*\b(official|oficial|video|vídeo|videoclipe|clipe|audio|áudio|lyrics?|letra|legendado|hd|hq|4k|1080p|720p|remaster(ed)?|visuali[sz]er|music video|full album|explicit|download|free download|mv|m/v|live)\b[^\]\)\}]*[\]\)\}]",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex s_Site = new Regex(@"\bwww\.[^\s]+?\.(com|net|org|br|info|ru|me)\b", RegexOptions.IgnoreCase);
        private static readonly Regex s_VideoId = new Regex(@"[\[\(]?\b[A-Za-z0-9_-]{11}\b[\]\)]?\s*$");
        private static readonly Regex s_Topic = new Regex(@"\s*-\s*topic\s*$", RegexOptions.IgnoreCase);
        private static readonly Regex s_Lead1 = new Regex(@"^\s*\(?\d{1,3}\)?\s*[\.\-_\)]+\s*");
        private static readonly Regex s_Lead2 = new Regex(@"^\s*\d{1,3}\s+(?=[^\d\s])");
        private static readonly Regex s_Spaces = new Regex(@"\s{2,}");
        private static readonly string[] s_Small = { "a", "an", "and", "as", "at", "but", "by", "for", "in", "of", "on", "or", "the", "to", "de", "da", "do", "das", "dos", "e", "em", "na", "no", "o", "os", "um", "uma", "com", "para", "por" };

        /// <summary>Takes the clutter out of a file name (without the extension).</summary>
        public static string Clean(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;
            string s = name.Replace('_', ' ');
            s = s_Site.Replace(s, " ");
            s = s_Noise.Replace(s, " ");
            s = s_Topic.Replace(s, "");
            // "Song name [dQw4w9WgXcQ]" (the id of a video at the end), only with brackets
            s = Regex.Replace(s, @"\s*[\[\(][A-Za-z0-9_-]{11}[\]\)]\s*$", "");
            s = s_Lead1.Replace(s, "");
            s = s_Lead2.Replace(s, "");
            s = s_Spaces.Replace(s, " ").Trim(' ', '-', '.', '_', '–', '—');
            if (s.Length == 0)
                return name;
            return TitleCaseIfShouting(s);
        }

        // "SOME SONG" and "some song" become "Some Song"; mixed names are left as the author wrote them
        private static string TitleCaseIfShouting(string s)
        {
            bool hasUpper = false, hasLower = false;
            foreach (char c in s)
            {
                if (char.IsUpper(c)) hasUpper = true;
                else if (char.IsLower(c)) hasLower = true;
            }
            if (hasUpper && hasLower)
                return s;
            if (!hasUpper && !hasLower)
                return s;
            string[] words = s.ToLower(CultureInfo.CurrentCulture).Split(' ');
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < words.Length; i++)
            {
                string w = words[i];
                if (w.Length == 0)
                    continue;
                bool small = i > 0 && Array.IndexOf(s_Small, w) >= 0;
                if (!small)
                    w = char.ToUpper(w[0], CultureInfo.CurrentCulture) + w.Substring(1);
                if (sb.Length > 0)
                    sb.Append(' ');
                sb.Append(w);
            }
            return sb.ToString();
        }

        /// <summary>Splits "Artist - Title" (only when there are exactly two parts).</summary>
        public static bool TrySplit(string name, out string artist, out string title)
        {
            artist = title = null;
            if (string.IsNullOrEmpty(name))
                return false;
            string[] parts = name.Split(new[] { " - " }, StringSplitOptions.None);
            if (parts.Length != 2)
                return false;
            artist = parts[0].Trim();
            title = parts[1].Trim();
            return artist.Length > 0 && title.Length > 0;
        }

        public static string Fill(string pattern, string artist, string title, string album, uint track)
        {
            string s = pattern;
            s = ReplaceTag(s, "artista", artist);
            s = ReplaceTag(s, "artist", artist);
            s = ReplaceTag(s, "titulo", title);
            s = ReplaceTag(s, "título", title);
            s = ReplaceTag(s, "title", title);
            s = ReplaceTag(s, "album", album);
            s = ReplaceTag(s, "álbum", album);
            s = ReplaceTag(s, "faixa", track > 0 ? track.ToString("00") : "");
            s = ReplaceTag(s, "track", track > 0 ? track.ToString("00") : "");
            return s;
        }

        private static string ReplaceTag(string text, string name, string value)
        {
            return Regex.Replace(text, "\\{" + Regex.Escape(name) + "\\}", (value ?? "").Replace("$", "$$"), RegexOptions.IgnoreCase);
        }

        public static string SanitizeFileName(string name)
        {
            StringBuilder sb = new StringBuilder();
            char[] bad = Path.GetInvalidFileNameChars();
            foreach (char c in name)
                sb.Append(Array.IndexOf(bad, c) >= 0 ? '-' : c);
            string s = s_Spaces.Replace(sb.ToString(), " ").Trim().TrimEnd('.', ' ');
            return s.Length == 0 ? "sem nome" : s;
        }
    }

    // ---- common window: a list of files ----

    public partial class Player
    {
        private static readonly string[] s_AudioExtensions = { ".mp3", ".ogg", ".opus", ".wav", ".flac", ".m4a", ".aac", ".wma", ".mp2" };

        private void InitMusicToolsMenu()
        {
            ToolStripMenuItem renamer = new ToolStripMenuItem(T("Renomeador de músicas", "Music renamer"));
            renamer.Click += (s, e) => ShowMusicRenamer();
            ToolStripMenuItem copyright = new ToolStripMenuItem(T("Verificador de direitos autorais", "Copyright check"));
            copyright.Click += (s, e) => ShowCopyrightCheck();
            extrasToolStripMenuItem.DropDownItems.Add(renamer);
            extrasToolStripMenuItem.DropDownItems.Add(copyright);
        }

        private void ShowMusicRenamer()
        {
            using (RenamerDialog dlg = new RenamerDialog(this))
                dlg.ShowDialog(this);
        }

        private void ShowCopyrightCheck()
        {
            using (CopyrightDialog dlg = new CopyrightDialog(this))
                dlg.ShowDialog(this);
        }

        private List<string> MusicFilesOfProject()
        {
            List<string> result = new List<string>();
            if (m_Project == null)
                return result;
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
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (FileRef r in collector.Files)
            {
                if (r.File.SoundFileType != SoundFileType.Music)
                    continue;
                string full = FullPathOf(r.File);
                try
                {
                    if (File.Exists(full) && seen.Add(full))
                        result.Add(full);
                }
                catch (Exception swallowed)
                {
                    Ares.Settings.ErrorLog.Note(swallowed);
                }
            }
            return result;
        }

        // after files were renamed: the open project points to the new names
        private void UpdateProjectPaths(Dictionary<string, string> oldToNew)
        {
            if (m_Project == null || oldToNew.Count == 0)
                return;
            FileCollector collector = new FileCollector();
            foreach (IMode mode in m_Project.GetModes())
            {
                foreach (IModeElement me in mode.GetElements())
                {
                    collector.Mode = mode;
                    collector.ModeElement = me;
                    collector.Place = mode.Title;
                    me.Visit(collector);
                }
            }
            List<KeyValuePair<FileRef, string>> changes = new List<KeyValuePair<FileRef, string>>();
            foreach (FileRef r in collector.Files)
            {
                string newPath;
                if (oldToNew.TryGetValue(FullPathOf(r.File), out newPath))
                    changes.Add(new KeyValuePair<FileRef, string>(r, newPath));
            }
            if (changes.Count == 0)
                return;
            string keep = SelectedModeNode != null ? SelectedModeNode.Title : null;
            ApplyProjectEdit(() =>
            {
                foreach (KeyValuePair<FileRef, string> change in changes)
                    change.Key.File.FilePath = ToStoredPath(change.Key.File, change.Value);
            }, keep);
        }

        private class ToolItem
        {
            public string Path;
            public string NewName;              // null = the name stays
            public string NewTitle, NewArtist, NewAlbum;  // null = the tag stays
            public string Status = "";
            public ListViewItem Row;
        }

        /// <summary>Base of both windows: the list of files and the buttons to fill it.</summary>
        private abstract class MusicToolForm : Form
        {
            protected readonly Player m_Owner;
            protected readonly List<ToolItem> m_Items = new List<ToolItem>();
            protected readonly ListView m_List = new ListView();
            protected readonly Label m_Info = new Label();
            protected readonly ProgressBar m_Progress = new ProgressBar();
            protected readonly FlowLayoutPanel m_Top = new FlowLayoutPanel();
            protected volatile bool m_Cancel;
            protected bool m_Busy;

            protected MusicToolForm(Player owner, string title)
            {
                m_Owner = owner;
                Text = title;
                AutoScaleMode = AutoScaleMode.Font;
                ClientSize = new Size(960, 620);
                MinimumSize = new Size(760, 480);
                StartPosition = FormStartPosition.CenterParent;
                ShowInTaskbar = false;
                MinimizeBox = false;
                try { Icon = Ares.CommonGUI.ApoloBrand.GetIcon(32, Ares.CommonGUI.ApoloState.Playing, 0); } catch (Exception swallowed) { Ares.Settings.ErrorLog.Note(swallowed); }

                m_Top.SetBounds(12, 10, 936, 36);
                m_Top.WrapContents = false;
                m_Top.FlowDirection = FlowDirection.LeftToRight;
                m_Top.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                Controls.Add(m_Top);
                int x = 0;
                x = AddTopButton(T("Adicionar arquivos", "Add files"), x, (s, e) => AddFiles());
                x = AddTopButton(T("Adicionar pasta", "Add folder"), x, (s, e) => AddFolder());
                x = AddTopButton(T("Músicas do projeto aberto", "Music of the open project"), x, (s, e) => AddProjectFiles());
                x = AddTopButton(T("Limpar lista", "Clear list"), x, (s, e) => ClearList());

                m_List.View = View.Details;
                m_List.FullRowSelect = true;
                m_List.GridLines = true;
                m_List.HideSelection = false;
                m_List.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
                Controls.Add(m_List);

                m_Info.AutoSize = false;
                m_Info.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
                Controls.Add(m_Info);

                m_Progress.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
                m_Progress.Visible = false;
                Controls.Add(m_Progress);

                FormClosing += (s, e) =>
                {
                    if (m_Busy)
                    {
                        m_Cancel = true;
                    }
                };
            }

            protected static string T(string pt, string en)
            {
                return Player.T(pt, en);
            }

            private int AddTopButton(string text, int x, EventHandler click)
            {
                Button b = new Button();
                b.Text = text;
                b.AutoSize = true;
                b.AutoSizeMode = AutoSizeMode.GrowAndShrink;
                b.Padding = new Padding(8, 2, 8, 2);
                b.Margin = new Padding(0, 0, 8, 0);
                b.Click += click;
                m_Top.Controls.Add(b);
                return b.Right + 8;
            }

            /// <summary>Places the list between the top buttons and the bottom area (called by the windows after they added their controls).</summary>
            protected void LayoutList(int top, int bottomHeight)
            {
                m_List.SetBounds(12, top, ClientSize.Width - 24, ClientSize.Height - top - bottomHeight);
            }

            protected abstract void OnListChanged();

            private void AddFiles()
            {
                using (OpenFileDialog dlg = new OpenFileDialog())
                {
                    dlg.Multiselect = true;
                    dlg.Filter = T("Arquivos de áudio|", "Audio files|") + string.Join(";", s_AudioExtensions.Select(x => "*" + x).ToArray()) + "|" + T("Todos os arquivos|*.*", "All files|*.*");
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                        Add(dlg.FileNames);
                }
            }

            private void AddFolder()
            {
                using (FolderBrowserDialog dlg = new FolderBrowserDialog())
                {
                    dlg.Description = T("Escolha uma pasta (as subpastas também entram)", "Choose a folder (subfolders are included)");
                    if (dlg.ShowDialog(this) != DialogResult.OK)
                        return;
                    List<string> files = new List<string>();
                    try
                    {
                        foreach (string f in Directory.GetFiles(dlg.SelectedPath, "*", SearchOption.AllDirectories))
                        {
                            if (Array.IndexOf(s_AudioExtensions, Path.GetExtension(f).ToLowerInvariant()) >= 0)
                                files.Add(f);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, ex.Message, StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    files.Sort(StringComparer.CurrentCultureIgnoreCase);
                    Add(files);
                }
            }

            private void AddProjectFiles()
            {
                if (m_Owner.m_Project == null)
                {
                    MessageBox.Show(this, T("Abra um projeto primeiro.", "Open a project first."), StringResources.Ares,
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                Add(m_Owner.MusicFilesOfProject());
            }

            private void ClearList()
            {
                if (m_Busy)
                    return;
                m_Items.Clear();
                m_List.Items.Clear();
                OnListChanged();
            }

            protected void Add(IEnumerable<string> paths)
            {
                if (m_Busy)
                    return;
                HashSet<string> have = new HashSet<string>(m_Items.Select(i => i.Path), StringComparer.OrdinalIgnoreCase);
                foreach (string p in paths)
                {
                    if (!have.Add(p))
                        continue;
                    ToolItem item = new ToolItem { Path = p };
                    m_Items.Add(item);
                }
                RebuildRows();
                OnListChanged();
            }

            protected abstract void RebuildRows();

            protected void SetBusy(bool busy)
            {
                m_Busy = busy;
                m_Progress.Visible = busy;
                if (!busy)
                    m_Cancel = false;
            }

            protected void Ui(Action action)
            {
                if (IsDisposed || !IsHandleCreated)
                    return;
                try { BeginInvoke(action); } catch (InvalidOperationException swallowed) { Ares.Settings.ErrorLog.Note(swallowed); }
            }
        }

        // ---- renamer ----

        private class RenamerDialog : MusicToolForm
        {
            private readonly CheckBox m_Clean = new CheckBox();
            private readonly CheckBox m_Online = new CheckBox();
            private readonly CheckBox m_Pattern = new CheckBox();
            private readonly ComboBox m_PatternBox = new ComboBox();
            private readonly Button m_Preview = new Button();
            private readonly Button m_Apply = new Button();
            private readonly Button m_Undo = new Button();
            private readonly Button m_Close = new Button();
            private bool m_HasPreview;

            private static string PatternFile
            {
                get { return Path.Combine(Ares.Settings.AppFolders.Root, "rename-pattern.txt"); }
            }

            // the pattern is remembered between sessions; the first time it is "title - artist"
            private static string LoadPattern()
            {
                try
                {
                    if (File.Exists(PatternFile))
                    {
                        string t = File.ReadAllText(PatternFile, Encoding.UTF8).Trim();
                        if (t.Length > 0)
                            return t;
                    }
                }
                catch (Exception swallowed)
                {
                    Ares.Settings.ErrorLog.Note(swallowed);
                }
                return "{titulo} - {artista}";
            }

            private static void SavePattern(string pattern)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(PatternFile));
                    File.WriteAllText(PatternFile, pattern.Trim(), Encoding.UTF8);
                }
                catch (Exception swallowed)
                {
                    Ares.Settings.ErrorLog.Note(swallowed);
                }
            }

            private static string UndoFile
            {
                get { return Path.Combine(Ares.Settings.AppFolders.Root, "rename-undo.tsv"); }
            }

            public RenamerDialog(Player owner)
                : base(owner, T("Renomeador de músicas", "Music renamer"))
            {
                m_List.CheckBoxes = true;
                m_List.Columns.Add(T("Arquivo atual", "Current file"), 250);
                m_List.Columns.Add(T("Novo nome", "New name"), 250);
                m_List.Columns.Add(T("Tags novas (artista | título | álbum)", "New tags (artist | title | album)"), 270);
                m_List.Columns.Add(T("Situação", "Status"), 150);

                GroupBox options = new GroupBox();
                options.Text = T("O que fazer", "What to do");
                options.SetBounds(12, 48, 936, 104);
                options.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                m_Clean.Text = T("Limpar o nome do arquivo (tira \"[Official Video]\", \"(Lyrics)\", números de faixa, sublinhados e ajusta as maiúsculas)",
                    "Clean the file name (removes \"[Official Video]\", \"(Lyrics)\", track numbers, underscores and fixes capitals)");
                m_Clean.Checked = true;
                m_Clean.SetBounds(12, 22, 910, 22);
                m_Online.Text = T("Buscar título, artista e álbum na internet (AcoustID + MusicBrainz) e gravar nas tags do arquivo — leva alguns segundos por música",
                    "Look up title, artist and album on the internet (AcoustID + MusicBrainz) and write them into the file tags — takes a few seconds per song");
                m_Online.SetBounds(12, 46, 910, 22);
                m_Pattern.Text = T("Renomear o arquivo pelas tags, no padrão:", "Rename the file by its tags, with the pattern:");
                m_Pattern.SetBounds(12, 70, 330, 22);
                m_PatternBox.DropDownStyle = ComboBoxStyle.DropDown;
                m_PatternBox.Items.AddRange(new object[] { "{titulo} - {artista}", "{artista} - {titulo}", "{faixa} - {titulo}", "{titulo}", "{artista} - {album} - {titulo}", "{album} - {faixa} - {titulo}" });
                m_PatternBox.Text = LoadPattern();
                m_PatternBox.SetBounds(346, 68, 250, 24);
                Label hint = new Label();
                hint.Text = T("escolha na lista ou escreva: {artista} {titulo} {album} {faixa}", "pick from the list or write: {artista} {titulo} {album} {faixa}");
                hint.SetBounds(604, 72, 320, 20);
                hint.ForeColor = SystemColors.GrayText;
                options.Controls.Add(m_Clean);
                options.Controls.Add(m_Online);
                options.Controls.Add(m_Pattern);
                options.Controls.Add(m_PatternBox);
                options.Controls.Add(hint);
                Controls.Add(options);

                LayoutList(160, 96);
                m_Info.SetBounds(12, ClientSize.Height - 90, ClientSize.Width - 24, 36);
                m_Progress.SetBounds(12, ClientSize.Height - 52, ClientSize.Width - 24, 8);

                int y = ClientSize.Height - 40;
                SetupButton(m_Preview, T("Gerar prévia", "Preview"), 12, y, 130, (s, e) => RunPreview());
                SetupButton(m_Apply, T("Aplicar", "Apply"), 150, y, 110, (s, e) => ApplyChanges());
                SetupButton(m_Undo, T("Desfazer a última renomeação", "Undo the last renaming"), 268, y, 210, (s, e) => UndoLast());
                SetupButton(m_Close, T("Fechar", "Close"), ClientSize.Width - 112, y, 100, (s, e) => Close());
                m_Close.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
                m_Apply.Enabled = false;
                m_Undo.Enabled = File.Exists(UndoFile);
                OnListChanged();
            }

            private void SetupButton(Button b, string text, int x, int y, int w, EventHandler click)
            {
                b.Text = text;
                b.SetBounds(x, y, w, 30);
                b.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
                b.Click += click;
                Controls.Add(b);
            }

            protected override void OnListChanged()
            {
                m_HasPreview = false;
                m_Apply.Enabled = false;
                m_Preview.Enabled = m_Items.Count > 0 && !m_Busy;
                m_Info.Text = m_Items.Count == 0
                    ? T("Adicione arquivos ou uma pasta, escolha o que fazer e clique em \"Gerar prévia\". Nada muda nos arquivos até você clicar em \"Aplicar\".",
                        "Add files or a folder, choose what to do and click \"Preview\". Nothing changes in the files until you click \"Apply\".")
                    : m_Items.Count + T(" arquivo(s) na lista. Clique em \"Gerar prévia\" para ver o que vai mudar.",
                        " file(s) in the list. Click \"Preview\" to see what will change.");
            }

            protected override void RebuildRows()
            {
                m_List.BeginUpdate();
                m_List.Items.Clear();
                foreach (ToolItem item in m_Items)
                {
                    ListViewItem row = new ListViewItem(Path.GetFileName(item.Path));
                    row.SubItems.Add("");
                    row.SubItems.Add("");
                    row.SubItems.Add("");
                    row.ToolTipText = item.Path;
                    row.Tag = item;
                    item.Row = row;
                    m_List.Items.Add(row);
                }
                m_List.EndUpdate();
            }

            private void SetBusyUi(bool busy)
            {
                SetBusy(busy);
                m_Preview.Enabled = !busy && m_Items.Count > 0;
                m_Apply.Enabled = !busy && m_HasPreview && m_Items.Any(i => HasChange(i));
                m_Undo.Enabled = !busy && File.Exists(UndoFile);
                m_Clean.Enabled = m_Online.Enabled = m_Pattern.Enabled = m_PatternBox.Enabled = !busy;
            }

            private static bool HasChange(ToolItem item)
            {
                return item.NewName != null || item.NewTitle != null || item.NewArtist != null || item.NewAlbum != null;
            }

            private void RunPreview()
            {
                if (m_Busy || m_Items.Count == 0)
                    return;
                bool clean = m_Clean.Checked, online = m_Online.Checked, usePattern = m_Pattern.Checked;
                string pattern = m_PatternBox.Text;
                if (usePattern && string.IsNullOrWhiteSpace(pattern))
                {
                    MessageBox.Show(this, T("Escreva o padrão do nome, por exemplo {artista} - {titulo}.", "Write the name pattern, for example {artista} - {titulo}."),
                        StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                if (!clean && !online && !usePattern)
                {
                    MessageBox.Show(this, T("Marque pelo menos uma opção em \"O que fazer\".", "Tick at least one option in \"What to do\"."),
                        StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                if (usePattern)
                    SavePattern(pattern);
                List<ToolItem> items = m_Items.ToList();
                SetBusyUi(true);
                m_Progress.Minimum = 0;
                m_Progress.Maximum = items.Count;
                m_Progress.Value = 0;
                System.Threading.Tasks.Task.Factory.StartNew(() =>
                {
                    int done = 0;
                    foreach (ToolItem item in items)
                    {
                        if (m_Cancel)
                            break;
                        try { Compute(item, clean, online, usePattern, pattern); }
                        catch (Exception ex) { item.Status = ex.Message; }
                        int n = ++done;
                        Ui(() =>
                        {
                            ShowRow(item);
                            m_Progress.Value = Math.Min(m_Progress.Maximum, n);
                        });
                    }
                    Ui(() =>
                    {
                        m_HasPreview = true;
                        SetBusyUi(false);
                        int changes = items.Count(i => HasChange(i));
                        m_Info.Text = changes + T(" arquivo(s) com mudanças marcadas. Desmarque o que você não quer mudar e clique em \"Aplicar\".",
                            " file(s) with changes ticked. Untick what you do not want to change and click \"Apply\".");
                    });
                });
            }

            private void Compute(ToolItem item, bool clean, bool online, bool usePattern, string pattern)
            {
                item.NewName = item.NewTitle = item.NewArtist = item.NewAlbum = null;
                item.Status = "";
                AudioTagInfo tag = AudioTags.Read(item.Path) ?? new AudioTagInfo();
                string ext = Path.GetExtension(item.Path);
                string stem = Path.GetFileNameWithoutExtension(item.Path);
                string artist = tag.artist, title = tag.title, album = tag.album;
                if (online)
                {
                    MusicLookupResult r = MusicLookup.Identify(item.Path, false);
                    if (r.Identified)
                    {
                        if (!string.IsNullOrEmpty(r.Title) && r.Title != tag.title) item.NewTitle = r.Title;
                        if (!string.IsNullOrEmpty(r.Artist) && r.Artist != tag.artist) item.NewArtist = r.Artist;
                        if (!string.IsNullOrEmpty(r.Album) && r.Album != tag.album) item.NewAlbum = r.Album;
                        title = item.NewTitle ?? title;
                        artist = item.NewArtist ?? artist;
                        album = item.NewAlbum ?? album;
                        item.Status = T("identificada", "identified");
                    }
                    else if (r.Error == "dll")
                        item.Status = T("falta a chromaprint.dll", "chromaprint.dll is missing");
                    else if (!string.IsNullOrEmpty(r.Error))
                        item.Status = T("erro: ", "error: ") + r.Error;
                    else
                        item.Status = T("não identificada", "not identified");
                }
                string baseName = clean ? MusicNames.Clean(stem) : stem;
                if (usePattern)
                {
                    string a = artist, t = title;
                    if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(t))
                    {
                        // no tags: "Artist - Title" in the file name itself
                        string ga, gt;
                        if (MusicNames.TrySplit(baseName, out ga, out gt))
                        {
                            if (string.IsNullOrEmpty(a)) a = ga;
                            if (string.IsNullOrEmpty(t)) t = gt;
                        }
                    }
                    if (!string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(t))
                        baseName = MusicNames.Fill(pattern, a, t, album, tag.track);
                    else
                        item.Status = (item.Status.Length > 0 ? item.Status + "; " : "") + T("sem artista/título para o padrão", "no artist/title for the pattern");
                }
                baseName = MusicNames.SanitizeFileName(baseName);
                string newName = baseName + ext;
                if (!string.Equals(newName, Path.GetFileName(item.Path), StringComparison.Ordinal))
                    item.NewName = newName;
                if (!HasChange(item) && item.Status.Length == 0)
                    item.Status = T("sem mudanças", "no changes");
            }

            private void ShowRow(ToolItem item)
            {
                ListViewItem row = item.Row;
                if (row == null)
                    return;
                row.SubItems[1].Text = item.NewName ?? "";
                List<string> tags = new List<string>();
                if (item.NewArtist != null) tags.Add(item.NewArtist);
                if (item.NewTitle != null) tags.Add(item.NewTitle);
                if (item.NewAlbum != null) tags.Add(item.NewAlbum);
                row.SubItems[2].Text = tags.Count == 0 ? "" : string.Join(" | ", tags.ToArray());
                row.SubItems[3].Text = item.Status;
                row.Checked = HasChange(item);
            }

            // ---- apply and undo ----

            private static string Esc(string s)
            {
                return (s ?? "").Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
            }

            private static string UniqueTarget(string path, string wantedPath)
            {
                if (!File.Exists(wantedPath) || string.Equals(path, wantedPath, StringComparison.OrdinalIgnoreCase))
                    return wantedPath;
                string dir = Path.GetDirectoryName(wantedPath);
                string stem = Path.GetFileNameWithoutExtension(wantedPath);
                string ext = Path.GetExtension(wantedPath);
                for (int i = 2; i < 1000; i++)
                {
                    string candidate = Path.Combine(dir, stem + " (" + i + ")" + ext);
                    if (!File.Exists(candidate))
                        return candidate;
                }
                return wantedPath;
            }

            private static void MoveFile(string from, string to)
            {
                if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase) && !string.Equals(from, to, StringComparison.Ordinal))
                {
                    // only the capitals change: through a temporary name
                    string temp = from + ".apolo-tmp";
                    File.Move(from, temp);
                    File.Move(temp, to);
                }
                else
                {
                    File.Move(from, to);
                }
            }

            private void ApplyChanges()
            {
                List<ToolItem> chosen = m_Items.Where(i => i.Row != null && i.Row.Checked && HasChange(i)).ToList();
                if (chosen.Count == 0)
                {
                    MessageBox.Show(this, T("Nada marcado para mudar.", "Nothing ticked to change."), StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                if (MessageBox.Show(this, T(chosen.Count + " arquivo(s) serão alterados (nomes e/ou tags). Você poderá desfazer esta renomeação depois. Continuar?",
                        chosen.Count + " file(s) will be changed (names and/or tags). You can undo this renaming afterwards. Continue?"),
                        StringResources.Ares, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;
                m_Owner.StopAllPlaying();
                List<string> log = new List<string>();
                Dictionary<string, string> moved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                int failed = 0;
                foreach (ToolItem item in chosen)
                {
                    try
                    {
                        AudioTagInfo old = AudioTags.Read(item.Path) ?? new AudioTagInfo();
                        bool tagsChanged = item.NewTitle != null || item.NewArtist != null || item.NewAlbum != null;
                        if (tagsChanged && !AudioTags.Write(item.Path, item.NewTitle, item.NewArtist, item.NewAlbum))
                        {
                            item.Status = T("não consegui gravar as tags", "could not write the tags");
                            tagsChanged = false;
                            failed++;
                        }
                        string target = item.Path;
                        if (item.NewName != null)
                        {
                            target = UniqueTarget(item.Path, Path.Combine(Path.GetDirectoryName(item.Path), item.NewName));
                            MoveFile(item.Path, target);
                            moved[item.Path] = target;
                        }
                        log.Add(Esc(item.Path) + "\t" + Esc(target) + "\t" + (tagsChanged ? "1" : "0") + "\t" + Esc(old.title) + "\t" + Esc(old.artist) + "\t" + Esc(old.album));
                        item.Status = T("feito", "done");
                        item.Path = target;
                    }
                    catch (Exception ex)
                    {
                        item.Status = ex.Message;
                        failed++;
                    }
                    ShowRow(item);
                    item.Row.SubItems[3].Text = item.Status;
                    item.Row.Checked = false;
                }
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(UndoFile));
                    File.WriteAllLines(UndoFile, log.ToArray(), Encoding.UTF8);
                }
                catch (Exception swallowed)
                {
                    Ares.Settings.ErrorLog.Note(swallowed);
                }
                m_Owner.UpdateProjectPaths(moved);
                m_Apply.Enabled = false;
                m_HasPreview = false;
                m_Undo.Enabled = File.Exists(UndoFile);
                m_Info.Text = (log.Count) + T(" arquivo(s) alterado(s).", " file(s) changed.")
                    + (failed > 0 ? "  " + failed + T(" com problema (veja a coluna Situação).", " with problems (see the Status column).") : "")
                    + "  " + T("O projeto aberto já aponta para os novos nomes; outros projetos podem pedir \"Arquivos de áudio ausentes\" no menu Ferramentas.",
                        "The open project already points to the new names; other projects may need \"Missing audio files\" in the Tools menu.");
            }

            private void UndoLast()
            {
                if (!File.Exists(UndoFile))
                    return;
                if (MessageBox.Show(this, T("Desfazer a última renomeação (nomes e tags)?", "Undo the last renaming (names and tags)?"),
                        StringResources.Ares, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;
                m_Owner.StopAllPlaying();
                string[] lines;
                try { lines = File.ReadAllLines(UndoFile, Encoding.UTF8); }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                Dictionary<string, string> moved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                int restored = 0, failed = 0;
                for (int i = lines.Length - 1; i >= 0; i--)
                {
                    string[] f = lines[i].Split('\t');
                    if (f.Length < 6)
                        continue;
                    string oldPath = f[0], newPath = f[1];
                    try
                    {
                        string current = newPath;
                        if (!File.Exists(current))
                        {
                            failed++;
                            continue;
                        }
                        if (f[2] == "1")
                            AudioTags.WriteExact(current, f[3], f[4], f[5]);
                        if (!string.Equals(oldPath, newPath, StringComparison.Ordinal))
                        {
                            if (File.Exists(oldPath) && !string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase))
                            {
                                failed++;
                                continue;
                            }
                            MoveFile(newPath, oldPath);
                            moved[newPath] = oldPath;
                        }
                        restored++;
                    }
                    catch (Exception)
                    {
                        failed++;
                    }
                }
                try { File.Delete(UndoFile); } catch (Exception swallowed) { Ares.Settings.ErrorLog.Note(swallowed); }
                m_Owner.UpdateProjectPaths(moved);
                m_Undo.Enabled = false;
                m_Items.Clear();
                m_List.Items.Clear();
                OnListChanged();
                m_Info.Text = restored + T(" arquivo(s) restaurado(s)", " file(s) restored")
                    + (failed > 0 ? ", " + failed + T(" não puderam ser restaurados (arquivo ausente ou nome já ocupado).", " could not be restored (file missing or name taken).") : ".");
            }
        }

        // ---- copyright check ----

        private class CopyrightDialog : MusicToolForm
        {
            private readonly Button m_Check = new Button();
            private readonly Button m_Close = new Button();
            private readonly Label m_Disclaimer = new Label();

            private static readonly Regex s_Free = new Regex(
                @"creative\s*commons|\bcc[\s\-]?by\b|\bcc0\b|public\s*domain|dom[ií]nio\s*p[úu]blico|royalty[\s\-]?free|sem\s*direitos|no\s*copyright|copyright[\s\-]?free|free\s*to\s*use|licen[cç]a\s*livre|incompetech|kevin\s*macleod|free\s*music\s*archive|pixabay|freesound|musopen|youtube\s*audio\s*library|biblioteca\s*de\s*[áa]udio|uppbeat|bensound|jamendo",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            private static readonly Regex s_NonCommercial = new Regex(@"\bby[\s\-]?nc\b|non[\s\-]?commercial|n[ãa]o\s*comercial", RegexOptions.IgnoreCase);
            private static readonly Regex s_Major = new Regex(
                @"universal|\bumg\b|sony|columbia|\brca\b|\bepic\b|warner|\bwmg\b|atlantic|elektra|parlophone|\bemi\b|capitol|virgin|island|interscope|def\s*jam|republic|polydor|geffen|\ba&m\b|motown|\bbmg\b|arista|mercury|decca|deutsche\s*grammophon|som\s*livre|rhino|sbk|roc nation|roadrunner",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            public CopyrightDialog(Player owner)
                : base(owner, T("Verificador de direitos autorais", "Copyright check"))
            {
                m_List.Columns.Add(T("Arquivo", "File"), 220);
                m_List.Columns.Add(T("Música identificada", "Identified song"), 250);
                m_List.Columns.Add(T("Selo", "Label"), 130);
                m_List.Columns.Add(T("Risco", "Risk"), 80);
                m_List.Columns.Add(T("Por quê", "Why"), 250);

                LayoutList(48, 130);
                m_Disclaimer.AutoSize = false;
                m_Disclaimer.SetBounds(12, ClientSize.Height - 124, ClientSize.Width - 24, 66);
                m_Disclaimer.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
                m_Disclaimer.ForeColor = SystemColors.GrayText;
                m_Disclaimer.Text = T("Atenção: isto é só um indicativo, não uma garantia. Ninguém consegue consultar o Content ID do YouTube nem as regras do Spotify; "
                    + "o Apolo reconhece a música pela \"impressão digital\" do áudio (AcoustID/MusicBrainz) e avisa quando ela parece ser um lançamento comercial. "
                    + "Para lives e vídeos monetizados, o mais seguro é música com licença livre, comprada com licença de uso ou feita por você.",
                    "Note: this is only an indication, not a guarantee. Nobody can query YouTube's Content ID or the rules of Spotify; "
                    + "Apolo recognizes the song by the audio \"fingerprint\" (AcoustID/MusicBrainz) and warns when it looks like a commercial release. "
                    + "For streams and monetized videos the safest choice is music with a free licence, bought with a usage licence, or made by you.");
                Controls.Add(m_Disclaimer);
                m_Info.SetBounds(12, ClientSize.Height - 54, ClientSize.Width - 24, 20);
                m_Progress.SetBounds(12, ClientSize.Height - 34, ClientSize.Width - 24, 6);
                m_Check.Text = T("Verificar", "Check");
                m_Check.SetBounds(12, ClientSize.Height - 40, 130, 30);
                m_Check.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
                m_Check.Click += (s, e) => RunCheck();
                Controls.Add(m_Check);
                m_Close.Text = T("Fechar", "Close");
                m_Close.SetBounds(ClientSize.Width - 112, ClientSize.Height - 40, 100, 30);
                m_Close.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
                m_Close.Click += (s, e) => Close();
                Controls.Add(m_Close);
                m_Info.SetBounds(150, ClientSize.Height - 36, ClientSize.Width - 270, 22);
                m_Progress.SetBounds(150, ClientSize.Height - 12, ClientSize.Width - 270, 6);
                OnListChanged();
            }

            protected override void OnListChanged()
            {
                m_Check.Enabled = m_Items.Count > 0 && !m_Busy;
                m_Info.Text = m_Items.Count == 0
                    ? T("Adicione arquivos ou uma pasta e clique em \"Verificar\".", "Add files or a folder and click \"Check\".")
                    : m_Items.Count + T(" arquivo(s). Clique em \"Verificar\".", " file(s). Click \"Check\".");
            }

            protected override void RebuildRows()
            {
                m_List.BeginUpdate();
                m_List.Items.Clear();
                foreach (ToolItem item in m_Items)
                {
                    ListViewItem row = new ListViewItem(Path.GetFileName(item.Path));
                    for (int i = 0; i < 4; i++)
                        row.SubItems.Add("");
                    row.ToolTipText = item.Path;
                    row.Tag = item;
                    item.Row = row;
                    m_List.Items.Add(row);
                }
                m_List.EndUpdate();
            }

            private void RunCheck()
            {
                if (m_Busy || m_Items.Count == 0)
                    return;
                List<ToolItem> items = m_Items.ToList();
                SetBusy(true);
                m_Check.Enabled = false;
                m_Progress.Minimum = 0;
                m_Progress.Maximum = items.Count;
                m_Progress.Value = 0;
                System.Threading.Tasks.Task.Factory.StartNew(() =>
                {
                    int done = 0;
                    foreach (ToolItem item in items)
                    {
                        if (m_Cancel)
                            break;
                        string song = "", label = "", risk = "", why = "";
                        Color color = Color.White;
                        try { Evaluate(item.Path, out song, out label, out risk, out why, out color); }
                        catch (Exception ex) { risk = "?"; why = ex.Message; }
                        int n = ++done;
                        ListViewItem row = item.Row;
                        Ui(() =>
                        {
                            row.SubItems[1].Text = song;
                            row.SubItems[2].Text = label;
                            row.SubItems[3].Text = risk;
                            row.SubItems[4].Text = why;
                            row.BackColor = color;
                            row.ForeColor = Color.Black;
                            m_Progress.Value = Math.Min(m_Progress.Maximum, n);
                        });
                    }
                    Ui(() =>
                    {
                        SetBusy(false);
                        m_Check.Enabled = m_Items.Count > 0;
                        m_Info.Text = T("Verificação concluída.", "Check finished.");
                    });
                });
            }

            private void Evaluate(string path, out string song, out string label, out string risk, out string why, out Color color)
            {
                AudioTagInfo tag = AudioTags.Read(path) ?? new AudioTagInfo();
                string tagText = tag.copyright + " " + tag.comment + " " + tag.title + " " + tag.artist + " " + tag.album + " " + Path.GetFileNameWithoutExtension(path);
                MusicLookupResult r = MusicLookup.Identify(path, true);
                song = r.Identified ? (r.Artist + " - " + r.Title + (string.IsNullOrEmpty(r.Year) ? "" : " (" + r.Year + ")")) : "";
                label = r.Label ?? "";
                Match free = s_Free.Match(tagText);
                if (free.Success)
                {
                    bool nc = s_NonCommercial.IsMatch(tagText);
                    risk = nc ? T("Amarelo", "Yellow") : T("Verde", "Green");
                    color = nc ? Color.FromArgb(255, 245, 190) : Color.FromArgb(205, 240, 205);
                    why = nc
                        ? T("Licença livre indicada (\"" + free.Value + "\"), mas só para uso NÃO comercial.", "Free licence mentioned (\"" + free.Value + "\"), but for NON-commercial use only.")
                        : T("Licença livre indicada nas tags ou no nome (\"" + free.Value + "\"). Confira se pede crédito ao autor.",
                            "Free licence mentioned in the tags or name (\"" + free.Value + "\"). Check whether it asks for credit to the author.");
                    return;
                }
                if (r.Identified)
                {
                    if (!string.IsNullOrEmpty(r.Label) && s_Major.IsMatch(r.Label))
                    {
                        risk = T("Vermelho", "Red");
                        color = Color.FromArgb(250, 205, 205);
                        why = T("Lançamento comercial de selo grande: alto risco de bloqueio ou claim em lives e vídeos.",
                            "Commercial release of a major label: high risk of a block or claim on streams and videos.");
                    }
                    else
                    {
                        risk = T("Amarelo", "Yellow");
                        color = Color.FromArgb(255, 245, 190);
                        why = T("Lançamento comercial" + (string.IsNullOrEmpty(r.Label) ? "" : " (selo independente ou desconhecido)") + ": risco médio, confira a licença antes de usar.",
                            "Commercial release" + (string.IsNullOrEmpty(r.Label) ? "" : " (independent or unknown label)") + ": medium risk, check the licence before using it.");
                    }
                    return;
                }
                risk = T("Amarelo", "Yellow");
                color = Color.FromArgb(255, 245, 190);
                if (r.Error == "dll")
                    why = T("Não deu para analisar o áudio (falta a chromaprint.dll).", "The audio could not be analysed (chromaprint.dll is missing).");
                else if (!string.IsNullOrEmpty(r.Error))
                    why = T("Não consegui consultar a internet: ", "Could not reach the internet: ") + r.Error;
                else
                    why = T("Não encontrei na base MusicBrainz. Pode ser música independente, original ou de biblioteca livre: confira de onde veio o arquivo.",
                        "Not found in MusicBrainz. It can be independent, original or from a free library: check where the file came from.");
            }
        }
    }
}
