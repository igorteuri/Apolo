/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 Export of the project in three ways (only the list, with the audio files, only the settings),
 import of a settings backup and the association of the .apolo files with Apolo.
*/
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Ares.Player
{
    public partial class Player
    {
        private void InitExportMenu()
        {
            ToolStripMenuItem export = new ToolStripMenuItem(T("Exportar...", "Export..."));
            export.Click += (s, e) => ExportDialogAndRun();
            ToolStripMenuItem restore = new ToolStripMenuItem(T("Importar backup das configurações...", "Import settings backup..."));
            restore.Click += (s, e) => RestoreSettingsBackup();
            int pos = projectToolStripMenuItem.DropDownItems.IndexOf(importToolStripMenuItem) + 1;
            projectToolStripMenuItem.DropDownItems.Insert(pos, export);
            projectToolStripMenuItem.DropDownItems.Insert(pos + 1, restore);
            EnsureFileAssociation();
        }

        // When nothing opens .apolo files yet (or what did has been deleted or moved), Apolo registers itself for the
        // current user, so that exported projects show the Apolo icon. Another working installation is left alone.
        private static void EnsureFileAssociation()
        {
            try
            {
                string exe = Application.ExecutablePath;
                string handler = null;
                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(".apolo"))
                {
                    if (key != null)
                        handler = key.GetValue("") as string;
                }
                if (!string.IsNullOrEmpty(handler) && handler != "ApoloProject")
                    return; // another program has been chosen for these files
                string icon = null;
                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(@"ApoloProject\DefaultIcon"))
                {
                    if (key != null)
                        icon = key.GetValue("") as string;
                }
                if (!string.IsNullOrEmpty(icon))
                {
                    string path = icon.Trim().Trim('"');
                    int comma = path.LastIndexOf(',');
                    if (comma > 0 && !File.Exists(path))
                        path = path.Substring(0, comma).Trim('"');
                    if (File.Exists(path))
                        return; // an installation of Apolo is registered and still there
                }
                RegisterFileTypes(exe);
            }
            catch (Exception swallowed)
            {
                Ares.Settings.ErrorLog.Note(swallowed);
            }
        }

        private enum ExportKind
        {
            ListOnly,
            WithAudio,
            SettingsBackup
        }

        private void ExportDialogAndRun()
        {
            ExportKind kind;
            if (!AskExportKind(out kind))
                return;
            switch (kind)
            {
                case ExportKind.ListOnly:
                    ExportListOnly();
                    break;
                case ExportKind.WithAudio:
                    ExportWithAudio();
                    break;
                default:
                    ExportSettingsBackup();
                    break;
            }
        }

        private bool AskExportKind(out ExportKind kind)
        {
            kind = ExportKind.ListOnly;
            using (Form dlg = new Form())
            {
                dlg.Text = T("Exportar", "Export");
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;
                dlg.ShowInTaskbar = false;
                dlg.AutoSize = true;
                dlg.AutoSizeMode = AutoSizeMode.GrowAndShrink;
                dlg.Padding = new Padding(12);
                FlowLayoutPanel flow = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Dock = DockStyle.Fill };
                dlg.Controls.Add(flow);
                flow.Controls.Add(new Label { AutoSize = true, Margin = new Padding(0, 0, 0, 8), Text = T("O que você quer exportar?", "What do you want to export?") });
                bool hasProject = m_Project != null;
                RadioButton list = AddExportChoice(flow, T("Só a lista de sons (projeto, sem as músicas)", "Only the list of sounds (project, without the music files)"),
                    T("Um arquivo .apolo pequeno. Quem abrir precisa ter os mesmos arquivos de áudio no computador.",
                      "A small .apolo file. Whoever opens it needs the same audio files on the computer."), hasProject);
                RadioButton audio = AddExportChoice(flow, T("Projeto completo, com as músicas e efeitos", "Complete project, with the music and sound effects"),
                    T("Um arquivo .apkg com a lista e todos os áudios, para compartilhar. Pode ficar grande.",
                      "An .apkg file with the list and all audio files, for sharing. It can get big."), hasProject);
                RadioButton settings = AddExportChoice(flow, T("Só o backup das configurações", "Only a backup of the settings"),
                    T("Aparência, atalhos e preferências do Apolo num arquivo .zip (sem projetos nem áudios).",
                      "Appearance, shortcuts and preferences of Apolo in a .zip file (no projects, no audio)."), true);
                (hasProject ? list : settings).Checked = true;
                FlowLayoutPanel buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 0) };
                Button ok = new Button { Text = T("Exportar", "Export"), DialogResult = DialogResult.OK, AutoSize = true };
                Button cancel = new Button { Text = T("Cancelar", "Cancel"), DialogResult = DialogResult.Cancel, AutoSize = true };
                buttons.Controls.Add(cancel);
                buttons.Controls.Add(ok);
                flow.Controls.Add(buttons);
                dlg.AcceptButton = ok;
                dlg.CancelButton = cancel;
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return false;
                kind = list.Checked ? ExportKind.ListOnly : (audio.Checked ? ExportKind.WithAudio : ExportKind.SettingsBackup);
                return true;
            }
        }

        private static RadioButton AddExportChoice(FlowLayoutPanel flow, string title, string description, bool enabled)
        {
            RadioButton radio = new RadioButton { AutoSize = true, Text = title, Enabled = enabled, Margin = new Padding(0, 4, 0, 0) };
            Label label = new Label
            {
                AutoSize = false,
                Width = 440,
                Height = 34,
                Text = description,
                Enabled = enabled,
                Margin = new Padding(20, 0, 0, 4),
                ForeColor = SystemColors.GrayText
            };
            flow.Controls.Add(radio);
            flow.Controls.Add(label);
            return radio;
        }

        private string ProjectBaseName()
        {
            string name = m_Project != null ? m_Project.Title : "";
            if (string.IsNullOrEmpty(name) && m_Project != null && !string.IsNullOrEmpty(m_Project.FileName))
                name = Path.GetFileNameWithoutExtension(m_Project.FileName);
            if (string.IsNullOrEmpty(name))
                name = "projeto";
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        private void ExportListOnly()
        {
            if (m_Project == null)
                return;
            using (SaveFileDialog dlg = new SaveFileDialog())
            {
                dlg.Filter = T("Projetos do Apolo|*.apolo|Todos os arquivos|*.*", "Apolo projects|*.apolo|All files|*.*");
                dlg.DefaultExt = "apolo";
                dlg.FileName = ProjectBaseName() + ".apolo";
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    SaveProjectNow();
                    string source = m_Project.FileName;
                    if (!string.Equals(Path.GetFullPath(source), Path.GetFullPath(dlg.FileName), StringComparison.OrdinalIgnoreCase))
                    {
                        File.Copy(source, dlg.FileName, true);
                        Ares.CommonGUI.ButtonImageStore.CopyTo(source, dlg.FileName);
                    }
                    ShowToast(new Ares.Players.Message(Ares.Players.MessageType.Info,
                        T("Lista exportada: ", "List exported: ") + Path.GetFileName(dlg.FileName)));
                }
                catch (Exception ex)
                {
                    Ares.CommonGUI.MessageDialog.Show(this, T("Erro ao exportar: ", "Export error: ") + ex.Message, StringResources.Ares,
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ExportWithAudio()
        {
            if (m_Project == null)
                return;
            using (SaveFileDialog dlg = new SaveFileDialog())
            {
                dlg.Filter = T("Projetos compactados do Apolo|*.apkg|Todos os arquivos|*.*", "Apolo packed projects|*.apkg|All files|*.*");
                dlg.DefaultExt = "apkg";
                dlg.FileName = ProjectBaseName() + ".apkg";
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    SaveProjectNow();
                    string target = dlg.FileName;
                    Ares.CommonGUI.ProgressMonitor monitor = new Ares.CommonGUI.ProgressMonitor(this, T("Exportando...", "Exporting..."));
                    // the pictures, icons and emojis of the buttons go into the package too
                    Dictionary<string, string> extras = new Dictionary<string, string>();
                    string iconsTempFile = null;
                    try
                    {
                        string sidecar = Ares.CommonGUI.ButtonImageStore.BuildPortable(m_Project.FileName, extras);
                        if (!string.IsNullOrEmpty(sidecar))
                        {
                            iconsTempFile = Path.Combine(Path.GetTempPath(), "apolo-icons-" + Guid.NewGuid().ToString("N") + ".txt");
                            File.WriteAllText(iconsTempFile, sidecar, new System.Text.UTF8Encoding(false));
                            extras["Icons/icons.txt"] = iconsTempFile;
                        }
                    }
                    catch (Exception)
                    {
                        extras.Clear();
                    }
                    // the nesting of the folders and the credits (authors and licences) travel in the package too
                    string creditsTempFile = null;
                    try
                    {
                        string treeFile = ModeTree.FileFor(m_Project.FileName);
                        if (File.Exists(treeFile))
                            extras[Ares.ModelInfo.Exporter.PROJECT_DIR + "/pastas.json"] = treeFile;
                        string credits = BuildCreditsText();
                        if (credits.Length > 0)
                        {
                            creditsTempFile = Path.Combine(Path.GetTempPath(), "apolo-credits-" + Guid.NewGuid().ToString("N") + ".txt");
                            File.WriteAllText(creditsTempFile, credits, new System.Text.UTF8Encoding(false));
                            extras[Ares.ModelInfo.Exporter.PROJECT_DIR + "/creditos.txt"] = creditsTempFile;
                        }
                    }
                    catch (Exception swallowed)
                    {
                        Ares.Settings.ErrorLog.Note(swallowed);
                    }
                    Ares.ModelInfo.Exporter.Export(monitor, m_Project, m_Project.FileName, target, error =>
                    {
                        monitor.Close();
                        if (iconsTempFile != null)
                        {
                            try { File.Delete(iconsTempFile); } catch (Exception swallowed) { Ares.Settings.ErrorLog.Note(swallowed); }
                        }
                        if (creditsTempFile != null)
                        {
                            try { File.Delete(creditsTempFile); } catch (Exception swallowed) { Ares.Settings.ErrorLog.Note(swallowed); }
                        }
                        if (error != null)
                        {
                            Ares.CommonGUI.MessageDialog.Show(T("Erro ao exportar: ", "Export error: ") + error.Message, StringResources.Ares,
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        else
                        {
                            ShowToast(new Ares.Players.Message(Ares.Players.MessageType.Info,
                                T("Projeto completo exportado: ", "Complete project exported: ") + Path.GetFileName(target)));
                        }
                    }, extras);
                }
                catch (Exception ex)
                {
                    Ares.CommonGUI.MessageDialog.Show(this, T("Erro ao exportar: ", "Export error: ") + ex.Message, StringResources.Ares,
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // the credits of the project: the hand-written file beside it plus what the tags of its sounds say
        private string BuildCreditsText()
        {
            return BuildCreditsText(null);
        }

        // for a big project reading the tags of thousands of files takes a while: shows a small window with the progress
        private string BuildCreditsTextWithProgress()
        {
            System.Threading.Tasks.Task<string> work = null;
            Ares.CommonGUI.ProgressDialog dialog = null;
            int total = 0;
            work = System.Threading.Tasks.Task.Run(() => BuildCreditsText((done, all) =>
            {
                total = all;
                Ares.CommonGUI.ProgressDialog d = dialog;
                if (d != null && d.IsHandleCreated && all > 0 && (done % 10 == 0 || done == all))
                    d.BeginInvoke(new Action(() =>
                    {
                        if (!d.IsDisposed)
                            d.SetProgress(Math.Min(100, done * 100 / all), T("Lendo as informações dos arquivos: ", "Reading the information of the files: ") + done + " / " + all);
                    }));
            }));
            if (work.Wait(400))
                return work.Result;
            dialog = new Ares.CommonGUI.ProgressDialog(T("Lendo as informações dos arquivos de áudio...", "Reading the information of the audio files..."));
            dialog.StartPosition = FormStartPosition.CenterParent;
            dialog.ShowInTaskbar = false;
            dialog.ControlBox = false;
            work.ContinueWith(t => { if (!dialog.IsDisposed && dialog.IsHandleCreated) dialog.BeginInvoke(new Action(dialog.Close)); });
            dialog.Shown += (s, e) => { if (work.IsCompleted) dialog.Close(); };
            Ares.CommonGUI.ThemeManager.Apply(dialog);
            dialog.ShowDialog(this);
            dialog.Dispose();
            return work.Result;
        }

        private string BuildCreditsText(Action<int, int> progress)
        {
            string manual = "";
            try
            {
                string file = ProjectCredits.FileFor(m_Project.FileName);
                if (File.Exists(file))
                    manual = File.ReadAllText(file, System.Text.Encoding.UTF8);
            }
            catch (Exception swallowed)
            {
                Ares.Settings.ErrorLog.Note(swallowed);
            }
            List<CreditEntry> entries = new List<CreditEntry>();
            List<Ares.Data.IFileElement> files = new List<Ares.Data.IFileElement>(new Ares.ModelInfo.FileLists().GetAllFiles(m_Project));
            int index = 0;
            foreach (Ares.Data.IFileElement element in files)
            {
                ++index;
                if (progress != null)
                    progress(index, files.Count);
                try
                {
                    string dir = element.SoundFileType == Ares.Data.SoundFileType.Music ?
                        Ares.Settings.Settings.Instance.MusicDirectory : Ares.Settings.Settings.Instance.SoundDirectory;
                    string path = Path.Combine(dir, element.FilePath);
                    if (!File.Exists(path))
                        continue;
                    Ares.Tags.AudioTagInfo tag = Ares.Tags.AudioTags.Read(path);
                    if (tag == null)
                        continue;
                    entries.Add(new CreditEntry { File = element.FilePath, Title = tag.title, Artist = tag.artist, Copyright = tag.copyright, Comment = tag.comment });
                }
                catch (Exception swallowed)
                {
                    Ares.Settings.ErrorLog.Note(swallowed);
                }
            }
            return ProjectCredits.Build(manual, entries);
        }

        // ---- backup of the settings ----

        private static bool SkipInBackup(string relativePath)
        {
            string first = relativePath.Replace('/', '\\').Split('\\')[0];
            if (string.Equals(first, "WebView2", StringComparison.OrdinalIgnoreCase))
                return true; // browser cache, rebuilt by itself
            string name = Path.GetFileName(relativePath);
            if (string.Equals(name, "cloudflared.exe", StringComparison.OrdinalIgnoreCase))
                return true; // downloaded again when needed
            if (name.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
                return true;
            return false;
        }

        private void ExportSettingsBackup()
        {
            using (SaveFileDialog dlg = new SaveFileDialog())
            {
                dlg.Filter = T("Backup do Apolo (*.zip)|*.zip|Todos os arquivos|*.*", "Apolo backup (*.zip)|*.zip|All files|*.*");
                dlg.DefaultExt = "zip";
                dlg.FileName = "apolo-configuracoes-" + DateTime.Now.ToString("yyyy-MM-dd") + ".zip";
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    string root = Ares.Settings.AppFolders.Root;
                    if (File.Exists(dlg.FileName))
                        File.Delete(dlg.FileName);
                    int count = 0;
                    using (ZipArchive zip = ZipFile.Open(dlg.FileName, ZipArchiveMode.Create))
                    {
                        ZipArchiveEntry marker = zip.CreateEntry("apolo-backup.txt");
                        using (StreamWriter w = new StreamWriter(marker.Open()))
                            w.WriteLine("Apolo settings backup " + DateTime.Now.ToString("s"));
                        foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                        {
                            string relative = file.Substring(root.Length).TrimStart('\\', '/');
                            if (SkipInBackup(relative))
                                continue;
                            try
                            {
                                using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                                {
                                    ZipArchiveEntry entry = zip.CreateEntry("settings/" + relative.Replace('\\', '/'));
                                    using (Stream output = entry.Open())
                                        fs.CopyTo(output);
                                }
                                ++count;
                            }
                            catch (IOException swallowed)
                            {
                                // a file in use is left out
                                Ares.Settings.ErrorLog.Note(swallowed);
                            }
                        }
                    }
                    ShowToast(new Ares.Players.Message(Ares.Players.MessageType.Info,
                        T("Backup das configurações salvo (" + count + " arquivos): ", "Settings backup saved (" + count + " files): ") + Path.GetFileName(dlg.FileName)));
                }
                catch (Exception ex)
                {
                    Ares.CommonGUI.MessageDialog.Show(this, T("Erro ao exportar: ", "Export error: ") + ex.Message, StringResources.Ares,
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void RestoreSettingsBackup()
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Filter = T("Backup do Apolo (*.zip)|*.zip|Todos os arquivos|*.*", "Apolo backup (*.zip)|*.zip|All files|*.*");
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    string root = Path.GetFullPath(Ares.Settings.AppFolders.Root);
                    List<ZipArchiveEntry> entries = new List<ZipArchiveEntry>();
                    using (ZipArchive zip = ZipFile.OpenRead(dlg.FileName))
                    {
                        bool valid = false;
                        foreach (ZipArchiveEntry entry in zip.Entries)
                        {
                            if (entry.FullName == "apolo-backup.txt")
                                valid = true;
                            if (entry.FullName.StartsWith("settings/", StringComparison.Ordinal) && entry.Name.Length > 0)
                                entries.Add(entry);
                        }
                        if (!valid)
                        {
                            Ares.CommonGUI.MessageDialog.Show(this, T("Este arquivo não é um backup de configurações do Apolo.", "This file is not an Apolo settings backup."),
                                StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return;
                        }
                        if (Ares.CommonGUI.MessageDialog.Show(this, T("As configurações atuais serão substituídas pelas do backup (" + entries.Count + " arquivos). O Apolo precisa ser reiniciado depois. Continuar?",
                                "The current settings will be replaced by those of the backup (" + entries.Count + " files). Apolo has to be restarted afterwards. Continue?"),
                                StringResources.Ares, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                            return;
                        foreach (ZipArchiveEntry entry in entries)
                        {
                            string relative = entry.FullName.Substring("settings/".Length).Replace('/', '\\');
                            string target = Path.GetFullPath(Path.Combine(root, relative));
                            if (!target.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase))
                                continue; // never write outside the data folder
                            Directory.CreateDirectory(Path.GetDirectoryName(target));
                            using (Stream input = entry.Open())
                            using (FileStream output = new FileStream(target, FileMode.Create, FileAccess.Write))
                                input.CopyTo(output);
                        }
                    }
                    Ares.CommonGUI.MessageDialog.Show(this, T("Configurações restauradas. Feche e abra o Apolo de novo para aplicá-las.",
                        "Settings restored. Close and open Apolo again to apply them."), StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    Ares.CommonGUI.MessageDialog.Show(this, T("Erro ao importar: ", "Import error: ") + ex.Message, StringResources.Ares,
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // ---- .apolo files open with Apolo (for the current user only) ----

        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);

        private void AssociateProjectFiles()
        {
            try
            {
                RegisterFileTypes(Application.ExecutablePath);
                ShowToast(new Ares.Players.Message(Ares.Players.MessageType.Info,
                    T("Arquivos .apolo e .apkg agora abrem com o Apolo.", ".apolo and .apkg files now open with Apolo.")));
            }
            catch (Exception ex)
            {
                Ares.CommonGUI.MessageDialog.Show(this, ex.Message, StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void RegisterFileTypes(string exe)
        {
            SetClassValue(@"Software\Classes\.apolo", "", "ApoloProject");
            SetClassValue(@"Software\Classes\.apkg", "", "ApoloPackedProject");
            foreach (string id in new[] { "ApoloProject", "ApoloPackedProject" })
            {
                SetClassValue(@"Software\Classes\" + id, "", id == "ApoloProject" ? "Apolo Project" : "Apolo Packed Project");
                SetClassValue(@"Software\Classes\" + id + @"\DefaultIcon", "", "\"" + exe + "\",0");
                SetClassValue(@"Software\Classes\" + id + @"\shell\open\command", "", "\"" + exe + "\" \"%1\"");
            }
            SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
        }

        private static void SetClassValue(string path, string name, string value)
        {
            using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(path))
            {
                key.SetValue(name, value);
            }
        }
    }
}
