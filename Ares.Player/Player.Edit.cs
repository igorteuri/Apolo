using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Ares.Data;

namespace Ares.Player
{
    // Simple "edit mode" inside the Player: add / rename / delete / reorder modes and buttons
    // without opening the full Editor.
    public partial class Player
    {
        private bool m_EditMode = false;
        private ToolStripButton m_EditButton;
        private ToolStripDropDownButton m_AddButton;
        private ContextMenuStrip m_ModeMenu;
        private ContextMenuStrip m_ElementMenu;
        private int m_MenuElementId = -1;

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
                dlg.Filter = T("Projetos do Apolo/Ares|*.ares|Todos os arquivos|*.*", "Apolo/Ares projects|*.ares|All files|*.*");
                dlg.DefaultExt = "ares";
                dlg.FileName = !string.IsNullOrEmpty(m_Project.Title) ? m_Project.Title + ".ares" : "projeto.ares";
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
            // toolbar: edit toggle + add menu (the add menu only shows in edit mode)
            m_EditButton = new ToolStripButton();
            m_EditButton.Name = "editModeButton";
            m_EditButton.CheckOnClick = true;
            m_EditButton.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
            m_EditButton.Text = T("Modo de Edição", "Edit mode");
            m_EditButton.ToolTipText = T("Modo de edição: adicionar, renomear, excluir e reordenar modos e botões",
                "Edit mode: add, rename, delete and reorder modes and buttons");
            m_EditButton.CheckedChanged += (s, e) => SetEditMode(m_EditButton.Checked);

            m_AddButton = new ToolStripDropDownButton();
            m_AddButton.Name = "addButton";
            m_AddButton.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
            m_AddButton.Text = T("Adicionar", "Add");
            m_AddButton.Visible = false;
            var addFolder = new ToolStripMenuItem(T("Pasta...", "Folder..."));
            addFolder.Click += (s, e) => AddFolderDialog();
            var addFiles = new ToolStripMenuItem(T("Arquivos de áudio...", "Audio files..."));
            addFiles.Click += (s, e) => AddFilesDialog();
            var addMode = new ToolStripMenuItem(T("Modo vazio...", "Empty mode..."));
            addMode.Click += (s, e) => NewEmptyMode();
            m_AddButton.DropDownItems.AddRange(new ToolStripItem[] { addFolder, addFiles, new ToolStripSeparator(), addMode });

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
            m_ModeMenu.Items.Add(T("Novo modo vazio...", "New empty mode..."), null, (s, e) => NewEmptyMode());
            m_ModeMenu.Items.Add(T("Renomear modo...", "Rename mode..."), null, (s, e) => RenameCurrentMode());
            m_ModeMenu.Items.Add(T("Excluir modo", "Delete mode"), null, (s, e) => DeleteCurrentMode());
            m_ModeMenu.Items.Add(new ToolStripSeparator());
            m_ModeMenu.Items.Add(T("Mover para cima", "Move up"), null, (s, e) => MoveCurrentMode(-1));
            m_ModeMenu.Items.Add(T("Mover para baixo", "Move down"), null, (s, e) => MoveCurrentMode(1));
            modesList.MouseUp += (s, e) =>
            {
                if (!m_EditMode || e.Button != MouseButtons.Right)
                    return;
                int index = modesList.IndexFromPoint(e.Location);
                if (index >= 0)
                    modesList.SelectedIndex = index;
                bool real = modesList.SelectedIndex >= 0;
                for (int i = 1; i < m_ModeMenu.Items.Count; i++)
                    m_ModeMenu.Items[i].Enabled = real;
                m_ModeMenu.Show(modesList, e.Location);
            };

            // context menu for buttons (elements)
            m_ElementMenu = new ContextMenuStrip();
            m_ElementMenu.Items.Add(T("Ver lista de músicas", "View track list"), null, (s, e) => ShowTrackPanel(m_MenuElementId));
            m_ElementMenu.Items.Add(new ToolStripSeparator());
            m_ElementMenu.Items.Add(T("Renomear...", "Rename..."), null, (s, e) => RenameElementButton());
            m_ElementMenu.Items.Add(T("Excluir", "Delete"), null, (s, e) => DeleteElementButton());
            m_ElementMenu.Items.Add(new ToolStripSeparator());
            m_ElementMenu.Items.Add(T("Escolher imagem...", "Choose picture..."), null, (s, e) => ChooseButtonPicture());
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
                    modesList.DoDragDrop(new DragInfo { IsMode = true, ModeIndex = index }, DragDropEffects.Move);
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
            };
        }

        private class DragInfo
        {
            public bool IsMode;
            public int ModeIndex;
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
            int modeIndex = modesList.SelectedIndex;
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
            int index = modesList.IndexFromPoint(modesList.PointToClient(new Point(e.X, e.Y)));
            e.Effect = index >= 0 ? DragDropEffects.Move : DragDropEffects.None;
        }

        private void ModesDragDrop(object sender, DragEventArgs e)
        {
            DragInfo info = e.Data.GetData(typeof(DragInfo)) as DragInfo;
            if (!m_EditMode || info == null || m_Project == null)
                return;
            int dropIndex = modesList.IndexFromPoint(modesList.PointToClient(new Point(e.X, e.Y)));
            if (dropIndex < 0 || dropIndex >= m_Project.GetModes().Count)
                return;
            if (info.IsMode)
            {
                int from = info.ModeIndex;
                if (from == dropIndex)
                    return;
                string title = m_Project.GetModes()[from].Title;
                BeginInvoke(new MethodInvoker(() => ApplyProjectEdit(() =>
                {
                    IMode m = m_Project.GetModes()[from];
                    m_Project.RemoveMode(m);
                    m_Project.InsertMode(dropIndex, m);
                }, title)));
            }
            else
            {
                int fromMode = modesList.SelectedIndex;
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
            if (on && m_Toast == null)
            {
                // reuse the toast to explain how it works
                ShowToast(new Ares.Players.Message(Ares.Players.MessageType.Warning,
                    T("Modo de edição: arraste botões para reordenar ou soltar sobre outro modo; arraste modos para reordenar; duplo clique no modo renomeia.",
                      "Edit mode: drag buttons to reorder or onto another mode; drag modes to reorder; double-click a mode to rename.")));
            }
        }

        // called by the element panel for every created button
        private void AttachElementMenu(CheckBox box, int elementId)
        {
            box.Tag = elementId;
            Point down = Point.Empty;
            bool tracking = false;
            box.MouseDown += (s, e) =>
            {
                tracking = m_EditMode && e.Button == MouseButtons.Left;
                down = e.Location;
            };
            box.MouseUp += (s, e) =>
            {
                tracking = false;
                if (e.Button != MouseButtons.Right)
                    return;
                m_MenuElementId = elementId;
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

        // ---- helpers ----

        private IMode CurrentMode()
        {
            int index = modesList.SelectedIndex;
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

        private void NewEmptyMode()
        {
            string name = Prompt(this, T("Novo modo", "New mode"), T("Nome do modo:", "Mode name:"), "");
            if (name == null)
                return;
            if (m_Project == null)
            {
                // no project open: use the library
                string path = LibraryProjectPath;
                EnsureProjectFile(path);
                OpenProject(path, false);
            }
            ApplyProjectEdit(() => m_Project.AddMode(name), name);
        }

        // ---- modes ----

        private void RenameCurrentMode()
        {
            IMode mode = CurrentMode();
            if (mode == null)
                return;
            string name = Prompt(this, T("Renomear modo", "Rename mode"), T("Novo nome:", "New name:"), mode.Title);
            if (name == null)
                return;
            int index = modesList.SelectedIndex;
            Ares.CommonGUI.ButtonImageStore.RenameMode(m_CurrentProjectPath, mode.Title, name);
            ApplyProjectEdit(() => m_Project.GetModes()[index].Title = name, name);
        }

        private void DeleteCurrentMode()
        {
            IMode mode = CurrentMode();
            if (mode == null)
                return;
            if (MessageBox.Show(this, T("Excluir o modo \"" + mode.Title + "\" e todos os seus botões?\n(Os arquivos de áudio não são apagados.)",
                    "Delete mode \"" + mode.Title + "\" and all its buttons?\n(Audio files are not deleted.)"),
                    StringResources.Ares, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            int index = modesList.SelectedIndex;
            ApplyProjectEdit(() =>
            {
                IMode m = m_Project.GetModes()[index];
                foreach (IModeElement me in m.GetElements().ToList())
                    DataModule.ElementRepository.DeleteElement(me.Id);
                m_Project.RemoveMode(m);
            }, null);
        }

        private void MoveCurrentMode(int delta)
        {
            IMode mode = CurrentMode();
            int index = modesList.SelectedIndex;
            if (mode == null)
                return;
            int target = index + delta;
            if (target < 0 || target >= m_Project.GetModes().Count)
                return;
            string title = mode.Title;
            ApplyProjectEdit(() =>
            {
                IMode m = m_Project.GetModes()[index];
                m_Project.RemoveMode(m);
                m_Project.InsertMode(target, m);
            }, title);
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
            string name = Prompt(this, T("Renomear botão", "Rename button"), T("Novo nome:", "New name:"), me.Title);
            if (name == null)
                return;
            int modeIndex = modesList.SelectedIndex;
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
                dlg.Title = T("Escolher imagem do botão", "Choose button picture");
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
            IMode mode; int index;
            IModeElement me = MenuElement(out mode, out index);
            if (me == null)
                return;
            if (MessageBox.Show(this, T("Excluir o botão \"" + me.Title + "\"?", "Delete button \"" + me.Title + "\"?"),
                    StringResources.Ares, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            int modeIndex = modesList.SelectedIndex;
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
            int modeIndex = modesList.SelectedIndex;
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
