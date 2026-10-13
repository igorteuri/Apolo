using System;
using System.Drawing;
using System.Windows.Forms;

namespace Ares.Player
{
    // Closing the window only hides the Player to the Windows notification area (system tray);
    // music and sounds keep playing. "Exit" (menu or tray) really quits.
    public partial class Player
    {
        private bool m_Quitting = false;
        private bool m_TrayHintShown = false;
        private ToolStripMenuItem m_CloseToTrayItem;
        private ContextMenuStrip m_TrayMenu;
        private ToolStripMenuItem m_TrayPause;

        private static bool CloseToTrayEnabled
        {
            get { return Ares.CommonGUI.Preferences.GetBool("CloseToTray", true); }
        }

        private Timer m_TrayTimer;
        private Ares.CommonGUI.ApoloState m_TrayState = Ares.CommonGUI.ApoloState.Idle;
        private int m_TrayPhase;

        private static int TrayIconSize
        {
            get { return Math.Max(16, SystemInformation.SmallIconSize.Width); }
        }

        // the tray icon reacts to what is happening: stopped (grey), playing (sun with moving rays), paused
        private void UpdateTrayIcon()
        {
            try
            {
                Ares.Players.PlayingControl control = m_PlayingControl;
                Ares.CommonGUI.ApoloState state = Ares.CommonGUI.ApoloState.Idle;
                if (control != null)
                {
                    double pos, len;
                    bool active = control.GetMusicPosition(out pos, out len) || control.GetRunningSounds().Count > 0;
                    if (active)
                        state = control.IsPaused() ? Ares.CommonGUI.ApoloState.Paused : Ares.CommonGUI.ApoloState.Playing;
                }
                int phase = state == Ares.CommonGUI.ApoloState.Playing ? (m_TrayPhase = (m_TrayPhase + 1) % 4) : 0;
                if (state == m_TrayState && state != Ares.CommonGUI.ApoloState.Playing)
                    return;
                m_TrayState = state;
                notifyIcon.Icon = Ares.CommonGUI.ApoloBrand.GetIcon(TrayIconSize, state, phase);
                notifyIcon.Text = "Apolo - " + (state == Ares.CommonGUI.ApoloState.Playing ? T("tocando", "playing")
                    : state == Ares.CommonGUI.ApoloState.Paused ? T("pausado", "paused") : T("parado", "stopped"));
            }
            catch (Exception)
            {
                // the icon is only informative
            }
        }

        private void InitTray()
        {
            try
            {
                this.Icon = Ares.CommonGUI.ApoloBrand.GetIcon(32, Ares.CommonGUI.ApoloState.Playing, 0);
                notifyIcon.Icon = Ares.CommonGUI.ApoloBrand.GetIcon(TrayIconSize, Ares.CommonGUI.ApoloState.Idle, 0);
                notifyIcon.Text = "Apolo";
                m_TrayTimer = new Timer();
                m_TrayTimer.Interval = 280;
                m_TrayTimer.Tick += (s, e) => UpdateTrayIcon();
                m_TrayTimer.Start();
                notifyIcon.Visible = true; // always shown in the tray

                m_TrayMenu = new ContextMenuStrip();
                m_TrayMenu.Items.Add(T("Abrir Apolo", "Open Apolo"), null, (s, e) => RestoreFromTray());
                m_TrayPause = new ToolStripMenuItem(T("Pausar / continuar", "Pause / resume"));
                m_TrayPause.Click += (s, e) =>
                {
                    Ares.Players.PlayingControl control = m_PlayingControl;
                    if (control != null)
                        control.TogglePause();
                };
                m_TrayMenu.Items.Add(m_TrayPause);
                m_TrayMenu.Items.Add(T("Parar tudo", "Stop all"), null, (s, e) => StopAllPlaying());
                m_TrayMenu.Items.Add(new ToolStripSeparator());
                m_TrayMenu.Items.Add(T("Sair", "Exit"), null, (s, e) => QuitApplication());
                notifyIcon.ContextMenuStrip = m_TrayMenu;

                // option in the Extras menu
                m_CloseToTrayItem = new ToolStripMenuItem(T("Fechar minimiza para a bandeja", "Closing minimizes to the tray"));
                m_CloseToTrayItem.Name = "closeToTrayItem";
                m_CloseToTrayItem.CheckOnClick = true;
                m_CloseToTrayItem.Checked = CloseToTrayEnabled;
                m_CloseToTrayItem.CheckedChanged += (s, e) =>
                    Ares.CommonGUI.Preferences.SetBool("CloseToTray", m_CloseToTrayItem.Checked);
                extrasToolStripMenuItem.DropDownItems.Insert(2, m_CloseToTrayItem);
                Ares.CommonGUI.LanguageManager.AddMenu(extrasToolStripMenuItem, 3);
                notifyIcon.MouseClick += (s, e) =>
                {
                    if (e.Button == MouseButtons.Left)
                        RestoreFromTray();
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }

        // returns true when the close request was turned into "hide to tray"
        private bool TryHideToTray(FormClosingEventArgs e)
        {
            if (m_Quitting || !CloseToTrayEnabled || e.CloseReason != CloseReason.UserClosing)
                return false;
            e.Cancel = true;
            HideToTray();
            return true;
        }

        private void HideToTray()
        {
            notifyIcon.Visible = true;
            Hide();
            if (!m_TrayHintShown)
            {
                m_TrayHintShown = true;
                notifyIcon.ShowBalloonTip(3000, "Apolo",
                    T("O Apolo continua tocando na bandeja do Windows. Clique no ícone para abrir; botão direito > Sair para fechar de vez.",
                      "Apolo keeps playing in the Windows tray. Click the icon to open; right-click > Exit to quit."),
                    ToolTipIcon.Info);
            }
        }

        private void RestoreFromTray()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        private void QuitApplication()
        {
            m_Quitting = true;
            Close();
        }
    }
}
