/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 Buttons which play a YouTube playlist. The button is a normal button (a web radio element whose address is the playlist, see
 YouTubePlaylist.cs); the engine hands it over before playing (PlayingModule.ExternalElementHook) and it plays in the YouTube tab
 of Apolo: the first video of the list is opened and YouTube goes on from there. A click on the same button pauses / continues.
 The titles of the list are read from the page and shown in the track list below the buttons; a click on a title plays that video.
 When a music button of Apolo starts, the playlist is paused.
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
        private string m_YtListId = "";
        private bool m_YtPaused;
        private WebTab m_YtTab;
        private int m_YtPhase;                           // 0 = nothing, 1 = waiting for the browser, 2 = waiting for the list page, 3 = playing
        private int m_YtTries;
        private int m_YtMiss;                            // reads in a row in which the tab was not on the playlist
        private int m_YtTick;
        private int m_YtPendingJump = -1;                // a title chosen in the track list before the playlist was running
        private bool m_YtReturnHome;
        private bool m_YtBusy;
        private YouTubePlaylist.Snapshot m_YtLast;
        private Timer m_YtTimer;
        private readonly List<IFileElement> m_YtRowFiles = new List<IFileElement>();   // one stand-in element for each title (created once)
        private List<string> m_YtTitles = new List<string>();

        private void InitYouTube()
        {
            Ares.Playing.PlayingModule.ExternalElementHook = YtHook;
            m_YtTimer = new Timer();
            m_YtTimer.Interval = 700;
            m_YtTimer.Tick += (s, e) => YtTick();
            FormClosed += (s, e) =>
            {
                Ares.Playing.PlayingModule.ExternalElementHook = null;
                m_YtTimer.Stop();
            };
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
            // a music of Apolo starts: the playlist of YouTube gives way
            if (m_YtPhase == 3 && !m_YtPaused && (KindsOf(me.StartElement, 0) & 1) != 0)
                YtPost(() => YtSetPaused(true));
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
            int previous = m_YtElementId;
            m_PlayingControl.StopMusicOnly();
            m_YtTab = YtFindTab();
            m_YtElementId = me.Id;
            m_YtListId = listId;
            m_YtPaused = false;
            m_YtLast = null;
            m_YtMiss = 0;
            m_YtTries = 0;
            m_YtPhase = 1;
            m_YtReturnHome = !m_ShowingWeb;
            if (previous != -1 && previous != me.Id)
                YtSetButtonChecked(previous, false);
            YtSetButtonChecked(me.Id, true);
            ShowYtRows(me.Id);
            // the browser of a tab is created when the tab is first shown
            if (m_YtTab.View == null || m_YtTab.View.CoreWebView2 == null)
                ShowWebTab(m_YtTab);
            m_YtTimer.Start();
        }

        private void YtSetPaused(bool pause)
        {
            if (m_YtPhase != 3 || m_YtTab == null || m_YtTab.View == null || m_YtTab.View.CoreWebView2 == null)
                return;
            try
            {
                m_YtTab.View.CoreWebView2.ExecuteScriptAsync(pause ? YouTubePlaylist.PauseScript : YouTubePlaylist.PlayScript);
            }
            catch (Exception swallowed)
            {
                Ares.Settings.ErrorLog.Note(swallowed);
            }
            m_YtPaused = pause;
            YtSetButtonChecked(m_YtElementId, !pause);
        }

        private void YtStop(bool tell)
        {
            int id = m_YtElementId;
            m_YtPhase = 0;
            m_YtElementId = -1;
            m_YtPaused = false;
            m_YtPendingJump = -1;
            m_YtTimer.Stop();
            YtSetButtonChecked(id, false);
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
                        if (++m_YtTries > 40)
                            YtFail("O navegador do Apolo não abriu a aba do YouTube.", "Apolo's browser did not open the YouTube tab.");
                        return;
                    }
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
                        if (++m_YtTries > 60)
                            YtFail("A playlist do YouTube não abriu. Confira o endereço e a internet.", "The YouTube playlist did not open. Check the address and the internet.");
                        return;
                    }
                    string answer = await core.ExecuteScriptAsync(YouTubePlaylist.StartScript);
                    if (answer != null && answer.Trim('"') == "1")
                    {
                        m_YtPhase = 3;
                        m_YtMiss = 0;
                        m_YtTick = 0;
                        if (m_YtReturnHome && m_ShowingWeb)
                            ShowHome();
                    }
                    else if (++m_YtTries > 60)
                    {
                        YtFail("Não consegui iniciar a playlist (a playlist está vazia ou é privada?).", "Could not start the playlist (is it empty or private?).");
                    }
                    return;
                }
                // playing: look at the page every other tick
                if ((m_YtTick++ & 1) != 0)
                    return;
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
            if (m_YtPendingJump >= 0 && m_YtPendingJump < snap.Items.Count)
            {
                int jump = m_YtPendingJump;
                m_YtPendingJump = -1;
                if (jump != snap.Index)
                    m_YtTab.View.CoreWebView2.ExecuteScriptAsync(YouTubePlaylist.JumpScript(jump));
            }
            bool changed = snap.Items.Count != m_YtTitles.Count;
            for (int i = 0; !changed && i < snap.Items.Count; i++)
                changed = snap.Items[i] != m_YtTitles[i];
            if (changed)
            {
                m_YtTitles = new List<string>(snap.Items);
                while (m_YtRowFiles.Count < m_YtTitles.Count)
                {
                    IFileElement stand = DataModule.ElementFactory.CreateFileElement("YouTube", SoundFileType.Music);
                    m_YtRowFiles.Add(stand);
                }
                ShowYtRows(m_YtElementId);
            }
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
                stand.Title = string.IsNullOrEmpty(m_YtTitles[i]) ? "YouTube " + (i + 1) : m_YtTitles[i];
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
            if (m_YtPhase == 3 && m_YtElementId == me.Id && m_YtTab != null && m_YtTab.View != null && m_YtTab.View.CoreWebView2 != null)
            {
                m_YtTab.View.CoreWebView2.ExecuteScriptAsync(YouTubePlaylist.JumpScript(index));
                if (m_YtPaused)
                    YtSetPaused(false);
                return;
            }
            YtToggle(me, YtListIdOf(me));
            m_YtPendingJump = index;
        }
    }
}
