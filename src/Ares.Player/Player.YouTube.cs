/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 Buttons which play a YouTube playlist. The button is a normal button (a web radio element whose address is the playlist, see
 YouTubePlaylist.cs); the engine hands it over before playing (PlayingModule.ExternalElementHook) and it plays in the YouTube tab
 of Apolo: the first video of the list is opened and YouTube goes on from there.
 While it plays it is "the music" of the program (PlayingControl.External): pause, previous, next, stop (Esc), the seek bar, the
 volume sliders, the title and the track list below the buttons all act on the YouTube video, like on a music of Apolo.
 A music of Apolo that starts stops the playlist. (No waveform: the sound does not pass through the engine.)
*/
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Ares.Data;

namespace Ares.Player
{
    public partial class Player
    {
        private int m_YtElementId = -1;                  // the button which plays a playlist now (or paused)
        private IModeElement m_YtMode;
        private string m_YtListId = "";
        private volatile bool m_YtPaused;
        private WebTab m_YtTab;
        private volatile int m_YtPhase;                  // 0 = nothing, 1 = waiting for the browser, 2 = waiting for the list page, 3 = playing
        private int m_YtTries;
        private int m_YtMiss;                            // reads in a row in which the tab was not on the playlist
        private int m_YtPendingJump = -1;                // a title chosen in the track list before the playlist was running
        private bool m_YtReturnHome;
        private int m_YtWaitPlay;                        // reads while waiting for the video to start (to go back to the home screen)
        private Microsoft.Web.WebView2.Core.CoreWebView2 m_YtHookedCore;   // the browser which already has the volume script
        private bool m_YtBusy;
        private YouTubePlaylist.Snapshot m_YtLast;
        private Timer m_YtTimer;
        private volatile int m_YtTrackId = -1;           // stand-in element of the video which plays now
        private volatile int m_YtVolume = -1;            // wanted volume of the video, 0 to 100 (-1 = leave as it is)
        private double m_YtPos, m_YtLen;
        private int m_YtPosTick;
        private readonly List<IFileElement> m_YtRowFiles = new List<IFileElement>();   // one stand-in element for each title (created once)
        private List<string> m_YtTitles = new List<string>();

        private void InitYouTube()
        {
            Ares.Playing.PlayingModule.ExternalElementHook = YtHook;
            if (m_PlayingControl != null)
                m_PlayingControl.External = new YtMusic(this);
            m_YtTimer = new Timer();
            m_YtTimer.Interval = 700;
            m_YtTimer.Tick += (s, e) => YtTick();
            FormClosed += (s, e) =>
            {
                Ares.Playing.PlayingModule.ExternalElementHook = null;
                m_YtTimer.Stop();
            };
        }

        // The playlist as "the music" for the controls of the program (see PlayingControl.External)
        private sealed class YtMusic : Ares.Players.IExternalMusic
        {
            private readonly Player m_P;
            public YtMusic(Player p) { m_P = p; }

            public bool Active { get { return m_P.m_YtPhase == 3; } }
            public IModeElement ModeElement { get { return m_P.m_YtMode; } }
            public int ListId { get { IModeElement m = m_P.m_YtMode; return m != null && m.StartElement != null ? m.StartElement.Id : -1; } }
            public int TrackId { get { return m_P.m_YtTrackId; } }
            public bool IsPaused { get { return m_P.m_YtPaused; } }
            public void TogglePause() { m_P.YtPost(() => m_P.YtSetPaused(!m_P.m_YtPaused)); }
            public void Next() { m_P.YtPost(() => m_P.YtRun(YouTubePlaylist.NextScript)); }
            public void Previous() { m_P.YtPost(() => m_P.YtRun(YouTubePlaylist.PreviousScript)); }
            public void Stop() { m_P.YtPost(m_P.YtStopPlayback); }
            public void Seek(double positionSeconds)
            {
                m_P.YtPost(() =>
                {
                    m_P.YtRun(YouTubePlaylist.SeekScript(positionSeconds));
                    m_P.m_YtPos = positionSeconds;
                    m_P.m_YtPosTick = Environment.TickCount;
                });
            }
            public bool GetPosition(out double positionSeconds, out double lengthSeconds)
            {
                lengthSeconds = m_P.m_YtLen;
                double pos = m_P.m_YtPos;
                if (!m_P.m_YtPaused)
                    pos += unchecked(Environment.TickCount - m_P.m_YtPosTick) / 1000.0;
                positionSeconds = Math.Max(0, Math.Min(lengthSeconds, pos));
                return lengthSeconds > 0;
            }
            public void SetVolume(int percent)
            {
                m_P.m_YtVolume = percent;
                m_P.YtPost(() => m_P.YtRun(YouTubePlaylist.VolumeScript(percent)));
            }
        }

        private void YtRun(string script)
        {
            try
            {
                if (m_YtTab != null && m_YtTab.View != null && m_YtTab.View.CoreWebView2 != null)
                    m_YtTab.View.CoreWebView2.ExecuteScriptAsync(script);
            }
            catch (Exception swallowed)
            {
                Ares.Settings.ErrorLog.Note(swallowed);
            }
        }

        private static string YtListIdOf(IModeElement me)
        {
            IWebRadioElement radio = me != null ? me.StartElement as IWebRadioElement : null;
            return radio != null ? YouTubePlaylist.IdOfButtonUrl(radio.Url) : null;
        }

        private static bool YtIsYouTubeButton(IModeElement me)
        {
            return YtListIdOf(me) != null;
        }

        private void YtPost(Action action)
        {
            try
            {
                if (IsHandleCreated && !IsDisposed)
                    BeginInvoke(action);
            }
            catch (Exception swallowed)
            {
                Ares.Settings.ErrorLog.Note(swallowed);
            }
        }

        // called by the engine for every button which is started (click, key, remote control, pad...), from any thread
        private bool YtHook(IModeElement me)
        {
            string id = YtListIdOf(me);
            if (id != null)
            {
                YtPost(() => YtToggle(me, id));
                return true;
            }
            // a music of Apolo starts: there is only one music at a time, the playlist of YouTube stops
            if (m_YtPhase != 0 && (KindsOf(me.StartElement, 0) & 1) != 0)
                YtPost(YtStopPlayback);
            return false;
        }

        // ---- buttons ----

        /// <summary>Asks for the address of a playlist and makes a button of it in the folder which is shown.</summary>
        private void AddYouTubeButton()
        {
            string text = Prompt(this, T("Playlist do YouTube", "YouTube playlist"),
                T("Endereço da playlist (cole o link):", "Address of the playlist (paste the link):"), "");
            if (text == null)
                return;
            string id = YouTubePlaylist.ExtractId(text);
            if (id == null)
            {
                Ares.CommonGUI.MessageDialog.Show(this, T("Isso não parece o endereço de uma playlist do YouTube. Abra a playlist no YouTube e copie o endereço da barra (ele tem \"list=\" no meio).",
                    "That does not look like the address of a YouTube playlist. Open the playlist on YouTube and copy the address from the bar (it has \"list=\" in it)."),
                    StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string name = Prompt(this, T("Playlist do YouTube", "YouTube playlist"), T("Nome do botão:", "Button name:"),
                T("Playlist do YouTube", "YouTube playlist"));
            if (name == null)
                return;
            IWebRadioElement radio = DataModule.ElementFactory.CreateWebRadioElement(name);
            radio.Url = YouTubePlaylist.UrlFor(id);
            IModeElement element = DataModule.ElementFactory.CreateModeElement(name, radio);
            AddElementsToOpenFolder(new List<IModeElement> { element }, name);
        }

        private void YtSetButtonChecked(int elementId, bool on)
        {
            CheckBox box;
            if (elementId < 0 || !m_ButtonsForIds.TryGetValue(elementId, out box))
                return;
            bool old = m_Listen;
            m_Listen = false;
            try
            {
                if (box.Checked != on)
                    box.Checked = on;
            }
            finally
            {
                m_Listen = old;
            }
        }

        // ---- start, pause, stop ----

        private void YtToggle(IModeElement me, string listId)
        {
            if (m_YtPhase == 3 && m_YtElementId == me.Id)
            {
                YtSetPaused(!m_YtPaused);
                return;
            }
            YtStart(me, listId);
        }

        private WebTab YtFindTab()
        {
            foreach (WebTab tab in m_WebWindow.Tabs)
            {
                if (tab.Kind == "web" && tab.Url != null && tab.Url.IndexOf("youtube.com", StringComparison.OrdinalIgnoreCase) >= 0)
                    return tab;
            }
            return m_WebWindow.EnsureYouTubeTab();
        }

        private void YtStart(IModeElement me, string listId)
        {
            if (m_WebWindow == null || m_WebWindow.IsDisposed)
                return;
            m_PlayingControl.External = m_PlayingControl.External ?? new YtMusic(this);
            if (m_YtPhase != 0)
                YtStopPlayback();   // another playlist of YouTube was playing
            // only one music at a time: the music of Apolo stops (and another playlist of YouTube too)
            m_PlayingControl.StopMusicOnly();
            m_YtTab = YtFindTab();
            m_YtMode = me;
            m_YtElementId = me.Id;
            m_YtListId = listId;
            m_YtPaused = false;
            m_YtLast = null;
            m_YtMiss = 0;
            m_YtTries = 0;
            m_YtTrackId = -1;
            m_YtPos = 0;
            m_YtLen = 0;
            m_YtTitles = new List<string>();
            m_YtVolume = m_PlayingControl.MusicVolume * m_PlayingControl.GlobalVolume / 100;
            m_YtWaitPlay = 0;
            m_YtReturnHome = !m_ShowingWeb;
            m_YtPhase = 1;
            m_YtTimer.Interval = 300;   // quick while the page is being opened
            YtSetButtonChecked(me.Id, true);
            ShowYtRows(me.Id);
            // A tab which is not on the screen loads slowly (the browser holds it back), so the YouTube tab is shown while the
            // playlist opens and the program goes back to the home screen by itself when the video is playing (see YtFollow)
            ShowWebTab(m_YtTab);
            m_YtTimer.Start();
        }

        private void YtSetPaused(bool pause)
        {
            if (m_YtPhase != 3)
                return;
            YtRun(pause ? YouTubePlaylist.PauseScript : YouTubePlaylist.PlayScript);
            m_YtPaused = pause;
            YtSetButtonChecked(m_YtElementId, !pause);
        }

        // stop (the Esc key, the stop button, another music starting): the video is paused and the program lets go of it
        private void YtStopPlayback()
        {
            if (m_YtPhase == 0)
                return;
            if (m_YtPhase == 3)
                YtRun(YouTubePlaylist.PauseScript);
            YtStop(false);
        }

        private void YtStop(bool tell)
        {
            int id = m_YtElementId;
            m_YtPhase = 0;
            m_YtElementId = -1;
            m_YtMode = null;
            m_YtPaused = false;
            m_YtTrackId = -1;
            m_YtPendingJump = -1;
            m_YtTimer.Stop();
            YtRun(YouTubePlaylist.ClearVolumeScript);
            YtSetButtonChecked(id, false);
            if (m_TrackPanel != null && m_TrackElementId == id)
            {
                BuildTrackRows(FindTrackModeElement(id));
                FillTrackList(false);
            }
            if (tell)
                ShowToast(new Ares.Players.Message(Ares.Players.MessageType.Info,
                    T("A playlist do YouTube parou.", "The YouTube playlist stopped.")));
        }

        private void YtFail(string pt, string en)
        {
            YtStop(false);
            if (m_YtReturnHome)
                ShowHome();
            ShowToast(new Ares.Players.Message(Ares.Players.MessageType.Error, T(pt, en)));
        }

        // ---- the clock: waits for the page, then follows it ----

        private async void YtTick()
        {
            if (m_YtBusy || m_YtPhase == 0)
                return;
            WebTab tab = m_YtTab;
            if (tab == null || m_WebWindow == null || m_WebWindow.IsDisposed || !m_WebWindow.Tabs.Contains(tab))
            {
                YtStop(true);
                return;
            }
            Microsoft.Web.WebView2.Core.CoreWebView2 core = tab.View != null ? tab.View.CoreWebView2 : null;
            m_YtBusy = true;
            try
            {
                if (m_YtPhase == 1)
                {
                    if (core == null)
                    {
                        if (++m_YtTries > 90)
                            YtFail("O navegador do Apolo não abriu a aba do YouTube.", "Apolo's browser did not open the YouTube tab.");
                        return;
                    }
                    if (!ReferenceEquals(m_YtHookedCore, core))
                    {
                        // keeps the volume of Apolo on every new video from its first moment (YouTube starts each one at its own volume)
                        await core.AddScriptToExecuteOnDocumentCreatedAsync(YouTubePlaylist.VolumeHookScript);
                        m_YtHookedCore = core;
                    }
                    YtRun(YouTubePlaylist.VolumeScript(Math.Max(0, m_YtVolume)));
                    core.Navigate(YouTubePlaylist.UrlFor(m_YtListId));
                    m_YtPhase = 2;
                    m_YtTries = 0;
                    return;
                }
                if (core == null)
                    return;
                if (m_YtPhase == 2)
                {
                    string src = tab.View.Source != null ? tab.View.Source.ToString() : "";
                    if (src.IndexOf("/playlist", StringComparison.OrdinalIgnoreCase) < 0 || src.IndexOf(m_YtListId, StringComparison.Ordinal) < 0)
                    {
                        if (++m_YtTries > 130)
                            YtFail("A playlist do YouTube não abriu. Confira o endereço e a internet.", "The YouTube playlist did not open. Check the address and the internet.");
                        return;
                    }
                    string answer = await core.ExecuteScriptAsync(YouTubePlaylist.StartScript);
                    if (answer != null && answer.Trim('"') == "1")
                    {
                        m_YtPhase = 3;   // from now on it is "the music" of the program
                        m_YtTimer.Interval = 700;
                        m_YtMiss = 0;
                    }
                    else if (++m_YtTries > 130)
                    {
                        YtFail("Não consegui iniciar a playlist (a playlist está vazia ou é privada?).", "Could not start the playlist (is it empty or private?).");
                    }
                    return;
                }
                YouTubePlaylist.Snapshot snap = YouTubePlaylist.Snapshot.Parse(await core.ExecuteScriptAsync(YouTubePlaylist.ReadScript));
                if (m_YtPhase != 3)
                    return;
                YtFollow(snap);
            }
            catch (Exception ex)
            {
                Ares.Settings.ErrorLog.Note(ex);
            }
            finally
            {
                m_YtBusy = false;
            }
        }

        private void YtFollow(YouTubePlaylist.Snapshot snap)
        {
            if (snap == null || snap.Page == "error")
                return;
            if (!snap.IsPlaying(m_YtListId))
            {
                // the user went somewhere else in the tab: the playlist is no longer playing
                if (++m_YtMiss >= 8)
                    YtStop(false);
                return;
            }
            m_YtMiss = 0;
            m_YtLast = snap;
            bool paused = snap.Paused || snap.Ended;
            if (paused != m_YtPaused)
            {
                m_YtPaused = paused;
                YtSetButtonChecked(m_YtElementId, !paused);
            }
            // the video is really playing: back to the home screen (after 30 s anyway)
            if (m_YtReturnHome && (snap.Position > 0.4 || ++m_YtWaitPlay > 40))
            {
                m_YtReturnHome = false;
                if (m_ShowingWeb)
                    ShowHome();
            }
            m_YtPos = snap.Position;
            m_YtLen = snap.Duration;
            m_YtPosTick = Environment.TickCount;
            // the volume of the sliders of Apolo is kept (YouTube sets its own when a new video starts)
            int wanted = m_YtVolume;
            if (wanted >= 0 && snap.Volume >= 0 && Math.Abs(snap.Volume * 100 - wanted) > 2)
                YtRun(YouTubePlaylist.VolumeScript(wanted));
            if (m_YtPendingJump >= 0 && m_YtPendingJump < snap.Items.Count)
            {
                int jump = m_YtPendingJump;
                m_YtPendingJump = -1;
                if (jump != snap.Index)
                    YtRun(YouTubePlaylist.JumpScript(jump));
            }
            bool changed = snap.Items.Count != m_YtTitles.Count;
            for (int i = 0; !changed && i < snap.Items.Count; i++)
                changed = snap.Items[i] != m_YtTitles[i];
            if (changed)
            {
                m_YtTitles = new List<string>(snap.Items);
                while (m_YtRowFiles.Count < m_YtTitles.Count)
                    m_YtRowFiles.Add(DataModule.ElementFactory.CreateFileElement(YouTubePlaylist.StandInPath, SoundFileType.Music));
                for (int i = 0; i < m_YtTitles.Count; i++)
                    m_YtRowFiles[i].Title = string.IsNullOrEmpty(m_YtTitles[i]) ? "YouTube " + (i + 1) : m_YtTitles[i];
            }
            // the video which plays now is "the music": its stand-in element gives the title to the program
            m_YtTrackId = snap.Index >= 0 && snap.Index < m_YtRowFiles.Count ? m_YtRowFiles[snap.Index].Id : -1;
            if (changed)
                ShowYtRows(m_YtElementId);
            YtSelectCurrentRow();
        }

        // ---- the track list below the buttons ----

        // the rows of a YouTube button: one for each title the page shows (nothing before the playlist is running)
        private bool YtBuildRows(IModeElement me)
        {
            if (!YtIsYouTubeButton(me))
                return false;
            m_TrackRows.Clear();
            if (me.Id != m_YtElementId)
                return true;
            for (int i = 0; i < m_YtTitles.Count && i < m_YtRowFiles.Count; i++)
            {
                IFileElement stand = m_YtRowFiles[i];
                m_TrackRows.Add(new TrackRow { File = stand, Index = -1, Number = i + 1, Original = i + 1, Loaded = true });
            }
            return true;
        }

        private void ShowYtRows(int elementId)
        {
            if (m_TrackPanel == null)
                return;
            if (m_TrackElementId == elementId)
            {
                BuildTrackRows(FindTrackModeElement(elementId));
                FillTrackList(false);
            }
            else if (m_TrackState == 2)
            {
                ShowTrackListOfClicked(elementId);
            }
        }

        private void YtSelectCurrentRow()
        {
            if (m_TrackList == null || m_YtLast == null || m_TrackElementId != m_YtElementId || m_YtLast.Index < 0)
                return;
            for (int i = 0; i < m_TrackShown.Count; i++)
            {
                if (m_TrackShown[i].Original - 1 == m_YtLast.Index)
                {
                    if (m_TrackList.SelectedIndex != i)
                        m_TrackList.SelectedIndex = i;
                    return;
                }
            }
        }

        // a title chosen in the track list
        private void YtPlayRow(IModeElement me, int originalNumber)
        {
            int index = originalNumber - 1;
            if (m_YtPhase == 3 && m_YtElementId == me.Id)
            {
                YtRun(YouTubePlaylist.JumpScript(index));
                if (m_YtPaused)
                    YtSetPaused(false);
                return;
            }
            YtToggle(me, YtListIdOf(me));
            m_YtPendingJump = index;
        }
    }
}
