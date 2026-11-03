using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Ares.Player
{
    // Quick project switch: the name of the project in the status bar opens the list of the recent projects.
    public partial class Player
    {
        private void InitProjectSwitch()
        {
            if (m_StatProject == null || m_StatusStrip == null)
                return;
            m_StatProject.ToolTipText = T("Clique para trocar de projeto", "Click to switch project");
            m_StatProject.Click += (s, e) => ShowProjectSwitch();
            m_StatProject.MouseEnter += (s, e) => m_StatusStrip.Cursor = Cursors.Hand;
            m_StatProject.MouseLeave += (s, e) => m_StatusStrip.Cursor = Cursors.Default;
        }

        private void ShowProjectSwitch()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            var recent = Ares.Settings.Settings.Instance.RecentFiles.GetFiles()
                .Select(f => new KeyValuePair<string, string>(f.FilePath, f.ProjectName));
            List<RecentProjects.Entry> list = RecentProjects.ForSwitch(recent, m_Project != null ? m_Project.FileName : null, File.Exists, 10);
            foreach (RecentProjects.Entry entry in list)
            {
                string path = entry.Path;
                ToolStripMenuItem item = new ToolStripMenuItem(entry.Title);
                item.ToolTipText = path;
                item.Click += (s, e) => OpenProject(path, false);
                menu.Items.Add(item);
            }
            if (list.Count == 0)
                menu.Items.Add(new ToolStripMenuItem(T("Nenhum outro projeto recente", "No other recent project")) { Enabled = false });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(T("Abrir outro projeto...", "Open another project..."), null, (s, e) => OpenProject());
            menu.ShowItemToolTips = true;
            Ares.CommonGUI.ThemeManager.Apply(menu);
            menu.Closed += (s, e) => BeginInvoke((MethodInvoker)(() => menu.Dispose()));
            Rectangle label = m_StatProject.Bounds;
            menu.Show(m_StatusStrip, new Point(label.Right, label.Top), ToolStripDropDownDirection.AboveLeft);
        }
    }
}
