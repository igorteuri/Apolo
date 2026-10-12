using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Ares.CommonGUI;

namespace Ares.Player
{
    /// <summary>Dialog to pick one of the built-in icons for an element button.</summary>
    internal class IconPickerDialog : Form
    {
        private readonly TextBox m_Search;
        private readonly FlowLayoutPanel m_Panel;
        private readonly ToolTip m_Tip = new ToolTip();
        private readonly List<string> m_Names;
        private string m_Selected;

        public string SelectedIcon { get { return m_Selected; } }

        public static string Pick(IWin32Window owner)
        {
            using (IconPickerDialog dlg = new IconPickerDialog())
            {
                return dlg.ShowDialog(owner) == DialogResult.OK ? dlg.m_Selected : null;
            }
        }

        private static bool IsPt
        {
            get { return System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt"; }
        }

        private IconPickerDialog()
        {
            Text = IsPt ? "Escolher ícone" : "Choose icon";
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(560, 420);
            MinimumSize = new Size(360, 260);

            m_Search = new TextBox();
            m_Search.Dock = DockStyle.Top;
            m_Search.TextChanged += (s, e) => Fill();

            Label hint = new Label();
            hint.Dock = DockStyle.Top;
            hint.AutoSize = false;
            hint.Height = 24;
            hint.TextAlign = ContentAlignment.MiddleLeft;
            hint.Text = IsPt ? "Digite para filtrar (nomes em inglês: sword, skull, flame, moon ...)" : "Type to filter (sword, skull, flame, moon ...)";

            m_Panel = new FlowLayoutPanel();
            m_Panel.Dock = DockStyle.Fill;
            m_Panel.AutoScroll = true;
            m_Panel.Padding = new Padding(6);

            Controls.Add(m_Panel);
            Controls.Add(hint);
            Controls.Add(m_Search);
            m_Names = IconLibrary.AllNames();
            Shown += (s, e) => Fill();
        }

        private void Fill()
        {
            string filter = m_Search.Text.Trim().ToLowerInvariant();
            m_Panel.SuspendLayout();
            foreach (Control c in new List<Control>(m_Panel.Controls.Cast<Control>()))
            {
                m_Panel.Controls.Remove(c);
                Button old = c as Button;
                if (old != null && old.Image != null)
                    old.Image.Dispose();
                c.Dispose();
            }
            int size = (int)Math.Round(32 * Math.Max(80, ThemeManager.ScalePercent) / 110f);
            Color color = IconLibrary.ThemeColor();
            foreach (string name in m_Names)
            {
                if (filter.Length > 0 && name.IndexOf(filter, StringComparison.Ordinal) < 0)
                    continue;
                Button b = new Button();
                b.Size = new Size(size + 18, size + 18);
                b.Margin = new Padding(3);
                b.Image = IconLibrary.Get(name, new Size(size, size), color);
                b.Tag = name;
                m_Tip.SetToolTip(b, name);
                b.Click += (s, e) =>
                {
                    m_Selected = (string)((Button)s).Tag;
                    DialogResult = DialogResult.OK;
                    Close();
                };
                m_Panel.Controls.Add(b);
            }
            m_Panel.ResumeLayout();
        }
    }
}
