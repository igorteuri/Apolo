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
