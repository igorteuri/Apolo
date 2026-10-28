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
            if (m_ShowingSettings)
            {
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
            if (pageIndex >= 0)
                m_SettingsView.SelectPage(pageIndex);
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
                form.Dock = DockStyle.Fill;
            host.Controls.Add(form);
            // the buttons which closed the dialog (OK, Close ...) close the embedded window
            Action<Control> hook = null;
            hook = c =>
            {
                foreach (Control child in c.Controls)
                {
                    Button b = child as Button;
                    if (b != null && b.DialogResult != DialogResult.None)
                        b.Click += (s, e) => form.BeginInvoke(new MethodInvoker(form.Close));
                    hook(child);
                }
            };
            hook(form);
            form.FormClosed += (s, e) =>
            {
                if (cleanup != null)
                    cleanup();
            };
            ThemeManager.Apply(form);
            host.AutoScrollMinSize = resizable ? Size.Empty : form.Size;
            form.Show();
        }

        // ------------------------------------------------------------------ the view

        private class SettingsView : Panel
        {
            private class PageEntry
            {
                public string Title, Icon;
                public Control Page;
                public SettingsPage Source;
            }

            private readonly Player m_Owner;
            private readonly List<PageEntry> m_Pages = new List<PageEntry>();
            private readonly Sidebar m_Sidebar = new Sidebar();
            private readonly Label m_Title = new Label();
            private readonly Panel m_Body = new Panel();
            private readonly Panel m_Column = new Panel();
            private int m_Selected = -1;
            private static int s_LastPage;

            /// <summary>The old dialog, never shown: it keeps the General and Music pages and saves them.</summary>
            public readonly SettingsDialog Legacy;

            public SettingsView(Player owner)
            {
                m_Owner = owner;
                Legacy = new SettingsDialog(Ares.Settings.Settings.Instance, owner.m_BasicSettings);
                Legacy.AddPage(new MusicPageHost());
                AddPage(Legacy.TitleOfPage(0), "sliders-horizontal", Legacy.TakePage(0), null);
                AddPage(Legacy.TitleOfPage(1), "music", Legacy.TakePage(1), null);
                SettingsPage ui = new InterfacePage(owner);
                AddPage(ui.PageTitle, "eye", ui.Page, ui);
                SettingsPage keys = new KeyboardPage(owner);
                AddPage(keys.PageTitle, "keyboard", keys.Page, keys);
                SettingsPage connections = new ConnectionsPage(owner);
                AddPage(connections.PageTitle, "link", connections.Page, connections);

                m_Sidebar.Dock = DockStyle.Left;
                m_Sidebar.SetItems(m_Pages.Select(p => p.Title).ToList(), m_Pages.Select(p => p.Icon).ToList());
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
                SelectPage(Math.Min(s_LastPage, m_Pages.Count - 1));
            }

            private void AddPage(string title, string icon, Control page, SettingsPage source)
            {
                m_Pages.Add(new PageEntry { Title = title, Icon = icon, Page = page, Source = source });
            }

            private void FitSizes()
            {
                m_Sidebar.Width = Math.Max(230, Font.Height * 15);
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
                if (index < 0 || index >= m_Pages.Count)
                    index = 0;
                if (m_Selected >= 0)
                    m_Pages[m_Selected].Page.Visible = false;
                m_Selected = index;
                s_LastPage = index;
                PageEntry entry = m_Pages[index];
                m_Title.Text = entry.Title;
                entry.Page.Visible = true;
                m_Sidebar.Current = index;
                if (entry.Source != null)
                    entry.Source.OnPageShown();
            }
        }

        // the list of pages at the left
        private class Sidebar : Control
        {
            private List<string> m_Titles = new List<string>(), m_Icons = new List<string>();
            private int m_Current, m_Hot = -1;

            public event Action<int> Selected;

            public Sidebar()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                    | ControlStyles.Selectable, true);
                TabStop = true;
                ThemeManager.Changed += (s, e) => Invalidate();
            }

            public void SetItems(List<string> titles, List<string> icons)
            {
                m_Titles = titles;
                m_Icons = icons;
                Invalidate();
            }

            public int Current
            {
                get { return m_Current; }
                set { m_Current = value; Invalidate(); }
            }

            private int RowHeight { get { return Math.Max(46, Font.Height + 28); } }

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
                Color accent = themed ? p.Accent : SystemColors.Highlight;
                e.Graphics.Clear(back);
                for (int i = 0; i < m_Titles.Count; i++)
                {
                    Rectangle r = RowRect(i);
                    bool selected = i == m_Current;
                    if (selected || i == m_Hot)
                    {
                        using (SolidBrush fill = new SolidBrush(themed ? (selected ? p.Selection : p.Hover) : SystemColors.ControlLight))
                            e.Graphics.FillRectangle(fill, r);
                    }
                    if (selected)
                    {
                        using (SolidBrush bar = new SolidBrush(accent))
                            e.Graphics.FillRectangle(bar, r.X, r.Y, 4, r.Height);
                    }
                    int x = 24;
                    using (Bitmap icon = IconLibrary.Get(m_Icons[i], new Size(22, 22), selected ? accent : text))
                    {
                        if (icon != null)
                            e.Graphics.DrawImage(icon, x, r.Y + (r.Height - 22) / 2, 22, 22);
                    }
                    Rectangle label = new Rectangle(x + 22 + 14, r.Y, r.Width - x - 22 - 20, r.Height);
                    using (Font font = new Font(Font, selected ? FontStyle.Bold : FontStyle.Regular))
                        TextRenderer.DrawText(e.Graphics, m_Titles[i], font, label, text,
                            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.Left);
                }
                using (Pen line = new Pen(themed ? p.Border : SystemColors.ControlDark))
                    e.Graphics.DrawLine(line, Width - 1, 0, Width - 1, Height);
                if (Focused && ShowFocusCues && m_Titles.Count > 0)
                {
                    Rectangle r = RowRect(m_Current);
                    r.Inflate(-2, -2);
                    ControlPaint.DrawFocusRectangle(e.Graphics, r);
                }
            }

            private int IndexAt(Point point)
            {
                for (int i = 0; i < m_Titles.Count; i++)
                    if (RowRect(i).Contains(point))
                        return i;
                return -1;
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                int hot = IndexAt(e.Location);
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
                    Choose(index);
                base.OnMouseDown(e);
            }

            private void Choose(int index)
            {
                Action<int> handler = Selected;
                if (handler != null)
                    handler(index);
            }

            protected override bool IsInputKey(Keys keyData)
            {
                return keyData == Keys.Up || keyData == Keys.Down || base.IsInputKey(keyData);
            }

            protected override void OnKeyDown(KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Down && m_Current < m_Titles.Count - 1)
                    Choose(m_Current + 1);
                else if (e.KeyCode == Keys.Up && m_Current > 0)
                    Choose(m_Current - 1);
                base.OnKeyDown(e);
            }

            protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
            protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
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
                m_Flow.Padding = new Padding(0, 4, 0, 12);
                m_Flow.Resize += (s, e) => FitWidths();
            }

            public Control Page { get { return m_Flow; } }
            public string PageTitle { get { return m_Title; } }
            public void OnConfirm() { }

            /// <summary>The page was brought to the front.</summary>
            public virtual void OnPageShown() { }

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
                    else if (!(c is FlowLayoutPanel && ((FlowLayoutPanel)c).AutoSize))
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
                l.Margin = new Padding(0, m_Flow.Controls.Count == 0 ? 8 : 24, 0, 6);
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
                c.Width = 260;
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

        // ---- Connections ----

        private class ConnectionsPage : SettingsPage
        {
            private readonly Panel m_Host;
            private readonly Label m_Hint = new Label();

            public ConnectionsPage(Player owner) : base(owner, Loc.T("Conexões", "Connections"))
            {
                Note(Loc.T("Tudo o que liga o Apolo a outros programas e pessoas. Escolha um serviço para ajustá-lo aqui mesmo.",
                    "Everything which connects Apolo to other programs and people. Choose a service to set it up right here."));
                FlowLayoutPanel chips = new FlowLayoutPanel();
                chips.AutoSize = true;
                chips.AutoSizeMode = AutoSizeMode.GrowAndShrink;
                chips.WrapContents = true;
                chips.Margin = new Padding(0, 0, 0, 8);
                m_Host = new Panel();
                Chip(chips, Loc.T("Controle remoto", "Remote control"), Owner.ShowRemoteDialog);
                Chip(chips, "Stream Deck", Owner.ShowDeckDialog);
                Chip(chips, "Discord", Owner.ShowDiscordDialog);
                Chip(chips, Loc.T("Overlay do OBS", "OBS overlay"), Owner.ShowOverlayDialog);
                Chip(chips, "Foundry VTT", Owner.ShowFoundryHelp);
                Button vdo = MakeButton("VDO.Ninja", () => Owner.ShowWebWindow(true), false);
                vdo.AutoSize = false;
                vdo.Size = new Size(Math.Max(110, TextRenderer.MeasureText("VDO.Ninja", vdo.Font).Width + 32), DialogKit.ButtonHeight);
                vdo.Margin = new Padding(0, 0, 8, 8);
                chips.Controls.Add(vdo);
                Add(chips);

                m_Host.Height = 660;
                m_Host.AutoScroll = true;
                m_Host.Margin = new Padding(0, 0, 0, 8);
                m_Hint.Text = Loc.T("Escolha um serviço acima. \"VDO.Ninja\" abre a aba de envio de áudio.", "Choose a service above. \"VDO.Ninja\" opens the audio sending tab.");
                m_Hint.Tag = "Muted";
                m_Hint.AutoSize = true;
                m_Hint.Location = new Point(0, 8);
                m_Host.Controls.Add(m_Hint);
                Add(m_Host);
            }

            private void Chip(FlowLayoutPanel parent, string title, Action open)
            {
                RadioButton chip = new RadioButton();
                chip.Appearance = Appearance.Button;
                chip.Text = title;
                chip.AutoSize = false;
                chip.TextAlign = ContentAlignment.MiddleCenter;
                chip.Size = new Size(Math.Max(110, TextRenderer.MeasureText(title, chip.Font).Width + 32), DialogKit.ButtonHeight);
                chip.Margin = new Padding(0, 0, 8, 8);
                chip.CheckedChanged += (s, e) =>
                {
                    if (!chip.Checked)
                        return;
                    Owner.m_EmbedHost = m_Host;
                    open();
                    Owner.m_EmbedHost = null;
                };
                parent.Controls.Add(chip);
            }
        }
    }
}
