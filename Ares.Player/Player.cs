/*
 Copyright (c) 2015 [Joerg Ruedenauer]
 
 This file is part of Ares.

 Ares is free software; you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation; either version 2 of the License, or
 (at your option) any later version.

 Ares is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 GNU General Public License for more details.

 You should have received a copy of the GNU General Public License
 along with Ares; if not, write to the Free Software
 Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA
 */
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
	public partial class Player : Ares.CommonGUI.InvokableForm, INetworkClient
    {
#if !MONO
        private Ares.Ipc.ApplicationInstance m_Instance;
#endif

        private INetworks m_Network;

        private Ares.Playing.BassInit m_BassInit;

        private bool m_HideToTray = false;
        private bool m_FirstShow = true;

#if !MONO
        private readonly MouseKeyboardActivityMonitor.KeyboardHookListener m_KeyboardHookManager;
#endif

        public Player(Ares.Playing.BassInit init)
        {
            m_BassInit = init;
            String projectName = Environment.GetCommandLineArgs().Length > 1 ? Environment.GetCommandLineArgs()[1] : String.Empty;
            if (projectName.StartsWith("Language="))
            {
                projectName = Environment.GetCommandLineArgs().Length > 2 ? Environment.GetCommandLineArgs()[2] : String.Empty;
            }
            if (projectName.StartsWith("--minimized"))
            {
                m_HideToTray = true;
                projectName = Environment.GetCommandLineArgs().Length > 3 ? Environment.GetCommandLineArgs()[3] : String.Empty;
            }
#if !MONO
            if (String.IsNullOrEmpty(projectName))
            {
                m_Instance = Ares.Ipc.ApplicationInstance.CreateOrActivate("Ares.Player");
            }
            else
            {
                m_Instance = Ares.Ipc.ApplicationInstance.CreateOrActivate("Ares.Player", projectName);
            }
            if (m_Instance == null)
            {
                throw new Ares.Ipc.ApplicationAlreadyStartedException();
            }
#endif
#if !MONO
            m_KeyboardHookManager = new MouseKeyboardActivityMonitor.KeyboardHookListener(new MouseKeyboardActivityMonitor.WinApi.AppHooker());
            m_KeyboardHookManager.Enabled = true;
            m_KeyboardHookManager.KeyDown += new KeyEventHandler(m_KeyboardHookManager_KeyDown);
#endif
            InitializeComponent();
            if (!m_HideToTray)
            {
                notifyIcon.Visible = false;
            }
#if !MONO
            else
            {
                Visible = false;
                WindowState = FormWindowState.Minimized;
            }
#endif
#if !MONO
            m_Instance.SetWindowHandle(Handle);
            m_Instance.ProjectOpenAction = (projectName2, projectPath) => OpenProjectFromRequest(projectName2, projectPath);
#endif
            PrepareModelChecks();
            m_PlayingControl = new PlayingControl();
            InitMusicProgressBar();
            InitLibraryDrop();
            InitEditMode();
            InitTray();
            InitWeb();
            InitStatusBar();
            InitListMode();
            InitTrackPanel();
            InitTransportRow();
            InitOverlay();
            InitRemote();
            InitDiscord();
        }

        private void FitMusicBar()
        {
            if (m_MusicProgressBar == null)
                return;
            int margin = groupBox2.Left;
            int left = margin;
            if (m_TransportStrip != null)
            {
                m_TransportStrip.Left = margin;
                m_TransportStrip.Top = m_MusicProgressBar.Top + Math.Max(0, (m_MusicProgressBar.Height - m_TransportStrip.Height) / 2);
                left = m_TransportStrip.Right + 8;
            }
            int width = ClientSize.Width - margin - left;
            if (width > 50 && (m_MusicProgressBar.Left != left || m_MusicProgressBar.Width != width))
            {
                m_MusicProgressBar.Left = left;
                m_MusicProgressBar.Width = width;
            }
        }

        private ToolStrip m_TransportStrip;

        // stop, pause, previous, next and repeat on the same row as the progress bar, at its left
        private void InitTransportRow()
        {
            try
            {
                m_TransportStrip = new ToolStrip();
                m_TransportStrip.Name = "transportStrip";
                m_TransportStrip.GripStyle = ToolStripGripStyle.Hidden;
                m_TransportStrip.Dock = DockStyle.None;
                m_TransportStrip.AutoSize = true;
                m_TransportStrip.CanOverflow = false;
                ToolStripItem[] moved = new ToolStripItem[] { m_PauseButton, stopButton, previousButton, nextButton, repeatButton };
                foreach (ToolStripItem item in moved)
                {
                    toolStrip1.Items.Remove(item);
                    m_TransportStrip.Items.Add(item);
                }
                toolStrip1.Items.Remove(toolStripSeparator1);
                Controls.Add(m_TransportStrip);
                m_TransportStrip.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                m_TransportStrip.SizeChanged += (s, e) => FitMusicBar();
                Ares.CommonGUI.IconLibrary.Bind(m_TransportStrip, new Dictionary<ToolStripItem, string>
                {
                    { stopButton, "square" },
                    { previousButton, "skip-back" },
                    { nextButton, "skip-forward" },
                    { repeatButton, "repeat" }
                });
                FitMusicBar();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }

        private MusicProgressBar m_MusicProgressBar;
        private SoundProgressPanel m_SoundPanel;
        private ToolStripButton m_PauseButton;
        private bool m_PauseShown;
        private ToolStripDropDownButton m_OutputButton;
        private int m_OutputShown = int.MinValue;
        private System.Windows.Forms.Timer m_ProgressTimer;

        /// <summary>
        /// Adds the seek bar for the current music title (top of the window), the small bars
        /// for the sound effects (in the info box) and the pause button (toolbar).
        /// </summary>
        private void InitMusicProgressBar()
        {
            // music bar: full width, directly below the toolbar
            m_MusicProgressBar = new MusicProgressBar();
            m_MusicProgressBar.Name = "musicProgressBar";
            // geometry is derived from existing controls, so that it is correct with any font scaling
            m_MusicProgressBar.Location = new Point(groupBox2.Left, toolStrip1.Bottom + 3);
            m_MusicProgressBar.Size = new Size(groupBox5.Right - groupBox2.Left, groupBox2.Top - toolStrip1.Bottom - 6);
            m_MusicProgressBar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            m_MusicProgressBar.SeekRequested += (sender, e) =>
            {
                PlayingControl control = m_PlayingControl;
                if (control != null)
                    control.SeekMusic(e.Seconds);
            };
            Controls.Add(m_MusicProgressBar);

            // one small bar with a stop button for each sound effect
            m_SoundPanel = new SoundProgressPanel();
            m_SoundPanel.Name = "soundProgressPanel";
            int top = soundsLabel.Bottom + 8;
            m_SoundPanel.Location = new Point(8, top);
            m_SoundPanel.Size = new Size(groupBox3.ClientSize.Width - 16, Math.Max(40, groupBox3.ClientSize.Height - top - 8));
            m_SoundPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            m_SoundPanel.StopRequested += (sender, e) =>
            {
                PlayingControl control = m_PlayingControl;
                if (control != null)
                    control.StopSound(e.Handle);
            };
            m_SoundPanel.SeekRequested += (sender, e) =>
            {
                PlayingControl control = m_PlayingControl;
                if (control != null)
                    control.SeekSound(e.Handle, e.Seconds);
            };
            m_SoundPanel.RepeatRequested += (sender, e) =>
            {
                PlayingControl control = m_PlayingControl;
                if (control != null)
                    control.SetSoundRepeat(e.Handle, e.Repeat);
            };
            groupBox3.Controls.Add(m_SoundPanel);

            // pause button, right behind the stop button
            m_PauseButton = new ToolStripButton();
            m_PauseButton.Name = "pauseButton";
            m_PauseButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
            m_PauseButton.ImageScaling = ToolStripItemImageScaling.None;
            m_PauseButton.Click += (sender, e) =>
            {
                PlayingControl control = m_PlayingControl;
                if (control != null)
                {
                    control.TogglePause();
                    UpdatePauseButton(control.IsPaused());
                }
            };
            int stopIndex = toolStrip1.Items.IndexOf(stopButton);
            toolStrip1.Items.Insert(stopIndex < 0 ? toolStrip1.Items.Count : stopIndex + 1, m_PauseButton);
            m_PauseShown = true; // forces the first update
            UpdatePauseButton(false);
            Ares.CommonGUI.ThemeManager.Changed += (sender, e) =>
            {
                bool paused = m_PauseShown;
                m_PauseShown = !paused;
                UpdatePauseButton(paused);
            };

            // audio output selector, at the right end of the toolbar
            m_OutputButton = new ToolStripDropDownButton();
            m_OutputButton.Name = "outputDeviceButton";
            m_OutputButton.Alignment = ToolStripItemAlignment.Right;
            m_OutputButton.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
            m_OutputButton.ImageScaling = ToolStripItemImageScaling.None;
            m_OutputButton.ToolTipText = OutputToolTip();
            m_OutputButton.DropDownOpening += (sender, e) => FillOutputMenu();
            m_OutputButton.Image = CreateSpeakerImage();
            toolStrip1.Items.Add(m_OutputButton);
            UpdateOutputButton();
            Ares.CommonGUI.ThemeManager.Changed += (sender, e) =>
            {
                Image oldImage = m_OutputButton.Image;
                m_OutputButton.Image = CreateSpeakerImage();
                if (oldImage != null)
                    oldImage.Dispose();
            };

            // modern line icons (Lucide) on the toolbar
            Ares.CommonGUI.IconLibrary.Bind(toolStrip1, new Dictionary<ToolStripItem, string>
            {
                { openButton, "folder-open" },
                { messagesButton, "message-square-text" },
                { editorButton, "square-pen" },
                { settingsButton, "settings" },
                { aboutButton, "info" },
                { stopButton, "square" },
                { previousButton, "skip-back" },
                { nextButton, "skip-forward" },
                { repeatButton, "repeat" }
            }, () =>
            {
                try
                {
                    bool paused = m_PauseShown;
                    m_PauseShown = !paused;
                    UpdatePauseButton(paused);
                    Image oldImage = m_OutputButton.Image;
                    m_OutputButton.Image = CreateSpeakerImage();
                    if (oldImage != null)
                        oldImage.Dispose();
                }
                catch (Exception)
                {
                }
            });

            Ares.CommonGUI.IconLibrary.BindMenu(menuStrip1, new Dictionary<string, string>
            {
                { "openToolStripMenuItem", "folder-open" },
                { "importToolStripMenuItem", "download" },
                { "recentToolStripMenuItem", "clock" },
                { "exitToolStripMenuItem", "log-out" },
                { "stopAllToolStripMenuItem", "square" },
                { "previousMusicToolStripMenuItem", "skip-back" },
                { "nextMusicTitleToolStripMenuItem", "skip-forward" },
                { "repeatCurrentMusicToolStripMenuItem", "repeat" },
                { "startEditorToolStripMenuItem", "square-pen" },
                { "messagesToolStripMenuItem", "message-square-text" },
                { "settingsToolStripMenuItem1", "settings" },
                { "showKeysMenuItem", "keyboard" },
                { "helpOnlineToolStripMenuItem", "circle-question-mark" },
                { "checkForUpdateToolStripMenuItem", "refresh-cw" },
                { "aboutToolStripMenuItem", "info" }
            });

            // keep the music bar between the (now taller) toolbar and the group boxes
            toolStrip1.SizeChanged += (sender, e) =>
            {
                m_MusicProgressBar.Top = toolStrip1.Bottom + 3;
                m_MusicProgressBar.Height = Math.Max(34, groupBox2.Top - m_MusicProgressBar.Top - 3);
            };

            // the music bar always spans the full window width (same margin on both sides)
            Layout += (sender, e) => FitMusicBar();
            Load += (sender, e) => FitMusicBar();

            // the window is now a bit taller: make sure it fits on small screens
            Load += (sender, e) =>
            {
                Rectangle area = Screen.FromControl(this).WorkingArea;
                if (Height > area.Height)
                    Height = area.Height;
            };

            m_ProgressTimer = new System.Windows.Forms.Timer();
            m_ProgressTimer.Interval = 200;
            m_ProgressTimer.Tick += (sender, e) => UpdateMusicProgress();
            m_ProgressTimer.Start();
        }

        private string OutputToolTip()
        {
            string lang = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            if (lang == "pt")
                return "Saída de áudio (placa de som)";
            if (lang == "de")
                return "Audioausgabe (Soundkarte)";
            return "Audio output (sound card)";
        }

        private static string ShortName(string name, int maxLength)
        {
            if (name == null)
                return string.Empty;
            return name.Length <= maxLength ? name : name.Substring(0, maxLength - 1) + "\u2026";
        }

        private string DeviceName(int deviceIndex)
        {
            try
            {
                foreach (Ares.Playing.BassInit.OutputDevice device in Ares.Playing.BassInit.GetDevices())
                {
                    if (device.Index == deviceIndex)
                        return device.Name;
                }
            }
            catch (Exception)
            {
            }
            return deviceIndex.ToString();
        }

        private void UpdateOutputButton()
        {
            if (m_OutputButton == null)
                return;
            int index = Settings.Settings.Instance.OutputDeviceIndex;
            if (index == m_OutputShown)
                return;
            m_OutputShown = index;
            string name = DeviceName(index);
            m_OutputButton.Text = ShortName(name, 28);
            m_OutputButton.ToolTipText = OutputToolTip() + ": " + name;
        }

        private void FillOutputMenu()
        {
            m_OutputButton.DropDownItems.Clear();
            int current = Settings.Settings.Instance.OutputDeviceIndex;
            List<Ares.Playing.BassInit.OutputDevice> devices;
            try
            {
                devices = Ares.Playing.BassInit.GetActiveDevices();
            }
            catch (Exception)
            {
                return;
            }
            foreach (Ares.Playing.BassInit.OutputDevice device in devices)
            {
                int index = device.Index;
                ToolStripMenuItem item = new ToolStripMenuItem(device.Name);
                item.Checked = index == current;
                item.Click += (sender, e) => ChangeOutputDevice(index);
                m_OutputButton.DropDownItems.Add(item);
            }
        }

        private void ChangeOutputDevice(int deviceIndex)
        {
            if (deviceIndex == Settings.Settings.Instance.OutputDeviceIndex)
                return;
            try
            {
                m_BassInit.SwitchDeviceKeepPlaying(deviceIndex);
                Settings.Settings.Instance.OutputDeviceIndex = deviceIndex;
                WriteSettings();
            }
            catch (Ares.Playing.BassInitException)
            {
                MessageBox.Show(this, String.Format(StringResources.DeviceInitError), StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            UpdateOutputButton();
        }

        private Image CreateSpeakerImage()
        {
            Image img = Ares.CommonGUI.IconLibrary.Get("volume-2", toolStrip1.ImageScalingSize);
            return img ?? new Bitmap(20, 20);
        }

        private string PauseText(bool paused)
        {
            string lang = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            if (lang == "pt")
                return paused ? "Continuar" : "Pausar";
            if (lang == "de")
                return paused ? "Fortsetzen" : "Pause";
            return paused ? "Resume" : "Pause";
        }

        private void UpdatePauseButton(bool paused)
        {
            if (m_PauseButton == null || m_PauseShown == paused && m_PauseButton.Image != null)
                return;
            m_PauseShown = paused;
            m_PauseButton.ToolTipText = PauseText(paused);
            m_PauseButton.Text = PauseText(paused);
            Image old = m_PauseButton.Image;
            m_PauseButton.Image = CreatePauseImage(paused);
            if (old != null)
                old.Dispose();
        }

        private Image CreatePauseImage(bool showPlayTriangle)
        {
            Image img = Ares.CommonGUI.IconLibrary.Get(showPlayTriangle ? "play" : "pause", toolStrip1.ImageScalingSize);
            return img ?? new Bitmap(20, 20);
        }

        private void UpdateMusicProgress()
        {
            try
            {
                PlayingControl control = m_PlayingControl;
                if (control == null || WindowState == FormWindowState.Minimized || !Visible)
                    return;
                double position, length;
                if (control.GetMusicPosition(out position, out length))
                    m_MusicProgressBar.SetProgress(position, length);
                else
                    m_MusicProgressBar.Clear();

                List<Ares.Playing.PlayingFileInfo> sounds = control.GetRunningSounds();
                if (sounds.Count > 0)
                    m_SoundPanel.UpdateSounds(sounds);
                else
                    m_SoundPanel.ClearSounds();

                UpdatePauseButton(control.IsPaused());
                UpdateOutputButton();
            }
            catch (Exception)
            {
                // the bars are only informative
            }
        }

#if !MONO
        void m_KeyboardHookManager_KeyDown(object sender, KeyEventArgs e)
        {
            if (!tagFadeUpDown.ContainsFocus)
            {
                m_PlayingControl.KeyReceived(GetPlayersKey(e.KeyData));
            }
        }
#endif

        private ToastPanel m_Toast;

        private void MessageReceived(Ares.Players.Message m)
        {
            if (m.Type >= MessageType.Warning && (int)m.Type >= Ares.Settings.Settings.Instance.MessageFilterLevel)
            {
                if (this.IsInvokeRequired())
                {
                    BeginInvoke(new MethodInvoker(() => ShowToast(m)));
                }
                else
                {
                    ShowToast(m);
                }
            }
        }

        // a small notification in the corner instead of a pop-up window
        private void ShowToast(Ares.Players.Message m)
        {
            if (!listen || m_MessagesForm != null)
                return;
            if (WindowState == FormWindowState.Minimized && m_HideToTray)
                return;
            if (m_Toast == null)
            {
                m_Toast = new ToastPanel();
                m_Toast.Activated += (s, e) => ShowMessagesForm();
                Controls.Add(m_Toast);
                Resize += (s, e) => m_Toast.PositionInParent();
            }
            m_Toast.ShowToast(m.Text, m.Type >= MessageType.Error);
        }

        private bool listen = true;

        private void ShowMessagesForm()
        {
            if (!listen)
                return;
            if (WindowState == FormWindowState.Minimized && m_HideToTray)
                return;
            if (m_MessagesForm == null)
            {
                m_MessagesForm = new MessagesForm();
                m_MessagesForm.Location = new Point(Location.X + Size.Width, Location.Y);
                m_MessagesForm.FormClosed += new FormClosedEventHandler(m_MessagesForm_FormClosed);
                m_MessagesForm.Show(this);
            }
        }

        void m_MessagesForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            listen = false;
            messagesButton.Checked = false;
            m_MessagesForm = null;
            listen = true;
        }

        bool m_InFileSystemWatcherHandler = false;

        private void StopAllPlaying()
        {
            Ares.Playing.PlayingModule.ProjectPlayer.StopAll();
            if (Ares.Playing.PlayingModule.Streamer.IsStreaming)
            {
                Ares.Playing.PlayingModule.Streamer.EndStreaming();
            }
        }

        private void fileSystemWatcher1_Changed(object sender, System.IO.FileSystemEventArgs e)
        {
            if (m_Project == null || m_InFileSystemWatcherHandler)
                return;
            m_InFileSystemWatcherHandler = true;
            String path = m_Project.FileName;
            if (e.FullPath == path)
            {
                if (MessageBox.Show(this, StringResources.ReloadProject, StringResources.Ares, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == System.Windows.Forms.DialogResult.Yes)
                {
                    StopAllPlaying();
                    OpenProject(path, false);
                }
            }
            m_InFileSystemWatcherHandler = false;
        }

        void updateTimer_Tick(object sender, EventArgs e)
        {
            UpdateStatus();
        }

        private void settingsButton_Click(object sender, EventArgs e)
        {
            ShowSettingsDialog(-1);
        }

        private void ShowSettingsDialog(int pageIndex)
        {
            StopAllPlaying();
#if !MONO
            m_KeyboardHookManager.Enabled = false; 
#endif
            Ares.Settings.Settings settings = Ares.Settings.Settings.Instance;
            Ares.CommonGUI.SettingsDialog dialog = new Ares.CommonGUI.SettingsDialog(Ares.Settings.Settings.Instance, m_BasicSettings);
            dialog.AddPage(new StreamingPageHost());
            dialog.AddPage(new MusicPageHost());
            dialog.SetVisiblePage(pageIndex);
            if (dialog.ShowDialog(this) == System.Windows.Forms.DialogResult.OK)
            {
                m_PlayingControl.UpdateDirectories();
                LoadTagsDB();
                SetOutputDevice();
                m_PlayingControl.SetPlayMusicOnAllSpeakers(settings.PlayMusicOnAllSpeakers);
                m_PlayingControl.SetFadingOnPreviousNext(settings.ButtonMusicFadeMode != 0, settings.ButtonMusicFadeMode == 2, settings.ButtonMusicFadeTime);
                if (m_Network != null)
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
#if !MONO
            m_KeyboardHookManager.Enabled = true;
#endif
        }

        private void SetOutputDevice()
        {
            try
            {
                m_BassInit.SwitchDevice(Settings.Settings.Instance.OutputDeviceIndex);
            }
            catch (Ares.Playing.BassInitException)
            {
                MessageBox.Show(this, String.Format(StringResources.DeviceInitError), StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Error);
                Ares.Settings.Settings.Instance.OutputDeviceIndex = -1;
            }
        }

        private void openButton_Click(object sender, EventArgs e)
        {
            OpenProject();
        }

        private void OpenProject()
        {
            StopAllPlaying();
            DialogResult result = openFileDialog1.ShowDialog(this);
            if (result != System.Windows.Forms.DialogResult.OK)
                return;
            OpenProject(openFileDialog1.FileName, false);
        }

        private void OpenProjectFromRequest(String projectName, String projectPath)
        {
            if (this.IsInvokeRequired ())
            {
                Invoke(new MethodInvoker(() => OpenProjectFromRequest(projectName, projectPath)));
            }
            else
            {
                if (MessageBox.Show(this, String.Format(StringResources.OpenProjectQuestion, projectName), StringResources.Ares, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == System.Windows.Forms.DialogResult.Yes)
                {
                    OpenProject(projectPath, false);
                }
            }
        }

        private void OpenProjectFromController(String fileName)
        {
            if (this.IsInvokeRequired ())
            {
                Invoke(new MethodInvoker(() => OpenProjectFromController(fileName)));
                return;
            }
            String path = fileName;
            if (!System.IO.Path.IsPathRooted(path))
            {
                String oldPath = m_Project != null ? m_Project.FileName : System.Environment.CurrentDirectory;
                if (m_Project != null)
                {
                    oldPath = System.IO.Directory.GetParent(oldPath).FullName;
                }
                path = oldPath + System.IO.Path.DirectorySeparatorChar + fileName;
            }
            if (System.IO.File.Exists(path) && !path.Equals(m_CurrentProjectPath, StringComparison.OrdinalIgnoreCase))
            {
                if (path.EndsWith(".apkg", StringComparison.InvariantCultureIgnoreCase))
                {
                    ImportProject(path, true);
                }
                else
                {
                    OpenProject(path, true);
                }
            }
        }

        private String m_CurrentProjectPath = String.Empty;

        private void OpenProject(String filePath, bool onControllerRequest)
        {
            StopAllPlaying();
            if (m_Project != null)
            {
                if (onControllerRequest && m_Project.FileName.Equals(filePath, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (m_Network != null)
                    {
                        m_Network.InformClientOfProject(m_Project);
                    }
                    return;
                }
                Ares.Data.DataModule.ProjectManager.UnloadProject(m_Project);
                m_Project = null;
                m_TagLanguageId = Ares.Tags.TagsModule.GetTagsDB().TranslationsInterface.GetIdOfCurrentUILanguage();
#if !MONO
                m_Instance.SetLoadedProject("-");
#endif
            }
            try
            {
                m_Project = Ares.Data.DataModule.ProjectManager.LoadProject(filePath);
                m_CurrentProjectPath = filePath;
                if (m_Project != null && m_Project.TagLanguageId != -1)
                {
                    m_TagLanguageId = m_Project.TagLanguageId;
                }
                else
                {
                    m_TagLanguageId = Ares.Tags.TagsModule.GetTagsDB().TranslationsInterface.GetIdOfCurrentUILanguage();
                }
                Ares.Settings.Settings.Instance.RecentFiles.AddFile(new RecentFiles.ProjectEntry(m_Project.FileName, m_Project.Title));
            }
            catch (Exception e)
            {
                if (!onControllerRequest)
                {
                    MessageBox.Show(this, String.Format(StringResources.LoadError, e.Message), StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    m_Network.ErrorOccurred(-1, String.Format(StringResources.LoadError, e.Message));
                }
                m_Project = null;
                m_CurrentProjectPath = String.Empty;
            }
            projectNameLabel.Text = m_Project != null ? m_Project.Title : StringResources.NoOpenedProject;
            this.Text = String.Format(StringResources.AresPlayer, projectNameLabel.Text);
            Ares.Playing.PlayingModule.ProjectPlayer.SetProject(m_Project);
            DoModelChecks();
            if (m_Project != null)
            {
                fileSystemWatcher1.Path = System.IO.Path.GetDirectoryName(m_Project.FileName);
            }
#if !MONO
            m_Instance.SetLoadedProject(filePath);
#endif
            if (m_Network != null)
            {
                m_Network.InformClientOfProject(m_Project);
                if (m_Project != null)
                {
                    m_Network.InformClientOfPossibleTags(m_TagLanguageId, m_Project);
                }
            }
            UpdateModesList();
        }

        private bool m_Listen = true;

        private void UpdateModesList()
        {
            foreach (CheckBox box in m_CurrentButtons.Keys)
            {
                box.CheckedChanged -= m_CurrentButtons[box];
            }
            m_CurrentButtons.Clear();
            foreach (CheckBox box in m_CurrentTagButtons.Keys)
            {
                box.CheckedChanged -= m_CurrentTagButtons[box];
            }
            m_ButtonsForIds.Clear();

            modesList.BeginUpdate();
            modesList.Items.Clear();
            m_Listen = false;
            if (m_Project != null)
            {
                IList<Ares.Data.IMode> modes = m_Project.GetModes();
                if (modes.Count == 0)
                {
                    modesList.EndUpdate();
                    m_Listen = true;
                    return;
                }
                int i = 0;
                int currentMode = 0;
                bool showKeys = Settings.Settings.Instance.ShowKeysInButtons;
                KeysConverter converter = new KeysConverter();
                foreach (Ares.Data.IMode mode in modes)
                {
                    String title = mode.Title;
                    if (showKeys && mode.KeyCode != 0)
                    {
                        title += " (" + converter.ConvertToString((System.Windows.Forms.Keys)mode.KeyCode) + ")";
                    }
                    modesList.Items.Add(title);
                    if (mode == m_PlayingControl.CurrentMode)
                    {
                        currentMode = i;
                    }
                    ++i;
                }
                modesList.SelectedIndex = currentMode;
            }
            modesList.EndUpdate();
            UpdateElementsPanel();
            m_Listen = true;
        }

        private Dictionary<CheckBox, EventHandler> m_CurrentButtons = new Dictionary<CheckBox, EventHandler>();
        private Dictionary<CheckBox, EventHandler> m_CurrentTagButtons = new Dictionary<CheckBox, EventHandler>();
        private Dictionary<int, CheckBox> m_ButtonsForIds = new Dictionary<int, CheckBox>();
        private bool m_ButtonsActive = true;
        private bool m_TagsControlsActive = false;

        private int m_TagLanguageId;
        private int m_LastTagCategoryId = -1;
        IList<Ares.Tags.CategoryForLanguage> m_MusicTagCategories;
        
        /// <summary>Width a button needs for its text with the font really in use (themes and zoom change the font after the buttons were first measured).</summary>
        private static int MeasureButtonWidth(string text, Font font)
        {
            Size s = TextRenderer.MeasureText(text, font, new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            return s.Width + (int)Math.Ceiling(font.Size * 3.2f) + 8;
        }

        private void UpdateElementsPanel()
        {
            int modeIndex = modesList.SelectedIndex;
            if (modeIndex == -1)
                return;
            if (SpecialViews && modeIndex == 0)
            {
                musicList.BeginUpdate();
                musicList.Items.Clear();
                Ares.Data.IElement element = Ares.Data.DataModule.ElementRepository.GetElement(m_PlayingControl.CurrentMusicList);
                if (element != null && element is Ares.Data.IMusicList)
                {
                    Ares.Data.IMusicList currentMusicList = (Ares.Data.IMusicList)element;
                    var fileElements = currentMusicList.GetFileElements();
                    int selIndex = -1;
                    for (int i = 0; i < fileElements.Count; ++i)
                    {
                        musicList.Items.Add(fileElements[i].Title);
                        if (fileElements[i].Id == m_PlayingControl.CurrentMusicElement)
                            selIndex = i;
                    }
                    if (selIndex != -1)
                        musicList.SelectedIndex = selIndex;
                }
                musicList.EndUpdate();
                elementsPanel.Visible = false;
                tagsPanel.Visible = false;
                m_ButtonsActive = false;
                m_TagsControlsActive = false;
                musicList.Visible = true;
            }
            else if (SpecialViews && modeIndex == 1)
            {
                foreach (CheckBox box in m_CurrentTagButtons.Keys)
                {
                    box.CheckedChanged -= m_CurrentTagButtons[box];
                }
                m_CurrentTagButtons.Clear();

                m_TagsControlsActive = false;
                Ares.Tags.ITagsDBReadByLanguage dbRead = Ares.Tags.TagsModule.GetTagsDB().GetReadInterfaceByLanguage(m_TagLanguageId);

                musicTagCategoryBox.Items.Clear();
                int selIndex = -1;
                try
                {
                    m_MusicTagCategories = dbRead.GetAllCategories();
                }
                catch (Ares.Tags.TagsDbException ex)
                {
                    Messages.AddMessage(MessageType.Error, String.Format(StringResources.TagsDbError, ex.Message));
                    m_MusicTagCategories = new List<Ares.Tags.CategoryForLanguage>();
                }
                if (m_Project != null && m_Project.GetHiddenTagCategories().Count > 0)
                {
                    HashSet<int> hiddenCategories = m_Project.GetHiddenTagCategories();
                    List<Ares.Tags.CategoryForLanguage> filteredCategories = new List<Tags.CategoryForLanguage>();
                    foreach (var category in m_MusicTagCategories)
                    {
                        if (!hiddenCategories.Contains(category.Id))
                            filteredCategories.Add(category);
                    }
                    m_MusicTagCategories = filteredCategories;
                }
                foreach (var category in m_MusicTagCategories)
                {
                    musicTagCategoryBox.Items.Add(category.Name);
                    if (category.Id == m_LastTagCategoryId)
                        selIndex = musicTagCategoryBox.Items.Count - 1;
                }
                if (selIndex == -1 && musicTagCategoryBox.Items.Count > 0)
                {
                    selIndex = 0;
                    m_LastTagCategoryId = m_MusicTagCategories[0].Id;
                }
                if (selIndex != -1)
                {
                    musicTagCategoryBox.SelectedIndex = selIndex;
                }
                if (musicTagCategoryBox.Items.Count == 0)
                    m_LastTagCategoryId = -1;

                HashSet<int> currentTags = m_PlayingControl.GetCurrentMusicTags();

                tagSelectionPanel.Controls.Clear();
                if (m_LastTagCategoryId != -1)
                {
                    IList<Ares.Tags.TagForLanguage> tags;
                    try 
                    {
                        tags = dbRead.GetAllTags(m_LastTagCategoryId);
                    }
                    catch (Ares.Tags.TagsDbException ex)
                    {
                        Messages.AddMessage(MessageType.Error, String.Format(StringResources.TagsDbError, ex.Message));
                        tags = new List<Ares.Tags.TagForLanguage>();
                    }
                    if (m_Project != null && m_Project.GetHiddenTags().Count > 0)
                    {
                        HashSet<int> hiddenTags = m_Project.GetHiddenTags();
                        List<Tags.TagForLanguage> filteredTags = new List<Tags.TagForLanguage>();
                        foreach (var tag in tags)
                        {
                            if (!hiddenTags.Contains(tag.Id))
                                filteredTags.Add(tag);
                        }
                        tags = filteredTags;
                    }

                    int maxWidth = 0;
                    CheckBox checkBox = new CheckBox();
                    checkBox.Appearance = Appearance.Button;
                    for (int i = 0; i < tags.Count; ++i)
                    {
                        String text = tags[i].Name;
                        int width = MeasureButtonWidth(text, tagSelectionPanel.Font);
                        if (width > maxWidth)
                            maxWidth = width;
                    }
                    checkBox.Dispose();
                    int count = 0;
                    for (int i = 0; i < tags.Count; ++i)
                    {
                        ++count;
                        int row = count / 4;
                        int column = count % 4;
                        checkBox = new CheckBox();
                        String text = tags[i].Name;
                        checkBox.Text = text;
                        checkBox.Appearance = Appearance.Button;
                        checkBox.Font = tagSelectionPanel.Font;
                        checkBox.AutoEllipsis = false;
                        checkBox.TextAlign = ContentAlignment.MiddleCenter;
                        Size size = checkBox.PreferredSize;
                        size.Width = maxWidth;
                        checkBox.SetBounds(0, 0, size.Width, size.Height);
                        checkBox.Checked = currentTags.Contains(tags[i].Id);
                        int tagId = tags[i].Id;
                        int categoryId = m_LastTagCategoryId;
                        EventHandler handler = new EventHandler((Object sender, EventArgs args) =>
                        {
                            if (m_Listen && m_TagsControlsActive)
                            {
                                if ((sender as CheckBox).Checked)
                                {
                                    m_PlayingControl.AddMusicTag(categoryId, tagId);
                                }
                                else
                                {
                                    m_PlayingControl.RemoveMusicTag(categoryId, tagId);
                                }
                            }
                        });
                        checkBox.CheckedChanged += handler;
                        m_CurrentTagButtons.Add(checkBox, handler);
                        tagSelectionPanel.Controls.Add(checkBox);
                    }
                    if (count == 0)
                    {
                        Label label = new Label();
                        label.Text = StringResources.NoTags;
                        label.SetBounds(0, 0, label.PreferredSize.Width, label.PreferredSize.Height);
                        tagSelectionPanel.Controls.Add(label);
                    }
                }

                if (m_UpdateCategoryCombinationBox)
                {
                    tagCategoryCombinationBox.SelectedIndex = (int)m_PlayingControl.GetMusicTagCategoriesCombination();
                }

                StringBuilder currentTagsBuilder = new StringBuilder();
                IList<Ares.Tags.TagInfoForLanguage> currentTagInfos;
                try 
                { 
                    currentTagInfos = dbRead.GetTagInfos(currentTags);
                }
                catch (Ares.Tags.TagsDbException ex)
                {
                    Messages.AddMessage(MessageType.Error, String.Format(StringResources.TagsDbError, ex.Message));
                    currentTagInfos = new List<Ares.Tags.TagInfoForLanguage>();
                }
                int catId = -1;
                foreach (var tagInfo in currentTagInfos)
                {
                    if (tagInfo.CategoryId != catId)
                    {
                        if (currentTagsBuilder.Length > 0)
                        {
                            currentTagsBuilder.Append("; ");
                        }
                        currentTagsBuilder.Append(tagInfo.Category);
                        currentTagsBuilder.Append(": ");
                        catId = tagInfo.CategoryId;
                    }
                    else
                    {
                        currentTagsBuilder.Append(", ");
                    }
                    currentTagsBuilder.Append(tagInfo.Name);
                }
                currentTagsLabel.Text = currentTagsBuilder.ToString();

                elementsPanel.Visible = false;
                tagsPanel.Visible = true;
                m_TagsControlsActive = true;
                m_ButtonsActive = false;
                musicList.Visible = false;
            }
            else
            {
                foreach (CheckBox box in m_CurrentButtons.Keys)
                {
                    box.CheckedChanged -= m_CurrentButtons[box];
                }
                m_CurrentButtons.Clear();
                m_ButtonsForIds.Clear();
                elementsPanel.Controls.Clear();

                if (m_Project != null && m_Project.GetModes().Count > modeIndex)
                {
                    bool showKeys = Settings.Settings.Instance.ShowKeysInButtons;
                    KeysConverter converter = new KeysConverter();
                    Ares.Data.IMode shownMode = m_Project.GetModes()[modeIndex];
                    IList<Ares.Data.IModeElement> elements = shownMode.GetElements();
                    string shownModeTitle = shownMode.Title;
                    // text, picture / icon of every visible button
                    List<Ares.Data.IModeElement> visible = new List<Ares.Data.IModeElement>();
                    List<string> texts = new List<string>();
                    List<string> specs = new List<string>();
                    List<int> kinds = new List<int>();
                    for (int i = 0; i < elements.Count; ++i)
                    {
                        if (!elements[i].IsVisibleInPlayer)
                            continue;
                        String text = elements[i].Title;
                        if (showKeys && elements[i].Trigger != null && elements[i].Trigger.TriggerType == TriggerType.Key)
                        {
                            int keyCode = (elements[i].Trigger as IKeyTrigger).KeyCode;
                            if (keyCode != 0)
                                text += " (" + converter.ConvertToString((System.Windows.Forms.Keys)keyCode) + ")";
                        }
                        string spec = Ares.CommonGUI.ButtonImageStore.Get(m_CurrentProjectPath, shownModeTitle, elements[i].Title);
                        int kind = 0;
                        if (spec != null && spec.StartsWith("img:") && Ares.CommonGUI.ButtonImageStore.LoadImage(m_CurrentProjectPath, spec) != null)
                            kind = 2;
                        else if (spec != null && spec.StartsWith("lucide:"))
                            kind = 1;
                        visible.Add(elements[i]);
                        texts.Add(text);
                        specs.Add(spec);
                        kinds.Add(kind);
                    }
                    Size cardSize = ElementButton.ComputeCardSize(texts, kinds, elementsPanel.Font);
                    int count = 0;
                    for (int i = 0; i < visible.Count; ++i)
                    {
                        ++count;
                        Ares.Data.IModeElement element = visible[i];
                        ElementButton checkBox = new ElementButton();
                        checkBox.Font = elementsPanel.Font;
                        checkBox.Text = texts[i];
                        if (kinds[i] == 2)
                            checkBox.Picture = Ares.CommonGUI.ButtonImageStore.LoadImage(m_CurrentProjectPath, specs[i]);
                        else if (kinds[i] == 1)
                            checkBox.IconName = specs[i].Substring(7);
                        checkBox.SetBounds(0, 0, cardSize.Width, cardSize.Height);
                        checkBox.Checked = m_PlayingControl.CurrentModeElements.Contains(element);
                        int id = element.Id;
                        EventHandler handler = new EventHandler((Object, EventArgs) =>
                        {
                            if (m_Listen && m_ButtonsActive)
                            {
                                m_PlayingControl.SwitchElement(id);
                            }
                        });
                        checkBox.CheckedChanged += handler;
                        m_CurrentButtons.Add(checkBox, handler);
                        m_ButtonsForIds.Add(id, checkBox);
                        AttachElementMenu(checkBox, id);
                        elementsPanel.Controls.Add(checkBox);
                    }
                    if (count == 0)
                    {
                        Label label = new Label();
                        label.Text = StringResources.NoElements;
                        label.SetBounds(0, 0, label.PreferredSize.Width, label.PreferredSize.Height);
                        elementsPanel.Controls.Add(label);
                    }
                }
                else
                {
                    Label label = new Label();
                    label.Text = StringResources.NoElements;
                    label.SetBounds(0, 0, label.PreferredSize.Width, label.PreferredSize.Height);
                    elementsPanel.Controls.Add(label);
                }
                AfterElementsBuilt(modeIndex);
                m_ButtonsActive = true;
                musicList.Visible = false;
                tagsPanel.Visible = false;
                m_TagsControlsActive = false;
                elementsPanel.Visible = true;
            }
        }

        private void PrepareModelChecks()
        {
            Ares.ModelInfo.ModelChecks.Instance.AddCheck(new Ares.CommonGUI.KeyChecks());
        }

        private void DoModelChecks()
        {
            Ares.ModelInfo.ModelChecks.Instance.CheckSynchronously(m_Project);
            foreach (Ares.ModelInfo.ModelError error in Ares.ModelInfo.ModelChecks.Instance.GetAllErrors())
            {
                Messages.AddMessage(error.Severity == ModelInfo.ModelError.ErrorSeverity.Error ? MessageType.Error : MessageType.Warning,
                    error.Message);
            }
        }

        private int GetPlayersKey(System.Windows.Forms.Keys key)
        {
            return (int)key;
        }

        protected override bool ProcessCmdKey(ref System.Windows.Forms.Message msg, System.Windows.Forms.Keys keyData)
        {
            if (keyData == System.Windows.Forms.Keys.F9)
            {
                ToggleTrackPanel();
                return true;
            }
            if (keyData == System.Windows.Forms.Keys.F8)
            {
                SetLeftOpen(!m_LeftOpen);
                return true;
            }
#if MONO
            const int WM_KEYDOWN = 0x100;
            if (msg.Msg == WM_KEYDOWN && !tagFadeUpDown.ContainsFocus)
            {
                return m_PlayingControl.KeyReceived(GetPlayersKey(keyData));
            }
#endif
            if (keyData == (System.Windows.Forms.Keys.F4 | System.Windows.Forms.Keys.Alt))
            {
                if (!CloseToTrayEnabled)
                    StopAllPlaying();
                Close();
                return true;
            }
            return false;
        }

        private int lastMusicElementId = -1;
        private String lastMode = String.Empty;
        /// <summary>The old "music list" / "music tags" pseudo modes were removed from the modes list.</summary>
        private const bool SpecialViews = false;
        private int lastMusicListId = -1;
        private HashSet<int> lastTagIds = null;

        private void UpdateStatus()
        {
            PlayingControl control = m_PlayingControl;
            m_Listen = false;
            IMode mode = control.CurrentMode;
            modeLabel.Text = mode != null ? mode.Title : String.Empty;
            if (m_Project != null && mode.Title != lastMode)
            {
                for (int i = 0; i < m_Project.GetModes().Count; ++i)
                {
                    if (m_Project.GetModes()[i].Title == mode.Title)
                    {
                        modesList.SelectedIndex = i;
                        break;
                    }
                }
                UpdateElementsPanel();
                lastMode = mode.Title;
            }
            StringBuilder modeElementsText = new StringBuilder();
            HashSet<int> currentModeElementIds = new HashSet<int>();
            foreach (IModeElement modeElement in control.CurrentModeElements)
            {
                if (modeElementsText.Length > 0)
                    modeElementsText.Append(", ");
                modeElementsText.Append(modeElement.Title);
                currentModeElementIds.Add(modeElement.Id);
            }
            foreach (int id in m_ButtonsForIds.Keys)
            {
                m_ButtonsForIds[id].Checked = currentModeElementIds.Contains(id);
            }
            elementsLabel.Text = modeElementsText.ToString();
            StringBuilder soundElementsText = new StringBuilder();
            foreach (int element in control.CurrentSoundElements)
            {
                if (soundElementsText.Length > 0)
                    soundElementsText.Append(", ");
                soundElementsText.Append(Ares.Data.DataModule.ElementRepository.GetElement(element).Title);
            }
            soundsLabel.Text = soundElementsText.ToString();
            int musicElementId = control.CurrentMusicElement;
            if (musicElementId != lastMusicElementId)
            {
                musicLabel.Text = MusicInfo.GetInfo(musicElementId).LongTitle;
                lastMusicElementId = musicElementId;
                SyncTrackSelection();
                var currentMusicList = Ares.Data.DataModule.ElementRepository.GetElement(m_PlayingControl.CurrentMusicList) as IMusicList;
                var elements = currentMusicList != null ? currentMusicList.GetFileElements() : null;
                if (elements != null && musicList.Items.Count > 0)
                {
                    for (int i = 0; i < elements.Count; ++i)
                    {
                        if (elements[i].Id == musicElementId)
                        {
                            if (musicList.Items.Count > i)
                                musicList.SelectedIndex = i;
                            break;
                        }
                    }
                }
            }
            bool repeat = m_PlayingControl.MusicRepeat;
            if (musicElementId == -1)
            {
                repeat = false;
            }
            repeatButton.Checked = repeat;
            bool settingsChanged = false;
            bool onAllSpeakers = m_PlayingControl.MusicOnAllSpeakers;
            if (onAllSpeakers != Ares.Settings.Settings.Instance.PlayMusicOnAllSpeakers)
            {
                Ares.Settings.Settings.Instance.PlayMusicOnAllSpeakers = onAllSpeakers;
                settingsChanged = true;
            }
            int previousNextFadeMode = m_PlayingControl.FadingOnPreviousNext;
            if (previousNextFadeMode != Ares.Settings.Settings.Instance.ButtonMusicFadeMode)
            {
                Ares.Settings.Settings.Instance.ButtonMusicFadeMode = previousNextFadeMode;
                settingsChanged = true;
            }
            int previousNextFadeTime = m_PlayingControl.FadeOnPreviousNextTime;
            if (previousNextFadeTime != Ares.Settings.Settings.Instance.ButtonMusicFadeTime)
            {
                Ares.Settings.Settings.Instance.ButtonMusicFadeTime = previousNextFadeTime;
                settingsChanged = true;
            }
            repeatCurrentMusicToolStripMenuItem.Checked = repeat;
            if (control.CurrentMusicList != lastMusicListId && SpecialViews && modesList.SelectedIndex == 0)
            {
                UpdateElementsPanel();
            }
            else if (SpecialViews && modesList.SelectedIndex == 1)
            {
                HashSet<int> tags = lastTagIds != null ? new HashSet<int>(lastTagIds) : new HashSet<int>();
                HashSet<int> currentTags = control.GetCurrentMusicTags();
                tags.SymmetricExceptWith(currentTags);
                if (tags.Count > 0)
                {
                    // current tag set has changed
                    lastTagIds = currentTags;
                    UpdateElementsPanel();
                }
                else
                {
                    m_TagsControlsActive = false;
                    if (m_UpdateCategoryCombinationBox)
                    {
                        tagCategoryCombinationBox.SelectedIndex = (int)m_PlayingControl.GetMusicTagCategoriesCombination();
                    }
                    m_TagsControlsActive = true;
                }
            }
            lastMusicListId = control.CurrentMusicList;
            m_Listen = true;
            if (overallVolumeBar.Value != control.GlobalVolume)
            {
                overallVolumeBar.Value = control.GlobalVolume;
                m_Network.InformClientOfVolume(Ares.Playing.VolumeTarget.Both, control.GlobalVolume);
                Settings.Settings.Instance.GlobalVolume = control.GlobalVolume;
                settingsChanged = true;
            }
            if (musicVolumeBar.Value != control.MusicVolume)
            {
                musicVolumeBar.Value = control.MusicVolume;
                m_Network.InformClientOfVolume(Ares.Playing.VolumeTarget.Music, control.MusicVolume);
                Settings.Settings.Instance.MusicVolume = control.MusicVolume;
                settingsChanged = true;
            }
            if (soundVolumeBar.Value != control.SoundVolume)
            {
                soundVolumeBar.Value = control.SoundVolume;
                m_Network.InformClientOfVolume(Ares.Playing.VolumeTarget.Sounds, control.SoundVolume);
                Settings.Settings.Instance.SoundVolume = control.SoundVolume;
                settingsChanged = true;
            }
            int musicFadeTime; bool fadeOnlyOnChange;
            int oldValue = (int)tagFadeUpDown.Value; // reading the value may update the value -- and fire a valueChange!
            control.GetMusicTagsFading(out musicFadeTime, out fadeOnlyOnChange);
            bool fadeChanged = false;
            if (musicFadeTime != oldValue)
            {
                tagFadeUpDown.Value = musicFadeTime;
                Settings.Settings.Instance.TagMusicFadeTime = musicFadeTime;
                fadeChanged = true;
                settingsChanged = true;
            }
            if (fadeOnlyOnChange != fadeOnlyOnChangeBox.Checked)
            {
                fadeOnlyOnChangeBox.Checked = fadeOnlyOnChange;
                Settings.Settings.Instance.TagMusicFadeOnlyOnChange = fadeOnlyOnChange;
                fadeChanged = true;
                settingsChanged = true;
            }
            if (fadeChanged)
            {
                m_Network.InformClientOfFading(musicFadeTime, fadeOnlyOnChange);
            }
            if (settingsChanged)
            {
                Settings.Settings.Instance.Commit();
            }
        }

        private BasicSettings m_BasicSettings;
        private IProject m_Project;
        private PlayingControl m_PlayingControl;
        private bool commitVolumes = true;

        private void overallVolumeBar_Scroll(object sender, EventArgs e)
        {
            m_PlayingControl.GlobalVolume = overallVolumeBar.Value;
            m_Network.InformClientOfVolume(Ares.Playing.VolumeTarget.Both, overallVolumeBar.Value);
            if (!commitVolumes) return;
            Settings.Settings.Instance.GlobalVolume = overallVolumeBar.Value;
            Settings.Settings.Instance.Commit();
        }

        private void musicVolumeBar_Scroll(object sender, EventArgs e)
        {
            m_PlayingControl.MusicVolume = musicVolumeBar.Value;
            m_Network.InformClientOfVolume(Ares.Playing.VolumeTarget.Music, musicVolumeBar.Value);
            if (!commitVolumes) return;
            Settings.Settings.Instance.MusicVolume = musicVolumeBar.Value;
            Settings.Settings.Instance.Commit();
        }

        private void soundVolumeBar_Scroll(object sender, EventArgs e)
        {
            m_PlayingControl.SoundVolume = soundVolumeBar.Value;
            m_Network.InformClientOfVolume(Ares.Playing.VolumeTarget.Sounds, soundVolumeBar.Value);
            if (!commitVolumes) return;
            Settings.Settings.Instance.SoundVolume = soundVolumeBar.Value;
            Settings.Settings.Instance.Commit();
        }

        private void aboutButton_Click(object sender, EventArgs e)
        {
            CommonGUI.AboutDialog dialog = new CommonGUI.AboutDialog();
            dialog.ShowDialog(this);
        }

        private void Player_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (TryHideToTray(e))
                return;
            notifyIcon.Visible = false;
            Shutdown();
        }

        private void Shutdown()
        {
            if (m_Network != null)
            {
                if (m_Network.ClientConnected)
                {
                    m_Network.DisconnectClient(false);
                }
                else
                {
                    m_Network.StopListenForClient();
                    System.Threading.Thread.Sleep(400);
                    if (m_Network.ClientConnected)
                    {
                        m_Network.DisconnectClient(false);
                    }
                }
                broadCastTimer.Enabled = false;
                m_Network.StopUdpBroadcast();
                m_Network.Shutdown();
            }
            m_PlayingControl.Dispose();
#if !MONO
            m_KeyboardHookManager.Dispose();
#endif
            WriteSettings();
            Settings.Settings.Instance.Shutdown();
        }

        private bool warnOnNetworkFail = true;
        
        void broadCastTimer_Tick(object sender, EventArgs e)
        {
            if (m_Network != null)
            {
                if (!m_Network.SendUdpPacket())
                {
                    if (warnOnNetworkFail)
                    {
                        warnOnNetworkFail = false;
                        MessageBox.Show(this, StringResources.NoStatusInfoError, StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void WriteSettings()
        {
            try
            {
                Ares.Settings.Settings.Instance.WriteToFile(m_BasicSettings.GetSettingsDir());
                m_BasicSettings.WriteToFile();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, String.Format(StringResources.WriteSettingsError, ex.Message), StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }

        private void ReadSettings()
        {
            bool foundSettings = m_BasicSettings.ReadFromFile();
            bool hasSettings = Ares.Settings.Settings.Instance.Initialize(Ares.Settings.Settings.PlayerID, foundSettings ? m_BasicSettings.GetSettingsDir() : null);
            if (!hasSettings)
            {
                MessageBox.Show(this, StringResources.NoSettings, StringResources.Ares);
                ShowSettingsDialog(-1);
                SettingsChanged(true);
            }
            else
            {
                SettingsChanged(true);
            }
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

        private void LoadTagsDB()
        {
            try
            {
                Ares.Tags.ITagsDBFiles tagsDBFiles = Ares.Tags.TagsModule.GetTagsDB().FilesInterface;
                String path = System.IO.Path.Combine(Ares.Settings.Settings.Instance.MusicDirectory, tagsDBFiles.DefaultFileName);
                tagsDBFiles.OpenOrCreateDatabase(path);
            }
            catch (Ares.Tags.TagsDbException ex)
            {
                MessageBox.Show(this, String.Format(StringResources.TagsDbError, ex.Message), StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private MessagesForm m_MessagesForm;

        private void messagesButton_CheckedChanged(object sender, EventArgs e)
        {
            if (m_MessagesForm != null)
            {
                m_MessagesForm.Close();
                m_MessagesForm = null;
            }
            else
            {
                ShowMessagesForm();
            }
        }

        private void editorButton_Click(object sender, EventArgs e)
        {
            StartEditor();
        }

        private void StartEditor()
        {
#if !MONO
            String appDir = System.IO.Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath);
            String commandLine = System.IO.Path.Combine(appDir, "Apolo.Editor.exe");
            try
            {
                Ares.Ipc.ApplicationInstance.CreateOrActivate("Ares.Editor", commandLine,
                    m_Project != null ? m_Project.Title : "", m_Project != null ? m_Project.FileName : "");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, String.Format(StringResources.EditorStartError, ex.Message), StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
#endif
        }

        private void stopButton_Click(object sender, EventArgs e)
        {
            m_PlayingControl.KeyReceived((int)Ares.Data.Keys.Escape);
        }

        private void previousButton_Click(object sender, EventArgs e)
        {
            m_PlayingControl.KeyReceived((int)Ares.Data.Keys.Left);
        }

        private void nextButton_Click(object sender, EventArgs e)
        {
            m_PlayingControl.KeyReceived((int)Ares.Data.Keys.Right);
        }

        public void KeyReceived(int key)
        {
            m_PlayingControl.KeyReceived(key);
        }

        public void ChangeMode(String title)
        {
            m_PlayingControl.SetMode(title);
        }

        public void VolumeReceived(Ares.Playing.VolumeTarget target, int value)
        {
            switch (target)
            {
                case Ares.Playing.VolumeTarget.Both:
                    m_PlayingControl.GlobalVolume = value;
                    break;
                case Ares.Playing.VolumeTarget.Music:
                    m_PlayingControl.MusicVolume = value;
                    break;
                case Ares.Playing.VolumeTarget.Sounds:
                    m_PlayingControl.SoundVolume = value;
                    break;
                default:
                    break;
            }
        }

        public void ClientDataChanged(bool listenAgainAfterDisconnect)
        {
            if (listenAgainAfterDisconnect)
                m_Network.StartUdpBroadcast();
            if (this.IsDisposed)
                return;
            this.Invoke(new MethodInvoker(UpdateClientData));
        }

        public void ClientConnected()
        {
            m_Network.StopUdpBroadcast();
            this.Invoke(new MethodInvoker(UpdateClientData));
        }

        public String GetProjectsDirectory()
        {
            String oldPath = m_Project != null ? m_Project.FileName : System.Environment.CurrentDirectory;
            if (m_Project != null)
            {
                oldPath = System.IO.Directory.GetParent(oldPath).FullName;
            }
            return oldPath;
        }

        public Ares.Settings.RecentFiles GetLastUsedProjects()
        {
            return Ares.Settings.Settings.Instance.RecentFiles;
        }

        public void ProjectShallChange(String newProjectPath)
        {
            OpenProjectFromController(newProjectPath);
        }

        public IProject GetCurrentProject()
        {
            return m_Project;
        }

        public void PlayOtherMusic(Int32 id)
        {
            m_PlayingControl.SelectMusicElement(id);
        }

        public void SwitchElement(Int32 id)
        {
            m_PlayingControl.SwitchElement(id);
        }

        public void SetMusicRepeat(bool repeat)
        {
            m_PlayingControl.SetRepeatCurrentMusic(repeat);
        }

        public void SwitchTag(Int32 categoryId, Int32 tagId, bool active)
        {
            if (active)
            {
                m_PlayingControl.AddMusicTag(categoryId, tagId);
            }
            else
            {
                m_PlayingControl.RemoveMusicTag(categoryId, tagId);
            }
        }

        public void DeactivateAllTags()
        {
            m_PlayingControl.RemoveAllMusicTags();
        }

        public void SetTagCategoryCombination(Data.TagCategoryCombination categoryCombination)
        {
            m_PlayingControl.SetMusicTagCategoriesCombination(categoryCombination);
        }

        public void SetMusicTagsFading(Int32 fadeTime, bool onlyOnChange)
        {
            m_PlayingControl.SetMusicTagFading(fadeTime, onlyOnChange);
        }

        public void SetPlayMusicOnAllSpeakers(bool onAllSpeakers)
        {
            m_PlayingControl.SetPlayMusicOnAllSpeakers(onAllSpeakers);
        }

        public void SetFadingOnPreviousNext(int fadeMode, int fadeTime)
        {
            m_PlayingControl.SetFadingOnPreviousNext(fadeMode != 0, fadeMode == 2, fadeTime);
        }

        private bool m_WasConnected = false;

        private void UpdateClientData()
        {
            UpdateClientState();
            if (m_Network.ClientConnected)
            {
                m_Network.InformClientOfEverything(m_PlayingControl.GlobalVolume, m_PlayingControl.MusicVolume,
                    m_PlayingControl.SoundVolume, m_PlayingControl.CurrentMode, MusicInfo.GetInfo(m_PlayingControl.CurrentMusicElement),
                    m_PlayingControl.CurrentModeElements, m_Project, 
                    m_PlayingControl.CurrentMusicList, m_PlayingControl.MusicRepeat,
                    m_TagLanguageId, new List<int>(m_PlayingControl.GetCurrentMusicTags()), m_PlayingControl.GetMusicTagCategoriesCombination(),
                    Settings.Settings.Instance.TagMusicFadeTime, Settings.Settings.Instance.TagMusicFadeOnlyOnChange,
                    Settings.Settings.Instance.PlayMusicOnAllSpeakers, 
                    Settings.Settings.Instance.ButtonMusicFadeMode, Settings.Settings.Instance.ButtonMusicFadeTime);
                m_WasConnected = true;
            }
            else
            {
                if (m_HideToTray && m_WasConnected)
                {
                    m_Quitting = true;
                    Close();
                }
            }
        }

        private void networkSettingsButton_Click(object sender, EventArgs e)
        {
            ShowSettingsDialog(1);
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            m_Quitting = true;
            Close();
        }

        private void extrasToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            messagesToolStripMenuItem.Checked = messagesButton.Checked;
        }

        private void messagesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            messagesButton.PerformClick();
        }

        private void recentToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            recentToolStripMenuItem.DropDownItems.Clear();
            foreach (RecentFiles.ProjectEntry projectEntry in Ares.Settings.Settings.Instance.RecentFiles.GetFiles())
            {
                ToolStripMenuItem item = new ToolStripMenuItem(projectEntry.ProjectName);
                item.ToolTipText = projectEntry.FilePath;
                item.Tag = projectEntry;
                item.Click += new EventHandler(recentItem_Click);

                recentToolStripMenuItem.DropDownItems.Add(item);
            }
        }

        void recentItem_Click(object sender, EventArgs e)
        {
            OpenProject(((sender as ToolStripMenuItem).Tag as RecentFiles.ProjectEntry).FilePath, false);
        }

        private void Player_Load(object sender, EventArgs e)
        {
            m_BasicSettings = new BasicSettings();
            ReadSettings();
            showKeysMenuItem.Checked = Settings.Settings.Instance.ShowKeysInButtons;
#if !MONO
            globalKeyHookItem.Checked = Settings.Settings.Instance.GlobalKeyHook;
            if (Settings.Settings.Instance.GlobalKeyHook)
            {
                m_KeyboardHookManager.Replace(new MouseKeyboardActivityMonitor.WinApi.GlobalHooker());
            }
#else
            globalKeyHookItem.Checked = false;
            globalKeyHookItem.Enabled = false;
#endif
            Messages.Instance.MessageReceived += new MessageReceivedHandler(MessageReceived);
            int argIndex = 1;
            String projectName = Environment.GetCommandLineArgs().Length > argIndex ? Environment.GetCommandLineArgs()[argIndex] : String.Empty;
            if (projectName.StartsWith("Language="))
            {
                argIndex++;
                projectName = Environment.GetCommandLineArgs().Length > argIndex ? Environment.GetCommandLineArgs()[argIndex] : String.Empty;
            }
            if (projectName.StartsWith("--minimized"))
            {
                m_HideToTray = true;
                argIndex++;
                projectName = Environment.GetCommandLineArgs().Length > argIndex ? Environment.GetCommandLineArgs()[argIndex] : String.Empty;
            }
            if (projectName.StartsWith("--udpPort="))
            {
                int udpPort = 0;
                if (Int32.TryParse(projectName.Substring("--udpPort=".Length), out udpPort))
                {
                    Settings.Settings.Instance.UdpPort = udpPort;
                }
                argIndex++;
                projectName = Environment.GetCommandLineArgs().Length > argIndex ? Environment.GetCommandLineArgs()[argIndex] : String.Empty;
            }
            if (projectName.StartsWith("--tcpPort="))
            {
                int tcpPort = 0;
                if (Int32.TryParse(projectName.Substring("--tcpPort=".Length), out tcpPort))
                {
                    Settings.Settings.Instance.TcpPort = tcpPort;
                }
                argIndex++;
                projectName = Environment.GetCommandLineArgs().Length > argIndex ? Environment.GetCommandLineArgs()[argIndex] : String.Empty;
            }
            if (projectName.StartsWith("--deviceIndex="))
            {
                int deviceIndex = -1;
                if (Int32.TryParse(projectName.Substring("--deviceIndex=".Length), out deviceIndex))
                {
                    Settings.Settings.Instance.OutputDeviceIndex = deviceIndex;
                }
                argIndex++;
                projectName = Environment.GetCommandLineArgs().Length > argIndex ? Environment.GetCommandLineArgs()[argIndex] : String.Empty;
            }

            if (Settings.Settings.Instance.OutputDeviceIndex != -1)
            {
                SetOutputDevice();
            }

            if (!String.IsNullOrEmpty(projectName))
            {
                if (projectName.EndsWith(".apkg", StringComparison.InvariantCultureIgnoreCase))
                    ImportProject(projectName, false);
                else
                    OpenProject(projectName, false);
            }
            else if (Ares.Settings.Settings.Instance.RecentFiles.GetFiles().Count > 0)
            {
                OpenProject(Ares.Settings.Settings.Instance.RecentFiles.GetFiles()[0].FilePath, false);
            }
            else
            {
                projectNameLabel.Text = m_Project != null ? m_Project.Title : StringResources.NoOpenedProject;
                this.Text = String.Format(StringResources.AresPlayer, projectNameLabel.Text);
            }
#if MONO
			editorButton.Enabled = false;
			startEditorToolStripMenuItem.Enabled = false;
#endif
            // the old network servers are gone (the remote control replaces them)
            Settings.Settings.Instance.UseLegacyNetwork = false;
            Settings.Settings.Instance.UseWebNetwork = false;
            m_Network = new Networks(this, false, false);
            m_Network.InitConnectionData();
            UpdateClientState();
            broadCastTimer.Tick += new EventHandler(broadCastTimer_Tick);
            broadCastTimer.Enabled = true;
            m_Network.ListenForClient();
            Timer updateTimer = new Timer();
            updateTimer.Interval = 50;
            updateTimer.Tick += new EventHandler(updateTimer_Tick);
            updateTimer.Start();
            fileSystemWatcher1.Changed += new System.IO.FileSystemEventHandler(fileSystemWatcher1_Changed);
            if (Settings.Settings.Instance.CheckForUpdate)
            {
                Ares.Online.OnlineOperations.CheckForUpdate(this, false);
            }
        }

        private void helpOnlineToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Ares.Online.OnlineOperations.ShowHelppage(StringResources.HelpPage, this);
        }

        private void checkForUpdateToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Ares.Online.OnlineOperations.CheckForUpdate(this, true);
        }

        private void notifyIcon_DoubleClick(object sender, EventArgs e)
        {
            RestoreFromTray();
        }

        private void Player_Resize(object sender, EventArgs e)
        {
#if !MONO
            if (m_HideToTray && WindowState == FormWindowState.Minimized)
            {
                Hide();
            }
#endif
        }

        private void Player_Shown(object sender, EventArgs e)
        {
            if (m_HideToTray && m_FirstShow)
            {
                Visible = false;
                WindowState = FormWindowState.Minimized;
            }
            m_FirstShow = false;
        }

        private void importToolStripMenuItem_Click(object sender, EventArgs e)
        {
            StopAllPlaying();

            DialogResult result = importFileDialog.ShowDialog(this);
            if (result != System.Windows.Forms.DialogResult.OK)
                return;
            ImportProject(importFileDialog.FileName, false);
        }

        private class MessageBoxProvider : Ares.ModelInfo.IMessageBoxProvider
        {
            public Ares.ModelInfo.MessageBoxResult ShowYesNoCancelBox(string prompt)
            {
                switch (MessageBox.Show(prompt, StringResources.Ares, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question))
                {
                    case DialogResult.Yes:
                        return Ares.ModelInfo.MessageBoxResult.Yes;
                    case DialogResult.No:
                        return Ares.ModelInfo.MessageBoxResult.No;
                    default:
                        return Ares.ModelInfo.MessageBoxResult.Cancel;
                }
            }
        }

        class GUIAndNetworkProgressMonitor : ModelInfo.IProgressMonitor
        {
            private ModelInfo.IProgressMonitor mGUIMonitor;
            private INetwork mNetwork;
            private double mPercent;

            public GUIAndNetworkProgressMonitor(ModelInfo.IProgressMonitor guiMonitor, INetwork network)
            {
                mGUIMonitor = guiMonitor;
                mNetwork = network;
                mPercent = 0;
            }

            public bool Canceled
            {
                get
                {
                    return mGUIMonitor.Canceled;
                }
            }

            public void IncreaseProgress(double percent)
            {
                mGUIMonitor.IncreaseProgress(percent);
                mPercent += percent;
                mNetwork.InformClientOfImportProgress((int)mPercent, String.Empty);
            }

            public void IncreaseProgress(double percent, string text)
            {
                mGUIMonitor.IncreaseProgress(percent, text);
                mPercent += percent;
                mNetwork.InformClientOfImportProgress((int)mPercent, text);
            }

            public void SetIndeterminate(string text)
            {
                mGUIMonitor.SetIndeterminate(text);
                mNetwork.InformClientOfImportProgress(-1, text);
            }

            public void SetProgress(int percent, string text)
            {
                mGUIMonitor.SetProgress(percent, text);
                mPercent = percent;
                mNetwork.InformClientOfImportProgress(percent, text);
            }
        }

        private void ImportProject(String fileName, bool controllerRequest)
        {
            String defaultProjectName = fileName;
            if (defaultProjectName.EndsWith(".apkg"))
            {
                defaultProjectName = defaultProjectName.Substring(0, defaultProjectName.Length - 5);
            }
            defaultProjectName = defaultProjectName + ".ares";
            String projectFileName = defaultProjectName;
            if (!controllerRequest)
            {
                saveFileDialog.FileName = defaultProjectName;
                DialogResult result = saveFileDialog.ShowDialog(this);
                if (result != System.Windows.Forms.DialogResult.OK)
                    return;
                projectFileName = saveFileDialog.FileName;
            }

            Ares.CommonGUI.ProgressMonitor monitor = new Ares.CommonGUI.ProgressMonitor(this, StringResources.Importing);
            GUIAndNetworkProgressMonitor monitor2 = new GUIAndNetworkProgressMonitor(monitor, m_Network);
            Ares.ModelInfo.Importer.Import(monitor2, fileName, projectFileName, controllerRequest, new MessageBoxProvider(), (error, cancelled) => 
            {
                monitor.Close();
                if (error != null)
                {
                    if (controllerRequest)
                    {
                        m_Network.ErrorOccurred(-1, String.Format(StringResources.LoadError, error.Message));
                    }
                    else
                    {
                        System.Windows.Forms.MessageBox.Show(String.Format(StringResources.ImportError, error.Message), StringResources.Ares,
                            System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                    }
                }
                else if (!cancelled)
                {
                    OpenProject(projectFileName, controllerRequest);
                }
            });
        }

        private void modesList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!m_Listen)
                return;
            if (modesList.SelectedIndex >= 0 && m_Project != null && modesList.SelectedIndex < m_Project.GetModes().Count)
            {
                m_PlayingControl.SetMode(m_Project.GetModes()[modesList.SelectedIndex]);
            }
            m_Listen = false;
            UpdateElementsPanel();
            m_Listen = true;
        }

        private void musicList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!m_Listen)
                return;
            IMusicList list = Ares.Data.DataModule.ElementRepository.GetElement(m_PlayingControl.CurrentMusicList) as IMusicList;
            var elements = list != null ? list.GetFileElements() : null;
            if (elements != null && musicList.SelectedIndex < elements.Count)
            {
                m_PlayingControl.SelectMusicElement(elements[musicList.SelectedIndex].Id);
            }
        }

        private void showKeysMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Settings.Instance.ShowKeysInButtons = showKeysMenuItem.Checked;
            Settings.Settings.Instance.Commit();
            m_Listen = false;
            UpdateModesList();
            m_Listen = true;
        }

        private void globalKeyHookItem_CheckedChanged(object sender, EventArgs e)
        {
#if !MONO
            Settings.Settings.Instance.GlobalKeyHook = globalKeyHookItem.Checked;
            Settings.Settings.Instance.Commit();
            MouseKeyboardActivityMonitor.WinApi.Hooker hook;

            if (!globalKeyHookItem.Checked)
            {
                hook = new MouseKeyboardActivityMonitor.WinApi.AppHooker();
            }
            else
            {
                hook = new MouseKeyboardActivityMonitor.WinApi.GlobalHooker();
            }

            m_KeyboardHookManager.Replace(hook);
#endif
            
        }

        private void repeatButton_Click(object sender, EventArgs e)
        {
            m_PlayingControl.SetRepeatCurrentMusic(!m_PlayingControl.MusicRepeat);
        }

        private void musicTagCategoryBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (m_TagsControlsActive)
            {
                m_LastTagCategoryId = m_MusicTagCategories[musicTagCategoryBox.SelectedIndex].Id;
                UpdateElementsPanel();
            }
        }

        private void clearTagsButton_Click(object sender, EventArgs e)
        {
            if (m_TagsControlsActive)
            {
                m_PlayingControl.RemoveAllMusicTags();
            }
        }

        private bool commitFading = true;

        private void tagFadeUpDown_ValueChanged(object sender, EventArgs e)
        {
            int fadeTime = (int)tagFadeUpDown.Value;
            if (commitFading)
            {
                Settings.Settings.Instance.TagMusicFadeTime = fadeTime;
                Settings.Settings.Instance.Commit();
            }
            if (m_PlayingControl != null)
            {
                m_PlayingControl.SetMusicTagFading(fadeTime, Settings.Settings.Instance.TagMusicFadeOnlyOnChange);
            }
        }

        private void fadeOnlyOnChangeBox_CheckedChanged(object sender, EventArgs e)
        {
            bool value = fadeOnlyOnChangeBox.Checked;
            if (commitFading)
            {
                Settings.Settings.Instance.TagMusicFadeOnlyOnChange = value;
                Settings.Settings.Instance.Commit();
            }
            if (m_PlayingControl != null)
            {
                m_PlayingControl.SetMusicTagFading(Settings.Settings.Instance.TagMusicFadeTime, value);
            }
        }

        private void tagCategoryCombinationBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (m_TagsControlsActive && m_PlayingControl != null)
            {
                m_PlayingControl.SetMusicTagCategoriesCombination((Data.TagCategoryCombination)tagCategoryCombinationBox.SelectedIndex);
            }
        }

        private bool m_UpdateCategoryCombinationBox = true;

        private void tagCategoryCombinationBox_DropDown(object sender, EventArgs e)
        {
            m_UpdateCategoryCombinationBox = false;
        }

        private void tagCategoryCombinationBox_DropDownClosed(object sender, EventArgs e)
        {
            m_UpdateCategoryCombinationBox = true;
        }

        private void webAddressLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(webAddressLabel.Text);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, String.Format(StringResources.OpenUrlError, ex.Message), StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }
    }
}
