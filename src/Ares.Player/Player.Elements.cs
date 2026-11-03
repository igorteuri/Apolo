using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Ares.Data;
using Ares.Players;
using Ares.Settings;
using Ares.CommonGUI;

namespace Ares.Player
{
    // The list of the folders (modes) and the panel of the buttons (cards / list) of the open folder.
    public partial class Player
    {
        private void UpdateModesList()
        {
            ClearModeIcons();
            foreach (CheckBox box in m_CurrentButtons.Keys)
            {
                box.CheckedChanged -= m_CurrentButtons[box];
            }
            m_CurrentButtons.Clear();
            foreach (CheckBox box in m_CurrentTagButtons.Keys)
            {
                box.CheckedChanged -= m_CurrentTagButtons[box];
            }
            m_ButtonsForIds.Clear();

            modesList.BeginUpdate();
            modesList.Items.Clear();
            m_ModeRows.Clear();
            m_Listen = false;
            if (m_Project != null)
            {
                IList<Ares.Data.IMode> modes = m_Project.GetModes();
                if (modes.Count == 0)
                {
                    modesList.EndUpdate();
                    m_Listen = true;
                    return;
                }
                EnsureModeTree();
                string currentTitle = m_PlayingControl.CurrentMode != null ? m_PlayingControl.CurrentMode.Title : null;
                // after an edit / undo the folder which was shown stays (the player itself would go back to the first one)
                bool keepShown = m_PreferredModeTitle != null && ModeIndexByTitle(m_PreferredModeTitle) >= 0;
                if (keepShown)
                    currentTitle = m_PreferredModeTitle;
                ModeNode currentNode = m_ModeTree.Find(currentTitle);
                if (currentNode != null && ExpandAncestors(currentNode))
                    SaveModeTree();
                RebuildModeRows();
                int currentRow = RowOfNode(currentNode);
                if (currentRow < 0 && m_ModeRows.Count > 0)
                    currentRow = 0;
                modesList.SelectedIndex = currentRow;
                if (keepShown && currentRow >= 0)
                {
                    int shownIndex = ModeIndexByTitle(m_PreferredModeTitle);
                    if (shownIndex >= 0)
                        m_PlayingControl.SetMode(m_Project.GetModes()[shownIndex]);
                }
            }
            modesList.EndUpdate();
            UpdateElementsPanel();
            m_Listen = true;
        }

        private Dictionary<CheckBox, EventHandler> m_CurrentButtons = new Dictionary<CheckBox, EventHandler>();
        private Dictionary<CheckBox, EventHandler> m_CurrentTagButtons = new Dictionary<CheckBox, EventHandler>();
        private Dictionary<int, CheckBox> m_ButtonsForIds = new Dictionary<int, CheckBox>();
        private bool m_ButtonsActive = true;
        private bool m_TagsControlsActive = false;

        private int m_TagLanguageId;
        private int m_LastTagCategoryId = -1;
        IList<Ares.Tags.CategoryForLanguage> m_MusicTagCategories;
        
        /// <summary>Width a button needs for its text with the font really in use (themes and zoom change the font after the buttons were first measured).</summary>
        private static int MeasureButtonWidth(string text, Font font)
        {
            Size s = TextRenderer.MeasureText(text, font, new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            return s.Width + (int)Math.Ceiling(font.Size * 3.2f) + 8;
        }

        private void UpdateElementsPanel()
        {
            try
            {
                DoUpdateElementsPanel();
            }
            finally
            {
                // whatever happened, the panel must be drawn again
                ResumeRedraw(elementsPanel);
            }
        }

        private void DoUpdateElementsPanel()
        {
            int modeIndex = SelectedModeIndex;
            if (modeIndex == -1)
                return;
            if (SpecialViews && modeIndex == 0)
            {
                musicList.BeginUpdate();
                musicList.Items.Clear();
                Ares.Data.IElement element = Ares.Data.DataModule.ElementRepository.GetElement(m_PlayingControl.CurrentMusicList);
                if (element != null && element is Ares.Data.IMusicList)
                {
                    Ares.Data.IMusicList currentMusicList = (Ares.Data.IMusicList)element;
                    var fileElements = currentMusicList.GetFileElements();
                    int selIndex = -1;
                    for (int i = 0; i < fileElements.Count; ++i)
                    {
                        musicList.Items.Add(fileElements[i].Title);
                        if (fileElements[i].Id == m_PlayingControl.CurrentMusicElement)
                            selIndex = i;
                    }
                    if (selIndex != -1)
                        musicList.SelectedIndex = selIndex;
                }
                musicList.EndUpdate();
                elementsPanel.Visible = false;
                tagsPanel.Visible = false;
                m_ButtonsActive = false;
                m_TagsControlsActive = false;
                musicList.Visible = true;
            }
            else if (SpecialViews && modeIndex == 1)
            {
                foreach (CheckBox box in m_CurrentTagButtons.Keys)
                {
                    box.CheckedChanged -= m_CurrentTagButtons[box];
                }
                m_CurrentTagButtons.Clear();

                m_TagsControlsActive = false;
                Ares.Tags.ITagsDBReadByLanguage dbRead = Ares.Tags.TagsModule.GetTagsDB().GetReadInterfaceByLanguage(m_TagLanguageId);

                musicTagCategoryBox.Items.Clear();
                int selIndex = -1;
                try
                {
                    m_MusicTagCategories = dbRead.GetAllCategories();
                }
                catch (Ares.Tags.TagsDbException ex)
                {
                    Messages.AddMessage(MessageType.Error, String.Format(StringResources.TagsDbError, ex.Message));
                    m_MusicTagCategories = new List<Ares.Tags.CategoryForLanguage>();
                }
                if (m_Project != null && m_Project.GetHiddenTagCategories().Count > 0)
                {
                    HashSet<int> hiddenCategories = m_Project.GetHiddenTagCategories();
                    List<Ares.Tags.CategoryForLanguage> filteredCategories = new List<Tags.CategoryForLanguage>();
                    foreach (var category in m_MusicTagCategories)
                    {
                        if (!hiddenCategories.Contains(category.Id))
                            filteredCategories.Add(category);
                    }
                    m_MusicTagCategories = filteredCategories;
                }
                foreach (var category in m_MusicTagCategories)
                {
                    musicTagCategoryBox.Items.Add(category.Name);
                    if (category.Id == m_LastTagCategoryId)
                        selIndex = musicTagCategoryBox.Items.Count - 1;
                }
                if (selIndex == -1 && musicTagCategoryBox.Items.Count > 0)
                {
                    selIndex = 0;
                    m_LastTagCategoryId = m_MusicTagCategories[0].Id;
                }
                if (selIndex != -1)
                {
                    musicTagCategoryBox.SelectedIndex = selIndex;
                }
                if (musicTagCategoryBox.Items.Count == 0)
                    m_LastTagCategoryId = -1;

                HashSet<int> currentTags = m_PlayingControl.GetCurrentMusicTags();

                tagSelectionPanel.Controls.Clear();
                if (m_LastTagCategoryId != -1)
                {
                    IList<Ares.Tags.TagForLanguage> tags;
                    try 
                    {
                        tags = dbRead.GetAllTags(m_LastTagCategoryId);
                    }
                    catch (Ares.Tags.TagsDbException ex)
                    {
                        Messages.AddMessage(MessageType.Error, String.Format(StringResources.TagsDbError, ex.Message));
                        tags = new List<Ares.Tags.TagForLanguage>();
                    }
                    if (m_Project != null && m_Project.GetHiddenTags().Count > 0)
                    {
                        HashSet<int> hiddenTags = m_Project.GetHiddenTags();
                        List<Tags.TagForLanguage> filteredTags = new List<Tags.TagForLanguage>();
                        foreach (var tag in tags)
                        {
                            if (!hiddenTags.Contains(tag.Id))
                                filteredTags.Add(tag);
                        }
                        tags = filteredTags;
                    }

                    int maxWidth = 0;
                    CheckBox checkBox = new CheckBox();
                    checkBox.Appearance = Appearance.Button;
                    for (int i = 0; i < tags.Count; ++i)
                    {
                        String text = tags[i].Name;
                        int width = MeasureButtonWidth(text, tagSelectionPanel.Font);
                        if (width > maxWidth)
                            maxWidth = width;
                    }
                    checkBox.Dispose();
                    int count = 0;
                    for (int i = 0; i < tags.Count; ++i)
                    {
                        ++count;
                        int row = count / 4;
                        int column = count % 4;
                        checkBox = new CheckBox();
                        String text = tags[i].Name;
                        checkBox.Text = text;
                        checkBox.Appearance = Appearance.Button;
                        checkBox.Font = tagSelectionPanel.Font;
                        checkBox.AutoEllipsis = false;
                        checkBox.TextAlign = ContentAlignment.MiddleCenter;
                        Size size = checkBox.PreferredSize;
                        size.Width = maxWidth;
                        checkBox.SetBounds(0, 0, size.Width, size.Height);
                        checkBox.Checked = currentTags.Contains(tags[i].Id);
                        int tagId = tags[i].Id;
                        int categoryId = m_LastTagCategoryId;
                        EventHandler handler = new EventHandler((Object sender, EventArgs args) =>
                        {
                            if (m_Listen && m_TagsControlsActive)
                            {
                                if ((sender as CheckBox).Checked)
                                {
                                    m_PlayingControl.AddMusicTag(categoryId, tagId);
                                }
                                else
                                {
                                    m_PlayingControl.RemoveMusicTag(categoryId, tagId);
                                }
                            }
                        });
                        checkBox.CheckedChanged += handler;
                        m_CurrentTagButtons.Add(checkBox, handler);
                        tagSelectionPanel.Controls.Add(checkBox);
                    }
                    if (count == 0)
                    {
                        Label label = new Label();
                        label.Text = StringResources.NoTags;
                        label.SetBounds(0, 0, label.PreferredSize.Width, label.PreferredSize.Height);
                        tagSelectionPanel.Controls.Add(label);
                    }
                }

                if (m_UpdateCategoryCombinationBox)
                {
                    tagCategoryCombinationBox.SelectedIndex = (int)m_PlayingControl.GetMusicTagCategoriesCombination();
                }

                StringBuilder currentTagsBuilder = new StringBuilder();
                IList<Ares.Tags.TagInfoForLanguage> currentTagInfos;
                try 
                { 
                    currentTagInfos = dbRead.GetTagInfos(currentTags);
                }
                catch (Ares.Tags.TagsDbException ex)
                {
                    Messages.AddMessage(MessageType.Error, String.Format(StringResources.TagsDbError, ex.Message));
                    currentTagInfos = new List<Ares.Tags.TagInfoForLanguage>();
                }
                int catId = -1;
                foreach (var tagInfo in currentTagInfos)
                {
                    if (tagInfo.CategoryId != catId)
                    {
                        if (currentTagsBuilder.Length > 0)
                        {
                            currentTagsBuilder.Append("; ");
                        }
                        currentTagsBuilder.Append(tagInfo.Category);
                        currentTagsBuilder.Append(": ");
                        catId = tagInfo.CategoryId;
                    }
                    else
                    {
                        currentTagsBuilder.Append(", ");
                    }
                    currentTagsBuilder.Append(tagInfo.Name);
                }
                currentTagsLabel.Text = currentTagsBuilder.ToString();

                elementsPanel.Visible = false;
                tagsPanel.Visible = true;
                m_TagsControlsActive = true;
                m_ButtonsActive = false;
                musicList.Visible = false;
            }
            else
            {
                foreach (CheckBox box in m_CurrentButtons.Keys)
                {
                    box.CheckedChanged -= m_CurrentButtons[box];
                }
                m_CurrentButtons.Clear();
                m_ButtonsForIds.Clear();
                m_ElementOwners.Clear();
                // thousands of buttons: nothing is laid out or drawn while they are made (it is done once at the end)
                SuspendRedraw(elementsPanel);
                elementsPanel.SuspendLayout();
                elementsPanel.Controls.Clear();

                if (m_Project != null && m_Project.GetModes().Count > modeIndex)
                {
                    bool showKeys = Settings.Settings.Instance.ShowKeysInButtons;
                    Ares.Data.IMode shownMode = m_Project.GetModes()[modeIndex];
                    // the buttons of the folder, then (under a heading each) the ones of its subfolders
                    List<ShownButton> shown = CollectShownButtons(shownMode);
                    // a big folder (or a mother folder of many) shows the first buttons; the rest comes on demand
                    if (shownMode.Title != m_ShownLimitFolder)
                    {
                        m_ShownLimitFolder = shownMode.Title;
                        m_ShownLimit = ShownLimitStep;
                    }
                    List<CollapsedGroup> collapsedGroups;
                    int hiddenInFirst;
                    shown = LimitShown(shown, m_ShownLimit, out collapsedGroups, out hiddenInFirst);
                    List<Ares.Data.IModeElement> elements = new List<Ares.Data.IModeElement>();
                    foreach (ShownButton sb in shown)
                        elements.Add(sb.Element);
                    // text, picture / icon of every visible button
                    List<Ares.Data.IModeElement> visible = new List<Ares.Data.IModeElement>();
                    List<string> texts = new List<string>();
                    List<string> specs = new List<string>();
                    List<int> kinds = new List<int>();
                    List<string> keyTexts = new List<string>();
                    for (int i = 0; i < elements.Count; ++i)
                    {
                        String text = elements[i].Title;
                        string keyText = null;
                        if (showKeys && elements[i].Trigger != null && elements[i].Trigger.TriggerType == TriggerType.Key)
                        {
                            int keyCode = (elements[i].Trigger as IKeyTrigger).KeyCode;
                            if (keyCode != 0)
                                keyText = KeyText((System.Windows.Forms.Keys)keyCode);
                        }
                        keyTexts.Add(keyText);
                        // room for the shortcut card below the name
                        if (keyText != null)
                            text += "\n ";
                        string spec = Ares.CommonGUI.ButtonImageStore.Get(m_CurrentProjectPath, shown[i].OwnerTitle, elements[i].Title);
                        int kind = 0;
                        if (spec != null && (spec.StartsWith("img:") || spec.StartsWith("lib:")) && Ares.CommonGUI.ButtonImageStore.LoadImage(m_CurrentProjectPath, spec) != null)
                            kind = 2;
                        else if (spec != null && spec.StartsWith("lucide:"))
                            kind = 1;
                        visible.Add(elements[i]);
                        texts.Add(text);
                        specs.Add(spec);
                        kinds.Add(kind);
                    }
                    Size cardSize = ElementButton.ComputeCardSize(texts, kinds, elementsPanel.Font);
                    if (cardSize.Height != m_ShownCardHeight)
                    {
                        // the room kept for the buttons depends on the size of the cards
                        m_ShownCardHeight = cardSize.Height;
                        m_CardHeightChanged = true;
                    }
                    int count = 0;
                    for (int i = 0; i < visible.Count; ++i)
                    {
                        ++count;
                        Ares.Data.IModeElement element = visible[i];
                        if (shown[i].Header != null)
                            elementsPanel.Controls.Add(MakeGroupHeader(shown[i].Header, shown[i].OwnerTitle));
                        ElementButton checkBox = new ElementButton();
                        checkBox.Font = elementsPanel.Font;
                        checkBox.Text = elements[i].Title;
                        checkBox.KeyText = keyTexts[i];
                        if (kinds[i] == 2)
                            checkBox.Picture = Ares.CommonGUI.ButtonImageStore.LoadImage(m_CurrentProjectPath, specs[i]);
                        else if (kinds[i] == 1)
                            checkBox.IconName = specs[i].Substring(7);
                        if (kinds[i] == 2 && specs[i].IndexOf("emoji-", StringComparison.OrdinalIgnoreCase) >= 0)
                            checkBox.PictureContain = true;
                        checkBox.TintColor = Ares.CommonGUI.ButtonImageStore.GetColor(m_CurrentProjectPath, shown[i].OwnerTitle, visible[i].Title);
                        checkBox.Kind = KindsOf(element.StartElement, 0);
                        checkBox.BadgeClickable = m_EditMode;
                        int badgeId = element.Id;
                        checkBox.KindBadgeClicked += (Object, EventArgs) => RunAfterDrag(() => ToggleButtonKind(badgeId));
                        checkBox.SetBounds(0, 0, cardSize.Width, cardSize.Height);
                        checkBox.Checked = m_PlayingControl.CurrentModeElements.Contains(element);
                        int id = element.Id;
                        EventHandler handler = new EventHandler((Object, EventArgs) =>
                        {
                            if (m_Listen && m_ButtonsActive)
                            {
                                m_PlayingControl.SwitchElement(id);
                                ShowTrackListOfClicked(id);
                            }
                        });
                        checkBox.CheckedChanged += handler;
                        m_CurrentButtons.Add(checkBox, handler);
                        m_ButtonsForIds.Add(id, checkBox);
                        m_ElementOwners[id] = shown[i].OwnerTitle;
                        AttachElementMenu(checkBox, id);
                        elementsPanel.Controls.Add(checkBox);
                    }
                    AddLimitLines(collapsedGroups, hiddenInFirst);
                    if (count == 0 && collapsedGroups.Count == 0)
                    {
                        Label label = new Label();
                        label.Text = StringResources.NoElements;
                        label.SetBounds(0, 0, label.PreferredSize.Width, label.PreferredSize.Height);
                        elementsPanel.Controls.Add(label);
                    }
                }
                else
                {
                    Label label = new Label();
                    label.Text = StringResources.NoElements;
                    label.SetBounds(0, 0, label.PreferredSize.Width, label.PreferredSize.Height);
                    elementsPanel.Controls.Add(label);
                }
                AfterElementsBuilt(modeIndex);
                m_ButtonsActive = true;
                musicList.Visible = false;
                tagsPanel.Visible = false;
                m_TagsControlsActive = false;
                elementsPanel.Visible = true;
                elementsPanel.ResumeLayout(true);
                if (m_CardHeightChanged)
                {
                    // the room kept for the buttons depends on the size of the cards: done now, while nothing is drawn yet
                    m_CardHeightChanged = false;
                    LayoutMainColumns();
                }
                ResumeRedraw(elementsPanel);
            }
        }

        private bool m_CardHeightChanged;
    }
}
