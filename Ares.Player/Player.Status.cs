using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Ares.Player
{
    // Status bar at the bottom of the window (state, repeat, output device, network, project)
    // and the network information window (Extras > Network), which replaces the old network box.
    public partial class Player
    {
        private StatusStrip m_StatusStrip;
        private ToolStripStatusLabel m_StatState, m_StatRepeat, m_StatOutput, m_StatShare, m_StatDiscord, m_StatProject;
        private Timer m_StatusTimer;

        private void InitStatusBar()
        {
            try
            {
                // the old network box is gone: give its room to the state box
                int extra = (groupBox1.Top + groupBox1.Height) - (groupBox3.Top + groupBox3.Height);
                groupBox1.Visible = false;
                if (extra > 0)
                    groupBox3.Height += extra;

                m_StatusStrip = new StatusStrip();
                m_StatusStrip.Name = "apoloStatusStrip";
                m_StatusStrip.SizingGrip = false;
                m_StatusStrip.Dock = DockStyle.Bottom;
                m_StatState = new ToolStripStatusLabel();
                m_StatRepeat = new ToolStripStatusLabel();
                m_StatOutput = new ToolStripStatusLabel();
                m_StatShare = new ToolStripStatusLabel();
                m_StatShare.ToolTipText = T("Controle remoto / compartilhamento. Clique para abrir.", "Remote control / sharing. Click to open.");
                m_StatShare.Click += (s, e) => ShowRemoteDialog();
                m_StatDiscord = new ToolStripStatusLabel();
                m_StatDiscord.ToolTipText = T("Discord: tocar o Apolo num canal de voz. Clique para abrir.", "Discord: play Apolo in a voice channel. Click to open.");
                m_StatDiscord.Click += (s, e) => ShowDiscordDialog();
                m_StatProject = new ToolStripStatusLabel();
                m_StatProject.Spring = true;
                m_StatProject.TextAlign = ContentAlignment.MiddleRight;
                foreach (ToolStripStatusLabel l in new ToolStripStatusLabel[] { m_StatState, m_StatRepeat, m_StatOutput, m_StatShare, m_StatDiscord })
                {
                    l.BorderSides = ToolStripStatusLabelBorderSides.Right;
                    l.Padding = new Padding(4, 0, 8, 0);
                }
                m_StatusStrip.Items.AddRange(new ToolStripItem[] { m_StatState, m_StatRepeat, m_StatOutput, m_StatShare, m_StatDiscord, m_StatProject });
                Controls.Add(m_StatusStrip);

                m_StatusTimer = new Timer();
                m_StatusTimer.Interval = 500;
                m_StatusTimer.Tick += (s, e) => UpdateStatusBar();
                m_StatusTimer.Start();

                // when the window is shown: make room for the bar (controls anchored to the bottom would be under it)
                Shown += (s, e) => MakeRoomForStatusBar();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }

        private void MakeRoomForStatusBar()
        {
            int sh = m_StatusStrip.Height;
            List<Control> bottoms = new List<Control>();
            foreach (Control c in Controls)
            {
                if (c == m_StatusStrip || !c.Visible && c != groupBox1)
                    continue;
                if ((c.Anchor & AnchorStyles.Bottom) != 0)
                    bottoms.Add(c);
            }
            SuspendLayout();
            if (WindowState == FormWindowState.Normal)
            {
                MinimumSize = new Size(MinimumSize.Width, MinimumSize.Height > 0 ? MinimumSize.Height + sh : 0);
                Height += sh;
            }
            foreach (Control c in bottoms)
            {
                if ((c.Anchor & AnchorStyles.Top) != 0)
                    c.Height -= sh;
                else
                    c.Top -= sh;
            }
            ResumeLayout(true);
            LayoutMainColumns();
            UpdateStatusBar();
        }

        private void UpdateStatusBar()
        {
            try
            {
                if (m_StatusStrip == null || !Visible)
                    return;
                string state;
                Color color = ForeColor;
                Ares.CommonGUI.ThemePalette p = Ares.CommonGUI.ThemeManager.Current;
                if (m_TrayState == Ares.CommonGUI.ApoloState.Playing)
                {
                    state = "● " + T("Tocando", "Playing");
                    color = p != null && !p.IsClassic ? p.Accent : Color.DarkGreen;
                }
                else if (m_TrayState == Ares.CommonGUI.ApoloState.Paused)
                {
                    state = "❚❚ " + T("Pausado", "Paused");
                }
                else
                {
                    state = "■ " + T("Parado", "Stopped");
                    if (p != null && !p.IsClassic)
                        color = p.TextDim;
                }
                SetStat(m_StatState, state, color);
                SetStat(m_StatRepeat, "↻ " + T("Repetição: ", "Repeat: ") + (repeatButton.Checked ? T("ligada", "on") : T("desligada", "off")), ForeColor);
                SetStat(m_StatOutput, "🔊 " + (m_OutputButton != null ? m_OutputButton.Text : ""), ForeColor);
                if (m_Remote != null && m_Remote.Running)
                {
                    bool ready = m_Tunnel != null && m_Tunnel.Url != null;
                    SetStat(m_StatShare, "🔗 " + (ready ? T("Compartilhamento: ativo", "Sharing: on") : T("Compartilhamento: iniciando...", "Sharing: starting...")),
                        ready && p != null && !p.IsClassic ? p.Accent : ForeColor);
                }
                else
                {
                    SetStat(m_StatShare, "🔗 " + T("Compartilhamento: inativo", "Sharing: off"), p != null && !p.IsClassic ? p.TextDim : ForeColor);
                }
                bool discordActive;
                string discordText = DiscordStatusText(out discordActive);
                SetStat(m_StatDiscord, "🎧 " + discordText, discordActive && p != null && !p.IsClassic ? p.Accent
                    : (m_Discord != null && m_Discord.State == DiscordState.Off && p != null && !p.IsClassic ? p.TextDim : ForeColor));
                SetStat(m_StatProject, m_Project != null ? m_Project.Title : "", ForeColor);
            }
            catch (Exception)
            {
                // only informative
            }
        }

        private static void SetStat(ToolStripStatusLabel label, string text, Color color)
        {
            if (label.Text != text)
                label.Text = text;
            if (label.ForeColor != color)
                label.ForeColor = color;
        }
    }
}
