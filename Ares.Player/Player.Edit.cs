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

        private void InitEditMode()
        {
            // toolbar: edit toggle + add menu (the add menu only shows in edit mode)
            m_EditButton = new ToolStripButton();
            m_EditButton.Name = "editModeButton";
            m_EditButton.CheckOnClick = true;
            m_EditButton.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
            m_EditButton.Text = T("Editar", "Edit");
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
                if (index >= 2)
                    modesList.SelectedIndex = index;
                bool real = modesList.SelectedIndex >= 2;
                for (int i = 1; i < m_ModeMenu.Items.Count; i++)
                    m_ModeMenu.Items[i].Enabled = real;
                m_ModeMenu.Show(modesList, e.Location);
            };

            // context menu for buttons (elements)
            m_ElementMenu = new ContextMenuStrip();
            m_ElementMenu.Items.Add(T("Renomear...", "Rename..."), null, (s, e) => RenameElementButton());
            m_ElementMenu.Items.Add(T("Excluir", "Delete"), null, (s, e) => DeleteElementButton());
            m_ElementMenu.Items.Add(new ToolStripSeparator());
            m_ElementMenu.Items.Add(T("Mover para trás", "Move earlier"), null, (s, e) => MoveElementButton(-1));
            m_ElementMenu.Items.Add(T("Mover para frente", "Move later"), null, (s, e) => MoveElementButton(1));
        }

        private void SetEditMode(bool on)
        {
            m_EditMode = on;
            m_AddButton.Visible = on;
            if (on && m_Toast == null)
            {
                // reuse the toast to explain how it works
                ShowToast(new Ares.Players.Message(Ares.Players.MessageType.Warning,
                    T("Modo de edição: clique com o botão direito em um modo ou botão. Arraste pastas para adicionar.",
                      "Edit mode: right-click a mode or button. Drag folders in to add them.")));
            }
        }

        // called by the element panel for every created button
        private void AttachElementMenu(CheckBox box, int elementId)
        {
            box.MouseUp += (s, e) =>
            {
                if (!m_EditMode || e.Button != MouseButtons.Right)
                    return;
                m_MenuElementId = elementId;
                m_ElementMenu.Show(box, e.Location);
            };
        }

        // ---- helpers ----

        private IMode CurrentMode()
        {
            int index = modesList.SelectedIndex - 2;
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
            int index = modesList.SelectedIndex - 2;
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
            int index = modesList.SelectedIndex - 2;
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
            int index = modesList.SelectedIndex - 2;
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
            int modeIndex = modesList.SelectedIndex - 2;
            string modeTitle = mode.Title;
            ApplyProjectEdit(() => m_Project.GetModes()[modeIndex].GetElements()[index].Title = name, modeTitle);
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
            int modeIndex = modesList.SelectedIndex - 2;
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
            int modeIndex = modesList.SelectedIndex - 2;
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
