/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 What the editor area shows when a folder is clicked: its playlists with their type.
 A double click on a line opens the playlist.
*/
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Ares.Data;

namespace Ares.Editor
{
    class ModeOverviewWindow : WeifenLuo.WinFormsUI.Docking.DockContent
    {
        private readonly ListView m_List;
        private readonly Label m_Title;
        private IMode m_Mode;
        private string m_ModeTitle;
        private string m_ProjectPath;
        private readonly ImageList m_Images = new ImageList();

        /// <summary>Called with the playlist the user wants to open.</summary>
        public System.Action<IModeElement> OpenRequested;

        public ModeOverviewWindow()
        {
            DockAreas = WeifenLuo.WinFormsUI.Docking.DockAreas.Document;
            Text = ProjectExplorer.Tx("Pasta", "Folder");
            m_Title = new Label();
            m_Title.Dock = DockStyle.Top;
            m_Title.Height = 34;
            m_Title.Padding = new Padding(8, 8, 8, 0);
            m_Title.Font = new Font(Font.FontFamily, Font.Size + 2, FontStyle.Bold);
            m_List = new ListView();
            m_List.Dock = DockStyle.Fill;
            m_List.View = View.Details;
            m_List.FullRowSelect = true;
            m_List.HideSelection = false;
            m_List.MultiSelect = false;
            m_List.BorderStyle = BorderStyle.None;
            m_Images.ImageSize = new Size(24, 24);
            m_Images.ColorDepth = ColorDepth.Depth32Bit;
            m_List.SmallImageList = m_Images;
            m_List.Columns.Add(ProjectExplorer.Tx("Playlist", "Playlist"), 280);
            m_List.Columns.Add(ProjectExplorer.Tx("Tipo", "Type"), 240);
            m_List.Columns.Add(ProjectExplorer.Tx("Itens", "Items"), 70);
            m_List.DoubleClick += (s, e) => OpenSelected();
            m_List.KeyDown += (s, e) =>
            {
                if (e.KeyCode == System.Windows.Forms.Keys.Enter)
                    OpenSelected();
            };
            Controls.Add(m_List);
            Controls.Add(m_Title);
            ClientSize = new Size(640, 360);
        }

        protected override string GetPersistString()
        {
            return "ModeOverview";
        }

        private void OpenSelected()
        {
            if (m_List.SelectedItems.Count == 0 || OpenRequested == null)
                return;
            IModeElement element = m_List.SelectedItems[0].Tag as IModeElement;
            if (element != null)
                OpenRequested(element);
        }

        public void SetMode(IMode mode, string title, string projectPath)
        {
            m_ProjectPath = projectPath;
            m_Mode = mode;
            m_ModeTitle = title;
            Reload();
        }

        public void Reload()
        {
            if (m_Mode == null)
                return;
            m_Title.Text = m_ModeTitle;
            Text = m_ModeTitle;
            m_List.BeginUpdate();
            try
            {
                m_List.Items.Clear();
                m_Images.Images.Clear();
                foreach (IModeElement element in m_Mode.GetElements())
                {
                    IElement inner = element.StartElement;
                    ListViewItem item = new ListViewItem(element.Title);
                    item.SubItems.Add(TypeName(inner));
                    item.SubItems.Add(CountOf(inner));
                    item.Tag = element;
                    DecorateItem(item, element.Title);
                    m_List.Items.Add(item);
                }
                if (m_List.Items.Count == 0)
                {
                    ListViewItem item = new ListViewItem(ProjectExplorer.Tx("(vazia: use o botão Adicionar)", "(empty: use the Add button)"));
                    item.ForeColor = SystemColors.GrayText;
                    m_List.Items.Add(item);
                }
            }
            finally
            {
                m_List.EndUpdate();
            }
        }

        private void DecorateItem(ListViewItem item, string elementTitle)
        {
            if (string.IsNullOrEmpty(m_ProjectPath) || m_Mode == null)
                return;
            try
            {
                string modeTitle = m_Mode.Title;
                string spec = Ares.CommonGUI.ButtonImageStore.Get(m_ProjectPath, modeTitle, elementTitle);
                Bitmap bmp = null;
                if (spec != null)
                {
                    if (spec.StartsWith("lucide:"))
                        bmp = Ares.CommonGUI.IconLibrary.Get(spec.Substring(7), new Size(24, 24), Ares.CommonGUI.IconLibrary.ThemeColor());
                    else
                        bmp = Ares.CommonGUI.ButtonImageStore.LoadImage(m_ProjectPath, spec);
                }
                if (bmp != null)
                {
                    m_Images.Images.Add(bmp);
                    item.ImageIndex = m_Images.Images.Count - 1;
                }
                Color color = Ares.CommonGUI.ButtonImageStore.GetColor(m_ProjectPath, modeTitle, elementTitle);
                if (!color.IsEmpty)
                {
                    Color back = m_List.BackColor;
                    item.UseItemStyleForSubItems = true;
                    item.BackColor = Color.FromArgb((back.R * 3 + color.R) / 4, (back.G * 3 + color.G) / 4, (back.B * 3 + color.B) / 4);
                }
            }
            catch (Exception)
            {
            }
        }

        private static string CountOf(IElement inner)
        {
            try
            {
                IGeneralElementContainer container = inner as IGeneralElementContainer;
                if (container != null)
                    return container.GetGeneralElements().Count.ToString();
            }
            catch (Exception)
            {
            }
            return "";
        }

        public static string TypeName(IElement element)
        {
            if (element is IRandomBackgroundMusicList)
                return ProjectExplorer.Tx("Lista de músicas aleatória", "Random music list");
            if (element is ISequentialBackgroundMusicList)
                return ProjectExplorer.Tx("Lista de músicas sequencial", "Sequential music list");
            if (element is IBackgroundSounds || element is IBackgroundSoundChoice)
                return ProjectExplorer.Tx("Sons de fundo", "Background sounds");
            if (element is IMacro)
                return "Macro";
            if (element is IElementContainer<IChoiceElement>)
                return ProjectExplorer.Tx("Lista aleatória", "Random list");
            if (element is ISequentialContainer)
                return ProjectExplorer.Tx("Lista sequencial", "Sequential list");
            if (element is IElementContainer<IParallelElement>)
                return ProjectExplorer.Tx("Lista paralela", "Parallel list");
            if (element is IMusicByTags)
                return ProjectExplorer.Tx("Música por tags", "Music by tags");
            if (element is IFileElement)
                return ProjectExplorer.Tx("Arquivo de som", "Sound file");
            return "";
        }
    }
}
