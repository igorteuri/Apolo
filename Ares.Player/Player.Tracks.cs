using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Ares.Data;

namespace Ares.Player
{
    // Side panel with the list of music files of a button's playlist: play by double click, search, reorder.
    public partial class Player
    {
        private class TrackRow
        {
            public IFileElement File;
            public int Index; // position in the container, -1 when not a top-level element
        }

        private Panel m_TrackPanel;
        private Label m_TrackTitle;
        private Button m_TrackClose;
        private Button m_TrackUp;
        private Button m_TrackDown;
        private TextBox m_TrackSearch;
        private ListBox m_TrackList;
        private bool m_TrackOpen = false;
        private int m_TrackElementId = -1;
        private string m_TrackModeTitle;
        private string m_TrackElementTitle;
        private int m_TrackShrink = 0;
        private int m_TrackGrew = 0;
        private List<TrackRow> m_TrackRows = new List<TrackRow>();
        private List<TrackRow> m_TrackShown = new List<TrackRow>();
        private Timer m_TrackPlayTimer;
        private int m_TrackPlayId;

        private void InitTrackPanel()
        {
            try
            {
                Control host = elementsPanel.Parent;
                if (host == null)
                    return;
                m_TrackPanel = new Panel();
                m_TrackPanel.Name = "trackPanel";
                m_TrackPanel.Visible = false;
                m_TrackTitle = new Label();
                m_TrackTitle.AutoEllipsis = true;
                m_TrackTitle.TextAlign = ContentAlignment.MiddleLeft;
                m_TrackClose = MakeSmallButton("✕", T("Fechar", "Close"));
                m_TrackUp = MakeSmallButton("▲", T("Mover música para cima (lista em sequência)", "Move track up (sequential list)"));
                m_TrackDown = MakeSmallButton("▼", T("Mover música para baixo (lista em sequência)", "Move track down (sequential list)"));
                m_TrackSearch = new TextBox();
                m_TrackList = new ListBox();
                m_TrackList.IntegralHeight = false;
                m_TrackList.HorizontalScrollbar = true;

                m_TrackPanel.Controls.Add(m_TrackList);
                m_TrackPanel.Controls.Add(m_TrackSearch);
                m_TrackPanel.Controls.Add(m_TrackTitle);
                m_TrackPanel.Controls.Add(m_TrackClose);
                m_TrackPanel.Controls.Add(m_TrackUp);
                m_TrackPanel.Controls.Add(m_TrackDown);
                m_TrackPanel.Resize += (s, e) => LayoutTrackPanel();
                host.Controls.Add(m_TrackPanel);

                m_TrackClose.Click += (s, e) => CloseTrackPanel();
                m_TrackUp.Click += (s, e) => MoveTrack(-1);
                m_TrackDown.Click += (s, e) => MoveTrack(1);
                m_TrackSearch.TextChanged += (s, e) => FillTrackList();
                m_TrackSearch.HandleCreated += (s, e) =>
                    SendMessage(m_TrackSearch.Handle, 0x1501, (IntPtr)1, T("Buscar música...", "Search track..."));
                m_TrackSearch.KeyDown += (s, e) =>
                {
                    if (e.KeyCode == System.Windows.Forms.Keys.Escape)
                    {
                        m_TrackSearch.Text = "";
                        e.SuppressKeyPress = true;
                    }
                    else if (e.KeyCode == System.Windows.Forms.Keys.Enter)
                    {
                        e.SuppressKeyPress = true;
                        if (m_TrackList.SelectedIndex < 0 && m_TrackList.Items.Count > 0)
                            m_TrackList.SelectedIndex = 0;
                        PlaySelectedTrack();
                    }
                    else if (e.KeyCode == System.Windows.Forms.Keys.Down && m_TrackList.Items.Count > 0)
                    {
                        m_TrackList.SelectedIndex = Math.Min(m_TrackList.Items.Count - 1, m_TrackList.SelectedIndex + 1);
                        e.SuppressKeyPress = true;
                    }
                    else if (e.KeyCode == System.Windows.Forms.Keys.Up && m_TrackList.Items.Count > 0)
                    {
                        m_TrackList.SelectedIndex = Math.Max(0, m_TrackList.SelectedIndex - 1);
                        e.SuppressKeyPress = true;
                    }
                };
                m_TrackList.DoubleClick += (s, e) =>
                {
                    Point p = m_TrackList.PointToClient(Cursor.Position);
                    int idx = m_TrackList.IndexFromPoint(p);
                    if (idx >= 0)
                    {
                        m_TrackList.SelectedIndex = idx;
                        PlaySelectedTrack();
                    }
                };
                m_TrackList.KeyDown += (s, e) =>
                {
                    if (e.KeyCode == System.Windows.Forms.Keys.Enter)
                    {
                        e.SuppressKeyPress = true;
                        PlaySelectedTrack();
                    }
                };
                m_TrackList.SelectedIndexChanged += (s, e) => UpdateTrackButtons();
                elementsPanel.VisibleChanged += (s, e) => m_TrackPanel.Visible = m_TrackOpen && elementsPanel.Visible;

                m_TrackPlayTimer = new Timer();
                m_TrackPlayTimer.Interval = 600;
                m_TrackPlayTimer.Tick += (s, e) =>
                {
                    m_TrackPlayTimer.Stop();
                    m_PlayingControl.SelectMusicElement(m_TrackPlayId);
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }

        private static Button MakeSmallButton(string text, string tip)
        {
            Button b = new Button();
            b.Text = text;
            b.FlatStyle = FlatStyle.System;
            b.UseVisualStyleBackColor = true;
            b.TabStop = false;
            ToolTip t = new ToolTip();
            t.SetToolTip(b, tip);
            return b;
        }

        private void LayoutTrackPanel()
        {
            if (m_TrackPanel == null)
                return;
            int h = Math.Max(26, m_TrackSearch.PreferredHeight + 2);
            int w = m_TrackPanel.ClientSize.Width;
            int bw = h;
            m_TrackClose.SetBounds(w - bw, 0, bw, h);
            m_TrackDown.SetBounds(w - 2 * bw - 2, 0, bw, h);
            m_TrackUp.SetBounds(w - 3 * bw - 4, 0, bw, h);
            m_TrackTitle.SetBounds(0, 0, Math.Max(20, w - 3 * bw - 8), h);
            m_TrackSearch.SetBounds(0, h + 4, w, m_TrackSearch.Height);
            int top = h + 4 + m_TrackSearch.Height + 4;
            m_TrackList.SetBounds(0, top, w, Math.Max(20, m_TrackPanel.ClientSize.Height - top));
        }

        private IModeElement FindTrackModeElement(int id)
        {
            IMode mode = CurrentMode();
            if (mode == null)
                return null;
            foreach (IModeElement me in mode.GetElements())
            {
                if (me.Id == id)
                    return me;
            }
            return null;
        }

        private IGeneralElementContainer TrackContainer()
        {
            IModeElement me = FindTrackModeElement(m_TrackElementId);
            return me != null ? me.StartElement as IGeneralElementContainer : null;
        }

        /// <summary>Opens (or switches) the track list for a button of the current mode.</summary>
        private void ShowTrackPanel(int modeElementId)
        {
            if (m_TrackPanel == null)
                return;
            IModeElement me = FindTrackModeElement(modeElementId);
            IMode mode = CurrentMode();
            if (me == null || mode == null)
                return;
            m_TrackElementId = modeElementId;
            m_TrackModeTitle = mode.Title;
            m_TrackElementTitle = me.Title;
            if (m_TrackSearch.Text.Length > 0)
                m_TrackSearch.Text = "";
            BuildTrackRows(me);
            FillTrackList();
            OpenTrackLayout();
            SyncTrackSelection();
        }

        private void BuildTrackRows(IModeElement me)
        {
            m_TrackRows.Clear();
            IGeneralElementContainer container = me.StartElement as IGeneralElementContainer;
            if (container == null)
                return;
            IList<IContainerElement> top = container.GetGeneralElements();
            IList<IFileElement> files = container.GetFileElements();
            foreach (IFileElement f in files)
            {
                TrackRow row = new TrackRow();
                row.File = f;
                row.Index = -1;
                for (int i = 0; i < top.Count; i++)
                {
                    if (top[i].InnerElement != null && top[i].InnerElement.Id == f.Id)
                    {
                        row.Index = i;
                        break;
                    }
                }
                m_TrackRows.Add(row);
            }
        }

        private void FillTrackList()
        {
            if (m_TrackList == null)
                return;
            string q = m_TrackSearch.Text.Trim();
            int keepId = -1;
            if (m_TrackList.SelectedIndex >= 0 && m_TrackList.SelectedIndex < m_TrackShown.Count)
                keepId = m_TrackShown[m_TrackList.SelectedIndex].File.Id;
            m_TrackShown.Clear();
            m_TrackList.BeginUpdate();
            m_TrackList.Items.Clear();
            for (int i = 0; i < m_TrackRows.Count; i++)
            {
                string title = m_TrackRows[i].File.Title;
                if (q.Length > 0 && title.IndexOf(q, StringComparison.CurrentCultureIgnoreCase) < 0)
                    continue;
                m_TrackShown.Add(m_TrackRows[i]);
                m_TrackList.Items.Add((i + 1).ToString() + "   " + title);
            }
            m_TrackList.EndUpdate();
            for (int i = 0; i < m_TrackShown.Count; i++)
            {
                if (m_TrackShown[i].File.Id == keepId)
                {
                    m_TrackList.SelectedIndex = i;
                    break;
                }
            }
            string kind = (TrackContainer() is ISequentialContainer) ? T("em sequência", "sequential") : T("aleatória", "random");
            m_TrackTitle.Text = m_TrackElementTitle + "  ·  " + m_TrackRows.Count + " " + T("músicas", "tracks") + " (" + kind + ")";
            UpdateTrackButtons();
        }

        private void UpdateTrackButtons()
        {
            bool seq = TrackContainer() is ISequentialContainer;
            int idx = m_TrackList.SelectedIndex;
            bool canMove = seq && idx >= 0 && idx < m_TrackShown.Count && m_TrackShown[idx].Index >= 0 && m_TrackSearch.Text.Trim().Length == 0;
            m_TrackUp.Enabled = canMove && m_TrackShown[idx].Index > 0;
            m_TrackDown.Enabled = canMove && m_TrackShown[idx].Index < m_TrackRows.Count - 1;
        }

        private void OpenTrackLayout()
        {
            Control host = elementsPanel.Parent;
            if (!m_TrackOpen)
            {
                int pw = (int)Math.Round(300 * Math.Max(80, Ares.CommonGUI.ThemeManager.ScalePercent) / 110.0);
                int gap = 6;
                int need = pw + gap;
                m_TrackGrew = 0;
                if (WindowState == FormWindowState.Normal)
                {
                    Rectangle area = Screen.FromControl(this).WorkingArea;
                    if (Right + need <= area.Right)
                    {
                        m_TrackGrew = need;
                        Width += need;
                    }
                    else if (Left - need >= area.Left)
                    {
                        // not enough room on the right: move the window left, then grow
                        m_TrackGrew = need;
                        Left -= need;
                        Width += need;
                    }
                }
                int minCards = 200;
                m_TrackShrink = need;
                if (elementsPanel.Width - m_TrackShrink < minCards)
                {
                    m_TrackShrink = Math.Max(0, elementsPanel.Width - minCards);
                    pw = Math.Max(180, m_TrackShrink - gap);
                }
                int barHeight = m_ListBar != null ? m_ListBar.Height + 4 : 0;
                int top = elementsPanel.Top - barHeight;
                elementsPanel.Width -= m_TrackShrink;
                if (m_ListBar != null)
                    m_ListBar.Width -= m_TrackShrink;
                m_TrackPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right;
                m_TrackPanel.SetBounds(elementsPanel.Right + gap, top, pw, elementsPanel.Bottom - top);
                m_TrackOpen = true;
                m_TrackPanel.Visible = elementsPanel.Visible;
                m_TrackPanel.BringToFront();
                LayoutTrackPanel();
            }
        }

        private void CloseTrackPanel()
        {
            if (!m_TrackOpen)
                return;
            m_TrackOpen = false;
            m_TrackPanel.Visible = false;
            elementsPanel.Width += m_TrackShrink;
            if (m_ListBar != null)
                m_ListBar.Width += m_TrackShrink;
            m_TrackShrink = 0;
            if (m_TrackGrew > 0 && WindowState == FormWindowState.Normal)
                Width -= m_TrackGrew;
            m_TrackGrew = 0;
            m_TrackElementId = -1;
        }

        private void PlaySelectedTrack()
        {
            int idx = m_TrackList.SelectedIndex;
            if (idx < 0 || idx >= m_TrackShown.Count || m_PlayingControl == null)
                return;
            IModeElement me = FindTrackModeElement(m_TrackElementId);
            if (me == null || me.StartElement == null)
                return;
            int fileId = m_TrackShown[idx].File.Id;
            bool listActive = m_PlayingControl.CurrentMusicList == me.StartElement.Id && m_PlayingControl.CurrentMusicElement != -1;
            if (listActive || m_PlayingControl.CurrentModeElements.Contains(me))
            {
                m_PlayingControl.SelectMusicElement(fileId);
            }
            else
            {
                // start the playlist first, then jump to the chosen file
                m_PlayingControl.SwitchElement(me.Id);
                m_TrackPlayId = fileId;
                m_TrackPlayTimer.Stop();
                m_TrackPlayTimer.Start();
            }
        }

        // selects the file which is playing now (called when the music changes)
        private void SyncTrackSelection()
        {
            if (!m_TrackOpen || m_PlayingControl == null)
                return;
            IModeElement me = FindTrackModeElement(m_TrackElementId);
            if (me == null || me.StartElement == null || m_PlayingControl.CurrentMusicList != me.StartElement.Id)
                return;
            int id = m_PlayingControl.CurrentMusicElement;
            for (int i = 0; i < m_TrackShown.Count; i++)
            {
                if (m_TrackShown[i].File.Id == id)
                {
                    if (m_TrackList.SelectedIndex != i)
                        m_TrackList.SelectedIndex = i;
                    break;
                }
            }
        }

        private void MoveTrack(int delta)
        {
            int idx = m_TrackList.SelectedIndex;
            ISequentialContainer seq = TrackContainer() as ISequentialContainer;
            if (seq == null || idx < 0 || idx >= m_TrackShown.Count)
                return;
            int from = m_TrackShown[idx].Index;
            int to = from + delta;
            if (from < 0 || to < 0 || to >= m_TrackRows.Count)
                return;
            string modeTitle = m_TrackModeTitle;
            string elementTitle = m_TrackElementTitle;
            ApplyProjectEdit(() => seq.MoveElements(from, from, delta), modeTitle);
            // the project was reloaded: open the list again on the same button
            IMode mode = CurrentMode();
            if (mode == null || mode.Title != modeTitle)
                return;
            foreach (IModeElement me in mode.GetElements())
            {
                if (me.Title == elementTitle)
                {
                    ShowTrackPanel(me.Id);
                    if (to < m_TrackShown.Count)
                        m_TrackList.SelectedIndex = to;
                    break;
                }
            }
        }

        // called after the buttons of a mode were rebuilt
        private void AfterElementsForTrackPanel()
        {
            if (!m_TrackOpen)
                return;
            IMode mode = CurrentMode();
            if (mode == null || mode.Title != m_TrackModeTitle)
            {
                CloseTrackPanel();
                return;
            }
            // refresh the list with the (possibly) new project objects
            foreach (IModeElement me in mode.GetElements())
            {
                if (me.Title == m_TrackElementTitle)
                {
                    m_TrackElementId = me.Id;
                    BuildTrackRows(me);
                    FillTrackList();
                    SyncTrackSelection();
                    return;
                }
            }
            CloseTrackPanel();
        }
    }
}
