using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Ares.Data;

namespace Ares.Player
{
    // The image bank: pictures in the style of the Apolo for the words of tabletop RPG (folder "BancoDeImagens" beside
    // the program: one WebP per picture and the list of words in "indice.txt"). A button gets the picture which fits
    // its name; this can be done by hand or automatically when a button is made.
    public partial class Player
    {
        private ImageBankIndex m_BankIndex;

        private static string BankFolder
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BancoDeImagens"); }
        }

        private static bool AutoBankEnabled
        {
            get { return Ares.CommonGUI.Preferences.GetBool("AutoBankImage", true); }
        }

        private ImageBankIndex Bank
        {
            get
            {
                if (m_BankIndex == null)
                {
                    try
                    {
                        string file = Path.Combine(BankFolder, "indice.txt");
                        m_BankIndex = File.Exists(file) ? ImageBankIndex.Parse(File.ReadAllText(file, System.Text.Encoding.UTF8)) : new ImageBankIndex();
                    }
                    catch (Exception)
                    {
                        m_BankIndex = new ImageBankIndex();
                    }
                }
                return m_BankIndex;
            }
        }

        private void InitBankMenu()
        {
            try
            {
                ToolStripMenuItem bank = new ToolStripMenuItem(T("Banco de imagens", "Image bank"));
                bank.Name = "bankMenuItem";
                ToolStripMenuItem auto = new ToolStripMenuItem(T("Colocar imagem do banco ao criar botões", "Put a picture of the bank on new buttons"));
                auto.Name = "autoBankItem";
                auto.CheckOnClick = true;
                auto.Checked = AutoBankEnabled;
                auto.CheckedChanged += (s, e) => Ares.CommonGUI.Preferences.SetBool("AutoBankImage", auto.Checked);
                ToolStripMenuItem fill = new ToolStripMenuItem(T("Preencher os botões sem imagem desta pasta", "Fill the buttons of this folder which have no picture"));
                fill.Name = "fillBankItem";
                fill.Click += (s, e) => FillFolderFromBank();
                ToolStripMenuItem fillAll = new ToolStripMenuItem(T("Preencher TODOS os botões do projeto sem imagem", "Fill ALL the buttons of the project which have no picture"));
                fillAll.Name = "fillAllBankItem";
                fillAll.Click += (s, e) => FillProjectFromBank();
                bank.DropDownItems.Add(fill);
                bank.DropDownItems.Add(fillAll);
                bank.DropDownItems.Add(new ToolStripSeparator());
                bank.DropDownItems.Add(auto);
                extrasToolStripMenuItem.DropDownItems.Insert(2, bank);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }

        // copies the picture of the bank (WebP) into the icons folder of Apolo as a PNG; returns the setting for it
        private string ImportBankPicture(BankEntry entry)
        {
            try
            {
                string name = "banco-" + entry.Id + ".png";
                string target = Path.Combine(Ares.CommonGUI.ButtonImageStore.IconsFolder, name);
                if (!File.Exists(target))
                {
                    byte[] png = Ares.CommonGUI.ImageDecoder.ToReadable(File.ReadAllBytes(Path.Combine(BankFolder, entry.Id + ".webp")));
                    if (png == null)
                        return null;
                    File.WriteAllBytes(target, png);
                }
                return "lib:" + name;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private bool ApplyBankPicture(string modeTitle, string elementTitle, BankEntry entry)
        {
            if (string.IsNullOrEmpty(m_CurrentProjectPath))
                return false;
            string spec = ImportBankPicture(entry);
            if (spec == null)
                return false;
            Ares.CommonGUI.ButtonImageStore.Set(m_CurrentProjectPath, modeTitle, elementTitle, spec);
            return true;
        }

        /// <summary>After buttons (and folders) were made: the ones without a picture get the one which fits their name.</summary>
        private void AutoBankAfterCreate(IEnumerable<KeyValuePair<string, string>> buttons, IEnumerable<string> folders)
        {
            if (!AutoBankEnabled || Bank.Entries.Count == 0 || string.IsNullOrEmpty(m_CurrentProjectPath))
                return;
            bool any = false;
            try
            {
                foreach (KeyValuePair<string, string> button in buttons)
                {
                    if (Ares.CommonGUI.ButtonImageStore.Get(m_CurrentProjectPath, button.Key, button.Value) != null)
                        continue;
                    BankEntry entry = Bank.Match(button.Value);
                    if (entry != null && ApplyBankPicture(button.Key, button.Value, entry))
                        any = true;
                }
                if (folders != null)
                {
                    foreach (string folder in folders)
                    {
                        if (Ares.CommonGUI.ButtonImageStore.Get(m_CurrentProjectPath, folder, "") != null)
                            continue;
                        BankEntry entry = Bank.Match(folder);
                        if (entry != null && ApplyBankPicture(folder, "", entry))
                            any = true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
            if (any)
            {
                UpdateElementsPanel();
                modesList.Invalidate();
            }
        }

        // right click on a button > picture of the bank which fits its name
        private void AutoBankForButton()
        {
            IMode mode; int index;
            IModeElement me = MenuElement(out mode, out index);
            if (me == null || string.IsNullOrEmpty(m_CurrentProjectPath))
                return;
            if (Bank.Entries.Count == 0)
            {
                MessageBox.Show(this, T("O banco de imagens não foi encontrado nesta instalação.", "The image bank was not found in this installation."),
                    StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            BankEntry entry = Bank.Match(me.Title);
            if (entry == null)
            {
                MessageBox.Show(this, T("Não achei no banco uma imagem para o nome \"" + me.Title + "\". Escolha uma na lista.",
                    "No picture of the bank fits the name \"" + me.Title + "\". Choose one from the list."),
                    StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Information);
                entry = BankPickerDialog.Pick(this, Bank, me.Title);
                if (entry == null)
                    return;
            }
            if (ApplyBankPicture(mode.Title, me.Title, entry))
                UpdateElementsPanel();
        }

        // right click on a button > choose from the bank
        private void ChooseBankForButton()
        {
            IMode mode; int index;
            IModeElement me = MenuElement(out mode, out index);
            if (me == null || string.IsNullOrEmpty(m_CurrentProjectPath))
                return;
            if (Bank.Entries.Count == 0)
            {
                MessageBox.Show(this, T("O banco de imagens não foi encontrado nesta instalação.", "The image bank was not found in this installation."),
                    StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            BankEntry entry = BankPickerDialog.Pick(this, Bank, me.Title);
            if (entry != null && ApplyBankPicture(mode.Title, me.Title, entry))
                UpdateElementsPanel();
        }

        // Extras > fills every button (and folder) of the project which has no picture yet
        private void FillProjectFromBank()
        {
            if (m_Project == null || string.IsNullOrEmpty(m_CurrentProjectPath))
                return;
            if (Bank.Entries.Count == 0)
            {
                MessageBox.Show(this, T("O banco de imagens não foi encontrado nesta instalação.", "The image bank was not found in this installation."),
                    StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            int done = 0, without = 0;
            foreach (IMode mode in m_Project.GetModes())
            {
                if (Ares.CommonGUI.ButtonImageStore.Get(m_CurrentProjectPath, mode.Title, "") == null)
                {
                    BankEntry folderEntry = Bank.Match(mode.Title);
                    if (folderEntry != null && ApplyBankPicture(mode.Title, "", folderEntry))
                        done++;
                }
                foreach (IModeElement me in mode.GetElements())
                {
                    if (Ares.CommonGUI.ButtonImageStore.Get(m_CurrentProjectPath, mode.Title, me.Title) != null)
                        continue;
                    BankEntry entry = Bank.Match(me.Title);
                    if (entry != null && ApplyBankPicture(mode.Title, me.Title, entry))
                        done++;
                    else
                        without++;
                }
            }
            UpdateElementsPanel();
            modesList.Invalidate();
            ShowToast(new Ares.Players.Message(Ares.Players.MessageType.Info,
                T(done + " imagem(ns) do banco aplicada(s); " + without + " botão(ões) sem imagem que combine com o nome.",
                  done + " picture(s) of the bank applied; " + without + " button(s) without a picture that fits the name.")));
        }

        // Extras > fills the buttons of the open folder (and of its subfolders under headings) which have no picture
        private void FillFolderFromBank()
        {
            IMode shown = CurrentMode();
            if (shown == null || m_Project == null || string.IsNullOrEmpty(m_CurrentProjectPath))
                return;
            if (Bank.Entries.Count == 0)
            {
                MessageBox.Show(this, T("O banco de imagens não foi encontrado nesta instalação.", "The image bank was not found in this installation."),
                    StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            int done = 0, without = 0;
            foreach (ShownButton button in CollectShownButtons(shown))
            {
                if (Ares.CommonGUI.ButtonImageStore.Get(m_CurrentProjectPath, button.OwnerTitle, button.Element.Title) != null)
                    continue;
                BankEntry entry = Bank.Match(button.Element.Title);
                if (entry != null && ApplyBankPicture(button.OwnerTitle, button.Element.Title, entry))
                    done++;
                else
                    without++;
            }
            UpdateElementsPanel();
            ShowToast(new Ares.Players.Message(Ares.Players.MessageType.Info,
                T(done + " botão(ões) receberam imagem do banco; " + without + " sem imagem que combine com o nome.",
                  done + " button(s) got a picture of the bank; " + without + " without a picture that fits the name.")));
        }
    }

    /// <summary>Window to choose a picture of the image bank: small pictures with a search box.</summary>
    internal class BankPickerDialog : Form
    {
        private readonly ImageBankIndex m_Index;
        private readonly TextBox m_Search = new TextBox();
        private readonly ListView m_List = new ListView();
        private readonly ImageList m_Images = new ImageList();
        private readonly Dictionary<string, int> m_ImageOf = new Dictionary<string, int>();

        public static BankEntry Pick(IWin32Window owner, ImageBankIndex index, string search)
        {
            using (BankPickerDialog dlg = new BankPickerDialog(index, search))
            {
                if (dlg.ShowDialog(owner) != DialogResult.OK || dlg.m_List.SelectedItems.Count == 0)
                    return null;
                return dlg.m_List.SelectedItems[0].Tag as BankEntry;
            }
        }

        private BankPickerDialog(ImageBankIndex index, string search)
        {
            m_Index = index;
            Text = Loc.T("Banco de imagens", "Image bank");
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(680, 520);
            MinimumSize = new Size(480, 360);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            MinimizeBox = false;

            Label hint = new Label();
            hint.SetBounds(12, 12, 656, 20);
            hint.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            hint.Text = Loc.T("Procure por uma palavra (floresta, dragão, taverna...) e dê dois cliques na imagem.", "Search for a word (forest, dragon, tavern...) and double click the picture.");
            Controls.Add(hint);

            m_Search.SetBounds(12, 36, 656, 24);
            m_Search.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            m_Search.Text = "";
            m_Search.TextChanged += (s, e) => Fill();
            Controls.Add(m_Search);

            m_Images.ImageSize = new Size(96, 96);
            m_Images.ColorDepth = ColorDepth.Depth32Bit;
            m_List.SetBounds(12, 68, 656, 396);
            m_List.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            m_List.View = View.LargeIcon;
            m_List.LargeImageList = m_Images;
            m_List.MultiSelect = false;
            m_List.HideSelection = false;
            m_List.DoubleClick += (s, e) =>
            {
                if (m_List.SelectedItems.Count > 0)
                    DialogResult = DialogResult.OK;
            };
            Controls.Add(m_List);

            Button ok = new Button();
            ok.Text = Loc.T("Usar esta imagem", "Use this picture");
            ok.SetBounds(ClientSize.Width - 12 - 120 - 8 - 90, 476, 120, 30);
            ok.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            ok.DialogResult = DialogResult.OK;
            Controls.Add(ok);
            Button cancel = new Button();
            cancel.Text = Loc.T("Cancelar", "Cancel");
            cancel.SetBounds(ClientSize.Width - 12 - 90, 476, 90, 30);
            cancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(cancel);
            CancelButton = cancel;

            Shown += (s, e) =>
            {
                m_Search.Text = search ?? "";
                if (Fill() == 0 && m_Search.Text.Length > 0)
                    m_Search.Text = "";
                m_Search.Focus();
                m_Search.SelectAll();
            };
        }

        private int Fill()
        {
            List<BankEntry> found = m_Index.Search(m_Search.Text);
            m_List.BeginUpdate();
            m_List.Items.Clear();
            foreach (BankEntry entry in found)
            {
                int image;
                if (!m_ImageOf.TryGetValue(entry.Id, out image))
                {
                    Bitmap bmp = Ares.CommonGUI.ImageDecoder.LoadBitmap(Path.Combine(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BancoDeImagens"), entry.Id + ".webp"));
                    if (bmp == null)
                    {
                        m_ImageOf[entry.Id] = -1;
                        continue;
                    }
                    m_Images.Images.Add(bmp);
                    image = m_Images.Images.Count - 1;
                    m_ImageOf[entry.Id] = image;
                }
                if (image < 0)
                    continue;
                ListViewItem item = new ListViewItem(entry.Label, image);
                item.Tag = entry;
                item.ToolTipText = string.Join(", ", entry.Words);
                m_List.Items.Add(item);
            }
            m_List.EndUpdate();
            if (m_List.Items.Count > 0)
                m_List.Items[0].Selected = true;
            return m_List.Items.Count;
        }
    }
}
