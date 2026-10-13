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
                // 1. one row for menus and toolbar: the useful toolbar items move to the right end of the menu bar,
                //    the others (open, messages, editor, settings, about) are already in the menus
                if (m_OutputButton != null)
                {
                    toolStrip1.Items.Remove(m_OutputButton);
                    menuStrip1.Items.Add(m_OutputButton);
                }
                if (m_AddButton != null)
                {
                    toolStrip1.Items.Remove(m_AddButton);
                    m_AddButton.Alignment = ToolStripItemAlignment.Right;
                    menuStrip1.Items.Add(m_AddButton);
                }
                if (m_EditButton != null)
                {
                    toolStrip1.Items.Remove(m_EditButton);
                    m_EditButton.Alignment = ToolStripItemAlignment.Right;
                    menuStrip1.Items.Add(m_EditButton);
                }
                // quick access to the sharing options (left of the edit buttons)
                menuStrip1.Items.Add(new ToolStripSeparator { Alignment = ToolStripItemAlignment.Right });
                m_NetButton = MakeQuickButton("netButton", T("Controle remoto / compartilhamento em rede...", "Remote control / network sharing..."), () => ShowRemoteDialog());
                m_VdoButton = MakeQuickButton("vdoButton", T("Enviar áudio por VDO.Ninja...", "Send audio through VDO.Ninja..."), () => ShowWebWindow(true));
                m_DiscordButton = MakeQuickButton("discordButton", T("Conectar ao Discord (tocar num canal de voz)...", "Connect to Discord (play in a voice channel)..."), () => ShowDiscordDialog());
                Ares.CommonGUI.IconLibrary.Bind(menuStrip1, new Dictionary<ToolStripItem, string>
                {
                    { m_NetButton, "globe" },
                    { m_VdoButton, "send" },
                    { m_DiscordButton, "headphones" }
                });
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

        private ToolStripButton MakeQuickButton(string name, string tip, Action click)
        {
            ToolStripButton b = new ToolStripButton();
            b.Name = name;
            b.DisplayStyle = ToolStripItemDisplayStyle.Image;
            b.ImageScaling = ToolStripItemImageScaling.None;
            b.ToolTipText = tip;
            b.Alignment = ToolStripItemAlignment.Right;
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
