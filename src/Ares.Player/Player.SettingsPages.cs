/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 The pages of the settings window which gather what used to be spread over the Extras and Settings menus:
   Interface (look of the program, language, tray), Keyboard and pad, Connections (remote control, Stream Deck, Discord ...).
 Everything on these pages applies at once (no OK needed); the pages drive the same switches the old menu items had.
*/
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Ares.CommonGUI;

namespace Ares.Player
{
    public partial class Player
    {
        private ToolStripMenuItem m_LanguageItem, m_BankAutoItem, m_PadCloseItem;
        private ToolStripButton m_GearButton;
        private Action m_AfterSettings;

        // The settings which used to be menu items leave the Tools menu; the items stay alive (without a menu) because the
        // pages use them as the switches.
        private void DetachSettingsItems()
        {
            ToolStripItemCollection extras = extrasToolStripMenuItem.DropDownItems;
            Func<Func<ToolStripItem, bool>, ToolStripItem> pull = test =>
            {
                ToolStripItem found = extras.Cast<ToolStripItem>().FirstOrDefault(test);
                if (found != null)
                    extras.Remove(found);
                return found;
            };
            Func<ToolStripItem, string, string, bool> starts = (i, pt, en) =>
                (i.Text ?? "").StartsWith(pt, StringComparison.OrdinalIgnoreCase) || (i.Text ?? "").StartsWith(en, StringComparison.OrdinalIgnoreCase);

            pull(i => i == settingsToolStripMenuItem1 || i == showKeysMenuItem || i == globalKeyHookItem);
            pull(i => i == showKeysMenuItem);
            pull(i => i == globalKeyHookItem);
            pull(i => i.Name == "closeToTrayItem");
            m_LanguageItem = pull(i => (i.Text ?? "").StartsWith("Idioma")) as ToolStripMenuItem;
            pull(i => starts(i, "Tamanho do texto das pastas", "Folder text size"));
            pull(i => starts(i, "Suavizar a repetição", "Smooth the repetition"));
            foreach (string name in new[] { "remoteMenuItem", "deckMenuItem", "discordMenuItem", "overlayMenuItem", "foundryMenuItem" })
                pull(i => i.Name == name);

            // the picture bank keeps its two "fill" actions; its switch moves to the settings
            foreach (ToolStripItem item in extras)
            {
                ToolStripMenuItem sub = item as ToolStripMenuItem;
                if (sub == null)
                    continue;
                ToolStripItem auto = sub.DropDownItems.Cast<ToolStripItem>().FirstOrDefault(d => d.Name == "autoBankItem");
                if (auto == null)
                    continue;
                m_BankAutoItem = auto as ToolStripMenuItem;
                sub.DropDownItems.Remove(auto);
                ToolStripItem last = sub.DropDownItems.Count > 0 ? sub.DropDownItems[sub.DropDownItems.Count - 1] : null;
                if (last is ToolStripSeparator)
                    sub.DropDownItems.Remove(last);
                break;
            }

            // the library (import and export of folders) belongs to the Project menu, under "Import"
            ToolStripItem library = pull(i => starts(i, "Biblioteca", "Library"));
            if (library != null)
                projectToolStripMenuItem.DropDownItems.Insert(projectToolStripMenuItem.DropDownItems.IndexOf(importToolStripMenuItem) + 1, library);

            for (int i = extras.Count - 1; i >= 0; i--)
            {
                bool separator = extras[i] is ToolStripSeparator;
                if (separator && (i == 0 || i == extras.Count - 1 || extras[i - 1] is ToolStripSeparator))
                    extras.RemoveAt(i);
            }
        }

        private void InitGearButton()
        {
            m_GearButton = new ToolStripButton();
            m_GearButton.Name = "gearButton";
            m_GearButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
            m_GearButton.Alignment = ToolStripItemAlignment.Right;
            m_GearButton.ToolTipText = T("Configurações (" + ShortcutText("settings") + ")", "Settings (" + ShortcutText("settings") + ")");
            m_GearButton.AccessibleName = T("Configurações", "Settings");
            m_GearButton.Click += (s, e) => ShowSettingsDialog(-1);
            menuStrip1.Items.Add(m_GearButton);
        }

        // ------------------------------------------------------------------ the pages

        private abstract class SettingsPage : ISettingsDialogPage
        {
            protected readonly Player Owner;
            private readonly FlowLayoutPanel m_Flow = new FlowLayoutPanel();
            private readonly string m_Title;

            protected SettingsPage(Player owner, string title)
            {
                Owner = owner;
                m_Title = title;
                m_Flow.FlowDirection = FlowDirection.TopDown;
                m_Flow.WrapContents = false;
                m_Flow.AutoScroll = true;
                m_Flow.Padding = new Padding(DialogKit.Margin - 8, 8, DialogKit.Margin - 8, 12);
                m_Flow.Resize += (s, e) => FitWidths();
            }

            public Control Page { get { return m_Flow; } }
            public string PageTitle { get { return m_Title; } }
            public void OnConfirm() { }

            private int ContentWidth
            {
                get { return Math.Max(200, m_Flow.ClientSize.Width - m_Flow.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth); }
            }

            private void FitWidths()
            {
                int w = ContentWidth;
                m_Flow.SuspendLayout();
                foreach (Control c in m_Flow.Controls)
                {
                    Label l = c as Label;
                    if (l != null && l.AutoSize)
                        l.MaximumSize = new Size(w, 0);
                    else
                        c.Width = w;
                }
                m_Flow.ResumeLayout(true);
            }

            protected void Add(Control c)
            {
                m_Flow.Controls.Add(c);
                FitWidths();
            }

            protected void Heading(string text)
            {
                Label l = new Label();
                l.AutoSize = true;
                l.Text = text;
                l.Font = new Font(m_Flow.Font, FontStyle.Bold);
                l.Margin = new Padding(0, m_Flow.Controls.Count == 0 ? 8 : 22, 0, 6);
                Add(l);
            }

            protected void Note(string text)
            {
                Label l = new Label();
                l.AutoSize = true;
                l.Text = text;
                l.Tag = "Muted";
                l.Margin = new Padding(0, 0, 0, 8);
                Add(l);
            }

            protected CheckBox Check(string text, bool value, Action<bool> changed)
            {
                CheckBox c = new CheckBox();
                c.Text = text;
                c.AutoSize = true;
                c.Checked = value;
                c.Margin = new Padding(0, 2, 0, 6);
                c.CheckedChanged += (s, e) => changed(c.Checked);
                Add(c);
                return c;
            }

            /// <summary>A line: the name at the left, the control (or a few) at the right.</summary>
            protected Panel Row(string label, params Control[] controls)
            {
                Panel p = new Panel();
                p.Height = DialogKit.FieldHeight + 6;
                p.Margin = new Padding(0, 0, 0, 4);
                Label l = new Label();
                l.Text = label;
                l.TextAlign = ContentAlignment.MiddleLeft;
                l.SetBounds(0, 0, 230, DialogKit.FieldHeight + 6);
                p.Controls.Add(l);
                int x = 240;
                foreach (Control c in controls)
                {
                    c.Left = x;
                    c.Top = Math.Max(0, (p.Height - c.Height) / 2);
                    p.Controls.Add(c);
                    x += c.Width + 8;
                }
                Add(p);
                return p;
            }

            protected Button MakeButton(string text, Action run, bool primary)
            {
                Button b = new Button();
                b.Text = text;
                b.AutoSize = true;
                b.AutoSizeMode = AutoSizeMode.GrowAndShrink;
                b.MinimumSize = new Size(0, DialogKit.ButtonHeight - 4);
                b.Padding = new Padding(10, 0, 10, 0);
                if (primary)
                    DialogKit.Primary(b);
                b.Click += (s, e) => run();
                return b;
            }

            protected Button AddButton(string text, Action run)
            {
                Button b = MakeButton(text, run, false);
                b.Margin = new Padding(0, 0, 0, 8);
                Add(b);
                return b;
            }

            /// <summary>Saves what the legacy pages hold and closes the window; then the action runs (a dialog of its own opens).</summary>
            protected void CloseThen(Action run)
            {
                Owner.m_AfterSettings = run;
                Form form = Page.FindForm();
                if (form == null)
                    return;
                Button accept = form.AcceptButton as Button;
                if (accept != null)
                    accept.PerformClick();
                else
                    form.Close();
            }

            protected ThemedComboBox Combo(IEnumerable<string> items, int selected, Action<int> changed)
            {
                ThemedComboBox c = new ThemedComboBox();
                foreach (string s in items)
                    c.Items.Add(s);
                c.Width = 240;
                c.ItemHeight = Math.Max(16, DialogKit.FieldHeight - 8);
                if (selected >= 0 && selected < c.Items.Count)
                    c.SelectedIndex = selected;
                c.SelectedIndexChanged += (s, e) => { if (c.SelectedIndex >= 0) changed(c.SelectedIndex); };
                return c;
            }
        }

        // ---- Interface: look, folders, language, tray ----

        private class InterfacePage : SettingsPage
        {
            public InterfacePage(Player owner) : base(owner, Loc.T("Interface", "Interface"))
            {
                ToolStripMenuItem appearance = Owner.menuStrip1.Items.OfType<ToolStripMenuItem>()
                    .FirstOrDefault(i => i.Text == "Aparência" || i.Text == "Appearance");
                Heading(Loc.T("Aparência", "Appearance"));
                if (appearance != null)
                {
                    List<ToolStripMenuItem> themes = appearance.DropDownItems.OfType<ToolStripMenuItem>().Where(i => i.Tag is string).ToList();
                    int current = themes.FindIndex(i => (string)i.Tag == ThemeManager.Current.Id);
                    Row(Loc.T("Tema", "Theme"), Combo(themes.Select(i => i.Text), current, index => themes[index].PerformClick()));
                    ToolStripMenuItem sizeRoot = appearance.DropDownItems.OfType<ToolStripMenuItem>().FirstOrDefault(i => i.DropDownItems.Count > 0 && i.DropDownItems[0].Tag is int);
                    if (sizeRoot != null)
                    {
                        List<ToolStripMenuItem> sizes = sizeRoot.DropDownItems.OfType<ToolStripMenuItem>().ToList();
                        int now = sizes.FindIndex(i => (int)i.Tag == ThemeManager.ScalePercent);
                        Row(Loc.T("Tamanho da interface", "Interface size"), Combo(sizes.Select(i => i.Text), now, index => sizes[index].PerformClick()));
                    }
                }
                Label zoomText = new Label();
                zoomText.Text = FolderZoom + "%";
                zoomText.TextAlign = ContentAlignment.MiddleCenter;
                zoomText.Width = 56;
                zoomText.Height = DialogKit.FieldHeight;
                Button smaller = MakeButton("−", () => { Owner.ChangeFolderZoom(-10); zoomText.Text = FolderZoom + "%"; }, false);
                Button bigger = MakeButton("+", () => { Owner.ChangeFolderZoom(10); zoomText.Text = FolderZoom + "%"; }, false);
                Button normal = MakeButton(Loc.T("Normal", "Normal"), () => { Owner.SetFolderZoom(100); zoomText.Text = FolderZoom + "%"; }, false);
                smaller.AutoSize = false; smaller.Width = 40; smaller.Height = DialogKit.FieldHeight;
                bigger.AutoSize = false; bigger.Width = 40; bigger.Height = DialogKit.FieldHeight;
                normal.AutoSize = false; normal.Width = 90; normal.Height = DialogKit.FieldHeight;
                Row(Loc.T("Texto da lista de pastas", "Text of the folder list"), smaller, zoomText, bigger, normal);
                Note(Loc.T("Também com Ctrl + roda do mouse sobre a lista de pastas.", "Also with Ctrl + mouse wheel over the folder list."));

                if (Owner.m_BankAutoItem != null)
                {
                    ToolStripMenuItem bank = Owner.m_BankAutoItem;
                    Check(bank.Text, bank.Checked, value => bank.Checked = value);
                }

                Heading(Loc.T("Programa", "Program"));
                if (Owner.m_LanguageItem != null)
                {
                    ToolStripMenuItem language = Owner.m_LanguageItem;
                    Button choose = null;
                    choose = MakeButton(Loc.T("Escolher idioma...", "Choose language..."), () => language.DropDown.Show(choose, new Point(0, choose.Height)), false);
                    Row(Loc.T("Idioma", "Language"), choose);
                }
                if (Owner.m_CloseToTrayItem != null)
                {
                    ToolStripMenuItem tray = Owner.m_CloseToTrayItem;
                    Check(tray.Text, tray.Checked, value => tray.Checked = value);
                }
            }
        }

        // ---- Keyboard and floating pad ----

        private class KeyboardPage : SettingsPage
        {
            public KeyboardPage(Player owner) : base(owner, Loc.T("Teclado e pad", "Keyboard and pad"))
            {
                Heading(Loc.T("Atalhos", "Shortcuts"));
                AddButton(Loc.T("Atalhos de teclado...", "Keyboard shortcuts..."), () => CloseThen(Owner.ShowShortcutsDialog));
                Check(Owner.showKeysMenuItem.Text, Owner.showKeysMenuItem.Checked, value => Owner.showKeysMenuItem.Checked = value);
                Check(Owner.globalKeyHookItem.Text, Owner.globalKeyHookItem.Checked, value => Owner.globalKeyHookItem.Checked = value)
                    .Enabled = Owner.globalKeyHookItem.Enabled;

                Heading(Loc.T("Pad flutuante", "Floating pad"));
                Note(Loc.T("Os botões de uma pasta aparecem por cima da tela, meio transparentes, com um atalho que funciona mesmo com o Apolo em segundo plano.",
                    "The buttons of a folder appear on top of the screen, slightly transparent, with a shortcut which works even when Apolo is in the background."));
                AddButton(Loc.T("Mostrar / esconder o pad", "Show / hide the pad"), () => CloseThen(Owner.TogglePad));
                Label keyText = new Label();
                keyText.Text = KeyText(PadHotkey);
                keyText.TextAlign = ContentAlignment.MiddleLeft;
                keyText.Width = 140;
                keyText.Height = DialogKit.FieldHeight;
                Button change = MakeButton(Loc.T("Alterar...", "Change..."), () => { Owner.ChoosePadHotkey(); keyText.Text = KeyText(PadHotkey); }, false);
                Row(Loc.T("Atalho do pad", "Pad shortcut"), keyText, change);
                if (Owner.m_PadCloseItem != null)
                {
                    ToolStripMenuItem close = Owner.m_PadCloseItem;
                    Check(close.Text, close.Checked, value => close.Checked = value);
                }
                int[] steps = { 100, 85, 70, 55 };
                string[] names = { Loc.T("Nenhuma", "None"), Loc.T("Pouca", "Little"), Loc.T("Média", "Medium"), Loc.T("Muita", "A lot") };
                Row(Loc.T("Transparência", "Transparency"), Combo(names, Array.IndexOf(steps, PadOpacityPercent), index =>
                {
                    Ares.CommonGUI.Preferences.Set("PadOpacity", steps[index].ToString());
                    if (Owner.m_Pad != null)
                        Owner.m_Pad.Opacity = steps[index] / 100.0;
                }));
            }
        }

        // ---- Connections ----

        private class ConnectionsPage : SettingsPage
        {
            public ConnectionsPage(Player owner) : base(owner, Loc.T("Conexões", "Connections"))
            {
                Note(Loc.T("Tudo o que liga o Apolo a outros programas e pessoas. Cada \"Configurar\" abre a janela do serviço.",
                    "Everything which connects Apolo to other programs and people. Each \"Set up\" opens the window of the service."));
                Service(Loc.T("Controle remoto", "Remote control"),
                    Loc.T("Abra o Apolo pelo navegador do celular ou pela internet, com senha e QR Code.", "Open Apolo from the browser of your phone or over the internet, with a password and QR code."),
                    Owner.ShowRemoteDialog);
                Service("Stream Deck",
                    Loc.T("Botões físicos do Stream Deck tocando os botões do Apolo.", "Physical Stream Deck keys playing the Apolo buttons."),
                    Owner.ShowDeckDialog);
                Service("Discord",
                    Loc.T("Tocar o Apolo num canal de voz, com um bot.", "Play Apolo in a voice channel, with a bot."),
                    Owner.ShowDiscordDialog);
                Service("VDO.Ninja",
                    Loc.T("Enviar o áudio do Apolo pela internet para jogadores e para o Foundry.", "Send the audio of Apolo over the internet to players and to Foundry."),
                    () => Owner.ShowWebWindow(true));
                Service(Loc.T("Overlay do OBS", "OBS overlay"),
                    Loc.T("Mostrar na sua live a música que está tocando.", "Show the music which is playing on your stream."),
                    Owner.ShowOverlayDialog);
                Service("Foundry VTT",
                    Loc.T("Módulo para tocar o Apolo dentro do Foundry: como instalar e usar.", "Module to play Apolo inside Foundry: how to install and use it."),
                    Owner.ShowFoundryHelp);
            }

            private void Service(string title, string description, Action open)
            {
                Panel p = new Panel();
                p.Height = 62;
                p.Margin = new Padding(0, 0, 0, 6);
                Button configure = MakeButton(Loc.T("Configurar...", "Set up..."), () => CloseThen(open), false);
                configure.AutoSize = false;
                configure.Size = new Size(130, DialogKit.ButtonHeight - 4);
                configure.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                Label name = new Label();
                name.Text = title;
                name.Font = new Font(p.Font, FontStyle.Bold);
                name.AutoEllipsis = true;
                name.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                Label about = new Label();
                about.Text = description;
                about.Tag = "Muted";
                about.AutoEllipsis = true;
                about.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                p.Controls.AddRange(new Control[] { name, about, configure });
                p.Resize += (s, e) =>
                {
                    configure.SetBounds(p.Width - 130, 8, 130, DialogKit.ButtonHeight - 4);
                    name.SetBounds(0, 4, Math.Max(40, p.Width - 146), 24);
                    about.SetBounds(0, 30, Math.Max(40, p.Width - 146), 24);
                };
                Add(p);
            }
        }
    }
}
