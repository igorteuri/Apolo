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

        private void InitTray()
        {
            try
            {
                if (notifyIcon.Icon == null)
                    notifyIcon.Icon = this.Icon;
                notifyIcon.Text = "Ares";

                m_TrayMenu = new ContextMenuStrip();
                m_TrayMenu.Items.Add(T("Abrir Ares", "Open Ares"), null, (s, e) => RestoreFromTray());
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
                notifyIcon.ShowBalloonTip(3000, "Ares",
                    T("O Ares continua tocando na bandeja do Windows. Clique no ícone para abrir; botão direito > Sair para fechar de vez.",
                      "Ares keeps playing in the Windows tray. Click the icon to open; right-click > Exit to quit."),
                    ToolTipIcon.Info);
            }
        }

        private void RestoreFromTray()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
            if (!m_HideToTray)
                notifyIcon.Visible = false;
        }

        private void QuitApplication()
        {
            m_Quitting = true;
            Close();
        }
    }
}
