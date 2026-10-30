/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 Looks of the buttons and folders:
   - emoji for a button (picker with search) and "automatic icon": the emoji is guessed from the title of the button;
     the same for all buttons of the project at once (Extras menu), with a preview;
   - highlight color of a button or of a folder.
*/
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Ares.Data;

namespace Ares.Player
{
    public partial class Player
    {
        private void InitAppearanceMenu()
        {
            ToolStripMenuItem item = new ToolStripMenuItem(T("Ícones automáticos nos botões...", "Automatic icons for the buttons..."));
            item.Click += (s, e) => ShowAutoIcons();
            extrasToolStripMenuItem.DropDownItems.Add(item);
        }

        private void ShowAutoIcons()
        {
            if (m_Project == null || string.IsNullOrEmpty(m_CurrentProjectPath))
            {
                MessageBox.Show(this, T("Abra um projeto primeiro.", "Open a project first."), StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (AutoIconDialog dlg = new AutoIconDialog(this))
                dlg.ShowDialog(this);
            UpdateElementsPanel();
        }

        private void ChooseButtonEmoji()
        {
            IMode mode; int index;
            IModeElement me = MenuElement(out mode, out index);
            if (me == null || string.IsNullOrEmpty(m_CurrentProjectPath))
                return;
            EmojiEntry entry = EmojiPickerDialog.Pick(this, "");
            if (entry == null)
                return;
            ApplyEmoji(mode.Title, me.Title, entry);
            UpdateElementsPanel();
        }

        private void AutoIconForButton()
        {
            IMode mode; int index;
            IModeElement me = MenuElement(out mode, out index);
            if (me == null || string.IsNullOrEmpty(m_CurrentProjectPath))
                return;
            EmojiEntry entry = EmojiCatalog.Match(me.Title);
            if (entry == null)
            {
                MessageBox.Show(this, T("Não encontrei um emoji para o nome \"" + me.Title + "\". Escolha um na lista.",
                    "No emoji fits the name \"" + me.Title + "\". Choose one from the list."), StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Information);
                entry = EmojiPickerDialog.Pick(this, "");
                if (entry == null)
                    return;
            }
            ApplyEmoji(mode.Title, me.Title, entry);
            UpdateElementsPanel();
        }

        /// <summary>
        /// A new folder gets the emoji which fits its name, like the picture bank does for new buttons. The picture is fetched
        /// in the background; nothing happens when no emoji fits or there is no internet.
        /// </summary>
        private void AutoEmojiForNewMode(string modeTitle)
        {
            EmojiEntry entry = EmojiCatalog.Match(modeTitle);
            string projectPath = m_CurrentProjectPath;
            if (entry == null || string.IsNullOrEmpty(projectPath))
                return;
            System.Threading.Tasks.Task.Run(() =>
            {
                string file = null;
                try { file = EmojiCatalog.GetPicture(entry.Code); }
                catch (Exception swallowed) { Ares.Settings.ErrorLog.Note(swallowed); }
                if (file == null || IsDisposed)
                    return;
                BeginInvoke(new System.Windows.Forms.MethodInvoker(() =>
                {
                    if (projectPath != m_CurrentProjectPath || Ares.CommonGUI.ButtonImageStore.Get(projectPath, modeTitle, "") != null)
                        return;
                    string spec = Ares.CommonGUI.ButtonImageStore.ImportEmoji(projectPath, file, entry.Code);
                    Ares.CommonGUI.ButtonImageStore.Set(projectPath, modeTitle, "", spec);
                    modesList.Invalidate();
                }));
            });
        }

        private bool ApplyEmoji(string modeTitle, string elementTitle, EmojiEntry entry)
        {
            Cursor old = Cursor;
            Cursor = Cursors.WaitCursor;
            try
            {
                string file = EmojiCatalog.GetPicture(entry.Code);
                if (file == null)
                {
                    MessageBox.Show(this, T("Não consegui baixar a imagem do emoji. Confira a internet e tente de novo.",
                        "The emoji picture could not be downloaded. Check the internet and try again."), StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }
                string spec = Ares.CommonGUI.ButtonImageStore.ImportEmoji(m_CurrentProjectPath, file, entry.Code);
                Ares.CommonGUI.ButtonImageStore.Set(m_CurrentProjectPath, modeTitle, elementTitle, spec);
                return true;
            }
            finally
            {
                Cursor = old;
            }
        }

        private void ChooseButtonColor()
        {
            IMode mode; int index;
            IModeElement me = MenuElement(out mode, out index);
            if (me == null || string.IsNullOrEmpty(m_CurrentProjectPath))
                return;
            Color color;
            if (!ColorChoiceDialog.Pick(this, T("Cor do botão \"" + me.Title + "\"", "Color of the button \"" + me.Title + "\""),
                    Ares.CommonGUI.ButtonImageStore.GetColor(m_CurrentProjectPath, mode.Title, me.Title), out color))
                return;
            Ares.CommonGUI.ButtonImageStore.SetColor(m_CurrentProjectPath, mode.Title, me.Title, color);
            UpdateElementsPanel();
        }

        // ---- emoji, picture and icon of a folder (same files and settings as for the buttons, with the folder name as key) ----

        private void ChooseModeEmoji()
        {
            IMode mode = CurrentMode();
            if (mode == null || string.IsNullOrEmpty(m_CurrentProjectPath))
                return;
            EmojiEntry entry = EmojiPickerDialog.Pick(this, mode.Title);
            if (entry == null)
                return;
            if (ApplyEmoji(mode.Title, "", entry))
                modesList.Invalidate();
        }

        private void AutoEmojiForMode()
        {
            IMode mode = CurrentMode();
            if (mode == null || string.IsNullOrEmpty(m_CurrentProjectPath))
                return;
            EmojiEntry entry = EmojiCatalog.Match(mode.Title);
            if (entry == null)
            {
                MessageBox.Show(this, T("Não encontrei um emoji para o nome \"" + mode.Title + "\". Escolha um na lista.",
                    "No emoji fits the name \"" + mode.Title + "\". Choose one from the list."), StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Information);
                entry = EmojiPickerDialog.Pick(this, "");
                if (entry == null)
                    return;
            }
            if (ApplyEmoji(mode.Title, "", entry))
                modesList.Invalidate();
        }

        private void ChooseModeIcon()
        {
            IMode mode = CurrentMode();
            if (mode == null || string.IsNullOrEmpty(m_CurrentProjectPath))
                return;
            string icon = IconPickerDialog.Pick(this);
            if (icon == null)
                return;
            Ares.CommonGUI.ButtonImageStore.Set(m_CurrentProjectPath, mode.Title, "", "lucide:" + icon);
            modesList.Invalidate();
        }

        private void ChooseModePicture()
        {
            IMode mode = CurrentMode();
            if (mode == null || string.IsNullOrEmpty(m_CurrentProjectPath))
                return;
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = T("Escolher imagem da pasta", "Choose folder picture");
                dlg.Filter = T("Imagens|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Todos os arquivos|*.*", "Pictures|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files|*.*");
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    string spec = Ares.CommonGUI.ButtonImageStore.ImportImage(m_CurrentProjectPath, dlg.FileName);
                    Ares.CommonGUI.ButtonImageStore.Set(m_CurrentProjectPath, mode.Title, "", spec);
                    modesList.Invalidate();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void RemoveModeImage()
        {
            IMode mode = CurrentMode();
            if (mode == null || string.IsNullOrEmpty(m_CurrentProjectPath))
                return;
            Ares.CommonGUI.ButtonImageStore.Set(m_CurrentProjectPath, mode.Title, "", null);
            modesList.Invalidate();
        }

        private void ChooseModeColor()
        {
            IMode mode = CurrentMode();
            if (mode == null || string.IsNullOrEmpty(m_CurrentProjectPath))
                return;
            Color color;
            if (!ColorChoiceDialog.Pick(this, T("Cor da pasta \"" + mode.Title + "\"", "Color of the folder \"" + mode.Title + "\""),
                    Ares.CommonGUI.ButtonImageStore.GetColor(m_CurrentProjectPath, mode.Title, null), out color))
                return;
            Ares.CommonGUI.ButtonImageStore.SetColor(m_CurrentProjectPath, mode.Title, null, color);
            modesList.Invalidate();
        }

        // ---- automatic icons for all buttons of the project ----

        private class AutoIconDialog : Form
        {
            private readonly Player m_Owner;
            private readonly ListView m_List = new ListView();
            private readonly CheckBox m_OnlyEmpty = new CheckBox();
            private readonly Button m_Apply = new Button();
            private readonly Label m_Info = new Label();
            private readonly ProgressBar m_Progress = new ProgressBar();
            private bool m_Busy;

            private class Row
            {
                public string Mode, Element, Existing;
                public EmojiEntry Match;
            }

            public AutoIconDialog(Player owner)
            {
                m_Owner = owner;
                Text = Player.T("Ícones automáticos nos botões", "Automatic icons for the buttons");
                AutoScaleMode = AutoScaleMode.Font;
                ClientSize = new Size(820, 580);
                MinimumSize = new Size(640, 420);
                StartPosition = FormStartPosition.CenterParent;
                ShowInTaskbar = false;
                MinimizeBox = false;

                Label hint = new Label();
                hint.SetBounds(12, 8, 796, 36);
                hint.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                hint.Text = Player.T("O Apolo lê o nome de cada botão (floresta, taverna, dragão, batalha...) e sugere um emoji. Desmarque o que você não quer e clique em \"Aplicar\".",
                    "Apolo reads the name of each button (forest, tavern, dragon, battle...) and suggests an emoji. Untick what you do not want and click \"Apply\".");
                Controls.Add(hint);

                m_OnlyEmpty.SetBounds(12, 48, 796, 22);
                m_OnlyEmpty.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                m_OnlyEmpty.Text = Player.T("Só os botões que ainda não têm imagem nem ícone", "Only buttons that have no picture or icon yet");
                m_OnlyEmpty.Checked = true;
                m_OnlyEmpty.CheckedChanged += (s, e) => Fill();
                Controls.Add(m_OnlyEmpty);

                m_List.View = View.Details;
                m_List.CheckBoxes = true;
                m_List.FullRowSelect = true;
                m_List.GridLines = true;
                m_List.HideSelection = false;
                m_List.Columns.Add(Player.T("Pasta", "Folder"), 220);
                m_List.Columns.Add(Player.T("Botão", "Button"), 260);
                m_List.Columns.Add(Player.T("Emoji sugerido", "Suggested emoji"), 160);
                m_List.Columns.Add(Player.T("Já tem", "Has already"), 110);
                m_List.SetBounds(12, 76, 796, 408);
                m_List.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
                Controls.Add(m_List);

                m_Info.SetBounds(12, 490, 796, 36);
                m_Info.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
                Controls.Add(m_Info);
                m_Progress.SetBounds(12, 530, 796, 6);
                m_Progress.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
                m_Progress.Visible = false;
                Controls.Add(m_Progress);

                m_Apply.SetBounds(12, 542, 160, 30);
                m_Apply.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
                m_Apply.Text = Player.T("Aplicar nos marcados", "Apply to the ticked");
                m_Apply.Click += (s, e) => Apply();
                Controls.Add(m_Apply);
                Button close = new Button();
                close.SetBounds(708, 542, 100, 30);
                close.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
                close.Text = Player.T("Fechar", "Close");
                close.Click += (s, e) => Close();
                Controls.Add(close);

                Fill();
                Shown += (s, e) => EmojiCatalog.PrepareExtendedAsync(() =>
                {
                    try
                    {
                        BeginInvoke(new Action(() =>
                        {
                            if (!m_Busy)
                                Fill();
                        }));
                    }
                    catch (InvalidOperationException swallowed)
                    {
                        Ares.Settings.ErrorLog.Note(swallowed);
                    }
                });
            }

            private void Fill()
            {
                m_List.BeginUpdate();
                m_List.Items.Clear();
                int suggested = 0, total = 0;
                bool onlyEmpty = m_OnlyEmpty.Checked;
                foreach (IMode mode in m_Owner.m_Project.GetModes())
                {
                    foreach (IModeElement me in mode.GetElements())
                    {
                        total++;
                        Row r = new Row();
                        r.Mode = mode.Title;
                        r.Element = me.Title;
                        r.Existing = Ares.CommonGUI.ButtonImageStore.Get(m_Owner.m_CurrentProjectPath, mode.Title, me.Title);
                        r.Match = EmojiCatalog.Match(me.Title);
                        ListViewItem item = new ListViewItem(mode.Title);
                        item.SubItems.Add(me.Title);
                        item.SubItems.Add(r.Match != null ? r.Match.Label : "-");
                        string has = "";
                        if (r.Existing != null)
                            has = r.Existing.StartsWith("lucide:") ? Player.T("ícone", "icon") : Player.T("imagem", "picture");
                        item.SubItems.Add(has);
                        item.Tag = r;
                        item.Checked = r.Match != null && (!onlyEmpty || r.Existing == null);
                        if (item.Checked)
                            suggested++;
                        m_List.Items.Add(item);
                    }
                }
                m_List.EndUpdate();
                m_Info.Text = total + Player.T(" botões; ", " buttons; ") + suggested + Player.T(" com emoji sugerido e marcados.", " with a suggested emoji and ticked.")
                    + " " + Player.T("O que não achou emoji você pode escolher à mão (botão direito no botão > Escolher emoji).",
                        "For the ones without an emoji, choose by hand (right click on the button > Choose emoji).");
            }

            private void Apply()
            {
                if (m_Busy)
                    return;
                List<Row> rows = new List<Row>();
                foreach (ListViewItem item in m_List.Items)
                {
                    Row r = item.Tag as Row;
                    if (item.Checked && r != null && r.Match != null)
                        rows.Add(r);
                }
                if (rows.Count == 0)
                {
                    MessageBox.Show(this, Player.T("Nada marcado.", "Nothing ticked."), StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                m_Busy = true;
                m_Apply.Enabled = false;
                m_Progress.Minimum = 0;
                m_Progress.Maximum = rows.Count;
                m_Progress.Value = 0;
                m_Progress.Visible = true;
                m_Info.Text = Player.T("Baixando os emojis e aplicando...", "Downloading the emojis and applying...");
                string projectPath = m_Owner.m_CurrentProjectPath;
                System.Threading.Tasks.Task.Factory.StartNew(() =>
                {
                    Dictionary<string, string> files = new Dictionary<string, string>();
                    int done = 0;
                    foreach (Row r in rows)
                    {
                        string code = r.Match.Code;
                        if (!files.ContainsKey(code))
                            files[code] = EmojiCatalog.GetPicture(code);
                        int n = ++done;
                        try
                        {
                            BeginInvoke(new Action(() => m_Progress.Value = Math.Min(m_Progress.Maximum, n)));
                        }
                        catch (InvalidOperationException)
                        {
                            return;
                        }
                    }
                    try
                    {
                        BeginInvoke(new Action(() =>
                        {
                            int ok = 0, failed = 0;
                            foreach (Row r in rows)
                            {
                                string file;
                                if (files.TryGetValue(r.Match.Code, out file) && file != null)
                                {
                                    try
                                    {
                                        string spec = Ares.CommonGUI.ButtonImageStore.ImportEmoji(projectPath, file, r.Match.Code);
                                        Ares.CommonGUI.ButtonImageStore.Set(projectPath, r.Mode, r.Element, spec);
                                        ok++;
                                        continue;
                                    }
                                    catch (Exception swallowed)
                                    {
                                        Ares.Settings.ErrorLog.Note(swallowed);
                                    }
                                }
                                failed++;
                            }
                            m_Busy = false;
                            m_Apply.Enabled = true;
                            m_Progress.Visible = false;
                            Fill();
                            m_Info.Text = ok + Player.T(" botão(ões) com emoji.", " button(s) with an emoji.")
                                + (failed > 0 ? " " + failed + Player.T(" falharam (internet?).", " failed (internet?).") : "");
                        }));
                    }
                    catch (InvalidOperationException swallowed)
                    {
                        Ares.Settings.ErrorLog.Note(swallowed);
                    }
                });
            }
        }
    }
}
