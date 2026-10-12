using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Ares.Player
{
    // Web pages inside Ares: a separate Ares window with one tab per favorite page
    // (YouTube, soundboards ...) and a special tab that sends the Ares audio through VDO.Ninja.
    public partial class Player
    {
        private WebWindow m_WebWindow;

        private void InitWeb()
        {
            Ares.CommonGUI.ThemeManager.AfterAppearanceMenu = (form, menu) =>
            {
                if (form != this || menu != menuStrip1)
                    return;
                foreach (ToolStripItem existing in menu.Items)
                {
                    if (existing.Name == "webMenuItem")
                        return;
                }
                ToolStripMenuItem web = new ToolStripMenuItem(T("Páginas web", "Web pages"));
                web.Name = "webMenuItem";
                web.Click += (s, e) => ShowWebWindow(false);
                ToolStripMenuItem vdo = new ToolStripMenuItem(T("Enviar VDO", "Send VDO"));
                vdo.Name = "vdoMenuItem";
                vdo.Click += (s, e) => ShowWebWindow(true);
                menu.Items.Add(web);
                menu.Items.Add(vdo);
            };
            FormClosed += (s, e) =>
            {
                if (m_WebWindow != null && !m_WebWindow.IsDisposed)
                    m_WebWindow.ForceClose();
            };
        }

        private void ShowWebWindow(bool vdo)
        {
            if (m_WebWindow == null || m_WebWindow.IsDisposed)
                m_WebWindow = new WebWindow(this);
            m_WebWindow.Present(vdo);
        }

        internal void BringToFrontFromWeb()
        {
            RestoreFromTray();
        }
    }

    internal class WebTab
    {
        public string Name;
        public string Url;
        public string Kind;   // "web" or "vdo"
        public TabPage Page;
        public WebView2 View;
        public bool Started;
    }

    internal class WebWindow : Form
    {
        private readonly Player m_Player;
        private readonly List<WebTab> m_Tabs = new List<WebTab>();
        private TabControl m_TabControl;
        private TextBox m_Address;
        private Button m_Back, m_Forward, m_Reload, m_Add, m_ToAres, m_VdoSettings, m_VdoCopy;
        private bool m_Force;
        private static Task<CoreWebView2Environment> s_Env;

        private static string T(string pt, string en)
        {
            return System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt" ? pt : en;
        }

        private static string ConfigFile
        {
            get
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Ares");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "webtabs.cfg");
            }
        }

        public WebWindow(Player player)
        {
            m_Player = player;
            Text = T("Ares - Páginas web", "Ares - Web pages");
            Icon = player.Icon;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1150, 760);

            Panel bar = new Panel();
            bar.Dock = DockStyle.Top;
            bar.Height = 38;
            bar.Padding = new Padding(4);

            m_ToAres = MakeBarButton(T("◀ Ares", "◀ Ares"), 4, 80, (s, e) => m_Player.BringToFrontFromWeb());
            m_Back = MakeBarButton("←", 88, 36, (s, e) => { WebTab t = Current(); if (t != null && t.View != null && t.View.CanGoBack) t.View.GoBack(); });
            m_Forward = MakeBarButton("→", 128, 36, (s, e) => { WebTab t = Current(); if (t != null && t.View != null && t.View.CanGoForward) t.View.GoForward(); });
            m_Reload = MakeBarButton("⟳", 168, 36, (s, e) => { WebTab t = Current(); if (t != null && t.View != null && t.View.CoreWebView2 != null) t.View.CoreWebView2.Reload(); });
            m_Add = MakeBarButton(T("+ Favorito", "+ Favorite"), 0, 90, (s, e) => AddFavorite());
            m_VdoSettings = MakeBarButton(T("Config. VDO", "VDO settings"), 0, 100, (s, e) => ConfigureVdo());
            m_VdoCopy = MakeBarButton(T("Copiar link do ouvinte", "Copy listener link"), 0, 170, (s, e) => CopyListenerLink());
            m_Address = new TextBox();
            m_Address.Location = new Point(212, 8);
            m_Address.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            m_Address.KeyDown += (s, e) =>
            {
                if (e.KeyCode == System.Windows.Forms.Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    GoToAddress();
                }
            };
            bar.Controls.AddRange(new Control[] { m_ToAres, m_Back, m_Forward, m_Reload, m_Address, m_VdoCopy, m_VdoSettings, m_Add });
            bar.Resize += (s, e) => LayoutBar(bar);

            m_TabControl = new TabControl();
            m_TabControl.Dock = DockStyle.Fill;
            m_TabControl.Multiline = true;
            m_TabControl.SelectedIndexChanged += (s, e) => OnTabSelected();
            m_TabControl.MouseUp += TabMouseUp;

            Controls.Add(m_TabControl);
            Controls.Add(bar);
            LayoutBar(bar);

            LoadTabs();
            if (m_TabControl.TabCount > 0)
                OnTabSelected();
        }

        private Button MakeBarButton(string text, int left, int width, EventHandler click)
        {
            Button b = new Button();
            b.Text = text;
            b.Location = new Point(left, 5);
            b.Size = new Size(width, 28);
            b.Click += click;
            return b;
        }

        private void LayoutBar(Panel bar)
        {
            int right = bar.ClientSize.Width - 4;
            m_Add.Left = right - m_Add.Width;
            right = m_Add.Left - 6;
            m_VdoSettings.Left = right - m_VdoSettings.Width;
            right = m_VdoSettings.Left - 6;
            m_VdoCopy.Left = right - m_VdoCopy.Width;
            right = m_VdoCopy.Left - 6;
            m_Address.Width = Math.Max(100, right - m_Address.Left);
        }

        // ------------------------------------------------------------------ tabs

        private WebTab Current()
        {
            int i = m_TabControl.SelectedIndex;
            return (i >= 0 && i < m_Tabs.Count) ? m_Tabs[i] : null;
        }

        private WebTab AddTab(string name, string url, string kind, bool select)
        {
            WebTab tab = new WebTab { Name = name, Url = url, Kind = kind };
            tab.Page = new TabPage(name);
            m_Tabs.Add(tab);
            m_TabControl.TabPages.Add(tab.Page);
            if (select)
                m_TabControl.SelectedIndex = m_Tabs.Count - 1;
            return tab;
        }

        private void LoadTabs()
        {
            try
            {
                if (File.Exists(ConfigFile))
                {
                    foreach (string line in File.ReadAllLines(ConfigFile))
                    {
                        string[] parts = line.Split('\t');
                        if (parts.Length >= 3 && (parts[0] == "web" || parts[0] == "vdo"))
                            AddTab(parts[1], parts[2], parts[0], false);
                    }
                }
            }
            catch (Exception)
            {
            }
            if (m_Tabs.Count == 0)
            {
                AddTab("YouTube", "https://www.youtube.com", "web", false);
                SaveTabs();
            }
        }

        private void SaveTabs()
        {
            try
            {
                List<string> lines = new List<string>();
                foreach (WebTab t in m_Tabs)
                    lines.Add(t.Kind + "\t" + t.Name.Replace('\t', ' ') + "\t" + (t.Kind == "vdo" ? "" : t.Url.Replace('\t', ' ')));
                File.WriteAllLines(ConfigFile, lines.ToArray());
            }
            catch (Exception)
            {
            }
        }

        private void OnTabSelected()
        {
            WebTab t = Current();
            bool vdo = t != null && t.Kind == "vdo";
            m_VdoCopy.Visible = vdo;
            m_VdoSettings.Visible = vdo;
            if (t == null)
            {
                m_Address.Text = "";
                return;
            }
            StartTab(t);
            m_Address.Text = (t.View != null && t.View.Source != null) ? t.View.Source.ToString() : (t.Kind == "vdo" ? "" : t.Url);
        }

        private static Task<CoreWebView2Environment> GetEnvironment()
        {
            if (s_Env == null)
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Ares", "WebView2");
                CoreWebView2EnvironmentOptions options = new CoreWebView2EnvironmentOptions("--autoplay-policy=no-user-gesture-required");
                s_Env = CoreWebView2Environment.CreateAsync(null, dir, options);
            }
            return s_Env;
        }

        private async void StartTab(WebTab tab)
        {
            if (tab.Started)
                return;
            tab.Started = true;
            try
            {
                string url = tab.Kind == "vdo" ? ShareDialog.BuildPushUrl() : tab.Url;
                tab.View = new WebView2();
                tab.View.Dock = DockStyle.Fill;
                tab.Page.Controls.Add(tab.View);
                CoreWebView2Environment env = await GetEnvironment();
                await tab.View.EnsureCoreWebView2Async(env);
                CoreWebView2 core = tab.View.CoreWebView2;
                core.PermissionRequested += (s, e) =>
                {
                    if (e.PermissionKind == CoreWebView2PermissionKind.Microphone)
                        e.State = CoreWebView2PermissionState.Allow;
                };
                core.NewWindowRequested += (s, e) =>
                {
                    e.Handled = true;
                    core.Navigate(e.Uri);
                };
                tab.View.SourceChanged += (s, e) =>
                {
                    if (Current() == tab && tab.View.Source != null)
                        m_Address.Text = tab.View.Source.ToString();
                };
                core.Navigate(url);
            }
            catch (Exception ex)
            {
                s_Env = null;
                tab.Started = false;
                if (tab.View != null)
                {
                    tab.Page.Controls.Remove(tab.View);
                    tab.View.Dispose();
                    tab.View = null;
                }
                Label error = new Label();
                error.Dock = DockStyle.Fill;
                error.Padding = new Padding(20);
                error.Text = T(
                    "Não foi possível iniciar o navegador embutido (WebView2). Instale o \"Microsoft Edge WebView2 Runtime\" (microsoft.com/edge/webview2) e abra esta aba de novo.\n\n",
                    "The embedded browser (WebView2) could not be started. Install the \"Microsoft Edge WebView2 Runtime\" (microsoft.com/edge/webview2) and open this tab again.\n\n") + ex.Message;
                tab.Page.Controls.Clear();
                tab.Page.Controls.Add(error);
            }
        }

        private void GoToAddress()
        {
            WebTab t = Current();
            if (t == null || t.View == null || t.View.CoreWebView2 == null)
                return;
            string text = m_Address.Text.Trim();
            if (text.Length == 0)
                return;
            if (text.IndexOf("://", StringComparison.Ordinal) < 0)
            {
                if (text.IndexOf(' ') >= 0 || text.IndexOf('.') < 0)
                    text = "https://www.google.com/search?q=" + Uri.EscapeDataString(text);
                else
                    text = "https://" + text;
            }
            try
            {
                t.View.CoreWebView2.Navigate(text);
            }
            catch (Exception)
            {
            }
        }

        // ------------------------------------------------------------------ favorites

        private void AddFavorite()
        {
            string name = "";
            string url = "https://";
            WebTab t = Current();
            if (t != null && t.View != null && t.View.Source != null && t.Kind == "web")
            {
                url = t.View.Source.ToString();
                if (t.View.CoreWebView2 != null)
                    name = t.View.CoreWebView2.DocumentTitle;
            }
            if (WebTabDialog.Ask(this, T("Novo favorito", "New favorite"), ref name, ref url))
            {
                WebTab nt = AddTab(name, url, "web", true);
                SaveTabs();
            }
        }

        private void TabMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;
            for (int i = 0; i < m_TabControl.TabCount; ++i)
            {
                if (m_TabControl.GetTabRect(i).Contains(e.Location))
                {
                    ShowTabMenu(i, e.Location);
                    return;
                }
            }
        }

        private void ShowTabMenu(int index, Point where)
        {
            WebTab tab = m_Tabs[index];
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add(T("Renomear / alterar endereço ...", "Rename / change address ..."), null, (s, e) =>
            {
                string name = tab.Name;
                string url = tab.Kind == "vdo" ? "" : tab.Url;
                if (tab.Kind == "web" && tab.View != null && tab.View.Source != null)
                    url = tab.View.Source.ToString();
                if (WebTabDialog.Ask(this, tab.Name, ref name, ref url, tab.Kind == "vdo"))
                {
                    tab.Name = name;
                    tab.Page.Text = name;
                    if (tab.Kind == "web")
                        tab.Url = url;
                    SaveTabs();
                }
            });
            menu.Items.Add(T("Recarregar", "Reload"), null, (s, e) =>
            {
                if (tab.View != null && tab.View.CoreWebView2 != null)
                    tab.View.CoreWebView2.Reload();
            });
            menu.Items.Add(T("Remover aba", "Remove tab"), null, (s, e) => RemoveTab(tab));
            menu.Show(m_TabControl, where);
        }

        private void RemoveTab(WebTab tab)
        {
            if (MessageBox.Show(this, T("Remover a aba \"", "Remove the tab \"") + tab.Name + "\"?", "Ares",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            m_Tabs.Remove(tab);
            m_TabControl.TabPages.Remove(tab.Page);
            tab.Page.Dispose();
            SaveTabs();
            OnTabSelected();
        }

        // ------------------------------------------------------------------ VDO.Ninja

        /// <summary>Shows the window; with vdo=true it selects (and if needed creates) the VDO.Ninja sender tab.</summary>
        public void Present(bool vdo)
        {
            if (!Visible)
                Show();
            if (WindowState == FormWindowState.Minimized)
                WindowState = FormWindowState.Normal;
            Activate();
            if (!vdo)
                return;
            WebTab tab = m_Tabs.Find(t => t.Kind == "vdo");
            if (tab == null)
            {
                tab = AddTab("VDO.Ninja", "", "vdo", true);
                SaveTabs();
            }
            else
            {
                m_TabControl.SelectedIndex = m_Tabs.IndexOf(tab);
            }
            OnTabSelected();
        }

        private void ConfigureVdo()
        {
            WebTab tab = Current();
            using (ShareDialog dlg = new ShareDialog())
            {
                if (dlg.ShowDialog(this) == DialogResult.OK && tab != null && tab.Kind == "vdo"
                    && tab.View != null && tab.View.CoreWebView2 != null)
                {
                    tab.View.CoreWebView2.Navigate(ShareDialog.BuildPushUrl());
                }
            }
        }

        private void CopyListenerLink()
        {
            try
            {
                Clipboard.SetText(ShareDialog.BuildViewUrl());
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Ares", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ------------------------------------------------------------------ closing

        public void ForceClose()
        {
            m_Force = true;
            Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // closing the window only hides it, so that pages (and the VDO.Ninja stream) keep running
            if (!m_Force && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            base.OnFormClosing(e);
        }
    }

    internal class WebTabDialog : Form
    {
        private TextBox m_Name;
        private TextBox m_Url;

        public static bool Ask(IWin32Window owner, string title, ref string name, ref string url)
        {
            return Ask(owner, title, ref name, ref url, false);
        }

        public static bool Ask(IWin32Window owner, string title, ref string name, ref string url, bool nameOnly)
        {
            using (WebTabDialog dlg = new WebTabDialog(title, name, url, nameOnly))
            {
                if (dlg.ShowDialog(owner) != DialogResult.OK)
                    return false;
                string n = dlg.m_Name.Text.Trim();
                string u = dlg.m_Url.Text.Trim();
                if (n.Length == 0)
                    n = u;
                if (!nameOnly && u.Length > 0 && u.IndexOf("://", StringComparison.Ordinal) < 0)
                    u = "https://" + u;
                if (n.Length == 0 || (!nameOnly && u.Length == 0))
                    return false;
                name = n;
                url = u;
                return true;
            }
        }

        private WebTabDialog(string title, string name, string url, bool nameOnly)
        {
            bool pt = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt";
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Font;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(12);

            TableLayoutPanel table = new TableLayoutPanel();
            table.AutoSize = true;
            table.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            table.ColumnCount = 2;
            Controls.Add(table);

            table.Controls.Add(new Label { Text = pt ? "Nome da aba:" : "Tab name:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 10, 6) }, 0, 0);
            m_Name = new TextBox { Text = name, Width = 360 };
            table.Controls.Add(m_Name, 1, 0);
            m_Url = new TextBox { Text = url, Width = 360 };
            if (!nameOnly)
            {
                table.Controls.Add(new Label { Text = pt ? "Endereço:" : "Address:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 10, 6) }, 0, 1);
                table.Controls.Add(m_Url, 1, 1);
            }

            Button ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true };
            Button cancel = new Button { Text = pt ? "Cancelar" : "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
            FlowLayoutPanel buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 0) };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);
            table.Controls.Add(buttons, 0, 2);
            table.SetColumnSpan(buttons, 2);
            AcceptButton = ok;
            CancelButton = cancel;
        }
    }
}
