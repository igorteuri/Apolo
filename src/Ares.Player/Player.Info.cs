/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 Two information windows:
   - Extras > Foundry VTT module: how to install and use Apolo inside Foundry;
   - Ajuda > update: how updates are delivered (the vault of the supporters of the channel).
*/
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Windows.Forms;

namespace Ares.Player
{
    public partial class Player
    {
        private const string SupportersUrl = "https://cutt.ly/clubecritico";
        private const string SourceUrl = "https://github.com/igorteuri/Apolo";
        // the tutorials: for now the search for the channel; later the playlist of videos about Apolo
        private const string TutorialsUrl = "https://www.youtube.com/@dadoscriticos/search?query=apolo";
        private const string VaultUrl = "https://igorteuri.itch.io/contedo-exclusivo-dados-criticos";

        private void InitInfoMenus()
        {
            ToolStripMenuItem foundry = new ToolStripMenuItem(T("Módulo do Foundry VTT...", "Foundry VTT module..."));
            foundry.Name = "foundryMenuItem";
            foundry.Click += (s, e) => ShowFoundryHelp();
            int pos = Math.Min(4, extrasToolStripMenuItem.DropDownItems.Count);
            extrasToolStripMenuItem.DropDownItems.Insert(pos, foundry);
            checkForUpdateToolStripMenuItem.Text = T("Como atualizar o Apolo...", "How to update Apolo...");

            // Projeto > Novo projeto (first entry of the menu)
            ToolStripMenuItem newProject = new ToolStripMenuItem(T("Novo projeto...", "New project..."));
            newProject.Name = "newProjectMenuItem";
            newProject.ShortcutKeys = System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.N;
            newProject.Click += (s, e) => NewProject();
            projectToolStripMenuItem.DropDownItems.Insert(0, newProject);

            // the menu "Ajuda" becomes "Sobre"
            helpToolStripMenuItem.Text = T("Sobre", "About");
            helpOnlineToolStripMenuItem.Text = T("Tutoriais (canal Dados Críticos)", "Tutorials (Dados Críticos channel)");
            aboutToolStripMenuItem.Text = T("Sobre o Apolo...", "About Apolo...");
            ToolStripMenuItem source = new ToolStripMenuItem(T("Código-fonte no GitHub", "Source code on GitHub"));
            source.Click += (s, e) => OpenAddress(SourceUrl);
            helpToolStripMenuItem.DropDownItems.Add(source);
            InitTutorial();
            InitPacks();
            InitPad();
            InitFolderZoom();
        }

        // creates an empty project (with one first folder) in a file chosen by the user and opens it
        private void NewProject()
        {
            string name = Prompt(this, T("Novo projeto", "New project"), T("Nome do projeto:", "Project name:"), "");
            if (name == null)
                return;
            name = name.Trim();
            if (name.Length == 0)
                return;
            string path;
            using (SaveFileDialog dlg = new SaveFileDialog())
            {
                dlg.Title = T("Onde guardar o novo projeto", "Where to save the new project");
                dlg.Filter = T("Projetos do Apolo|*.apolo", "Apolo projects|*.apolo");
                dlg.DefaultExt = "apolo";
                dlg.AddExtension = true;
                dlg.OverwritePrompt = true;
                string safe = name;
                foreach (char c in Path.GetInvalidFileNameChars())
                    safe = safe.Replace(c, '_');
                dlg.FileName = safe;
                dlg.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;
                path = dlg.FileName;
            }
            bool oldWatch = fileSystemWatcher1.EnableRaisingEvents;
            fileSystemWatcher1.EnableRaisingEvents = false;
            try
            {
                StopAllPlaying();
                Ares.Data.IProject created = Ares.Data.DataModule.ProjectManager.CreateProject(name);
                created.FileName = path;
                created.AddMode(T("Principal", "Main"));
                Ares.Data.DataModule.ProjectManager.SaveProject(created);
                Ares.Data.DataModule.ProjectManager.UnloadProject(created);
                OpenProject(path, false);
                ShowToast(new Ares.Players.Message(Ares.Players.MessageType.Info,
                    T("Projeto criado. Use o modo de edição para adicionar playlists.", "Project created. Use edit mode to add playlists.")));
            }
            catch (Exception ex)
            {
                Ares.CommonGUI.MessageDialog.Show(this, T("Não foi possível criar o projeto: ", "Could not create the project: ") + ex.Message,
                    StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                fileSystemWatcher1.EnableRaisingEvents = oldWatch;
            }
        }

        // window "Sobre": version, credits, links
        private void ShowAboutApolo()
        {
            string version = DisplayVersion();
            string text = T(
@"APOLO

Versão " + version + @"

Controlador de músicas para mesas de RPG, feito para tocar música e efeitos sonoros ao mesmo tempo (não vem com músicas: use as suas ou baixe pacotes prontos), com controle pelo celular, Stream Deck, Discord e Foundry VTT.

O Apolo é mantido pelo canal Dados Críticos. As novidades chegam primeiro aos Clubistas, os apoiadores do canal.

CRÉDITOS
O Apolo nasceu do Ares, criado por Jörg Ruedenauer, e continua sob a licença GNU GPL (versão 2 ou posterior). Obrigado a quem fez o Ares e a todas as bibliotecas livres usadas aqui.

CÓDIGO-FONTE
O código do Apolo é aberto e está no GitHub:
" + SourceUrl,
@"APOLO

Version " + version + @"

Music controller for tabletop RPG, made to play music and sound effects at the same time (it does not come with music: use your own or download ready-made packs), with control from a phone, Stream Deck, Discord and Foundry VTT.

Apolo is kept by the Dados Críticos channel. The news reach the Clubistas, the supporters of the channel, first.

CREDITS
Apolo was born from Ares, created by Jörg Ruedenauer, and stays under the GNU GPL licence (version 2 or later). Thanks to everyone who made Ares and to all the free libraries used here.

SOURCE CODE
The code of Apolo is open and lives on GitHub:
" + SourceUrl);
            List<KeyValuePair<string, Action>> buttons = new List<KeyValuePair<string, Action>>();
            buttons.Add(new KeyValuePair<string, Action>(T("Código-fonte no GitHub", "Source code on GitHub"), () => OpenAddress(SourceUrl)));
            buttons.Add(new KeyValuePair<string, Action>(T("Tutoriais", "Tutorials"), () => OpenAddress(TutorialsUrl)));
            buttons.Add(new KeyValuePair<string, Action>(T("Fechar", "Close"), null));
            ShowInfoWindow(T("Sobre o Apolo", "About Apolo"), text, buttons);
        }

        private void OpenAddress(string url)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Ares.CommonGUI.MessageDialog.Show(this, T("Não consegui abrir o endereço: ", "Could not open the address: ") + url + "\r\n" + ex.Message,
                    StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // a window with a text and a row of buttons (the last button closes it)
        // "0.82.140.0" (version + number of the build) as "0.82 (build 140)"
        private static string DisplayVersion()
        {
            string v = System.Windows.Forms.Application.ProductVersion;
            string[] p = v.Split('.');
            return p.Length >= 3 && p[2] != "0" ? p[0] + "." + p[1] + " (build " + p[2] + ")" : (p.Length >= 2 ? p[0] + "." + p[1] : v);
        }

        // a line which is a heading: all capitals ("O MÓDULO DO FOUNDRY VTT", "1. PREPARE O APOLO (mestre)")
        private static bool IsHeadingLine(string line)
        {
            string t = line.Trim();
            if (t.Length < 3 || t.Length > 70)
                return false;
            int letters = 0, upper = 0;
            foreach (char c in t)
            {
                if (char.IsLetter(c))
                {
                    letters++;
                    if (char.IsUpper(c))
                        upper++;
                }
            }
            return letters >= 3 && upper >= letters * 0.8;
        }

        private void ShowInfoWindow(string title, string text, List<KeyValuePair<string, Action>> buttons)
        {
            Form form = new Form();
            {
                form.Text = title;
                form.StartPosition = FormStartPosition.CenterParent;
                form.ShowInTaskbar = false;
                form.MinimizeBox = false;
                form.MaximizeBox = true;
                form.ClientSize = new Size(720, 560);
                form.MinimumSize = new Size(560, 400);
                try { form.Icon = Ares.CommonGUI.ApoloBrand.GetIcon(32, Ares.CommonGUI.ApoloState.Playing, 0); } catch (Exception swallowed) { Ares.Settings.ErrorLog.Note(swallowed); }

                RichTextBox body = new RichTextBox();
                body.ReadOnly = true;
                body.ScrollBars = RichTextBoxScrollBars.Vertical;
                body.BorderStyle = BorderStyle.None;
                body.Dock = DockStyle.Fill;
                body.Font = new Font(form.Font.FontFamily, 10.5f);
                body.DetectUrls = true;
                body.TabStop = false;
                Ares.CommonGUI.ThemePalette palette = Ares.CommonGUI.ThemeManager.Current;
                if (palette != null && !palette.IsClassic)
                {
                    body.BackColor = palette.Surface;
                    body.ForeColor = palette.Text;
                }
                body.Text = text.Replace("\r\n", "\n").Replace("\n", "\r\n");
                // headings in bold and in the accent color: a long text is easier to scan
                using (Font bold = new Font(body.Font, FontStyle.Bold))
                {
                    int position = 0;
                    foreach (string line in body.Text.Split('\n'))
                    {
                        string clean = line.TrimEnd('\r');
                        if (IsHeadingLine(clean))
                        {
                            body.Select(position, clean.Length);
                            body.SelectionFont = bold;
                            if (palette != null && !palette.IsClassic)
                                body.SelectionColor = palette.Accent;
                        }
                        position += line.Length + 1;
                    }
                    body.Select(0, 0);
                }

                Panel pad = new Panel();
                pad.Dock = DockStyle.Fill;
                pad.Padding = new Padding(24, 16, 24, 8);
                pad.Controls.Add(body);

                FlowLayoutPanel row = new FlowLayoutPanel();
                row.Dock = DockStyle.Bottom;
                row.Height = 60;
                row.FlowDirection = FlowDirection.RightToLeft;
                row.Padding = new Padding(16, 10, 16, 10);
                for (int i = buttons.Count - 1; i >= 0; i--)
                {
                    Action action = buttons[i].Value;
                    Button b = new Button();
                    b.Text = buttons[i].Key;
                    b.AutoSize = true;
                    b.MinimumSize = new Size(120, 36);
                    if (action != null && i == 0)
                        b.Tag = "Primary"; // the first action of the window (install, ...) is the main one
                    b.Padding = new Padding(8, 0, 8, 0);
                    if (action == null)
                    {
                        b.DialogResult = DialogResult.OK;
                        form.AcceptButton = b;
                        form.CancelButton = b;
                    }
                    else
                    {
                        b.Click += (s, e) => action();
                    }
                    row.Controls.Add(b);
                }
                form.Controls.Add(pad);
                form.Controls.Add(row);
                Ares.CommonGUI.ThemeManager.Apply(form);
                form.Shown += (s, e) => body.Select(0, 0);
                ShowOrEmbed(form, null);
            }
        }

        // ---------------------------------------------------------------- Foundry VTT

        private static string FoundryFolder
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FoundryModule"); }
        }

        private void ShowFoundryHelp()
        {
            if (RedirectToSettings("foundry"))
                return;
            // the ID comes from the VDO.Ninja tab ("Send audio"): it is opened for the user (it is needed while the Foundry plays)
            if (m_EmbedHost == null) // inside the settings tab it must not leave the tab
            {
                try { ShowWebWindow(true); } catch (Exception swallowed) { Ares.Settings.ErrorLog.Note(swallowed); }
            }
            string id = ShareDialog.CleanStreamId(Ares.CommonGUI.Preferences.Get("VdoId", ""));
            string idLine = id.Length > 0
                ? T("O seu ID da transmissão é: " + id + "  (o botão \"Copiar ID\" abaixo copia para você).",
                    "Your stream ID is: " + id + "  (the \"Copy ID\" button below copies it).")
                : T("Você ainda não criou a transmissão: faça o passo 1 e depois abra esta janela de novo para copiar o ID.",
                    "You have not created the stream yet: do step 1 and open this window again to copy the ID.");
            string text = T(
@"O MÓDULO DO FOUNDRY VTT

Com ele, os jogadores ouvem dentro do Foundry a música e os efeitos que você toca no Apolo, e veem o nome da música que está tocando. Você também pode deixar um jogador controlar a música (pausar, próxima, trocar de pasta e de playlist). O áudio vai direto do seu computador para os jogadores (pelo VDO.Ninja); nada passa pelo servidor do Foundry.

Funciona no Foundry VTT v12 a v14.


1. PREPARE O APOLO (mestre)
Na barra de menus do Apolo, clique no ícone de envio (VDO.Ninja), confira as opções e clique em Aplicar. A entrada de áudio padrão é o ""Apolo Audio"", o microfone virtual do próprio Apolo, e não precisa instalar nada a mais (Windows 10 versão 2004 ou Windows 11). Deixe o Apolo aberto durante a sessão.

ATENÇÃO: quem envia o som para o Foundry é a aba ""Enviar áudio"" (VDO.Ninja) do Apolo. O ID da transmissão só existe com ela e o Foundry só escuta enquanto ela estiver aberta: pode deixá-la em segundo plano, mas não feche essa aba durante a sessão. Ao abrir esta janela, o Apolo já abre essa aba para você.

2. INSTALE O MÓDULO NO FOUNDRY
Clique em ""Instalar o módulo..."" abaixo e escolha a pasta Data\modules do seu Foundry (normalmente C:\Users\SEU_NOME\AppData\Local\FoundryVTT\Data\modules). O Apolo copia o módulo para lá. Se você usa o Forge ou outra hospedagem, clique em ""Abrir a pasta do módulo"" e envie o conteúdo do arquivo apolo-audio.zip pelo gerenciador de arquivos deles.

3. ATIVE E CONFIGURE (no Foundry, como mestre)
Reinicie o Foundry, abra o mundo e ative ""Apolo - Áudio ao vivo"" em Gerenciar módulos. Depois, em Configurações > Configurações dos módulos, cole o ID da transmissão (ou o link do ouvinte) no campo ""ID da transmissão do Apolo"".
" + idLine + @"

4. COMO USAR
Cada jogador encontra os controles do Apolo na aba Playlists da barra lateral, junto do volume do Foundry. Depois do primeiro clique em qualquer lugar, ele conecta sozinho. Há botão para ligar/desligar, silenciar e volume (só para aquele jogador). O mestre tem o botão de megafone para chamar todos para ouvir.

Para deixar um jogador controlar a música: Configurações dos módulos > Jogadores que controlam a música > Escolher jogadores (ou a engrenagem na linha do Apolo). Esse jogador ganha os botões anterior/pausar/parar/próxima e o botão de pastas e playlists, que abre as pastas e playlists do seu projeto logo abaixo da barra de progresso. Clicar numa playlist toca, como no Apolo.

IMPORTANTE: para o nome da música e o controle pelos jogadores funcionarem, o Foundry do MESTRE precisa estar aberto no mesmo computador do Apolo. Se o navegador perguntar se o site pode acessar dispositivos da rede local, permita.

DICAS
- Para mesas grandes (mais de 8 pessoas), reduza a qualidade (kbps) da transmissão no Apolo.
- O atraso é de uma fração de segundo: ótimo para música e ambiente, mas não para efeitos que precisam bater com uma ação na tela.
- O mestre continua ouvindo o Apolo no próprio computador; para ouvir também pelo Foundry, ligue a opção ""O mestre também ouve pelo Foundry"" nas configurações do módulo.",
@"THE FOUNDRY VTT MODULE

With it, the players hear inside Foundry the music and effects you play in Apolo, and see the name of the music which is playing. You can also let a player control the music (pause, next, change folder and playlist). The audio goes straight from your computer to the players (through VDO.Ninja); nothing goes through the Foundry server.

It works with Foundry VTT v12 to v14.


1. PREPARE APOLO (game master)
In the menu bar of Apolo click the send icon (VDO.Ninja), check the options and click Apply. The default audio input is ""Apolo Audio"", the virtual microphone of Apolo itself, and nothing else has to be installed (Windows 10 version 2004 or Windows 11). Keep Apolo open during the session.

ATTENTION: what sends the sound to Foundry is the ""Send audio"" (VDO.Ninja) tab of Apolo. The stream ID only exists with it and Foundry only hears while it is open: you can leave it in the background, but do not close that tab during the session. When you open this window, Apolo already opens that tab for you.

2. INSTALL THE MODULE IN FOUNDRY
Click ""Install the module..."" below and choose the Data\modules folder of your Foundry (usually C:\Users\YOUR_NAME\AppData\Local\FoundryVTT\Data\modules). Apolo copies the module there. If you use Forge or another host, click ""Open the module folder"" and upload the content of apolo-audio.zip with their file manager.

3. ENABLE AND CONFIGURE (in Foundry, as game master)
Restart Foundry, open the world and enable ""Apolo - Áudio ao vivo"" in Manage Modules. Then, in Settings > Module Settings, paste the stream ID (or the listener link) into the field ""Apolo stream ID"".
" + idLine + @"

4. HOW TO USE
Every player finds the Apolo controls in the Playlists tab of the sidebar, next to the Foundry volume. After the first click anywhere it connects by itself. There are buttons to switch on/off, mute and volume (only for that player). The game master has a megaphone button to call everybody to listen.

To let a player control the music: Module Settings > Players who control the music > Choose players (or the gear in the Apolo row). That player gets the previous/pause/stop/next buttons and the folders and playlists button, which opens the folders and playlists of your project right below the progress bar. Clicking a playlist plays it, as in Apolo.

IMPORTANT: for the music name and the control by players to work, the Foundry of the GAME MASTER must be open on the same computer as Apolo. If the browser asks whether the site may access devices of the local network, allow it.

TIPS
- For big tables (more than 8 people) lower the quality (kbps) of the stream in Apolo.
- The delay is a fraction of a second: great for music and ambience, not for effects which have to match an action on the screen.
- The game master keeps hearing Apolo on his own computer; to hear it through Foundry too, switch on ""The game master also listens through Foundry"" in the module settings.");

            List<KeyValuePair<string, Action>> buttons = new List<KeyValuePair<string, Action>>();
            buttons.Add(new KeyValuePair<string, Action>(T("Instalar o módulo...", "Install the module..."), InstallFoundryModule));
            buttons.Add(new KeyValuePair<string, Action>(T("Abrir a pasta do módulo", "Open the module folder"), OpenFoundryFolder));
            if (id.Length > 0)
            {
                buttons.Add(new KeyValuePair<string, Action>(T("Copiar ID", "Copy ID"), () =>
                {
                    try { Clipboard.SetText(id); } catch (Exception swallowed) { Ares.Settings.ErrorLog.Note(swallowed); }
                }));
            }
            buttons.Add(new KeyValuePair<string, Action>(T("Fechar", "Close"), null));
            ShowInfoWindow(T("Apolo no Foundry VTT", "Apolo in Foundry VTT"), text, buttons);
        }

        private void OpenFoundryFolder()
        {
            string dir = FoundryFolder;
            if (!Directory.Exists(dir))
            {
                Ares.CommonGUI.MessageDialog.Show(this, T("A pasta do módulo não está na pasta do Apolo (FoundryModule). Ela vem no pacote gerado pelo build.",
                    "The module folder is not in the Apolo folder (FoundryModule). It comes in the package made by the build."),
                    StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            OpenAddress(dir);
        }

        private void InstallFoundryModule()
        {
            string zip = Path.Combine(FoundryFolder, "apolo-audio.zip");
            if (!File.Exists(zip))
            {
                Ares.CommonGUI.MessageDialog.Show(this, T("O arquivo do módulo (FoundryModule\\apolo-audio.zip) não está na pasta do Apolo. Ele vem no pacote gerado pelo build.",
                    "The module file (FoundryModule\\apolo-audio.zip) is not in the Apolo folder. It comes in the package made by the build."),
                    StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (FolderBrowserDialog dlg = new FolderBrowserDialog())
            {
                dlg.Description = T("Escolha a pasta Data\\modules do Foundry VTT", "Choose the Data\\modules folder of Foundry VTT");
                string guess = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FoundryVTT", "Data", "modules");
                if (Directory.Exists(guess))
                    dlg.SelectedPath = guess;
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    string target = Path.Combine(dlg.SelectedPath, "apolo-audio");
                    Directory.CreateDirectory(target);
                    string root = Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                    using (ZipArchive archive = ZipFile.OpenRead(zip))
                    {
                        foreach (ZipArchiveEntry entry in archive.Entries)
                        {
                            string full = Path.GetFullPath(Path.Combine(target, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
                            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                                continue;
                            if (entry.FullName.EndsWith("/") || entry.Name.Length == 0)
                            {
                                Directory.CreateDirectory(full);
                                continue;
                            }
                            Directory.CreateDirectory(Path.GetDirectoryName(full));
                            using (Stream input = entry.Open())
                            using (FileStream output = File.Create(full))
                                input.CopyTo(output);
                        }
                    }
                    Ares.CommonGUI.MessageDialog.Show(this, T("Módulo instalado em:\r\n" + target + "\r\n\r\nAgora reinicie o Foundry e ative \"Apolo - Áudio ao vivo\" em Gerenciar módulos.",
                        "Module installed in:\r\n" + target + "\r\n\r\nNow restart Foundry and enable \"Apolo - Áudio ao vivo\" in Manage Modules."),
                        StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    Ares.CommonGUI.MessageDialog.Show(this, T("Não consegui instalar o módulo: ", "Could not install the module: ") + ex.Message,
                        StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // ---------------------------------------------------------------- updates

        private void ShowUpdateInfo()
        {
            string version = DisplayVersion();
            string text = T(
@"COMO ATUALIZAR O APOLO

A sua versão: " + version + @"

O Apolo é feito com muito carinho e é mantido pelo canal Dados Críticos. As versões novas e atualizadas não ficam em uma página pública de downloads: elas são entregues no Cofre de Conteúdo dos Clubistas, os apoiadores do canal.

COMO BAIXAR A VERSÃO MAIS NOVA
1. Abra o Cofre de Conteúdo (botão abaixo).
2. O Cofre pede uma senha. Pegue a senha atual na área exclusiva dos Clubistas, no Discord. A senha muda todo mês.
3. Baixe o arquivo mais recente do Apolo e instale por cima da versão atual. Os seus projetos, ícones e configurações são mantidos.

AINDA NÃO É CLUBISTA?
Os Clubistas apoiam o canal e recebem as novidades primeiro, como as atualizações do Apolo. Para saber como participar, abra a página dos apoiadores (botão abaixo).

O Apolo não procura atualizações sozinho e não envia nenhum dado pela internet por causa disso.",
@"HOW TO UPDATE APOLO

Your version: " + version + @"

Apolo is made with a lot of care and is kept by the Dados Críticos channel. New and updated versions are not on a public download page: they are delivered in the Content Vault of the Clubistas, the supporters of the channel.

HOW TO GET THE NEWEST VERSION
1. Open the Content Vault (button below).
2. The vault asks for a password. Get the current password in the exclusive area of the Clubistas on Discord. The password changes every month.
3. Download the latest Apolo file and install it over the current version. Your projects, icons and settings are kept.

NOT A CLUBISTA YET?
The Clubistas support the channel and get the news first, such as the Apolo updates. To learn how to join, open the supporters page (button below).

Apolo does not look for updates by itself and does not send any data over the internet because of this.");
            List<KeyValuePair<string, Action>> buttons = new List<KeyValuePair<string, Action>>();
            buttons.Add(new KeyValuePair<string, Action>(T("Abrir o Cofre de Conteúdo", "Open the Content Vault"), () => OpenAddress(VaultUrl)));
            buttons.Add(new KeyValuePair<string, Action>(T("Conhecer os Clubistas", "About the Clubistas"), () => OpenAddress(SupportersUrl)));
            buttons.Add(new KeyValuePair<string, Action>(T("Fechar", "Close"), null));
            ShowInfoWindow(T("Como atualizar o Apolo", "How to update Apolo"), text, buttons);
        }
    }
}
