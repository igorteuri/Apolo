using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Ares.Data;
using Keys = System.Windows.Forms.Keys;

namespace Ares.Player
{
    // Keyboard shortcuts: the ones of the program (panels, edit mode, tabs, playback) can be changed, and keys can be
    // given to folders and playlists. One window shows all of them.
    public partial class Player
    {
        private class ProgramShortcut
        {
            public string Id;
            public string Pt, En;
            public Keys Default;
            public Keys Current;
            public Action Run;
        }

        private readonly List<ProgramShortcut> m_Shortcuts = new List<ProgramShortcut>();

        private static Keys ReadShortcut(string id, Keys defaultKey)
        {
            string text = Ares.CommonGUI.Preferences.Get("Key." + id, null);
            int value;
            if (text != null && int.TryParse(text, out value))
                return (Keys)value;
            return defaultKey;
        }

        private static readonly Dictionary<string, Keys> s_ShortcutDefaults = new Dictionary<string, Keys>
        {
            { "left", Keys.F8 },
            { "tracks", Keys.F9 },
            { "edit", Keys.Control | Keys.E },
            { "home", Keys.None },
            { "nexttab", Keys.Control | Keys.Tab },
            { "prevtab", Keys.Control | Keys.Shift | Keys.Tab },
            { "pause", Keys.None },
            { "stop", Keys.None },
            { "previous", Keys.None },
            { "next", Keys.None },
            { "repeat", Keys.None }
        };

        /// <summary>Text of a program shortcut for tooltips and menus ("F9"), or "" when none is set.</summary>
        internal static string ShortcutText(string id)
        {
            Keys defaultKey;
            s_ShortcutDefaults.TryGetValue(id, out defaultKey);
            return KeyText(ReadShortcut(id, defaultKey));
        }

        internal static string KeyText(Keys key)
        {
            if (key == Keys.None)
                return "";
            try
            {
                return new KeysConverter().ConvertToString(null, CultureInfo.InvariantCulture, key);
            }
            catch (Exception)
            {
                return key.ToString();
            }
        }

        private void InitShortcuts()
        {
            Action<string, string, string, Action> add = (id, pt, en, run) =>
            {
                Keys def;
                s_ShortcutDefaults.TryGetValue(id, out def);
                m_Shortcuts.Add(new ProgramShortcut { Id = id, Pt = pt, En = en, Default = def, Current = ReadShortcut(id, def), Run = run });
            };
            add("left", "Mostrar / esconder o painel esquerdo", "Show / hide the left panel", () => SetLeftOpen(!m_LeftOpen));
            add("tracks", "Mostrar / esconder a lista de músicas", "Show / hide the track list", ToggleTrackPanel);
            add("edit", "Ligar / desligar o modo de edição", "Turn edit mode on / off", () => { if (m_EditButton != null) m_EditButton.Checked = !m_EditButton.Checked; });
            add("home", "Ir para a aba Apolo (tela inicial)", "Go to the Apolo tab (home)", ShowHome);
            add("nexttab", "Próxima aba", "Next tab", () => CycleTab(1));
            add("prevtab", "Aba anterior", "Previous tab", () => CycleTab(-1));
            add("pause", "Pausar / continuar", "Pause / resume", () => { if (m_PauseButton != null) m_PauseButton.PerformClick(); });
            add("stop", "Parar tudo", "Stop all", () => stopButton_Click(this, EventArgs.Empty));
            add("previous", "Música anterior", "Previous track", () => previousButton_Click(this, EventArgs.Empty));
            add("next", "Próxima música", "Next track", () => nextButton_Click(this, EventArgs.Empty));
            add("repeat", "Repetir a música atual", "Repeat the current track", () => repeatButton_Click(this, EventArgs.Empty));

            ToolStripMenuItem keysItem = new ToolStripMenuItem(T("Atalhos de teclado...", "Keyboard shortcuts..."));
            keysItem.Name = "shortcutsMenuItem";
            keysItem.Click += (s, e) => ShowShortcutsDialog();
            ToolStripMenuItem missingItem = new ToolStripMenuItem(T("Arquivos de áudio ausentes...", "Missing audio files..."));
            missingItem.Click += (s, e) => ShowMissingFiles();
            extrasToolStripMenuItem.DropDownItems.Add(missingItem);
            extrasToolStripMenuItem.DropDownOpening += (s, e) => RefreshShortcutMenuTexts();
            RefreshShortcutMenuTexts();
            BuildSettingsMenu(keysItem);
        }

        // "Configurações": everything which is a setting leaves the Extras menu and gets its own place
        private void BuildSettingsMenu(ToolStripMenuItem keysItem)
        {
            ToolStripItemCollection extras = extrasToolStripMenuItem.DropDownItems;
            Func<string, ToolStripItem> take = name =>
            {
                foreach (ToolStripItem item in extras)
                {
                    if (item.Name == name)
                    {
                        extras.Remove(item);
                        return item;
                    }
                }
                return null;
            };
            ToolStripMenuItem settings = new ToolStripMenuItem(T("Configurações", "Settings"));
            settings.Name = "settingsMenuRoot";
            List<ToolStripItem> general = new List<ToolStripItem>();
            general.Add(settingsToolStripMenuItem1);
            settingsToolStripMenuItem1.Text = T("Configurações gerais...", "General settings...");
            general.Add(keysItem);
            general.Add(showKeysMenuItem);
            general.Add(globalKeyHookItem);
            general.Add(take("closeToTrayItem"));
            ToolStripItem language = null;
            foreach (ToolStripItem item in extras)
            {
                if (item.Text != null && item.Text.StartsWith("Idioma"))
                {
                    language = item;
                    break;
                }
            }
            if (language != null)
                extras.Remove(language);
            general.Add(language);
            // Stream Deck, remote control, Discord and the OBS overlay stay in Extras (they are tools, not settings)

            extras.Remove(settingsToolStripMenuItem1);
            extras.Remove(showKeysMenuItem);
            extras.Remove(globalKeyHookItem);
            foreach (ToolStripItem item in general.Where(i => i != null))
                settings.DropDownItems.Add(item);
            int at = menuStrip1.Items.IndexOf(extrasToolStripMenuItem) + 1;
            menuStrip1.Items.Insert(Math.Max(at, 0), settings);

            // tidy up the Extras menu: no double / leading / trailing separators
            for (int i = extras.Count - 1; i >= 0; i--)
            {
                bool separator = extras[i] is ToolStripSeparator;
                if (!separator)
                    continue;
                bool leading = i == 0;
                bool trailing = i == extras.Count - 1;
                bool doubled = i > 0 && extras[i - 1] is ToolStripSeparator;
                if (leading || trailing || doubled)
                    extras.RemoveAt(i);
            }
        }

        private void RefreshShortcutMenuTexts()
        {
            foreach (ToolStripItem item in extrasToolStripMenuItem.DropDownItems)
            {
                string id = item.Tag as string;
                if (id == null)
                    continue;
                ToolStripMenuItem mi = item as ToolStripMenuItem;
                if (mi != null)
                    mi.ShortcutKeyDisplayString = ShortcutText(id);
            }
        }

        private void CycleTab(int delta)
        {
            if (m_WebWindow == null)
                return;
            int count = m_WebWindow.Tabs.Count + 1;
            int current = (m_ShowingWeb && m_WebWindow.Selected != null) ? 1 + m_WebWindow.Tabs.IndexOf(m_WebWindow.Selected) : 0;
            int next = ((current + delta) % count + count) % count;
            TabBar_TabSelected(next);
        }

        // called by ProcessCmdKey
        private bool HandleProgramShortcut(Keys keyData)
        {
            if (keyData == Keys.None)
                return false;
            Keys plain = keyData & Keys.KeyCode;
            bool hasModifier = (keyData & Keys.Modifiers) != 0;
            bool functionKey = plain >= Keys.F1 && plain <= Keys.F24;
            if (!hasModifier && !functionKey && ActiveControl is TextBoxBase)
                return false;
            foreach (ProgramShortcut sc in m_Shortcuts)
            {
                if (sc.Current != Keys.None && sc.Current == keyData)
                {
                    sc.Run();
                    return true;
                }
            }
            return false;
        }

        // ------------------------------------------------------------------ the window

        private class ShortcutRow
        {
            public string Kind;        // "prog", "mode", "element"
            public ProgramShortcut Program;
            public string ModeTitle;
            public int ElementId;
            public string Label;
            public int Key;
            public int Original;
            public ListViewItem Item;
        }

        private void ShowShortcutsDialog()
        {
            List<ShortcutRow> rows = new List<ShortcutRow>();
            foreach (ProgramShortcut sc in m_Shortcuts)
                rows.Add(new ShortcutRow { Kind = "prog", Program = sc, Label = T(sc.Pt, sc.En), Key = (int)sc.Current, Original = (int)sc.Current });
            if (m_Project != null)
            {
                EnsureModeTree();
                Action<ModeNode, int> walk = null;
                walk = (node, depth) =>
                {
                    IMode mode = m_Project.GetModes().FirstOrDefault(m => m.Title == node.Title);
                    if (mode != null)
                    {
                        rows.Add(new ShortcutRow { Kind = "mode", ModeTitle = mode.Title, Label = new string(' ', depth * 4) + mode.Title, Key = mode.KeyCode, Original = mode.KeyCode });
                        foreach (IModeElement me in mode.GetElements())
                        {
                            int key = (me.Trigger != null && me.Trigger.TriggerType == TriggerType.Key) ? ((IKeyTrigger)me.Trigger).KeyCode : 0;
                            rows.Add(new ShortcutRow { Kind = "element", ModeTitle = mode.Title, ElementId = me.Id, Label = new string(' ', depth * 4 + 4) + "♪ " + me.Title, Key = key, Original = key });
                        }
                    }
                    foreach (ModeNode child in node.Children)
                        walk(child, depth + 1);
                };
                if (m_ModeTree != null)
                {
                    foreach (ModeNode top in m_ModeTree.Root.Children)
                        walk(top, 0);
                }
            }
            using (ShortcutsDialog dlg = new ShortcutsDialog(rows))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;
            }
            foreach (ShortcutRow row in rows)
            {
                if (row.Kind == "prog")
                {
                    row.Program.Current = (Keys)row.Key;
                    Ares.CommonGUI.Preferences.Set("Key." + row.Program.Id, row.Key.ToString());
                }
            }
            RefreshShortcutMenuTexts();
            List<ShortcutRow> changed = rows.Where(r => r.Kind != "prog" && r.Key != r.Original).ToList();
            if (changed.Count == 0)
                return;
            string keep = SelectedModeNode != null ? SelectedModeNode.Title : null;
            ApplyProjectEdit(() =>
            {
                foreach (ShortcutRow row in changed)
                {
                    IMode mode = m_Project.GetModes().FirstOrDefault(m => m.Title == row.ModeTitle);
                    if (mode == null)
                        continue;
                    if (row.Kind == "mode")
                    {
                        mode.KeyCode = row.Key;
                        continue;
                    }
                    IModeElement me = mode.GetElements().FirstOrDefault(x => x.Id == row.ElementId);
                    if (me == null)
                        continue;
                    ITrigger trigger;
                    if (row.Key == 0)
                    {
                        trigger = DataModule.ElementFactory.CreateNoTrigger();
                    }
                    else
                    {
                        IKeyTrigger kt = DataModule.ElementFactory.CreateKeyTrigger();
                        kt.TargetElementId = me.Id;
                        kt.KeyCode = row.Key;
                        trigger = kt;
                    }
                    if (me.Trigger != null && me.Trigger.TriggerType == TriggerType.Key)
                    {
                        trigger.StopSounds = me.Trigger.StopSounds;
                        trigger.StopMusic = me.Trigger.StopMusic;
                    }
                    me.Trigger = trigger;
                }
            }, keep);
        }

        private class ShortcutsDialog : Form
        {
            private readonly List<ShortcutRow> m_Rows;
            private readonly ListView m_List;
            private readonly Button m_Change, m_Remove, m_Default;

            private static readonly HashSet<Keys> s_Reserved = new HashSet<Keys>
            {
                Keys.Left, Keys.Right, Keys.Up, Keys.Down, Keys.PageUp, Keys.PageDown, Keys.Insert, Keys.Delete, Keys.Escape
            };

            public ShortcutsDialog(List<ShortcutRow> rows)
            {
                m_Rows = rows;
                Text = T("Atalhos de teclado", "Keyboard shortcuts");
                AutoScaleMode = AutoScaleMode.Font;
                ClientSize = new Size(640, 520);
                MinimumSize = new Size(520, 360);
                StartPosition = FormStartPosition.CenterParent;
                ShowInTaskbar = false;
                MinimizeBox = false;
                MaximizeBox = true;

                Label help = new Label();
                help.Text = T("Dê dois cliques num item (ou use \"Alterar...\") e aperte a combinação de teclas desejada. Os atalhos de pastas e playlists ficam salvos no projeto.",
                    "Double-click an item (or use \"Change...\") and press the key combination you want. Shortcuts of folders and playlists are saved in the project.");
                help.SetBounds(12, 10, 616, 34);
                help.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                Controls.Add(help);

                m_List = new ListView();
                m_List.View = View.Details;
                m_List.FullRowSelect = true;
                m_List.HideSelection = false;
                m_List.MultiSelect = false;
                m_List.GridLines = false;
                m_List.Columns.Add(T("Item", "Item"), 400);
                m_List.Columns.Add(T("Atalho", "Shortcut"), 170);
                m_List.SetBounds(12, 48, 616, 410);
                m_List.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
                ListViewGroup progGroup = new ListViewGroup(T("Programa", "Program"));
                ListViewGroup folderGroup = new ListViewGroup(T("Pastas e playlists do projeto", "Folders and playlists of the project"));
                m_List.Groups.Add(progGroup);
                m_List.Groups.Add(folderGroup);
                foreach (ShortcutRow row in m_Rows)
                {
                    ListViewItem item = new ListViewItem(row.Label);
                    item.SubItems.Add(KeyText((Keys)row.Key));
                    item.Group = row.Kind == "prog" ? progGroup : folderGroup;
                    item.Tag = row;
                    if (row.Kind == "mode")
                        item.Font = new Font(m_List.Font, FontStyle.Bold);
                    row.Item = item;
                    m_List.Items.Add(item);
                }
                m_List.DoubleClick += (s, e) => ChangeSelected();
                m_List.SelectedIndexChanged += (s, e) => UpdateButtons();
                m_List.Resize += (s, e) =>
                {
                    if (m_List.Columns.Count == 2)
                    {
                        m_List.Columns[1].Width = 170;
                        m_List.Columns[0].Width = Math.Max(150, m_List.ClientSize.Width - 175);
                    }
                };
                Controls.Add(m_List);

                m_Change = new Button { Text = T("Alterar...", "Change...") };
                m_Remove = new Button { Text = T("Remover", "Remove") };
                m_Default = new Button { Text = T("Padrão", "Default") };
                Button ok = new Button { Text = "OK", DialogResult = DialogResult.OK };
                Button cancel = new Button { Text = T("Cancelar", "Cancel"), DialogResult = DialogResult.Cancel };
                m_Change.SetBounds(12, 470, 100, 32);
                m_Remove.SetBounds(120, 470, 100, 32);
                m_Default.SetBounds(228, 470, 100, 32);
                ok.SetBounds(420, 470, 100, 32);
                cancel.SetBounds(528, 470, 100, 32);
                m_Change.Anchor = m_Remove.Anchor = m_Default.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
                ok.Anchor = cancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
                m_Change.Click += (s, e) => ChangeSelected();
                m_Remove.Click += (s, e) => SetKey(Selected(), 0);
                m_Default.Click += (s, e) =>
                {
                    ShortcutRow row = Selected();
                    if (row != null && row.Kind == "prog")
                        SetKey(row, (int)row.Program.Default);
                };
                Controls.AddRange(new Control[] { m_Change, m_Remove, m_Default, ok, cancel });
                AcceptButton = ok;
                CancelButton = cancel;
                UpdateButtons();
            }

            private ShortcutRow Selected()
            {
                return m_List.SelectedItems.Count == 0 ? null : (ShortcutRow)m_List.SelectedItems[0].Tag;
            }

            private void UpdateButtons()
            {
                ShortcutRow row = Selected();
                m_Change.Enabled = row != null;
                m_Remove.Enabled = row != null && row.Key != 0;
                m_Default.Enabled = row != null && row.Kind == "prog";
            }

            private void SetKey(ShortcutRow row, int key)
            {
                if (row == null)
                    return;
                row.Key = key;
                row.Item.SubItems[1].Text = KeyText((Keys)key);
                UpdateButtons();
            }

            private void ChangeSelected()
            {
                ShortcutRow row = Selected();
                if (row == null)
                    return;
                int key;
                using (KeyCaptureDialog dlg = new KeyCaptureDialog(row.Label.Trim()))
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK)
                        return;
                    key = (int)dlg.Result;
                }
                if (s_Reserved.Contains((Keys)key))
                {
                    MessageBox.Show(this, T("Essa tecla é reservada pelo programa (setas, Esc, Delete, Insert, PageUp/PageDown). Escolha outra.",
                        "That key is reserved by the program (arrows, Esc, Delete, Insert, PageUp/PageDown). Choose another one."),
                        StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                foreach (ShortcutRow other in m_Rows)
                {
                    if (other == row || other.Key != key)
                        continue;
                    // a key can only do one thing inside the same scope: the program, or the same folder
                    bool sameScope = other.Kind == "prog" || row.Kind == "prog"
                        || (other.Kind == "mode") || (row.Kind == "mode") || other.ModeTitle == row.ModeTitle;
                    if (!sameScope)
                        continue;
                    DialogResult answer = MessageBox.Show(this,
                        T("A tecla " + KeyText((Keys)key) + " já é usada por \"" + other.Label.Trim() + "\". Tirar de lá e usar aqui?",
                          "The key " + KeyText((Keys)key) + " is already used by \"" + other.Label.Trim() + "\". Take it from there and use it here?"),
                        StringResources.Ares, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (answer != DialogResult.Yes)
                        return;
                    SetKey(other, 0);
                }
                SetKey(row, key);
            }
        }

        private class KeyCaptureDialog : Form
        {
            public Keys Result;
            private readonly Label m_Label;

            public KeyCaptureDialog(string what)
            {
                Text = T("Novo atalho", "New shortcut");
                FormBorderStyle = FormBorderStyle.FixedDialog;
                StartPosition = FormStartPosition.CenterParent;
                MaximizeBox = false;
                MinimizeBox = false;
                ShowInTaskbar = false;
                AutoScaleMode = AutoScaleMode.Font;
                ClientSize = new Size(420, 130);
                m_Label = new Label();
                m_Label.Text = T("Aperte a combinação de teclas para:\n", "Press the key combination for:\n") + what
                    + T("\n\n(Esc cancela)", "\n\n(Esc cancels)");
                m_Label.SetBounds(16, 16, 388, 100);
                Controls.Add(m_Label);
            }

            protected override bool ProcessCmdKey(ref System.Windows.Forms.Message msg, Keys keyData)
            {
                Keys plain = keyData & Keys.KeyCode;
                if (plain == Keys.Escape && (keyData & Keys.Modifiers) == 0)
                {
                    DialogResult = DialogResult.Cancel;
                    return true;
                }
                if (plain == Keys.ControlKey || plain == Keys.ShiftKey || plain == Keys.Menu || plain == Keys.None)
                    return true;
                Result = keyData;
                DialogResult = DialogResult.OK;
                return true;
            }
        }
    }
}
