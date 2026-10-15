using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Ares.Data;

namespace Ares.Player
{
    // Panel with the list of files of a button's playlist: play by double click, search, reorder,
    // change the playlist type. The panel sits below the element buttons; it can be collapsed to a thin strip at the
    // bottom (button, menu "Extras" or F9) and resized with the splitter above it.
    public partial class Player
    {
        private class TrackRow
        {
            public IFileElement File;
            public int Index; // position in the container, -1 when not a top-level element
            public int Number; // position in the list, starting with 1
            public string Artist;
            public string Album;
            public double Seconds;
            public bool Loaded;
            public int Plays;
        }

        private const int TrackGap = 6;
        private const int TrackStripWidth = 30;

        private GroupBox m_TrackPanel;
        private Panel m_TrackBody;
        private bool m_TrackPinned = false;
        private int m_TrackLastListId = int.MinValue;
        private Panel m_TrackSplit;
        private EdgeHandle m_TrackStrip;
        private Label m_TrackTitle;
        private Button m_TrackCollapse;
        private Button m_TrackUp;
        private Button m_TrackDown;
        private ComboBox m_TrackKind;
        private bool m_TrackKindLock;
        private TextBox m_TrackSearch;
        private TrackListView m_TrackList;
        private int m_TrackState = 0; // 0 = not placed yet, 1 = collapsed, 2 = open
        private int m_TrackHeight = 230;
        private int m_TrackElementId = -1;
        private string m_TrackModeTitle;
        private string m_TrackElementTitle;
        private bool m_LeftOpen = true;
        private int m_ModesLeftOriginal;
        private int m_ElementsTopOriginal;
        private EdgeHandle m_LeftStrip;
        private List<TrackRow> m_TrackRows = new List<TrackRow>();
        private List<TrackRow> m_TrackShown = new List<TrackRow>();
        private Timer m_TrackPlayTimer;
        private int m_TrackPlayId;

        /// <summary>Opens the Explorer with the file selected (or the folder, if the file is gone).</summary>
        private void ShowInFolder(string fullPath)
        {
            try
            {
                if (!string.IsNullOrEmpty(fullPath) && System.IO.File.Exists(fullPath))
                {
                    System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + System.IO.Path.GetFullPath(fullPath) + "\"");
                    return;
                }
                string dir = string.IsNullOrEmpty(fullPath) ? null : System.IO.Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(dir) && System.IO.Directory.Exists(dir))
                {
                    System.Diagnostics.Process.Start("explorer.exe", "\"" + dir + "\"");
                    return;
                }
                ShowToast(new Ares.Players.Message(Ares.Players.MessageType.Warning,
                    T("Arquivo não encontrado: ", "File not found: ") + fullPath));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowSelectedTrackInFolder()
        {
            int idx = m_TrackList.SelectedIndex;
            if (idx < 0 || idx >= m_TrackShown.Count)
                return;
            ShowInFolder(FullPathOf(m_TrackShown[idx].File));
        }

        private void InitTrackPanel()
        {
            try
            {
                Control host = this;
                int saved;
                if (int.TryParse(Ares.CommonGUI.Preferences.Get("TracksHeight", "230"), out saved))
                    m_TrackHeight = Math.Max(110, Math.Min(900, saved));

                m_TrackPanel = new GroupBox();
                m_TrackPanel.Name = "trackPanel";
                m_TrackPanel.Text = T("Lista de músicas", "Track list");
                m_TrackPanel.Visible = false;
                m_TrackBody = new Panel();
                m_TrackBody.Dock = DockStyle.Fill;
                m_TrackPanel.Controls.Add(m_TrackBody);
                m_TrackSplit = new Panel();
                m_TrackSplit.Name = "trackSplit";
                m_TrackSplit.Cursor = Cursors.HSplit;
                m_TrackSplit.Visible = false;
                m_TrackStrip = new EdgeHandle();
                m_TrackStrip.Name = "trackStrip";
                m_TrackStrip.Visible = false;
                new ToolTip().SetToolTip(m_TrackStrip, T("Mostrar / esconder a lista de músicas" + KeyHint("tracks"), "Show / hide the track list" + KeyHint("tracks")));

                m_TrackTitle = new Label();
                m_TrackTitle.AutoEllipsis = true;
                m_TrackTitle.TextAlign = ContentAlignment.MiddleLeft;
                m_TrackCollapse = MakeSmallButton("▶", T("Recolher o painel para a lateral" + KeyHint("tracks"), "Collapse the panel to the side" + KeyHint("tracks")));
                m_TrackUp = MakeSmallButton("▲", T("Mover a música para cima (só em lista em sequência)", "Move the track up (sequential lists only)"));
                m_TrackDown = MakeSmallButton("▼", T("Mover a música para baixo (só em lista em sequência)", "Move the track down (sequential lists only)"));
                m_TrackKind = new ComboBox();
                m_TrackKind.DropDownStyle = ComboBoxStyle.DropDownList;
                m_TrackKind.Items.Add(T("Música — aleatória", "Music — random"));
                m_TrackKind.Items.Add(T("Música — em sequência", "Music — sequential"));
                m_TrackKind.Items.Add(T("Efeito sonoro — um aleatório", "Sound effect — one at random"));
                new ToolTip().SetToolTip(m_TrackKind, T("Tipo da playlist: muda como ela toca os arquivos", "Playlist type: changes how it plays its files"));
                m_TrackSearch = new TextBox();
                m_TrackList = new TrackListView();
                InitTrackColumns();

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
                m_ElementsTopOriginal = groupBox5.Top;
                groupBox5.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                m_TrackPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                m_TrackSplit.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                m_TrackStrip.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                m_TrackStrip.Horizontal = true;
                m_LeftOpen = Ares.CommonGUI.Preferences.GetBool("LeftOpenV2", false);
                m_LeftStrip = new EdgeHandle();
                m_LeftStrip.Name = "leftStrip";
                m_LeftStrip.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
                new ToolTip().SetToolTip(m_LeftStrip, T("Mostrar / esconder o painel esquerdo" + KeyHint("left"), "Show / hide the left panel" + KeyHint("left")));
                m_LeftStrip.Click += (s2, e2) => SetLeftOpen(!m_LeftOpen);
                host.Controls.Add(m_LeftStrip);
                SizeChanged += (s, e) => LayoutMainColumns();

                m_TrackCollapse.Visible = false;
                m_TrackStrip.Click += (s, e) => ToggleTrackPanel();
                m_TrackUp.Click += (s, e) => MoveTrack(-1);
                m_TrackDown.Click += (s, e) => MoveTrack(1);
                m_TrackKind.SelectedIndexChanged += (s, e) =>
                {
                    if (!m_TrackKindLock)
                        ChangeTrackKind(m_TrackKind.SelectedIndex);
                };
                m_TrackSearch.TextChanged += (s, e) => FillTrackList(false);
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

                // right click: show the file in the Explorer
                ContextMenuStrip trackMenu = new ContextMenuStrip();
                ToolStripMenuItem playItem = new ToolStripMenuItem(T("Tocar", "Play"));
                playItem.Click += (s, e) => PlaySelectedTrack();
                ToolStripMenuItem showItem = new ToolStripMenuItem(T("Mostrar na pasta", "Show in folder"));
                showItem.Click += (s, e) => ShowSelectedTrackInFolder();
                trackMenu.Items.Add(playItem);
                trackMenu.Items.Add(showItem);
                ToolStripMenuItem editItem = new ToolStripMenuItem(T("Editar informações...", "Edit information..."));
                editItem.Click += (s, e) => EditSelectedTrackInfo();
                m_TrackSaveOrderItem = new ToolStripMenuItem(T("Gravar esta ordem na playlist", "Save this order in the playlist"));
                m_TrackSaveOrderItem.Click += (s, e) => SaveTrackOrder();
                trackMenu.Items.Add(editItem);
                trackMenu.Items.Add(m_TrackSaveOrderItem);
                m_TrackMostPlayedItem = new ToolStripMenuItem(T("Ordem: mais tocadas primeiro (gravar)", "Order: most played first (save)"));
                m_TrackMostPlayedItem.Click += (s, e) => OrderByPlays(false);
                m_TrackLeastPlayedItem = new ToolStripMenuItem(T("Ordem: menos tocadas primeiro (gravar)", "Order: least played first (save)"));
                m_TrackLeastPlayedItem.Click += (s, e) => OrderByPlays(true);
                trackMenu.Items.Add(m_TrackMostPlayedItem);
                trackMenu.Items.Add(m_TrackLeastPlayedItem);
                m_TrackList.ContextMenuStrip = trackMenu;
                m_TrackList.MouseDown += (s, e) =>
                {
                    if (e.Button == MouseButtons.Right)
                    {
                        int idx = m_TrackList.IndexFromPoint(e.Location);
                        if (idx >= 0)
                            m_TrackList.SelectedIndex = idx;
                    }
                };
                trackMenu.Opening += (s, e) =>
                {
                    if (m_TrackList.SelectedIndex < 0 || m_TrackList.SelectedIndex >= m_TrackShown.Count)
                        e.Cancel = true;
                    m_TrackMostPlayedItem.Visible = m_TrackLeastPlayedItem.Visible = TrackContainer() is ISequentialContainer && m_TrackSearch.Text.Trim().Length == 0;
                    m_TrackSaveOrderItem.Visible = m_TrackSortCol >= 0 && TrackContainer() is ISequentialContainer && m_TrackSearch.Text.Trim().Length == 0;
                };

                // splitter: drag to change the height of the panel
                int startY = 0;
                int startHeight = 0;
                bool dragging = false;
                m_TrackSplit.MouseDown += (s, e) =>
                {
                    if (e.Button != MouseButtons.Left)
                        return;
                    dragging = true;
                    startY = Cursor.Position.Y;
                    startHeight = m_TrackPanel.Height;
                };
                m_TrackSplit.MouseMove += (s, e) =>
                {
                    if (!dragging)
                        return;
                    ResizeTrackPanel(startHeight - (Cursor.Position.Y - startY));
                };
                m_TrackSplit.MouseUp += (s, e) =>
                {
                    if (!dragging)
                        return;
                    dragging = false;
                    Ares.CommonGUI.Preferences.Set("TracksHeight", m_TrackHeight.ToString());
                };

                m_TrackPlayTimer = new Timer();
                m_TrackPlayTimer.Interval = 600;
                m_TrackPlayTimer.Tick += (s, e) =>
                {
                    m_TrackPlayTimer.Stop();
                    m_PlayingControl.SelectMusicElement(m_TrackPlayId);
                };

                // menu entry (Extras) with the shortcut
                ToolStripMenuItem item = new ToolStripMenuItem(T("Lista de músicas", "Track list"));
                item.Tag = "tracks";
                item.Click += (s, e) => ToggleTrackPanel();
                extrasToolStripMenuItem.DropDownItems.Insert(0, item);

                ToolStripMenuItem leftItem = new ToolStripMenuItem(T("Painel esquerdo", "Left panel"));
                leftItem.Tag = "left";
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

        private static string KeyHint(string id)
        {
            string text = ShortcutText(id);
            return text.Length == 0 ? "" : " (" + text + ")";
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
            // one header row (title | list type | search | up | down) and the wide list below it
            int h = Math.Max(26, m_TrackSearch.PreferredHeight + 2);
            int w = m_TrackBody.ClientSize.Width;
            int bw = h;
            m_TrackCollapse.SetBounds(0, 0, 0, 0);
            m_TrackDown.SetBounds(w - bw, 0, bw, h);
            m_TrackUp.SetBounds(w - 2 * bw - 2, 0, bw, h);
            int rest = w - 2 * bw - 2 - 8; // room for title, type and search
            int kindW = Math.Max(120, Math.Min(230, rest / 3));
            int titleW = Math.Max(60, Math.Min(170, rest / 4));
            int searchW = Math.Max(60, rest - kindW - titleW - 8);
            int x = 0;
            m_TrackTitle.SetBounds(x, 0, titleW, h);
            x += titleW + 4;
            m_TrackKind.SetBounds(x, (h - m_TrackKind.Height) / 2, kindW, m_TrackKind.Height);
            x += kindW + 4;
            m_TrackSearch.SetBounds(x, (h - m_TrackSearch.Height) / 2, searchW, m_TrackSearch.Height);
            int y = h + 4;
            m_TrackList.SetBounds(0, y, w, Math.Max(20, m_TrackBody.ClientSize.Height - y));
        }

        private void UpdateTrackVisibility()
        {
            m_TrackPanel.Visible = m_TrackState == 2;
            m_TrackSplit.Visible = m_TrackState == 2;
            m_TrackStrip.Visible = m_TrackState != 0;
            m_TrackStrip.PointsLeft = m_TrackState == 1;
        }

        // opens / collapses the track panel (state 2 / 1); everything is positioned by LayoutMainColumns
        private void SetTrackState(int state)
        {
            if (m_TrackPanel == null || state == m_TrackState)
                return;
            m_TrackState = state;
            Ares.CommonGUI.Preferences.SetBool("TracksOpen", state == 2);
            UpdateTrackVisibility();
            LayoutMainColumns();
            if (state == 2)
            {
                m_TrackPanel.BringToFront();
                SyncTrackSelection();
            }
        }

        private const int MainRightMargin = 12;
        private const int ElementsMinHeight = 150;
        private const int TrackStripHeight = 17;
        private const int LeftStripThickness = 17;
        private const int LeftStripWidth = 30;

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
                LayoutHeader();

                // vertical limits of everything below the header: from the header down to the status bar
                int margin = groupBox3.Left;
                int headerBottom = m_VolumePanel != null ? m_VolumePanel.Bottom : menuStrip1.Bottom + 6;
                int bodyTop = headerBottom + 8;
                if (m_NowPlaying != null)
                {
                    // the "playing now" strip has a fixed place, so that nothing jumps when a sound starts
                    m_NowPlaying.Visible = true;
                    {
                        m_NowPlaying.SetBounds(margin, headerBottom + 4, Math.Max(100, clientWidth - 2 * margin), NowPlayingStrip.StripHeight);
                        m_NowPlaying.BringToFront();
                        bodyTop = m_NowPlaying.Bottom + 6;
                    }
                }
                int statusHeight = m_StatusStrip != null && m_StatusStrip.Visible ? m_StatusStrip.Height : 0;
                int bodyBottom = ClientSize.Height - statusHeight - 10;
                int bodyHeight = Math.Max(150, bodyBottom - bodyTop);

                // left column (state of the program) or its strip
                bool leftOpen = m_LeftOpen;
                groupBox1.Visible = false; // the network info lives in Extras > Network
                groupBox2.Visible = false; // the volume sliders live in the header
                groupBox3.Visible = leftOpen;
                m_LeftStrip.Visible = true;
                m_LeftStrip.PointsLeft = leftOpen;
                int modesLeft = leftOpen ? m_ModesLeftOriginal : LeftStripThickness;
                if (leftOpen)
                {
                    int lw = m_ModesLeftOriginal - TrackGap - margin;
                    if (lw >= 200)
                        groupBox3.SetBounds(margin, bodyTop, lw, bodyHeight);
                }
                groupBox4.SetBounds(modesLeft, bodyTop, groupBox4.Width, bodyHeight);
                groupBox5.Left = modesLeft + groupBox4.Width + TrackGap;
                m_LeftStrip.SetBounds(0, bodyTop, LeftStripThickness, bodyHeight);

                // the list of folders fills its box down to the bottom
                modesList.SetBounds(modesList.Left, modesList.Top,
                    Math.Max(50, groupBox4.ClientSize.Width - 2 * modesList.Left),
                    Math.Max(50, groupBox4.ClientSize.Height - modesList.Top - modesList.Left));

                // right column: elements box on top, track list below it, handle at the bottom
                int right = clientWidth - MainRightMargin;
                int colLeft = groupBox5.Left;
                int colWidth = Math.Max(100, right - colLeft);
                int top = bodyTop;
                int bottom = bodyBottom - TrackStripHeight - 2;
                int total = Math.Max(100, bottom - top);
                int panelHeight = 0;
                if (m_TrackState == 2)
                    panelHeight = Math.Max(90, Math.Min(m_TrackHeight, total - ElementsMinHeight - TrackGap));
                int elementsHeight = total - (panelHeight > 0 ? panelHeight + TrackGap : 0);
                groupBox5.SetBounds(colLeft, top, colWidth, elementsHeight);
                int panelTop = top + elementsHeight + TrackGap;
                m_TrackPanel.SetBounds(colLeft, panelTop, colWidth, panelHeight);
                m_TrackSplit.SetBounds(colLeft, panelTop - TrackGap, colWidth, TrackGap);
                m_TrackStrip.SetBounds(colLeft, bottom + 2, colWidth, TrackStripHeight);
                if (m_TrackState == 2)
                    LayoutTrackPanel();

                // the buttons of the playlists use the whole width of their box
                int inner = elementsPanel.Left;
                int innerWidth = Math.Max(50, groupBox5.ClientSize.Width - 2 * inner);
                if (m_ListBar != null)
                    m_ListBar.SetBounds(inner, m_ListBar.Top, innerWidth, m_ListBar.Height);
                elementsPanel.SetBounds(inner, elementsPanel.Top, innerWidth,
                    Math.Max(40, groupBox5.ClientSize.Height - elementsPanel.Top - inner));
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
            Ares.CommonGUI.Preferences.SetBool("LeftOpenV2", open);
            LayoutMainColumns();
        }

        private void ResizeTrackPanel(int newHeight)
        {
            if (m_TrackState != 2)
                return;
            int total = groupBox4.Bottom - TrackStripHeight - 2 - groupBox4.Top;
            int max = total - ElementsMinHeight - TrackGap;
            newHeight = Math.Max(90, Math.Min(newHeight, max));
            if (newHeight == m_TrackHeight)
                return;
            m_TrackHeight = newHeight;
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
            m_TrackTitle.Text = T("Botão direito numa playlist > Ver músicas", "Right-click a playlist > View tracks");
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
                row.Number = m_TrackRows.Count + 1;
                row.Plays = PlayCounts.Get(FullPathOf(f));
                ApplyCachedTags(row);
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
            FillTrackList(true);
        }

        private void FillTrackList(bool loadMeta)
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
            List<TrackRow> filtered = new List<TrackRow>();
            for (int i = 0; i < m_TrackRows.Count; i++)
            {
                string title = m_TrackRows[i].File.Title;
                if (q.Length > 0 && (title ?? "").IndexOf(q, StringComparison.CurrentCultureIgnoreCase) < 0
                    && (m_TrackRows[i].Artist ?? "").IndexOf(q, StringComparison.CurrentCultureIgnoreCase) < 0)
                    continue;
                filtered.Add(m_TrackRows[i]);
            }
            m_TrackShown.AddRange(SortedTrackRows(filtered));
            m_TrackList.BeginUpdate();
            m_TrackList.Items.Clear();
            foreach (TrackRow shown in m_TrackShown)
                m_TrackList.Items.Add(MakeTrackItem(shown));
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
            if (loadMeta)
                StartTrackMetaLoading();
        }

        private void UpdateTrackButtons()
        {
            bool seq = TrackContainer() is ISequentialContainer;
            int idx = m_TrackList.SelectedIndex;
            bool canMove = idx >= 0 && idx < m_TrackShown.Count && m_TrackShown[idx].Index >= 0 && m_TrackSearch.Text.Trim().Length == 0 && m_TrackSortCol < 0;
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
