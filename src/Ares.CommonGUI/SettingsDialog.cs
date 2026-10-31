/*
 Copyright (c) 2015 [Joerg Ruedenauer]
 
 This file is part of Ares.

 Ares is free software; you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation; either version 2 of the License, or
 (at your option) any later version.

 Ares is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 GNU General Public License for more details.

 You should have received a copy of the GNU General Public License
 along with Ares; if not, write to the Free Software
 Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA
 */
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Ares.Settings;

namespace Ares.CommonGUI
{
    public partial class SettingsDialog : Form
    {
        private List<ISettingsDialogPage> m_Pages = new System.Collections.Generic.List<ISettingsDialogPage>();

        public SettingsDialog(Ares.Settings.Settings settings, BasicSettings basicSettings)
        {
            InitializeComponent();
            musicDirLabel.Text = settings.MusicDirectory;
            soundDirLabel.Text = settings.SoundDirectory;
            if (basicSettings.UserSettingsLocation == BasicSettings.SettingsLocation.Custom)
            {
                otherDirButton.Checked = true;
            }
            else if (basicSettings.UserSettingsLocation == BasicSettings.SettingsLocation.AppDir && BasicSettings.IsAppDirAllowed())
            {
                appDirButton.Checked = true;
            }
            else
            {
                userDirButton.Checked = true;
            }
            otherDirLabel.Text = basicSettings.CustomSettingsDirectory;
            userDirLabel.Text = basicSettings.GetSettingsDir(BasicSettings.SettingsLocation.AppDataDir);
            appDirLabel.Text = basicSettings.GetSettingsDir(BasicSettings.SettingsLocation.AppDir);
            appDirButton.Enabled = BasicSettings.IsAppDirAllowed();
            updateCheckBox.Checked = settings.CheckForUpdate;
            // Apolo has its own way of telling about new versions: this old option of the Ares does nothing
            if (updateCheckBox.Parent != null)
                updateCheckBox.Parent.Visible = false;
            m_Settings = settings;
            m_BasicSettings = basicSettings;
        }

        // ------------------------------------------------------------------ layout fix for translated / larger texts

        private int m_Overflow;

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            try
            {
                m_Overflow = 0;
                FitCaptions(tabControl);
                foreach (TabPage page in tabControl.TabPages)
                {
                    if (page != generalPage)
                        FitCaptions(page);
                }
                if (m_Overflow > 0)
                    Width += m_Overflow + 8;
                LayoutGeneralPage(generalPage.ClientSize.Width, true);
            }
            catch (Exception swallowed)
            {
                Ares.Settings.ErrorLog.Note(swallowed);
            }
        }

        // The first page is laid out again by code with the real size of the texts and fonts: the fixed positions of the
        // designer put the "..." buttons over the folder names when the text is larger than the designer expected.
        private void LayoutGeneralPage(int pageW, bool grow)
        {
            int gap = 10;
            int rowH = Math.Max(30, TextRenderer.MeasureText("Ag", Font).Height + 14);
            int buttonW = Math.Max(36, TextRenderer.MeasureText("...", Font).Width + 24);
            int top = 8;

            // the folders of the music and the sounds were the way of the old Ares to find the files of a project; Apolo keeps
            // the paths in the project itself, so the box is not shown (the values stay as they are)
            groupBox2.Visible = false;

            // where the settings are kept (the "application folder" only when it can be used: not inside Program Files)
            bool appDirOk = BasicSettings.IsAppDirAllowed();
            appDirButton.Visible = appDirOk;
            appDirLabel.Visible = appDirOk;
            Control[] captions1 = appDirOk ? new Control[] { userDirButton, appDirButton, otherDirButton } : new Control[] { userDirButton, otherDirButton };
            Label[] paths1 = appDirOk ? new Label[] { userDirLabel, appDirLabel, otherDirLabel } : new Label[] { userDirLabel, otherDirLabel };
            Button[] buttons1 = appDirOk ? new Button[] { null, null, selectOtherDirButton } : new Button[] { null, selectOtherDirButton };
            LayoutRows(groupBox1, top, pageW, gap, rowH, buttonW, captions1, paths1, buttons1);
            top = groupBox1.Bottom + 10;

            groupBox3.SetBounds(6, top, pageW - 12, Math.Max(50, rowH + 30));
            updateCheckBox.Location = new Point(gap, 24);
            top = groupBox3.Bottom + 10;

            int missing = top - generalPage.ClientSize.Height;
            if (grow && missing > 0)
                Height += missing;
        }

        // ---- used when the pages are shown inside the main window instead of this dialog ----

        /// <summary>Hands out the content of a page (0 = general, then the pages added with AddPage), taken out of the tabs.</summary>
        public Control TakePage(int index)
        {
            if (index == 0)
            {
                Panel panel = new Panel();
                panel.AutoScroll = true;
                int width = 760;
                LayoutGeneralPage(width, false);
                int bottom = 0;
                foreach (Control c in GetControls(generalPage))
                {
                    if (c.Visible)
                        bottom = Math.Max(bottom, c.Bottom);
                    panel.Controls.Add(c);
                }
                panel.AutoScrollMinSize = new Size(width, bottom + 12);
                return panel;
            }
            Control page = m_Pages[index - 1].Page;
            if (page.Parent != null)
                page.Parent.Controls.Remove(page);
            page.Dock = DockStyle.None;
            return page;
        }

        /// <summary>Lays the page handed out by TakePage(0) out again (its font changed in its new place); returns the height it needs.</summary>
        public int FitGeneralPage(Control page, int width)
        {
            LayoutGeneralPage(width, false);
            int bottom = 0;
            foreach (Control c in page.Controls)
            {
                if (c.Visible)
                    bottom = Math.Max(bottom, c.Bottom);
            }
            return bottom + 12;
        }

        private static List<Control> GetControls(Control parent)
        {
            List<Control> list = new List<Control>();
            foreach (Control c in parent.Controls)
                list.Add(c);
            return list;
        }

        public string TitleOfPage(int index)
        {
            return index == 0 ? generalPage.Text : m_Pages[index - 1].PageTitle;
        }

        public int PageCount
        {
            get { return m_Pages.Count + 1; }
        }

        /// <summary>Saves everything the pages hold (the same as the OK button); false when something is not valid (a message was shown).</summary>
        public bool Confirm()
        {
            s_lastPage = tabControl.SelectedIndex;
            foreach (var page in m_Pages)
            {
                page.OnConfirm();
            }
            if (otherDirButton.Checked)
            {
                if (String.IsNullOrEmpty(otherDirLabel.Text))
                {
                    Ares.CommonGUI.MessageDialog.Show(this, StringResources.NeedDirectory, "Ares", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
                m_BasicSettings.UserSettingsLocation = BasicSettings.SettingsLocation.Custom;
            }
            else if (appDirButton.Checked && BasicSettings.IsAppDirAllowed())
            {
                m_BasicSettings.UserSettingsLocation = BasicSettings.SettingsLocation.AppDir;
            }
            else
            {
                m_BasicSettings.UserSettingsLocation = BasicSettings.SettingsLocation.AppDataDir;
            }
            m_Settings.MusicDirectory = musicDirLabel.Text;
            m_Settings.SoundDirectory = soundDirLabel.Text;
            m_Settings.CheckForUpdate = updateCheckBox.Checked;
            m_BasicSettings.CustomSettingsDirectory = otherDirLabel.Text;
            m_Settings.Commit();
            return true;
        }

        private void LayoutRows(GroupBox box, int top, int pageW, int gap, int rowH, int buttonW, Control[] captions, Label[] paths, Button[] buttons)
        {
            int captionW = 0;
            foreach (Control c in captions)
                captionW = Math.Max(captionW, TextRenderer.MeasureText(c.Text, c.Font).Width + (c is RadioButton ? 40 : 14));
            box.SetBounds(6, top, pageW - 12, 30 + captions.Length * rowH + 6);
            int innerW = box.ClientSize.Width;
            for (int i = 0; i < captions.Length; i++)
            {
                int y = 26 + i * rowH;
                Control cap = captions[i];
                cap.AutoSize = false;
                cap.SetBounds(gap, y, captionW, rowH - 4);
                if (cap is Label)
                    ((Label)cap).TextAlign = ContentAlignment.MiddleLeft;
                int pathLeft = gap + captionW + gap;
                int right = innerW - gap;
                if (buttons[i] != null)
                {
                    buttons[i].SetBounds(innerW - gap - buttonW, y - 1, buttonW, rowH - 4);
                    buttons[i].Anchor = AnchorStyles.Top | AnchorStyles.Right;
                    right = buttons[i].Left - gap;
                }
                Label path = paths[i];
                path.AutoSize = false;
                path.AutoEllipsis = true;
                path.TextAlign = ContentAlignment.MiddleLeft;
                path.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                path.SetBounds(pathLeft, y, Math.Max(40, right - pathLeft), rowH - 4);
                new ToolTip().SetToolTip(path, path.Text);
            }
        }

        private void FitCaptions(Control container)
        {
            List<Control> children = new List<Control>();
            foreach (Control c in container.Controls)
                children.Add(c);
            foreach (Control ch in children)
            {
                bool caption = (ch is Label && !((Label)ch).AutoEllipsis && (ch.Name == null || ch.Name.IndexOf("DirLabel", StringComparison.OrdinalIgnoreCase) < 0))
                    || ((ch is CheckBox || ch is RadioButton) && !((ButtonBase)ch).AutoSize);
                if (caption && !string.IsNullOrEmpty(ch.Text))
                {
                    int needed = TextRenderer.MeasureText(ch.Text, ch.Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width
                        + (ch is Label ? 6 : 26);
                    int single = TextRenderer.MeasureText("Ag", ch.Font).Height * 2;
                    if (ch.Height <= single)
                    {
                        // controls to the right of the caption's start in the same row; they may even overlap the old text
                        List<Control> row = new List<Control>();
                        int minLeft = int.MaxValue;
                        foreach (Control s in children)
                        {
                            if (s != ch && s.Left > ch.Left && s.Top < ch.Bottom && s.Bottom > ch.Top)
                            {
                                row.Add(s);
                                minLeft = Math.Min(minLeft, s.Left);
                            }
                        }
                        if (needed > ch.Width - 2)
                            ch.Width = needed;
                        if (row.Count > 0)
                        {
                            int delta = ch.Right + 8 - minLeft;
                            if (delta > 0)
                            {
                                foreach (Control s in row)
                                    s.Left += delta;
                            }
                        }
                    }
                }
                if (ch.HasChildren)
                    FitCaptions(ch);
            }
            int maxRight = 0;
            foreach (Control c in children)
                maxRight = Math.Max(maxRight, c.Right);
            int over = maxRight - (container.ClientSize.Width - 6);
            if (over > m_Overflow && !(container is TabControl))
                m_Overflow = over;
        }

        public void AddPage(ISettingsDialogPage page)
        {
            TabPage tabPage = new TabPage(page.PageTitle);
            tabPage.Controls.Add(page.Page);
            page.Page.Dock = DockStyle.Fill;
            tabControl.TabPages.Add(tabPage);
            m_Pages.Add(page);
        }

        private static int s_lastPage = -1;

        public void SetVisiblePage(int index)
        {
            if (index == -1)
                index = s_lastPage;
            if (index < 0 || index >= tabControl.TabPages.Count)
                index = 0;
            tabControl.SelectedIndex = index;
        }

        private void okButton_Click(object sender, EventArgs e)
        {
            if (!Confirm())
                DialogResult = DialogResult.None;
        }

        private Ares.Settings.Settings m_Settings { get; set; }
        private BasicSettings m_BasicSettings { get; set; }

        private void selectMusicButton_Click(object sender, EventArgs e)
        {
            SelectDirectory(musicDirLabel);
        }

        private void SelectDirectory(Label label)
        {
            folderBrowserDialog.SelectedPath = label.Text;
            if (folderBrowserDialog.ShowDialog(this) == System.Windows.Forms.DialogResult.OK)
            {
                label.Text = folderBrowserDialog.SelectedPath;
            }
        }

        private void selectSoundButton_Click(object sender, EventArgs e)
        {
            SelectDirectory(soundDirLabel);
        }

        private void selectOtherDirButton_Click(object sender, EventArgs e)
        {
            SelectDirectory(otherDirLabel);
        }
    }

    public interface ISettingsDialogPage
    {
        Control Page { get; }
        String PageTitle { get; }
        void OnConfirm();
    }
}
