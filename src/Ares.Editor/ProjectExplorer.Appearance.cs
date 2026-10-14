/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 Looks of the buttons and folders in the Editor (same files and dialogs as in the Player):
 emoji, automatic emoji by name, color, icon, picture, pictures from the internet.
*/
using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Ares.Data;

namespace Ares.Editor
{
    partial class ProjectExplorer
    {
        private ToolStripMenuItem m_ModeLooks;
        private ToolStripMenuItem m_ElementLooks;

        private void InitAppearanceMenus()
        {
            m_ModeLooks = BuildLooksMenu();
            m_ElementLooks = BuildLooksMenu();
            modeContextMenu.Items.Add(new ToolStripSeparator());
            modeContextMenu.Items.Add(m_ModeLooks);
            elementContextMenu.Items.Add(new ToolStripSeparator());
            elementContextMenu.Items.Add(m_ElementLooks);
            elementContextMenu.Opening += (s, e) =>
            {
                // only the buttons of a folder (not the parts inside a playlist) have their own look
                bool top = SelectedNode != null && SelectedNode.Tag is IModeElement;
                m_ElementLooks.Visible = top;
                elementContextMenu.Items[elementContextMenu.Items.IndexOf(m_ElementLooks) - 1].Visible = top;
            };
        }

        private ToolStripMenuItem BuildLooksMenu()
        {
            ToolStripMenuItem root = new ToolStripMenuItem(Tx("Aparência no Player", "Look in the Player"));
            root.DropDownItems.Add(Tx("Escolher emoji", "Choose emoji"), null, (s, e) => LooksChooseEmoji());
            root.DropDownItems.Add(Tx("Emoji automático pelo nome", "Automatic emoji from the name"), null, (s, e) => LooksAutoEmoji());
            root.DropDownItems.Add(Tx("Cor do botão", "Button color"), null, (s, e) => LooksChooseColor());
            root.DropDownItems.Add(Tx("Escolher ícone", "Choose icon"), null, (s, e) => LooksChooseIcon());
            root.DropDownItems.Add(Tx("Escolher imagem", "Choose picture"), null, (s, e) => LooksChoosePicture());
            root.DropDownItems.Add(Tx("Sugerir imagens na internet", "Suggest pictures from the internet"), null, (s, e) => LooksSuggestImages());
            root.DropDownItems.Add(Tx("Remover imagem", "Remove picture"), null, (s, e) => LooksRemoveImage());
            return root;
        }

        private class LookTarget
        {
            public string ProjectPath;
            public string Mode;
            public string Element;   // "" for a folder
            public string Title;
            public bool IsFolder;
        }

        private LookTarget GetLookTarget()
        {
            TreeNode node = SelectedNode;
            if (node == null || m_Project == null)
                return null;
            LookTarget target = new LookTarget();
            IMode mode = node.Tag as IMode;
            if (mode != null)
            {
                target.Mode = mode.Title;
                target.Element = "";
                target.Title = mode.Title;
                target.IsFolder = true;
            }
            else
            {
                IModeElement modeElement = node.Tag as IModeElement;
                IMode parentMode = node.Parent != null ? node.Parent.Tag as IMode : null;
                if (modeElement == null || parentMode == null)
                    return null;
                target.Mode = parentMode.Title;
                target.Element = modeElement.Title;
                target.Title = modeElement.Title;
            }
            if (string.IsNullOrEmpty(m_Project.FileName))
            {
                MessageBox.Show(this, Tx("Salve o projeto primeiro (Arquivo > Salvar). Os ícones ficam guardados junto com o arquivo do projeto.",
                    "Save the project first (File > Save). The icons are kept beside the project file."), "Apolo",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return null;
            }
            target.ProjectPath = m_Project.FileName;
            return target;
        }

        /// <summary>Raised when another folder or playlist is selected (null when nothing useful is selected).</summary>
        public event System.Action<SelectionInfo> SelectionInfoChanged;

        internal void PlaySelected()
        {
            if (playButton.Enabled)
                playButton.PerformClick();
        }

        private void RaiseSelectionInfo()
        {
            if (SelectionInfoChanged == null)
                return;
            SelectionInfo info = null;
            TreeNode node = SelectedNode;
            if (node != null && m_Project != null)
            {
                IMode mode = node.Tag as IMode;
                IModeElement modeElement = node.Tag as IModeElement;
                IMode parentMode = node.Parent != null ? node.Parent.Tag as IMode : null;
                if (mode != null)
                {
                    info = new SelectionInfo { Source = this, ProjectPath = m_Project.FileName, Mode = mode.Title, Element = "", Title = mode.Title, TypeName = "", IsFolder = true };
                }
                else if (modeElement != null && parentMode != null)
                {
                    info = new SelectionInfo { Source = this, ProjectPath = m_Project.FileName, Mode = parentMode.Title, Element = modeElement.Title,
                        Title = modeElement.Title, TypeName = ModeOverviewWindow.TypeName(modeElement.StartElement), IsFolder = false };
                }
            }
            SelectionInfoChanged(info);
        }

        private void LooksChanged()
        {
            RaiseSelectionInfo();
            if (m_Overview != null && !m_Overview.IsDisposed)
                m_Overview.Reload();
            projectTree.Invalidate();
        }

        private bool ApplyEmojiTo(LookTarget t, Ares.Player.EmojiEntry entry)
        {
            Cursor old = Cursor;
            Cursor = Cursors.WaitCursor;
            try
            {
                string file = Ares.Player.EmojiCatalog.GetPicture(entry.Code);
                if (file == null)
                {
                    MessageBox.Show(this, Tx("Não consegui baixar a imagem do emoji. Confira a internet e tente de novo.",
                        "The emoji picture could not be downloaded. Check the internet and try again."), "Apolo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }
                string spec = Ares.CommonGUI.ButtonImageStore.ImportEmoji(t.ProjectPath, file, entry.Code);
                Ares.CommonGUI.ButtonImageStore.Set(t.ProjectPath, t.Mode, t.Element, spec);
                return true;
            }
            finally
            {
                Cursor = old;
            }
        }

        internal void LooksChooseEmoji()
        {
            LookTarget t = GetLookTarget();
            if (t == null)
                return;
            Ares.Player.EmojiEntry entry = Ares.Player.EmojiPickerDialog.Pick(this, t.IsFolder ? t.Title : "");
            if (entry == null)
                return;
            if (ApplyEmojiTo(t, entry))
                LooksChanged();
        }

        internal void LooksAutoEmoji()
        {
            LookTarget t = GetLookTarget();
            if (t == null)
                return;
            Ares.Player.EmojiEntry entry = Ares.Player.EmojiCatalog.Match(t.Title);
            if (entry == null)
            {
                MessageBox.Show(this, Tx("Não encontrei um emoji para o nome \"" + t.Title + "\". Escolha um na lista.",
                    "No emoji fits the name \"" + t.Title + "\". Choose one from the list."), "Apolo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                entry = Ares.Player.EmojiPickerDialog.Pick(this, "");
                if (entry == null)
                    return;
            }
            if (ApplyEmojiTo(t, entry))
                LooksChanged();
        }

        internal void LooksChooseColor()
        {
            LookTarget t = GetLookTarget();
            if (t == null)
                return;
            string colorKey = t.IsFolder ? null : t.Element;
            Color color;
            if (!Ares.Player.ColorChoiceDialog.Pick(this,
                    t.IsFolder ? Tx("Cor da pasta \"" + t.Title + "\"", "Color of the folder \"" + t.Title + "\"")
                               : Tx("Cor do botão \"" + t.Title + "\"", "Color of the button \"" + t.Title + "\""),
                    Ares.CommonGUI.ButtonImageStore.GetColor(t.ProjectPath, t.Mode, colorKey), out color))
                return;
            Ares.CommonGUI.ButtonImageStore.SetColor(t.ProjectPath, t.Mode, colorKey, color);
            LooksChanged();
        }

        internal void LooksChooseIcon()
        {
            LookTarget t = GetLookTarget();
            if (t == null)
                return;
            string icon = Ares.Player.IconPickerDialog.Pick(this);
            if (icon == null)
                return;
            Ares.CommonGUI.ButtonImageStore.Set(t.ProjectPath, t.Mode, t.Element, "lucide:" + icon);
            LooksChanged();
        }

        internal void LooksChoosePicture()
        {
            LookTarget t = GetLookTarget();
            if (t == null)
                return;
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = Tx("Escolher imagem", "Choose picture");
                dlg.Filter = Tx("Imagens|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Todos os arquivos|*.*", "Pictures|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files|*.*");
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    string spec = Ares.CommonGUI.ButtonImageStore.ImportImage(t.ProjectPath, dlg.FileName);
                    Ares.CommonGUI.ButtonImageStore.Set(t.ProjectPath, t.Mode, t.Element, spec);
                    LooksChanged();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "Apolo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        internal void LooksSuggestImages()
        {
            LookTarget t = GetLookTarget();
            if (t == null)
                return;
            string temp;
            Ares.Player.MediaHit hit;
            using (Ares.Player.ImageSearchDialog dlg = new Ares.Player.ImageSearchDialog(t.Title))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK || dlg.TempFile == null)
                    return;
                temp = dlg.TempFile;
                hit = dlg.Chosen;
            }
            try
            {
                string spec = Ares.CommonGUI.ButtonImageStore.ImportImage(t.ProjectPath, temp);
                Ares.CommonGUI.ButtonImageStore.Set(t.ProjectPath, t.Mode, t.Element, spec);
                try
                {
                    string dir = Ares.CommonGUI.ButtonImageStore.IconsFolder;
                    string line = spec.Substring(4) + "\t" + hit.Title + "\t" + hit.Author + "\t" + hit.License + "\t" + hit.PageUrl;
                    File.AppendAllText(Path.Combine(dir, "creditos.txt"), line.Replace("\r", " ").Replace("\n", " ") + Environment.NewLine, Encoding.UTF8);
                }
                catch (Exception)
                {
                }
                LooksChanged();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Apolo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                try { File.Delete(temp); } catch (Exception) { }
            }
        }

        internal void LooksRemoveImage()
        {
            LookTarget t = GetLookTarget();
            if (t == null)
                return;
            Ares.CommonGUI.ButtonImageStore.Set(t.ProjectPath, t.Mode, t.Element, null);
            LooksChanged();
        }
    }
}
