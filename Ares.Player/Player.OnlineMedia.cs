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
                    string dir = Path.Combine(Path.GetDirectoryName(m_CurrentProjectPath), "apolo_icons");
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

        private abstract class MediaForm : Form
        {
            protected static string T(string pt, string en)
            {
                return Player.T(pt, en);
            }

            protected void Ui(Action action)
            {
                if (IsDisposed || !IsHandleCreated)
                    return;
                try { BeginInvoke(action); } catch (InvalidOperationException) { }
            }

            protected MediaForm(string title)
            {
                Text = title;
                AutoScaleMode = AutoScaleMode.Font;
                ClientSize = new Size(920, 640);
                MinimumSize = new Size(760, 480);
                StartPosition = FormStartPosition.CenterParent;
                ShowInTaskbar = false;
                MinimizeBox = false;
                try { Icon = Ares.CommonGUI.ApoloBrand.GetIcon(32, Ares.CommonGUI.ApoloState.Playing, 0); } catch (Exception) { }
            }

            protected static void Place(Control c, int x, int y, int w, int h, AnchorStyles anchor)
            {
                c.SetBounds(x, y, w, h);
                c.Anchor = anchor;
            }

            protected const AnchorStyles TopLR = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            protected const AnchorStyles BottomL = AnchorStyles.Bottom | AnchorStyles.Left;
            protected const AnchorStyles BottomR = AnchorStyles.Bottom | AnchorStyles.Right;
            protected const AnchorStyles BottomLR = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            protected const AnchorStyles All4 = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        }

        // ---- pictures for the buttons ----

        private class ImageSearchDialog : MediaForm
        {
            private readonly TextBox m_Query = new TextBox();
            private readonly CheckBox m_Commercial = new CheckBox();
            private readonly Button m_Search = new Button();
            private readonly ListView m_List = new ListView();
            private readonly ImageList m_Images = new ImageList();
            private readonly Label m_Credit = new Label();
            private readonly Button m_Use = new Button();
            private readonly Button m_Cancel = new Button();
            private int m_Generation;

            public string TempFile;
            public MediaHit Chosen;

            public ImageSearchDialog(string title)
                : base(T("Sugerir imagens para o botão", "Suggest pictures for the button"))
            {
                Label l = new Label();
                l.Text = T("Buscar:", "Search:");
                Place(l, 12, 16, 60, 20, AnchorStyles.Top | AnchorStyles.Left);
                Controls.Add(l);
                Place(m_Query, 76, 12, 560, 24, TopLR);
                m_Query.Text = title;
                m_Query.KeyDown += (s, e) =>
                {
                    if (e.KeyCode == System.Windows.Forms.Keys.Enter)
                    {
                        e.SuppressKeyPress = true;
                        RunSearch();
                    }
                };
                Controls.Add(m_Query);
                Place(m_Search, 646, 10, 120, 28, AnchorStyles.Top | AnchorStyles.Right);
                m_Search.Text = T("Buscar", "Search");
                m_Search.Click += (s, e) => RunSearch();
                Controls.Add(m_Search);
                Place(m_Commercial, 12, 42, 890, 22, TopLR);
                m_Commercial.Text = T("Só imagens que podem ser usadas em lives e vídeos monetizados (uso comercial)", "Only pictures that may be used in streams and monetized videos (commercial use)");
                m_Commercial.Checked = true;
                Controls.Add(m_Commercial);

                m_Images.ImageSize = new Size(160, 120);
                m_Images.ColorDepth = ColorDepth.Depth32Bit;
                m_List.View = View.LargeIcon;
                m_List.LargeImageList = m_Images;
                m_List.MultiSelect = false;
                m_List.HideSelection = false;
                Place(m_List, 12, 70, 896, 456, All4);
                m_List.SelectedIndexChanged += (s, e) => ShowCredit();
                m_List.DoubleClick += (s, e) => UseSelected();
                Controls.Add(m_List);

                Place(m_Credit, 12, 532, 896, 40, BottomLR);
                m_Credit.Text = T("Dica: se o título estiver em português e vierem poucas imagens, tente em inglês (ex.: \"forest\").",
                    "Tip: if few pictures come up, try other words.");
                Controls.Add(m_Credit);

                Place(m_Use, 12, 596, 170, 30, BottomL);
                m_Use.Text = T("Usar esta imagem", "Use this picture");
                m_Use.Enabled = false;
                m_Use.Click += (s, e) => UseSelected();
                Controls.Add(m_Use);
                Place(m_Cancel, 808, 596, 100, 30, BottomR);
                m_Cancel.Text = T("Cancelar", "Cancel");
                m_Cancel.DialogResult = DialogResult.Cancel;
                Controls.Add(m_Cancel);
                CancelButton = m_Cancel;

                Shown += (s, e) => RunSearch();
            }

            private void ShowCredit()
            {
                MediaHit h = SelectedHit();
                m_Use.Enabled = h != null;
                if (h == null)
                    return;
                m_Credit.Text = h.Credit + "  [" + h.Source + "]";
            }

            private MediaHit SelectedHit()
            {
                if (m_List.SelectedItems.Count == 0)
                    return null;
                return m_List.SelectedItems[0].Tag as MediaHit;
            }

            private static Bitmap Fit(byte[] data, int w, int h)
            {
                using (MemoryStream ms = new MemoryStream(data))
                using (Image img = Image.FromStream(ms))
                {
                    Bitmap bmp = new Bitmap(w, h);
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.Clear(Color.Transparent);
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        float scale = Math.Min((float)w / img.Width, (float)h / img.Height);
                        int dw = Math.Max(1, (int)(img.Width * scale));
                        int dh = Math.Max(1, (int)(img.Height * scale));
                        g.DrawImage(img, (w - dw) / 2, (h - dh) / 2, dw, dh);
                    }
                    return bmp;
                }
            }

            private void RunSearch()
            {
                string query = m_Query.Text.Trim();
                if (query.Length == 0)
                    return;
                int gen = ++m_Generation;
                bool commercial = m_Commercial.Checked;
                m_List.Items.Clear();
                m_Images.Images.Clear();
                m_Use.Enabled = false;
                m_Credit.Text = T("Buscando...", "Searching...");
                m_Search.Enabled = false;
                System.Threading.Tasks.Task.Factory.StartNew(() =>
                {
                    string problems;
                    List<MediaHit> hits = OpenMedia.SearchImages(query, commercial, out problems);
                    int shown = 0;
                    foreach (MediaHit h in hits)
                    {
                        if (gen != m_Generation)
                            return;
                        Bitmap bmp = null;
                        try { bmp = Fit(OpenMedia.GetBytes(h.ThumbUrl), 160, 120); }
                        catch (Exception) { }
                        if (bmp == null)
                            continue;
                        shown++;
                        MediaHit hit = h;
                        Bitmap b = bmp;
                        Ui(() =>
                        {
                            if (gen != m_Generation)
                            {
                                b.Dispose();
                                return;
                            }
                            m_Images.Images.Add(b);
                            b.Dispose();
                            ListViewItem item = new ListViewItem(hit.Title.Length > 22 ? hit.Title.Substring(0, 21) + "…" : hit.Title);
                            item.ImageIndex = m_Images.Images.Count - 1;
                            item.Tag = hit;
                            item.ToolTipText = hit.Credit;
                            m_List.Items.Add(item);
                        });
                    }
                    Ui(() =>
                    {
                        if (gen != m_Generation)
                            return;
                        m_Search.Enabled = true;
                        if (shown == 0)
                            m_Credit.Text = problems.Length > 0
                                ? T("Não consegui buscar: ", "Could not search: ") + problems
                                : T("Nenhuma imagem encontrada. Tente outras palavras.", "No picture found. Try other words.");
                        else
                            m_Credit.Text = shown + T(" imagens. Clique numa para ver o autor e a licença; duplo clique para usar.",
                                " pictures. Click one to see the author and licence; double click to use it.");
                    });
                });
            }

            private void UseSelected()
            {
                MediaHit h = SelectedHit();
                if (h == null)
                    return;
                Cursor old = Cursor;
                Cursor = Cursors.WaitCursor;
                try
                {
                    string temp = Path.Combine(Path.GetTempPath(), "apolo-img-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".img");
                    OpenMedia.DownloadFile(h.DownloadUrl, temp);
                    TempFile = temp;
                    Chosen = h;
                    DialogResult = DialogResult.OK;
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                finally
                {
                    Cursor = old;
                }
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
            private readonly Button m_Close = new Button();
            private bool m_Busy;

            public FreeAudioDialog()
                : base(T("Música e efeitos sonoros livres", "Free music and sound effects"))
            {
                Label l = new Label();
                l.Text = T("Buscar:", "Search:");
                Place(l, 12, 16, 60, 20, AnchorStyles.Top | AnchorStyles.Left);
                Controls.Add(l);
                Place(m_Query, 76, 12, 400, 24, TopLR);
                m_Query.KeyDown += (s, e) =>
                {
                    if (e.KeyCode == System.Windows.Forms.Keys.Enter)
                    {
                        e.SuppressKeyPress = true;
                        RunSearch();
                    }
                };
                Controls.Add(m_Query);
                Place(m_Kind, 486, 12, 180, 24, AnchorStyles.Top | AnchorStyles.Right);
                m_Kind.DropDownStyle = ComboBoxStyle.DropDownList;
                m_Kind.Items.Add(T("Música", "Music"));
                m_Kind.Items.Add(T("Efeitos sonoros", "Sound effects"));
                m_Kind.SelectedIndex = 0;
                Controls.Add(m_Kind);
                Place(m_Search, 676, 10, 110, 28, AnchorStyles.Top | AnchorStyles.Right);
                m_Search.Text = T("Buscar", "Search");
                m_Search.Click += (s, e) => RunSearch();
                Controls.Add(m_Search);
                Place(m_Key, 792, 10, 116, 28, AnchorStyles.Top | AnchorStyles.Right);
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
                Place(m_List, 12, 70, 896, 430, All4);
                m_List.SelectedIndexChanged += (s, e) => UpdateButtons();
                Controls.Add(m_List);

                Place(m_Info, 12, 506, 896, 56, BottomLR);
                Controls.Add(m_Info);
                Place(m_Progress, 12, 566, 896, 8, BottomLR);
                m_Progress.Visible = false;
                Controls.Add(m_Progress);

                Place(m_Page, 12, 596, 190, 30, BottomL);
                m_Page.Text = T("Ouvir / ver a página", "Listen / see the page");
                m_Page.Click += (s, e) => OpenPage();
                Controls.Add(m_Page);
                Place(m_Download, 210, 596, 190, 30, BottomL);
                m_Download.Text = T("Baixar para a Biblioteca", "Download to the library");
                m_Download.Click += (s, e) => DownloadSelected();
                Controls.Add(m_Download);
                Place(m_Folder, 408, 596, 190, 30, BottomL);
                m_Folder.Text = T("Abrir a pasta dos downloads", "Open the downloads folder");
                m_Folder.Click += (s, e) => OpenFolder();
                Controls.Add(m_Folder);
                Place(m_Close, 808, 596, 100, 30, BottomR);
                m_Close.Text = T("Fechar", "Close");
                m_Close.Click += (s, e) => Close();
                Controls.Add(m_Close);

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
                        + "Sem a chave, os efeitos vêm só do Openverse.",
                        "Freesound (great for sound effects) asks for a free key. Create yours at freesound.org/apiv2/apply and paste it here. "
                        + "Without it, effects come only from Openverse.");
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
                                + (effects && OpenMedia.FreesoundKey.Length == 0 ? "  " + T("Para mais efeitos, use o botão \"Chave Freesound\".", "For more effects, use the \"Freesound key\" button.") : "");
                    });
                });
            }

            private void OpenPage()
            {
                foreach (MediaHit h in SelectedHits())
                {
                    if (h.PageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        try { Process.Start(h.PageUrl); } catch (Exception) { }
                    }
                    break;
                }
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
