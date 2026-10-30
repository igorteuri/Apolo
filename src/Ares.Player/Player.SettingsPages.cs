/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 The settings of Apolo live in a tab of the main window ("Configurações"), with a side bar to choose the page:
   General and Music (the pages of the old dialog, taken out of it), Interface, Keyboard and pad, Connections.
 The services (remote control, Stream Deck, Discord, OBS, Foundry) and the list of shortcuts open inside their page
 (ShowOrEmbed): the windows which used to be separate dialogs are shown in the space of the tab.
 The pages of this file apply at once; General and Music are saved when the tab is left.
*/
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Ares.CommonGUI;
using Ares.Players;

namespace Ares.Player
{
    public partial class Player
    {
        private ToolStripMenuItem m_LanguageItem, m_BankAutoItem, m_PadCloseItem;
        private ToolStripButton m_GearButton;

        // the settings tab
        private SettingsView m_SettingsView;
        private bool m_SettingsOpen, m_ShowingSettings;
        private string m_SettingsSnapshot;
        // when set, the next window of a service opens in this panel instead of a dialog
        private Panel m_EmbedHost;

        // The settings which used to be menu items leave the Tools menu; the items stay alive (without a menu) because the
        // pages use them as the switches.
        private ToolStripMenuItem m_AppearanceItem;

        // the "Appearance" menu (added by the theme manager) moves to the settings; its items stay as the switches
        private void HideAppearanceMenu()
        {
            ToolStripMenuItem found = menuStrip1.Items.OfType<ToolStripMenuItem>()
                .FirstOrDefault(i => i.Text == "Aparência" || i.Text == "Appearance");
            if (found == null)
                return;
            m_AppearanceItem = found;
            menuStrip1.Items.Remove(found);
        }

        private void DetachSettingsItems()
        {
            HideAppearanceMenu();
            ThemeManager.AfterAppearanceMenu = (form, menu) =>
            {
                Player player = form as Player;
                if (player != null)
                    player.HideAppearanceMenu();
            };
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

            pull(i => i == settingsToolStripMenuItem1);
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
            m_GearButton.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
            m_GearButton.Text = T("Configurações", "Settings");
            m_GearButton.Tag = "AccentText"; // the theme draws its text in the highlight color
            m_GearButton.ImageScaling = ToolStripItemImageScaling.None;
            m_GearButton.Margin = new Padding(6, 0, 6, 0);
            m_GearButton.Alignment = ToolStripItemAlignment.Right;
            Action paintIcon = () =>
            {
                Image old = m_GearButton.Image;
                m_GearButton.Image = IconLibrary.Get("settings", new Size(20, 20), ThemeManager.Current.Accent);
                if (old != null)
                    old.Dispose();
            };
            paintIcon();
            ThemeManager.Changed += (s, e) => paintIcon();
            m_GearButton.ToolTipText = T("Configurações (" + ShortcutText("settings") + ")", "Settings (" + ShortcutText("settings") + ")");
            m_GearButton.AccessibleName = T("Configurações", "Settings");
            m_GearButton.Click += (s, e) => ShowSettingsDialog(-1);
            menuStrip1.Items.Add(m_GearButton);
        }

        // ------------------------------------------------------------------ the tab

        private int SettingsTabIndex
        {
            get { return 1 + (m_WebWindow != null ? m_WebWindow.Tabs.Count : 0); }
        }

        // opens (or shows again) the settings tab, on a page (-1 = the one used last)
        private void ShowSettingsDialog(int pageIndex)
        {
            ShowSettingsPage(pageIndex, null);
        }

        // a window of a service (remote control, Discord ...) asked for by something outside the settings: it opens in its page
        private bool RedirectToSettings(string key)
        {
            if (m_EmbedHost != null)
                return false;
            ShowSettingsPage(-1, key);
            return true;
        }

        private void ShowSettingsPage(int pageIndex, string key)
        {
            if (m_ShowingSettings)
            {
                if (key != null)
                    pageIndex = m_SettingsView.IndexOfKey(key);
                if (pageIndex >= 0)
                    m_SettingsView.SelectPage(pageIndex);
                return;
            }
            if (m_SettingsView == null)
            {
                m_SettingsView = new SettingsView(this);
                m_SettingsView.Dock = DockStyle.Fill;
                m_SettingsView.Visible = false;
                Controls.Add(m_SettingsView);
            }
            m_SettingsOpen = true;
            m_ShowingWeb = false;
            if (m_WebWindow != null)
                m_WebWindow.Visible = false;
            SetEditChromeVisible(false);
            m_SettingsSnapshot = SettingsSnapshot();
            m_ShowingSettings = true;
            m_SettingsView.Visible = true;
            m_SettingsView.BringToFront();
            m_TabBar.SendToBack();
#if !MONO
            m_KeyboardHookManager.Enabled = false;
#endif
            if (key != null)
                pageIndex = m_SettingsView.IndexOfKey(key);
            if (pageIndex >= 0)
                m_SettingsView.SelectPage(pageIndex);
            else
                m_SettingsView.RefreshCurrent();
            RefreshTabBar();
        }

        private static string SettingsSnapshot()
        {
            Ares.Settings.Settings s = Ares.Settings.Settings.Instance;
            return string.Join("|", new object[] { s.MusicDirectory, s.SoundDirectory, s.PlayMusicOnAllSpeakers, s.ButtonMusicFadeMode,
                s.ButtonMusicFadeTime, s.UseLegacyNetwork, s.UseWebNetwork });
        }

        // Leaving the settings tab: General and Music are saved; false when something is not valid (the tab stays).
        private bool LeaveSettings()
        {
            if (!m_ShowingSettings || m_SettingsView == null)
                return true;
            if (!m_SettingsView.Legacy.Confirm())
                return false;
            m_ShowingSettings = false;
            m_SettingsView.Visible = false;
#if !MONO
            m_KeyboardHookManager.Enabled = true;
#endif
            if (SettingsSnapshot() != m_SettingsSnapshot)
                ApplySettingsChanges();
            return true;
        }

        private void CloseSettingsTab()
        {
            if (!LeaveSettings())
            {
                RefreshTabBar();
                return;
            }
            m_SettingsOpen = false;
            if (m_SettingsView != null)
            {
                Controls.Remove(m_SettingsView);
                m_SettingsView.Dispose();
                m_SettingsView = null;
            }
            if (m_WebWindow != null && m_WebWindow.Tabs.Count > 0 && m_WebWindow.Selected != null)
                ShowWebTab(m_WebWindow.Selected);
            else
                ShowHome();
        }

        // what the old settings dialog did after OK
        private void ApplySettingsChanges()
        {
            Ares.Settings.Settings settings = Ares.Settings.Settings.Instance;
            m_PlayingControl.UpdateDirectories();
            LoadTagsDB();
            SetOutputDevice();
            m_PlayingControl.SetPlayMusicOnAllSpeakers(settings.PlayMusicOnAllSpeakers);
            m_PlayingControl.SetFadingOnPreviousNext(settings.ButtonMusicFadeMode != 0, settings.ButtonMusicFadeMode == 2, settings.ButtonMusicFadeTime);
            if (m_Network != null)
            {
                if (m_Network.ClientConnected)
                {
                    m_Network.DisconnectClient(false);
                }
                m_Network.Shutdown();
                m_Network = new Networks(this, settings.UseLegacyNetwork, settings.UseWebNetwork);
                m_Network.InitConnectionData();
                if (settings.UseWebNetwork || settings.UseLegacyNetwork)
                {
                    m_Network.StartUdpBroadcast();
                    m_Network.ListenForClient();
                }
                UpdateClientState();
            }
        }

        /// <summary>
        /// Shows a window of a service: in a dialog, or (when the settings tab asked for it through m_EmbedHost) inside that panel.
        /// "cleanup" runs when the window is closed.
        /// </summary>
        private void ShowOrEmbed(Form form, Action cleanup)
        {
            Panel host = m_EmbedHost;
            m_EmbedHost = null;
            if (host == null)
            {
                form.ShowDialog(this);
                if (cleanup != null)
                    cleanup();
                form.Dispose();
                return;
            }
            foreach (Control old in host.Controls.Cast<Control>().ToList())
            {
                host.Controls.Remove(old);
                old.Dispose();
            }
            bool resizable = form.FormBorderStyle == FormBorderStyle.Sizable || form.FormBorderStyle == FormBorderStyle.SizableToolWindow;
            form.TopLevel = false;
            form.FormBorderStyle = FormBorderStyle.None;
            form.Location = Point.Empty;
            if (resizable)
            {
                form.MinimumSize = Size.Empty;
                form.Dock = DockStyle.Fill;
            }
            if (resizable)
                host.Controls.Add(form);
            else
                PutInCard(host, form);
            // the buttons which closed the dialog (OK, Close ...) close the embedded window
            Action<Control> hook = null;
            hook = c =>
            {
                foreach (Control child in c.Controls)
                {
                    Button b = child as Button;
                    if (b != null && b.DialogResult != DialogResult.None)
                    {
                        // inside the settings tab a "Close" button only opened the same page again: the sidebar is the way out
                        if (b.Text == "Fechar" || b.Text == "Close")
                            b.Visible = false;
                        b.Click += (s, e) => form.BeginInvoke(new MethodInvoker(form.Close));
                    }
                    hook(child);
                }
            };
            hook(form);
            form.FormClosed += (s, e) =>
            {
                if (cleanup != null)
                    cleanup();
                Action again = host.Tag as Action; // the page opens the window again
                if (again != null && host.IsHandleCreated && !host.IsDisposed)
                    host.BeginInvoke(again);
            };
            ThemeManager.Apply(form);
            host.AutoScrollMinSize = Size.Empty;
            form.Show();
            // no control starts with the focus (the dotted focus frame sat on the first check box of the page)
            form.ActiveControl = null;
        }

        /// <summary>Shows "content" in a card as wide as the cards of the other settings pages (it follows the width of the page).</summary>
        private static CardPanel PutInCard(Panel host, Control content)
        {
            CardPanel card = new CardPanel();
            card.Margin = Padding.Empty;
            card.Location = Point.Empty;
            content.Margin = Padding.Empty;
            card.Controls.Add(content);
            host.Controls.Add(card);
            Action fit = () =>
            {
                if (!card.IsDisposed)
                    card.MinimumSize = new Size(Math.Max(0, host.ClientSize.Width - SystemInformation.VerticalScrollBarWidth), 0);
            };
            host.Resize += (s, e) => fit();
            fit();
            return card;
        }

        // ------------------------------------------------------------------ the view

        // a row of the side bar: a page, or the title of a group whose pages follow (indented)
        private class SidebarEntry
        {
            public string Title, Icon;
            public int Page = -1;     // index of the page; -1 for the title of a group
            public bool Child;        // belongs to the group above
        }

        private class SettingsView : Panel
        {
            private class PageEntry
            {
                public string Key, Title, Icon, Group;
                public Control Page;
                public Action Shown;
            }

            private readonly Player m_Owner;
            private readonly List<PageEntry> m_Pages = new List<PageEntry>();
            private readonly List<SidebarEntry> m_Entries = new List<SidebarEntry>();
            private readonly Sidebar m_Sidebar = new Sidebar();
            private readonly Label m_Title = new Label();
            private readonly Panel m_Body = new Panel();
            private readonly Panel m_Column = new Panel();
            private int m_Selected = -1;
            private string m_Group;
            private static int s_LastPage;

            /// <summary>The old dialog, never shown: it keeps the General and Music pages and saves them.</summary>
            public readonly SettingsDialog Legacy;

            public SettingsView(Player owner)
            {
                m_Owner = owner;
                Legacy = new SettingsDialog(Ares.Settings.Settings.Instance, owner.m_BasicSettings);
                Legacy.AddPage(new MusicPageHost());
                SettingsPage general = new GeneralPage(owner, Legacy);
                AddPage("general", general.PageTitle, "sliders-horizontal", general.Page, general.OnPageShown);
                AddPage("music", Legacy.TitleOfPage(1), "music", Legacy.TakePage(1), null);
                SettingsPage keys = new KeyboardPage(owner);
                AddPage("keys", keys.PageTitle, "keyboard", keys.Page, keys.OnPageShown);

                // the services are a group of their own: each one has its page, with its window shown inside it
                AddGroup(Loc.T("Conexões", "Connections"), "link");
                AddService("remote", Loc.T("Controle remoto", "Remote control"), "radio", owner.ShowRemoteDialog);
                AddService("deck", "Stream Deck", "gamepad-2", owner.ShowDeckDialog);
                AddService("discord", "Discord", "message-circle", owner.ShowDiscordDialog);
                AddService("overlay", Loc.T("Overlay do OBS", "OBS overlay"), "tv", owner.ShowOverlayDialog);
                AddService("foundry", "Foundry VTT", "dices", owner.ShowFoundryHelp);
                AddVdoPage();

                m_Sidebar.Dock = DockStyle.Left;
                m_Sidebar.SetItems(m_Entries);
                m_Sidebar.Selected += SelectPage;

                m_Title.Dock = DockStyle.Top;
                m_Title.TextAlign = ContentAlignment.BottomLeft;
                m_Title.Padding = new Padding(32, 0, 0, 10);
                m_Title.AutoSize = false;

                m_Body.Dock = DockStyle.Fill;
                m_Body.Padding = new Padding(32, 4, 24, 16);
                m_Column.Dock = DockStyle.Left;
                m_Body.Controls.Add(m_Column);
                m_Body.Resize += (s, e) => FitColumn();
                foreach (PageEntry entry in m_Pages)
                {
                    entry.Page.Dock = DockStyle.Fill;
                    entry.Page.Visible = false;
                    m_Column.Controls.Add(entry.Page);
                }

                Controls.Add(m_Body);
                Controls.Add(m_Title);
                Controls.Add(m_Sidebar);
                m_Sidebar.SendToBack();
                m_Title.BringToFront();
                m_Body.BringToFront();
                FontChanged += (s, e) => FitSizes();
                FitSizes();
                ShowPage(Math.Min(s_LastPage, m_Pages.Count - 1), false);
            }

            private void AddPage(string key, string title, string icon, Control page, Action shown)
            {
                m_Pages.Add(new PageEntry { Key = key, Title = title, Icon = icon, Page = page, Shown = shown, Group = m_Group });
                m_Entries.Add(new SidebarEntry { Title = title, Icon = icon, Page = m_Pages.Count - 1, Child = m_Group != null });
            }

            private void AddGroup(string title, string icon)
            {
                m_Group = title;
                m_Entries.Add(new SidebarEntry { Title = title, Icon = icon });
            }

            // a service: its own window (remote control, Discord ...) shown inside the page, using all of the space
            private void AddService(string key, string title, string icon, Action open)
            {
                Panel host = new Panel();
                host.AutoScroll = true;
                int index = m_Pages.Count;
                System.Diagnostics.Stopwatch opened = new System.Diagnostics.Stopwatch();
                Action show = null;
                show = () =>
                {
                    if (host.Controls.Count > 0)
                        return;
                    m_Owner.m_EmbedHost = host;
                    opened.Restart();
                    try { open(); }
                    finally { m_Owner.m_EmbedHost = null; }
                    if (host.Controls.Count == 0)
                    {
                        Label none = new Label();
                        none.Text = Loc.T("Este recurso não está disponível agora.", "This feature is not available now.");
                        none.Tag = "Muted";
                        none.AutoSize = true;
                        none.Location = new Point(0, 8);
                        host.Controls.Add(none);
                    }
                };
                // the window was closed by one of its own buttons: it opens again, fresh (unless it closed at once)
                host.Tag = (Action)(() =>
                {
                    if (m_Selected == index && m_Owner.m_ShowingSettings && opened.ElapsedMilliseconds > 700)
                        show();
                });
                AddPage(key, title, icon, host, show);
            }

            private void AddVdoPage()
            {
                Panel host = new Panel();
                Label text = new Label();
                text.Text = Loc.T(
                    "O VDO.Ninja envia o áudio do Apolo pela internet (para o Foundry e para os jogadores). Ele roda numa aba do navegador do Apolo, por isso abre ali e não aqui.\n\nO botão \"Enviar áudio\" do cabeçalho faz a mesma coisa.",
                    "VDO.Ninja sends the audio of Apolo over the internet (to Foundry and the players). It runs in a tab of the Apolo browser, so it opens there and not here.\n\nThe \"Send audio\" button of the header does the same.");
                text.AutoSize = false;
                text.SetBounds(0, 8, 640, 110);
                Button open = new Button();
                open.Text = Loc.T("Abrir a aba de envio de áudio", "Open the audio sending tab");
                open.AutoSize = true;
                open.MinimumSize = new Size(240, DialogKit.ButtonHeight);
                open.Padding = new Padding(10, 0, 10, 0);
                open.Location = new Point(0, 128);
                DialogKit.Primary(open);
                open.Click += (s, e) => m_Owner.ShowWebWindow(true);
                Label marginText = new Label();
                marginText.Text = Loc.T(
                    "Margem de segurança do áudio enviado: quanto som das abas (YouTube etc.) o Apolo junta antes de enviar. Mais margem evita picotes quando o PC está pesado, ao custo de um pequeno atraso. Vale a partir da próxima vez que o envio for aberto.",
                    "Safety margin of the sent audio: how much sound of the tabs (YouTube etc.) Apolo collects before sending. More margin avoids stutter when the PC is busy, at the cost of a small delay. It applies the next time sending is opened.");
                marginText.AutoSize = false;
                marginText.SetBounds(0, 190, 640, 70);
                NumericUpDown margin = new NumericUpDown();
                margin.Minimum = AppAudioCapture.MinMarginMs;
                margin.Maximum = AppAudioCapture.MaxMarginMs;
                margin.Increment = 100;
                margin.Value = AppAudioCapture.MarginMs;
                margin.Width = 100;
                margin.Location = new Point(0, 268);
                margin.ValueChanged += (s, e) => Ares.CommonGUI.Preferences.Set("AudioSendMargin", ((int)margin.Value).ToString());
                Label unit = new Label();
                unit.Text = Loc.T("ms (padrão 300; use 600 a 1000 se ainda picotar)", "ms (default 300; use 600 to 1000 if it still stutters)");
                unit.AutoSize = true;
                unit.Location = new Point(110, 272);
                Panel content = new Panel();
                content.Size = new Size(660, 300);
                content.Controls.Add(text);
                content.Controls.Add(open);
                content.Controls.Add(marginText);
                content.Controls.Add(margin);
                content.Controls.Add(unit);
                host.AutoScroll = true;
                PutInCard(host, content);
                AddPage("vdo", "VDO.Ninja", "send", host, null);
            }

            public int IndexOfKey(string key)
            {
                return m_Pages.FindIndex(p => p.Key == key);
            }

            private void FitSizes()
            {
                m_Sidebar.Width = Math.Max(250, Font.Height * 16);
                m_Title.Height = Math.Max(64, Font.Height * 4);
                m_Title.Font = new Font(Font.FontFamily, Font.Size + 7f, FontStyle.Bold);
                FitColumn();
            }

            // the page uses the width of the window, up to a comfortable line length
            private void FitColumn()
            {
                m_Column.Width = Math.Max(300, Math.Min(1040, m_Body.ClientSize.Width - m_Body.Padding.Horizontal));
            }

            public void SelectPage(int index)
            {
                ShowPage(index, true);
            }

            /// <summary>The page in front is shown again (the tab was opened).</summary>
            public void RefreshCurrent()
            {
                if (m_Selected >= 0 && m_Pages[m_Selected].Shown != null)
                    m_Pages[m_Selected].Shown();
            }

            private void ShowPage(int index, bool notify)
            {
                if (index < 0 || index >= m_Pages.Count)
                    index = 0;
                if (m_Selected >= 0)
                    m_Pages[m_Selected].Page.Visible = false;
                m_Selected = index;
                s_LastPage = index;
                PageEntry entry = m_Pages[index];
                m_Title.Text = entry.Group != null ? entry.Group + "  ›  " + entry.Title : entry.Title;
                entry.Page.Visible = true;
                m_Sidebar.Current = index;
                if (notify && entry.Shown != null)
                    entry.Shown();
            }
        }

        // the list of pages at the left (with groups)
        private class Sidebar : Control
        {
            private List<SidebarEntry> m_Entries = new List<SidebarEntry>();
            private int m_Current, m_Hot = -1;

            public event Action<int> Selected;

            public Sidebar()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                    | ControlStyles.Selectable, true);
                TabStop = true;
                ThemeManager.Changed += (s, e) => Invalidate();
            }

            public void SetItems(List<SidebarEntry> entries)
            {
                m_Entries = entries;
                Invalidate();
            }

            public int Current
            {
                get { return m_Current; }
                set { m_Current = value; Invalidate(); }
            }

            private int RowHeight { get { return Math.Max(44, Font.Height + 26); } }

            private Rectangle RowRect(int i)
            {
                return new Rectangle(0, 16 + i * RowHeight, Width - 1, RowHeight);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                ThemePalette p = ThemeManager.Current;
                bool themed = p != null && !p.IsClassic;
                Color back = themed ? p.Surface : SystemColors.Window;
                Color text = themed ? p.Text : SystemColors.ControlText;
                Color dim = themed ? p.TextDim : SystemColors.GrayText;
                Color accent = themed ? p.Accent : SystemColors.Highlight;
                e.Graphics.Clear(back);
                for (int i = 0; i < m_Entries.Count; i++)
                {
                    SidebarEntry entry = m_Entries[i];
                    Rectangle r = RowRect(i);
                    bool header = entry.Page < 0;
                    bool selected = !header && entry.Page == m_Current;
                    if (!header && (selected || i == m_Hot))
                    {
                        using (SolidBrush fill = new SolidBrush(themed ? (selected ? p.Selection : p.Hover) : SystemColors.ControlLight))
                            e.Graphics.FillRectangle(fill, r);
                    }
                    if (selected)
                    {
                        using (SolidBrush bar = new SolidBrush(accent))
                            e.Graphics.FillRectangle(bar, r.X, r.Y, 4, r.Height);
                    }
                    int x = entry.Child ? 46 : 24;
                    int iconSize = entry.Child ? 18 : 22;
                    using (Bitmap icon = IconLibrary.Get(entry.Icon, new Size(iconSize, iconSize), selected || header ? accent : text))
                    {
                        if (icon != null)
                            e.Graphics.DrawImage(icon, x, r.Y + (r.Height - iconSize) / 2, iconSize, iconSize);
                    }
                    Rectangle label = new Rectangle(x + iconSize + 12, r.Y, Math.Max(10, r.Width - x - iconSize - 18), r.Height);
                    using (Font font = new Font(Font, selected || header ? FontStyle.Bold : FontStyle.Regular))
                        TextRenderer.DrawText(e.Graphics, entry.Title, font, label, header ? dim : text,
                            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.Left);
                }
                using (Pen line = new Pen(themed ? p.Border : SystemColors.ControlDark))
                    e.Graphics.DrawLine(line, Width - 1, 0, Width - 1, Height);
                int focusRow = RowOfPage(m_Current);
                if (Focused && ShowFocusCues && focusRow >= 0)
                {
                    Rectangle r = RowRect(focusRow);
                    r.Inflate(-2, -2);
                    ControlPaint.DrawFocusRectangle(e.Graphics, r);
                }
            }

            private int RowOfPage(int page)
            {
                for (int i = 0; i < m_Entries.Count; i++)
                    if (m_Entries[i].Page == page)
                        return i;
                return -1;
            }

            private int IndexAt(Point point)
            {
                for (int i = 0; i < m_Entries.Count; i++)
                    if (RowRect(i).Contains(point))
                        return i;
                return -1;
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                int hot = IndexAt(e.Location);
                if (hot >= 0 && m_Entries[hot].Page < 0)
                    hot = -1; // a group title is not a page
                if (hot != m_Hot)
                {
                    m_Hot = hot;
                    Cursor = hot >= 0 ? Cursors.Hand : Cursors.Default;
                    Invalidate();
                }
                base.OnMouseMove(e);
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                m_Hot = -1;
                Invalidate();
                base.OnMouseLeave(e);
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                Focus();
                int index = IndexAt(e.Location);
                if (index >= 0)
                {
                    // the title of a group leads to its first page
                    int page = m_Entries[index].Page;
                    for (int i = index; page < 0 && i < m_Entries.Count; i++)
                        page = m_Entries[i].Page;
                    if (page >= 0)
                        Choose(page);
                }
                base.OnMouseDown(e);
            }

            private void Choose(int page)
            {
                Action<int> handler = Selected;
                if (handler != null)
                    handler(page);
            }

            protected override bool IsInputKey(Keys keyData)
            {
                return keyData == Keys.Up || keyData == Keys.Down || base.IsInputKey(keyData);
            }

            protected override void OnKeyDown(KeyEventArgs e)
            {
                int pages = 0;
                foreach (SidebarEntry entry in m_Entries)
                    if (entry.Page >= 0)
                        pages++;
                if (e.KeyCode == Keys.Down && m_Current < pages - 1)
                    Choose(m_Current + 1);
                else if (e.KeyCode == Keys.Up && m_Current > 0)
                    Choose(m_Current - 1);
                base.OnKeyDown(e);
            }

            protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
            protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        }

        // ------------------------------------------------------------------ the pages

        // a group of settings: a title and a rounded outline around it, so that the sets are told apart
        private class CardPanel : FlowLayoutPanel
        {
            public CardPanel()
            {
                DoubleBuffered = true;
                FlowDirection = FlowDirection.TopDown;
                WrapContents = false;
                AutoSize = true;
                AutoSizeMode = AutoSizeMode.GrowAndShrink;
                Padding = new Padding(18, 14, 18, 8);
                Margin = new Padding(0, 0, 0, 16);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                ThemePalette p = ThemeManager.Current;
                Color line = p != null && !p.IsClassic ? p.Border : SystemColors.ControlDark;
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
                int d = 14;
                using (System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath())
                {
                    path.AddArc(r.X, r.Y, d, d, 180, 90);
                    path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                    path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                    path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                    path.CloseFigure();
                    using (Pen pen = new Pen(line, 1.5f))
                        e.Graphics.DrawPath(pen, path);
                }
            }
        }

        private abstract class SettingsPage
        {
            protected readonly Player Owner;
            private readonly FlowLayoutPanel m_Flow = new FlowLayoutPanel();
            private readonly string m_Title;
            private CardPanel m_Card;

            protected SettingsPage(Player owner, string title)
            {
                Owner = owner;
                m_Title = title;
                m_Flow.FlowDirection = FlowDirection.TopDown;
                m_Flow.WrapContents = false;
                m_Flow.AutoScroll = true;
                m_Flow.Padding = new Padding(0, 4, 0, 12);
                m_Flow.Resize += (s, e) => FitWidths();
            }

            public Control Page { get { return m_Flow; } }
            public string PageTitle { get { return m_Title; } }

            /// <summary>The page was brought to the front.</summary>
            public virtual void OnPageShown() { }

            private int ContentWidth
            {
                get { return Math.Max(200, m_Flow.ClientSize.Width - m_Flow.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth); }
            }

            private static void FitIn(FlowLayoutPanel flow, int w)
            {
                flow.SuspendLayout();
                foreach (Control c in flow.Controls)
                {
                    Label l = c as Label;
                    CardPanel card = c as CardPanel;
                    if (l != null && l.AutoSize)
                        l.MaximumSize = new Size(w, 0);
                    else if (card != null)
                    {
                        card.MinimumSize = new Size(w, 0);
                        FitIn(card, w - card.Padding.Horizontal);
                    }
                    else if (!(c is FlowLayoutPanel && ((FlowLayoutPanel)c).AutoSize))
                        c.Width = w;
                }
                flow.ResumeLayout(true);
            }

            private void FitWidths()
            {
                FitIn(m_Flow, ContentWidth);
            }

            protected void Add(Control c)
            {
                if (m_Card != null)
                    m_Card.Controls.Add(c);
                else
                    m_Flow.Controls.Add(c);
                FitWidths();
            }

            /// <summary>Starts a new group (a card) with a title; what is added next goes inside it.</summary>
            protected void Heading(string text)
            {
                m_Card = new CardPanel();
                m_Flow.Controls.Add(m_Card);
                Label l = new Label();
                l.AutoSize = true;
                l.Text = text;
                l.Font = new Font(m_Flow.Font.FontFamily, m_Flow.Font.Size + 1f, FontStyle.Bold);
                l.Margin = new Padding(0, 0, 0, 8);
                m_Card.Controls.Add(l);
                FitWidths();
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
                l.SetBounds(0, 0, 250, DialogKit.FieldHeight + 6);
                p.Controls.Add(l);
                int x = 260;
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

            protected ThemedComboBox Combo(IEnumerable<string> items, int selected, Action<int> changed)
            {
                ThemedComboBox c = new ThemedComboBox();
                foreach (string s in items)
                    c.Items.Add(s);
                c.Width = 320;
                c.ItemHeight = Math.Max(16, DialogKit.FieldHeight - 8);
                if (selected >= 0 && selected < c.Items.Count)
                    c.SelectedIndex = selected;
                c.SelectedIndexChanged += (s, e) => { if (c.SelectedIndex >= 0) changed(c.SelectedIndex); };
                return c;
            }

            /// <summary>An empty area of a given height, where a window of Apolo is shown inside the page.</summary>
            protected Panel AddHost(int height)
            {
                Panel host = new Panel();
                host.Height = height;
                host.AutoScroll = true;
                host.Margin = new Padding(0, 4, 0, 8);
                Add(host);
                return host;
            }
        }

        // ---- General: look, language, audio output, where the settings are kept ----

        private class GeneralPage : SettingsPage
        {
            private Panel m_Old;
            private SettingsDialog m_Legacy;

            // the rows of the old page are laid out with the font they have now (the card cut off its last row)
            private void FitOld()
            {
                if (m_Old != null && !m_Old.IsDisposed)
                    m_Old.Height = Math.Max(60, m_Legacy.FitGeneralPage(m_Old, Math.Max(m_Old.AutoScrollMinSize.Width, m_Old.Width)));
            }

            public override void OnPageShown()
            {
                FitOld();
            }

            public GeneralPage(Player owner, SettingsDialog legacy) : base(owner, Loc.T("Geral", "General"))
            {
                ToolStripMenuItem appearance = Owner.m_AppearanceItem;
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
                    List<ToolStripMenuItem> languages = language.DropDownItems.OfType<ToolStripMenuItem>().ToList();
                    Row(Loc.T("Idioma", "Language"), Combo(languages.Select(i => i.Text), languages.FindIndex(i => i.Checked), index => languages[index].PerformClick()));
                }
                if (Owner.m_CloseToTrayItem != null)
                {
                    ToolStripMenuItem tray = Owner.m_CloseToTrayItem;
                    Check(tray.Text, tray.Checked, value => tray.Checked = value);
                }

                Heading(Loc.T("Saída de áudio", "Audio output"));
                List<Ares.Playing.AudioInit.OutputDevice> devices = null;
                try { devices = Ares.Playing.AudioInit.GetActiveDevices(); }
                catch (Exception swallowed) { Ares.Settings.ErrorLog.Note(swallowed); }
                if (devices != null && devices.Count > 0)
                {
                    List<Ares.Playing.AudioInit.OutputDevice> list = devices;
                    int now = list.FindIndex(d => d.Index == Ares.Settings.Settings.Instance.OutputDeviceIndex);
                    ThemedComboBox box = Combo(list.Select(d => d.Name), now, index => Owner.ChangeOutputDevice(list[index].Index));
                    box.Width = 420;
                    Row(Loc.T("Dispositivo", "Device"), box);
                    Note(Loc.T("Onde o Apolo toca. Também dá para trocar pelo botão no canto direito da barra de baixo.",
                        "Where Apolo plays. It can also be changed with the button at the right end of the bottom bar."));
                }
                else
                {
                    Note(Loc.T("Nenhum dispositivo de saída foi encontrado.", "No output device was found."));
                }

                Heading(Loc.T("Arquivos do Apolo", "Apolo files"));
                Note(Loc.T("Faz os arquivos .apolo e .apkg abrirem com o Apolo (duplo clique).", "Makes .apolo and .apkg files open with Apolo (double click)."));
                AddButton(Loc.T("Associar arquivos .apolo ao Apolo", "Open .apolo files with Apolo"), Owner.AssociateProjectFiles);

                // the old "General" page: where the settings are kept
                Heading(Loc.T("Onde guardar as configurações", "Where the settings are kept"));
                Panel old = legacy.TakePage(0) as Panel;
                if (old != null)
                {
                    old.Height = Math.Max(60, old.AutoScrollMinSize.Height);
                    old.AutoScroll = false;
                    // measured with the font of the old dialog: with the larger font of the page the texts were cut ("Pasta do usuári")
                    // and the last row did not fit
                    old.FontChanged += (s, e) => FitOld();
                    m_Old = old;
                    m_Legacy = legacy;
                    old.Margin = new Padding(0, 0, 0, 8);
                    Add(old);
                }
            }
        }

        // ---- Keyboard and floating pad ----

        private class KeyboardPage : SettingsPage
        {
            private readonly Panel m_ShortcutsHost;
            private bool m_First = true;

            public KeyboardPage(Player owner) : base(owner, Loc.T("Teclado e pad", "Keyboard and pad"))
            {
                Heading(Loc.T("Pad flutuante", "Floating pad"));
                Note(Loc.T("Os botões de uma pasta aparecem por cima da tela, meio transparentes, com um atalho que funciona mesmo com o Apolo em segundo plano.",
                    "The buttons of a folder appear on top of the screen, slightly transparent, with a shortcut which works even when Apolo is in the background."));
                AddButton(Loc.T("Mostrar / esconder o pad", "Show / hide the pad"), Owner.TogglePad);
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

                Heading(Loc.T("Atalhos de teclado", "Keyboard shortcuts"));
                Check(Owner.showKeysMenuItem.Text, Owner.showKeysMenuItem.Checked, value => Owner.showKeysMenuItem.Checked = value);
                Note(Loc.T("Mostra o atalho de cada pasta e de cada botão num pequeno cartão no canto.",
                    "Shows the shortcut of every folder and button in a small card in the corner."));
                Check(Owner.globalKeyHookItem.Text, Owner.globalKeyHookItem.Checked, value => Owner.globalKeyHookItem.Checked = value)
                    .Enabled = Owner.globalKeyHookItem.Enabled;
                AddButton(Loc.T("Mostrar a lista de atalhos de novo", "Show the list of shortcuts again"), ShowShortcuts);
                m_ShortcutsHost = AddHost(540);
            }

            private void ShowShortcuts()
            {
                Owner.m_EmbedHost = m_ShortcutsHost;
                Owner.ShowShortcutsDialog();
                Owner.m_EmbedHost = null;
            }

            public override void OnPageShown()
            {
                if (!m_First)
                    return;
                m_First = false;
                ShowShortcuts();
            }
        }
    }
}
