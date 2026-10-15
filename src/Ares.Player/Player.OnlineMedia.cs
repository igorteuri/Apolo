/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 Two windows that use free material from the internet:
   - "Sugerir imagens na internet..." (menu of a playlist button): pictures for the buttons, by the title of the button.
   - Extras > "Baixar música e efeitos livres...": search and download of free music and sound effects into the library.
 The author and the licence of everything that is downloaded are saved (tags, "creditos.txt").
*/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Ares.Data;

namespace Ares.Player
{
    public partial class Player
    {
        private void InitOnlineMediaMenu()
        {
            ToolStripMenuItem item = new ToolStripMenuItem(T("Baixar música e efeitos livres...", "Download free music and effects..."));
            item.Click += (s, e) => ShowFreeAudioDownloader();
            extrasToolStripMenuItem.DropDownItems.Add(item);
        }

        private void ShowFreeAudioDownloader()
        {
            using (FreeAudioDialog dlg = new FreeAudioDialog())
                dlg.ShowDialog(this);
        }

        private void SuggestButtonImages()
        {
            IMode mode; int index;
            IModeElement me = MenuElement(out mode, out index);
            if (me == null || string.IsNullOrEmpty(m_CurrentProjectPath))
                return;
            string temp;
            MediaHit hit;
            using (ImageSearchDialog dlg = new ImageSearchDialog(me.Title))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK || dlg.TempFile == null)
                    return;
                temp = dlg.TempFile;
                hit = dlg.Chosen;
            }
            try
            {
                string spec = Ares.CommonGUI.ButtonImageStore.ImportImage(m_CurrentProjectPath, temp);
                Ares.CommonGUI.ButtonImageStore.Set(m_CurrentProjectPath, mode.Title, me.Title, spec);
                try
                {
                    string dir = Ares.CommonGUI.ButtonImageStore.IconsFolder;
                    string line = spec.Substring(4) + "\t" + hit.Title + "\t" + hit.Author + "\t" + hit.License + "\t" + hit.PageUrl;
                    File.AppendAllText(Path.Combine(dir, "creditos.txt"), line.Replace("\r", " ").Replace("\n", " ") + Environment.NewLine, Encoding.UTF8);
                }
                catch (Exception)
                {
                }
                UpdateElementsPanel();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                try { File.Delete(temp); } catch (Exception) { }
            }
        }

        // ---- free music and effects ----

        private class FreeAudioDialog : MediaForm
        {
            private readonly TextBox m_Query = new TextBox();
            private readonly ComboBox m_Kind = new ComboBox();
            private readonly CheckBox m_Commercial = new CheckBox();
            private readonly Button m_Search = new Button();
            private readonly ListView m_List = new ListView();
            private readonly Label m_Info = new Label();
            private readonly ProgressBar m_Progress = new ProgressBar();
            private readonly Button m_Page = new Button();
            private readonly Button m_Download = new Button();
            private readonly Button m_Folder = new Button();
            private readonly Button m_Key = new Button();
            private readonly Button m_YouTube = new Button();
            private readonly Button m_Import = new Button();
            private readonly Button m_Close = new Button();
            private bool m_Busy;

            public FreeAudioDialog()
                : base(T("Música e efeitos sonoros livres", "Free music and sound effects"))
            {
                ClientSize = new Size(920, 690);
                Label l = new Label();
                l.Text = T("Buscar:", "Search:");
                Place(l, 12, 16, 60, 20, AnchorStyles.Top | AnchorStyles.Left);
                Controls.Add(l);
                Place(m_Query, 76, 12, 390, 24, TopLR);
                m_Query.KeyDown += (s, e) =>
                {
                    if (e.KeyCode == System.Windows.Forms.Keys.Enter)
                    {
                        e.SuppressKeyPress = true;
                        RunSearch();
                    }
                };
                Controls.Add(m_Query);
                Place(m_Kind, 476, 12, 150, 24, AnchorStyles.Top | AnchorStyles.Right);
                m_Kind.DropDownStyle = ComboBoxStyle.DropDownList;
                m_Kind.Items.Add(T("Música", "Music"));
                m_Kind.Items.Add(T("Efeitos sonoros", "Sound effects"));
                m_Kind.SelectedIndex = 0;
                Controls.Add(m_Kind);
                Place(m_Search, 634, 10, 100, 28, AnchorStyles.Top | AnchorStyles.Right);
                m_Search.Text = T("Buscar", "Search");
                m_Search.Click += (s, e) => RunSearch();
                Controls.Add(m_Search);
                Place(m_Key, 742, 10, 166, 28, AnchorStyles.Top | AnchorStyles.Right);
                m_Key.Text = T("Chave Freesound", "Freesound key");
                m_Key.Click += (s, e) => AskFreesoundKey();
                Controls.Add(m_Key);
                Place(m_Commercial, 12, 42, 890, 22, TopLR);
                m_Commercial.Text = T("Só o que pode ser usado em lives e vídeos monetizados (uso comercial)", "Only what may be used in streams and monetized videos (commercial use)");
                m_Commercial.Checked = true;
                Controls.Add(m_Commercial);

                m_List.View = View.Details;
                m_List.FullRowSelect = true;
                m_List.GridLines = true;
                m_List.HideSelection = false;
                m_List.MultiSelect = true;
                m_List.Columns.Add(T("Título", "Title"), 300);
                m_List.Columns.Add(T("Autor", "Author"), 170);
                m_List.Columns.Add(T("Licença", "Licence"), 110);
                m_List.Columns.Add(T("Origem", "Source"), 110);
                m_List.Columns.Add(T("Duração", "Length"), 80);
                Place(m_List, 12, 70, 896, 450, All4);
                m_List.SelectedIndexChanged += (s, e) => UpdateButtons();
                Controls.Add(m_List);

                Place(m_Info, 12, 526, 896, 52, BottomLR);
                Controls.Add(m_Info);
                Place(m_Progress, 12, 582, 896, 8, BottomLR);
                m_Progress.Visible = false;
                Controls.Add(m_Progress);

                Place(m_Page, 12, 598, 190, 30, BottomL);
                m_Page.Text = T("Ouvir / ver a página", "Listen / see the page");
                m_Page.Click += (s, e) => OpenPage();
                Controls.Add(m_Page);
                Place(m_Download, 210, 598, 190, 30, BottomL);
                m_Download.Text = T("Baixar para a Biblioteca", "Download to the library");
                m_Download.Click += (s, e) => DownloadSelected();
                Controls.Add(m_Download);
                Place(m_Folder, 408, 598, 190, 30, BottomL);
                m_Folder.Text = T("Abrir a pasta dos downloads", "Open the downloads folder");
                m_Folder.Click += (s, e) => OpenFolder();
                Controls.Add(m_Folder);
                Place(m_Close, 808, 634, 100, 30, BottomR);
                m_Close.Text = T("Fechar", "Close");
                m_Close.Click += (s, e) => Close();
                Controls.Add(m_Close);

                Place(m_YouTube, 12, 634, 280, 30, BottomL);
                m_YouTube.Text = T("Biblioteca de áudio do YouTube (abrir)", "YouTube Audio Library (open)");
                m_YouTube.Click += (s, e) => OpenYouTubeLibrary();
                Controls.Add(m_YouTube);
                Place(m_Import, 300, 634, 300, 30, BottomL);
                m_Import.Text = T("Trazer arquivos baixados do YouTube...", "Bring files downloaded from YouTube...");
                m_Import.Click += (s, e) => ImportYouTubeFiles();
                Controls.Add(m_Import);

                m_Info.Text = T("Digite o que procura (em inglês acha mais). Tudo vem com autor e licença, que o Apolo guarda nas tags do arquivo e no arquivo creditos.txt. "
                    + "Licenças \"BY\" pedem que você dê crédito ao autor (na descrição da live, por exemplo).",
                    "Type what you look for. Everything comes with author and licence, which Apolo keeps in the tags of the file and in creditos.txt. "
                    + "\"BY\" licences ask you to credit the author.");
                UpdateButtons();
            }

            private static string DownloadFolder(bool effects)
            {
                return Path.Combine(Path.Combine(Ares.CommonGUI.LibraryHelper.LibraryFolder, "Livres"), effects ? "Efeitos" : "Musicas");
            }

            private bool Effects { get { return m_Kind.SelectedIndex == 1; } }

            private void UpdateButtons()
            {
                bool any = m_List.SelectedItems.Count > 0;
                m_Page.Enabled = any && !m_Busy;
                m_Download.Enabled = any && !m_Busy;
                m_Search.Enabled = !m_Busy;
            }

            private List<MediaHit> SelectedHits()
            {
                List<MediaHit> list = new List<MediaHit>();
                foreach (ListViewItem item in m_List.SelectedItems)
                {
                    MediaHit h = item.Tag as MediaHit;
                    if (h != null)
                        list.Add(h);
                }
                return list;
            }

            private void AskFreesoundKey()
            {
                using (Form f = new Form())
                {
                    f.Text = T("Chave do Freesound (opcional)", "Freesound key (optional)");
                    f.ClientSize = new Size(560, 150);
                    f.StartPosition = FormStartPosition.CenterParent;
                    f.FormBorderStyle = FormBorderStyle.FixedDialog;
                    f.MaximizeBox = false;
                    f.MinimizeBox = false;
                    f.ShowInTaskbar = false;
                    Label l = new Label();
                    l.SetBounds(12, 10, 536, 56);
                    l.Text = T("O Freesound (ótimo para efeitos sonoros) pede uma chave gratuita. Crie a sua em freesound.org/apiv2/apply e cole aqui. "
                        + "Sem a chave, a busca usa só o Wikimedia Commons e o Internet Archive.",
                        "Freesound (great for sound effects) asks for a free key. Create yours at freesound.org/apiv2/apply and paste it here. "
                        + "Without it, the search uses only Wikimedia Commons and the Internet Archive.");
                    f.Controls.Add(l);
                    TextBox box = new TextBox();
                    box.SetBounds(12, 72, 536, 24);
                    box.Text = OpenMedia.FreesoundKey;
                    f.Controls.Add(box);
                    Button ok = new Button();
                    ok.Text = "OK";
                    ok.SetBounds(358, 110, 90, 28);
                    ok.DialogResult = DialogResult.OK;
                    f.Controls.Add(ok);
                    Button cancel = new Button();
                    cancel.Text = T("Cancelar", "Cancel");
                    cancel.SetBounds(458, 110, 90, 28);
                    cancel.DialogResult = DialogResult.Cancel;
                    f.Controls.Add(cancel);
                    f.AcceptButton = ok;
                    f.CancelButton = cancel;
                    if (f.ShowDialog(this) != DialogResult.OK)
                        return;
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(OpenMedia.FreesoundKeyFile));
                        File.WriteAllText(OpenMedia.FreesoundKeyFile, box.Text.Trim());
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, ex.Message, StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }

            private void RunSearch()
            {
                string query = m_Query.Text.Trim();
                if (query.Length == 0 || m_Busy)
                    return;
                bool effects = Effects, commercial = m_Commercial.Checked;
                m_Busy = true;
                UpdateButtons();
                m_List.Items.Clear();
                m_Info.Text = T("Buscando...", "Searching...");
                System.Threading.Tasks.Task.Factory.StartNew(() =>
                {
                    string problems = "";
                    List<MediaHit> hits = null;
                    try { hits = OpenMedia.SearchAudio(query, effects, commercial, out problems); }
                    catch (Exception ex) { problems = ex.Message; }
                    Ui(() =>
                    {
                        m_Busy = false;
                        if (hits != null)
                        {
                            foreach (MediaHit h in hits)
                            {
                                string len = "";
                                if (h.Duration > 0)
                                {
                                    TimeSpan ts = TimeSpan.FromSeconds(h.Duration);
                                    len = ((int)ts.TotalMinutes) + ":" + ts.Seconds.ToString("00");
                                }
                                ListViewItem item = new ListViewItem(h.Title);
                                item.SubItems.Add(h.Author);
                                item.SubItems.Add(h.License);
                                item.SubItems.Add(h.Source);
                                item.SubItems.Add(len);
                                item.Tag = h;
                                item.ToolTipText = h.Credit;
                                m_List.Items.Add(item);
                            }
                        }
                        UpdateButtons();
                        if (m_List.Items.Count == 0)
                            m_Info.Text = problems.Length > 0
                                ? T("Não consegui buscar: ", "Could not search: ") + problems
                                : T("Nada encontrado. Tente outras palavras (em inglês acha mais).", "Nothing found. Try other words.");
                        else
                            m_Info.Text = m_List.Items.Count + T(" resultado(s). Selecione e clique em \"Baixar para a Biblioteca\".", " result(s). Select and click \"Download to the library\".")
                                + (problems.Length > 0 ? "  (" + problems + ")" : "")
                                + (OpenMedia.FreesoundKey.Length == 0 ? "  " + T("Para incluir o Freesound, use o botão \"Chave Freesound\".", "To include Freesound, use the \"Freesound key\" button.") : "");
                    });
                });
            }

            private void OpenPage()
            {
                foreach (MediaHit h in SelectedHits())
                {
                    if (h.PageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        try { Process.Start(new ProcessStartInfo(h.PageUrl) { UseShellExecute = true }); } catch (Exception) { }
                    }
                    break;
                }
            }

            // The audio library of YouTube has no public search, it only exists inside YouTube Studio (with the account of the channel).
            // So Apolo opens it, and brings the downloaded files into the library with the credit.
            private void OpenYouTubeLibrary()
            {
                try { Process.Start(new ProcessStartInfo("https://studio.youtube.com/") { UseShellExecute = true }); } catch (Exception ex) { MessageBox.Show(this, ex.Message, StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
                m_Info.Text = T("No YouTube Studio, abra \"Biblioteca de áudio\" no menu da esquerda, filtre por gênero, humor ou duração e baixe as faixas. "
                    + "Depois clique em \"Trazer arquivos baixados do YouTube...\" para colocá-las na Biblioteca do Apolo.",
                    "In YouTube Studio open \"Audio library\" in the left menu, filter by genre, mood or length and download the tracks. "
                    + "Then click \"Bring files downloaded from YouTube...\" to put them in the Apolo library.");
            }

            private void ImportYouTubeFiles()
            {
                string[] files;
                using (OpenFileDialog dlg = new OpenFileDialog())
                {
                    dlg.Multiselect = true;
                    dlg.Title = T("Arquivos baixados da Biblioteca de áudio do YouTube", "Files downloaded from the YouTube Audio Library");
                    dlg.Filter = T("Arquivos de áudio|*.mp3;*.ogg;*.wav;*.flac;*.m4a|Todos os arquivos|*.*", "Audio files|*.mp3;*.ogg;*.wav;*.flac;*.m4a|All files|*.*");
                    string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                    if (Directory.Exists(downloads))
                        dlg.InitialDirectory = downloads;
                    if (dlg.ShowDialog(this) != DialogResult.OK)
                        return;
                    files = dlg.FileNames;
                }
                string folder = Path.Combine(Path.Combine(Ares.CommonGUI.LibraryHelper.LibraryFolder, "Livres"), "YouTube");
                int ok = 0;
                string error = null;
                foreach (string f in files)
                {
                    try
                    {
                        Directory.CreateDirectory(folder);
                        string name = Path.GetFileName(f);
                        string target = Path.Combine(folder, name);
                        for (int i = 2; File.Exists(target) && i < 1000; i++)
                            target = Path.Combine(folder, Path.GetFileNameWithoutExtension(name) + " (" + i + ")" + Path.GetExtension(name));
                        File.Copy(f, target);
                        Ares.Tags.AudioTags.WriteCredit(target, "YouTube Audio Library",
                            "YouTube Audio Library: confira na Biblioteca de áudio se a faixa pede crédito e onde ela pode ser usada.");
                        try
                        {
                            File.AppendAllText(Path.Combine(folder, "creditos.txt"),
                                name + "\t\t\tYouTube Audio Library\thttps://studio.youtube.com/" + Environment.NewLine, System.Text.Encoding.UTF8);
                        }
                        catch (Exception) { }
                        ok++;
                    }
                    catch (Exception ex)
                    {
                        error = ex.Message;
                    }
                }
                m_Info.Text = ok + T(" arquivo(s) copiado(s) para: ", " file(s) copied to: ") + folder
                    + (error != null ? "\n" + T("Erro: ", "Error: ") + error : "")
                    + "\n" + T("Atenção: as faixas do YouTube têm regras próprias (algumas pedem crédito, e o uso fora do YouTube pode ser restrito). Confira na Biblioteca de áudio.",
                        "Note: YouTube tracks have their own rules (some ask for credit, and use outside YouTube may be restricted). Check them in the Audio library.");
            }

            private void OpenFolder()
            {
                try
                {
                    string dir = DownloadFolder(Effects);
                    Directory.CreateDirectory(dir);
                    Process.Start("explorer.exe", "\"" + dir + "\"");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }

            private void DownloadSelected()
            {
                List<MediaHit> hits = SelectedHits();
                if (hits.Count == 0 || m_Busy)
                    return;
                string folder = DownloadFolder(Effects);
                m_Busy = true;
                UpdateButtons();
                m_Progress.Minimum = 0;
                m_Progress.Maximum = hits.Count;
                m_Progress.Value = 0;
                m_Progress.Visible = true;
                System.Threading.Tasks.Task.Factory.StartNew(() =>
                {
                    int ok = 0, done = 0;
                    List<string> errors = new List<string>();
                    foreach (MediaHit h in hits)
                    {
                        try
                        {
                            string path = OpenMedia.DownloadAudio(h, folder);
                            OpenMedia.SaveCredit(h, path);
                            ok++;
                        }
                        catch (Exception ex)
                        {
                            errors.Add(h.Title + ": " + ex.Message);
                        }
                        int n = ++done;
                        Ui(() => { m_Progress.Value = Math.Min(m_Progress.Maximum, n); });
                    }
                    Ui(() =>
                    {
                        m_Busy = false;
                        m_Progress.Visible = false;
                        UpdateButtons();
                        m_Info.Text = ok + T(" arquivo(s) baixado(s) para: ", " file(s) downloaded to: ") + folder
                            + (errors.Count > 0 ? "\n" + errors.Count + T(" com erro: ", " failed: ") + errors[0] : "")
                            + "\n" + T("A pasta fica na Biblioteca; você pode arrastar as músicas para um projeto. O autor e a licença estão nas tags e em creditos.txt.",
                                "The folder is in the library; you can drag the songs into a project. Author and licence are in the tags and in creditos.txt.");
                    });
                });
            }
        }
    }
}
