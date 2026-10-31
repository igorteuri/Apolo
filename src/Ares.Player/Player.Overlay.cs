using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Ares.Players;

namespace Ares.Player
{
    /// <summary>
    /// A tiny web server on this computer (localhost only). It serves the overlay page for the OBS browser source
    /// (current music title), the page where the overlay's look is adjusted, and the current state as JSON.
    /// The pages are opened by OBS / the browser, they do not have to stay open inside Ares.
    /// </summary>
    internal class ObsOverlayServer
    {
        private HttpListener m_Listener;
        private volatile string m_State = "{\"playing\":false}";

        public int Port { get; private set; }
        public string LastError { get; private set; }

        public bool Running
        {
            get { return m_Listener != null && m_Listener.IsListening; }
        }

        public void SetState(string json)
        {
            m_State = json;
        }

        public bool Start(int port)
        {
            Stop();
            try
            {
                HttpListener listener = new HttpListener();
                listener.Prefixes.Add("http://localhost:" + port + "/");
                listener.Prefixes.Add("http://127.0.0.1:" + port + "/");
                listener.Start();
                m_Listener = listener;
                Port = port;
                LastError = null;
                listener.BeginGetContext(OnContext, listener);
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                m_Listener = null;
                return false;
            }
        }

        public void Stop()
        {
            HttpListener listener = m_Listener;
            m_Listener = null;
            if (listener == null)
                return;
            try
            {
                listener.Close();
            }
            catch (Exception swallowed)
            {
                Ares.Settings.ErrorLog.Note(swallowed);
            }
        }

        private void OnContext(IAsyncResult ar)
        {
            HttpListener listener = (HttpListener)ar.AsyncState;
            HttpListenerContext context;
            try
            {
                context = listener.EndGetContext(ar);
            }
            catch (Exception)
            {
                return;
            }
            try
            {
                if (listener.IsListening)
                    listener.BeginGetContext(OnContext, listener);
            }
            catch (Exception swallowed)
            {
                Ares.Settings.ErrorLog.Note(swallowed);
            }
            try
            {
                Handle(context);
            }
            catch (Exception)
            {
                try { context.Response.Abort(); } catch (Exception swallowed) { Ares.Settings.ErrorLog.Note(swallowed); }
            }
        }

        private static string Resource(string name)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if (stream == null)
                    return null;
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                    return reader.ReadToEnd();
            }
        }

        private void Handle(HttpListenerContext context)
        {
            string path = context.Request.Url.AbsolutePath.ToLowerInvariant();
            string body;
            string type = "text/html; charset=utf-8";
            int status = 200;
            if (path == "/now.json")
            {
                body = m_State;
                type = "application/json; charset=utf-8";
            }
            else if (path == "/overlay" || path == "/overlay.html")
            {
                body = Resource("obs.overlay.html");
            }
            else if (path == "/config" || path == "/config.html" || path == "/")
            {
                body = Resource("obs.config.html");
            }
            else
            {
                body = "Not found";
                status = 404;
                type = "text/plain; charset=utf-8";
            }
            if (body == null)
            {
                body = "Resource missing";
                status = 500;
                type = "text/plain; charset=utf-8";
            }
            byte[] bytes = Encoding.UTF8.GetBytes(body);
            HttpListenerResponse response = context.Response;
            response.StatusCode = status;
            response.ContentType = type;
            response.ContentLength64 = bytes.Length;
            response.Headers.Add("Cache-Control", "no-store");
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.OutputStream.Write(bytes, 0, bytes.Length);
            response.OutputStream.Close();
        }
    }

    public partial class Player
    {
        private ObsOverlayServer m_Overlay;
        private System.Windows.Forms.Timer m_OverlayTimer;
        private int m_OverlayInfoId = int.MinValue;
        private MusicInfo m_OverlayInfo;

        private static int OverlayPort
        {
            get
            {
                int port;
                if (int.TryParse(Ares.CommonGUI.Preferences.Get("ObsOverlayPort", "8765"), out port) && port >= 1024 && port <= 65535)
                    return port;
                return 8765;
            }
        }

        private void InitOverlay()
        {
            try
            {
                m_Overlay = new ObsOverlayServer();
                if (Ares.CommonGUI.Preferences.GetBool("ObsOverlayEnabled", true))
                    m_Overlay.Start(OverlayPort);

                m_OverlayTimer = new System.Windows.Forms.Timer();
                m_OverlayTimer.Interval = 500;
                m_OverlayTimer.Tick += (s, e) => UpdateOverlayState();
                m_OverlayTimer.Start();

                ToolStripMenuItem item = new ToolStripMenuItem(T("Overlay para OBS...", "OBS overlay..."));
                item.Name = "overlayMenuItem";
                item.Click += (s, e) => ShowOverlayDialog();
                extrasToolStripMenuItem.DropDownItems.Insert(1, item);
                FormClosed += (s, e) =>
                {
                    m_OverlayTimer.Stop();
                    m_Overlay.Stop();
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }

        private static string JsonString(string s)
        {
            return JsonText.Quote(s);
        }

        private void UpdateOverlayState()
        {
            if (m_Overlay == null || !m_Overlay.Running)
                return;
            PlayingControl control = m_PlayingControl;
            if (control == null)
                return;
            try
            {
                int id = control.CurrentMusicElement;
                bool playing = id != -1;
                double position = 0, length = 0;
                bool paused = false;
                string listTitle = "";
                if (playing)
                {
                    if (id != m_OverlayInfoId)
                    {
                        m_OverlayInfoId = id;
                        m_OverlayInfo = MusicInfo.GetInfo(id);
                    }
                    control.GetMusicPosition(out position, out length);
                    paused = control.IsPaused();
                    Ares.Data.IElement list = Ares.Data.DataModule.ElementRepository.GetElement(control.CurrentMusicList);
                    if (list != null)
                        listTitle = list.Title;
                }
                else
                {
                    m_OverlayInfoId = int.MinValue;
                    m_OverlayInfo = null;
                }
                MusicInfo info = m_OverlayInfo;
                string artist = info != null ? info.Artist : "";
                if (info != null && !string.IsNullOrEmpty(info.Album))
                    artist = string.IsNullOrEmpty(artist) ? info.Album : artist + " • " + info.Album;
                StringBuilder json = new StringBuilder();
                json.Append("{\"playing\":").Append(playing ? "true" : "false");
                json.Append(",\"paused\":").Append(paused ? "true" : "false");
                json.Append(",\"id\":").Append(id.ToString(CultureInfo.InvariantCulture));
                json.Append(",\"title\":").Append(JsonString(info != null ? info.Title : ""));
                json.Append(",\"artist\":").Append(JsonString(artist));
                json.Append(",\"list\":").Append(JsonString(listTitle));
                json.Append(",\"position\":").Append(position.ToString("0.0", CultureInfo.InvariantCulture));
                json.Append(",\"length\":").Append(length.ToString("0.0", CultureInfo.InvariantCulture));
                json.Append("}");
                m_Overlay.SetState(json.ToString());
            }
            catch (Exception swallowed)
            {
                // the overlay must never disturb playing
                Ares.Settings.ErrorLog.Note(swallowed);
            }
        }

        private void OpenInBrowser(string url)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Ares.CommonGUI.MessageDialog.Show(this, ex.Message, StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowOverlayDialog()
        {
            if (RedirectToSettings("overlay"))
                return;
            if (m_Overlay == null)
                return;
            Form form = new Form();
            {
                form.Text = T("Overlay para OBS", "OBS overlay");
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.StartPosition = FormStartPosition.CenterParent;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.ShowInTaskbar = false;
                form.ClientSize = new Size(600, 400);

                Label intro = new Label();
                intro.SetBounds(24, 20, 552, 64);
                intro.Text = T(
                    "O Apolo gera uma página com o título da música que está tocando. No OBS, adicione uma fonte \"Navegador\" e cole o link abaixo (largura 800, altura 200). O Apolo precisa estar aberto; esta janela não.",
                    "Apolo serves a page with the title of the music that is playing. In OBS, add a \"Browser\" source and paste the link below (width 800, height 200). Apolo must be running; this window does not.");

                CheckBox enabled = new CheckBox();
                enabled.SetBounds(24, 112, 400, 28);
                enabled.Text = T("Ativar o overlay (servidor local)", "Enable the overlay (local server)");
                enabled.Checked = m_Overlay.Running;

                Label portLabel = new Label();
                portLabel.SetBounds(24, 152, 60, 32);
                portLabel.Text = T("Porta:", "Port:");
                portLabel.TextAlign = ContentAlignment.MiddleLeft;
                NumericUpDown port = new NumericUpDown();
                DialogKit.InRow(port, 88, 152, 100, 32);
                port.Minimum = 1024;
                port.Maximum = 65535;
                port.Value = OverlayPort;

                Label linkLabel = new Label();
                linkLabel.SetBounds(24, 212, 552, 22);
                linkLabel.Text = T("Link para colar no OBS:", "Link to paste into OBS:");
                TextBox link = new TextBox();
                DialogKit.InRow(link, 24, 238, 552, 32);
                link.ReadOnly = true;

                Label status = new Label();
                status.SetBounds(24, 284, 552, 40);
                DialogKit.Muted(status);

                Button copy = new Button();
                copy.SetBounds(24, 340, 140, 36); DialogKit.Primary(copy);
                copy.Text = T("Copiar link", "Copy link");
                Button configure = new Button();
                configure.SetBounds(176, 340, 210, 36);
                configure.Text = T("Ajustar aparência...", "Adjust appearance...");
                Button close = new Button();
                close.SetBounds(600 - 24 - 110, 340, 110, 36);
                close.Text = T("Fechar", "Close");
                close.DialogResult = DialogResult.OK;
                form.AcceptButton = close;

                Action refresh = () =>
                {
                    int p = (int)port.Value;
                    link.Text = "http://localhost:" + p + "/overlay";
                    bool on = m_Overlay.Running;
                    copy.Enabled = on;
                    configure.Enabled = on;
                    if (on)
                        status.Text = T("Ativo. Ajuste a aparência e copie o link; no OBS use a URL acima.", "Running. Adjust the look and copy the link; in OBS use the URL above.");
                    else if (m_Overlay.LastError != null)
                        status.Text = T("Não foi possível iniciar: ", "Could not start: ") + m_Overlay.LastError + T(" (tente outra porta)", " (try another port)");
                    else
                        status.Text = T("Desativado.", "Disabled.");
                };
                Action apply = () =>
                {
                    Ares.CommonGUI.Preferences.SetBool("ObsOverlayEnabled", enabled.Checked);
                    Ares.CommonGUI.Preferences.Set("ObsOverlayPort", ((int)port.Value).ToString());
                    if (enabled.Checked)
                    {
                        if (!m_Overlay.Running || m_Overlay.Port != (int)port.Value)
                            m_Overlay.Start((int)port.Value);
                    }
                    else
                    {
                        m_Overlay.Stop();
                    }
                    refresh();
                };
                enabled.CheckedChanged += (s, e) => apply();
                port.ValueChanged += (s, e) =>
                {
                    if (enabled.Checked)
                        apply();
                    else
                        refresh();
                };
                copy.Click += (s, e) =>
                {
                    try { Clipboard.SetText(link.Text); } catch (Exception swallowed) { Ares.Settings.ErrorLog.Note(swallowed); }
                    status.Text = T("Link copiado.", "Link copied.");
                };
                configure.Click += (s, e) => OpenInBrowser("http://localhost:" + (int)port.Value + "/config");
                refresh();

                form.Controls.AddRange(new Control[] { intro, enabled, portLabel, port, linkLabel, link, status, copy, configure, close,
                    DialogKit.MakeRule(96, 552), DialogKit.MakeRule(196, 552) });
                DialogKit.Finish(form, close, enabled);
                ShowOrEmbed(form, null);
            }
        }
    }
}
