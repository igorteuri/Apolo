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
    // The transport row (play/pause, previous, next), the bar of the music and its progress.
    public partial class Player
    {
        private void FitMusicBar()
        {
            if (m_MusicProgressBar == null)
                return;
            if (m_VolumePanel != null)
            {
                LayoutHeader();
                return;
            }
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
                repeatButton.Paint += RepeatButton_Paint;
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

        private ToolStripMenuItem m_MusicPauseItem, m_SoundsPauseItem;

        /// <summary>The main pause button: pauses everything, or resumes it.</summary>
        internal void TogglePauseAll()
        {
            PlayingControl control = m_PlayingControl;
            if (control != null)
            {
                control.TogglePause();
                UpdatePauseButton(control.IsPaused());
            }
        }

        // Pause and Stop: a click acts on everything; a right click offers only the music or only the effects
        private void AddScopeItems()
        {
            ContextMenuStrip pauseMenu = new ContextMenuStrip();
            ContextMenuStrip stopMenu = new ContextMenuStrip();
            m_MusicPauseItem = ScopeItem(pauseMenu, T("Pausar só a música", "Pause only the music"), c => c.ToggleMusicPause());
            m_SoundsPauseItem = ScopeItem(pauseMenu, T("Pausar só os efeitos", "Pause only the effects"), c => c.ToggleSoundsPause());
            ScopeItem(stopMenu, T("Parar só a música", "Stop only the music"), c => c.StopMusicOnly());
            ScopeItem(stopMenu, T("Parar só os efeitos", "Stop only the effects"), c => c.StopSoundEffectsOnly());
            RightClickMenu(m_PauseButton, pauseMenu);
            RightClickMenu(stopButton, stopMenu);
            m_PauseButton.ToolTipText = PauseText(false);
        }

        // left click = everything (the button itself); right click = the choice "only the music / only the effects"
        private void RightClickMenu(ToolStripItem button, ContextMenuStrip menu)
        {
            button.MouseUp += (s, e) =>
            {
                if (e.Button == MouseButtons.Right)
                    menu.Show(Cursor.Position);
            };
        }

        private ToolStripMenuItem ScopeItem(ContextMenuStrip menu, string text, Action<PlayingControl> action)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            item.Click += (s, e) =>
            {
                PlayingControl control = m_PlayingControl;
                if (control != null)
                    action(control);
            };
            menu.Items.Add(item);
            return item;
        }

        private MusicProgressBar m_MusicProgressBar;
        private SoundProgressPanel m_SoundPanel;
        private NowPlayingStrip m_NowPlaying;
        private Ares.CommonGUI.MenuHintButton m_PauseButton;
        private Size m_PauseIconSize;
        private bool m_PauseShown;
        private ToolStripButton m_OutputButton;
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
            m_SoundPanel.PauseRequested += (sender, e) =>
            {
                PlayingControl control = m_PlayingControl;
                if (control != null)
                    control.ToggleSoundPause(e.Handle);
            };
            m_SoundPanel.SeekRequested += (sender, e) =>
            {
                PlayingControl control = m_PlayingControl;
                if (control != null)
                    control.SeekSound(e.Handle, e.Seconds);
            };
            m_SoundPanel.RepeatRequested += (sender, e) =>
            {
                m_RepeatTouched.Add(e.Handle);
                PlayingControl control = m_PlayingControl;
                if (control != null)
                    control.SetSoundRepeat(e.Handle, e.Repeat);
            };
            groupBox3.Controls.Add(m_SoundPanel);

            // the slim "playing now" strip below the header
            m_NowPlaying = new NowPlayingStrip();
            m_NowPlaying.Name = "nowPlayingStrip";
            m_NowPlaying.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            m_NowPlaying.StopRequested += npHandle => { PlayingControl npc = m_PlayingControl; if (npc != null) npc.StopSound(npHandle); };
            m_NowPlaying.PauseRequested += npHandle => { PlayingControl npc = m_PlayingControl; if (npc != null) npc.ToggleSoundPause(npHandle); };
            m_NowPlaying.RepeatRequested += (npHandle, npRepeat) => { m_RepeatTouched.Add(npHandle); PlayingControl npc = m_PlayingControl; if (npc != null) npc.SetSoundRepeat(npHandle, npRepeat); };
            m_NowPlaying.SeekRequested += (npHandle, npSec) => { PlayingControl npc = m_PlayingControl; if (npc != null) npc.SeekSound(npHandle, npSec); };
            Controls.Add(m_NowPlaying);

            // pause button, right behind the stop button
            m_PauseButton = new Ares.CommonGUI.MenuHintButton();
            m_PauseButton.Name = "pauseButton";
            m_PauseButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
            m_PauseButton.ImageScaling = ToolStripItemImageScaling.None;
            m_PauseButton.Click += (sender, e) => TogglePauseAll();
            AddScopeItems();
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
            m_OutputButton = new OutputStripButton(() => m_OutputPopup != null);
            m_OutputButton.Name = "outputDeviceButton";
            m_OutputButton.Tag = "AccentText"; // highlighted like the settings button
            m_OutputButton.Alignment = ToolStripItemAlignment.Right;
            m_OutputButton.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
            m_OutputButton.ImageScaling = ToolStripItemImageScaling.None;
            m_OutputButton.ToolTipText = OutputToolTip();
            m_OutputButton.Click += (sender, e) => ShowOutputPopup();
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
                catch (Exception swallowed)
                {
                    Ares.Settings.ErrorLog.Note(swallowed);
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
                if (m_VolumePanel != null)
                    return; // the header is placed by LayoutHeader
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

        // ---------------------------------------------------------------- waveform of the music in the seek bar

        private string m_WavePath;
        private int m_WaveToken;
        private WaveformCache m_WaveCache;
        private static readonly System.Threading.SemaphoreSlim s_WaveGate = new System.Threading.SemaphoreSlim(1, 1);

        private string MusicFilePathOf(int elementId)
        {
            if (elementId == -1)
                return null;
            Ares.Data.IFileElement file = Ares.Data.DataModule.ElementRepository.GetElement(elementId) as Ares.Data.IFileElement;
            if (file == null || string.IsNullOrEmpty(file.FilePath))
                return null;
            return System.IO.Path.Combine(Ares.Settings.Settings.Instance.MusicDirectory ?? "", file.FilePath);
        }

        // when the music changes: the plain bar at once; the waveform from the cache, or analyzed in the background
        private void UpdateWave(int elementId)
        {
            string path = MusicFilePathOf(elementId);
            if (path == m_WavePath)
                return;
            m_WavePath = path;
            int token = ++m_WaveToken;
            m_MusicProgressBar.SetWave(null);
            if (path == null)
                return;
            if (m_WaveCache == null)
                m_WaveCache = new WaveformCache(System.IO.Path.Combine(Ares.Settings.AppFolders.Root, "waveforms"));
            WaveformCache cache = m_WaveCache;
            System.Threading.Tasks.Task.Run(() =>
            {
                byte[] wave = cache.Get(path);
                if (wave == null)
                {
                    s_WaveGate.Wait();
                    try
                    {
                        if (token != m_WaveToken)
                            return; // the music changed meanwhile
                        wave = Ares.Playing.Audio.WaveformAnalyzer.Analyze(path);
                        cache.Put(path, wave);
                    }
                    finally
                    {
                        s_WaveGate.Release();
                    }
                }
                if (wave == null || IsDisposed)
                    return;
                try
                {
                    BeginInvoke(new System.Windows.Forms.MethodInvoker(() =>
                    {
                        if (token == m_WaveToken)
                            m_MusicProgressBar.SetWave(wave);
                    }));
                }
                catch (Exception swallowed)
                {
                    Ares.Settings.ErrorLog.Note(swallowed); // the window was closed
                }
            });
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
            // the icon is made again when the size of the toolbar icons changed (the scale of the theme is applied after the start)
            Size iconSize = (m_TransportStrip ?? toolStrip1).ImageScalingSize;
            if (m_PauseButton == null || m_PauseShown == paused && m_PauseButton.Image != null && iconSize == m_PauseIconSize)
                return;
            m_PauseIconSize = iconSize;
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
            Image img = Ares.CommonGUI.IconLibrary.Get(showPlayTriangle ? "play" : "pause", (m_TransportStrip ?? toolStrip1).ImageScalingSize);
            return img ?? new Bitmap(20, 20);
        }

        private void UpdateMusicProgress()
        {
            try
            {
                PlayingControl control = m_PlayingControl;
                if (control == null || WindowState == FormWindowState.Minimized || !Visible)
                    return;
                m_MusicProgressBar.Title = control.CurrentMusicElement == -1 ? "" : (musicLabel.Text ?? "");
                UpdateWave(control.CurrentMusicElement);
                double position, length;
                if (control.GetMusicPosition(out position, out length))
                    m_MusicProgressBar.SetProgress(position, length);
                else
                    m_MusicProgressBar.Clear();

                List<Ares.Playing.PlayingFileInfo> sounds = control.GetRunningSounds();
                // the repeat set in the playlist shows in both places (the bars on the left and the chips)
                ShowConfiguredRepeat(sounds);
                if (sounds.Count > 0)
                    m_SoundPanel.UpdateSounds(sounds);
                else
                    m_SoundPanel.ClearSounds();
                if (m_NowPlaying != null && m_NowPlaying.SetSounds(sounds))
                    LayoutMainColumns();

                // nothing playing: the button shows "play" and is dimmed (it was a "pause" icon beside the status "Stopped")
                bool idle = m_TrayState == Ares.CommonGUI.ApoloState.Idle;
                UpdatePauseButton(control.IsPaused() || idle);
                if (m_MusicPauseItem != null)
                {
                    m_MusicPauseItem.Text = control.IsMusicPaused() ? T("Continuar a música", "Resume the music") : T("Pausar só a música", "Pause only the music");
                    m_SoundsPauseItem.Text = control.IsSoundsPaused() ? T("Continuar os efeitos", "Resume the effects") : T("Pausar só os efeitos", "Pause only the effects");
                }
                if (m_PauseButton != null && m_PauseButton.Enabled == idle)
                    m_PauseButton.Enabled = !idle;
                UpdateOutputButton();
            }
            catch (Exception swallowed)
            {
                // the bars are only informative
                Ares.Settings.ErrorLog.Note(swallowed);
            }
        }
    }
}
