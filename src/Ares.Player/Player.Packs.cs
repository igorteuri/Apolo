/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 Content packs: ready-made projects (folders, buttons, pictures and free music, packed as .apkg) that are not inside the
 installer but are downloaded when the user asks. The list of the packs is a small text file (pacotes.ini) published in the
 releases of the repository; see PackCatalog.cs for its format. Also here: the window with the credits of the project.
*/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text;
using System.Windows.Forms;

namespace Ares.Player
{
    public partial class Player
    {
        // the release "pacotes" of the repository holds pacotes.ini and the .apkg files (see docs/PACOTES.md)
        private const string PacksCatalogUrl = "https://github.com/igorteuri/Apolo/releases/download/pacotes/pacotes.ini";

        private void InitPacks()
        {
            ToolStripMenuItem packs = new ToolStripMenuItem(T("Pacotes de conteúdo prontos...", "Ready-made content packs..."));
            packs.Click += (s, e) => ShowPacksDialog();
            ToolStripMenuItem credits = new ToolStripMenuItem(T("Créditos do projeto...", "Project credits..."));
            credits.Click += (s, e) => ShowProjectCredits();
            int pos = projectToolStripMenuItem.DropDownItems.IndexOf(importToolStripMenuItem) + 1;
            projectToolStripMenuItem.DropDownItems.Insert(pos, packs);
            projectToolStripMenuItem.DropDownItems.Insert(pos + 1, credits);
            Shown += (s, e) =>
            {
                // after the tutorial the first-time offer is made by ShowTutorial; here it is for when the tutorial was seen already
                if (Ares.CommonGUI.Preferences.GetBool("TutorialSeen", false))
                    BeginInvoke((MethodInvoker)(() => OfferPacksFirstTime()));
            };
        }

        // once, for someone who has no project yet: offers to look at the packs
        private void OfferPacksFirstTime()
        {
            if (Ares.CommonGUI.Preferences.GetBool("PacksOffered", false))
                return;
            if (Ares.Settings.Settings.Instance.RecentFiles.GetFiles().Count > 0)
                return;
            Ares.CommonGUI.Preferences.SetBool("PacksOffered", true);
            if (MessageBox.Show(this,
                T("Quer ver os pacotes prontos (pastas, botões e músicas livres) para começar mais rápido?\n\nEles são baixados da internet só se você escolher. Dá para abrir depois em Projeto > Pacotes de conteúdo prontos.",
                  "Do you want to see the ready-made packs (folders, buttons and free music) to start faster?\n\nThey are downloaded from the internet only if you choose. You can open this later in Project > Ready-made content packs."),
                StringResources.Ares, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                ShowPacksDialog();
        }

        private static void UseTls()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }

        private void ShowPacksDialog()
        {
            UseTls();
            List<PackInfo> packs = new List<PackInfo>();
            bool busy = false;
            using (Form form = new Form())
            {
                form.Text = T("Pacotes de conteúdo prontos", "Ready-made content packs");
                form.StartPosition = FormStartPosition.CenterParent;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.ShowInTaskbar = false;
                form.ClientSize = new Size(560, 400);

                Label intro = new Label();
                intro.SetBounds(16, 12, 528, 36);
                intro.Text = T("Escolha um pacote para baixar. Ele vira um projeto novo, com as pastas, os botões, as imagens e as músicas.",
                    "Choose a pack to download. It becomes a new project, with the folders, buttons, pictures and music.");
                form.Controls.Add(intro);

                ListBox list = new ListBox();
                list.SetBounds(16, 54, 528, 130);
                form.Controls.Add(list);

                TextBox info = new TextBox();
                info.Multiline = true;
                info.ReadOnly = true;
                info.ScrollBars = ScrollBars.Vertical;
                info.SetBounds(16, 194, 528, 100);
                form.Controls.Add(info);

                Label status = new Label();
                status.SetBounds(16, 304, 528, 20);
                status.Text = T("Procurando os pacotes...", "Looking for the packs...");
                form.Controls.Add(status);

                ProgressBar bar = new ProgressBar();
                bar.SetBounds(16, 328, 528, 18);
                form.Controls.Add(bar);

                Button download = new Button();
                download.Text = T("Baixar e abrir", "Download and open");
                download.SetBounds(16, 358, 150, 30);
                download.Enabled = false;
                form.Controls.Add(download);

                Button close = new Button();
                close.Text = T("Fechar", "Close");
                close.SetBounds(464, 358, 80, 30);
                close.DialogResult = DialogResult.Cancel;
                form.Controls.Add(close);
                form.CancelButton = close;

                list.SelectedIndexChanged += (s, e) =>
                {
                    PackInfo p = list.SelectedIndex >= 0 && list.SelectedIndex < packs.Count ? packs[list.SelectedIndex] : null;
                    info.Text = p == null ? "" : p.Description.Replace("\\n", "\r\n");
                    download.Enabled = p != null && !busy;
                };

                WebClient catalog = new WebClient();
                catalog.Encoding = Encoding.UTF8;
                catalog.DownloadStringCompleted += (s, e) =>
                {
                    if (form.IsDisposed)
                        return;
                    if (e.Cancelled)
                        return;
                    if (e.Error != null)
                    {
                        status.Text = T("Não consegui buscar a lista de pacotes. Confira a internet e tente de novo. (", "Could not get the list of packs. Check the internet and try again. (")
                            + e.Error.Message + ")";
                        return;
                    }
                    packs.AddRange(PackCatalog.Parse(e.Result, PacksCatalogUrl));
                    foreach (PackInfo p in packs)
                    {
                        string size = PackCatalog.FormatSize(p.Bytes);
                        list.Items.Add(size.Length > 0 ? p.Title + "   (" + size + ")" : p.Title);
                    }
                    status.Text = packs.Count > 0 ? T("Escolha um pacote da lista.", "Choose a pack from the list.")
                        : T("Nenhum pacote disponível por enquanto.", "No packs available yet.");
                    if (packs.Count > 0)
                        list.SelectedIndex = 0;
                };

                download.Click += (s, e) =>
                {
                    PackInfo p = list.SelectedIndex >= 0 && list.SelectedIndex < packs.Count ? packs[list.SelectedIndex] : null;
                    if (p == null || busy)
                        return;
                    busy = true;
                    download.Enabled = false;
                    list.Enabled = false;
                    string temp = Path.Combine(Path.GetTempPath(), "apolo-pacote-" + PackCatalog.SafeName(p.Id) + ".apkg.part");
                    WebClient client = new WebClient();
                    client.DownloadProgressChanged += (s2, e2) =>
                    {
                        if (form.IsDisposed)
                            return;
                        bar.Value = Math.Max(0, Math.Min(100, e2.ProgressPercentage));
                        status.Text = T("Baixando... ", "Downloading... ") + e2.ProgressPercentage + "%";
                    };
                    client.DownloadFileCompleted += (s2, e2) =>
                    {
                        busy = false;
                        if (form.IsDisposed)
                        {
                            TryDelete(temp);
                            return;
                        }
                        list.Enabled = true;
                        download.Enabled = true;
                        if (e2.Cancelled || e2.Error != null)
                        {
                            TryDelete(temp);
                            status.Text = T("O download não terminou. Tente de novo. ", "The download did not finish. Try again. ") + (e2.Error != null ? e2.Error.Message : "");
                            return;
                        }
                        string problem = CheckDownloadedPack(temp, p);
                        if (problem != null)
                        {
                            TryDelete(temp);
                            status.Text = problem;
                            return;
                        }
                        form.DialogResult = DialogResult.OK;
                        string file = temp;
                        BeginInvoke((MethodInvoker)(() => ImportDownloadedPack(file, p)));
                    };
                    status.Text = T("Baixando...", "Downloading...");
                    bar.Value = 0;
                    try
                    {
                        client.DownloadFileAsync(new Uri(p.Url), temp);
                    }
                    catch (Exception ex)
                    {
                        busy = false;
                        list.Enabled = true;
                        download.Enabled = true;
                        status.Text = ex.Message;
                    }
                };

                form.FormClosing += (s, e) =>
                {
                    if (busy)
                    {
                        e.Cancel = true; // the download is still running
                        MessageBox.Show(form, T("Espere o download terminar.", "Wait for the download to finish."), StringResources.Ares,
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        catalog.CancelAsync();
                    }
                };

                Ares.CommonGUI.ThemeManager.Apply(form);
                try { catalog.DownloadStringAsync(new Uri(PacksCatalogUrl)); }
                catch (Exception ex) { status.Text = ex.Message; }
                form.ShowDialog(this);
            }
        }

        private static void TryDelete(string file)
        {
            try { if (File.Exists(file)) File.Delete(file); } catch (Exception swallowed) { Ares.Settings.ErrorLog.Note(swallowed); }
        }

        // null when the download looks right, otherwise the message for the user
        private static string CheckDownloadedPack(string file, PackInfo pack)
        {
            try
            {
                long length = new FileInfo(file).Length;
                if (length < 1024)
                    return T("O arquivo baixado está incompleto.", "The downloaded file is incomplete.");
                if (pack.Bytes > 0 && length != pack.Bytes)
                    return T("O arquivo baixado não tem o tamanho esperado. Tente de novo.", "The downloaded file does not have the expected size. Try again.");
                if (pack.Sha256.Length > 0)
                {
                    using (FileStream fs = File.OpenRead(file))
                    using (System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create())
                    {
                        StringBuilder hex = new StringBuilder();
                        foreach (byte b in sha.ComputeHash(fs))
                            hex.Append(b.ToString("x2"));
                        if (!string.Equals(hex.ToString(), pack.Sha256, StringComparison.OrdinalIgnoreCase))
                            return T("O arquivo baixado não confere com o esperado (assinatura diferente). Tente de novo.", "The downloaded file does not match what is expected (different signature). Try again.");
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private static string FreeProjectFile(string folder, string baseName)
        {
            string path = Path.Combine(folder, baseName + Ares.Settings.ProjectFiles.Extension);
            for (int i = 2; File.Exists(path); i++)
                path = Path.Combine(folder, baseName + "-" + i + Ares.Settings.ProjectFiles.Extension);
            return path;
        }

        private void ImportDownloadedPack(string packFile, PackInfo pack)
        {
            string folder = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Apolo"), "Pacotes");
            string target;
            try
            {
                Directory.CreateDirectory(folder);
                target = FreeProjectFile(folder, PackCatalog.SafeName(pack.Id));
            }
            catch (Exception ex)
            {
                TryDelete(packFile);
                MessageBox.Show(this, ex.Message, StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            StopAllPlaying();
            Ares.CommonGUI.ProgressMonitor monitor = new Ares.CommonGUI.ProgressMonitor(this, StringResources.Importing);
            Ares.ModelInfo.Importer.Import(monitor, packFile, target, true, new MessageBoxProvider(), (error, cancelled) =>
            {
                monitor.Close();
                TryDelete(packFile);
                if (error != null)
                {
                    MessageBox.Show(String.Format(StringResources.ImportError, error.Message), StringResources.Ares,
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else if (!cancelled)
                {
                    OpenProject(target, false);
                    ShowToast(new Ares.Players.Message(Ares.Players.MessageType.Info,
                        T("Pacote aberto: ", "Pack opened: ") + pack.Title + T(". Os créditos estão em Projeto > Créditos do projeto.", ". The credits are in Project > Project credits.")));
                }
            });
        }

        // ---- credits of the project ----

        private void ShowProjectCredits()
        {
            if (m_Project == null)
            {
                MessageBox.Show(this, T("Abra um projeto primeiro.", "Open a project first."), StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string creditsFile = ProjectCredits.FileFor(m_Project.FileName);
            using (Form form = new Form())
            {
                form.Text = T("Créditos do projeto", "Project credits");
                form.StartPosition = FormStartPosition.CenterParent;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.ShowInTaskbar = false;
                form.ClientSize = new Size(620, 440);

                Label intro = new Label();
                intro.SetBounds(16, 12, 588, 52);
                intro.Text = T("Autores e licenças das músicas deste projeto. Este texto vai junto quando você exporta o projeto completo (.apkg). A parte de cima é escrita por você; a lista de sons vem das informações gravadas nos arquivos de áudio.",
                    "Authors and licences of the music of this project. This text goes along when you export the complete project (.apkg). The top part is written by you; the list of sounds comes from the information stored in the audio files.");
                form.Controls.Add(intro);

                TextBox text = new TextBox();
                text.Multiline = true;
                text.ReadOnly = true;
                text.ScrollBars = ScrollBars.Vertical;
                text.SetBounds(16, 70, 588, 310);
                form.Controls.Add(text);

                Action refresh = () =>
                {
                    string credits = BuildCreditsTextWithProgress();
                    text.Text = credits.Length > 0 ? credits
                        : T("Ainda não há créditos. Use \"Escrever créditos\" para dizer quem fez as músicas e sob qual licença.",
                            "There are no credits yet. Use \"Write credits\" to say who made the music and under which licence.");
                    text.Select(0, 0);
                };
                refresh();

                Button edit = new Button();
                edit.Text = T("Escrever créditos", "Write credits");
                edit.SetBounds(16, 394, 150, 30);
                edit.Click += (s, e) =>
                {
                    try
                    {
                        if (!File.Exists(creditsFile))
                            File.WriteAllText(creditsFile, ProjectCredits.Template(), new UTF8Encoding(true));
                        Process.Start(new ProcessStartInfo(creditsFile) { UseShellExecute = true });
                        MessageBox.Show(form, T("Edite o arquivo que abriu, salve, e clique em OK aqui para atualizar.", "Edit the file that opened, save it, and click OK here to refresh."),
                            StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Information);
                        refresh();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(form, ex.Message, StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                };
                form.Controls.Add(edit);

                Button close = new Button();
                close.Text = T("Fechar", "Close");
                close.SetBounds(524, 394, 80, 30);
                close.DialogResult = DialogResult.Cancel;
                form.Controls.Add(close);
                form.CancelButton = close;

                Ares.CommonGUI.ThemeManager.Apply(form);
                form.ShowDialog(this);
            }
        }
    }
}
