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

        private int HeaderHeight
        {
            get { return Math.Max(58, Font.Height * 3 + 8); }
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
                m_HeaderSpacer = new ToolStripLabel();
                m_HeaderSpacer.Name = "headerSpacer";
                m_HeaderSpacer.AutoSize = false;
                m_HeaderSpacer.Width = 60;
                m_HeaderSpacer.Margin = new Padding(0);
                m_HeaderSpacer.Enabled = false;
                menuStrip1.Items.Add(m_HeaderSpacer);
                m_DiscordButton = MakeQuickButton("discordButton", T("Discord: conectar o Apolo a um canal de voz", "Discord: connect Apolo to a voice channel"), () => ShowDiscordDialog());
                m_VdoButton = MakeQuickButton("vdoButton", T("VDO.Ninja: enviar o áudio do Apolo pela internet (Foundry, jogadores)", "VDO.Ninja: send the Apolo audio over the internet (Foundry, players)"), () => ShowWebWindow(true));
                m_NetButton = MakeQuickButton("netButton", T("Controle remoto e compartilhamento em rede (celular / internet)", "Remote control and network sharing (phone / internet)"), () => ShowRemoteDialog());
                Ares.CommonGUI.IconLibrary.Bind(menuStrip1, new Dictionary<ToolStripItem, string>
                {
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

        private ToolStripButton m_NetButton, m_VdoButton, m_DiscordButton;
        private ToolStripLabel m_HeaderSpacer;
        private bool m_Centering;

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
                foreach (ToolStripItem item in menuStrip1.Items)
                {
                    if (item == m_HeaderSpacer) { afterSpacer = true; continue; }
                    if (!item.Visible || item.Alignment != ToolStripItemAlignment.Left)
                        continue;
                    int w = item.Width + item.Margin.Horizontal;
                    if (afterSpacer) groupWidth += w; else before += w;
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
                string file = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Ares", "Icons", name + ".png");
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
            catch (Exception)
            {
            }
        }

        private ToolStripButton MakeQuickButton(string name, string tip, Action click)
        {
            ToolStripButton b = new ToolStripButton();
            b.Name = name;
            b.DisplayStyle = ToolStripItemDisplayStyle.Image;
            b.ImageScaling = ToolStripItemImageScaling.None;
            b.ToolTipText = tip;
            b.Alignment = ToolStripItemAlignment.Left;
            b.Margin = new Padding(14, 0, 14, 0);
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
            int headerTop = menuStrip1.Bottom + 6;
            int hh = HeaderHeight;

            int vw = 3 * VolumeColumnWidth + 8;
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
