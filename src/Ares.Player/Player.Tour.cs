/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 The guided tour: the window is dimmed except for one part at a time, which is outlined, and a card beside it explains
 how to use it (Back / Next / Skip). It opens the first time Apolo starts and later from the menu Sobre > Tour guiado.
 The dimming is a borderless window over the main window with a hole (Region) where the part is; a nearly invisible
 window over the hole takes the clicks, so that nothing is triggered by accident during the tour.
*/
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Ares.Player
{
    internal class TourStep
    {
        public string Title;
        public string Text;
        /// <summary>The part to outline, in screen coordinates; Rectangle.Empty = not shown right now (the step is skipped).</summary>
        public Func<Rectangle> Target;
        /// <summary>True for the steps without a part (welcome, end): the card is in the middle.</summary>
        public bool Centered;
    }

    /// <summary>A borderless window that does not take the focus.</summary>
    internal class TourLayer : Form
    {
        public Rectangle Hole = Rectangle.Empty;
        public Color Ring = Color.Gold;

        public TourLayer()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.Black;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                return cp;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            if (Hole.IsEmpty)
                return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = Rounded(Hole, 8))
            using (Pen pen = new Pen(Ring, 5f))
                e.Graphics.DrawPath(pen, path);
        }

        public static GraphicsPath Rounded(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = Math.Max(2, Math.Min(radius * 2, Math.Min(r.Width, r.Height)));
            path.AddArc(r.Left, r.Top, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal class GuidedTour
    {
        private const int CardWidth = 380;

        private readonly Form m_Owner;
        private readonly List<TourStep> m_Steps;
        private readonly Action<bool> m_Finished;
        private readonly Func<string, string, string> m_T;

        private TourLayer m_Dim, m_Blocker;
        private Form m_Card;
        private Label m_Title, m_Body, m_Counter;
        private Button m_Back, m_Next, m_Skip;
        private int m_Index = -1;
        private bool m_Done;
        private EventHandler m_OwnerChanged;

        public GuidedTour(Form owner, List<TourStep> steps, Func<string, string, string> t, Action<bool> finished)
        {
            m_Owner = owner;
            m_Steps = steps;
            m_T = t;
            m_Finished = finished;
        }

        public void Start()
        {
            m_Dim = new TourLayer();
            m_Dim.Opacity = 0.62;
            m_Blocker = new TourLayer();
            m_Blocker.Opacity = 0.01;
            m_Card = BuildCard();
            m_OwnerChanged = (s, e) => Relayout();
            m_Owner.LocationChanged += m_OwnerChanged;
            m_Owner.SizeChanged += m_OwnerChanged;
            m_Owner.FormClosing += OwnerClosing;
            m_Dim.Owner = m_Owner;
            m_Blocker.Owner = m_Owner;
            m_Card.Owner = m_Owner;
            m_Dim.Bounds = m_Owner.Bounds;
            m_Dim.Show(m_Owner);
            m_Blocker.Bounds = Rectangle.Empty;
            m_Blocker.Show(m_Owner);
            m_Card.Show(m_Owner);
            Go(1);
        }

        private void OwnerClosing(object sender, FormClosingEventArgs e)
        {
            Finish(false);
        }

        private Form BuildCard()
        {
            Form card = new Form();
            card.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            card.ControlBox = false;
            card.ShowInTaskbar = false;
            card.StartPosition = FormStartPosition.Manual;
            card.KeyPreview = true;
            card.ClientSize = new Size(CardWidth, 200);
            card.Text = m_T("Tour do Apolo", "Apolo tour");

            m_Title = new Label();
            m_Title.Font = new Font(card.Font.FontFamily, 13f, FontStyle.Bold);
            m_Title.SetBounds(16, 12, CardWidth - 32, 28);
            card.Controls.Add(m_Title);

            m_Body = new Label();
            m_Body.AutoSize = false;
            m_Body.Font = new Font(card.Font.FontFamily, 10f);
            m_Body.SetBounds(16, 46, CardWidth - 32, 80);
            card.Controls.Add(m_Body);

            m_Counter = new Label();
            m_Counter.AutoSize = true;
            card.Controls.Add(m_Counter);

            m_Skip = new Button();
            m_Skip.AutoSize = true;
            m_Skip.Click += (s, e) => Finish(false);
            card.Controls.Add(m_Skip);

            m_Back = new Button();
            m_Back.Text = m_T("Voltar", "Back");
            m_Back.Click += (s, e) => Go(-1);
            card.Controls.Add(m_Back);

            m_Next = new Button();
            m_Next.Click += (s, e) => Go(1);
            card.Controls.Add(m_Next);

            card.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    Finish(false);
                    e.Handled = true;
                }
                else if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Enter)
                {
                    Go(1);
                    e.Handled = true;
                }
                else if (e.KeyCode == Keys.Left)
                {
                    Go(-1);
                    e.Handled = true;
                }
            };
            card.FormClosing += (s, e) =>
            {
                if (!m_Done)
                    Finish(false);
            };
            Ares.CommonGUI.ThemeManager.Apply(card);
            return card;
        }

        // direction +1 / -1: goes to the next step that can be shown
        private void Go(int direction)
        {
            if (m_Done)
                return;
            int i = m_Index;
            while (true)
            {
                i += direction;
                if (i >= m_Steps.Count)
                {
                    Finish(true);
                    return;
                }
                if (i < 0)
                    return;
                TourStep step = m_Steps[i];
                if (step.Centered || !step.Target().IsEmpty)
                    break;
            }
            m_Index = i;
            Relayout();
        }

        private int ShownCount()
        {
            int n = 0;
            foreach (TourStep s in m_Steps)
            {
                if (s.Centered || !s.Target().IsEmpty)
                    n++;
            }
            return n;
        }

        private int ShownNumber()
        {
            int n = 0;
            for (int i = 0; i <= m_Index && i < m_Steps.Count; i++)
            {
                if (m_Steps[i].Centered || !m_Steps[i].Target().IsEmpty)
                    n++;
            }
            return n;
        }

        private void Relayout()
        {
            if (m_Done || m_Index < 0 || m_Index >= m_Steps.Count)
                return;
            TourStep step = m_Steps[m_Index];
            Rectangle window = m_Owner.Bounds;
            m_Dim.Bounds = window;

            Rectangle target = step.Centered ? Rectangle.Empty : step.Target();
            Rectangle hole = Rectangle.Empty;
            if (!target.IsEmpty)
            {
                hole = Rectangle.Intersect(Rectangle.Inflate(target, 6, 6), window);
                if (hole.Width < 4 || hole.Height < 4)
                    hole = Rectangle.Empty;
            }
            // the hole in the dimming window
            Rectangle local = hole.IsEmpty ? Rectangle.Empty : new Rectangle(hole.X - window.X, hole.Y - window.Y, hole.Width, hole.Height);
            m_Dim.Hole = local;
            Region region = new Region(new Rectangle(0, 0, window.Width, window.Height));
            if (!local.IsEmpty)
            {
                using (GraphicsPath path = TourLayer.Rounded(local, 8))
                    region.Exclude(path);
            }
            Region old = m_Dim.Region;
            m_Dim.Region = region;
            if (old != null)
                old.Dispose();
            Ares.CommonGUI.ThemePalette palette = Ares.CommonGUI.ThemeManager.Current;
            m_Dim.Ring = palette != null && !palette.IsClassic ? palette.Accent : Color.Gold;
            m_Dim.Invalidate();
            m_Blocker.Bounds = hole.IsEmpty ? Rectangle.Empty : hole;
            m_Blocker.Visible = !hole.IsEmpty;

            // the card
            m_Title.Text = step.Title;
            m_Body.Text = step.Text;
            Size need = TextRenderer.MeasureText(step.Text, m_Body.Font, new Size(CardWidth - 32, 2000), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
            int bodyHeight = Math.Max(40, need.Height + 6);
            m_Body.Height = bodyHeight;
            int buttonsTop = m_Body.Bottom + 14;
            bool first = m_Index == 0 || ShownNumber() <= 1;
            bool last = ShownNumber() >= ShownCount();
            m_Skip.Text = first ? m_T("Pular o tour", "Skip the tour") : m_T("Sair do tour", "Leave the tour");
            m_Next.Text = first && step.Centered ? m_T("Começar", "Start") : (last ? m_T("Concluir", "Finish") : m_T("Próximo", "Next"));
            m_Back.Enabled = !first;
            m_Counter.Text = ShownNumber() + " / " + ShownCount();
            m_Next.SetBounds(CardWidth - 16 - 100, buttonsTop, 100, 30);
            m_Back.SetBounds(m_Next.Left - 8 - 80, buttonsTop, 80, 30);
            m_Skip.Location = new Point(16, buttonsTop + 2);
            m_Counter.Location = new Point(16, buttonsTop + 36);
            m_Skip.Height = 26;
            m_Card.ClientSize = new Size(CardWidth, m_Counter.Bottom + 10);
            PlaceCard(hole, window);
            m_Card.Activate();
            m_Next.Focus();
        }

        private void PlaceCard(Rectangle hole, Rectangle window)
        {
            Rectangle area = Screen.FromRectangle(window).WorkingArea;
            Size size = m_Card.Size;
            int gap = 16;
            Point p;
            if (hole.IsEmpty)
            {
                p = new Point(window.X + (window.Width - size.Width) / 2, window.Y + (window.Height - size.Height) / 2);
            }
            else if (hole.Bottom + gap + size.Height <= area.Bottom)
            {
                p = new Point(hole.X + (hole.Width - size.Width) / 2, hole.Bottom + gap);
            }
            else if (hole.Top - gap - size.Height >= area.Top)
            {
                p = new Point(hole.X + (hole.Width - size.Width) / 2, hole.Top - gap - size.Height);
            }
            else if (hole.Right + gap + size.Width <= area.Right)
            {
                p = new Point(hole.Right + gap, hole.Y + (hole.Height - size.Height) / 2);
            }
            else
            {
                p = new Point(hole.Left - gap - size.Width, hole.Y + (hole.Height - size.Height) / 2);
            }
            p.X = Math.Max(area.Left, Math.Min(p.X, area.Right - size.Width));
            p.Y = Math.Max(area.Top, Math.Min(p.Y, area.Bottom - size.Height));
            m_Card.Location = p;
        }

        private void Finish(bool completed)
        {
            if (m_Done)
                return;
            m_Done = true;
            m_Owner.LocationChanged -= m_OwnerChanged;
            m_Owner.SizeChanged -= m_OwnerChanged;
            m_Owner.FormClosing -= OwnerClosing;
            try
            {
                m_Card.Close();
                m_Blocker.Close();
                m_Dim.Close();
                m_Owner.Activate();
            }
            catch (Exception swallowed)
            {
                Ares.Settings.ErrorLog.Note(swallowed);
            }
            if (m_Finished != null)
                m_Finished(completed);
        }
    }

    public partial class Player
    {
        private GuidedTour m_Tour;

        private static Rectangle TourOf(Control c)
        {
            if (c == null || !c.Visible || !c.IsHandleCreated || c.Width < 4 || c.Height < 4)
                return Rectangle.Empty;
            return c.RectangleToScreen(c.ClientRectangle);
        }

        private static Rectangle TourOf(ToolStrip strip, ToolStripItem item)
        {
            if (strip == null || item == null || !item.Visible || !strip.Visible || item.Bounds.Width < 4 || item.Bounds.Height < 4
                || item.Placement != ToolStripItemPlacement.Main)
                return Rectangle.Empty;
            return strip.RectangleToScreen(item.Bounds);
        }

        private List<TourStep> BuildTourSteps()
        {
            List<TourStep> steps = new List<TourStep>();
            Action<string, string, Func<Rectangle>> add = (title, text, target) =>
                steps.Add(new TourStep { Title = title, Text = text, Target = target });

            steps.Add(new TourStep
            {
                Centered = true,
                Title = T("Bem-vindo ao Apolo!", "Welcome to Apolo!"),
                Text = T("O Apolo é um controlador de músicas para mesas de RPG: você organiza músicas e efeitos em pastas e botões e toca tudo com um clique.\n\nEle NÃO vem com músicas. Você traz as suas ou baixa pacotes prontos (veja mais adiante).\n\nEste tour leva uns 2 minutos. Se preferir, pule agora: dá para refazer quando quiser em Sobre > Tour guiado.",
                         "Apolo is a music controller for tabletop RPG: you organize music and effects in folders and buttons and play everything with one click.\n\nIt does NOT come with music. You bring your own or download ready-made packs (see further on).\n\nThis tour takes about 2 minutes. You can skip it now and do it again whenever you want in About > Guided tour.")
            });
            add(T("Menu Projeto", "Project menu"),
                T("Aqui você cria e abre projetos, importa, exporta e baixa pacotes prontos (pastas, botões e músicas livres) para começar mais rápido.",
                  "Here you create and open projects, import, export and download ready-made packs (folders, buttons and free music) to start faster."),
                () => TourOf(menuStrip1, projectToolStripMenuItem));
            add(T("Pastas", "Folders"),
                T("As pastas agrupam suas playlists (por exemplo Exploração, Ação, Taverna). Clique numa pasta para ver os botões dela. Pastas podem ter subpastas.",
                  "Folders group your playlists (for example Exploration, Action, Tavern). Click a folder to see its buttons. Folders can have subfolders."),
                () => TourOf(modesList));
            add(T("Botões", "Buttons"),
                T("Cada cartão é uma playlist ou um efeito sonoro. Clique para tocar, clique de novo para parar.\n\n♫ é música (toca ao fundo) e ⚡ é efeito (toca por cima da música).",
                  "Each card is a playlist or a sound effect. Click to play, click again to stop.\n\n♫ is music (plays in the background) and ⚡ is an effect (plays over the music)."),
                () => TourOf(elementsPanel));
            add(T("Controles da música", "Music controls"),
                T("Pausar, voltar para a música anterior, pular para a próxima e parar tudo.",
                  "Pause, go back to the previous piece, skip to the next one and stop everything."),
                () => TourOf(m_TransportStrip));
            add(T("O que está tocando", "Now playing"),
                T("Mostra a música atual e quanto já tocou.", "Shows the current piece and how far it has played."),
                () => TourOf(m_MusicProgressBar));
            add(T("Volumes", "Volumes"),
                T("Três volumes separados: Geral (tudo), Música e Sons (efeitos). Quando um deles fica quase no zero, ele fica vermelho para você não achar que o programa quebrou.",
                  "Three separate volumes: Overall (everything), Music and Sounds (effects). When one is near zero it turns red, so you do not think the program is broken."),
                () => m_VolumePanel == null ? Rectangle.Empty : TourOf(m_VolumePanel));
            add(T("Nivelador de volume", "Volume leveler"),
                T("Um compressor suave que deixa as músicas com volume parecido (as baixas sobem, as altas descem) e evita estouros. Vale também para o que vai para a live. Desligue aqui se quiser o som original dos arquivos.",
                  "A gentle compressor that makes pieces about equally loud (quiet ones come up, loud ones go down) and avoids peaks. It also applies to what goes to the stream. Turn it off here for the original sound of the files."),
                () => m_LevelToggle == null ? Rectangle.Empty : TourOf(m_LevelToggle));
            add(T("Modo de edição", "Edit mode"),
                T("Ligue para criar e mudar pastas e botões: arrastar para reorganizar, trocar imagem, emoji e cor, renomear e excluir. Uma borda amarela mostra que está ligado. Errou? Ctrl+Z desfaz.",
                  "Turn it on to create and change folders and buttons: drag to reorganize, change picture, emoji and color, rename and delete. A yellow border shows it is on. Made a mistake? Ctrl+Z undoes it."),
                () => TourOf(menuStrip1, m_EditButton));
            steps.Add(new TourStep
            {
                Centered = true,
                Title = T("Trazendo suas músicas", "Bringing your music"),
                Text = T("Arraste arquivos ou pastas de áudio do Explorer para a janela do Apolo. Na janelinha que aparece, escolha se viram playlists de música ou efeitos sonoros.\n\nO Apolo guarda tudo sozinho a cada mudança.",
                         "Drag audio files or folders from Explorer into the Apolo window. In the small window that appears, choose whether they become music playlists or sound effects.\n\nApolo saves everything by itself after each change.")
            });
            steps.Add(new TourStep
            {
                Centered = true,
                Title = T("Pacotes prontos", "Ready-made packs"),
                Text = T("Sem músicas ainda? Abra Projeto > Pacotes de conteúdo prontos. Escolha um pacote na lista e clique em baixar: o Apolo traz pastas, botões, imagens e músicas livres (com os créditos dos autores) e já abre o projeto.\n\nOs pacotes ficam na internet, por isso o instalador continua pequeno.",
                         "No music yet? Open Project > Ready-made content packs. Pick a pack from the list and click download: Apolo brings folders, buttons, pictures and free music (with the credits of the authors) and opens the project.\n\nPacks live on the internet, which is why the installer stays small.")
            });
            add(T("Saída de áudio", "Audio output"),
                T("Escolha em qual placa de som o Apolo toca, ou \"não ouvir neste PC\" para só enviar o som para a live (Discord, VDO.Ninja, OBS).",
                  "Choose which sound card Apolo plays on, or \"do not listen on this PC\" to only send the sound to the stream (Discord, VDO.Ninja, OBS)."),
                () => TourOf(menuStrip1, m_OutputButton));
            add(T("Menu Extras", "Extras menu"),
                T("Pad flutuante (botões da pasta por cima da tela, com um atalho), controle remoto pelo celular, Stream Deck, Discord, OBS, banco de imagens para os botões, ferramentas para renomear músicas e checar direitos autorais.",
                  "Floating pad (the buttons of a folder on top of the screen, with a shortcut), remote control from your phone, Stream Deck, Discord, OBS, the image bank for the buttons, tools to rename music and check copyright."),
                () => TourOf(menuStrip1, extrasToolStripMenuItem));
            add(T("Lista de faixas", "Track list"),
                T("As músicas do botão selecionado, com busca. A coluna Direitos é um semáforo que indica o risco de a música dar problema em lives (é só uma indicação). F9 abre e fecha este painel.",
                  "The pieces of the selected button, with search. The Rights column is a traffic light for the risk of a piece causing trouble on streams (just an indication). F9 opens and closes this panel."),
                () => TourOf(m_TrackPanel));
            add(T("Sobre", "About"),
                T("Aqui você refaz este tour, vê o tutorial em texto, o código-fonte e os créditos do programa.",
                  "Here you can repeat this tour, see the text tutorial, the source code and the credits of the program."),
                () => TourOf(menuStrip1, helpToolStripMenuItem));
            steps.Add(new TourStep
            {
                Centered = true,
                Title = T("Pronto!", "Done!"),
                Text = T("Para começar, arraste uma pasta de músicas para cá. Ou abra Projeto > Pacotes de conteúdo prontos e baixe pastas, botões e músicas livres para testar.\n\nBom jogo!",
                         "To start, drag a folder of music here. Or open Project > Ready-made content packs and download folders, buttons and free music to try.\n\nHave a good game!")
            });
            return steps;
        }

        // firstTime: opened by the start of Apolo (the choice is kept so that it does not open again)
        private void ShowGuidedTour(bool firstTime)
        {
            if (m_Tour != null)
                return;
            if (WindowState == FormWindowState.Minimized || !Visible)
                return;
            m_Tour = new GuidedTour(this, BuildTourSteps(), (pt, en) => T(pt, en), completed =>
            {
                m_Tour = null;
                Ares.CommonGUI.Preferences.SetBool("TutorialSeen", true);
                // after the first tour, the ready-made packs are offered (once)
                BeginInvoke((MethodInvoker)(() => OfferPacksFirstTime()));
            });
            m_Tour.Start();
        }
    }
}
