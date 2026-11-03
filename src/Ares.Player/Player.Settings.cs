using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Ares.Data;
using Ares.Players;
using Ares.Settings;
using Ares.CommonGUI;

namespace Ares.Player
{
    // Reading and writing the settings, and applying them when they change.
    public partial class Player
    {
        private void WriteSettings()
        {
            try
            {
                Ares.Settings.Settings.Instance.WriteToFile(m_BasicSettings.GetSettingsDir());
                m_BasicSettings.WriteToFile();
            }
            catch (Exception ex)
            {
                Ares.CommonGUI.MessageDialog.Show(this, String.Format(StringResources.WriteSettingsError, ex.Message), StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }

        private void ReadSettings()
        {
            bool foundSettings = m_BasicSettings.ReadFromFile();
            bool hasSettings = Ares.Settings.Settings.Instance.Initialize(Ares.Settings.Settings.PlayerID, foundSettings ? m_BasicSettings.GetSettingsDir() : null);
            if (!hasSettings)
            {
                // first start: the default settings are kept (the guided tour explains the program) and written at once,
                // instead of the old "first start of the player" message with the settings window
                WriteSettings();
            }
            SettingsChanged(true);
#if !MONO
            Ares.Settings.Settings.Instance.SettingsChanged += new EventHandler<Settings.Settings.SettingsEventArgs>(SettingsChanged);
#endif
        }
		
#if !MONO
        void SettingsChanged(object sender, Settings.Settings.SettingsEventArgs e)
        {
            SettingsChanged(e.FundamentalChange);
        }
#endif

        private void UpdateClientState()
        {
            Ares.Settings.Settings settings = Ares.Settings.Settings.Instance;
            if (settings.UseWebNetwork)
            {
                System.Net.IPAddress address = null;
                if (System.Net.IPAddress.TryParse(settings.IPAddress, out address) && address != null && address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                {
                    String ipaddress = settings.IPAddress;
                    int index = ipaddress.IndexOf('%');
                    if (index != -1)
                        ipaddress = ipaddress.Substring(0, index);
                    webAddressLabel.Text = "http://[" + ipaddress + "]:" + settings.WebTcpPort + "/";
                }
                else
                {
                    webAddressLabel.Text = "http://" + settings.IPAddress + ":" + settings.WebTcpPort + "/";
                }
            }
            else
            {
                webAddressLabel.Text = "";
            }
            if (settings.UseLegacyNetwork)
            {
                controllerPortLabel.Text = "" + settings.TcpPort;
            }
            else
            {
                controllerPortLabel.Text = "";
            }
            if (settings.UseLegacyNetwork || settings.UseWebNetwork)
            {
                if (m_Network.ClientConnected)
                {
                    clientStateLabel.Text = StringResources.ConnectedWith + m_Network.ClientName;
                    clientStateLabel.ForeColor = System.Drawing.Color.DarkGreen;
                }
                else
                {
                    clientStateLabel.Text = StringResources.NotConnected;
                    clientStateLabel.ForeColor = System.Drawing.Color.Red;
                }
            }
            else
            {
                clientStateLabel.Text = StringResources.Disabled;
                clientStateLabel.ForeColor = System.Drawing.Color.Red;
            }
        }

        private void SettingsChanged(bool fundamentalChange)
        {
            if (this.IsInvokeRequired ())
            {
                Invoke(new MethodInvoker(() => SettingsChanged(fundamentalChange)));
                return;
            }
            Ares.Settings.Settings settings = Ares.Settings.Settings.Instance;
            if (fundamentalChange)
            {
                m_PlayingControl.KeyReceived((int)Ares.Data.Keys.Escape);
                m_PlayingControl.UpdateDirectories();
                LoadTagsDB();
                SetOutputDevice();
            }
            commitVolumes = false;
            overallVolumeBar.Value = settings.GlobalVolume;
            musicVolumeBar.Value = settings.MusicVolume;
            soundVolumeBar.Value = settings.SoundVolume;
            m_PlayingControl.GlobalVolume = settings.GlobalVolume;
            m_PlayingControl.MusicVolume = settings.MusicVolume;
            m_PlayingControl.SoundVolume = settings.SoundVolume;
            commitVolumes = true;
            commitFading = false;
            tagFadeUpDown.Value = settings.TagMusicFadeTime;
            fadeOnlyOnChangeBox.Checked = settings.TagMusicFadeOnlyOnChange;
            m_PlayingControl.SetMusicTagFading(settings.TagMusicFadeTime, settings.TagMusicFadeOnlyOnChange);
            m_PlayingControl.SetPlayMusicOnAllSpeakers(settings.PlayMusicOnAllSpeakers);
            m_PlayingControl.SetFadingOnPreviousNext(settings.ButtonMusicFadeMode != 0, settings.ButtonMusicFadeMode == 2, settings.ButtonMusicFadeTime);
            commitFading = true;
            if (fundamentalChange && m_Network != null)
            {
                if (m_Network.ClientConnected)
                {
                    m_Network.DisconnectClient(false);
                }
                m_Network.Shutdown();
                m_Network = new Networks(this, settings.UseLegacyNetwork, settings.UseWebNetwork);
                m_Network.InitConnectionData();
                if (Settings.Settings.Instance.UseWebNetwork || Settings.Settings.Instance.UseLegacyNetwork)
                {
                    m_Network.StartUdpBroadcast();
                    m_Network.ListenForClient();
                }
                UpdateClientState();
            }
        }
    }
}
