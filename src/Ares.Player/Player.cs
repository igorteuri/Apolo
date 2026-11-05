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

        private Ares.Playing.AudioInit m_BassInit;

        private bool m_HideToTray = false;
        private bool m_FirstShow = true;

#if !MONO
        private readonly MouseKeyboardActivityMonitor.KeyboardHookListener m_KeyboardHookManager;
#endif

        public Player(Ares.Playing.AudioInit init)
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
            // APOLO_INSTANCE_TAG: a second copy of the program may run beside the first (to look at it, tests)
            string instanceName = "Ares.Player" + (Environment.GetEnvironmentVariable("APOLO_INSTANCE_TAG") ?? "");
            if (String.IsNullOrEmpty(projectName))
            {
                m_Instance = Ares.Ipc.ApplicationInstance.CreateOrActivate(instanceName);
            }
            else
            {
                m_Instance = Ares.Ipc.ApplicationInstance.CreateOrActivate(instanceName, projectName);
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
            BeginHiddenStart();
            InitWindowFit();
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
            // the repeat mode of the last session
            int savedRepeat;
            if (int.TryParse(Ares.CommonGUI.Preferences.Get("MusicRepeatMode", "0"), out savedRepeat) && savedRepeat >= 0 && savedRepeat <= 2)
                m_PlayingControl.SetMusicRepeatMode(savedRepeat);
            InitMusicProgressBar();
            InitLibraryDrop();
            InitModeTree();
            InitEditMode();
            InitPreview();
            InitTray();
            InitWeb();
            InitStatusBar();
            InitProjectSwitch();
            InitListMode();
            InitTrackPanel();
            InitTransportRow();
            InitHeader();
            InitOverlay();
            InitRemote();
            InitDiscord();
            InitDeck();
            InitShortcuts();
            InitBankMenu();
            OrganizeExtrasMenu();
        }

#if !MONO
        void m_KeyboardHookManager_KeyDown(object sender, KeyEventArgs e)
        {
            // with the global capture of keys on: Esc, left and right (stop, previous, next) are keys of every other program
            // too, so they only count while a window of Apolo is the active one; the keys of the buttons stay global
            if ((e.KeyCode == System.Windows.Forms.Keys.Escape || e.KeyCode == System.Windows.Forms.Keys.Left || e.KeyCode == System.Windows.Forms.Keys.Right)
                && e.Modifiers == System.Windows.Forms.Keys.None && Form.ActiveForm == null)
                return;
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
                // centered over the window of Apolo (on the screen where it is), never outside the screen
                m_MessagesForm.StartPosition = FormStartPosition.Manual;
                Rectangle area = Screen.FromControl(this).WorkingArea;
                int mx = Location.X + (Width - m_MessagesForm.Width) / 2;
                int my = Location.Y + (Height - m_MessagesForm.Height) / 2;
                mx = Math.Max(area.Left, Math.Min(mx, area.Right - m_MessagesForm.Width));
                my = Math.Max(area.Top, Math.Min(my, area.Bottom - m_MessagesForm.Height));
                m_MessagesForm.Location = new Point(mx, my);
                m_MessagesForm.FindMissingRequested += (s, e) => ShowMissingFiles();
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
                if (Ares.CommonGUI.MessageDialog.Show(this, StringResources.ReloadProject, StringResources.Ares, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == System.Windows.Forms.DialogResult.Yes)
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
                if (Ares.CommonGUI.MessageDialog.Show(this, String.Format(StringResources.OpenProjectQuestion, projectName), StringResources.Ares, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == System.Windows.Forms.DialogResult.Yes)
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
            // a packed project (.apkg) is imported, never opened directly (it is a zip file)
            if (filePath != null && filePath.EndsWith(".apkg", StringComparison.OrdinalIgnoreCase))
            {
                ImportProject(filePath, onControllerRequest);
                return;
            }
            if (!onControllerRequest)
                filePath = Ares.Settings.ProjectFiles.Upgrade(filePath); // an old .ares project continues as .apolo
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
                if (Ares.Data.ProjectRecovery.UsedBackup && !onControllerRequest)
                    Ares.CommonGUI.MessageDialog.Show(this, T("O arquivo do projeto estava danificado. Abri a cópia da versão anterior (.bak); o arquivo danificado ficou guardado como .corrompido.", "The project file was damaged. I opened the copy of the previous version (.bak); the damaged file was kept as .corrompido."), StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception e)
            {
                if (!onControllerRequest)
                {
                    Ares.CommonGUI.MessageDialog.Show(this, String.Format(StringResources.LoadError, e.Message), StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Error);
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
        // set while a project is opened again after an edit: the folder to show
        private string m_PreferredModeTitle;

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
            if (HandleUndoKey(keyData))
                return true;
            if (keyData == System.Windows.Forms.Keys.F2 && TryRenameWithF2())
                return true;
            if (HandleProgramShortcut(keyData))
                return true;
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
            if (control == null)
                return;
            m_Listen = false;
            IMode mode = control.CurrentMode;
            modeLabel.Text = mode != null ? mode.Title : String.Empty;
            if (m_Project != null && mode != null && mode.Title != lastMode)
            {
                for (int i = 0; i < m_Project.GetModes().Count; ++i)
                {
                    if (m_Project.GetModes()[i].Title == mode.Title)
                    {
                        SelectModeIndex(i);
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
                m_ButtonsForIds[id].Checked = currentModeElementIds.Contains(id) || (id == m_YtElementId && !m_YtPaused);
            }
            elementsLabel.Text = modeElementsText.ToString();
            StringBuilder soundElementsText = new StringBuilder();
            foreach (int element in control.CurrentSoundElements)
            {
                if (soundElementsText.Length > 0)
                    soundElementsText.Append(", ");
                Ares.Data.IElement soundElement = Ares.Data.DataModule.ElementRepository.GetElement(element);
                if (soundElement != null)
                    soundElementsText.Append(soundElement.Title);
            }
            soundsLabel.Text = soundElementsText.ToString();
            int musicElementId = control.CurrentMusicElement;
            if (musicElementId != lastMusicElementId)
            {
                musicLabel.Text = MusicInfo.GetInfo(musicElementId).LongTitle;
                lastMusicElementId = musicElementId;
                CountMusicPlay(musicElementId);
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
            // the repeat mode is a setting of the player: it shows (and works) before anything plays
            ShowRepeatMode(m_PlayingControl.MusicRepeatMode);
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
            ShowAboutApolo();
        }

        private void Player_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (TryHideToTray(e))
                return;
            if (CancelCloseForUnsaved(e))
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
                        Ares.CommonGUI.MessageDialog.Show(this, StringResources.NoStatusInfoError, StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
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
                Ares.CommonGUI.MessageDialog.Show(this, String.Format(StringResources.TagsDbError, ex.Message), StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                Ares.CommonGUI.MessageDialog.Show(this, String.Format(StringResources.EditorStartError, ex.Message), StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Error);
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

        private void networkSettingsButton_Click(object sender, EventArgs e)
        {
            ShowSettingsDialog(0);
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
            // no automatic search for updates: new versions are delivered in the vault of the supporters (menu Ajuda)
        }

        private void helpOnlineToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenAddress(TutorialsUrl);
        }

        private void checkForUpdateToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowUpdateInfo();
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
                switch (Ares.CommonGUI.MessageDialog.Show(prompt, StringResources.Ares, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question))
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
                if (mNetwork != null) mNetwork.InformClientOfImportProgress((int)mPercent, String.Empty);
            }

            public void IncreaseProgress(double percent, string text)
            {
                mGUIMonitor.IncreaseProgress(percent, text);
                mPercent += percent;
                if (mNetwork != null) mNetwork.InformClientOfImportProgress((int)mPercent, text);
            }

            public void SetIndeterminate(string text)
            {
                mGUIMonitor.SetIndeterminate(text);
                if (mNetwork != null) mNetwork.InformClientOfImportProgress(-1, text);
            }

            public void SetProgress(int percent, string text)
            {
                mGUIMonitor.SetProgress(percent, text);
                mPercent = percent;
                if (mNetwork != null) mNetwork.InformClientOfImportProgress(percent, text);
            }
        }

        private void ImportProject(String fileName, bool controllerRequest)
        {
            String defaultProjectName = fileName;
            if (defaultProjectName.EndsWith(".apkg"))
            {
                defaultProjectName = defaultProjectName.Substring(0, defaultProjectName.Length - 5);
            }
            defaultProjectName = defaultProjectName + Ares.Settings.ProjectFiles.Extension;
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
                        Ares.CommonGUI.MessageDialog.Show(String.Format(StringResources.ImportError, error.Message), StringResources.Ares,
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
            if (IsFoldClick())
                return; // click on the arrow of a folder: no change of the mode
            if (m_EditMode && modesList.SelectedIndices.Count > 1)
                return; // several folders selected (to delete them): the shown folder stays
            ClearElementSelection();
            if (SelectedModeIndex >= 0 && m_Project != null)
            {
                m_PlayingControl.SetMode(m_Project.GetModes()[SelectedModeIndex]);
                // the panel is built right below: the status poll must not build it a second time (a visible blink)
                lastMode = m_Project.GetModes()[SelectedModeIndex].Title;
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
            m_PlayingControl.CycleMusicRepeatMode();
            ShowRepeatMode(m_PlayingControl.MusicRepeatMode);
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
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(webAddressLabel.Text) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Ares.CommonGUI.MessageDialog.Show(this, String.Format(StringResources.OpenUrlError, ex.Message), StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }
    }
}
