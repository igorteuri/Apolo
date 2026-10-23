using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Ares.Data;

namespace Ares.Player
{
    // A folder which has subfolders shows its own buttons and, under a heading per subfolder, the buttons of the subfolders.
    public partial class Player
    {
        /// <summary>A button of the panel: the element, the folder it belongs to and (for the first one of a group) the heading.</summary>
        private class ShownButton
        {
            public IModeElement Element;
            public string OwnerTitle;
            public string Header;
        }

        /// <summary>Heading of a group of buttons; a click opens that folder.</summary>
        private class GroupHeaderLabel : Label
        {
            public string ModeTitle;
        }

        // folder (title) which owns each button of the panel
        private readonly Dictionary<int, string> m_ElementOwners = new Dictionary<int, string>();

        /// <summary>The visible buttons of the folder, then the ones of its subfolders (depth first).</summary>
        private List<ShownButton> CollectShownButtons(IMode shownMode)
        {
            List<ShownButton> result = new List<ShownButton>();
            Dictionary<string, IMode> byTitle = new Dictionary<string, IMode>(StringComparer.Ordinal);
            foreach (IMode mode in m_Project.GetModes())
            {
                if (!byTitle.ContainsKey(mode.Title))
                    byTitle.Add(mode.Title, mode);
            }
            ModeNode node = m_ModeTree != null ? m_ModeTree.Find(shownMode.Title) : null;
            bool nested = node != null && node.Children.Count > 0;
            AddGroupButtons(result, shownMode, nested ? shownMode.Title : null);
            if (node != null)
                AddSubfolderButtons(result, node, "", byTitle, 0);
            return result;
        }

        private void AddSubfolderButtons(List<ShownButton> result, ModeNode parent, string prefix,
            Dictionary<string, IMode> byTitle, int depth)
        {
            if (depth > 32)
                return;
            foreach (ModeNode child in parent.Children)
            {
                string label = prefix.Length == 0 ? child.Title : prefix + " › " + child.Title;
                IMode mode;
                if (byTitle.TryGetValue(child.Title, out mode))
                    AddGroupButtons(result, mode, label);
                AddSubfolderButtons(result, child, label, byTitle, depth + 1);
            }
        }

        private static void AddGroupButtons(List<ShownButton> result, IMode mode, string header)
        {
            foreach (IModeElement element in mode.GetElements())
            {
                if (!element.IsVisibleInPlayer)
                    continue;
                result.Add(new ShownButton { Element = element, OwnerTitle = mode.Title, Header = header });
                header = null;
            }
        }

        // ---- a limit of buttons shown at once (the buttons are heavy controls: a thousand of them take seconds)

        private const int ShownLimitStep = 200;
        private int m_ShownLimit = ShownLimitStep;
        private string m_ShownLimitFolder;

        private class CollapsedGroup
        {
            public string Header;
            public string OwnerTitle;
            public int Count;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private static void SuspendRedraw(Control control)
        {
            if (control.IsHandleCreated)
                SendMessage(control.Handle, 0x000B, IntPtr.Zero, IntPtr.Zero); // WM_SETREDRAW off
        }

        private static void ResumeRedraw(Control control)
        {
            if (!control.IsHandleCreated)
                return;
            SendMessage(control.Handle, 0x000B, (IntPtr)1, IntPtr.Zero); // WM_SETREDRAW on
            control.Invalidate(true);
        }

        /// <summary>
        /// Keeps the first buttons (up to "limit"): the groups (a folder and its subfolders) which fit entirely, the first group
        /// partly; the other groups are only headings with their number of buttons (a click opens that folder).
        /// </summary>
        private static List<ShownButton> LimitShown(List<ShownButton> shown, int limit, out List<CollapsedGroup> collapsed, out int hiddenInFirst)
        {
            collapsed = new List<CollapsedGroup>();
            hiddenInFirst = 0;
            if (shown.Count <= limit)
                return shown;
            // the groups: a new one starts at every button which carries a heading (the first group has none)
            List<List<ShownButton>> groups = new List<List<ShownButton>>();
            foreach (ShownButton sb in shown)
            {
                if (groups.Count == 0 || sb.Header != null)
                    groups.Add(new List<ShownButton>());
                groups[groups.Count - 1].Add(sb);
            }
            List<ShownButton> kept = new List<ShownButton>();
            bool full = false;
            foreach (List<ShownButton> group in groups)
            {
                if (!full && kept.Count == 0 && group.Count > limit)
                {
                    // the first group alone is too big
                    kept.AddRange(group.GetRange(0, limit));
                    hiddenInFirst = group.Count - limit;
                    full = true;
                }
                else if (!full && kept.Count + group.Count <= limit)
                {
                    kept.AddRange(group);
                }
                else
                {
                    full = true;
                    collapsed.Add(new CollapsedGroup { Header = group[0].Header, OwnerTitle = group[0].OwnerTitle, Count = group.Count });
                }
            }
            return kept;
        }

        private void AddLimitLines(List<CollapsedGroup> collapsed, int hiddenInFirst)
        {
            if (hiddenInFirst > 0)
            {
                int more = Math.Min(ShownLimitStep, hiddenInFirst);
                Label label = MakeLimitLabel(T("▼ Mostrar mais " + more + " botões (faltam " + hiddenInFirst + ")",
                    "▼ Show " + more + " more buttons (" + hiddenInFirst + " left)"), () =>
                {
                    m_ShownLimit += ShownLimitStep;
                    UpdateElementsPanel();
                });
                elementsPanel.Controls.Add(label);
            }
            foreach (CollapsedGroup group in collapsed)
            {
                string text = (group.Header ?? group.OwnerTitle) + "  —  " + group.Count + T(" botões (clique para abrir)", " buttons (click to open)");
                elementsPanel.Controls.Add(MakeGroupHeader(text, group.OwnerTitle));
            }
        }

        private Label MakeLimitLabel(string text, Action click)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = false;
            label.Font = new Font(elementsPanel.Font, FontStyle.Bold);
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Cursor = Cursors.Hand;
            label.Margin = new Padding(3, 10, 3, 6);
            label.Height = TextRenderer.MeasureText("Ag", label.Font).Height + 14;
            label.Width = HeaderWidth();
            label.Click += (s, e) => click();
            elementsPanel.SetFlowBreak(label, true);
            return label;
        }

        private Label MakeGroupHeader(string text, string modeTitle)
        {
            GroupHeaderLabel label = new GroupHeaderLabel();
            label.ModeTitle = modeTitle;
            label.Text = text;
            label.AutoSize = false;
            label.Font = new Font(elementsPanel.Font, FontStyle.Bold);
            label.TextAlign = ContentAlignment.BottomLeft;
            label.AutoEllipsis = true;
            label.Cursor = Cursors.Hand;
            label.Margin = new Padding(3, 12, 3, 4);
            label.Height = TextRenderer.MeasureText("Ag", label.Font).Height + 10;
            label.Width = HeaderWidth();
            label.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(Color.FromArgb(90, label.ForeColor)))
                    e.Graphics.DrawLine(pen, 0, label.Height - 1, label.Width, label.Height - 1);
            };
            label.Click += (s, e) => SelectModeByTitle(modeTitle);
            elementsPanel.SetFlowBreak(label, true);
            return label;
        }

        private int HeaderWidth()
        {
            return Math.Max(120, elementsPanel.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 16);
        }

        private void FitHeaderWidths()
        {
            int w = HeaderWidth();
            foreach (Control c in elementsPanel.Controls)
            {
                if (c is GroupHeaderLabel && c.Width != w)
                    c.Width = w;
            }
        }

        // a heading is hidden when the search leaves none of its buttons
        private void ApplyHeaderFilter(bool searching)
        {
            GroupHeaderLabel current = null;
            bool any = false;
            foreach (Control c in elementsPanel.Controls)
            {
                GroupHeaderLabel header = c as GroupHeaderLabel;
                if (header != null)
                {
                    if (current != null)
                        current.Visible = any || !searching;
                    current = header;
                    any = false;
                }
                else if (c is ElementButton && c.Visible)
                {
                    any = true;
                }
            }
            if (current != null)
                current.Visible = any || !searching;
        }

        private string OwnerTitleOf(int elementId, string fallback)
        {
            string title;
            return m_ElementOwners.TryGetValue(elementId, out title) ? title : fallback;
        }

        /// <summary>
        /// Where a dragged button lands: the folder (the shown one or a subfolder under a heading) and the button
        /// it goes in front of (-1 = at the end of that folder).
        /// </summary>
        private void FindDropPosition(Point panelPoint, string shownTitle, out int beforeId, out string ownerTitle)
        {
            beforeId = -1;
            ownerTitle = shownTitle;
            List<ElementButton> buttons = new List<ElementButton>();
            foreach (Control c in elementsPanel.Controls)
            {
                ElementButton b = c as ElementButton;
                if (b != null && b.Visible && b.Tag is int)
                    buttons.Add(b);
            }
            foreach (ElementButton b in buttons)
            {
                if (!b.Bounds.Contains(panelPoint))
                    continue;
                ownerTitle = OwnerTitleOf((int)b.Tag, shownTitle);
                bool front = m_ListMode ? panelPoint.Y < b.Top + b.Height / 2 : panelPoint.X < b.Left + b.Width / 2;
                if (front)
                {
                    beforeId = (int)b.Tag;
                    return;
                }
                // behind it: in front of the next button of the same folder, or at the end of the folder
                bool passed = false;
                foreach (ElementButton next in buttons)
                {
                    if (passed && OwnerTitleOf((int)next.Tag, shownTitle) == ownerTitle)
                    {
                        beforeId = (int)next.Tag;
                        return;
                    }
                    if (next == b)
                        passed = true;
                }
                return;
            }
            // on a heading: at the start of that folder
            foreach (Control c in elementsPanel.Controls)
            {
                GroupHeaderLabel header = c as GroupHeaderLabel;
                if (header == null || !header.Visible || !header.Bounds.Contains(panelPoint))
                    continue;
                ownerTitle = header.ModeTitle;
                foreach (ElementButton b in buttons)
                {
                    if (OwnerTitleOf((int)b.Tag, shownTitle) == ownerTitle)
                    {
                        beforeId = (int)b.Tag;
                        break;
                    }
                }
                return;
            }
            // free space: at the end of the last folder
            if (buttons.Count > 0)
                ownerTitle = OwnerTitleOf((int)buttons[buttons.Count - 1].Tag, shownTitle);
        }

        /// <summary>The folder which owns a button of the panel (the shown folder when it is not known).</summary>
        private IMode OwnerModeOf(int elementId)
        {
            string title;
            if (m_Project != null && m_ElementOwners.TryGetValue(elementId, out title))
            {
                int index = ModeIndexByTitle(title);
                if (index >= 0)
                    return m_Project.GetModes()[index];
            }
            return CurrentMode();
        }
    }
}
