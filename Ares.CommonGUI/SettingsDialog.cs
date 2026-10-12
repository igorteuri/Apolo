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
                    FitCaptions(page);
                if (m_Overflow > 0)
                    Width += m_Overflow + 8;
            }
            catch (Exception)
            {
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
                    if (ch.Height <= single && needed > ch.Width - 2)
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
                        ch.Width = needed;
                        if (row.Count > 0)
                        {
                            int delta = ch.Left + needed + 4 - minLeft;
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
            s_lastPage = tabControl.SelectedIndex;

            foreach (var page in m_Pages)
            {
                page.OnConfirm();
            }

            if (otherDirButton.Checked)
            {
                if (String.IsNullOrEmpty(otherDirLabel.Text))
                {
                    MessageBox.Show(this, StringResources.NeedDirectory, "Ares", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    DialogResult = DialogResult.None;
                    return;
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
