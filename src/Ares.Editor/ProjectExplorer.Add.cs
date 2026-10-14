/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 Project explorer: the "Adicionar" button at the top, the folder overview shown in the editor area
 and the "Ver no editor" entry of the context menus (a right click alone does not change what is shown).
*/
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Ares.Data;

namespace Ares.Editor
{
    partial class ProjectExplorer
    {
        internal static string Tx(string pt, string en)
        {
            return System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt" ? pt : en;
        }

        private ToolStripDropDownButton m_AddButton;
        private ContextMenuStrip m_AddMenu;
        private bool m_NoPreview;
        private ModeOverviewWindow m_Overview;

        private void InitAddButton()
        {
            // web radio is not used any more
            webRadioMenuItem.Visible = false;
            webRadioItem2.Visible = false;

            ElementEditors.Editors.CloseOverview = () => CloseOverview();
            m_AddMenu = new ContextMenuStrip();
            m_AddMenu.Items.Add(Tx("Pasta", "Folder"), null, (s, e) => AddInProject(() => AddMode(true)));
            m_AddMenu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem music = new ToolStripMenuItem(Tx("Música", "Music"));
            music.DropDownItems.Add(Tx("Lista de músicas aleatória", "Random music list"), null, (s, e) => AddInFolder(AddRandomPlaylist));
            music.DropDownItems.Add(Tx("Lista de músicas sequencial", "Sequential music list"), null, (s, e) => AddInFolder(AddSequentialPlaylist));
            m_AddMenu.Items.Add(music);
            ToolStripMenuItem effects = new ToolStripMenuItem(Tx("Efeitos e sons", "Effects and sounds"));
            effects.DropDownItems.Add(Tx("Sons de fundo", "Background sounds"), null, (s, e) => AddInFolder(AddBackgroundSounds));
            effects.DropDownItems.Add(Tx("Lista paralela", "Parallel list"), null, (s, e) => AddInFolder(AddParallelList));
            effects.DropDownItems.Add(Tx("Lista sequencial", "Sequential list"), null, (s, e) => AddInFolder(AddSequentialList));
            effects.DropDownItems.Add(Tx("Lista aleatória", "Random list"), null, (s, e) => AddInFolder(AddRandomList));
            m_AddMenu.Items.Add(effects);
            m_AddMenu.Items.Add("Macro", null, (s, e) => AddInFolder(AddMacro));

            m_AddButton = new ToolStripDropDownButton(Tx("Adicionar", "Add"));
            m_AddButton.Name = "addButton";
            m_AddButton.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
            m_AddButton.ShowDropDownArrow = true;
            m_AddButton.DropDown = m_AddMenu;
            m_AddButton.ToolTipText = Tx("Adicionar pasta ou playlist", "Add a folder or a playlist");
            toolStrip1.Items.Insert(0, m_AddButton);
            toolStrip1.Items.Insert(1, new ToolStripSeparator());
            ToolStripButton expandAll = new ToolStripButton();
            expandAll.Name = "expandAllButton";
            expandAll.DisplayStyle = ToolStripItemDisplayStyle.Image;
            expandAll.ToolTipText = Tx("Expandir todas as pastas", "Expand all folders");
            expandAll.Click += (s, e) => { projectTree.BeginUpdate(); projectTree.ExpandAll(); projectTree.EndUpdate(); };
            ToolStripButton collapseAll = new ToolStripButton();
            collapseAll.Name = "collapseAllButton";
            collapseAll.DisplayStyle = ToolStripItemDisplayStyle.Image;
            collapseAll.ToolTipText = Tx("Recolher todas as pastas", "Collapse all folders");
            collapseAll.Click += (s, e) =>
            {
                projectTree.BeginUpdate();
                projectTree.CollapseAll();
                if (projectTree.Nodes.Count > 0)
                    projectTree.Nodes[0].Expand();
                projectTree.EndUpdate();
            };
            toolStrip1.Items.Insert(2, expandAll);
            toolStrip1.Items.Insert(3, collapseAll);
            toolStrip1.Items.Insert(4, new ToolStripSeparator());
            m_AddMenu.Opening += (s, e) =>
            {
                bool folder = FolderNodeOfSelection() != null;
                for (int i = 2; i < m_AddMenu.Items.Count; i++)
                    m_AddMenu.Items[i].Enabled = folder;
            };

            // a right click only opens the menu; the editor changes when "Ver no editor" is chosen
            foreach (ContextMenuStrip menu in new ContextMenuStrip[] { modeContextMenu, elementContextMenu })
            {
                ToolStripMenuItem view = new ToolStripMenuItem(Tx("Ver no editor", "Show in editor"));
                view.Click += (s, e) => { m_NoPreview = false; ShowPreviewEditor(); };
                menu.Items.Insert(0, view);
                menu.Items.Insert(1, new ToolStripSeparator());
                menu.Closed += (s, e) => m_NoPreview = false;
            }
            projectTree.MouseDown += (s, e) =>
            {
                m_NoPreview = e.Button == MouseButtons.Right;
            };
        }

        /// <summary>Save / undo / redo buttons of the main window, shown beside the buttons of the explorer.</summary>
        public void AdoptToolbarItems(params ToolStripItem[] items)
        {
            int index = 5;
            foreach (ToolStripItem item in items)
            {
                if (item.Owner != null)
                    item.Owner.Items.Remove(item);
                toolStrip1.Items.Insert(index++, item);
            }
            toolStrip1.Items.Insert(index, new ToolStripSeparator());
        }

        private TreeNode FolderNodeOfSelection()
        {
            TreeNode node = projectTree.SelectedNode;
            while (node != null && !(node.Tag is IMode))
                node = node.Parent;
            return node;
        }

        private void AddInFolder(System.Action add)
        {
            TreeNode folder = FolderNodeOfSelection();
            if (folder == null)
                return;
            m_NoPreview = false;
            projectTree.SelectedNode = folder;
            add();
        }

        private void AddInProject(System.Action add)
        {
            if (projectTree.Nodes.Count == 0)
                return;
            m_NoPreview = false;
            projectTree.SelectedNode = projectTree.Nodes[0];
            add();
        }

        /// <summary>The folder was clicked: its playlists are listed in the editor area.</summary>
        private void ShowModeOverview(TreeNode modeNode)
        {
            IMode mode = modeNode.Tag as IMode;
            if (mode == null || DockPanel == null)
                return;
            CloseOtherEditors();
            if (m_Overview == null || m_Overview.IsDisposed)
            {
                m_Overview = new ModeOverviewWindow();
                m_Overview.OpenRequested = element =>
                {
                    TreeNode found = FindModeElementNode(projectTree.Nodes, element);
                    if (found != null)
                    {
                        m_NoPreview = false;
                        projectTree.SelectedNode = found;
                        found.EnsureVisible();
                    }
                };
                m_Overview.ShowHint = WeifenLuo.WinFormsUI.Docking.DockState.Document;
                m_Overview.Show(DockPanel);
            }
            m_Overview.SetMode(mode, modeNode.Text, m_Project != null ? m_Project.FileName : null);
            projectTree.Focus();
        }

        private static TreeNode FindModeElementNode(TreeNodeCollection nodes, IModeElement element)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Tag == element)
                    return node;
                TreeNode sub = FindModeElementNode(node.Nodes, element);
                if (sub != null)
                    return sub;
            }
            return null;
        }

        private void CloseOverview()
        {
            if (m_Overview != null && !m_Overview.IsDisposed)
                m_Overview.Close();
            m_Overview = null;
        }

        private void CloseOtherEditors()
        {
            foreach (ElementEditors.EditorBase editor in ElementEditors.EditorRegistry.Instance.GetAllEditors())
            {
                try { editor.Close(); } catch (Exception) { }
            }
            m_PreviewElementId = -1;
        }
    }
}
