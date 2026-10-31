using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Ares.Player
{
    // Things which concern the whole window: the yellow frame of the edit mode, the "saved" mark in the status bar,
    // the question when the program is closed with unsaved changes, and the end of the start (window shown only when ready).
    public partial class Player
    {
        private static readonly Color EditBorderColor = Color.FromArgb(245, 184, 0);

        /// <summary>One side of the frame; clicks go through it to the window below.</summary>
        private class EditBorderStrip : Control
        {
            public EditBorderStrip()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
                TabStop = false;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(EditBorderColor);
            }

            protected override void WndProc(ref Message m)
            {
                const int WM_NCHITTEST = 0x84;
                if (m.Msg == WM_NCHITTEST)
                {
                    m.Result = (IntPtr)(-1); // HTTRANSPARENT
                    return;
                }
                base.WndProc(ref m);
            }
        }

        private EditBorderStrip[] m_EditBorder;

        // yellow frame around the whole window while the edit mode is on
        private void UpdateEditBorder(bool on)
        {
            try
            {
                if (m_EditBorder == null)
                {
                    if (!on)
                        return;
                    m_EditBorder = new EditBorderStrip[4];
                    for (int i = 0; i < m_EditBorder.Length; i++)
                    {
                        m_EditBorder[i] = new EditBorderStrip();
                        m_EditBorder[i].Name = "editBorder" + i;
                        Controls.Add(m_EditBorder[i]);
                    }
                    Resize += (s, e) => LayoutEditBorder();
                }
                foreach (EditBorderStrip strip in m_EditBorder)
                    strip.Visible = on;
                if (on)
                {
                    LayoutEditBorder();
                    foreach (EditBorderStrip strip in m_EditBorder)
                        strip.BringToFront();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }

        private void LayoutEditBorder()
        {
            if (m_EditBorder == null)
                return;
            int t = Math.Max(3, (int)Math.Round(4 * DeviceDpi / 96f));
            int w = ClientSize.Width;
            int h = ClientSize.Height;
            m_EditBorder[0].SetBounds(0, 0, w, t);
            m_EditBorder[1].SetBounds(0, h - t, w, t);
            m_EditBorder[2].SetBounds(0, 0, t, h);
            m_EditBorder[3].SetBounds(w - t, 0, t, h);
        }

        // ---- the Extras menu: grouped by what the items are for ----

        private void OrganizeExtrasMenu()
        {
            try
            {
                DetachSettingsItems();
                ToolStripItemCollection items = extrasToolStripMenuItem.DropDownItems;
                List<ToolStripItem> pool = new List<ToolStripItem>();
                foreach (ToolStripItem item in items)
                {
                    if (!(item is ToolStripSeparator))
                        pool.Add(item);
                }
                // each group: names of the items (or the beginning of their text)
                string[][] groups =
                {
                    new[] { "Ícones automáticos", "Automatic icons", "bankMenuItem" },
                    new[] { "Renomeador", "Music renamer", "Verificador", "Copyright check", "Baixar música", "Download free", "Arquivos de áudio ausentes", "Missing audio" }
                };
                List<List<ToolStripItem>> ordered = new List<List<ToolStripItem>>();
                foreach (string[] group in groups)
                {
                    List<ToolStripItem> list = new List<ToolStripItem>();
                    foreach (string key in group)
                    {
                        ToolStripItem found = pool.FirstOrDefault(i => i.Name == key || (i.Text ?? "").StartsWith(key, StringComparison.OrdinalIgnoreCase));
                        if (found != null)
                        {
                            list.Add(found);
                            pool.Remove(found);
                        }
                    }
                    ordered.Add(list);
                }
                // what is left (the lists, the left panel, the library, the notices) stays at the top
                ordered.Insert(0, pool);
                items.Clear();
                bool first = true;
                foreach (List<ToolStripItem> list in ordered)
                {
                    if (list.Count == 0)
                        continue;
                    if (!first)
                        items.Add(new ToolStripSeparator());
                    foreach (ToolStripItem item in list)
                        items.Add(item);
                    first = false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }

        // ---- unsaved changes ----

        // time of the last change of the project (every edit, also of pictures and undo, is saved at once)
        private DateTime m_LastChange = DateTime.MinValue;
        private string m_LastChangeProject = "";

        private void NoteChange()
        {
            if (m_Project == null)
                return;
            m_LastChange = DateTime.Now;
            m_LastChangeProject = m_Project.FileName ?? "";
            if (m_StatSaved != null)
                UpdateSavedLabel(Ares.CommonGUI.ThemeManager.Current);
        }

        // The edits are saved at once; the project stays "changed" only when saving did not work
        private bool IsProjectDirty()
        {
            return m_Project != null && m_Project.Changed;
        }

        private void UpdateSavedLabel(Ares.CommonGUI.ThemePalette p)
        {
            if (m_StatSaved == null)
                return;
            m_StatSaved.Visible = m_Project != null;
            if (m_Project == null)
                return;
            if (IsProjectDirty())
            {
                SetStat(m_StatSaved, "● " + T("Alterações não salvas", "Unsaved changes"), EditBorderColor);
                m_StatSaved.ToolTipText = T("Clique para salvar agora (Ctrl+S).", "Click to save now (Ctrl+S).");
            }
            else
            {
                bool changed = m_LastChange != DateTime.MinValue && string.Equals(m_LastChangeProject, m_Project.FileName ?? "", StringComparison.OrdinalIgnoreCase);
                string when = changed ? m_LastChange.ToString("HH:mm:ss") : null;
                SetStat(m_StatSaved, "✓ " + (when != null ? T("Salvo às ", "Saved at ") + when : T("Salvo", "Saved")),
                    changed ? (p != null && !p.IsClassic ? p.Accent : ForeColor) : (p != null && !p.IsClassic ? p.TextDim : ForeColor));
                m_StatSaved.ToolTipText = changed
                    ? T("Última alteração salva às " + when + ". O Apolo salva sozinho a cada alteração.", "Last change saved at " + when + ". Apolo saves after every change.")
                    : T("Tudo salvo. O Apolo salva sozinho a cada alteração.", "Everything is saved. Apolo saves after every change.");
            }
        }

        // asks about unsaved changes when the program really closes; true = the closing was cancelled
        private bool CancelCloseForUnsaved(FormClosingEventArgs e)
        {
            if (!IsProjectDirty())
                return false;
            if (e.CloseReason != CloseReason.UserClosing)
            {
                // Windows is closing: no questions, just try to save
                try { DataModule_SaveQuiet(); } catch (Exception swallowed) { Ares.Settings.ErrorLog.Note(swallowed); }
                return false;
            }
            if (!Visible || WindowState == FormWindowState.Minimized)
                RestoreFromTray();
            string name = !string.IsNullOrEmpty(m_Project.Title) ? m_Project.Title : System.IO.Path.GetFileName(m_Project.FileName);
            DialogResult answer = Ares.CommonGUI.MessageDialog.Show(this,
                T("O projeto \"" + name + "\" tem alterações que não foram salvas.\n\nSalvar antes de sair?",
                  "The project \"" + name + "\" has unsaved changes.\n\nSave before leaving?"),
                StringResources.Ares, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
            if (answer == DialogResult.Yes)
            {
                SaveProjectNow();
                if (IsProjectDirty())
                    answer = DialogResult.Cancel; // saving failed: stay
            }
            if (answer == DialogResult.Cancel)
            {
                e.Cancel = true;
                m_Quitting = false;
                return true;
            }
            return false;
        }

        private void DataModule_SaveQuiet()
        {
            Ares.Data.DataModule.ProjectManager.SaveProject(m_Project);
        }

        // ---- start of the program: the window appears only when it is complete and has its theme ----

        private bool m_StartupDone;

        private void BeginHiddenStart()
        {
            // invisible until FinishStartup (the splash screen is on meanwhile)
            Opacity = 0;
            Shown += (s, e) =>
            {
                // after the first idle time: the theme and the menus of the appearance are in place
                EventHandler once = null;
                once = (s2, e2) =>
                {
                    Application.Idle -= once;
                    FinishStartup();
                };
                Application.Idle += once;
            };
            // in any case after some seconds (e.g. when the program starts hidden in the tray)
            Timer fallback = new Timer();
            fallback.Interval = 4000;
            fallback.Tick += (s, e) =>
            {
                fallback.Stop();
                fallback.Dispose();
                FinishStartup();
            };
            fallback.Start();
        }

        private void FinishStartup()
        {
            if (m_StartupDone)
                return;
            m_StartupDone = true;
            try
            {
                Ares.CommonGUI.ThemeManager.Apply(this);
                KeepWindowOnScreen(); // the text size of the theme can make the window bigger than the screen
                PreloadWebTabs();
                if (Visible)
                    Refresh(); // paints everything with the theme while the window is still invisible
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
            Opacity = 1;
            SplashForm.CloseSplash();
            // the scale of the interface settles a little later: look once more
            Timer late = new Timer();
            late.Interval = 1500;
            late.Tick += (s2, e2) => { late.Stop(); late.Dispose(); KeepWindowOnScreen(); };
            late.Start();
        }
    }
}
