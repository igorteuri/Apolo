/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 A short tutorial window: opens the first time Apolo starts and later from the menu Sobre > Tutorial do Apolo.
*/
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Ares.Player
{
    public partial class Player
    {
        private void InitTutorial()
        {
            ToolStripMenuItem item = new ToolStripMenuItem(T("Tutorial do Apolo...", "Apolo tutorial..."));
            item.Name = "tutorialMenuItem";
            item.Click += (s, e) => ShowTutorial();
            helpToolStripMenuItem.DropDownItems.Insert(0, item);
            Shown += (s, e) =>
            {
                if (Ares.CommonGUI.Preferences.GetBool("TutorialSeen", false))
                    return;
                BeginInvoke((MethodInvoker)(() => ShowTutorial()));
            };
        }

        private static string[][] TutorialPages()
        {
            return new string[][]
            {
                new string[] { T("Bem-vindo ao Apolo", "Welcome to Apolo"), T(
@"O Apolo toca música e efeitos sonoros AO MESMO TEMPO, para mesas de RPG, lives e jogos.

COMO ELE SE ORGANIZA
• Projeto: o arquivo com tudo o que você montou (menu Projeto > Novo projeto).
• Pasta: um grupo de playlists, como Exploração, Ação ou Efeitos do deserto (lista à esquerda).
• Playlist (botão): cada cartão grande. Clique para tocar, clique de novo para parar.

Um botão pode ser uma playlist de MÚSICA (selo ♫ no canto), um EFEITO SONORO (selo ⚡) ou os dois. A música toca em segundo plano e os efeitos tocam por cima dela.",
@"Apolo plays music and sound effects AT THE SAME TIME, for tabletop RPG, streams and games.

HOW IT IS ORGANIZED
• Project: the file with everything you built (menu Project > New project).
• Folder: a group of playlists, like Exploration, Action or Desert effects (list on the left).
• Playlist (button): each big card. Click to play, click again to stop.

A button can be a MUSIC playlist (♫ badge in the corner), a SOUND EFFECT (⚡ badge) or both. The music plays in the background and the effects play over it.") },

                new string[] { T("Adicionando sons", "Adding sounds"), T(
@"ARRASTE do Explorer para a janela do Apolo: arquivos ou pastas de áudio. Eles entram sempre no projeto que está aberto.

Na janelinha que aparece, escolha o que fazer:
• Trilha: uma playlist de música por pasta (toca em ordem aleatória ou em sequência).
• Efeitos: um botão por arquivo, para disparar quando quiser.

Para organizar, entre no modo de edição (botão no topo): dá para arrastar os botões de lugar, trocar emoji e cor, renomear e excluir. Se errar, use Ctrl+Z.",
@"DRAG audio files or folders from Explorer into the Apolo window. They always go into the project which is open.

In the small window, choose what to do:
• Soundtrack: one music playlist per folder (plays in random order or in sequence).
• Effects: one button per file, to fire whenever you want.

To organize, enter edit mode (button at the top): you can drag buttons around, change emoji and colour, rename and delete. If you make a mistake, press Ctrl+Z.") },

                new string[] { T("Tocando e acompanhando", "Playing and following"), T(
@"• Barra de progresso (no topo): mostra a música atual, com o nome embaixo. Clique ou arraste para pular para outro ponto.
• Faixa ""Tocando agora"": cada efeito que está tocando vira um chip com pausar, parar e repetir. Abas com som (YouTube, VDO.Ninja) aparecem à direita, com botão de mutar.
• Lista de músicas (botão direito numa playlist > Ver músicas): colunas de tipo, título, artista, álbum, duração, quantas vezes tocou e direitos autorais. Clique no cabeçalho para ordenar e arraste as linhas para reorganizar.

A bolinha de DIREITOS (verde, amarela ou vermelha) é só um INDICATIVO, nunca uma garantia. Use ""Verificar direitos autorais"" no botão direito da lista.",
@"• Progress bar (top): shows the current music, with its name below. Click or drag to jump.
• ""Playing now"" strip: every effect that is playing becomes a chip with pause, stop and repeat. Tabs with sound (YouTube, VDO.Ninja) show on the right, with a mute button.
• Track list (right-click a playlist > View tracks): columns for kind, title, artist, album, duration, play count and copyright. Click a header to sort and drag rows to reorder.

The RIGHTS dot (green, yellow or red) is only an INDICATION, never a guarantee. Use ""Check the copyright"" in the right-click menu of the list.") },

                new string[] { T("Compartilhando o som", "Sharing the sound"), T(
@"• Discord: leva o som do Apolo para um canal de voz.
• Enviar áudio (VDO.Ninja): manda o som pela internet, para o Foundry e para os jogadores.
• Compartilhar: controle remoto pelo celular (QR code) e na rede.

Também há o módulo para o Foundry VTT e o plugin do Stream Deck (menu Extras). As abas do topo abrem páginas da web, como o YouTube, que tocam junto com o Apolo.

Atualizações do Apolo: menu Sobre > Como atualizar.",
@"• Discord: takes the Apolo sound to a voice channel.
• Send audio (VDO.Ninja): sends the sound over the internet, to Foundry and to the players.
• Share: remote control from the phone (QR code) and on the network.

There is also the Foundry VTT module and the Stream Deck plugin (menu Extras). The tabs at the top open web pages, like YouTube, which play together with Apolo.

Apolo updates: menu About > How to update.") }
            };
        }

        private void ShowTutorial()
        {
            string[][] pages = TutorialPages();
            int index = 0;
            using (Form form = new Form())
            {
                form.Text = T("Tutorial do Apolo", "Apolo tutorial");
                form.StartPosition = FormStartPosition.CenterParent;
                form.ShowInTaskbar = false;
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.ClientSize = new Size(680, 470);
                try { form.Icon = Ares.CommonGUI.ApoloBrand.GetIcon(32, Ares.CommonGUI.ApoloState.Playing, 0); } catch (Exception) { }

                Label title = new Label();
                title.SetBounds(20, 14, 640, 34);
                title.Font = new Font(form.Font.FontFamily, 15f, FontStyle.Bold);
                form.Controls.Add(title);

                TextBox body = new TextBox();
                body.Multiline = true;
                body.ReadOnly = true;
                body.BorderStyle = BorderStyle.None;
                body.TabStop = false;
                body.SetBounds(22, 56, 636, 340);
                body.Font = new Font(form.Font.FontFamily, 10.5f);
                form.Controls.Add(body);

                Label counter = new Label();
                counter.SetBounds(20, 432, 160, 22);
                form.Controls.Add(counter);

                CheckBox again = new CheckBox();
                again.Text = T("Mostrar ao abrir o Apolo", "Show when Apolo starts");
                again.AutoSize = true;
                again.Location = new Point(20, 404);
                again.Checked = !Ares.CommonGUI.Preferences.GetBool("TutorialSeen", false);
                form.Controls.Add(again);

                Button back = new Button();
                back.Text = T("Voltar", "Back");
                back.SetBounds(form.ClientSize.Width - 300, 428, 90, 30);
                Button next = new Button();
                next.Text = T("Próximo", "Next");
                next.SetBounds(form.ClientSize.Width - 200, 428, 90, 30);
                Button close = new Button();
                close.Text = T("Fechar", "Close");
                close.DialogResult = DialogResult.Cancel;
                close.SetBounds(form.ClientSize.Width - 100, 428, 80, 30);
                form.Controls.Add(back);
                form.Controls.Add(next);
                form.Controls.Add(close);
                form.CancelButton = close;

                Action show = () =>
                {
                    title.Text = pages[index][0];
                    body.Text = pages[index][1].Replace("\r\n", "\n").Replace("\n", "\r\n");
                    body.Select(0, 0);
                    counter.Text = (index + 1) + " / " + pages.Length;
                    back.Enabled = index > 0;
                    next.Text = index == pages.Length - 1 ? T("Concluir", "Finish") : T("Próximo", "Next");
                };
                back.Click += (s, e) => { if (index > 0) { index--; show(); } };
                next.Click += (s, e) =>
                {
                    if (index < pages.Length - 1)
                    {
                        index++;
                        show();
                    }
                    else
                    {
                        form.DialogResult = DialogResult.OK;
                    }
                };
                show();
                Ares.CommonGUI.ThemeManager.Apply(form);
                form.ShowDialog(this);
                Ares.CommonGUI.Preferences.SetBool("TutorialSeen", !again.Checked);
            }
        }
    }
}
