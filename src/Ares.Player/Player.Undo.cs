/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 Undo / redo of the edits of the project (menu Projeto, Ctrl+Z / Ctrl+Y):
 before every change a copy of the project files is kept (project, pictures / colors, folder tree);
 undo puts the copy back and opens the project again.
*/
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using Ares.Data;
using WinKeys = System.Windows.Forms.Keys;

namespace Ares.Player
{
    public partial class Player
    {
        private class UndoSnapshot
        {
            public byte[] Project;
            public byte[] Icons;
            public byte[] Tree;
            public string ModeTitle;
        }

        private readonly List<UndoSnapshot> m_UndoStack = new List<UndoSnapshot>();
        private readonly List<UndoSnapshot> m_RedoStack = new List<UndoSnapshot>();
        private string m_UndoProject = "";
        private DateTime m_LastUndoCapture = DateTime.MinValue;
        private ToolStripMenuItem m_UndoItem;
        private ToolStripMenuItem m_RedoItem;
        private const int MaxUndo = 30;
        private bool m_UndoBusy;

        private void InitUndoMenu()
        {
            m_UndoItem = new ToolStripMenuItem(T("Desfazer", "Undo"));
            m_UndoItem.ShortcutKeyDisplayString = "Ctrl+Z";
            m_UndoItem.Click += (s, e) => UndoEdit();
            m_RedoItem = new ToolStripMenuItem(T("Refazer", "Redo"));
            m_RedoItem.ShortcutKeyDisplayString = "Ctrl+Y";
            m_RedoItem.Click += (s, e) => RedoEdit();
            int pos = projectToolStripMenuItem.DropDownItems.IndexOf(openToolStripMenuItem) + 3;
            if (pos < 0 || pos > projectToolStripMenuItem.DropDownItems.Count)
                pos = projectToolStripMenuItem.DropDownItems.Count;
            projectToolStripMenuItem.DropDownItems.Insert(pos, new ToolStripSeparator());
            projectToolStripMenuItem.DropDownItems.Insert(pos + 1, m_UndoItem);
            projectToolStripMenuItem.DropDownItems.Insert(pos + 2, m_RedoItem);
            projectToolStripMenuItem.DropDownOpening += (s, e) => UpdateUndoMenu();
            Ares.CommonGUI.ButtonImageStore.BeforeChange += OnPicturesBeforeChange;
            UpdateUndoMenu();
        }

        private void UpdateUndoMenu()
        {
            if (m_UndoItem == null)
                return;
            bool open = m_Project != null && string.Equals(m_UndoProject, m_Project.FileName, StringComparison.OrdinalIgnoreCase);
            m_UndoItem.Enabled = open && m_UndoStack.Count > 0;
            m_RedoItem.Enabled = open && m_RedoStack.Count > 0;
        }

        private void OnPicturesBeforeChange(string projectPath)
        {
            if (m_Project != null && string.Equals(projectPath, m_Project.FileName, StringComparison.OrdinalIgnoreCase))
                PushUndo();
        }

        private static byte[] ReadOrNull(string path)
        {
            try
            {
                return File.Exists(path) ? File.ReadAllBytes(path) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void WriteOrDelete(string path, byte[] data)
        {
            try
            {
                if (data == null)
                {
                    if (File.Exists(path))
                        File.Delete(path);
                }
                else
                {
                    File.WriteAllBytes(path, data);
                }
            }
            catch (Exception)
            {
            }
        }

        private UndoSnapshot CaptureSnapshot()
        {
            if (m_Project == null || string.IsNullOrEmpty(m_Project.FileName))
                return null;
            string path = m_Project.FileName;
            UndoSnapshot snap = new UndoSnapshot();
            snap.Project = ReadOrNull(path);
            if (snap.Project == null)
                return null;
            snap.Icons = ReadOrNull(Ares.CommonGUI.ButtonImageStore.SettingsFile(path));
            snap.Tree = ReadOrNull(ModeTree.FileFor(path));
            IMode current = CurrentMode();
            snap.ModeTitle = current != null ? current.Title : null;
            return snap;
        }

        // called before every change of the project; changes within two seconds count as one step
        private void PushUndo()
        {
            try
            {
                if (m_Project == null || m_UndoBusy)
                    return;
                DateTime now = DateTime.UtcNow;
                bool quick = (now - m_LastUndoCapture).TotalSeconds < 2;
                m_LastUndoCapture = now;
                if (!string.Equals(m_UndoProject, m_Project.FileName, StringComparison.OrdinalIgnoreCase))
                {
                    m_UndoStack.Clear();
                    m_RedoStack.Clear();
                    m_UndoProject = m_Project.FileName;
                    quick = false;
                }
                if (quick)
                    return;
                UndoSnapshot snap = CaptureSnapshot();
                if (snap == null)
                    return;
                m_UndoStack.Add(snap);
                if (m_UndoStack.Count > MaxUndo)
                    m_UndoStack.RemoveAt(0);
                m_RedoStack.Clear();
                UpdateUndoMenu();
            }
            catch (Exception)
            {
            }
        }

        private void UndoEdit()
        {
            Travel(m_UndoStack, m_RedoStack);
        }

        private void RedoEdit()
        {
            Travel(m_RedoStack, m_UndoStack);
        }

        private void Travel(List<UndoSnapshot> from, List<UndoSnapshot> to)
        {
            if (m_Project == null || from.Count == 0 || !string.Equals(m_UndoProject, m_Project.FileName, StringComparison.OrdinalIgnoreCase))
                return;
            bool oldWatch = fileSystemWatcher1.EnableRaisingEvents;
            fileSystemWatcher1.EnableRaisingEvents = false;
            try
            {
                string path = m_Project.FileName;
                UndoSnapshot target = from[from.Count - 1];
                from.RemoveAt(from.Count - 1);
                UndoSnapshot now = CaptureSnapshot();
                if (now != null)
                    to.Add(now);
                StopAllPlaying();
                WriteOrDelete(path, target.Project);
                WriteOrDelete(Ares.CommonGUI.ButtonImageStore.SettingsFile(path), target.Icons);
                WriteOrDelete(ModeTree.FileFor(path), target.Tree);
                Ares.CommonGUI.ButtonImageStore.Reload(path);
                m_ModeTree = null;
                m_ModeTreePath = null;
                m_LastUndoCapture = DateTime.MinValue;
                // the folder which is shown now stays (when it still exists)
                string title = now != null ? now.ModeTitle : null;
                if (title == null)
                    title = target.ModeTitle;
                m_PreferredModeTitle = title;
                OpenProject(path, false);
                if (title == null || ModeIndexByTitle(title) < 0)
                    title = target.ModeTitle;
                ShowModeAfterReload(title);
                ShowToast(new Ares.Players.Message(Ares.Players.MessageType.Info,
                    ReferenceEquals(from, m_UndoStack) ? T("Desfeito.", "Undone.") : T("Refeito.", "Redone.")));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, T("Erro ao desfazer: ", "Undo error: ") + ex.Message, StringResources.Ares,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                m_PreferredModeTitle = null;
                fileSystemWatcher1.EnableRaisingEvents = oldWatch;
                UpdateUndoMenu();
            }
        }

        // after the project was opened again: the same folder is shown as before (also after the list was filled again)
        private void ShowModeAfterReload(string title)
        {
            if (title == null)
                return;
            SelectModeByTitle(title);
            int index = SelectedModeIndex;
            if (m_Project != null && index >= 0 && index < m_Project.GetModes().Count && m_Project.GetModes()[index].Title == title)
            {
                m_PlayingControl.SetMode(m_Project.GetModes()[index]);
                bool oldListen = m_Listen;
                m_Listen = false;
                try
                {
                    UpdateElementsPanel();
                }
                finally
                {
                    m_Listen = oldListen;
                }
            }
        }

        // Ctrl+Z / Ctrl+Y (not while typing in a text box, which has its own undo)
        private bool HandleUndoKey(WinKeys keyData)
        {
            if (keyData != (WinKeys.Control | WinKeys.Z) && keyData != (WinKeys.Control | WinKeys.Y) && keyData != (WinKeys.Control | WinKeys.Shift | WinKeys.Z))
                return false;
            if (ActiveControl is TextBoxBase || (ActiveControl != null && ActiveControl.ContainsFocus && FocusedControlIsText()))
                return false;
            if (keyData == (WinKeys.Control | WinKeys.Z))
                UndoEdit();
            else
                RedoEdit();
            return true;
        }

        private bool FocusedControlIsText()
        {
            Control c = ActiveControl;
            while (c is ContainerControl && ((ContainerControl)c).ActiveControl != null)
                c = ((ContainerControl)c).ActiveControl;
            return c is TextBoxBase || c is ComboBox || c is NumericUpDown;
        }
    }
}
