using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Ares.Data;

namespace Ares.Player
{
    // Side panel with the list of files of a button's playlist: play by double click, search, reorder,
    // change the playlist type. The panel is always there; it can be collapsed to a thin strip at the
    // right edge (button, menu "Extras" or F9) and resized with the splitter in front of it.
    public partial class Player
    {
        private class TrackRow
        {
            public IFileElement File;
            public int Index; // position in the container, -1 when not a top-level element
        }

        private const int TrackGap = 6;
        private const int TrackStripWidth = 30;

        private GroupBox m_TrackPanel;
        private Panel m_TrackBody;
        private bool m_TrackPinned = false;
        private int m_TrackLastListId = int.MinValue;
        private Panel m_TrackSplit;
        private Button m_TrackStrip;
        private Label m_TrackTitle;
        private Button m_TrackCollapse;
        private Button m_TrackUp;
        private Button m_TrackDown;
        private ComboBox m_TrackKind;
        private bool m_TrackKindLock;
        private TextBox m_TrackSearch;
        private ListBox m_TrackList;
        private int m_TrackState = 0; // 0 = not placed yet, 1 = collapsed, 2 = open
        private int m_TrackWidth = 300;
        private int m_TrackElementId = -1;
        private string m_TrackModeTitle;
        private string m_TrackElementTitle;
        private bool m_LeftOpen = true;
        private int m_ModesLeftOriginal;
        private Button m_LeftStrip;
        private Button m_LeftCollapse;
        private List<TrackRow> m_TrackRows = new List<TrackRow>();
        private List<TrackRow> m_TrackShown = new List<TrackRow>();
        private Timer m_TrackPlayTimer;
        private int m_TrackPlayId;

        private void InitTrackPanel()
        {
            try
            {
                Control host = this;
                int saved;
                if (int.TryParse(Ares.CommonGUI.Preferences.Get("TracksWidth", "300"), out saved))
                    m_TrackWidth = Math.Max(200, Math.Min(900, saved));

                m_TrackPanel = new GroupBox();
                m_TrackPanel.Name = "trackPanel";
                m_TrackPanel.Text = T("Lista de músicas", "Track list");
                m_TrackPanel.Visible = false;
                m_TrackBody = new Panel();
                m_TrackBody.Dock = DockStyle.Fill;
                m_TrackPanel.Controls.Add(m_TrackBody);
                m_TrackSplit = new Panel();
                m_TrackSplit.Name = "trackSplit";
                m_TrackSplit.Cursor = Cursors.VSplit;
                m_TrackSplit.Visible = false;
                m_TrackStrip = new Button();
                m_TrackStrip.Name = "trackStrip";
                m_TrackStrip.Text = "◀";
                m_TrackStrip.Font = new Font(m_TrackStrip.Font.FontFamily, 14f, FontStyle.Bold);
                m_TrackStrip.FlatStyle = FlatStyle.System;
                m_TrackStrip.UseVisualStyleBackColor = true;
                m_TrackStrip.Visible = false;
                new ToolTip().SetToolTip(m_TrackStrip, T("Mostrar a lista de músicas (F9)", "Show the track list (F9)"));

                m_TrackTitle = new Label();
                m_TrackTitle.AutoEllipsis = true;
                m_TrackTitle.TextAlign = ContentAlignment.MiddleLeft;
                m_TrackCollapse = MakeSmallButton("▶", T("Recolher o painel para a lateral (F9)", "Collapse the panel to the side (F9)"));
                m_TrackUp = MakeSmallButton("▲", T("Mover a música para cima (só em lista em sequência)", "Move the track up (sequential lists only)"));
                m_TrackDown = MakeSmallButton("▼", T("Mover a música para baixo (só em lista em sequência)", "Move the track down (sequential lists only)"));
                m_TrackKind = new ComboBox();
                m_TrackKind.DropDownStyle = ComboBoxStyle.DropDownList;
                m_TrackKind.Items.Add(T("Música — aleatória", "Music — random"));
                m_TrackKind.Items.Add(T("Música — em sequência", "Music — sequential"));
                m_TrackKind.Items.Add(T("Efeito sonoro — um aleatório", "Sound effect — one at random"));
                new ToolTip().SetToolTip(m_TrackKind, T("Tipo da lista: muda como este botão toca os arquivos", "List type: changes how this button plays its files"));
                m_TrackSearch = new TextBox();
                m_TrackList = new ListBox();
                m_TrackList.IntegralHeight = false;
                m_TrackList.HorizontalScrollbar = true;

                m_TrackBody.Controls.Add(m_TrackList);
                m_TrackBody.Controls.Add(m_TrackSearch);
                m_TrackBody.Controls.Add(m_TrackKind);
                m_TrackBody.Controls.Add(m_TrackTitle);
                m_TrackBody.Controls.Add(m_TrackCollapse);
                m_TrackBody.Controls.Add(m_TrackUp);
                m_TrackBody.Controls.Add(m_TrackDown);
                m_TrackBody.Resize += (s, e) => LayoutTrackPanel();
                host.Controls.Add(m_TrackPanel);
                host.Controls.Add(m_TrackSplit);
                host.Controls.Add(m_TrackStrip);

                // everything on the right of the left column is positioned by code
                m_ModesLeftOriginal = groupBox4.Left;
                groupBox5.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
                m_TrackPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
                m_TrackSplit.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
                m_TrackStrip.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                m_LeftOpen = Ares.CommonGUI.Preferences.GetBool("LeftOpen", true);
                m_LeftStrip = new Button();
                m_LeftStrip.Name = "leftStrip";
                m_LeftStrip.Text = "▶";
                m_LeftStrip.Font = new Font(m_LeftStrip.Font.FontFamily, 14f, FontStyle.Bold);
                m_LeftStrip.FlatStyle = FlatStyle.System;
                m_LeftStrip.UseVisualStyleBackColor = true;
                m_LeftStrip.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                m_LeftStrip.Visible = false;
                new ToolTip().SetToolTip(m_LeftStrip, T("Mostrar o painel esquerdo (volume, estado, rede) (F8)", "Show the left panel (volume, state, network) (F8)"));
                m_LeftStrip.Click += (s, e) => SetLeftOpen(true);
                m_LeftCollapse = new Button();
                m_LeftCollapse.Name = "leftCollapse";
                m_LeftCollapse.Text = "◀";
                m_LeftCollapse.FlatStyle = FlatStyle.System;
                m_LeftCollapse.UseVisualStyleBackColor = true;
                m_LeftCollapse.TabStop = false;
                m_LeftCollapse.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                new ToolTip().SetToolTip(m_LeftCollapse, T("Esconder o painel esquerdo (F8)", "Hide the left panel (F8)"));
                m_LeftCollapse.Click += (s, e) => SetLeftOpen(false);
                host.Controls.Add(m_LeftStrip);
                host.Controls.Add(m_LeftCollapse);
                SizeChanged += (s, e) => LayoutMainColumns();

                m_TrackCollapse.Click += (s, e) => SetTrackState(1);
                m_TrackStrip.Click += (s, e) => SetTrackState(2);
                m_TrackUp.Click += (s, e) => MoveTrack(-1);
                m_TrackDown.Click += (s, e) => MoveTrack(1);
                m_TrackKind.SelectedIndexChanged += (s, e) =>
                {
                    if (!m_TrackKindLock)
                        ChangeTrackKind(m_TrackKind.SelectedIndex);
                };
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

                // splitter: drag to change the width of the panel
                int startX = 0;
                int startWidth = 0;
                bool dragging = false;
                m_TrackSplit.MouseDown += (s, e) =>
                {
                    if (e.Button != MouseButtons.Left)
                        return;
                    dragging = true;
                    startX = Cursor.Position.X;
                    startWidth = m_TrackPanel.Width;
                };
                m_TrackSplit.MouseMove += (s, e) =>
                {
                    if (!dragging)
                        return;
                    ResizeTrackPanel(startWidth - (Cursor.Position.X - startX));
                };
                m_TrackSplit.MouseUp += (s, e) =>
                {
                    if (!dragging)
                        return;
                    dragging = false;
                    Ares.CommonGUI.Preferences.Set("TracksWidth", m_TrackWidth.ToString());
                };

                m_TrackPlayTimer = new Timer();
                m_TrackPlayTimer.Interval = 600;
                m_TrackPlayTimer.Tick += (s, e) =>
                {
                    m_TrackPlayTimer.Stop();
                    m_PlayingControl.SelectMusicElement(m_TrackPlayId);
                };

                // menu entry (Extras) with the shortcut
                ToolStripMenuItem item = new ToolStripMenuItem(T("Lista de músicas\tF9", "Track list\tF9"));
                item.Click += (s, e) => ToggleTrackPanel();
                extrasToolStripMenuItem.DropDownItems.Insert(0, item);

                ToolStripMenuItem leftItem = new ToolStripMenuItem(T("Painel esquerdo\tF8", "Left panel\tF8"));
                leftItem.Click += (s, e) => SetLeftOpen(!m_LeftOpen);
                extrasToolStripMenuItem.DropDownItems.Insert(1, leftItem);

                ShowEmptyTrackList();
                Shown += (s, e) =>
                {
                    bool open = Ares.CommonGUI.Preferences.GetBool("TracksOpen", false);
                    SetTrackState(open ? 2 : 1);
                    LayoutMainColumns();
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

        private void ToggleTrackPanel()
        {
            SetTrackState(m_TrackState == 2 ? 1 : 2);
        }

        private void LayoutTrackPanel()
        {
            if (m_TrackPanel == null)
                return;
            int h = Math.Max(26, m_TrackSearch.PreferredHeight + 2);
            int w = m_TrackBody.ClientSize.Width;
            int bw = h;
            m_TrackCollapse.SetBounds(w - bw, 0, bw, h);
            m_TrackDown.SetBounds(w - 2 * bw - 2, 0, bw, h);
            m_TrackUp.SetBounds(w - 3 * bw - 4, 0, bw, h);
            m_TrackTitle.SetBounds(0, 0, Math.Max(20, w - 3 * bw - 8), h);
            int y = h + 4;
            m_TrackKind.SetBounds(0, y, w, m_TrackKind.Height);
            y += m_TrackKind.Height + 4;
            m_TrackSearch.SetBounds(0, y, w, m_TrackSearch.Height);
            y += m_TrackSearch.Height + 4;
            m_TrackList.SetBounds(0, y, w, Math.Max(20, m_TrackBody.ClientSize.Height - y));
        }

        private void UpdateTrackVisibility()
        {
            m_TrackPanel.Visible = m_TrackState == 2;
            m_TrackSplit.Visible = m_TrackState == 2;
            m_TrackStrip.Visible = m_TrackState == 1;
        }

        // opens / collapses the track panel (state 2 / 1); everything is positioned by LayoutMainColumns
        private void SetTrackState(int state)
        {
            if (m_TrackPanel == null || state == m_TrackState)
                return;
            m_TrackState = state;
            Ares.CommonGUI.Preferences.SetBool("TracksOpen", state == 2);
            if (state == 2 && WindowState == FormWindowState.Normal)
                GrowForTrackPanel();
            UpdateTrackVisibility();
            LayoutMainColumns();
            if (state == 2)
            {
                m_TrackPanel.BringToFront();
                SyncTrackSelection();
            }
        }

        private const int MainRightMargin = 12;
        private const int ElementsMinWidth = 300;
        private const int LeftStripWidth = 30;

        // makes the window wider (as far as the screen allows) so the elements box keeps its size
        private void GrowForTrackPanel()
        {
            int have = (ClientSize.Width - MainRightMargin - groupBox5.Left) - 395;
            int deficit = m_TrackWidth + TrackGap - have;
            if (deficit <= 0)
                return;
            Rectangle area = Screen.FromControl(this).WorkingArea;
            int g = Math.Max(0, Math.Min(deficit, area.Right - Right));
            if (g > 0)
                Width += g;
            int rest = deficit - g;
            if (rest > 0 && Left - area.Left >= rest)
            {
                Left -= rest;
                Width += rest;
            }
        }

        private bool m_LayoutingColumns;

        // one place decides where the left column, modes, elements, track panel and the two strips go
        private void LayoutMainColumns()
        {
            if (m_TrackPanel == null || m_LayoutingColumns)
                return;
            int clientWidth = ClientSize.Width;
            if (clientWidth < 300 || WindowState == FormWindowState.Minimized)
                return;
            m_LayoutingColumns = true;
            try
            {
                // left column (volume, state, network) or its strip
                bool leftOpen = m_LeftOpen;
                groupBox1.Visible = leftOpen;
                groupBox2.Visible = leftOpen;
                groupBox3.Visible = leftOpen;
                m_LeftCollapse.Visible = leftOpen;
                m_LeftStrip.Visible = !leftOpen;
                int modesLeft = leftOpen ? m_ModesLeftOriginal : 12 + LeftStripWidth + TrackGap;
                groupBox4.Left = modesLeft;
                groupBox5.Left = modesLeft + groupBox4.Width + TrackGap;
                m_LeftStrip.SetBounds(12, groupBox2.Top, LeftStripWidth, Math.Min(90, groupBox4.Height));
                m_LeftCollapse.SetBounds(groupBox2.Right - 30, groupBox2.Top + 1, 26, 20);
                m_LeftCollapse.BringToFront();

                // elements box, track panel / strip on the right
                int right = clientWidth - MainRightMargin;
                int top = groupBox5.Top;
                int height = groupBox5.Height;
                int panelWidth = 0;
                if (m_TrackState == 2)
                    panelWidth = Math.Max(160, Math.Min(m_TrackWidth, right - groupBox5.Left - ElementsMinWidth - TrackGap));
                else if (m_TrackState == 1)
                    panelWidth = TrackStripWidth;
                int reserved = panelWidth > 0 ? panelWidth + TrackGap : 0;
                groupBox5.Width = Math.Max(100, right - reserved - groupBox5.Left);
                int left = right - panelWidth;
                m_TrackPanel.SetBounds(left, top, panelWidth, height);
                m_TrackSplit.SetBounds(left - TrackGap, top, TrackGap, height);
                m_TrackStrip.SetBounds(left, top, TrackStripWidth, Math.Min(height, 90));
                if (m_TrackState == 2)
                    LayoutTrackPanel();
            }
            finally
            {
                m_LayoutingColumns = false;
            }
        }

        private void SetLeftOpen(bool open)
        {
            if (m_LeftOpen == open)
                return;
            m_LeftOpen = open;
            Ares.CommonGUI.Preferences.SetBool("LeftOpen", open);
            LayoutMainColumns();
        }

        private void ResizeTrackPanel(int newWidth)
        {
            if (m_TrackState != 2)
                return;
            int max = ClientSize.Width - MainRightMargin - groupBox5.Left - ElementsMinWidth - TrackGap;
            newWidth = Math.Max(200, Math.Min(newWidth, max));
            if (newWidth == m_TrackWidth)
                return;
            m_TrackWidth = newWidth;
            LayoutMainColumns();
        }

        // ---- lookups: any mode of the project, not only the one on screen ----

        private IMode FindModeByTitle(string title)
        {
            if (m_Project == null || title == null)
                return null;
            foreach (IMode m in m_Project.GetModes())
            {
                if (m.Title == title)
                    return m;
            }
            return null;
        }

        private IModeElement FindTrackModeElement(int id)
        {
            IMode mode;
            return FindTrackModeElement(id, out mode);
        }

        private IModeElement FindTrackModeElement(int id, out IMode foundMode)
        {
            foundMode = null;
            if (m_Project == null || id < 0)
                return null;
            foreach (IMode m in m_Project.GetModes())
            {
                foreach (IModeElement me in m.GetElements())
                {
                    if (me.Id == id)
                    {
                        foundMode = m;
                        return me;
                    }
                }
            }
            return null;
        }

        private IGeneralElementContainer TrackContainer()
        {
            IModeElement me = FindTrackModeElement(m_TrackElementId);
            return me != null ? me.StartElement as IGeneralElementContainer : null;
        }

        private int TrackKindOf(IElement start)
        {
            if (start is ISequentialBackgroundMusicList)
                return 1;
            if (start is IRandomBackgroundMusicList)
                return 0;
            if (start is IElementContainer<IChoiceElement>)
                return 2;
            return -1;
        }

        // whether a button is the one whose playlist is playing now. For sequential lists the player reports the
        // id of the inner container, so the id alone is not enough: the playing file must belong to the button.
        private bool TrackElementIsPlaying(IModeElement me)
        {
            if (me == null || me.StartElement == null || m_PlayingControl == null)
                return false;
            int listId = m_PlayingControl.CurrentMusicList;
            if (listId != -1 && me.StartElement.Id == listId)
                return true;
            int musicId = m_PlayingControl.CurrentMusicElement;
            if (musicId == -1)
                return false;
            IGeneralElementContainer container = me.StartElement as IGeneralElementContainer;
            if (container == null)
                return false;
            foreach (IFileElement f in container.GetFileElements())
            {
                if (f.Id == musicId)
                    return true;
            }
            return false;
        }

        private void ShowEmptyTrackList()
        {
            m_TrackElementId = -1;
            m_TrackRows.Clear();
            m_TrackShown.Clear();
            m_TrackList.Items.Clear();
            m_TrackTitle.Text = T("Botão direito num botão > Ver músicas", "Right-click a button > View tracks");
            m_TrackKindLock = true;
            m_TrackKind.SelectedIndex = -1;
            m_TrackKindLock = false;
            m_TrackKind.Enabled = false;
            UpdateTrackButtons();
        }

        /// <summary>Shows the track list of a button of the current mode; opens the panel when it is collapsed.</summary>
        private void ShowTrackPanel(int modeElementId)
        {
            if (m_TrackPanel == null)
                return;
            m_TrackPinned = true; // the user chose this list: stay on it until another playlist starts
            m_TrackLastListId = m_PlayingControl != null ? m_PlayingControl.CurrentMusicList : int.MinValue;
            LoadTrackList(modeElementId);
            if (m_TrackState != 2)
                SetTrackState(2);
            SyncTrackSelection();
        }

        private void LoadTrackList(int modeElementId)
        {
            IMode mode;
            IModeElement me = FindTrackModeElement(modeElementId, out mode);
            if (me == null || mode == null)
                return;
            bool changed = modeElementId != m_TrackElementId;
            m_TrackElementId = modeElementId;
            m_TrackModeTitle = mode.Title;
            m_TrackElementTitle = me.Title;
            if (changed && m_TrackSearch.Text.Length > 0)
                m_TrackSearch.Text = "";
            BuildTrackRows(me);
            FillTrackList();
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
            if (m_TrackElementId < 0)
            {
                ShowEmptyTrackList();
                return;
            }
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
            IModeElement me = FindTrackModeElement(m_TrackElementId);
            int kind = me != null ? TrackKindOf(me.StartElement) : -1;
            m_TrackKindLock = true;
            m_TrackKind.SelectedIndex = kind;
            m_TrackKindLock = false;
            m_TrackKind.Enabled = kind >= 0;
            m_TrackTitle.Text = m_TrackElementTitle + "  ·  " + m_TrackRows.Count + " " + T("arquivos", "files");
            UpdateTrackButtons();
        }

        private void UpdateTrackButtons()
        {
            bool seq = TrackContainer() is ISequentialContainer;
            int idx = m_TrackList.SelectedIndex;
            bool canMove = idx >= 0 && idx < m_TrackShown.Count && m_TrackShown[idx].Index >= 0 && m_TrackSearch.Text.Trim().Length == 0;
            m_TrackUp.Enabled = canMove && m_TrackShown[idx].Index > 0;
            m_TrackDown.Enabled = canMove && m_TrackShown[idx].Index < m_TrackRows.Count - 1;
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
            bool listActive = TrackElementIsPlaying(me);
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

        // follows the playlist which is playing (unless the user picked another one) and
        // selects the file which is playing now; called when the music changes
        private void SyncTrackSelection()
        {
            if (m_TrackPanel == null || m_PlayingControl == null || m_Project == null)
                return;
            int listId = m_PlayingControl.CurrentMusicList;
            if (listId != m_TrackLastListId)
            {
                // another playlist started: show it
                m_TrackLastListId = listId;
                m_TrackPinned = false;
            }
            IModeElement shown = FindTrackModeElement(m_TrackElementId);
            bool shownPlays = TrackElementIsPlaying(shown);
            if (!m_TrackPinned && !shownPlays && m_PlayingControl.CurrentMusicElement != -1)
            {
                foreach (IMode m in m_Project.GetModes())
                {
                    foreach (IModeElement me in m.GetElements())
                    {
                        if (TrackElementIsPlaying(me))
                        {
                            LoadTrackList(me.Id);
                            shown = me;
                            shownPlays = true;
                            break;
                        }
                    }
                    if (shownPlays)
                        break;
                }
            }
            if (!shownPlays)
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
            if (idx < 0 || idx >= m_TrackShown.Count)
                return;
            ISequentialContainer seq = TrackContainer() as ISequentialContainer;
            if (seq == null)
            {
                // random lists and effect choices have no fixed order: offer a sequential list instead
                if (m_TrackKind.Enabled && m_TrackKind.SelectedIndex != 1)
                {
                    MessageBox.Show(this,
                        T("Esta lista não toca em ordem fixa, por isso a ordem não importa. Para reorganizar, mude o tipo para \"Música — em sequência\" (você será perguntado).",
                          "This list has no fixed order. To reorder tracks, change the type to a sequential list (you will be asked)."),
                        StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    m_TrackKind.DroppedDown = true;
                }
                return;
            }
            int from = m_TrackShown[idx].Index;
            int to = from + delta;
            if (from < 0 || to < 0 || to >= m_TrackRows.Count)
                return;
            string modeTitle = m_TrackModeTitle;
            string elementTitle = m_TrackElementTitle;
            ApplyProjectEdit(() => seq.MoveElements(from, from, delta), modeTitle);
            ReloadTrackList(modeTitle, elementTitle, to);
        }

        // after the project was reloaded: show the list of the same button again
        private void ReloadTrackList(string modeTitle, string elementTitle, int selectIndex)
        {
            IMode mode = FindModeByTitle(modeTitle);
            if (mode == null)
                return;
            foreach (IModeElement me in mode.GetElements())
            {
                if (me.Title == elementTitle)
                {
                    LoadTrackList(me.Id);
                    if (selectIndex >= 0 && selectIndex < m_TrackShown.Count)
                        m_TrackList.SelectedIndex = selectIndex;
                    break;
                }
            }
        }

        // the user picked another type in the list: rebuild the button's playlist with the same files
        private void ChangeTrackKind(int kind)
        {
            IMode mode;
            IModeElement old = FindTrackModeElement(m_TrackElementId, out mode);
            if (mode == null || old == null || kind < 0 || kind == TrackKindOf(old.StartElement))
                return;
            if (MessageBox.Show(this,
                    T("Mudar o tipo de \"" + old.Title + "\"?\nA lista é recriada com os mesmos arquivos (ajustes de efeitos por arquivo são perdidos) e a reprodução é interrompida.",
                      "Change the type of \"" + old.Title + "\"?\nThe list is rebuilt with the same files (per-file effect settings are lost) and playback is stopped."),
                    StringResources.Ares, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                m_TrackKindLock = true;
                m_TrackKind.SelectedIndex = TrackKindOf(old.StartElement);
                m_TrackKindLock = false;
                return;
            }
            List<string> paths = new List<string>();
            foreach (TrackRow row in m_TrackRows)
                paths.Add(row.File.FilePath);
            string title = old.Title;
            string modeTitle = mode.Title;
            int modeIndex = new List<IMode>(m_Project.GetModes()).IndexOf(mode);
            int index = mode.GetElements().IndexOf(old);
            bool visible = old.IsVisibleInPlayer;
            int keyCode = old.Trigger is IKeyTrigger ? ((IKeyTrigger)old.Trigger).KeyCode : 0;
            ApplyProjectEdit(() =>
            {
                IMode m = m_Project.GetModes()[modeIndex];
                IModeElement current = m.GetElements()[index];
                SoundFileType type = kind == 2 ? SoundFileType.SoundEffect : SoundFileType.Music;
                IElement container;
                if (kind == 0)
                {
                    var list = DataModule.ElementFactory.CreateRandomBackgroundMusicList(title);
                    foreach (string p in paths)
                        list.AddElement(DataModule.ElementFactory.CreateFileElement(p, type));
                    container = list;
                }
                else if (kind == 1)
                {
                    var list = DataModule.ElementFactory.CreateSequentialBackgroundMusicList(title);
                    foreach (string p in paths)
                        list.AddElement(DataModule.ElementFactory.CreateFileElement(p, type));
                    container = list;
                }
                else
                {
                    var choice = DataModule.ElementFactory.CreateChoiceContainer(title);
                    foreach (string p in paths)
                        choice.AddElement(DataModule.ElementFactory.CreateFileElement(p, type));
                    container = choice;
                }
                IModeElement created = DataModule.ElementFactory.CreateModeElement(title, container);
                created.IsVisibleInPlayer = visible;
                if (keyCode != 0)
                {
                    IKeyTrigger trigger = DataModule.ElementFactory.CreateKeyTrigger();
                    trigger.KeyCode = keyCode;
                    created.Trigger = trigger;
                }
                m.InsertElement(index, created);
                m.RemoveElement(current);
                DataModule.ElementRepository.DeleteElement(current.Id);
            }, modeTitle);
            ReloadTrackList(modeTitle, title, -1);
        }

        // called after the buttons of a mode were rebuilt (the project may have been reloaded)
        private void AfterElementsForTrackPanel()
        {
            if (m_TrackPanel == null || m_TrackElementId < 0)
                return;
            IMode mode = FindModeByTitle(m_TrackModeTitle);
            if (mode != null)
            {
                foreach (IModeElement me in mode.GetElements())
                {
                    if (me.Title == m_TrackElementTitle)
                    {
                        m_TrackElementId = me.Id;
                        BuildTrackRows(me);
                        FillTrackList();
                        return;
                    }
                }
            }
            ShowEmptyTrackList();
        }
    }
}
