using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
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
        private WebAudioPanel m_WebAudioPanel;

        private void UpdateWebAudio()
        {
            if (m_WebAudioPanel == null)
                return;
            System.Collections.Generic.List<WebAudioInfo> infos = (m_WebWindow == null || m_WebWindow.IsDisposed)
                ? new System.Collections.Generic.List<WebAudioInfo>()
                : m_WebWindow.GetAudioStates();
            m_WebAudioPanel.Rebuild(infos);
            int h = m_WebAudioPanel.Height;
            m_WebAudioPanel.Top = groupBox3.ClientSize.Height - h - 4;
            int bottom = (h > 0) ? m_WebAudioPanel.Top - 4 : groupBox3.ClientSize.Height - 8;
            m_SoundPanel.Height = Math.Max(40, bottom - m_SoundPanel.Top);
        }

        private void InitWeb()
        {
            m_WebAudioPanel = new WebAudioPanel();
            m_WebAudioPanel.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            m_WebAudioPanel.Left = 8;
            m_WebAudioPanel.Width = Math.Max(50, groupBox3.ClientSize.Width - 16);
            groupBox3.Controls.Add(m_WebAudioPanel);
            WebWindow.StateChanged += UpdateWebAudio;

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
        public Panel Page;
        public ToolStripButton Button;
        public WebView2 View;
        public bool Started;
        public bool Playing;   // the page plays audio
        public bool Muted;     // muted by the user
        public bool Loaded;    // page loaded
    }

    internal class WebAudioInfo
    {
        public string Name;
        public bool Vdo;
        public bool Muted;
        public Action Toggle;
    }

    /// <summary>Small list in the Player window: which web tabs make (or send) sound, each with a mute button.</summary>
    internal class WebAudioPanel : Panel
    {
        private const int RowHeight = 28;

        public WebAudioPanel()
        {
            Height = 0;
            Visible = false;
        }

        private static string T(string pt, string en)
        {
            return System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt" ? pt : en;
        }

        public void Rebuild(System.Collections.Generic.List<WebAudioInfo> infos)
        {
            SuspendLayout();
            foreach (Control c in new System.Collections.Generic.List<Control>(Controls.Cast<Control>()))
            {
                Controls.Remove(c);
                c.Dispose();
            }
            for (int i = 0; i < infos.Count; ++i)
            {
                WebAudioInfo info = infos[i];
                Label label = new Label();
                label.AutoEllipsis = true;
                label.TextAlign = ContentAlignment.MiddleLeft;
                label.Text = (info.Vdo ? T("Enviando: ", "Sending: ") : T("Tocando: ", "Playing: ")) + info.Name
                    + (info.Muted ? T("  (mudo)", "  (muted)") : "");
                label.Tag = i;
                Button button = new Button();
                button.Text = info.Muted ? T("Ativar som", "Unmute") : T("Mutar", "Mute");
                button.Tag = i;
                button.Click += (s, e) => info.Toggle();
                Controls.Add(label);
                Controls.Add(button);
            }
            Height = infos.Count * RowHeight;
            Visible = infos.Count > 0;
            ResumeLayout();
            Relayout();
        }

        private void Relayout()
        {
            int buttonWidth = 96;
            foreach (Control c in Controls)
            {
                int row = (int)c.Tag;
                if (c is Button)
                    c.SetBounds(Width - buttonWidth, row * RowHeight + 1, buttonWidth, RowHeight - 3);
                else
                    c.SetBounds(0, row * RowHeight, Math.Max(10, Width - buttonWidth - 6), RowHeight);
            }
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            Relayout();
        }
    }

    internal class WebWindow : Form
    {
        private readonly Player m_Player;
        private readonly List<WebTab> m_Tabs = new List<WebTab>();
        private ToolStrip m_Strip;
        private Panel m_Host;
        private WebTab m_Selected;
        private TextBox m_Address;
        private Button m_Back, m_Forward, m_Reload, m_Add, m_ToAres, m_VdoSettings, m_VdoCopy, m_Mute;
        public static event Action StateChanged;

        // Registers the audio of the page, so that the sent sound can be muted without leaving the stream.
        private const string MuteScript = @"(function(){
 if (window.__aresMuteInit) return; window.__aresMuteInit = true;
 window.__aresMuted = false; var pcs = [], tracks = [];
 var Orig = window.RTCPeerConnection;
 if (Orig) { window.RTCPeerConnection = new Proxy(Orig, { construct: function(t, a, n) { var pc = Reflect.construct(t, a, n); pcs.push(pc); return pc; } }); }
 var md = navigator.mediaDevices; var gum = md && md.getUserMedia;
 if (gum) { md.getUserMedia = function() { return gum.apply(this, arguments).then(function(s) { s.getAudioTracks().forEach(function(t) { tracks.push(t); if (window.__aresMuted) t.enabled = false; }); return s; }); }; }
 var cl = MediaStreamTrack.prototype.clone;
 MediaStreamTrack.prototype.clone = function() { var c = cl.apply(this, arguments); if (c.kind === 'audio') { tracks.push(c); if (window.__aresMuted) c.enabled = false; } return c; };
 window.__aresSetMuted = function(m) { window.__aresMuted = m; tracks.forEach(function(t) { if (t.readyState === 'live') t.enabled = !m; }); pcs.forEach(function(pc) { try { pc.getSenders().forEach(function(s) { if (s.track && s.track.kind === 'audio') s.track.enabled = !m; }); } catch (e) {} }); };
})();";
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
            m_Mute = MakeBarButton(T("Mutar aba", "Mute tab"), 0, 100, (s, e) => { WebTab t = Current(); if (t != null) SetTabMuted(t, !t.Muted); });
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
            bar.Controls.AddRange(new Control[] { m_ToAres, m_Back, m_Forward, m_Reload, m_Address, m_Mute, m_VdoCopy, m_VdoSettings, m_Add });
            bar.Resize += (s, e) => LayoutBar(bar);

            m_Strip = new ToolStrip();
            m_Strip.Dock = DockStyle.Top;
            m_Strip.GripStyle = ToolStripGripStyle.Hidden;
            m_Strip.LayoutStyle = ToolStripLayoutStyle.Flow;
            m_Strip.ImageScalingSize = new Size(1, 1);

            m_Host = new Panel();
            m_Host.Dock = DockStyle.Fill;
            m_Host.Padding = new Padding(0);

            Controls.Add(m_Host);
            Controls.Add(m_Strip);
            Controls.Add(bar);
            LayoutBar(bar);

            LoadTabs();
            if (m_Tabs.Count > 0)
                SelectTab(m_Tabs[0]);
            Shown += (s, e) => OnTabSelected();
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
            m_Mute.Left = right - m_Mute.Width;
            right = m_Mute.Left - 6;
            m_Address.Width = Math.Max(100, right - m_Address.Left);
        }

        // ------------------------------------------------------------------ tabs

        private WebTab Current()
        {
            return m_Selected;
        }

        private void SelectTab(WebTab tab)
        {
            m_Selected = tab;
            foreach (WebTab other in m_Tabs)
            {
                bool on = (other == tab);
                other.Page.Visible = on;
                other.Button.Checked = on;
            }
            OnTabSelected();
        }

        private WebTab AddTab(string name, string url, string kind, bool select)
        {
            WebTab tab = new WebTab { Name = name, Url = url, Kind = kind };
            tab.Page = new Panel();
            tab.Page.Dock = DockStyle.Fill;
            tab.Page.Visible = false;
            m_Host.Controls.Add(tab.Page);
            tab.Button = new ToolStripButton(name);
            tab.Button.DisplayStyle = ToolStripItemDisplayStyle.Text;
            tab.Button.Padding = new Padding(8, 4, 8, 4);
            tab.Button.Click += (s, e) => SelectTab(tab);
            tab.Button.MouseUp += (s, e) =>
            {
                if (e.Button == MouseButtons.Right)
                    ShowTabMenu(tab, Cursor.Position);
            };
            m_Strip.Items.Add(tab.Button);
            m_Tabs.Add(tab);
            if (select)
                SelectTab(tab);
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
            m_Mute.Visible = t != null;
            m_Mute.Text = (t != null && t.Muted) ? T("Ativar som", "Unmute") : T("Mutar aba", "Mute tab");
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
            // the embedded browser can only be created once the window exists and is visible
            if (!IsHandleCreated || !Visible || tab != m_Selected)
                return;
            tab.Started = true;
            int attempt = 0;
            while (true)
            {
            try
            {
                string url = tab.Kind == "vdo" ? ShareDialog.BuildPushUrl() : tab.Url;
                tab.Page.Controls.Clear();
                tab.View = new WebView2();
                tab.View.Dock = DockStyle.Fill;
                tab.Page.Controls.Add(tab.View);
                CoreWebView2Environment env = await GetEnvironment();
                await tab.View.EnsureCoreWebView2Async(env);
                CoreWebView2 core = tab.View.CoreWebView2;
                core.IsDocumentPlayingAudioChanged += (s, e) =>
                {
                    tab.Playing = core.IsDocumentPlayingAudio;
                    UpdateTabState(tab);
                };
                core.NavigationCompleted += (s, e) =>
                {
                    tab.Loaded = e.IsSuccess;
                    if (tab.Kind == "vdo" && tab.Muted)
                        core.ExecuteScriptAsync("window.__aresSetMuted && window.__aresSetMuted(true);");
                    UpdateTabState(tab);
                };
                if (tab.Kind == "vdo")
                    await core.AddScriptToExecuteOnDocumentCreatedAsync(MuteScript);
                core.IsMuted = tab.Kind != "vdo" && tab.Muted;
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
                return;
            }
            catch (Exception ex)
            {
                s_Env = null;
                if (tab.View != null)
                {
                    tab.Page.Controls.Remove(tab.View);
                    tab.View.Dispose();
                    tab.View = null;
                }
                if (++attempt < 3)
                {
                    await Task.Delay(500);
                    continue;
                }
                tab.Started = false;
                Label error = new Label();
                error.Dock = DockStyle.Fill;
                error.Padding = new Padding(20);
                error.Text = T(
                    "Não foi possível iniciar o navegador embutido (WebView2). Instale o \"Microsoft Edge WebView2 Runtime\" (microsoft.com/edge/webview2) e abra esta aba de novo.\n\n",
                    "The embedded browser (WebView2) could not be started. Install the \"Microsoft Edge WebView2 Runtime\" (microsoft.com/edge/webview2) and open this tab again.\n\n") + ex.Message;
                tab.Page.Controls.Clear();
                tab.Page.Controls.Add(error);
                return;
            }
            }
        }

        private void UpdateTabState(WebTab tab)
        {
            string prefix = "";
            if (tab.Muted)
                prefix = T("[mudo] ", "[muted] ");
            else if (tab.Kind == "vdo" && tab.Loaded)
                prefix = "● ";
            else if (tab.Playing)
                prefix = "♪ ";
            tab.Button.Text = prefix + tab.Name;
            if (tab == m_Selected)
                m_Mute.Text = tab.Muted ? T("Ativar som", "Unmute") : T("Mutar aba", "Mute tab");
            Action h = StateChanged;
            if (h != null)
                h();
        }

        public void SetTabMuted(WebTab tab, bool muted)
        {
            tab.Muted = muted;
            if (tab.View != null && tab.View.CoreWebView2 != null)
            {
                if (tab.Kind == "vdo")
                    tab.View.CoreWebView2.ExecuteScriptAsync("window.__aresSetMuted && window.__aresSetMuted(" + (muted ? "true" : "false") + ");");
                else
                    tab.View.CoreWebView2.IsMuted = muted;
            }
            UpdateTabState(tab);
        }

        /// <summary>Tabs that currently make sound (or send it), for the indicator in the Player window.</summary>
        public System.Collections.Generic.List<WebAudioInfo> GetAudioStates()
        {
            System.Collections.Generic.List<WebAudioInfo> list = new System.Collections.Generic.List<WebAudioInfo>();
            foreach (WebTab tab in m_Tabs)
            {
                WebTab captured = tab;
                bool active = tab.Started && (tab.Playing || tab.Muted || (tab.Kind == "vdo" && tab.Loaded));
                if (!active)
                    continue;
                list.Add(new WebAudioInfo
                {
                    Name = tab.Name,
                    Vdo = tab.Kind == "vdo",
                    Muted = tab.Muted,
                    Toggle = () => SetTabMuted(captured, !captured.Muted)
                });
            }
            return list;
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
                AddTab(name, url, "web", true);
                SaveTabs();
            }
        }

        private void ShowTabMenu(WebTab tab, Point screenPoint)
        {
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
                    UpdateTabState(tab);
                    if (tab.Kind == "web")
                        tab.Url = url;
                    SaveTabs();
                }
            });
            menu.Items.Add(tab.Muted ? T("Ativar som", "Unmute") : T("Mutar aba", "Mute tab"), null, (s, e) => SetTabMuted(tab, !tab.Muted));
            menu.Items.Add(T("Recarregar", "Reload"), null, (s, e) =>
            {
                if (tab.View != null && tab.View.CoreWebView2 != null)
                    tab.View.CoreWebView2.Reload();
            });
            menu.Items.Add(T("Remover aba", "Remove tab"), null, (s, e) => RemoveTab(tab));
            menu.Show(screenPoint);
        }

        private void RemoveTab(WebTab tab)
        {
            if (MessageBox.Show(this, T("Remover a aba \"", "Remove the tab \"") + tab.Name + "\"?", "Ares",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            int index = m_Tabs.IndexOf(tab);
            m_Tabs.Remove(tab);
            m_Strip.Items.Remove(tab.Button);
            m_Host.Controls.Remove(tab.Page);
            tab.Page.Dispose();
            SaveTabs();
            m_Selected = null;
            Action removed = StateChanged;
            if (removed != null)
                removed();
            if (m_Tabs.Count > 0)
                SelectTab(m_Tabs[Math.Min(index, m_Tabs.Count - 1)]);
            else
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
            SelectTab(tab);
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
