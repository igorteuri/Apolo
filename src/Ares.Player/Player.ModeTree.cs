using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Ares.Data;

namespace Ares.Player
{
    public partial class Player
    {
        private ModeTree m_ModeTree;
        private string m_ModeTreePath;
        private readonly List<ModeNode> m_ModeRows = new List<ModeNode>();
        private const int ModeIndentWidth = 16;
        private const int ModeGlyphWidth = 16;

        private void InitModeTree()
        {
            try
            {
                modesList.DrawMode = DrawMode.OwnerDrawFixed;
                modesList.ItemHeight = Math.Max(22, modesList.Font.Height + 8);
                modesList.FontChanged += (s, e) => modesList.ItemHeight = Math.Max(22, modesList.Font.Height + 8);
                Ares.CommonGUI.ThemeManager.Changed += (s, e) => { ClearModeIcons(); modesList.Invalidate(); };
                modesList.DrawItem += ModesDrawItem;
                modesList.MouseDown += ModesTreeMouseDown;
                modesList.MouseUp += ModesTreeMouseUp;
                modesList.KeyDown += ModesTreeKeyDown;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }

        private void ModesDrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= m_ModeRows.Count)
                return;
            Ares.CommonGUI.ThemePalette p = Ares.CommonGUI.ThemeManager.Current;
            bool themed = p != null && !p.IsClassic;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            ModeNode node = m_ModeRows[e.Index];
            Color back = selected ? (themed ? p.Selection : SystemColors.Highlight) : modesList.BackColor;
            Color fore = themed ? p.Text : (selected ? SystemColors.HighlightText : modesList.ForeColor);
            Color dim = themed ? p.TextDim : fore;
            Color tint = Ares.CommonGUI.ButtonImageStore.GetColor(m_CurrentProjectPath, node.Title, null);
            if (!tint.IsEmpty)
            {
                double t = selected ? 0.45 : 0.14; // soft: the color of the folder is a hint, the strip at the left carries it
                back = Color.FromArgb(255, (int)(back.R + (tint.R - back.R) * t), (int)(back.G + (tint.G - back.G) * t), (int)(back.B + (tint.B - back.B) * t));
            }
            using (SolidBrush b = new SolidBrush(back))
                e.Graphics.FillRectangle(b, e.Bounds);
            if (!tint.IsEmpty)
            {
                using (SolidBrush strip = new SolidBrush(tint))
                    e.Graphics.FillRectangle(strip, e.Bounds.X, e.Bounds.Y, 5, e.Bounds.Height);
            }
            if (selected && themed)
            {
                using (SolidBrush bar = new SolidBrush(p.Accent))
                    e.Graphics.FillRectangle(bar, e.Bounds.X, e.Bounds.Y, 3, e.Bounds.Height);
            }
            int x = e.Bounds.X + 8 + node.Depth * ModeIndentWidth;
            if (node.Children.Count > 0)
            {
                float cx = x + ModeGlyphWidth / 2f - 2;
                float cy = e.Bounds.Y + e.Bounds.Height / 2f;
                SmoothingMode old = e.Graphics.SmoothingMode;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (Pen pen = new Pen(selected ? fore : dim, 1.8f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    pen.LineJoin = LineJoin.Round;
                    if (node.Collapsed)
                        e.Graphics.DrawLines(pen, new PointF[] { new PointF(cx - 2, cy - 4), new PointF(cx + 2, cy), new PointF(cx - 2, cy + 4) });
                    else
                        e.Graphics.DrawLines(pen, new PointF[] { new PointF(cx - 4, cy - 2), new PointF(cx, cy + 2), new PointF(cx + 4, cy - 2) });
                }
                e.Graphics.SmoothingMode = old;
            }
            string text = e.Index < modesList.Items.Count ? Convert.ToString(modesList.Items[e.Index]) : node.Title;
            Rectangle r = new Rectangle(x + ModeGlyphWidth, e.Bounds.Y, Math.Max(10, e.Bounds.Right - x - ModeGlyphWidth - 2), e.Bounds.Height);
            string keyText;
            if (m_ModeKeyTexts.TryGetValue(node, out keyText))
            {
                // soft card with the shortcut, at the right end
                Size ks = TextRenderer.MeasureText(keyText, modesList.Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                int kh = Math.Min(e.Bounds.Height - 6, ks.Height + 2);
                int kw = Math.Max(kh, ks.Width + 10);
                Rectangle card = new Rectangle(e.Bounds.Right - kw - 8, e.Bounds.Y + (e.Bounds.Height - kh) / 2, kw, kh);
                using (SolidBrush kb = new SolidBrush(Color.FromArgb(selected ? 70 : 45, fore)))
                    e.Graphics.FillRectangle(kb, card);
                using (Pen kd = new Pen(Color.FromArgb(80, fore)))
                    e.Graphics.DrawRectangle(kd, card.X, card.Y, card.Width - 1, card.Height - 1);
                TextRenderer.DrawText(e.Graphics, keyText, modesList.Font, card, dim,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                r.Width = Math.Max(10, r.Width - kw - 10);
            }
            // emoji, picture or icon of the folder
            int iconSize = Math.Min(20, e.Bounds.Height - 4);
            if (iconSize >= 10 && DrawModeIcon(e.Graphics, node.Title, new Rectangle(r.X, e.Bounds.Y + (e.Bounds.Height - iconSize) / 2, iconSize, iconSize), fore))
            {
                r.X += iconSize + 5;
                r.Width = Math.Max(10, r.Width - iconSize - 5);
            }
            TextRenderer.DrawText(e.Graphics, text, modesList.Font, r, fore,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.Left);
        }

        // draws the emoji / picture / icon of a folder; false if it has none
        private readonly Dictionary<string, Bitmap> m_ModeIcons = new Dictionary<string, Bitmap>();

        private void ClearModeIcons()
        {
            foreach (Bitmap b in m_ModeIcons.Values)
            {
                if (b != null)
                    b.Dispose();
            }
            m_ModeIcons.Clear();
        }

        private bool DrawModeIcon(Graphics g, string modeTitle, Rectangle bounds, Color color)
        {
            string spec = Ares.CommonGUI.ButtonImageStore.Get(m_CurrentProjectPath, modeTitle, "");
            if (string.IsNullOrEmpty(spec))
                return false;
            try
            {
                if (spec.StartsWith("lucide:"))
                {
                    // the list is painted again and again (hover, scroll, the buttons being built): the icons are made once
                    string key = spec + "|" + bounds.Width + "x" + bounds.Height + "|" + color.ToArgb();
                    Bitmap icon;
                    if (!m_ModeIcons.TryGetValue(key, out icon))
                    {
                        icon = Ares.CommonGUI.IconLibrary.Get(spec.Substring(7), bounds.Size, color);
                        m_ModeIcons[key] = icon;
                    }
                    if (icon == null)
                        return false;
                    g.DrawImage(icon, bounds);
                    return true;
                }
                Bitmap picture = Ares.CommonGUI.ButtonImageStore.LoadImage(m_CurrentProjectPath, spec);
                if (picture == null)
                    return false;
                float scale = Math.Min((float)bounds.Width / picture.Width, (float)bounds.Height / picture.Height);
                int w = Math.Max(1, (int)(picture.Width * scale));
                int h = Math.Max(1, (int)(picture.Height * scale));
                System.Drawing.Drawing2D.InterpolationMode old = g.InterpolationMode;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(picture, bounds.X + (bounds.Width - w) / 2, bounds.Y + (bounds.Height - h) / 2, w, h);
                g.InterpolationMode = old;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // ---- the tree of the open project ----

        private void EnsureModeTree()
        {
            if (m_Project == null)
            {
                m_ModeTree = null;
                m_ModeTreePath = null;
                return;
            }
            string path = m_Project.FileName;
            if (m_ModeTree == null || !string.Equals(m_ModeTreePath, path, StringComparison.OrdinalIgnoreCase))
            {
                m_ModeTree = ModeTree.Load(path);
                m_ModeTreePath = path;
            }
            List<string> titles = new List<string>();
            foreach (IMode mode in m_Project.GetModes())
                titles.Add(mode.Title);
            if (m_ModeTree.Sync(titles))
                m_ModeTree.Save(path);
        }

        private void SaveModeTree()
        {
            if (m_ModeTree != null && m_Project != null)
                m_ModeTree.Save(m_Project.FileName);
        }

        // fills the list from the tree (collapsed folders hide their content); does not touch the selection
        private void RebuildModeRows()
        {
            bool oldListen = m_Listen;
            m_Listen = false;
            modesList.BeginUpdate();
            try
            {
                modesList.Items.Clear();
                m_ModeRows.Clear();
                m_ModeKeyTexts.Clear();
                if (m_ModeTree == null || m_Project == null)
                    return;
                bool showKeys = Settings.Settings.Instance.ShowKeysInButtons;
                KeysConverter converter = new KeysConverter();
                Dictionary<string, IMode> modes = new Dictionary<string, IMode>(StringComparer.Ordinal);
                foreach (IMode mode in m_Project.GetModes())
                {
                    if (!modes.ContainsKey(mode.Title))
                        modes.Add(mode.Title, mode);
                }
                AddRows(m_ModeTree.Root, modes, showKeys, converter);
            }
            finally
            {
                modesList.EndUpdate();
                m_Listen = oldListen;
            }
        }

        // the shortcut of a folder (when "show keys" is on), drawn as a small card at the right end of its row
        private readonly Dictionary<ModeNode, string> m_ModeKeyTexts = new Dictionary<ModeNode, string>();

        private void AddRows(ModeNode parent, Dictionary<string, IMode> modes, bool showKeys, KeysConverter converter)
        {
            foreach (ModeNode node in parent.Children)
            {
                string title = node.Title;
                IMode mode;
                if (showKeys && modes.TryGetValue(title, out mode) && mode.KeyCode != 0)
                    m_ModeKeyTexts[node] = KeyText((System.Windows.Forms.Keys)mode.KeyCode);
                m_ModeRows.Add(node);
                modesList.Items.Add(title);
                if (!node.Collapsed)
                    AddRows(node, modes, showKeys, converter);
            }
        }

        private int RowOfNode(ModeNode node)
        {
            return node == null ? -1 : m_ModeRows.IndexOf(node);
        }

        private ModeNode SelectedModeNode
        {
            get
            {
                int row = modesList.SelectedIndex;
                return row >= 0 && row < m_ModeRows.Count ? m_ModeRows[row] : null;
            }
        }

        private int ModeIndexByTitle(string title)
        {
            if (m_Project == null || title == null)
                return -1;
            IList<IMode> modes = m_Project.GetModes();
            for (int i = 0; i < modes.Count; i++)
            {
                if (string.Equals(modes[i].Title, title, StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        /// <summary>Index (in the project) of the mode selected in the list, or -1.</summary>
        private int SelectedModeIndex
        {
            get
            {
                ModeNode node = SelectedModeNode;
                return node == null ? -1 : ModeIndexByTitle(node.Title);
            }
        }

        private int ModeIndexAtRow(int row)
        {
            return row >= 0 && row < m_ModeRows.Count ? ModeIndexByTitle(m_ModeRows[row].Title) : -1;
        }

        private void SelectModeIndex(int modeIndex)
        {
            if (m_Project == null || modeIndex < 0 || modeIndex >= m_Project.GetModes().Count)
                return;
            SelectModeByTitle(m_Project.GetModes()[modeIndex].Title);
        }

        // opens the folders above a mode so it is visible
        private bool ExpandAncestors(ModeNode node)
        {
            bool changed = false;
            for (ModeNode n = node != null ? node.Parent : null; n != null && n.Title != null; n = n.Parent)
            {
                if (n.Collapsed)
                {
                    n.Collapsed = false;
                    changed = true;
                }
            }
            return changed;
        }

        private void SelectModeByTitle(string title)
        {
            if (title == null || m_ModeTree == null)
                return;
            try
            {
                ModeNode node = m_ModeTree.Find(title);
                if (node == null)
                    return;
                if (ExpandAncestors(node))
                {
                    SaveModeTree();
                    RebuildModeRows();
                }
                int row = RowOfNode(node);
                if (row >= 0 && modesList.SelectedIndex != row)
                    modesList.SelectedIndex = row;
            }
            catch (Exception swallowed)
            {
                Ares.Settings.ErrorLog.Note(swallowed);
            }
        }

        // ---- folding ----

        private ModeNode m_FoldNode;

        // the folder whose arrow is under the point (null if the point is not on an arrow)
        private ModeNode FoldArrowAt(Point location)
        {
            int row = modesList.IndexFromPoint(location);
            if (row < 0 || row >= m_ModeRows.Count)
                return null;
            ModeNode node = m_ModeRows[row];
            if (node.Children.Count == 0)
                return null;
            int x = 8 + node.Depth * ModeIndentWidth;
            return (location.X >= x - 4 && location.X < x + ModeGlyphWidth + 2) ? node : null;
        }

        /// <summary>True while the left mouse button is down on a folder arrow: the click only opens / closes the folder.</summary>
        private bool IsFoldClick()
        {
            if (Control.MouseButtons != MouseButtons.Left)
                return false;
            return FoldArrowAt(modesList.PointToClient(Cursor.Position)) != null;
        }

        private void ModesTreeMouseDown(object sender, MouseEventArgs e)
        {
            m_FoldNode = e.Button == MouseButtons.Left ? FoldArrowAt(e.Location) : null;
        }

        private void ModesTreeMouseUp(object sender, MouseEventArgs e)
        {
            ModeNode node = m_FoldNode;
            m_FoldNode = null;
            if (node != null && e.Button == MouseButtons.Left)
                SetFolded(node, !node.Collapsed);
        }

        private void ModesTreeKeyDown(object sender, KeyEventArgs e)
        {
            ModeNode node = SelectedModeNode;
            if (node == null || e.Modifiers != System.Windows.Forms.Keys.None)
                return;
            if (e.KeyCode == System.Windows.Forms.Keys.Right && node.Children.Count > 0 && node.Collapsed)
            {
                SetFolded(node, false);
                e.Handled = true;
            }
            else if (e.KeyCode == System.Windows.Forms.Keys.Left && node.Children.Count > 0 && !node.Collapsed)
            {
                SetFolded(node, true);
                e.Handled = true;
            }
        }

        // opens / closes a folder and keeps the mode which is playing selected
        private void SetFolded(ModeNode node, bool collapsed)
        {
            if (node.Collapsed == collapsed)
                return;
            string keep = m_PlayingControl != null && m_PlayingControl.CurrentMode != null ? m_PlayingControl.CurrentMode.Title : null;
            node.Collapsed = collapsed;
            SaveModeTree();
            bool oldListen = m_Listen;
            m_Listen = false;
            RebuildModeRows();
            ModeNode keepNode = m_ModeTree.Find(keep);
            // the playing mode may be hidden inside the folder which was just closed: show the folder instead
            while (keepNode != null && RowOfNode(keepNode) < 0 && keepNode.Parent != null && keepNode.Parent.Title != null)
                keepNode = keepNode.Parent;
            int row = RowOfNode(keepNode);
            if (row >= 0)
                modesList.SelectedIndex = row;
            m_Listen = oldListen;
        }

        // ---- changing the tree (no change of the project file) ----

        // moves a folder up / down between its siblings
        private void MoveModeNode(ModeNode node, int delta)
        {
            if (node == null || node.Parent == null)
                return;
            int idx = node.Parent.Children.IndexOf(node);
            int target = idx + delta;
            if (target < 0 || target >= node.Parent.Children.Count)
                return;
            m_ModeTree.Attach(node, node.Parent, target);
            AfterTreeChange(node.Title);
        }

        // takes a folder out of its parent (it goes behind it)
        private void OutdentModeNode(ModeNode node)
        {
            if (node == null || node.Parent == null || node.Parent.Title == null)
                return;
            ModeNode parent = node.Parent;
            int at = parent.Parent.Children.IndexOf(parent) + 1;
            m_ModeTree.Attach(node, parent.Parent, at);
            AfterTreeChange(node.Title);
        }

        private void AfterTreeChange(string selectTitle)
        {
            PushUndo();
            SaveModeTree();
            RebuildModeRows();
            bool oldListen = m_Listen;
            m_Listen = false;
            ModeNode node = m_ModeTree.Find(selectTitle);
            if (ExpandAncestors(node))
            {
                SaveModeTree();
                RebuildModeRows();
            }
            int row = RowOfNode(node);
            if (row >= 0)
                modesList.SelectedIndex = row;
            m_Listen = oldListen;
        }
    }
}
