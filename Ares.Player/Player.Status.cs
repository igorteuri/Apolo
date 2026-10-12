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
        private ToolStripStatusLabel m_StatState, m_StatRepeat, m_StatOutput, m_StatNetwork, m_StatProject;
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
                m_StatNetwork = new ToolStripStatusLabel();
                m_StatNetwork.IsLink = false;
                m_StatNetwork.ToolTipText = T("Clique para ver os endereços de rede", "Click to see the network addresses");
                m_StatNetwork.Click += (s, e) => ShowNetworkDialog();
                m_StatProject = new ToolStripStatusLabel();
                m_StatProject.Spring = true;
                m_StatProject.TextAlign = ContentAlignment.MiddleRight;
                foreach (ToolStripStatusLabel l in new ToolStripStatusLabel[] { m_StatState, m_StatRepeat, m_StatOutput, m_StatNetwork })
                {
                    l.BorderSides = ToolStripStatusLabelBorderSides.Right;
                    l.Padding = new Padding(4, 0, 8, 0);
                }
                m_StatusStrip.Items.AddRange(new ToolStripItem[] { m_StatState, m_StatRepeat, m_StatOutput, m_StatNetwork, m_StatProject });
                Controls.Add(m_StatusStrip);

                ToolStripMenuItem net = new ToolStripMenuItem(T("Rede...", "Network..."));
                net.Click += (s, e) => ShowNetworkDialog();
                extrasToolStripMenuItem.DropDownItems.Insert(2, net);

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
                string web = webAddressLabel.Text;
                SetStat(m_StatNetwork, T("Rede: ", "Network: ") + (string.IsNullOrEmpty(web) ? T("desligada", "off") : web), ForeColor);
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

        private void ShowNetworkDialog()
        {
            using (Form f = new Form())
            {
                f.Text = T("Rede", "Network");
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.StartPosition = FormStartPosition.CenterParent;
                f.MaximizeBox = false;
                f.MinimizeBox = false;
                f.ShowInTaskbar = false;
                f.ClientSize = new Size(520, 190);
                f.Icon = Ares.CommonGUI.ApoloBrand.GetIcon(32, Ares.CommonGUI.ApoloState.Playing, 0);

                Label l1 = new Label();
                l1.Text = T("Controle pelo navegador (web):", "Browser (web) control:");
                l1.AutoSize = true;
                l1.Location = new Point(16, 18);
                f.Controls.Add(l1);
                LinkLabel link = new LinkLabel();
                string web = webAddressLabel.Text;
                link.Text = string.IsNullOrEmpty(web) ? T("(desligado nas configurações)", "(off in the settings)") : web;
                link.AutoSize = true;
                link.Location = new Point(32, 42);
                link.Enabled = !string.IsNullOrEmpty(web);
                link.LinkClicked += (s, e) =>
                {
                    try { System.Diagnostics.Process.Start(web); } catch (Exception) { }
                };
                f.Controls.Add(link);

                Label l2 = new Label();
                l2.Text = T("Porta do controle remoto (rede):", "Remote control port (network):");
                l2.AutoSize = true;
                l2.Location = new Point(16, 78);
                f.Controls.Add(l2);
                Label port = new Label();
                port.Text = string.IsNullOrEmpty(controllerPortLabel.Text) ? T("(desligada)", "(off)") : controllerPortLabel.Text;
                port.AutoSize = true;
                port.Location = new Point(32, 102);
                f.Controls.Add(port);

                Button ok = new Button();
                ok.Text = "OK";
                ok.DialogResult = DialogResult.OK;
                ok.SetBounds(520 - 16 - 90, 190 - 16 - 28, 90, 28);
                f.Controls.Add(ok);
                f.AcceptButton = ok;
                f.CancelButton = ok;
                Ares.CommonGUI.ThemeManager.Apply(f);
                f.ShowDialog(this);
            }
        }
    }
}
