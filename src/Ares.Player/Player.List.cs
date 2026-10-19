using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Ares.Player
{
    // Search box and "cards / list" switch above the buttons of a mode.
    // In list mode every element is one row of a vertical list (like a sound board's list view);
    // the rows are the same ElementButton controls, so playing, state, menus and edit mode work unchanged.
    public partial class Player
    {
        private bool m_ListMode = false;
        private Panel m_ListBar;
        private TextBox m_SearchBox;
        private Button m_ListToggle;
        private Button m_AddBarButton;
        private int m_FilterModeIndex = -1;
        private bool m_ListReady = false;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        private void InitListMode()
        {
            try
            {
                m_ListMode = Ares.CommonGUI.Preferences.GetBool("ListMode", false);
                Control host = elementsPanel.Parent;
                if (host == null)
                    return;

                m_ListBar = new Panel();
                m_ListBar.Name = "listBar";
                m_SearchBox = new TextBox();
                m_SearchBox.Name = "searchBox";
                m_ListToggle = new Button();
                m_ListToggle.Name = "listToggle";
                m_ListToggle.FlatStyle = FlatStyle.System;
                m_ListToggle.UseVisualStyleBackColor = true;
                m_AddBarButton = new Button();
                m_AddBarButton.Name = "addBarButton";
                m_AddBarButton.FlatStyle = FlatStyle.System;
                m_AddBarButton.UseVisualStyleBackColor = true;
                m_AddBarButton.Text = T("+ Adicionar \u25BE", "+ Add \u25BE");
                m_AddBarButton.Click += (s, e) => ShowAddMenu(m_AddBarButton, new Point(0, m_AddBarButton.Height));

                int barHeight = Math.Max(26, m_SearchBox.PreferredHeight + 2);
                int gap = 4;
                m_ListBar.SetBounds(elementsPanel.Left, elementsPanel.Top, elementsPanel.Width, barHeight);
                m_ListBar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                m_ListBar.Controls.Add(m_SearchBox);
                m_ListBar.Controls.Add(m_ListToggle);
                m_ListBar.Controls.Add(m_AddBarButton);
                m_ListBar.Resize += (s, e) => LayoutListBar();

                elementsPanel.Top += barHeight + gap;
                elementsPanel.Height -= barHeight + gap;
                host.Controls.Add(m_ListBar);

                m_ListToggle.Click += (s, e) =>
                {
                    m_ListMode = !m_ListMode;
                    Ares.CommonGUI.Preferences.SetBool("ListMode", m_ListMode);
                    UpdateListToggleText();
                    UpdateElementsPanel();
                };
                m_SearchBox.TextChanged += (s, e) => ApplyElementFilter();
                m_SearchBox.KeyDown += (s, e) =>
                {
                    if (e.KeyCode == Keys.Escape)
                    {
                        m_SearchBox.Text = "";
                        e.SuppressKeyPress = true;
                    }
                    else if (e.KeyCode == Keys.Enter)
                    {
                        e.SuppressKeyPress = true;
                        foreach (Control c in elementsPanel.Controls)
                        {
                            if (c is ElementButton && c.Visible && c.Tag is int)
                            {
                                m_PlayingControl.SwitchElement((int)c.Tag);
                                break;
                            }
                        }
                    }
                };
                m_SearchBox.HandleCreated += (s, e) =>
                    SendMessage(m_SearchBox.Handle, 0x1501, (IntPtr)1, T("Buscar playlist...", "Search playlist..."));

                elementsPanel.VisibleChanged += (s, e) => m_ListBar.Visible = elementsPanel.Visible;
                elementsPanel.Resize += (s, e) =>
                {
                    FitHeaderWidths();
                    if (m_ListMode)
                        FitRowWidths();
                };
                m_ListBar.Visible = elementsPanel.Visible;
                UpdateListToggleText();
                LayoutListBar();
                m_ListReady = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }

        private void UpdateListToggleText()
        {
            m_ListToggle.Text = m_ListMode ? T("▦ Cartões", "▦ Cards") : T("☰ Lista", "☰ List");
        }

        private void LayoutListBar()
        {
            if (m_ListBar == null)
                return;
            int toggleWidth = Math.Max(90, TextRenderer.MeasureText("▦ Cartões", m_ListToggle.Font).Width + 24);
            int addWidth = Math.Max(90, TextRenderer.MeasureText(m_AddBarButton.Text, m_AddBarButton.Font).Width + 24);
            m_ListToggle.SetBounds(m_ListBar.ClientSize.Width - toggleWidth, 0, toggleWidth, m_ListBar.ClientSize.Height);
            m_AddBarButton.SetBounds(m_ListBar.ClientSize.Width - toggleWidth - 6 - addWidth, 0, addWidth, m_ListBar.ClientSize.Height);
            m_SearchBox.SetBounds(0, 0, Math.Max(40, m_ListBar.ClientSize.Width - toggleWidth - addWidth - 12), m_SearchBox.Height);
            m_SearchBox.Top = (m_ListBar.ClientSize.Height - m_SearchBox.Height) / 2;
        }

        private void FitRowWidths()
        {
            int w = elementsPanel.ClientSize.Width - 8 - SystemInformation.VerticalScrollBarWidth;
            w = Math.Max(120, w);
            foreach (Control c in elementsPanel.Controls)
            {
                if (c is ElementButton && c.Width != w)
                    c.Width = w;
            }
        }

        // called at the end of UpdateElementsPanel, after the buttons were created
        private void AfterElementsBuilt(int modeIndex)
        {
            if (!m_ListReady)
                return;
            elementsPanel.SuspendLayout();
            if (m_ListMode)
            {
                elementsPanel.FlowDirection = FlowDirection.TopDown;
                elementsPanel.WrapContents = false;
                int rowHeight = ElementButton.RowHeight(elementsPanel.Font);
                foreach (Control c in elementsPanel.Controls)
                {
                    ElementButton b = c as ElementButton;
                    if (b == null)
                        continue;
                    b.ListMode = true;
                    b.Height = rowHeight;
                }
                FitRowWidths();
            }
            else
            {
                elementsPanel.FlowDirection = FlowDirection.LeftToRight;
                elementsPanel.WrapContents = true;
            }
            // a new search for every mode
            if (m_FilterModeIndex != modeIndex)
            {
                m_FilterModeIndex = modeIndex;
                if (m_SearchBox.Text.Length > 0)
                    m_SearchBox.Text = "";
            }
            FitHeaderWidths();
            ApplyElementFilter();
            elementsPanel.ResumeLayout(true);
            AfterElementsForTrackPanel();
        }

        private void ApplyElementFilter()
        {
            if (m_SearchBox == null)
                return;
            string q = m_SearchBox.Text.Trim();
            elementsPanel.SuspendLayout();
            foreach (Control c in elementsPanel.Controls)
            {
                if (c is ElementButton)
                    c.Visible = q.Length == 0 || c.Text.IndexOf(q, StringComparison.CurrentCultureIgnoreCase) >= 0;
            }
            ApplyHeaderFilter(q.Length > 0);
            elementsPanel.ResumeLayout(true);
        }
    }
}
