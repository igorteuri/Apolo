using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Ares.Player
{
    // The header of the main window (inspired by the Kenku FM player bar):
    //   menu bar (with edit mode / add / audio output at its right end)
    //   [ transport buttons ] [ progress of the music ] [ volume: general / music / sounds ]
    // The left column below only shows the state of the program.
    public partial class Player
    {
        private Panel m_VolumePanel;
        private const int VolumeColumnWidth = 124;
        private const int LevelColumnWidth = 70;
        private Label m_LevelLabel;
        private Ares.CommonGUI.ModernToggle m_LevelToggle;

        private int HeaderHeight
        {
            get { return Math.Max(48, Font.Height + 32); } // name + percentage on one line over the slider
        }

        private void InitHeader()
        {
            try
            {
                // 1. one row for menus and toolbar: the useful toolbar items move into the menu bar,
                //    the others (open, messages, editor, settings, about) are already in the menus.
                //    Layout: menus | (space) [Discord] [VDO] [network] [edit mode] (space) | audio output (right end)
                menuStrip1.ShowItemToolTips = true;
                if (m_OutputButton != null)
                {
                    toolStrip1.Items.Remove(m_OutputButton);
                    m_OutputButton.Alignment = ToolStripItemAlignment.Right;
                    if (string.IsNullOrEmpty(m_OutputButton.ToolTipText) || m_OutputButton.ToolTipText == m_OutputButton.Text)
                        m_OutputButton.ToolTipText = T("Dispositivo de saída de áudio (onde o Apolo toca)", "Audio output device (where Apolo plays)");
                    menuStrip1.Items.Add(m_OutputButton);
                }
                InitGearButton();
                m_HeaderSpacer = new ToolStripLabel();
                m_HeaderSpacer.Name = "headerSpacer";
                m_HeaderSpacer.AutoSize = false;
                m_HeaderSpacer.Width = 60;
                m_HeaderSpacer.Margin = new Padding(0);
                m_HeaderSpacer.Enabled = false;
                menuStrip1.Items.Add(m_HeaderSpacer);
                m_DiscordButton = MakeQuickButton("discordButton", "Discord", T("Discord: conectar o Apolo a um canal de voz", "Discord: connect Apolo to a voice channel"), () => ShowDiscordDialog());
                m_VdoButton = MakeQuickButton("vdoButton", T("Enviar áudio", "Send audio"), T("VDO.Ninja: enviar o áudio do Apolo pela internet (Foundry, jogadores)", "VDO.Ninja: send the Apolo audio over the internet (Foundry, players)"), () => ShowWebWindow(true));
                m_ObsButton = MakeQuickButton("obsButton", "OBS", T("Overlay para o OBS: mostra a música que está tocando na sua live", "OBS overlay: shows the music which is playing on your stream"), () => ShowOverlayDialog());
                m_NetButton = MakeQuickButton("netButton", T("Compartilhar", "Share"), T("Controle remoto e compartilhamento em rede (celular / internet)", "Remote control and network sharing (phone / internet)"), () => ShowRemoteDialog());
                // thin separators make clear that these are three different buttons
                menuStrip1.Items.Insert(menuStrip1.Items.IndexOf(m_VdoButton), new ToolStripSeparator());
                menuStrip1.Items.Insert(menuStrip1.Items.IndexOf(m_NetButton), new ToolStripSeparator());
                menuStrip1.Items.Insert(menuStrip1.Items.IndexOf(m_ObsButton), new ToolStripSeparator());
                Ares.CommonGUI.IconLibrary.Bind(menuStrip1, new Dictionary<ToolStripItem, string>
                {
                    { m_GearButton, "settings" },
                    { m_ObsButton, "tv" },
                    { m_NetButton, "globe" },
                    { m_VdoButton, "send" },
                    { m_DiscordButton, "headphones" }
                });
                // own pictures (for example the official logos) put in %APPDATA%\Ares\Icons\discord.png / vdo.png / net.png win over the built-in icons
                ApplyCustomIcon(m_DiscordButton, "discord");
                ApplyCustomIcon(m_VdoButton, "vdo");
                ApplyCustomIcon(m_NetButton, "net");
                if (m_EditButton != null)
                {
                    toolStrip1.Items.Remove(m_EditButton);
                    m_EditButton.Alignment = ToolStripItemAlignment.Left;
                    m_EditButton.Margin = new Padding(14, 0, 14, 0);
                    if (string.IsNullOrEmpty(m_EditButton.ToolTipText) || m_EditButton.ToolTipText == m_EditButton.Text)
                        m_EditButton.ToolTipText = T("Modo de edição: criar e alterar playlists e elementos", "Edit mode: create and change playlists and elements");
                    m_EditSeparator = new ToolStripSeparator();
                    menuStrip1.Items.Add(m_EditSeparator);
                    menuStrip1.Items.Add(m_EditButton);
                }
                if (m_AddButton != null)
                {
                    toolStrip1.Items.Remove(m_AddButton);
                    m_AddButton.Alignment = ToolStripItemAlignment.Left;
                    m_AddButton.Margin = new Padding(14, 0, 14, 0);
                    menuStrip1.Items.Add(m_AddButton);
                }
                menuStrip1.Resize += (s2, e2) => CenterQuickButtons();
                menuStrip1.Layout += (s2, e2) => CenterQuickButtons();
                toolStrip1.Visible = false;

                // 2. bigger transport buttons
                if (m_TransportStrip != null)
                {
                    Ares.CommonGUI.IconLibrary.SetSizeFactor(m_TransportStrip, 1.45);
                    foreach (ToolStripItem item in m_TransportStrip.Items)
                        item.Padding = new Padding(5);
                    m_PauseShown = !m_PauseShown;
                    UpdatePauseButton(!m_PauseShown);
                }

                // 3. the three volume sliders move to the header (more knobs than a normal player)
                m_VolumePanel = new Panel();
                m_VolumePanel.Name = "volumePanel";
                m_VolumePanel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                Control[][] columns =
                {
                    new Control[] { label2, overallVolumeBar },
                    new Control[] { label3, musicVolumeBar },
                    new Control[] { label4, soundVolumeBar }
                };
                foreach (Control[] column in columns)
                {
                    foreach (Control c in column)
                    {
                        if (c.Parent != null)
                            c.Parent.Controls.Remove(c);
                        c.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                        m_VolumePanel.Controls.Add(c);
                    }
                }
                InitLeveler();
                InitVolumeLabels();
                Controls.Add(m_VolumePanel);
                groupBox2.Visible = false;

                // 4. the left column and the folders column are placed by LayoutMainColumns
                groupBox3.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                groupBox4.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                modesList.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                modesList.IntegralHeight = false;

                LayoutHeader();
                LayoutMainColumns();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }

        private ToolStripButton m_NetButton, m_VdoButton, m_DiscordButton, m_ObsButton;
        private ToolStripLabel m_HeaderSpacer;
        private ToolStripSeparator m_EditSeparator;
        private bool m_EditChromeHidden;

        // edit mode is about the playlists: in a web tab its button (and the Add button) are not shown, but keep their room so that the others do not move
        private void SetEditChromeVisible(bool visible)
        {
            m_EditChromeHidden = !visible;
            if (m_EditButton != null)
                m_EditButton.Visible = visible;
            if (m_EditSeparator != null)
                m_EditSeparator.Visible = visible;
            if (m_AddButton != null)
                m_AddButton.Visible = visible && m_EditMode;
            CenterQuickButtons();
        }
        private bool m_Centering;
        private bool m_MovingAppearance;

        // the spacer pushes the group of quick buttons towards the middle of the window
        private void CenterQuickButtons()
        {
            if (m_HeaderSpacer == null || m_Centering)
                return;
            try
            {
                m_Centering = true;
                int before = 0;
                int groupWidth = 0;
                bool afterSpacer = false;
                ToolStripItem appearance = null;
                foreach (ToolStripItem item in menuStrip1.Items)
                {
                    if (item == m_HeaderSpacer) { afterSpacer = true; continue; }
                    // the "Appearance" menu is added later by the theme: it belongs to the menus on the left
                    if (afterSpacer && item is ToolStripMenuItem && (item.Text == "Aparência" || item.Text == "Appearance"))
                        appearance = item;
                    // the Add button appears in edit mode: its room is kept, so that the other buttons do not move
                    bool reserved = (item == m_AddButton && !item.Visible)
                        || (m_EditChromeHidden && (item == m_EditButton || item == m_EditSeparator));
                    if ((!item.Visible && !reserved) || item.Alignment != ToolStripItemAlignment.Left)
                        continue;
                    int w = (reserved ? item.GetPreferredSize(Size.Empty).Width : item.Width) + item.Margin.Horizontal;
                    if (afterSpacer) groupWidth += w; else before += w;
                }
                if (appearance != null && !m_MovingAppearance)
                {
                    m_MovingAppearance = true;
                    ToolStripItem moving = appearance;
                    BeginInvoke(new MethodInvoker(() =>
                    {
                        try
                        {
                            menuStrip1.Items.Remove(moving);
                            menuStrip1.Items.Insert(menuStrip1.Items.IndexOf(m_HeaderSpacer), moving);
                        }
                        finally
                        {
                            m_MovingAppearance = false;
                        }
                    }));
                    return;
                }
                int wanted = menuStrip1.Width / 2 - groupWidth / 2 - before - menuStrip1.Padding.Left;
                int width = Math.Max(10, wanted);
                if (Math.Abs(m_HeaderSpacer.Width - width) > 1)
                    m_HeaderSpacer.Width = width;
            }
            finally
            {
                m_Centering = false;
            }
        }

        private void ApplyCustomIcon(ToolStripButton button, string name)
        {
            try
            {
                string file = System.IO.Path.Combine(Ares.Settings.AppFolders.Root, "Icons", name + ".png");
                if (!System.IO.File.Exists(file) || button.Image == null)
                    return;
                Size size = button.Image.Size;
                using (Image source = Image.FromFile(file))
                {
                    Bitmap bitmap = new Bitmap(size.Width, size.Height);
                    using (Graphics g = Graphics.FromImage(bitmap))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.DrawImage(source, 0, 0, size.Width, size.Height);
                    }
                    button.Image = bitmap;
                }
            }
            catch (Exception swallowed)
            {
                Ares.Settings.ErrorLog.Note(swallowed);
            }
        }

        private ToolStripButton MakeQuickButton(string name, string label, string tip, Action click)
        {
            ToolStripButton b = new ToolStripButton();
            b.Name = name;
            b.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
            b.Text = label;
            b.TextImageRelation = TextImageRelation.ImageBeforeText;
            b.ImageScaling = ToolStripItemImageScaling.None;
            b.ToolTipText = tip;
            b.Alignment = ToolStripItemAlignment.Left;
            b.Margin = new Padding(4, 0, 4, 0);
            b.Padding = new Padding(6, 0, 6, 0);
            b.Click += (s, e) => click();
            menuStrip1.Items.Add(b);
            return b;
        }

        // places the transport buttons, the progress bar and the volume sliders in the header
        private void LayoutHeader()
        {
            if (m_VolumePanel == null || m_MusicProgressBar == null)
                return;
            int margin = 12;
            int width = ClientSize.Width;
            if (width < 300)
                return;
            int headerTop = menuStrip1.Bottom + 4;
            int hh = HeaderHeight;

            int vw = 3 * VolumeColumnWidth + 8 + LevelColumnWidth;
            m_VolumePanel.SetBounds(width - margin - vw, headerTop, vw, hh);
            int labelHeight = Math.Max(16, Font.Height + 2);
            int sliderTop = labelHeight + 2;
            int sliderHeight = Math.Max(24, hh - sliderTop - 2);
            Control[][] columns =
            {
                new Control[] { label2, overallVolumeBar },
                new Control[] { label3, musicVolumeBar },
                new Control[] { label4, soundVolumeBar }
            };
            for (int i = 0; i < columns.Length; i++)
            {
                int x = 4 + i * VolumeColumnWidth;
                columns[i][0].AutoSize = false;
                columns[i][0].SetBounds(x + 4, 2, VolumeColumnWidth - 12, labelHeight);
                columns[i][1].SetBounds(x, sliderTop, VolumeColumnWidth - 8, sliderHeight);
            }

            if (m_LevelLabel != null && m_LevelToggle != null)
            {
                int lx = 4 + 3 * VolumeColumnWidth;
                m_LevelLabel.SetBounds(lx + 4, 2, LevelColumnWidth - 8, labelHeight);
                m_LevelToggle.SetBounds(lx + 4, sliderTop, LevelColumnWidth - 12, Math.Min(sliderHeight, 30));
            }

            int left = margin;
            if (m_TransportStrip != null)
            {
                m_TransportStrip.Left = margin;
                m_TransportStrip.Top = headerTop + Math.Max(0, (hh - m_TransportStrip.Height) / 2);
                left = m_TransportStrip.Right + 12;
            }
            int barWidth = m_VolumePanel.Left - 12 - left;
            if (barWidth > 80)
                m_MusicProgressBar.SetBounds(left, headerTop, barWidth, hh);
        }
    }
}
