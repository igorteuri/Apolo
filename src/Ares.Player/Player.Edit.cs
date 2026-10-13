using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Ares.Data;

namespace Ares.Player
{
    // Simple "edit mode" inside the Player: add / rename / delete / reorder folders (modes) and playlists (buttons)
    // without opening the full Editor.
    public partial class Player
    {
        private bool m_EditMode = false;
        private ToolStripButton m_EditButton;
        private ToolStripDropDownButton m_AddButton;
        private ContextMenuStrip m_ModeMenu;
        private ContextMenuStrip m_ElementMenu;
        private int m_MenuElementId = -1;
        private ToolStripItem m_ElementDeleteItem;
        private readonly HashSet<int> m_SelectedElements = new HashSet<int>();
        private int m_SelectAnchor = -1;

        private static string T(string pt, string en)
        {
            return IsPt ? pt : en;
        }

        // File menu: save the project (edits are also saved automatically) / save a copy under another name
        private void InitSaveMenu()
        {
            ToolStripMenuItem save = new ToolStripMenuItem(T("Salvar projeto", "Save project"));
            save.ShortcutKeys = System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.S;
            save.Click += (s, e) => SaveProjectNow();
            ToolStripMenuItem saveAs = new ToolStripMenuItem(T("Salvar projeto como...", "Save project as..."));
            saveAs.ShortcutKeys = System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift | System.Windows.Forms.Keys.S;
            saveAs.Click += (s, e) => SaveProjectAsNew();
            int pos = projectToolStripMenuItem.DropDownItems.IndexOf(openToolStripMenuItem) + 1;
            projectToolStripMenuItem.DropDownItems.Insert(pos, save);
            projectToolStripMenuItem.DropDownItems.Insert(pos + 1, saveAs);
        }

        private void SaveProjectNow()
        {
            if (m_Project == null)
                return;
            try
            {
                bool oldWatch = fileSystemWatcher1.EnableRaisingEvents;
                fileSystemWatcher1.EnableRaisingEvents = false;
                try
                {
                    m_Project.Changed = true;
                    DataModule.ProjectManager.SaveProject(m_Project);
                }
                finally
                {
                    fileSystemWatcher1.EnableRaisingEvents = oldWatch;
                }
                ShowToast(new Ares.Players.Message(Ares.Players.MessageType.Info,
                    T("Projeto salvo: ", "Project saved: ") + System.IO.Path.GetFileName(m_Project.FileName)));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, T("Erro ao salvar: ", "Save error: ") + ex.Message, StringResources.Ares,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveProjectAsNew()
        {
            if (m_Project == null)
                return;
            using (SaveFileDialog dlg = new SaveFileDialog())
            {
                dlg.Filter = T("Projetos do Apolo|*.apolo|Todos os arquivos|*.*", "Apolo projects|*.apolo|All files|*.*");
                dlg.DefaultExt = "apolo";
                dlg.FileName = !string.IsNullOrEmpty(m_Project.Title) ? m_Project.Title + ".apolo" : "projeto.apolo";
                if (!string.IsNullOrEmpty(m_Project.FileName))
                    dlg.InitialDirectory = System.IO.Path.GetDirectoryName(m_Project.FileName);
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    m_Project.FileName = dlg.FileName;
                    m_Project.Changed = true;
                    DataModule.ProjectManager.SaveProject(m_Project);
                    OpenProject(dlg.FileName, false);
                    ShowToast(new Ares.Players.Message(Ares.Players.MessageType.Info,
                        T("Projeto salvo como: ", "Project saved as: ") + System.IO.Path.GetFileName(dlg.FileName)));
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, T("Erro ao salvar: ", "Save error: ") + ex.Message, StringResources.Ares,
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void InitEditMode()
        {
            InitSaveMenu();
            InitExportMenu();
            InitMusicToolsMenu();
            InitOnlineMediaMenu();
            InitAppearanceMenu();
            // toolbar: edit toggle + add menu (the add menu only shows in edit mode)
            m_EditButton = new ToolStripButton();
            m_EditButton.Name = "editModeButton";
            m_EditButton.CheckOnClick = true;
            m_EditButton.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
            m_EditButton.Text = T("Modo de Edição", "Edit mode");
            m_EditButton.ToolTipText = T("Modo de edição: adicionar, renomear, excluir e reordenar pastas e playlists",
                "Edit mode: add, rename, delete and reorder folders and playlists");
            m_EditButton.CheckedChanged += (s, e) => SetEditMode(m_EditButton.Checked);

            m_AddButton = new ToolStripDropDownButton();
            m_AddButton.Name = "addButton";
            m_AddButton.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
            m_AddButton.Text = T("Adicionar", "Add");
            m_AddButton.Visible = false;
            var addFolder = new ToolStripMenuItem(T("Pasta de áudio como uma playlist por pasta...", "Audio folder as one playlist per folder..."));
            addFolder.Click += (s, e) => AddFolderDialog();
            var addFiles = new ToolStripMenuItem(T("Arquivos de áudio...", "Audio files..."));
            addFiles.Click += (s, e) => AddFilesDialog();
            var addMode = new ToolStripMenuItem(T("Nova pasta...", "New folder..."));
            addMode.Click += (s, e) => NewEmptyMode(false);
            var addSub = new ToolStripMenuItem(T("Nova subpasta na pasta selecionada...", "New subfolder in the selected folder..."));
            addSub.Click += (s, e) => NewEmptyMode(true);
            var addTree = new ToolStripMenuItem(T("Pasta do computador (cria pastas e playlists)...", "Folder of the computer (creates folders and playlists)..."));
            addTree.Click += (s, e) => ImportFolderAsFolders(false);
            m_AddButton.DropDownItems.AddRange(new ToolStripItem[] { addTree, addFolder, addFiles, new ToolStripSeparator(), addMode, addSub });

            int outIndex = toolStrip1.Items.IndexOf(m_OutputButton);
            if (outIndex < 0)
                outIndex = toolStrip1.Items.Count;
            toolStrip1.Items.Insert(outIndex, new ToolStripSeparator());
            toolStrip1.Items.Insert(outIndex + 1, m_EditButton);
            toolStrip1.Items.Insert(outIndex + 2, m_AddButton);
            Ares.CommonGUI.IconLibrary.Bind(toolStrip1, new Dictionary<ToolStripItem, string>
            {
                { m_EditButton, "pencil" },
                { m_AddButton, "plus" }
            });

            // context menu for modes (list on the left)
            m_ModeMenu = new ContextMenuStrip();
            m_ModeMenu.Items.Add(T("Nova pasta...", "New folder..."), null, (s, e) => NewEmptyMode(false));
            m_ModeMenu.Items.Add(T("Nova subpasta aqui...", "New subfolder here..."), null, (s, e) => NewEmptyMode(true));
            m_ModeMenu.Items.Add(T("Importar pasta do computador aqui dentro...", "Import a folder of the computer in here..."), null, (s, e) => ImportFolderAsFolders(true));
            m_ModeMenu.Items.Add(T("Renomear pasta...", "Rename folder..."), null, (s, e) => RenameCurrentMode());
            m_ModeMenu.Items.Add(T("Cor da pasta...", "Folder color..."), null, (s, e) => ChooseModeColor());
            m_ModeMenu.Items.Add(T("Excluir pasta", "Delete folder"), null, (s, e) => DeleteCurrentMode());
            m_ModeMenu.Items.Add(new ToolStripSeparator());
            m_ModeMenu.Items.Add(T("Mover para cima", "Move up"), null, (s, e) => MoveCurrentMode(-1));
            m_ModeMenu.Items.Add(T("Mover para baixo", "Move down"), null, (s, e) => MoveCurrentMode(1));
            m_ModeMenu.Items.Add(T("Tirar da pasta (subir um nível)", "Move out of the folder (up one level)"), null, (s, e) => OutdentModeNode(SelectedModeNode));
            modesList.MouseUp += (s, e) =>
            {
                if (!m_EditMode || e.Button != MouseButtons.Right)
                    return;
                int index = modesList.IndexFromPoint(e.Location);
                bool inSelection = index >= 0 && modesList.SelectedIndices.Contains(index) && modesList.SelectedIndices.Count > 1;
                if (index >= 0 && !inSelection)
                    modesList.SelectedIndex = index;
                bool real = index >= 0 && SelectedModeIndex >= 0;
                int many = modesList.SelectedIndices.Count;
                for (int i = 1; i < m_ModeMenu.Items.Count; i++)
                    m_ModeMenu.Items[i].Enabled = real && many <= 1;
                m_ModeMenu.Items[0].Enabled = many <= 1;
                m_ModeMenu.Items[4].Text = many > 1 ? T("Excluir " + many + " pastas selecionadas", "Delete " + many + " selected folders") : T("Excluir pasta", "Delete folder");
                m_ModeMenu.Items[4].Enabled = real;
                m_ModeMenu.Show(modesList, e.Location);
            };

            // context menu for buttons (elements)
            m_ElementMenu = new ContextMenuStrip();
            m_ElementMenu.Items.Add(T("Ver lista de músicas", "View track list"), null, (s, e) => ShowTrackPanel(m_MenuElementId));
            m_ElementMenu.Items.Add(new ToolStripSeparator());
            m_ElementMenu.Items.Add(T("Renomear...", "Rename..."), null, (s, e) => RenameElementButton());
            m_ElementDeleteItem = m_ElementMenu.Items.Add(T("Excluir", "Delete"), null, (s, e) => DeleteElementButton());
            m_ElementMenu.Items.Add(new ToolStripSeparator());
            m_ElementMenu.Items.Add(T("Escolher imagem...", "Choose picture..."), null, (s, e) => ChooseButtonPicture());
            m_ElementMenu.Items.Add(T("Sugerir imagens na internet...", "Suggest pictures from the internet..."), null, (s, e) => SuggestButtonImages());
            m_ElementMenu.Items.Add(T("Escolher emoji...", "Choose emoji..."), null, (s, e) => ChooseButtonEmoji());
            m_ElementMenu.Items.Add(T("Emoji automático (pelo nome)", "Automatic emoji (from the name)"), null, (s, e) => AutoIconForButton());
            m_ElementMenu.Items.Add(T("Cor do botão...", "Button color..."), null, (s, e) => ChooseButtonColor());
            m_ElementMenu.Items.Add(T("Escolher ícone...", "Choose icon..."), null, (s, e) => ChooseButtonIcon());
            m_ElementMenu.Items.Add(T("Remover imagem / ícone", "Remove picture / icon"), null, (s, e) => RemoveButtonImage());
            m_ElementMenu.Items.Add(new ToolStripSeparator());
            m_ElementMenu.Items.Add(T("Mover para trás", "Move earlier"), null, (s, e) => MoveElementButton(-1));
            m_ElementMenu.Items.Add(T("Mover para frente", "Move later"), null, (s, e) => MoveElementButton(1));

            // manual drag & drop: reorder buttons, move them to another mode, reorder modes
            elementsPanel.AllowDrop = true;
            elementsPanel.DragOver += ElementsDragOver;
            elementsPanel.DragDrop += ElementsDragDrop;
            elementsPanel.DragLeave += (s, e) => HideDropMarker();
            modesList.AllowDrop = true;
            modesList.DragOver += ModesDragOver;
            modesList.DragDrop += ModesDragDrop;
            Point modeDown = Point.Empty;
            bool modeTracking = false;
            modesList.MouseDown += (s, e) =>
            {
                modeTracking = m_EditMode && e.Button == MouseButtons.Left && modesList.IndexFromPoint(e.Location) >= 0;
                modeDown = e.Location;
            };
            modesList.MouseUp += (s, e) => modeTracking = false;
            modesList.MouseMove += (s, e) =>
            {
                if (!modeTracking || e.Button != MouseButtons.Left)
                    return;
                Size ds = SystemInformation.DragSize;
                if (Math.Abs(e.X - modeDown.X) < ds.Width && Math.Abs(e.Y - modeDown.Y) < ds.Height)
                    return;
                modeTracking = false;
                int index = modesList.IndexFromPoint(modeDown);
                if (index >= 0)
                    {
                    if (index < m_ModeRows.Count)
                        modesList.DoDragDrop(new DragInfo { IsMode = true, ModeTitle = m_ModeRows[index].Title }, DragDropEffects.Move);
                }
            };
            modesList.DoubleClick += (s, e) =>
            {
                if (m_EditMode)
                    RenameCurrentMode();
            };
            modesList.KeyDown += (s, e) =>
            {
                if (m_EditMode && e.KeyCode == System.Windows.Forms.Keys.F2)
                    RenameCurrentMode();
                else if (m_EditMode && e.KeyCode == System.Windows.Forms.Keys.Delete)
                    DeleteCurrentMode();
            };
        }

        private class DragInfo
        {
            public bool IsMode;
            public string ModeTitle;
            public int ElementId;
        }

        private Panel m_DropMarker;

        private void HideDropMarker()
        {
            if (m_DropMarker != null)
                m_DropMarker.Visible = false;
        }

        // finds the button before which a dragged button would be inserted (null = at the end)
        private Control FindInsertBefore(Point panelPoint, out Rectangle markerRect)
        {
            markerRect = Rectangle.Empty;
            foreach (Control c in elementsPanel.Controls)
            {
                if (!(c is CheckBox) || !c.Visible)
                    continue;
                Rectangle r = new Rectangle(c.Left - c.Margin.Left, c.Top - c.Margin.Top,
                    c.Width + c.Margin.Horizontal, c.Height + c.Margin.Vertical);
                if (r.Contains(panelPoint))
                {
                    bool before = m_ListMode ? panelPoint.Y < c.Top + c.Height / 2 : panelPoint.X < c.Left + c.Width / 2;
                    if (before)
                    {
                        markerRect = m_ListMode ? new Rectangle(c.Left, c.Top - 3, c.Width, 3) : new Rectangle(c.Left - 3, c.Top, 3, c.Height);
                        return c;
                    }
                    int idx = elementsPanel.Controls.IndexOf(c);
                    Control next = null;
                    for (int i = idx + 1; i < elementsPanel.Controls.Count; i++)
                    {
                        if (elementsPanel.Controls[i] is CheckBox && elementsPanel.Controls[i].Visible)
                        {
                            next = elementsPanel.Controls[i];
                            break;
                        }
                    }
                    markerRect = m_ListMode ? new Rectangle(c.Left, c.Bottom, c.Width, 3) : new Rectangle(c.Right, c.Top, 3, c.Height);
                    return next;
                }
            }
            // not on a button: at the end, marker after the last one
            for (int i = elementsPanel.Controls.Count - 1; i >= 0; i--)
            {
                Control c = elementsPanel.Controls[i];
                if (c is CheckBox && c.Visible)
                {
                    markerRect = m_ListMode ? new Rectangle(c.Left, c.Bottom, c.Width, 3) : new Rectangle(c.Right, c.Top, 3, c.Height);
                    break;
                }
            }
            return null;
        }

        private void ElementsDragOver(object sender, DragEventArgs e)
        {
            DragInfo info = e.Data.GetData(typeof(DragInfo)) as DragInfo;
            if (!m_EditMode || info == null || info.IsMode)
                return;
            e.Effect = DragDropEffects.Move;
            Rectangle marker;
            FindInsertBefore(elementsPanel.PointToClient(new Point(e.X, e.Y)), out marker);
            if (marker.IsEmpty || elementsPanel.Parent == null)
            {
                HideDropMarker();
                return;
            }
            if (m_DropMarker == null)
            {
                m_DropMarker = new Panel();
                m_DropMarker.Enabled = false;
                elementsPanel.Parent.Controls.Add(m_DropMarker);
            }
            Ares.CommonGUI.ThemePalette p = Ares.CommonGUI.ThemeManager.Current;
            m_DropMarker.BackColor = (p != null && !p.IsClassic) ? p.Accent : SystemColors.Highlight;
            Point topLeft = elementsPanel.Parent.PointToClient(elementsPanel.PointToScreen(marker.Location));
            m_DropMarker.Bounds = new Rectangle(topLeft, marker.Size);
            m_DropMarker.Visible = true;
            m_DropMarker.BringToFront();
        }

        private void ElementsDragDrop(object sender, DragEventArgs e)
        {
            HideDropMarker();
            DragInfo info = e.Data.GetData(typeof(DragInfo)) as DragInfo;
            if (!m_EditMode || info == null || info.IsMode)
                return;
            Rectangle marker;
            Control before = FindInsertBefore(elementsPanel.PointToClient(new Point(e.X, e.Y)), out marker);
            int beforeId = -1;
            if (before != null && before.Tag is int)
                beforeId = (int)before.Tag;
            int movedId = info.ElementId;
            if (beforeId == movedId)
                return;
            IMode mode = CurrentMode();
            if (mode == null)
                return;
            int modeIndex = SelectedModeIndex;
            string modeTitle = mode.Title;
            BeginInvoke(new MethodInvoker(() => ApplyProjectEdit(() =>
            {
                IMode m = m_Project.GetModes()[modeIndex];
                IModeElement moved = m.GetElements().FirstOrDefault(x => x.Id == movedId);
                if (moved == null)
                    return;
                m.RemoveElement(moved);
                int target = beforeId == -1 ? -1 : m.GetElements().ToList().FindIndex(x => x.Id == beforeId);
                if (target < 0)
                    m.AddElement(moved);
                else
                    m.InsertElement(target, moved);
            }, modeTitle)));
        }

        private void ModesDragOver(object sender, DragEventArgs e)
        {
            DragInfo info = e.Data.GetData(typeof(DragInfo)) as DragInfo;
            if (!m_EditMode || info == null)
                return;
            int row = modesList.IndexFromPoint(modesList.PointToClient(new Point(e.X, e.Y)));
            // folders can also be dropped on the empty area (they go to the top level)
            e.Effect = (row >= 0 || info.IsMode) ? DragDropEffects.Move : DragDropEffects.None;
        }

        private void ModesDragDrop(object sender, DragEventArgs e)
        {
            DragInfo info = e.Data.GetData(typeof(DragInfo)) as DragInfo;
            if (!m_EditMode || info == null || m_Project == null || m_ModeTree == null)
                return;
            Point pt = modesList.PointToClient(new Point(e.X, e.Y));
            int row = modesList.IndexFromPoint(pt);
            if (info.IsMode)
            {
                ModeNode moved = m_ModeTree.Find(info.ModeTitle);
                if (moved == null)
                    return;
                if (row < 0 || row >= m_ModeRows.Count)
                {
                    m_ModeTree.Attach(moved, m_ModeTree.Root, -1);
                    AfterTreeChange(moved.Title);
                    return;
                }
                ModeNode target = m_ModeRows[row];
                if (target == moved || target.IsDescendantOf(moved))
                    return;
                Rectangle rect = modesList.GetItemRectangle(row);
                int rel = pt.Y - rect.Y;
                if (rel < rect.Height / 4 || rel > rect.Height * 3 / 4)
                {
                    // between two rows: put it before / after the row, at the same level
                    int after = rel > rect.Height / 2 ? 1 : 0;
                    m_ModeTree.Detach(moved);
                    int at = target.Parent.Children.IndexOf(target) + after;
                    m_ModeTree.Attach(moved, target.Parent, at);
                }
                else
                {
                    // on a row: it goes inside that folder
                    target.Collapsed = false;
                    m_ModeTree.Attach(moved, target, -1);
                }
                AfterTreeChange(moved.Title);
            }
            else
            {
                int dropIndex = ModeIndexAtRow(row);
                if (dropIndex < 0)
                    return;
                int fromMode = SelectedModeIndex;
                if (fromMode == dropIndex)
                    return;
                int movedId = info.ElementId;
                string targetTitle = m_Project.GetModes()[dropIndex].Title;
                BeginInvoke(new MethodInvoker(() => ApplyProjectEdit(() =>
                {
                    IMode src = m_Project.GetModes()[fromMode];
                    IMode dst = m_Project.GetModes()[dropIndex];
                    IModeElement moved = src.GetElements().FirstOrDefault(x => x.Id == movedId);
                    if (moved == null)
                        return;
                    src.RemoveElement(moved);
                    dst.AddElement(moved);
                }, targetTitle)));
            }
        }

        private void SetEditMode(bool on)
        {
            m_EditMode = on;
            m_AddButton.Visible = on;
            ClearElementSelection();
            // Ctrl / Shift select several folders (to delete them together)
            bool oldListen = m_Listen;
            m_Listen = false;
            try
            {
                int keep = modesList.SelectedIndex;
                modesList.SelectionMode = on ? SelectionMode.MultiExtended : SelectionMode.One;
                if (keep >= 0 && keep < modesList.Items.Count)
                    modesList.SelectedIndex = keep;
            }
            finally
            {
                m_Listen = oldListen;
            }
            if (on && m_Toast == null)
            {
                // reuse the toast to explain how it works
                ShowToast(new Ares.Players.Message(Ares.Players.MessageType.Warning,
                    T("Modo de edição: arraste playlists para reordenar ou soltar sobre outra pasta; arraste pastas para reordenar ou soltar sobre outra pasta (vira subpasta); duplo clique na pasta renomeia.",
                      "Edit mode: drag playlists to reorder or onto another folder; drag folders to reorder or onto another folder (it becomes a subfolder); double-click a folder to rename.")));
            }
        }

        // called by the element panel for every created button
        private void AttachElementMenu(CheckBox box, int elementId)
        {
            box.Tag = elementId;
            Point down = Point.Empty;
            bool tracking = false;
            ElementButton card = box as ElementButton;
            if (card != null)
                card.MultiSelected = m_SelectedElements.Contains(elementId);
            box.MouseDown += (s, e) =>
            {
                tracking = m_EditMode && e.Button == MouseButtons.Left;
                down = e.Location;
                if (m_EditMode && e.Button == MouseButtons.Left)
                {
                    bool ctrl = (Control.ModifierKeys & System.Windows.Forms.Keys.Control) != 0;
                    bool shift = (Control.ModifierKeys & System.Windows.Forms.Keys.Shift) != 0;
                    if (ctrl || shift)
                    {
                        // select instead of play
                        tracking = false;
                        if (card != null)
                            card.SwallowClick = true;
                        ToggleElementSelection(elementId, shift);
                        box.Focus();
                    }
                    else if (m_SelectedElements.Count > 0)
                    {
                        ClearElementSelection();
                    }
                }
            };
            box.KeyDown += (s, e) =>
            {
                if (m_EditMode && e.KeyCode == System.Windows.Forms.Keys.Delete && m_SelectedElements.Count > 0)
                {
                    e.Handled = true;
                    DeleteSelectedElements();
                }
            };
            box.MouseUp += (s, e) =>
            {
                tracking = false;
                if (e.Button != MouseButtons.Right)
                    return;
                m_MenuElementId = elementId;
                if (m_SelectedElements.Count > 0 && !m_SelectedElements.Contains(elementId))
                    ClearElementSelection();
                int selectedCount = m_SelectedElements.Count;
                if (m_ElementDeleteItem != null)
                    m_ElementDeleteItem.Text = selectedCount > 1 ? T("Excluir " + selectedCount + " selecionadas", "Delete " + selectedCount + " selected") : T("Excluir", "Delete");
                // outside edit mode only "View tracks" is offered
                for (int i = 1; i < m_ElementMenu.Items.Count; i++)
                    m_ElementMenu.Items[i].Visible = m_EditMode;
                m_ElementMenu.Show(box, e.Location);
            };
            box.MouseMove += (s, e) =>
            {
                if (!tracking || !m_EditMode || e.Button != MouseButtons.Left)
                    return;
                Size ds = SystemInformation.DragSize;
                if (Math.Abs(e.X - down.X) < ds.Width && Math.Abs(e.Y - down.Y) < ds.Height)
                    return;
                tracking = false;
                box.DoDragDrop(new DragInfo { ElementId = elementId }, DragDropEffects.Move);
            };
            // dragging over a button counts as dragging over the panel
            box.DragOver += ElementsDragOver;
            box.DragDrop += ElementsDragDrop;
            box.DragLeave += (s, e) => HideDropMarker();
        }

        // ---- selecting several buttons ----

        private void ToggleElementSelection(int id, bool range)
        {
            IMode mode = CurrentMode();
            if (range && m_SelectAnchor >= 0 && mode != null)
            {
                List<int> ids = mode.GetElements().Select(x => x.Id).ToList();
                int a = ids.IndexOf(m_SelectAnchor);
                int b = ids.IndexOf(id);
                if (a >= 0 && b >= 0)
                {
                    for (int i = Math.Min(a, b); i <= Math.Max(a, b); i++)
                        m_SelectedElements.Add(ids[i]);
                    RefreshElementSelection();
                    return;
                }
            }
            if (!m_SelectedElements.Remove(id))
                m_SelectedElements.Add(id);
            m_SelectAnchor = id;
            RefreshElementSelection();
        }

        private void ClearElementSelection()
        {
            if (m_SelectedElements.Count == 0 && m_SelectAnchor < 0)
                return;
            m_SelectedElements.Clear();
            m_SelectAnchor = -1;
            RefreshElementSelection();
        }

        private void RefreshElementSelection()
        {
            foreach (Control c in elementsPanel.Controls)
            {
                ElementButton card = c as ElementButton;
                if (card != null && card.Tag is int)
                    card.MultiSelected = m_SelectedElements.Contains((int)card.Tag);
            }
        }

        private void DeleteSelectedElements()
        {
            IMode mode = CurrentMode();
            if (mode == null || m_SelectedElements.Count == 0)
                return;
            List<int> ids = m_SelectedElements.ToList();
            if (MessageBox.Show(this, T("Excluir as " + ids.Count + " playlists selecionadas?\n(Os arquivos de áudio não são apagados.)",
                    "Delete the " + ids.Count + " selected playlists?\n(Audio files are not deleted.)"),
                    StringResources.Ares, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            int modeIndex = SelectedModeIndex;
            string modeTitle = mode.Title;
            m_SelectedElements.Clear();
            m_SelectAnchor = -1;
            ApplyProjectEdit(() =>
            {
                IMode m = m_Project.GetModes()[modeIndex];
                foreach (int id in ids)
                {
                    IModeElement e = m.GetElements().FirstOrDefault(x => x.Id == id);
                    if (e == null)
                        continue;
                    m.RemoveElement(e);
                    DataModule.ElementRepository.DeleteElement(e.Id);
                }
            }, modeTitle);
        }

        private void DeleteSelectedModes()
        {
            List<string> titles = new List<string>();
            foreach (int row in modesList.SelectedIndices)
            {
                if (row >= 0 && row < m_ModeRows.Count && !titles.Contains(m_ModeRows[row].Title))
                    titles.Add(m_ModeRows[row].Title);
            }
            if (titles.Count == 0)
                return;
            if (MessageBox.Show(this, T("Excluir as " + titles.Count + " pastas selecionadas e todas as playlists delas?\n(Os arquivos de áudio não são apagados.)",
                    "Delete the " + titles.Count + " selected folders and all their playlists?\n(Audio files are not deleted.)"),
                    StringResources.Ares, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            ApplyProjectEdit(() =>
            {
                foreach (string title in titles)
                {
                    IMode m = m_Project.GetModes().FirstOrDefault(x => x.Title == title);
                    if (m == null)
                        continue;
                    foreach (IModeElement me in m.GetElements().ToList())
                        DataModule.ElementRepository.DeleteElement(me.Id);
                    m_Project.RemoveMode(m);
                }
            }, null);
        }

        // ---- helpers ----

        private IMode CurrentMode()
        {
            int index = SelectedModeIndex;
            if (m_Project == null || index < 0 || index >= m_Project.GetModes().Count)
                return null;
            return m_Project.GetModes()[index];
        }

        private void ApplyProjectEdit(Action edit, string selectModeTitle)
        {
            if (m_Project == null)
                return;
            bool oldWatch = fileSystemWatcher1.EnableRaisingEvents;
            fileSystemWatcher1.EnableRaisingEvents = false;
            try
            {
                StopAllPlaying();
                edit();
                m_Project.Changed = true;
                DataModule.ProjectManager.SaveProject(m_Project);
                OpenProject(m_Project.FileName, false);
                SelectModeByTitle(selectModeTitle);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, T("Erro ao editar: ", "Edit error: ") + ex.Message, StringResources.Ares,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                fileSystemWatcher1.EnableRaisingEvents = oldWatch;
            }
        }

        private static string Prompt(IWin32Window owner, string title, string label, string initial)
        {
            using (var form = new Form())
            {
                form.Text = title;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.StartPosition = FormStartPosition.CenterParent;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.ShowInTaskbar = false;
                form.AutoSize = true;
                form.AutoSizeMode = AutoSizeMode.GrowAndShrink;
                form.Padding = new Padding(12);
                var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Fill };
                var lbl = new Label { Text = label, AutoSize = true };
                var box = new TextBox { Text = initial ?? "", Width = 320 };
                var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 0) };
                var cancel = new Button { Text = T("Cancelar", "Cancel"), DialogResult = DialogResult.Cancel, AutoSize = true };
                var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true };
                buttons.Controls.Add(cancel);
                buttons.Controls.Add(ok);
                layout.Controls.Add(lbl);
                layout.Controls.Add(box);
                layout.Controls.Add(buttons);
                form.Controls.Add(layout);
                form.AcceptButton = ok;
                form.CancelButton = cancel;
                form.Shown += (s, e) => { box.SelectAll(); box.Focus(); };
                if (form.ShowDialog(owner) != DialogResult.OK)
                    return null;
                string text = box.Text.Trim();
                return text.Length == 0 ? null : text;
            }
        }

        // ---- add ----

        private void AddFolderDialog()
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = T("Escolha a pasta com os arquivos de áudio", "Choose the folder with the audio files");
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    ImportDroppedPaths(new[] { dlg.SelectedPath });
            }
        }

        private void AddFilesDialog()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Multiselect = true;
                dlg.Filter = T("Arquivos de áudio", "Audio files")
                    + "|*.mp3;*.wav;*.ogg;*.opus;*.flac;*.m4a;*.aac;*.wma;*.aif;*.aiff;*.mp2;*.mp1";
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    ImportDroppedPaths(dlg.FileNames);
            }
        }

        private void NewEmptyMode(bool asChild)
        {
            string name = Prompt(this, asChild ? T("Nova subpasta", "New subfolder") : T("Nova pasta", "New folder"),
                T("Nome da pasta:", "Folder name:"), "");
            if (name == null)
                return;
            if (m_Project == null)
            {
                // no project open: use the library
                string path = LibraryProjectPath;
                EnsureProjectFile(path);
                OpenProject(path, false);
            }
            // the new folder goes into the selected one (subfolder) or beside it (same level)
            ModeNode selected = SelectedModeNode;
            ModeNode parent = asChild ? selected : (selected != null ? selected.Parent : null);
            ApplyProjectEdit(() =>
            {
                m_Project.AddMode(name);
                EnsureModeTree();
                ModeNode node = m_ModeTree.Find(name);
                if (node != null && parent != null && parent != m_ModeTree.Root && parent != node)
                {
                    m_ModeTree.Attach(node, parent, -1);
                    parent.Collapsed = false;
                }
                SaveModeTree();
            }, name);
        }

        // ---- folders (the modes of the project) ----

        private void RenameCurrentMode()
        {
            IMode mode = CurrentMode();
            if (mode == null)
                return;
            string name = Prompt(this, T("Renomear pasta", "Rename folder"), T("Novo nome:", "New name:"), mode.Title);
            if (name == null)
                return;
            int index = SelectedModeIndex;
            string oldTitle = mode.Title;
            Ares.CommonGUI.ButtonImageStore.RenameMode(m_CurrentProjectPath, oldTitle, name);
            ApplyProjectEdit(() =>
            {
                m_Project.GetModes()[index].Title = name;
                ModeNode node = m_ModeTree != null ? m_ModeTree.Find(oldTitle) : null;
                if (node != null)
                    node.Title = name;
                SaveModeTree();
            }, name);
        }

        private void DeleteCurrentMode()
        {
            if (modesList.SelectedIndices.Count > 1)
            {
                DeleteSelectedModes();
                return;
            }
            IMode mode = CurrentMode();
            if (mode == null)
                return;
            ModeNode node = SelectedModeNode;
            string extra = node != null && node.Children.Count > 0
                ? T("\n(As subpastas dela sobem um nível e continuam existindo.)", "\n(Its subfolders move up one level and stay.)") : "";
            if (MessageBox.Show(this, T("Excluir a pasta \"" + mode.Title + "\" e todas as suas playlists?\n(Os arquivos de áudio não são apagados.)" + extra,
                    "Delete folder \"" + mode.Title + "\" and all its playlists?\n(Audio files are not deleted.)" + extra),
                    StringResources.Ares, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            int index = SelectedModeIndex;
            ApplyProjectEdit(() =>
            {
                IMode m = m_Project.GetModes()[index];
                foreach (IModeElement me in m.GetElements().ToList())
                    DataModule.ElementRepository.DeleteElement(me.Id);
                m_Project.RemoveMode(m);
            }, null);
        }

        // moving is only a change of the folder tree; the project file keeps its order
        private void MoveCurrentMode(int delta)
        {
            MoveModeNode(SelectedModeNode, delta);
        }

        // ---- buttons ----

        private IModeElement MenuElement(out IMode mode, out int index)
        {
            mode = CurrentMode();
            index = -1;
            if (mode == null)
                return null;
            IList<IModeElement> list = mode.GetElements();
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Id == m_MenuElementId)
                {
                    index = i;
                    return list[i];
                }
            }
            return null;
        }

        private void RenameElementButton()
        {
            IMode mode; int index;
            IModeElement me = MenuElement(out mode, out index);
            if (me == null)
                return;
            string name = Prompt(this, T("Renomear playlist", "Rename playlist"), T("Novo nome:", "New name:"), me.Title);
            if (name == null)
                return;
            int modeIndex = SelectedModeIndex;
            string modeTitle = mode.Title;
            Ares.CommonGUI.ButtonImageStore.RenameElement(m_CurrentProjectPath, modeTitle, me.Title, name);
            ApplyProjectEdit(() => m_Project.GetModes()[modeIndex].GetElements()[index].Title = name, modeTitle);
        }

        private void ChooseButtonPicture()
        {
            IMode mode; int index;
            IModeElement me = MenuElement(out mode, out index);
            if (me == null || string.IsNullOrEmpty(m_CurrentProjectPath))
                return;
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = T("Escolher imagem da playlist", "Choose playlist picture");
                dlg.Filter = T("Imagens|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Todos os arquivos|*.*", "Pictures|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files|*.*");
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    string spec = Ares.CommonGUI.ButtonImageStore.ImportImage(m_CurrentProjectPath, dlg.FileName);
                    Ares.CommonGUI.ButtonImageStore.Set(m_CurrentProjectPath, mode.Title, me.Title, spec);
                    UpdateElementsPanel();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void ChooseButtonIcon()
        {
            IMode mode; int index;
            IModeElement me = MenuElement(out mode, out index);
            if (me == null || string.IsNullOrEmpty(m_CurrentProjectPath))
                return;
            string icon = IconPickerDialog.Pick(this);
            if (icon == null)
                return;
            Ares.CommonGUI.ButtonImageStore.Set(m_CurrentProjectPath, mode.Title, me.Title, "lucide:" + icon);
            UpdateElementsPanel();
        }

        private void RemoveButtonImage()
        {
            IMode mode; int index;
            IModeElement me = MenuElement(out mode, out index);
            if (me == null || string.IsNullOrEmpty(m_CurrentProjectPath))
                return;
            Ares.CommonGUI.ButtonImageStore.Set(m_CurrentProjectPath, mode.Title, me.Title, null);
            UpdateElementsPanel();
        }

        private void DeleteElementButton()
        {
            if (m_SelectedElements.Count > 1 && m_SelectedElements.Contains(m_MenuElementId))
            {
                DeleteSelectedElements();
                return;
            }
            IMode mode; int index;
            IModeElement me = MenuElement(out mode, out index);
            if (me == null)
                return;
            if (MessageBox.Show(this, T("Excluir a playlist \"" + me.Title + "\"?", "Delete playlist \"" + me.Title + "\"?"),
                    StringResources.Ares, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            int modeIndex = SelectedModeIndex;
            string modeTitle = mode.Title;
            ApplyProjectEdit(() =>
            {
                IMode m = m_Project.GetModes()[modeIndex];
                IModeElement e = m.GetElements()[index];
                m.RemoveElement(e);
                DataModule.ElementRepository.DeleteElement(e.Id);
            }, modeTitle);
        }

        private void MoveElementButton(int delta)
        {
            IMode mode; int index;
            IModeElement me = MenuElement(out mode, out index);
            if (me == null)
                return;
            int target = index + delta;
            if (target < 0 || target >= mode.GetElements().Count)
                return;
            int modeIndex = SelectedModeIndex;
            string modeTitle = mode.Title;
            ApplyProjectEdit(() =>
            {
                IMode m = m_Project.GetModes()[modeIndex];
                IModeElement e = m.GetElements()[index];
                m.RemoveElement(e);
                m.InsertElement(target, e);
            }, modeTitle);
        }
    }
}
