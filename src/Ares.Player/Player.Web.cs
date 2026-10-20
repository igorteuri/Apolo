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
            if (m_NowPlaying != null && m_NowPlaying.SetWeb(infos))
                LayoutMainColumns();
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

            // Kenku-style tab bar at the top: the first tab is the Apolo home, the others are web pages
            m_TabBar = new AppTabBar();
            m_WebWindow = new WebWindow(this);
            m_WebWindow.Dock = DockStyle.Fill;
            m_WebWindow.Visible = false;
            Controls.Add(m_WebWindow);
            Controls.Add(m_TabBar);
            m_WebWindow.BringToFront();
            m_TabBar.SendToBack();
            m_TabBar.TabSelected += TabBar_TabSelected;
            m_TabBar.TabClosed += TabBar_TabClosed;
            m_TabBar.AddClicked += TabBar_AddClicked;
            m_TabBar.TabContext += (i, pt) =>
            {
                if (i > 0 && i - 1 < m_WebWindow.Tabs.Count)
                    m_WebWindow.ShowTabMenu(m_WebWindow.Tabs[i - 1], pt);
            };
            m_WebWindow.TabsChanged += RefreshTabBar;
            m_WebWindow.SelectionChanged += RefreshTabBar;
            WebWindow.StateChanged += RefreshTabBar;
            RefreshTabBar();
            InitCustomChrome();
            Shown += (s, e) => MakeRoomForTabBar();

            // the pages are reached through the tab bar; the old "Tocando" menu is replaced by the buttons below the lists
            menuStrip1.Items.Remove(playToolStripMenuItem);
            FormClosed += (s, e) =>
            {
                if (m_WebWindow != null && !m_WebWindow.IsDisposed)
                    m_WebWindow.Dispose();
            };
        }

        private AppTabBar m_TabBar;
        private bool m_ShowingWeb;

        private void MakeRoomForTabBar()
        {
            int h = AppTabBar.BarHeight;
            SuspendLayout();
            m_ElementsTopOriginal += h;
            if (WindowState == FormWindowState.Normal)
            {
                MinimumSize = new Size(MinimumSize.Width, MinimumSize.Height > 0 ? MinimumSize.Height + h : 0);
                Height += h;
            }
            foreach (Control c in Controls)
            {
                if (c == m_TabBar || c == m_WebWindow || c.Dock != DockStyle.None)
                    continue;
                bool top = (c.Anchor & AnchorStyles.Top) != 0;
                bool bottom = (c.Anchor & AnchorStyles.Bottom) != 0;
                if (top && bottom)
                    c.SetBounds(c.Left, c.Top + h, c.Width, c.Height - h);
                else if (top)
                    c.Top += h;
            }
            ResumeLayout(true);
            LayoutMainColumns();
        }

        private void RefreshTabBar()
        {
            if (m_TabBar == null || m_WebWindow == null || m_WebWindow.IsDisposed)
                return;
            List<AppTabItem> items = new List<AppTabItem>();
            items.Add(new AppTabItem { Text = "Apolo", Home = true, Closable = false });
            foreach (WebTab t in m_WebWindow.Tabs)
            {
                items.Add(new AppTabItem
                {
                    Text = t.Name,
                    Closable = true,
                    Sound = t.Playing || (t.Kind == "vdo" && t.Loaded),
                    Muted = t.Muted
                });
            }
            int selected = 0;
            if (m_ShowingWeb && m_WebWindow.Selected != null)
                selected = 1 + m_WebWindow.Tabs.IndexOf(m_WebWindow.Selected);
            m_TabBar.SetItems(items, selected);
        }

        private void ShowHome()
        {
            m_ShowingWeb = false;
            if (m_WebWindow != null)
                m_WebWindow.Visible = false;
            RefreshTabBar();
        }

        private void ShowWebTab(WebTab tab)
        {
            if (tab == null)
            {
                ShowHome();
                return;
            }
            m_ShowingWeb = true;
            m_WebWindow.Visible = true;
            m_WebWindow.BringToFront();
            m_TabBar.SendToBack();
            m_WebWindow.SelectTab(tab);
            RefreshTabBar();
        }

        private void TabBar_TabSelected(int index)
        {
            if (index <= 0)
                ShowHome();

            else if (index - 1 < m_WebWindow.Tabs.Count)
                ShowWebTab(m_WebWindow.Tabs[index - 1]);
        }

        private void TabBar_TabClosed(int index)
        {
            if (index <= 0 || index - 1 >= m_WebWindow.Tabs.Count)
                return;
            m_WebWindow.RemoveTab(m_WebWindow.Tabs[index - 1]);
            if (m_WebWindow.Tabs.Count == 0)
                ShowHome();
            RefreshTabBar();
        }

        private void TabBar_AddClicked(Point screen)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add(T("Adicionar página favorita...", "Add favorite page..."), null, (s, e) =>
            {
                WebTab t = m_WebWindow.AddFavorite();
                if (t != null)
                    ShowWebTab(t);
            });
            menu.Items.Add(T("Enviar áudio por VDO.Ninja", "Send audio through VDO.Ninja"), null, (s, e) => ShowWebWindow(true));
            Ares.CommonGUI.ThemeManager.Apply(menu);
            menu.Show(screen);
        }

        private void ShowWebWindow(bool vdo)
        {
            WebTab tab = null;
            if (vdo)
                tab = m_WebWindow.EnsureVdoTab();
            else if (m_WebWindow.Tabs.Count > 0)
                tab = m_WebWindow.Selected ?? m_WebWindow.Tabs[0];
            ShowWebTab(tab);
        }

        internal void BringToFrontFromWeb()
        {
            ShowHome();
        }

        // when the program starts (the window is still invisible): every web tab is opened once, so that the pages
        // are loaded and the audio ones work without the user having to click them first
        private void PreloadWebTabs()
        {
            try
            {
                if (m_WebWindow == null || m_WebWindow.Tabs.Count == 0 || !Visible)
                    return;
                List<WebTab> tabs = new List<WebTab>(m_WebWindow.Tabs);
                foreach (WebTab tab in tabs)
                    ShowWebTab(tab);
                ShowHome();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }
    }

    internal class WebTab
    {
        public string Name;
        public string Url;
        public string Kind;   // "web" or "vdo"
        public Panel Page;
        public WebView2 View;
        public bool Started;
        public bool Playing;   // the page plays audio
        public bool Muted;     // muted by the user
        public bool Loaded;    // page loaded
        public VirtualMic Mic; // the virtual microphone "Apolo Audio" (VDO tab)
        public bool CaptureHooked; // the sound of the page was taken from inside the page (PC does not play it)
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

    internal class WebWindow : Panel
    {
        private readonly Player m_Player;
        private readonly List<WebTab> m_Tabs = new List<WebTab>();
        private Panel m_Host;
        private WebTab m_Selected;
        private TextBox m_Address;
        private Button m_Back, m_Forward, m_Reload, m_Add, m_ToAres, m_VdoSettings, m_VdoCopy, m_Mute, m_Extensions;
        private ExtensionBar m_ExtBar;
        public static event Action StateChanged;
        public event Action TabsChanged;
        public event Action SelectionChanged;

        public List<WebTab> Tabs { get { return m_Tabs; } }
        public WebTab Selected { get { return m_Selected; } }

        // Registers the audio of the page, so that the sent sound can be muted without leaving the stream.
        private const string MuteScript = @"(function(){
 if (window.__aresMuteInit) return; window.__aresMuteInit = true;
 window.__aresMuted = false; var pcs = [], tracks = [];
 var Orig = window.RTCPeerConnection;
 if (Orig) { window.RTCPeerConnection = new Proxy(Orig, { construct: function(t, a, n) { var pc = Reflect.construct(t, a, n); pcs.push(pc); return pc; } }); }
 var md = navigator.mediaDevices; var gum = md && md.getUserMedia; var enumD = md && md.enumerateDevices;
 var VID = 'apolo-virtual', VLABEL = 'Apolo Audio';
 function post(o) { try { window.chrome.webview.postMessage(JSON.stringify(o)); } catch (e) {} }
 function reg(s) { s.getAudioTracks().forEach(function(t) { tracks.push(t); if (window.__aresMuted) t.enabled = false; }); return s; }
 // ---- Apolo Audio: virtual microphone fed by the program with its own sound
 var vctx = null, vdest = null, vq = [], vlen = 0, vprimed = false;
 function vfeed(s) {
   var bin = atob(s), n = bin.length >> 1, u8 = new Uint8Array(n * 2);
   for (var i = 0; i < u8.length; i++) u8[i] = bin.charCodeAt(i);
   var arr = new Int16Array(u8.buffer); vq.push(arr); vlen += arr.length >> 1;
   while (vlen > 38400 && vq.length > 1) { vlen -= vq.shift().length >> 1; }
 }
 try { window.chrome.webview.addEventListener('message', function(e) { if (typeof e.data === 'string' && e.data.charAt(0) !== '!') vfeed(e.data); }); } catch (e) {}
 function vstart() {
   if (vctx) { if (vctx.state === 'suspended') vctx.resume(); return; }
   var AC = window.AudioContext || window.webkitAudioContext;
   vctx = new AC({ sampleRate: 48000, latencyHint: 'interactive' });
   vdest = vctx.createMediaStreamDestination();
   var node = vctx.createScriptProcessor(2048, 0, 2);
   var pos = 0;
   node.onaudioprocess = function(ev) {
     var L = ev.outputBuffer.getChannelData(0), R = ev.outputBuffer.getChannelData(1), n = L.length;
     if (!vprimed && vlen >= n * 4) vprimed = true;
     if (!vprimed) { L.fill(0); R.fill(0); return; }
     for (var i = 0; i < n; i++) {
       if (!vq.length) { vprimed = false; for (; i < n; i++) { L[i] = 0; R[i] = 0; } break; }
       var a = vq[0];
       L[i] = a[pos * 2] / 32768; R[i] = a[pos * 2 + 1] / 32768;
       pos++; vlen--;
       if (pos * 2 >= a.length) { vq.shift(); pos = 0; }
     }
   };
   node.connect(vdest);
   window.__aresVNode = node;
   vctx.resume();
   post({ t: 'vmic', on: true });
 }
 function vstream() {
   vstart();
   var src = vdest.stream.getAudioTracks()[0].clone();
   try { Object.defineProperty(src, 'label', { value: VLABEL }); } catch (e) {}
   try { var gs = src.getSettings.bind(src); src.getSettings = function() { var o = gs(); o.deviceId = VID; return o; }; } catch (e) {}
   return reg(new MediaStream([src]));
 }
 function wantedId(c) {
   var a = c && c.audio; if (!a || a === true) return null;
   var d = a.deviceId; if (!d) return null;
   if (typeof d === 'string') return d;
   if (Array.isArray(d)) return d[0] || null;
   var v = d.exact || d.ideal; if (Array.isArray(v)) v = v[0];
   return v || null;
 }
 if (enumD) { md.enumerateDevices = function() {
   return enumD.apply(this, arguments).then(function(list) {
     var out = list.slice();
     for (var i = 0; i < out.length; i++) if (out[i].deviceId === VID) return out;
     out.push({ deviceId: VID, groupId: 'apolo', kind: 'audioinput', label: VLABEL, toJSON: function() { return { deviceId: VID, groupId: 'apolo', kind: 'audioinput', label: VLABEL }; } });
     return out;
   });
 }; }
 if (gum) { md.getUserMedia = function(c) {
   var self = this, args = arguments, w = (window.__aresMic || '').toLowerCase();
   if (!c || !c.audio) return gum.apply(self, args).then(reg);
   var id = wantedId(c);
   // the device which the user chose inside the VDO.Ninja page is always respected
   if (id === VID) { post({ t: 'mic', label: VLABEL }); try { return Promise.resolve(vstream()); } catch (e) { return Promise.reject(e); } }
   if (id) {
     return gum.apply(self, args).then(function(s) {
       reg(s);
       if (enumD) enumD.call(md).then(function(list) {
         for (var i = 0; i < list.length; i++) if (list[i].kind === 'audioinput' && list[i].deviceId === id && list[i].label) { post({ t: 'mic', label: list[i].label }); break; }
       }).catch(function() {});
       return s;
     });
   }
   if (!w) return gum.apply(self, args).then(reg);
   if (w.indexOf('apolo audio') >= 0) { try { return Promise.resolve(vstream()); } catch (e) { return gum.apply(self, args).then(reg); } }
   return gum.call(self, { audio: true }).then(function(tmp) { tmp.getTracks().forEach(function(t) { t.stop(); }); return enumD.call(md); }).then(function(list) {
     var found = null, short = w.indexOf(' (') > 0 ? w.substring(0, w.indexOf(' (')) : w;
     list.forEach(function(d) { if (!found && d.kind === 'audioinput' && d.label.toLowerCase().indexOf(w) >= 0) found = d; });
     list.forEach(function(d) { if (!found && d.kind === 'audioinput' && d.label.toLowerCase().indexOf(short) >= 0) found = d; });
     if (!found) return gum.apply(self, args).then(reg);
     var nc = Object.assign({}, c), a = (c.audio === true) ? {} : Object.assign({}, c.audio);
     a.deviceId = { exact: found.deviceId }; nc.audio = a;
     return gum.call(self, nc).then(reg);
   }).catch(function() { return gum.apply(self, args).then(reg); });
 }; }
 var cl = MediaStreamTrack.prototype.clone;
 MediaStreamTrack.prototype.clone = function() { var c = cl.apply(this, arguments); if (c.kind === 'audio') { tracks.push(c); if (window.__aresMuted) c.enabled = false; } return c; };
 window.__aresSetMuted = function(m) { window.__aresMuted = m; tracks.forEach(function(t) { if (t.readyState === 'live') t.enabled = !m; }); pcs.forEach(function(pc) { try { pc.getSenders().forEach(function(s) { if (s.track && s.track.kind === 'audio') s.track.enabled = !m; }); } catch (e) {} }); };
})();";
        private static Task<CoreWebView2Environment> s_Env;

        private static string T(string pt, string en)
        {
            return System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt" ? pt : en;
        }

        private static string ConfigFile
        {
            get
            {
                string dir = Ares.Settings.AppFolders.Root;
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "webtabs.cfg");
            }
        }

        public WebWindow(Player player)
        {
            m_Player = player;
            Panel bar = new Panel();
            bar.Dock = DockStyle.Top;
            bar.Height = 38;
            bar.Padding = new Padding(4);

            m_ToAres = MakeBarButton(T("◀ Apolo", "◀ Apolo"), 4, 80, (s, e) => m_Player.BringToFrontFromWeb());
            m_Back = MakeBarButton("←", 88, 36, (s, e) => { WebTab t = Current(); if (t != null && t.View != null && t.View.CanGoBack) t.View.GoBack(); });
            m_Forward = MakeBarButton("→", 128, 36, (s, e) => { WebTab t = Current(); if (t != null && t.View != null && t.View.CanGoForward) t.View.GoForward(); });
            m_Reload = MakeBarButton("⟳", 168, 36, (s, e) => { WebTab t = Current(); if (t != null && t.View != null && t.View.CoreWebView2 != null) t.View.CoreWebView2.Reload(); });
            m_Add = MakeBarButton(T("+ Favorito", "+ Favorite"), 0, 90, (s, e) => AddFavorite());
            m_Mute = MakeBarButton(T("Mutar aba", "Mute tab"), 0, 100, (s, e) => { WebTab t = Current(); if (t != null) SetTabMuted(t, !t.Muted); });
            m_Extensions = MakeBarButton(T("Extensões", "Extensions"), 0, 90, (s, e) => ManageExtensions());
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
            // one button for each installed extension (settings, window, disable, remove), at the right of the address
            m_ExtBar = new ExtensionBar(() => GetEnvironment(), () => LayoutBar(bar), () => ManageExtensions());
            m_ExtBar.Top = 5;
            bar.Controls.AddRange(new Control[] { m_ToAres, m_Back, m_Forward, m_Reload, m_Address, m_Mute, m_Extensions, m_ExtBar, m_VdoCopy, m_VdoSettings, m_Add });
            bar.Resize += (s, e) => LayoutBar(bar);

            m_Host = new Panel();
            m_Host.Dock = DockStyle.Fill;
            m_Host.Padding = new Padding(0);

            Controls.Add(m_Host);
            Controls.Add(bar);
            LayoutBar(bar);

            Ares.Playing.AudioInit.OutputChanged += OnOutputChangedForTabs;
            LoadTabs();
            if (m_Tabs.Count > 0)
                m_Selected = m_Tabs[0];
            VisibleChanged += (s, e) =>
            {
                if (Visible)
                    OnTabSelected();
            };
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
            m_Extensions.Left = right - m_Extensions.Width;
            right = m_Extensions.Left - 6;
            if (m_ExtBar != null && m_ExtBar.Visible)
            {
                m_ExtBar.Left = right - m_ExtBar.Width;
                right = m_ExtBar.Left - 6;
            }
            m_Address.Width = Math.Max(100, right - m_Address.Left);
        }

        // ------------------------------------------------------------------ tabs

        private WebTab Current()
        {
            return m_Selected;
        }

        public void SelectTab(WebTab tab)
        {
            m_Selected = tab;
            foreach (WebTab other in m_Tabs)
                other.Page.Visible = (other == tab);
            OnTabSelected();
            Action h = SelectionChanged;
            if (h != null)
                h();
        }

        private WebTab AddTab(string name, string url, string kind, bool select)
        {
            WebTab tab = new WebTab { Name = name, Url = url, Kind = kind };
            tab.Page = new Panel();
            tab.Page.Dock = DockStyle.Fill;
            tab.Page.Visible = false;
            m_Host.Controls.Add(tab.Page);
            m_Tabs.Add(tab);
            if (select)
                SelectTab(tab);
            Action changed = TabsChanged;
            if (changed != null)
                changed();
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
                string dir = Path.Combine(Ares.Settings.AppFolders.Root, "WebView2");
                CoreWebView2EnvironmentOptions options = new CoreWebView2EnvironmentOptions("--autoplay-policy=no-user-gesture-required");
                options.AreBrowserExtensionsEnabled = true;
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
                RefreshExtensionBar(core);
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
                    await core.AddScriptToExecuteOnDocumentCreatedAsync(BuildInitScript());
                if (tab.Kind == "vdo")
                {
                    VirtualMic vmic = new VirtualMic(this, core);
                    tab.Mic = vmic;
                    core.WebMessageReceived += (s, e) => HandleVdoMessage(tab, vmic, e);
                    core.NavigationStarting += (s, e) => vmic.Stop();
                }
                if (tab.Kind != "vdo")
                {
                    // sound taken from inside the page: used when the PC doesn't play Apolo ("don't listen on this PC")
                    await core.AddScriptToExecuteOnDocumentCreatedAsync(CaptureScript);
                    core.WebMessageReceived += (s, e) => HandlePageMessage(tab, core, e);
                }
                core.IsMuted = tab.Kind != "vdo" && (tab.Muted || CaptureMode);
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

        // messages from the VDO.Ninja page: virtual microphone requests and the input device chosen by the user
        private void HandleVdoMessage(WebTab tab, VirtualMic vmic, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string text = e.TryGetWebMessageAsString();
                if (string.IsNullOrEmpty(text))
                    return;
                if (text.IndexOf("\"vmic\"", StringComparison.Ordinal) >= 0)
                {
                    vmic.StartAsync();
                }
                else if (text.IndexOf("\"mic\"", StringComparison.Ordinal) >= 0)
                {
                    int i = text.IndexOf("\"label\":\"", StringComparison.Ordinal);
                    if (i < 0)
                        return;
                    i += 9;
                    int j = text.LastIndexOf('"');
                    if (j <= i)
                        return;
                    string label = text.Substring(i, j - i).Replace("\\\"", "\"").Replace("\\\\", "\\");
                    if (label.Length > 0)
                        Ares.CommonGUI.Preferences.Set("VdoDevice", label);
                }
            }
            catch (Exception)
            {
            }
        }

        private static string BuildInitScript()
        {
            string device = Ares.CommonGUI.Preferences.Get("VdoDevice", "").Trim();
            if (device.Length == 0)
                device = ShareDialog.AutoDevice();
            string json = "\"" + device.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
            return "window.__aresMic = " + json + ";\n" + MuteScript;
        }

        private void UpdateTabState(WebTab tab)
        {
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
                    tab.View.CoreWebView2.IsMuted = muted || CaptureMode;
            }
            UpdateTabState(tab);
        }

        // ---- sound of the web pages when the PC does not play Apolo ----
        // Windows can only capture what a page plays on the speakers. When the output is "don't listen on this PC" the
        // pages are silenced in the browser and their sound is taken from inside the page (Web Audio) and mixed by Apolo.

        private static bool CaptureMode
        {
            get { return !Ares.Playing.AudioInit.LocalOutputEnabled; }
        }

        private void OnOutputChangedForTabs()
        {
            try
            {
                if (IsHandleCreated && !IsDisposed)
                    BeginInvoke(new Action(ApplyCaptureModeAll));
            }
            catch (Exception)
            {
            }
        }

        private void ApplyCaptureModeAll()
        {
            bool mode = CaptureMode;
            foreach (WebTab tab in m_Tabs)
            {
                if (tab.Kind == "vdo" || tab.View == null || tab.View.CoreWebView2 == null)
                    continue;
                CoreWebView2 core = tab.View.CoreWebView2;
                try
                {
                    core.IsMuted = tab.Muted || mode;
                    core.PostWebMessageAsString(mode ? "apolo-capture:on" : "apolo-capture:off");
                    if (!mode && tab.CaptureHooked)
                    {
                        // the page was rerouted through Web Audio: a reload gives the normal sound back
                        tab.CaptureHooked = false;
                        core.Reload();
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        private void HandlePageMessage(WebTab tab, CoreWebView2 core, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string msg;
            try
            {
                msg = e.TryGetWebMessageAsString();
            }
            catch (Exception)
            {
                return;
            }
            if (msg == null)
                return;
            if (msg == "apolo-hello")
            {
                core.PostWebMessageAsString(CaptureMode ? "apolo-capture:on" : "apolo-capture:off");
            }
            else if (msg.StartsWith("apolo-pcm:", StringComparison.Ordinal))
            {
                if (tab.Muted || !CaptureMode)
                    return;
                try
                {
                    byte[] data = Convert.FromBase64String(msg.Substring(10));
                    tab.CaptureHooked = true;
                    AppAudioCapture.PushPageAudio(tab, data, data.Length);
                }
                catch (FormatException)
                {
                }
            }
        }

        private const string CaptureScript = @"(function() {
 if (window.__apoloCap) return;
 var cap = window.__apoloCap = { on: false };
 var wv = window.chrome && window.chrome.webview;
 if (!wv) return;
 var origConnect = AudioNode.prototype.connect, origDisconnect = AudioNode.prototype.disconnect;
 var queue = [], queued = 0;
 function toB64(i16) {
  var bytes = new Uint8Array(i16.buffer, i16.byteOffset, i16.byteLength), s = '';
  for (var i = 0; i < bytes.length; i += 8192) s += String.fromCharCode.apply(null, bytes.subarray(i, i + 8192));
  return btoa(s);
 }
 // resampling to 48 kHz keeps its position and its last sample from one block to the next (no clicks at the block borders)
 var rs = { t: 0, pl: 0, pr: 0 };
 function push(l, r, rate) {
  if (!cap.on) return;
  var step = rate / 48000, N = l.length;
  var out = new Int16Array(Math.ceil((N + 1) / step) * 2 + 4), o = 0;
  var t = rs.t;
  while (t < N - 1) {
   var k = Math.floor(t), f = t - k, a, b;
   if (k < 0) { a = rs.pl + (l[0] - rs.pl) * f; b = rs.pr + (r[0] - rs.pr) * f; }
   else { a = l[k] + (l[k + 1] - l[k]) * f; b = r[k] + (r[k + 1] - r[k]) * f; }
   a = a > 1 ? 1 : (a < -1 ? -1 : a); b = b > 1 ? 1 : (b < -1 ? -1 : b);
   out[o++] = a * 32767; out[o++] = b * 32767;
   t += step;
  }
  rs.t = t - N; rs.pl = l[N - 1]; rs.pr = r[N - 1];
  try { wv.postMessage('apolo-pcm:' + toB64(out.subarray(0, o))); } catch (e) {}
 }
 function tapOf(ctx) {
  if (ctx.__apoloTap) return ctx.__apoloTap;
  var sp = ctx.createScriptProcessor(4096, 2, 2);
  sp.__apoloIsTap = true;
  sp.onaudioprocess = function(e) {
   if (!cap.on) return;
   var ib = e.inputBuffer, l = ib.getChannelData(0), r = ib.numberOfChannels > 1 ? ib.getChannelData(1) : l;
   push(l, r, ctx.sampleRate);
  };
  origConnect.call(sp, ctx.destination);
  ctx.__apoloTap = sp;
  return sp;
 }
 AudioNode.prototype.connect = function(dest) {
  var r = origConnect.apply(this, arguments);
  try { if (dest instanceof AudioDestinationNode && !this.__apoloIsTap) origConnect.call(this, tapOf(dest.context)); } catch (e) {}
  return r;
 };
 AudioNode.prototype.disconnect = function(dest) {
  try {
   if ((arguments.length === 0 || dest instanceof AudioDestinationNode) && this.context && this.context.__apoloTap && !this.__apoloIsTap)
    origDisconnect.call(this, this.context.__apoloTap);
  } catch (e) {}
  return origDisconnect.apply(this, arguments);
 };
 function hook(el) {
  if (el.__apoloHooked) return;
  el.__apoloHooked = true;
  try {
   var ctx = new (window.AudioContext || window.webkitAudioContext)();
   var src = ctx.createMediaElementSource(el);
   src.connect(ctx.destination);
   ctx.resume();
  } catch (e) {}
 }
 var origPlay = HTMLMediaElement.prototype.play;
 HTMLMediaElement.prototype.play = function() { if (cap.on) hook(this); return origPlay.apply(this, arguments); };
 function hookAll() { var els = document.querySelectorAll('audio,video'); for (var i = 0; i < els.length; i++) hook(els[i]); }
 wv.addEventListener('message', function(e) {
  if (e.data === 'apolo-capture:on') { cap.on = true; hookAll(); }
  else if (e.data === 'apolo-capture:off') { cap.on = false; }
 });
 wv.postMessage('apolo-hello');
})();";

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

        public WebTab AddFavorite()
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
            if (WebTabDialog.Ask(FindForm(), T("Novo favorito", "New favorite"), ref name, ref url))
            {
                WebTab added = AddTab(name, url, "web", false);
                SaveTabs();
                return added;
            }
            return null;
        }

        // browser extensions (unpacked folders, or .crx / .zip files which are unpacked first)
        public void ManageExtensions()
        {
            CoreWebView2 core = null;
            WebTab current = Current();
            if (current != null && current.View != null && current.View.CoreWebView2 != null)
                core = current.View.CoreWebView2;
            if (core == null)
            {
                foreach (WebTab t in m_Tabs)
                {
                    if (t.View != null && t.View.CoreWebView2 != null)
                    {
                        core = t.View.CoreWebView2;
                        break;
                    }
                }
            }
            if (core == null)
            {
                MessageBox.Show(FindForm(), T("Abra uma aba da internet primeiro.", "Open a web tab first."), "Apolo",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (ExtensionsDialog dialog = new ExtensionsDialog(core, () => GetEnvironment()))
            {
                dialog.ShowDialog(FindForm());
            }
            RefreshExtensionBar(core);
        }

        private async void RefreshExtensionBar(CoreWebView2 core)
        {
            try
            {
                if (m_ExtBar != null)
                    await m_ExtBar.RefreshAsync(core);
            }
            catch (Exception)
            {
            }
        }

        public void ShowTabMenu(WebTab tab, Point screenPoint)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add(T("Renomear / alterar endereço ...", "Rename / change address ..."), null, (s, e) =>
            {
                string name = tab.Name;
                string url = tab.Kind == "vdo" ? "" : tab.Url;
                if (tab.Kind == "web" && tab.View != null && tab.View.Source != null)
                    url = tab.View.Source.ToString();
                if (WebTabDialog.Ask(FindForm(), tab.Name, ref name, ref url, tab.Kind == "vdo"))
                {
                    tab.Name = name;
                    UpdateTabState(tab);
                    if (tab.Kind == "web")
                        tab.Url = url;
                    SaveTabs();
                    Action changed = TabsChanged;
                    if (changed != null)
                        changed();
                }
            });
            menu.Items.Add(tab.Muted ? T("Ativar som", "Unmute") : T("Mutar aba", "Mute tab"), null, (s, e) => SetTabMuted(tab, !tab.Muted));
            menu.Items.Add(T("Recarregar", "Reload"), null, (s, e) =>
            {
                if (tab.View != null && tab.View.CoreWebView2 != null)
                    tab.View.CoreWebView2.Reload();
            });
            menu.Items.Add(T("Remover aba", "Remove tab"), null, (s, e) => RemoveTab(tab));
            Ares.CommonGUI.ThemeManager.Apply(menu);
            menu.Show(screenPoint);
        }

        public void RemoveTab(WebTab tab)
        {
            if (MessageBox.Show(FindForm(), T("Remover a aba \"", "Remove the tab \"") + tab.Name + "\"?", "Apolo",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            int index = m_Tabs.IndexOf(tab);
            bool wasSelected = (tab == m_Selected);
            m_Tabs.Remove(tab);
            m_Host.Controls.Remove(tab.Page);
            if (tab.Mic != null)
                tab.Mic.Dispose();
            tab.Page.Dispose();
            SaveTabs();
            if (wasSelected)
            {
                m_Selected = null;
                if (m_Tabs.Count > 0)
                    SelectTab(m_Tabs[Math.Min(index, m_Tabs.Count - 1)]);
                else
                    OnTabSelected();
            }
            Action changed = TabsChanged;
            if (changed != null)
                changed();
            Action removed = StateChanged;
            if (removed != null)
                removed();
        }

        // ------------------------------------------------------------------ VDO.Ninja

        /// <summary>Returns the VDO.Ninja sender tab, creating it when needed.</summary>
        public WebTab EnsureVdoTab()
        {
            WebTab tab = m_Tabs.Find(t => t.Kind == "vdo");
            if (tab == null)
            {
                tab = AddTab("VDO.Ninja", "", "vdo", false);
                SaveTabs();
            }
            return tab;
        }

        private void ConfigureVdo()
        {
            WebTab tab = Current();
            using (ShareDialog dlg = new ShareDialog())
            {
                if (dlg.ShowDialog(FindForm()) == DialogResult.OK && tab != null && tab.Kind == "vdo"
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
                MessageBox.Show(FindForm(), ex.Message, "Apolo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    internal class ExtensionsDialog : Form
    {
        private readonly CoreWebView2 m_Core;
        private readonly Func<Task<CoreWebView2Environment>> m_Environment;
        private readonly ListBox m_List = new ListBox();
        private readonly Label m_Info = new Label();
        private List<CoreWebView2BrowserExtension> m_Items = new List<CoreWebView2BrowserExtension>();

        private static string T(string pt, string en)
        {
            return System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt" ? pt : en;
        }

        public ExtensionsDialog(CoreWebView2 core, Func<Task<CoreWebView2Environment>> environment)
        {
            m_Core = core;
            m_Environment = environment;
            Text = T("Extensões do navegador", "Browser extensions");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(560, 424);

            Label help = new Label();
            help.SetBounds(12, 10, 536, 78);
            help.Text = T(
                "Aqui você instala extensões nas abas de internet do Apolo (bloqueador de anúncios, melhorias do YouTube etc.). A Chrome Web Store não pode ser usada direto: baixe a extensão (arquivo .crx ou .zip) ou escolha a pasta de uma extensão já descompactada.",
                "Install extensions in the web tabs of Apolo (ad blockers, YouTube improvements ...). The Chrome Web Store can't be used directly: download the extension (.crx or .zip file) or choose the folder of an unpacked extension.");
            Controls.Add(help);

            m_List.SetBounds(12, 94, 536, 190);
            Controls.Add(m_List);

            m_Info.SetBounds(12, 290, 536, 36);
            Controls.Add(m_Info);

            Button addFolder = MakeButton(T("Adicionar pasta...", "Add folder..."), 12, 336, 170);
            addFolder.Click += async (s, e) => await AddFolder();
            Button addFile = MakeButton(T("Adicionar arquivo...", "Add file..."), 190, 336, 170);
            addFile.Click += async (s, e) => await AddFile();
            Button settings = MakeButton(T("Configurações da extensão", "Extension settings"), 12, 376, 220);
            settings.Click += (s, e) => OpenSettingsOfSelected();
            Button remove = MakeButton(T("Remover", "Remove"), 240, 376, 110);
            remove.Click += async (s, e) => await RemoveSelected();
            Button close = MakeButton(T("Fechar", "Close"), 458, 376, 90);
            close.DialogResult = DialogResult.Cancel;
            CancelButton = close;
            Controls.AddRange(new Control[] { addFolder, addFile, settings, remove, close });

            Shown += async (s, e) => await Refresh();
        }

        private static Button MakeButton(string text, int x, int y, int width)
        {
            Button b = new Button();
            b.Text = text;
            b.SetBounds(x, y, width, 30);
            return b;
        }

        private async Task Refresh()
        {
            try
            {
                IReadOnlyList<CoreWebView2BrowserExtension> list = await m_Core.Profile.GetBrowserExtensionsAsync();
                m_Items = new List<CoreWebView2BrowserExtension>(list);
                m_List.Items.Clear();
                foreach (CoreWebView2BrowserExtension ext in m_Items)
                    m_List.Items.Add(ext.Name + (ext.IsEnabled ? "" : T("  (desativada)", "  (disabled)")));
            }
            catch (Exception ex)
            {
                m_Info.Text = ex.Message;
            }
        }

        private async Task Install(string folder)
        {
            try
            {
                string manifest = Path.Combine(folder, "manifest.json");
                if (!File.Exists(manifest))
                {
                    m_Info.Text = T("Esta pasta não tem um arquivo manifest.json (não é uma extensão descompactada).",
                        "This folder has no manifest.json file (it isn't an unpacked extension).");
                    return;
                }
                m_Info.Text = T("Instalando...", "Installing...");
                CoreWebView2BrowserExtension ext = await m_Core.Profile.AddBrowserExtensionAsync(folder);
                ExtensionCatalog.Remember(ext.Id, folder);
                m_Info.Text = T("Instalada: ", "Installed: ") + ext.Name + T(". Recarregue as abas para usar.", ". Reload the tabs to use it.");
                await Refresh();
            }
            catch (Exception ex)
            {
                m_Info.Text = T("Não foi possível instalar: ", "Could not install: ") + ex.Message;
            }
        }

        private async Task AddFolder()
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = T("Escolha a pasta da extensão (a que contém o manifest.json)", "Choose the folder of the extension (the one with manifest.json)");
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                await Install(dialog.SelectedPath);
            }
        }

        private async Task AddFile()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = T("Extensões (*.crx;*.zip)|*.crx;*.zip", "Extensions (*.crx;*.zip)|*.crx;*.zip");
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    string name = Path.GetFileNameWithoutExtension(dialog.FileName);
                    string target = Path.Combine(Ares.Settings.AppFolders.Root, "Extensions", name);
                    if (Directory.Exists(target))
                        Directory.Delete(target, true);
                    Directory.CreateDirectory(target);
                    byte[] data = File.ReadAllBytes(dialog.FileName);
                    int zipStart = 0;
                    if (data.Length > 16 && data[0] == (byte)'C' && data[1] == (byte)'r' && data[2] == (byte)'2' && data[3] == (byte)'4')
                    {
                        // CRX: a header in front of the zip (version 2 or 3)
                        int version = BitConverter.ToInt32(data, 4);
                        if (version == 3)
                            zipStart = 12 + BitConverter.ToInt32(data, 8);
                        else
                            zipStart = 16 + BitConverter.ToInt32(data, 8) + BitConverter.ToInt32(data, 12);
                    }
                    string temp = Path.Combine(Path.GetTempPath(), "apolo-ext-" + Guid.NewGuid().ToString("N") + ".zip");
                    using (FileStream fs = File.Create(temp))
                        fs.Write(data, zipStart, data.Length - zipStart);
                    try
                    {
                        System.IO.Compression.ZipFile.ExtractToDirectory(temp, target);
                    }
                    finally
                    {
                        try { File.Delete(temp); } catch (Exception) { }
                    }
                    await Install(target);
                }
                catch (Exception ex)
                {
                    m_Info.Text = T("Não foi possível abrir o arquivo: ", "Could not open the file: ") + ex.Message;
                }
            }
        }

        // opens the page of settings of the extension which is selected in the list
        private void OpenSettingsOfSelected()
        {
            int index = m_List.SelectedIndex;
            if (index < 0 || index >= m_Items.Count)
            {
                m_Info.Text = T("Escolha uma extensão da lista.", "Choose an extension of the list.");
                return;
            }
            ExtensionEntry entry = ExtensionCatalog.Describe(m_Items[index]);
            if (entry.OptionsPage == null)
            {
                m_Info.Text = entry.Folder == null
                    ? T("Não achei os arquivos desta extensão para abrir as configurações. Instale-a de novo por aqui (Adicionar pasta/arquivo).",
                        "I could not find the files of this extension to open its settings. Install it again here (Add folder/file).")
                    : T("Esta extensão não tem página de configurações.", "This extension has no settings page.");
                return;
            }
            ExtensionPageWindow.Open(this, T("Configurações: ", "Settings: ") + entry.Name,
                "chrome-extension://" + entry.Id + "/" + entry.OptionsPage.TrimStart('/'), m_Environment, new Size(900, 700));
        }

        private async Task RemoveSelected()
        {
            int index = m_List.SelectedIndex;
            if (index < 0 || index >= m_Items.Count)
                return;
            try
            {
                await m_Items[index].RemoveAsync();
                m_Info.Text = T("Extensão removida.", "Extension removed.");
                await Refresh();
            }
            catch (Exception ex)
            {
                m_Info.Text = ex.Message;
            }
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
