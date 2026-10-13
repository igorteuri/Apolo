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
    /// <summary>A folder ("mode" of the project) of the folder tree; it can contain other folders.</summary>
    internal class ModeNode
    {
        public string Title;
        public bool Collapsed;
        public ModeNode Parent;
        public List<ModeNode> Children = new List<ModeNode>();

        public int Depth
        {
            get
            {
                int d = 0;
                for (ModeNode n = Parent; n != null && n.Title != null; n = n.Parent)
                    d++;
                return d;
            }
        }

        public bool IsDescendantOf(ModeNode other)
        {
            for (ModeNode n = Parent; n != null; n = n.Parent)
            {
                if (n == other)
                    return true;
            }
            return false;
        }
    }

    /// <summary>
    /// How the modes of a project are nested in the list (folders inside folders). The project file of Ares
    /// keeps the modes flat, so the nesting is stored beside the project in "[project].ares.pastas.json".
    /// </summary>
    internal class ModeTree
    {
        public readonly ModeNode Root = new ModeNode();

        public static string FileFor(string projectPath)
        {
            return projectPath + ".pastas.json";
        }

        public static ModeTree Load(string projectPath)
        {
            ModeTree tree = new ModeTree();
            try
            {
                if (string.IsNullOrEmpty(projectPath) || !File.Exists(FileFor(projectPath)))
                    return tree;
                IDictionary doc = new JavaScriptSerializer().DeserializeObject(File.ReadAllText(FileFor(projectPath), Encoding.UTF8)) as IDictionary;
                if (doc != null && doc.Contains("nodes"))
                    ReadNodes(doc["nodes"], tree.Root);
            }
            catch (Exception)
            {
                // an unreadable file just means "no folders yet"
            }
            return tree;
        }

        private static void ReadNodes(object list, ModeNode parent)
        {
            IEnumerable items = list as IEnumerable;
            if (items == null)
                return;
            foreach (object o in items)
            {
                IDictionary d = o as IDictionary;
                if (d == null || !d.Contains("title") || d["title"] == null)
                    continue;
                ModeNode node = new ModeNode();
                node.Title = d["title"].ToString();
                node.Collapsed = d.Contains("collapsed") && d["collapsed"] is bool && (bool)d["collapsed"];
                node.Parent = parent;
                parent.Children.Add(node);
                if (d.Contains("children"))
                    ReadNodes(d["children"], node);
            }
        }

        private static List<object> WriteNodes(ModeNode parent)
        {
            List<object> result = new List<object>();
            foreach (ModeNode n in parent.Children)
            {
                Dictionary<string, object> d = new Dictionary<string, object>();
                d["title"] = n.Title;
                if (n.Collapsed)
                    d["collapsed"] = true;
                if (n.Children.Count > 0)
                    d["children"] = WriteNodes(n);
                result.Add(d);
            }
            return result;
        }

        public void Save(string projectPath)
        {
            if (string.IsNullOrEmpty(projectPath))
                return;
            try
            {
                string file = FileFor(projectPath);
                bool nested = Root.Children.Any(n => n.Children.Count > 0);
                if (!nested && !File.Exists(file))
                    return; // nothing to remember yet
                Dictionary<string, object> doc = new Dictionary<string, object>();
                doc["version"] = 1;
                doc["nodes"] = WriteNodes(Root);
                File.WriteAllText(file, new JavaScriptSerializer().Serialize(doc), new UTF8Encoding(false));
            }
            catch (Exception)
            {
            }
        }

        public IEnumerable<ModeNode> All()
        {
            Stack<ModeNode> stack = new Stack<ModeNode>();
            for (int i = Root.Children.Count - 1; i >= 0; i--)
                stack.Push(Root.Children[i]);
            while (stack.Count > 0)
            {
                ModeNode n = stack.Pop();
                yield return n;
                for (int i = n.Children.Count - 1; i >= 0; i--)
                    stack.Push(n.Children[i]);
            }
        }

        public ModeNode Find(string title)
        {
            if (title == null)
                return null;
            foreach (ModeNode n in All())
            {
                if (string.Equals(n.Title, title, StringComparison.Ordinal))
                    return n;
            }
            return null;
        }

        /// <summary>Makes the tree match the modes of the project: new modes go to the end of the top level,
        /// vanished ones disappear (their sub folders move up). Returns true if something changed.</summary>
        public bool Sync(IList<string> titles)
        {
            bool changed = false;
            HashSet<string> wanted = new HashSet<string>(titles, StringComparer.Ordinal);
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (ModeNode n in All().ToList())
            {
                if (wanted.Contains(n.Title) && seen.Add(n.Title))
                    continue;
                int idx = n.Parent.Children.IndexOf(n);
                n.Parent.Children.RemoveAt(idx);
                foreach (ModeNode c in n.Children)
                    c.Parent = n.Parent;
                n.Parent.Children.InsertRange(idx, n.Children);
                n.Children = new List<ModeNode>();
                changed = true;
            }
            foreach (string title in titles)
            {
                if (seen.Add(title))
                {
                    Root.Children.Add(new ModeNode { Title = title, Parent = Root });
                    changed = true;
                }
            }
            return changed;
        }

        public void Detach(ModeNode node)
        {
            if (node.Parent != null)
                node.Parent.Children.Remove(node);
            node.Parent = null;
        }

        public void Attach(ModeNode node, ModeNode parent, int index)
        {
            Detach(node);
            node.Parent = parent;
            if (index < 0 || index > parent.Children.Count)
                parent.Children.Add(node);
            else
                parent.Children.Insert(index, node);
        }
    }

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
            using (SolidBrush b = new SolidBrush(back))
                e.Graphics.FillRectangle(b, e.Bounds);
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
            TextRenderer.DrawText(e.Graphics, text, modesList.Font, r, fore,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.Left);
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

        private void AddRows(ModeNode parent, Dictionary<string, IMode> modes, bool showKeys, KeysConverter converter)
        {
            foreach (ModeNode node in parent.Children)
            {
                string title = node.Title;
                IMode mode;
                if (showKeys && modes.TryGetValue(title, out mode) && mode.KeyCode != 0)
                    title += " (" + converter.ConvertToString((System.Windows.Forms.Keys)mode.KeyCode) + ")";
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
            catch (Exception)
            {
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
