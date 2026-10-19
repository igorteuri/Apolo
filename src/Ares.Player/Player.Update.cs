using System;
using System.IO;
using System.Net;
using System.Windows.Forms;

namespace Ares.Player
{
    // "New version available" notice. Apolo reads ONE small public file (a few bytes, with the number of the newest
    // version) at most once a day and sends nothing about the user. Nothing happens while UpdateInfoUrl is empty.
    public partial class Player
    {
        /// <summary>
        /// Address of the public file {"version": "0.85", "url": "https://..."} which tells the newest version.
        /// Empty = the program does not look for updates.
        /// </summary>
        private const string UpdateInfoUrl = "";

        private ToolStripStatusLabel m_StatUpdate;
        private string m_NewVersion;
        private string m_NewVersionUrl;

        private static bool UpdateCheckAvailable
        {
            get { return UpdateInfoUrl.Length > 0; }
        }

        private static bool UpdateCheckEnabled
        {
            get { return UpdateCheckAvailable && Ares.CommonGUI.Preferences.GetBool("UpdateCheck", true); }
        }

        private void InitUpdateCheck()
        {
            if (!UpdateCheckAvailable)
                return;
            try
            {
                ToolStripMenuItem item = new ToolStripMenuItem(T("Avisar quando houver versão nova", "Tell me when a new version exists"));
                item.Name = "updateCheckItem";
                item.CheckOnClick = true;
                item.Checked = UpdateCheckEnabled;
                item.CheckedChanged += (s, e) => Ares.CommonGUI.Preferences.SetBool("UpdateCheck", item.Checked);
                extrasToolStripMenuItem.DropDownItems.Insert(2, item);

                m_StatUpdate = new ToolStripStatusLabel();
                m_StatUpdate.Visible = false;
                m_StatUpdate.BorderSides = ToolStripStatusLabelBorderSides.Right;
                m_StatUpdate.Padding = new Padding(4, 0, 8, 0);
                m_StatUpdate.Click += (s, e) => ShowUpdateInfo();
                m_StatusStrip.Items.Insert(m_StatusStrip.Items.IndexOf(m_StatProject), m_StatUpdate);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }

        // a few seconds after the window appeared; not more than once a day
        private void StartUpdateCheckLater()
        {
            if (!UpdateCheckEnabled)
                return;
            Timer later = new Timer();
            later.Interval = 6000;
            later.Tick += (s, e) =>
            {
                later.Stop();
                later.Dispose();
                try
                {
                    long last;
                    long.TryParse(Ares.CommonGUI.Preferences.Get("UpdateCheckLast", "0"), out last);
                    if (DateTime.UtcNow.Ticks - last < TimeSpan.TicksPerDay)
                        return;
                    Ares.CommonGUI.Preferences.Set("UpdateCheckLast", DateTime.UtcNow.Ticks.ToString());
                    System.Threading.ThreadPool.QueueUserWorkItem(o => FetchUpdateInfo());
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                }
            };
            later.Start();
        }

        private void FetchUpdateInfo()
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(UpdateInfoUrl);
                request.Timeout = 8000;
                request.ReadWriteTimeout = 8000;
                request.UserAgent = "Apolo";
                string body;
                using (WebResponse response = request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                    body = reader.ReadToEnd();
                string version, url;
                if (!UpdateVersion.TryReadInfo(body, out version, out url))
                    return;
                if (!UpdateVersion.IsNewer(version, Application.ProductVersion))
                    return;
                BeginInvoke(new MethodInvoker(() => ShowNewVersion(version, url)));
            }
            catch (Exception ex)
            {
                // no internet or no answer: nothing to say
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }

        private void ShowNewVersion(string version, string url)
        {
            m_NewVersion = version;
            m_NewVersionUrl = url;
            if (m_StatUpdate != null)
            {
                m_StatUpdate.Text = "⬆ " + T("Nova versão: ", "New version: ") + version;
                m_StatUpdate.ToolTipText = T("Clique para ver como atualizar.", "Click to see how to update.");
                Ares.CommonGUI.ThemePalette p = Ares.CommonGUI.ThemeManager.Current;
                m_StatUpdate.ForeColor = p != null && !p.IsClassic ? p.Accent : System.Drawing.Color.DarkGreen;
                m_StatUpdate.Visible = true;
            }
            ShowToast(new Ares.Players.Message(Ares.Players.MessageType.Info,
                T("Há uma versão nova do Apolo: " + version + ". Veja em Ajuda > Como atualizar.",
                  "A new version of Apolo exists: " + version + ". See Help > How to update.")));
        }
    }
}
