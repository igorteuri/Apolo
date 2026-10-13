using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Ares.Player
{
    // Discord: Apolo plays its sound into a voice channel of a Discord server through a bot of the user
    // (same idea as Kenku FM). The sound keeps playing on the local speakers as well.
    public partial class Player
    {
        private DiscordBridge m_Discord;

        private void InitDiscord()
        {
            try
            {
                m_Discord = new DiscordBridge();
                ToolStripMenuItem item = new ToolStripMenuItem(T("Discord (tocar num canal de voz)...", "Discord (play in a voice channel)..."));
                item.Name = "discordMenuItem";
                item.Click += (s, e) => ShowDiscordDialog();
                extrasToolStripMenuItem.DropDownItems.Insert(2, item);
                FormClosed += (s, e) =>
                {
                    try { m_Discord.Dispose(); } catch (Exception) { }
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }

        /// <summary>Text and colour for the status bar.</summary>
        private string DiscordStatusText(out bool active)
        {
            active = false;
            if (m_Discord == null)
                return T("Discord: indisponível", "Discord: unavailable");
            switch (m_Discord.State)
            {
                case DiscordState.Starting:
                    return T("Discord: conectando...", "Discord: connecting...");
                case DiscordState.Ready:
                    return T("Discord: bot online", "Discord: bot online");
                case DiscordState.InVoice:
                    active = true;
                    return T("Discord: no canal ", "Discord: in ") + m_Discord.JoinedChannel;
                default:
                    return T("Discord: desligado", "Discord: off");
            }
        }

        private string DiscordFriendlyError(string error)
        {
            if (string.IsNullOrEmpty(error))
                return "";
            string low = error.ToLowerInvariant();
            if (low.Contains("invalid token") || low.Contains("token"))
                return T("O Discord não aceitou o token. Confira se copiou o token inteiro (use Reset Token e Copy de novo).",
                    "Discord did not accept the token. Make sure you copied all of it (use Reset Token and Copy again).");
            if (low.Contains("missing permissions") || low.Contains("permission"))
                return T("O bot não tem permissão para entrar nesse canal. Dê as permissões Conectar e Falar a ele.",
                    "The bot is not allowed to join that channel. Give it the Connect and Speak permissions.");
            return error;
        }

        private void ShowDiscordHelp(IWin32Window owner)
        {
            using (Form form = new Form())
            {
                form.Text = T("Como conseguir o token do bot do Discord", "How to get the Discord bot token");
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.StartPosition = FormStartPosition.CenterParent;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.ShowInTaskbar = false;
                form.ClientSize = new Size(640, 560);
                form.Icon = Ares.CommonGUI.ApoloBrand.GetIcon(32, Ares.CommonGUI.ApoloState.Playing, 0);

                TextBox text = new TextBox();
                text.Multiline = true;
                text.ReadOnly = true;
                text.ScrollBars = ScrollBars.Vertical;
                text.BorderStyle = BorderStyle.None;
                text.SetBounds(16, 14, 608, 458);
                text.Text = T(
                    "Para o Apolo tocar num canal de voz do Discord você precisa criar o seu próprio bot. É grátis, leva uns 3 minutos e só precisa de uma conta comum do Discord.\r\n\r\n" +
                    "1. CRIAR O APLICATIVO\r\n" +
                    "   • Clique em \"Abrir o Portal de Desenvolvedores\" (abaixo) e entre com a sua conta do Discord.\r\n" +
                    "   • Clique em New Application (canto superior direito), dê um nome (por ex. Apolo) e clique em Create.\r\n\r\n" +
                    "2. PEGAR O TOKEN\r\n" +
                    "   • No menu da esquerda, abra Bot.\r\n" +
                    "   • Clique em Reset Token (se pedir, confirme sua identidade) e depois em Copy.\r\n" +
                    "   • Volte ao Apolo, cole o token no campo \"Token do bot\" e clique em Conectar bot.\r\n\r\n" +
                    "3. COLOCAR O BOT NO SEU SERVIDOR\r\n" +
                    "   • Com o bot conectado, clique em \"Adicionar o bot a um servidor\" no Apolo. Ele abre o link já pronto, com as permissões Conectar e Falar.\r\n" +
                    "   • Escolha o servidor, clique em Continuar e depois em Autorizar (resolva o captcha se aparecer).\r\n" +
                    "   • Para vários servidores, repita este passo.\r\n\r\n" +
                    "4. TOCAR\r\n" +
                    "   • No Apolo, escolha o servidor e o canal de voz e clique em Entrar no canal. Entre também no mesmo canal pelo Discord para ouvir.\r\n" +
                    "   • Tudo que o Apolo toca (músicas, efeitos, o que mais tocar) vai para o canal e continua saindo nas suas caixas de som.\r\n\r\n" +
                    "IMPORTANTE\r\n" +
                    "   • O token é como uma senha do bot. Não mostre nem envie a ninguém. O Apolo guarda o token só neste computador, criptografado para o seu usuário do Windows.\r\n" +
                    "   • Se o token vazar, volte ao Portal, abra Bot e clique em Reset Token: o antigo para de funcionar.\r\n" +
                    "   • Dica: você pode dar um avatar e um nome ao bot na mesma página do Portal.",
                    "To play Apolo in a Discord voice channel you need your own bot. It is free, takes about 3 minutes and needs only a normal Discord account.\r\n\r\n" +
                    "1. CREATE THE APPLICATION\r\n" +
                    "   • Click \"Open the Developer Portal\" (below) and sign in with your Discord account.\r\n" +
                    "   • Click New Application (top right), give it a name (for example Apolo) and click Create.\r\n\r\n" +
                    "2. GET THE TOKEN\r\n" +
                    "   • In the left menu open Bot.\r\n" +
                    "   • Click Reset Token (confirm your identity if asked) and then Copy.\r\n" +
                    "   • Go back to Apolo, paste the token into the \"Bot token\" field and click Connect bot.\r\n\r\n" +
                    "3. PUT THE BOT IN YOUR SERVER\r\n" +
                    "   • With the bot connected, click \"Add the bot to a server\" in Apolo. It opens a ready link with the Connect and Speak permissions.\r\n" +
                    "   • Choose the server, click Continue and then Authorize (solve the captcha if shown).\r\n" +
                    "   • For several servers repeat this step.\r\n\r\n" +
                    "4. PLAY\r\n" +
                    "   • In Apolo choose the server and the voice channel and click Join channel. Join the same channel in Discord to listen.\r\n" +
                    "   • Everything Apolo plays (music, effects, anything else) goes to the channel and still plays on your speakers.\r\n\r\n" +
                    "IMPORTANT\r\n" +
                    "   • The token is like a password of the bot. Never show or send it to anyone. Apolo keeps it only on this computer, encrypted for your Windows user.\r\n" +
                    "   • If the token leaks, go back to the Portal, open Bot and click Reset Token: the old one stops working.\r\n" +
                    "   • Tip: you can give the bot an avatar and a name on the same page of the Portal.");

                Button portal = new Button();
                portal.Text = T("Abrir o Portal de Desenvolvedores", "Open the Developer Portal");
                portal.SetBounds(16, 484, 280, 36);
                portal.Click += (s, e) => OpenInBrowser("https://discord.com/developers/applications");
                Button close = new Button();
                close.Text = T("Fechar", "Close");
                close.DialogResult = DialogResult.OK;
                close.SetBounds(640 - 16 - 100, 484, 100, 36);

                form.Controls.AddRange(new Control[] { text, portal, close });
                form.AcceptButton = close;
                Ares.CommonGUI.ThemeManager.Apply(form);
                form.ShowDialog(owner);
            }
        }

        private void ShowDiscordDialog()
        {
            if (m_Discord == null)
                return;
            using (Form form = new Form())
            {
                form.Text = T("Discord", "Discord");
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.StartPosition = FormStartPosition.CenterParent;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.ShowInTaskbar = false;
                form.ClientSize = new Size(700, 520);
                form.Icon = Ares.CommonGUI.ApoloBrand.GetIcon(32, Ares.CommonGUI.ApoloState.Playing, 0);

                Label intro = new Label();
                intro.SetBounds(16, 12, 668, 58);
                intro.Text = T(
                    "Toque o Apolo num canal de voz do Discord (como o Kenku FM). O bot é seu: você cola aqui o token dele. Tudo que o Apolo toca vai para o canal e continua saindo nas suas caixas de som.",
                    "Play Apolo in a Discord voice channel (like Kenku FM). The bot is yours: paste its token here. Everything Apolo plays goes to the channel and still plays on your speakers.");

                Label tokenLabel = new Label();
                tokenLabel.SetBounds(16, 80, 110, 26);
                tokenLabel.Text = T("Token do bot:", "Bot token:");
                tokenLabel.TextAlign = ContentAlignment.MiddleLeft;
                TextBox token = new TextBox();
                token.SetBounds(130, 80, 400, 26);
                token.UseSystemPasswordChar = true;
                bool remember = Ares.CommonGUI.Preferences.GetBool("DiscordRemember", true);
                token.Text = remember ? DiscordTokenStore.Load() : "";
                CheckBox show = new CheckBox();
                show.SetBounds(540, 80, 150, 26);
                show.Text = T("Mostrar", "Show");
                show.CheckedChanged += (s, e) => token.UseSystemPasswordChar = !show.Checked;

                CheckBox rememberBox = new CheckBox();
                rememberBox.SetBounds(130, 110, 400, 24);
                rememberBox.Text = T("Lembrar o token neste computador (criptografado)", "Remember the token on this computer (encrypted)");
                rememberBox.Checked = remember;
                Button help = new Button();
                help.SetBounds(540, 108, 144, 30);
                help.Text = T("Como obter?", "How to get it?");
                help.Click += (s, e) => ShowDiscordHelp(form);

                Button connect = new Button();
                connect.SetBounds(16, 150, 200, 38);
                Label status = new Label();
                status.SetBounds(228, 150, 456, 38);
                status.TextAlign = ContentAlignment.MiddleLeft;

                Label guildLabel = new Label();
                guildLabel.SetBounds(16, 206, 110, 26);
                guildLabel.Text = T("Servidor:", "Server:");
                guildLabel.TextAlign = ContentAlignment.MiddleLeft;
                ComboBox guilds = new ComboBox();
                guilds.DropDownStyle = ComboBoxStyle.DropDownList;
                guilds.SetBounds(130, 206, 320, 26);
                Button invite = new Button();
                invite.SetBounds(460, 203, 224, 32);
                invite.Text = T("Adicionar o bot a um servidor", "Add the bot to a server");

                Label channelLabel = new Label();
                channelLabel.SetBounds(16, 246, 110, 26);
                channelLabel.Text = T("Canal de voz:", "Voice channel:");
                channelLabel.TextAlign = ContentAlignment.MiddleLeft;
                ComboBox channels = new ComboBox();
                channels.DropDownStyle = ComboBoxStyle.DropDownList;
                channels.SetBounds(130, 246, 320, 26);
                Button join = new Button();
                join.SetBounds(460, 243, 224, 32);

                Label hint = new Label();
                hint.SetBounds(16, 290, 668, 54);
                hint.ForeColor = Color.Gray;

                Label notes = new Label();
                notes.SetBounds(16, 350, 668, 110);
                notes.ForeColor = Color.Gray;
                notes.Text = T(
                    "• O Apolo envia o som dele mesmo (não os outros programas do computador).\r\n• Precisa do Windows 10 atualizado (versão 2004 ou mais nova) ou do Windows 11.\r\n• O token fica só neste computador. Quem tiver o token controla o bot: não compartilhe.\r\n• Este recurso usa o componente \"discord\" que vem na pasta do Apolo.",
                    "• Apolo sends its own sound (not the other programs on the computer).\r\n• Needs an up-to-date Windows 10 (version 2004 or newer) or Windows 11.\r\n• The token stays on this computer only. Whoever has the token controls the bot: do not share it.\r\n• This feature uses the \"discord\" component which comes in the Apolo folder.");

                Button close = new Button();
                close.Text = T("Fechar", "Close");
                close.DialogResult = DialogResult.OK;
                close.SetBounds(700 - 16 - 100, 520 - 12 - 34, 100, 34);

                string guildsKey = null;
                bool updating = false;
                Action fillChannels = () =>
                {
                    string wantChannel = channels.SelectedItem != null ? ((DiscordChannelInfo)channels.SelectedItem).Id
                        : Ares.CommonGUI.Preferences.Get("DiscordChannel", "");
                    channels.Items.Clear();
                    DiscordGuildInfo g = guilds.SelectedItem as DiscordGuildInfo;
                    if (g == null)
                        return;
                    foreach (DiscordChannelInfo c in g.Channels)
                        channels.Items.Add(c);
                    for (int i = 0; i < channels.Items.Count; ++i)
                    {
                        if (((DiscordChannelInfo)channels.Items[i]).Id == wantChannel)
                            channels.SelectedIndex = i;
                    }
                    if (channels.SelectedIndex < 0 && channels.Items.Count > 0)
                        channels.SelectedIndex = 0;
                };

                Action refresh = () =>
                {
                    DiscordState st = m_Discord.State;
                    bool off = st == DiscordState.Off;
                    token.ReadOnly = !off;
                    connect.Text = off ? T("Conectar bot", "Connect bot") : T("Desconectar bot", "Disconnect bot");
                    connect.Enabled = st != DiscordState.Starting;
                    guilds.Enabled = channels.Enabled = st == DiscordState.Ready || st == DiscordState.InVoice;
                    invite.Enabled = !string.IsNullOrEmpty(m_Discord.AppId) && !off;
                    join.Enabled = (st == DiscordState.Ready || st == DiscordState.InVoice) && channels.SelectedItem != null;
                    join.Text = st == DiscordState.InVoice ? T("Sair do canal", "Leave channel") : T("Entrar no canal", "Join channel");

                    string text;
                    if (!DiscordBridge.Available)
                        text = T("Falta o componente do Discord (pasta \"discord\" ao lado do Apolo). Ele vem no pacote gerado pelo build.yml atualizado.",
                            "The Discord component is missing (folder \"discord\" next to Apolo). It comes in the package made by the updated build.yml.");
                    else if (st == DiscordState.Off)
                        text = string.IsNullOrEmpty(m_Discord.Error) ? T("Desligado.", "Off.") : DiscordFriendlyError(m_Discord.Error);
                    else if (st == DiscordState.Starting)
                        text = T("Conectando o bot...", "Connecting the bot...");
                    else if (st == DiscordState.Ready)
                        text = T("Bot online: ", "Bot online: ") + m_Discord.UserName + (string.IsNullOrEmpty(m_Discord.Error) ? "" : "\r\n" + DiscordFriendlyError(m_Discord.Error));
                    else
                        text = T("Tocando em: ", "Playing in: ") + m_Discord.JoinedGuild + " › " + m_Discord.JoinedChannel;
                    status.Text = text;

                    if (st == DiscordState.InVoice && m_Discord.CaptureError != null)
                        hint.Text = T("Entrei no canal, mas não consegui capturar o som do Apolo (", "I joined the channel, but could not capture Apolo's sound (")
                            + m_Discord.CaptureError + T("). Isso exige Windows 10 versão 2004+ ou Windows 11.", "). This needs Windows 10 version 2004+ or Windows 11.");
                    else if ((st == DiscordState.Ready) && m_Discord.Guilds.Count == 0)
                        hint.Text = T("O bot ainda não está em nenhum servidor. Clique em \"Adicionar o bot a um servidor\", autorize no navegador e volte aqui.",
                            "The bot is not in any server yet. Click \"Add the bot to a server\", authorize in the browser and come back.");
                    else
                        hint.Text = "";

                    // server / channel lists
                    List<DiscordGuildInfo> list = m_Discord.Guilds;
                    System.Text.StringBuilder key = new System.Text.StringBuilder();
                    foreach (DiscordGuildInfo g in list)
                    {
                        key.Append(g.Id).Append(':').Append(g.Name);
                        foreach (DiscordChannelInfo c in g.Channels)
                            key.Append(',').Append(c.Id).Append(c.Name);
                        key.Append(';');
                    }
                    if (key.ToString() != guildsKey)
                    {
                        guildsKey = key.ToString();
                        updating = true;
                        string wantGuild = guilds.SelectedItem != null ? ((DiscordGuildInfo)guilds.SelectedItem).Id
                            : Ares.CommonGUI.Preferences.Get("DiscordGuild", "");
                        guilds.Items.Clear();
                        foreach (DiscordGuildInfo g in list)
                            guilds.Items.Add(g);
                        for (int i = 0; i < guilds.Items.Count; ++i)
                        {
                            if (((DiscordGuildInfo)guilds.Items[i]).Id == wantGuild)
                                guilds.SelectedIndex = i;
                        }
                        if (guilds.SelectedIndex < 0 && guilds.Items.Count > 0)
                            guilds.SelectedIndex = 0;
                        updating = false;
                        fillChannels();
                    }
                };

                guilds.SelectedIndexChanged += (s, e) =>
                {
                    if (updating)
                        return;
                    DiscordGuildInfo g = guilds.SelectedItem as DiscordGuildInfo;
                    if (g != null)
                        Ares.CommonGUI.Preferences.Set("DiscordGuild", g.Id);
                    channels.SelectedItem = null;
                    fillChannels();
                    refresh();
                };
                channels.SelectedIndexChanged += (s, e) =>
                {
                    DiscordChannelInfo c = channels.SelectedItem as DiscordChannelInfo;
                    if (c != null)
                        Ares.CommonGUI.Preferences.Set("DiscordChannel", c.Id);
                };

                connect.Click += (s, e) =>
                {
                    if (m_Discord.State != DiscordState.Off)
                    {
                        m_Discord.Disconnect();
                        refresh();
                        return;
                    }
                    string value = token.Text.Trim();
                    if (value.Length < 20)
                    {
                        MessageBox.Show(form, T("Cole o token do bot primeiro. Clique em \"Como obter?\" se precisar de ajuda.",
                            "Paste the bot token first. Click \"How to get it?\" if you need help."),
                            StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    Ares.CommonGUI.Preferences.SetBool("DiscordRemember", rememberBox.Checked);
                    DiscordTokenStore.Save(rememberBox.Checked ? value : "");
                    string error = m_Discord.Connect(value);
                    if (error != null && error != "missing")
                    {
                        MessageBox.Show(form, error, StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    refresh();
                };
                invite.Click += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(m_Discord.AppId))
                        OpenInBrowser("https://discord.com/oauth2/authorize?client_id=" + m_Discord.AppId + "&scope=bot&permissions=3145728");
                };
                join.Click += (s, e) =>
                {
                    if (m_Discord.State == DiscordState.InVoice)
                    {
                        m_Discord.Leave();
                    }
                    else
                    {
                        DiscordGuildInfo g = guilds.SelectedItem as DiscordGuildInfo;
                        DiscordChannelInfo c = channels.SelectedItem as DiscordChannelInfo;
                        if (g != null && c != null)
                            m_Discord.Join(g.Id, c.Id);
                    }
                    refresh();
                };

                Timer tick = new Timer();
                tick.Interval = 400;
                tick.Tick += (s, e) => refresh();
                tick.Start();

                form.Controls.AddRange(new Control[] { intro, tokenLabel, token, show, rememberBox, help, connect, status,
                    guildLabel, guilds, invite, channelLabel, channels, join, hint, notes, close });
                form.AcceptButton = close;
                Ares.CommonGUI.ThemeManager.Apply(form);
                refresh();
                form.ShowDialog(this);
                tick.Stop();
                tick.Dispose();
                token.Text = "";
            }
        }
    }
}
