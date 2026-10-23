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
            ToolStripMenuItem item = new ToolStripMenuItem(T("Tutorial em texto...", "Text tutorial..."));
            item.Name = "tutorialMenuItem";
            item.Click += (s, e) => ShowTutorial();
            helpToolStripMenuItem.DropDownItems.Insert(0, item);
            ToolStripMenuItem tour = new ToolStripMenuItem(T("Tour guiado do Apolo...", "Apolo guided tour..."));
            tour.Name = "guidedTourMenuItem";
            tour.Click += (s, e) => ShowGuidedTour(false);
            helpToolStripMenuItem.DropDownItems.Insert(0, tour);
            Shown += (s, e) =>
            {
                if (Ares.CommonGUI.Preferences.GetBool("TutorialSeen", false))
                    return;
                // the first time: the guided tour, once the window is on screen (it can be skipped; it is in the menu Sobre)
                Timer wait = new Timer();
                wait.Interval = 250;
                int tries = 0;
                wait.Tick += (s2, e2) =>
                {
                    tries++;
                    if (!m_StartupDone && tries < 40)
                        return;
                    wait.Stop();
                    wait.Dispose();
                    ShowGuidedTour(true);
                };
                wait.Start();
            };
        }

        private static string[][] TutorialPages()
        {
            return new string[][]
            {
                new string[] { T("Bem-vindo ao Apolo", "Welcome to Apolo"), T(
@"O Apolo é um controlador de músicas para mesas de RPG, lives e jogos. Ele toca música e efeitos sonoros AO MESMO TEMPO.

ATENÇÃO: o Apolo NÃO vem com músicas. Você usa as suas ou baixa pacotes prontos (veja a página ""Pacotes prontos"").

COMO ELE SE ORGANIZA
• Projeto: o arquivo com tudo o que você montou (menu Projeto > Novo projeto).
• Pasta: um grupo de playlists, como Exploração, Ação ou Efeitos do deserto (lista à esquerda).
• Playlist (botão): cada cartão grande. Clique para tocar, clique de novo para parar.

Um botão pode ser uma playlist de MÚSICA (selo ♫ no canto), um EFEITO SONORO (selo ⚡) ou os dois. A música toca em segundo plano e os efeitos tocam por cima dela.",
@"Apolo is a music controller for tabletop RPG, streams and games. It plays music and sound effects AT THE SAME TIME.

NOTE: Apolo does NOT come with music. You use your own or download ready-made packs (see the page ""Ready-made packs"").

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

                new string[] { T("Pacotes prontos", "Ready-made packs"), T(
@"Quer começar sem montar tudo? Use os PACOTES: projetos prontos, com pastas, botões, imagens e músicas livres.

COMO BAIXAR
• Menu Projeto > Pacotes de conteúdo prontos...
• Escolha o pacote, clique em baixar e espere. O Apolo confere o arquivo, importa tudo e abre o projeto.

As músicas dos pacotes têm licença livre e os créditos dos autores vão junto: veja em Projeto > Créditos do projeto. Se usar na live ou em vídeo, credite também lá.

Para abrir um pacote que você recebeu (arquivo .apkg), use Projeto > Importar.",
@"Want to start without building everything? Use the PACKS: ready projects with folders, buttons, pictures and free music.

HOW TO DOWNLOAD
• Menu Project > Ready-made content packs...
• Pick the pack, click download and wait. Apolo checks the file, imports everything and opens the project.

The music in the packs has a free licence and the credits of the authors come with it: see Project > Project credits. If you use it in a stream or video, credit it there too.

To open a pack you received (an .apkg file), use Project > Import.") },

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
• Pad flutuante (menu Extras): os botões de uma pasta aparecem por cima da tela, meio transparentes, com um atalho de teclado. Clique num botão para trocar de música e o pad fecha.

Também há o módulo para o Foundry VTT e o plugin do Stream Deck (menu Extras). As abas do topo abrem páginas da web, como o YouTube, que tocam junto com o Apolo.

Atualizações do Apolo: menu Sobre > Como atualizar.",
@"• Discord: takes the Apolo sound to a voice channel.
• Send audio (VDO.Ninja): sends the sound over the internet, to Foundry and to the players.
• Share: remote control from the phone (QR code) and on the network.
• Floating pad (Extras menu): the buttons of a folder appear on top of the screen, slightly transparent, with a keyboard shortcut. Click a button to change the music and the pad closes.

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
            // first start: after the tutorial, offer the ready-made packs (only once)
            BeginInvoke((MethodInvoker)(() => OfferPacksFirstTime()));
        }
    }
}
