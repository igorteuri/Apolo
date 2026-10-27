using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Ares.Players;

namespace Ares.Player
{
    /// <summary>
    /// Small web server (this computer only) with the remote control page for phones / other people.
    /// Everything needs the password; the page is reached from outside through the tunnel (CloudflareTunnel).
    /// </summary>
    internal class RemoteServer
    {
        private HttpListener m_Listener;
        private volatile string m_State = "{}";
        private string m_Password = "";
        private readonly HashSet<string> m_Tokens = new HashSet<string>();
        private readonly object m_Lock = new object();
        private int m_Failures;
        private DateTime m_LockedUntil = DateTime.MinValue;

        public int Port { get; private set; }
        public string LastError { get; private set; }
        public Func<string, string, string> Execute;
        /// <summary>Returns the modes and elements of the project as JSON (used by the Stream Deck plugin).</summary>
        public Func<string> Project;
        /// <summary>Bridge mode (Foundry VTT module): this computer only, answers browser (CORS) requests, key in the X-Apolo header.</summary>
        public bool Bridge;
        public Func<string> BridgeKey;
        public Func<string, bool> BridgeAllows;
        /// <summary>Last time a client asked for the state (the state is only built while somebody asks).</summary>
        public DateTime LastUse = DateTime.MinValue;

        public bool Running
        {
            get { return m_Listener != null && m_Listener.IsListening; }
        }

        public void SetState(string json)
        {
            m_State = json;
        }

        public bool Start(int firstPort, string password)
        {
            Stop();
            m_Password = password ?? "";
            lock (m_Lock)
            {
                m_Tokens.Clear();
                m_Failures = 0;
            }
            for (int port = firstPort; port < firstPort + 10; port++)
            {
                try
                {
                    HttpListener listener = new HttpListener();
                    listener.Prefixes.Add("http://localhost:" + port + "/");
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
                }
            }
            m_Listener = null;
            return false;
        }

        public void Stop()
        {
            HttpListener listener = m_Listener;
            m_Listener = null;
            lock (m_Lock)
            {
                m_Tokens.Clear();
            }
            if (listener == null)
                return;
            try { listener.Close(); } catch (Exception swallowed) { Ares.Settings.ErrorLog.Note(swallowed); }
        }

        private void OnContext(IAsyncResult ar)
        {
            HttpListener listener = (HttpListener)ar.AsyncState;
            HttpListenerContext context;
            try { context = listener.EndGetContext(ar); }
            catch (Exception) { return; }
            try
            {
                if (listener.IsListening)
                    listener.BeginGetContext(OnContext, listener);
            }
            catch (Exception swallowed) { Ares.Settings.ErrorLog.Note(swallowed); }
            try { Handle(context); }
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

        private static Dictionary<string, string> ParseForm(string body)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(body))
                return result;
            foreach (string part in body.Split('&'))
            {
                int idx = part.IndexOf('=');
                if (idx <= 0)
                    continue;
                try
                {
                    string key = Uri.UnescapeDataString(part.Substring(0, idx).Replace('+', ' '));
                    string value = Uri.UnescapeDataString(part.Substring(idx + 1).Replace('+', ' '));
                    result[key] = value;
                }
                catch (Exception swallowed) { Ares.Settings.ErrorLog.Note(swallowed); }
            }
            return result;
        }

        private static string ReadBody(HttpListenerRequest request)
        {
            if (!request.HasEntityBody || request.ContentLength64 > 2048)
                return "";
            using (StreamReader reader = new StreamReader(request.InputStream, Encoding.UTF8))
                return reader.ReadToEnd();
        }

        private static bool SameText(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length)
                return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];
            return diff == 0;
        }

        // picture of a button (only files of the icons folder of Apolo, nothing else can be reached)
        private static byte[] ReadIcon(string name)
        {
            try
            {
                if (string.IsNullOrEmpty(name))
                    return null;
                string file = System.IO.Path.GetFileName(name);
                if (file != name || !file.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    return null;
                string full = System.IO.Path.Combine(Ares.CommonGUI.ButtonImageStore.IconsFolder, file);
                if (!System.IO.File.Exists(full) || new System.IO.FileInfo(full).Length > 2000000)
                    return null;
                return System.IO.File.ReadAllBytes(full);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private bool Authorized(HttpListenerRequest request)
        {
            string token = request.Headers["X-Token"];
            if (string.IsNullOrEmpty(token))
                token = request.QueryString["t"];
            if (string.IsNullOrEmpty(token))
                return false;
            lock (m_Lock)
            {
                return m_Tokens.Contains(token);
            }
        }

        private string NewToken()
        {
            byte[] bytes = new byte[24];
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
                rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }

        private void Reply(HttpListenerContext context, int status, string type, string body)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(body ?? "");
            HttpListenerResponse response = context.Response;
            response.StatusCode = status;
            response.ContentType = type;
            response.ContentLength64 = bytes.Length;
            response.Headers.Add("Cache-Control", "no-store");
            response.Headers.Add("X-Content-Type-Options", "nosniff");
            if (Bridge)
                AddCors(response);
            response.OutputStream.Write(bytes, 0, bytes.Length);
            response.OutputStream.Close();
        }

        private static void AddCors(HttpListenerResponse response)
        {
            response.Headers.Set("Access-Control-Allow-Origin", "*");
            response.Headers.Set("Access-Control-Allow-Headers", "X-Apolo, Content-Type");
            response.Headers.Set("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            response.Headers.Set("Access-Control-Allow-Private-Network", "true");
            response.Headers.Set("Access-Control-Max-Age", "600");
        }

        // requests of the Foundry VTT module (sent by the browser of the game master on this computer)
        private void HandleBridge(HttpListenerContext context)
        {
            HttpListenerRequest request = context.Request;
            string path = request.Url.AbsolutePath.ToLowerInvariant();
            if (request.HttpMethod == "OPTIONS")
            {
                AddCors(context.Response);
                context.Response.StatusCode = 204;
                context.Response.ContentLength64 = 0;
                context.Response.OutputStream.Close();
                return;
            }
            string key = BridgeKey != null ? BridgeKey() : "";
            string given = request.Headers["X-Apolo"];
            if (string.IsNullOrEmpty(key) || key.Length < 4 || !SameText(given, key))
            {
                Reply(context, 401, "text/plain; charset=utf-8", "key");
                return;
            }
            if (path == "/foundry/ping")
            {
                Reply(context, 200, "text/plain; charset=utf-8", "apolo");
                return;
            }
            if (path == "/foundry/state" && request.HttpMethod == "GET")
            {
                LastUse = DateTime.UtcNow;
                Reply(context, 200, "application/json; charset=utf-8", m_State);
                return;
            }
            if (path == "/foundry/cmd" && request.HttpMethod == "POST")
            {
                Dictionary<string, string> form = ParseForm(ReadBody(request));
                string cmd, arg;
                form.TryGetValue("cmd", out cmd);
                form.TryGetValue("arg", out arg);
                Func<string, string, string> exec = Execute;
                Func<string, bool> allowed = BridgeAllows;
                string result = (exec != null && !string.IsNullOrEmpty(cmd) && (allowed == null || allowed(cmd))) ? exec(cmd, arg ?? "") : "no";
                Reply(context, 200, "text/plain; charset=utf-8", result);
                return;
            }
            Reply(context, 404, "text/plain; charset=utf-8", "Not found");
        }

        private void Handle(HttpListenerContext context)
        {
            if (Bridge)
            {
                HandleBridge(context);
                return;
            }
            HttpListenerRequest request = context.Request;
            string path = request.Url.AbsolutePath.ToLowerInvariant();
            if (request.HttpMethod == "GET" && (path == "/" || path == "/index.html"))
            {
                string page = Resource("remote.html");
                Reply(context, page != null ? 200 : 500, "text/html; charset=utf-8", page ?? "missing");
                return;
            }
            if (request.HttpMethod == "POST" && path == "/api/login")
            {
                Dictionary<string, string> form = ParseForm(ReadBody(request));
                string given;
                form.TryGetValue("password", out given);
                lock (m_Lock)
                {
                    if (DateTime.UtcNow < m_LockedUntil)
                    {
                        Reply(context, 429, "text/plain; charset=utf-8", "locked");
                        return;
                    }
                    if (m_Password.Length >= 4 && SameText(given, m_Password))
                    {
                        m_Failures = 0;
                        if (m_Tokens.Count > 20)
                            m_Tokens.Clear();
                        string token = NewToken();
                        m_Tokens.Add(token);
                        Reply(context, 200, "text/plain; charset=utf-8", token);
                        return;
                    }
                    m_Failures++;
                    if (m_Failures >= 5)
                    {
                        m_LockedUntil = DateTime.UtcNow.AddSeconds(60);
                        m_Failures = 0;
                    }
                }
                System.Threading.Thread.Sleep(600); // slows down guessing
                Reply(context, 401, "text/plain; charset=utf-8", "wrong password");
                return;
            }
            if (path == "/api/state" && request.HttpMethod == "GET")
            {
                if (!Authorized(request))
                {
                    Reply(context, 401, "text/plain; charset=utf-8", "login");
                    return;
                }
                Reply(context, 200, "application/json; charset=utf-8", m_State);
                return;
            }
            if (path == "/api/icon" && request.HttpMethod == "GET")
            {
                if (!Authorized(request))
                {
                    Reply(context, 401, "text/plain; charset=utf-8", "login");
                    return;
                }
                byte[] icon = ReadIcon(request.QueryString["n"]);
                if (icon == null)
                {
                    Reply(context, 404, "text/plain; charset=utf-8", "Not found");
                    return;
                }
                HttpListenerResponse response = context.Response;
                response.StatusCode = 200;
                response.ContentType = "image/png";
                response.ContentLength64 = icon.Length;
                response.Headers.Add("Cache-Control", "private, max-age=3600");
                response.Headers.Add("X-Content-Type-Options", "nosniff");
                response.OutputStream.Write(icon, 0, icon.Length);
                response.OutputStream.Close();
                return;
            }
            if (path == "/api/project" && request.HttpMethod == "GET")
            {
                if (!Authorized(request))
                {
                    Reply(context, 401, "text/plain; charset=utf-8", "login");
                    return;
                }
                Func<string> provider = Project;
                Reply(context, 200, "application/json; charset=utf-8", provider != null ? provider() : "[]");
                return;
            }
            if (path == "/api/cmd" && request.HttpMethod == "POST")
            {
                if (!Authorized(request))
                {
                    Reply(context, 401, "text/plain; charset=utf-8", "login");
                    return;
                }
                Dictionary<string, string> form = ParseForm(ReadBody(request));
                string cmd, arg;
                form.TryGetValue("cmd", out cmd);
                form.TryGetValue("arg", out arg);
                Func<string, string, string> exec = Execute;
                string result = (exec != null && !string.IsNullOrEmpty(cmd)) ? exec(cmd, arg ?? "") : "no";
                Reply(context, 200, "text/plain; charset=utf-8", result);
                return;
            }
            Reply(context, 404, "text/plain; charset=utf-8", "Not found");
        }
    }

    /// <summary>
    /// Starts cloudflared (free "quick tunnel", no account) which gives this computer a public https address
    /// without opening ports or knowing the IP.
    /// </summary>
    internal class CloudflareTunnel
    {
        private Process m_Process;
        private static readonly Regex UrlPattern = new Regex(@"https://[a-z0-9\-]+\.trycloudflare\.com", RegexOptions.IgnoreCase);

        public volatile string Url;
        public volatile string Status = "";

        private const string DownloadUrl = "https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe";

        // next to the program if it is there, otherwise in the user's data folder (where a download goes)
        public static string ExePath
        {
            get
            {
                string beside = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "cloudflared.exe");
                if (File.Exists(beside))
                    return beside;
                return Path.Combine(Ares.Settings.AppFolders.Root, "cloudflared.exe");
            }
        }

        public static bool Available
        {
            get { return File.Exists(ExePath); }
        }

        /// <summary>Downloads the official cloudflared.exe (about 20 MB). Returns null on success, otherwise the error.</summary>
        public static string Download()
        {
            string target = ExePath;
            string temp = target + ".part";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12;
                using (System.Net.WebClient client = new System.Net.WebClient())
                    client.DownloadFile(DownloadUrl, temp);
                if (new FileInfo(temp).Length < 1000000)
                    return "download incomplete";
                if (File.Exists(target))
                    File.Delete(target);
                File.Move(temp, target);
                return null;
            }
            catch (Exception ex)
            {
                try { if (File.Exists(temp)) File.Delete(temp); } catch (Exception swallowed) { Ares.Settings.ErrorLog.Note(swallowed); }
                return ex.Message;
            }
        }

        public bool Running
        {
            get
            {
                try { return m_Process != null && !m_Process.HasExited; }
                catch (Exception) { return false; }
            }
        }

        public bool Start(int port, out string error)
        {
            error = null;
            Stop();
            if (!File.Exists(ExePath))
            {
                error = "cloudflared.exe";
                return false;
            }
            try
            {
                ProcessStartInfo info = new ProcessStartInfo(ExePath,
                    "tunnel --no-autoupdate --url http://localhost:" + port + " --http-host-header localhost:" + port);
                info.UseShellExecute = false;
                info.CreateNoWindow = true;
                info.RedirectStandardError = true;
                info.RedirectStandardOutput = true;
                Process process = new Process();
                process.StartInfo = info;
                process.EnableRaisingEvents = true;
                DataReceivedEventHandler onData = (s, e) =>
                {
                    if (e.Data == null)
                        return;
                    Match m = UrlPattern.Match(e.Data);
                    if (m.Success)
                    {
                        Url = m.Value;
                        Status = "ok";
                    }
                    else if (e.Data.IndexOf("ERR", StringComparison.Ordinal) >= 0 && Url == null)
                    {
                        Status = e.Data.Length > 160 ? e.Data.Substring(0, 160) : e.Data;
                    }
                };
                process.OutputDataReceived += onData;
                process.ErrorDataReceived += onData;
                process.Exited += (s, e) =>
                {
                    Url = null;
                    if (Status == "ok" || Status == "starting")
                        Status = "ended";
                };
                Url = null;
                Status = "starting";
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                m_Process = process;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public void Stop()
        {
            Process process = m_Process;
            m_Process = null;
            Url = null;
            Status = "";
            if (process == null)
                return;
            try
            {
                if (!process.HasExited)
                    process.Kill();
            }
            catch (Exception swallowed) { Ares.Settings.ErrorLog.Note(swallowed); }
        }
    }

    public partial class Player
    {
        private RemoteServer m_Remote;
        private CloudflareTunnel m_Tunnel;
        private Timer m_RemoteTimer;
        private int m_RemoteInfoId = int.MinValue;
        private MusicInfo m_RemoteInfo;

        private void InitRemote()
        {
            try
            {
                m_Remote = new RemoteServer();
                m_Remote.Execute = RemoteExecute;
                // bridge for the Foundry VTT module (shows the playing music, lets a chosen player pause / skip)
                m_Bridge = new RemoteServer();
                m_Bridge.Bridge = true;
                m_Bridge.Execute = RemoteExecute;
                m_Bridge.BridgeKey = () => ShareDialog.CleanStreamId(Ares.CommonGUI.Preferences.Get("VdoId", ""));
                m_Bridge.BridgeAllows = (c) => c == "pause" || c == "stop" || c == "prev" || c == "next" || c == "repeat" || c == "mode" || c == "element";
                m_Bridge.Start(8791, "");
                m_Tunnel = new CloudflareTunnel();
                m_RemoteTimer = new Timer();
                m_RemoteTimer.Interval = 450;
                m_RemoteTimer.Tick += (s, e) => UpdateRemoteState();
                m_RemoteTimer.Start();
                ToolStripMenuItem item = new ToolStripMenuItem(T("Controle remoto (celular / internet)...", "Remote control (phone / internet)..."));
                item.Name = "remoteMenuItem";
                item.Click += (s, e) => ShowRemoteDialog();
                extrasToolStripMenuItem.DropDownItems.Insert(1, item);
                FormClosed += (s, e) =>
                {
                    m_RemoteTimer.Stop();
                    m_Tunnel.Stop();
                    m_Remote.Stop();
                    m_Bridge.Stop();
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        private RemoteServer m_Bridge;

        private void UpdateRemoteState()
        {
            bool remoteOn = m_Remote != null && m_Remote.Running;
            bool deckOn = m_Deck != null && m_Deck.Running;
            bool bridgeOn = m_Bridge != null && m_Bridge.Running && (DateTime.UtcNow - m_Bridge.LastUse).TotalSeconds < 20;
            if (!remoteOn && !deckOn && !bridgeOn)
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
                    if (id != m_RemoteInfoId)
                    {
                        m_RemoteInfoId = id;
                        m_RemoteInfo = MusicInfo.GetInfo(id);
                    }
                    control.GetMusicPosition(out position, out length);
                    paused = control.IsPaused();
                    Ares.Data.IElement list = Ares.Data.DataModule.ElementRepository.GetElement(control.CurrentMusicList);
                    if (list != null)
                        listTitle = list.Title;
                }
                else
                {
                    m_RemoteInfoId = int.MinValue;
                    m_RemoteInfo = null;
                }
                MusicInfo info = m_RemoteInfo;
                StringBuilder json = new StringBuilder();
                json.Append("{\"playing\":").Append(playing ? "true" : "false");
                json.Append(",\"paused\":").Append(paused ? "true" : "false");
                json.Append(",\"title\":").Append(JsonString(info != null ? info.Title : ""));
                json.Append(",\"artist\":").Append(JsonString(info != null ? info.Artist : ""));
                json.Append(",\"list\":").Append(JsonString(listTitle));
                json.Append(",\"position\":").Append(position.ToString("0.0", CultureInfo.InvariantCulture));
                json.Append(",\"length\":").Append(length.ToString("0.0", CultureInfo.InvariantCulture));
                json.Append(",\"repeat\":").Append(control.MusicRepeatMode != 0 ? "true" : "false");
                json.Append(",\"repeatMode\":").Append(control.MusicRepeatMode);
                json.Append(",\"vol\":{\"overall\":").Append(control.GlobalVolume);
                json.Append(",\"music\":").Append(control.MusicVolume);
                json.Append(",\"sound\":").Append(control.SoundVolume).Append("}");

                int modeIndex = -1;
                json.Append(",\"modes\":[");
                if (m_Project != null)
                {
                    Ares.Data.IMode current = control.CurrentMode;
                    int i = 0;
                    foreach (Ares.Data.IMode mode in m_Project.GetModes())
                    {
                        if (i > 0)
                            json.Append(",");
                        json.Append(JsonString(mode.Title));
                        if (current != null && mode.Title == current.Title && modeIndex < 0)
                            modeIndex = i;
                        i++;
                    }
                }
                json.Append("],\"tree\":[");
                if (m_Project != null)
                {
                    bool firstFolder = true;
                    foreach (FolderInfo folder in FolderRows())
                    {
                        if (!firstFolder)
                            json.Append(",");
                        firstFolder = false;
                        json.Append("{\"i\":").Append(folder.Index).Append(",\"p\":").Append(folder.Parent)
                            .Append(",\"d\":").Append(folder.Depth).Append("}");
                    }
                }
                json.Append("],\"mode\":").Append(modeIndex);
                HashSet<int> active = new HashSet<int>();
                foreach (Ares.Data.IModeElement playingElement in control.CurrentModeElements)
                    active.Add(playingElement.Id);
                json.Append(",\"elements\":[");
                if (m_Project != null && modeIndex >= 0)
                {
                    Ares.Data.IMode mode = m_Project.GetModes()[modeIndex];
                    bool first = true;
                    foreach (Ares.Data.IModeElement me in mode.GetElements())
                    {
                        if (!me.IsVisibleInPlayer)
                            continue;
                        if (!first)
                            json.Append(",");
                        first = false;
                        json.Append("{\"id\":").Append(me.Id).Append(",\"title\":").Append(JsonString(me.Title));
                        json.Append(",\"active\":").Append(active.Contains(me.Id) ? "true" : "false");
                        AppendLook(json, mode.Title, me.Title);
                        json.Append("}");
                    }
                }
                // the buttons of the subfolders of the open folder, one group (with the name of the subfolder) after the other
                json.Append("],\"groups\":[");
                if (m_Project != null && modeIndex >= 0)
                {
                    Ares.Data.IMode shownMode = m_Project.GetModes()[modeIndex];
                    bool firstGroup = true, openGroup = false, firstInGroup = true;
                    foreach (ShownButton sub in CollectShownButtons(shownMode))
                    {
                        if (sub.OwnerTitle == shownMode.Title)
                            continue;
                        if (sub.Header != null)
                        {
                            if (openGroup)
                                json.Append("]}");
                            if (!firstGroup)
                                json.Append(",");
                            firstGroup = false;
                            openGroup = true;
                            firstInGroup = true;
                            json.Append("{\"title\":").Append(JsonString(sub.Header)).Append(",\"elements\":[");
                        }
                        if (!firstInGroup)
                            json.Append(",");
                        firstInGroup = false;
                        json.Append("{\"id\":").Append(sub.Element.Id).Append(",\"title\":").Append(JsonString(sub.Element.Title));
                        json.Append(",\"active\":").Append(active.Contains(sub.Element.Id) ? "true" : "false");
                        AppendLook(json, sub.OwnerTitle, sub.Element.Title);
                        json.Append("}");
                    }
                    if (openGroup)
                        json.Append("]}");
                }
                // ids of everything which is playing (also buttons of other folders): for the keys of the Stream Deck
                json.Append("],\"active\":[").Append(string.Join(",", active.Select(a => a.ToString()).ToArray())).Append("]}");
                string stateText = json.ToString();
                if (remoteOn)
                    m_Remote.SetState(stateText);
                if (deckOn)
                    m_Deck.SetState(stateText);
                if (bridgeOn)
                    m_Bridge.SetState(stateText);
            }
            catch (Exception swallowed)
            {
                // the remote control must never disturb playing
                Ares.Settings.ErrorLog.Note(swallowed);
            }
        }

        // runs the command from the web page on the user interface thread
        private string RemoteExecute(string cmd, string arg)
        {
            string result = "ok";
            try
            {
                Invoke(new System.Windows.Forms.MethodInvoker(() =>
                {
                    PlayingControl control = m_PlayingControl;
                    if (control == null)
                    {
                        result = "no";
                        return;
                    }
                    int n;
                    double d;
                    switch (cmd)
                    {
                        case "pause": control.TogglePause(); break;
                        case "stop": StopAllPlaying(); break;
                        case "prev": previousButton_Click(null, EventArgs.Empty); break;
                        case "next": nextButton_Click(null, EventArgs.Empty); break;
                        case "repeat": repeatButton_Click(null, EventArgs.Empty); break;
                        case "seek":
                            if (double.TryParse(arg, NumberStyles.Float, CultureInfo.InvariantCulture, out d) && d >= 0)
                                control.SeekMusic(d);
                            break;
                        case "vol_overall":
                            if (int.TryParse(arg, out n)) { overallVolumeBar.Value = Math.Max(0, Math.Min(100, n)); overallVolumeBar_Scroll(null, EventArgs.Empty); }
                            break;
                        case "vol_music":
                            if (int.TryParse(arg, out n)) { musicVolumeBar.Value = Math.Max(0, Math.Min(100, n)); musicVolumeBar_Scroll(null, EventArgs.Empty); }
                            break;
                        case "vol_sound":
                            if (int.TryParse(arg, out n)) { soundVolumeBar.Value = Math.Max(0, Math.Min(100, n)); soundVolumeBar_Scroll(null, EventArgs.Empty); }
                            break;
                        case "mode":
                            if (int.TryParse(arg, out n) && m_Project != null)
                            {
                                var modes = m_Project.GetModes();
                                if (n >= 0 && n < modes.Count)
                                    control.SetMode(modes[n].Title);
                            }
                            break;
                        case "element":
                            if (int.TryParse(arg, out n))
                                control.SwitchElement(n);
                            break;
                        default:
                            result = "unknown";
                            break;
                    }
                }));
            }
            catch (Exception)
            {
                result = "error";
            }
            return result;
        }

        private static string RandomPassword()
        {
            const string chars = "abcdefghjkmnpqrstuvwxyz23456789";
            byte[] bytes = new byte[10];
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
                rng.GetBytes(bytes);
            StringBuilder sb = new StringBuilder();
            foreach (byte b in bytes)
                sb.Append(chars[b % chars.Length]);
            return sb.ToString();
        }

        private void ShowRemoteDialog()
        {
            if (m_Remote == null)
                return;
            using (Form form = new Form())
            {
                form.Text = T("Controle remoto", "Remote control");
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.StartPosition = FormStartPosition.CenterParent;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.ShowInTaskbar = false;
                form.ClientSize = new Size(780, 470);
                form.Icon = Ares.CommonGUI.ApoloBrand.GetIcon(32, Ares.CommonGUI.ApoloState.Playing, 0);

                Label intro = new Label();
                intro.SetBounds(16, 12, 740, 56);
                intro.Text = T(
                    "Controle o Apolo pelo celular ou deixe outra pessoa controlar pela internet. Ao ligar, o Apolo cria um link seguro (https) — sem IP, sem abrir portas. Abra o link (ou leia o QR code) no celular e digite a senha.",
                    "Control Apolo from your phone, or let someone else control it over the internet. When switched on, Apolo creates a secure (https) link - no IP, no open ports. Open the link (or scan the QR code) on the phone and type the password.");

                Label pwLabel = new Label();
                pwLabel.SetBounds(16, 80, 70, 26);
                pwLabel.Text = T("Senha:", "Password:");
                pwLabel.TextAlign = ContentAlignment.MiddleLeft;
                TextBox pw = new TextBox();
                pw.SetBounds(90, 80, 200, 26);
                string saved = Ares.CommonGUI.Preferences.Get("RemotePassword", "");
                pw.Text = string.IsNullOrEmpty(saved) ? RandomPassword() : saved;
                pw.UseSystemPasswordChar = true;
                CheckBox show = new CheckBox();
                show.SetBounds(300, 80, 140, 26);
                show.Text = T("Mostrar", "Show");
                show.CheckedChanged += (s, e) => pw.UseSystemPasswordChar = !show.Checked;
                Button copyPw = new Button();
                copyPw.SetBounds(446, 78, 130, 30);
                copyPw.Text = T("Copiar senha", "Copy password");
                copyPw.Click += (s, e) =>
                {
                    try { Clipboard.SetText(pw.Text); } catch (Exception swallowed) { Ares.Settings.ErrorLog.Note(swallowed); }
                };

                Button toggle = new Button();
                toggle.SetBounds(16, 124, 220, 38);
                Label status = new Label();
                status.SetBounds(248, 124, 328, 38);
                status.TextAlign = ContentAlignment.MiddleLeft;

                Label linkLabel = new Label();
                linkLabel.SetBounds(16, 178, 560, 22);
                linkLabel.Text = T("Link para abrir no celular:", "Link to open on the phone:");
                TextBox link = new TextBox();
                link.SetBounds(16, 202, 560, 26);
                link.ReadOnly = true;
                Button copy = new Button();
                copy.SetBounds(16, 238, 130, 32);
                copy.Text = T("Copiar link", "Copy link");
                Button open = new Button();
                open.SetBounds(156, 238, 130, 32);
                open.Text = T("Abrir aqui", "Open here");

                // QR code of the link (black on white, so every camera reads it)
                PictureBox qr = new PictureBox();
                qr.SetBounds(596, 80, 168, 168);
                qr.SizeMode = PictureBoxSizeMode.Zoom;
                qr.BackColor = Color.White;
                Button copyQr = new Button();
                copyQr.SetBounds(596, 256, 82, 30);
                copyQr.Text = T("Copiar QR", "Copy QR");
                Button saveQr = new Button();
                saveQr.SetBounds(682, 256, 82, 30);
                saveQr.Text = T("Salvar QR", "Save QR");
                string qrFor = null;
                Bitmap qrBitmap = null;
                copyQr.Click += (s, e) =>
                {
                    if (qrBitmap != null)
                    {
                        try { Clipboard.SetImage(qrBitmap); } catch (Exception swallowed) { Ares.Settings.ErrorLog.Note(swallowed); }
                    }
                };
                saveQr.Click += (s, e) =>
                {
                    if (qrBitmap == null)
                        return;
                    using (SaveFileDialog dlg = new SaveFileDialog())
                    {
                        dlg.Filter = "PNG|*.png";
                        dlg.FileName = "apolo-qr.png";
                        if (dlg.ShowDialog(form) == DialogResult.OK)
                        {
                            try { qrBitmap.Save(dlg.FileName, System.Drawing.Imaging.ImageFormat.Png); } catch (Exception swallowed) { Ares.Settings.ErrorLog.Note(swallowed); }
                        }
                    }
                };

                Label warn = new Label();
                warn.SetBounds(16, 300, 748, 120);
                warn.Text = T(
                    "• Quem tiver o link E a senha controla o Apolo. Use uma senha forte e desligue ao terminar.\r\n• O link muda toda vez que você liga; a senha fica salva.\r\n• O QR code só contém o link — a senha você envia separada.\r\n• O acesso passa pelo túnel gratuito da Cloudflare (sem conta). O Apolo precisa ficar aberto neste computador.\r\n• Depois de 5 senhas erradas, o acesso é bloqueado por 1 minuto.",
                    "• Anyone with the link AND the password controls Apolo. Use a strong password and switch it off when done.\r\n• The link changes every time you switch it on; the password is kept.\r\n• The QR code only holds the link - send the password separately.\r\n• Access goes through the free Cloudflare tunnel (no account). Apolo must stay open on this computer.\r\n• After 5 wrong passwords access is blocked for 1 minute.");
                warn.ForeColor = Color.Gray;

                Action refresh = () =>
                {
                    bool on = m_Remote.Running;
                    toggle.Text = on ? T("Desligar controle remoto", "Switch off") : T("Ligar controle remoto", "Switch on");
                    pw.ReadOnly = on; // can still be selected and copied while it is on
                    string url = m_Tunnel.Url;
                    link.Text = url ?? "";
                    copy.Enabled = open.Enabled = url != null;
                    if (url != qrFor)
                    {
                        qrFor = url;
                        Bitmap old = qrBitmap;
                        qrBitmap = url != null ? QrCode.Render(url, 8) : null;
                        qr.Image = qrBitmap;
                        if (old != null)
                            old.Dispose();
                    }
                    copyQr.Enabled = saveQr.Enabled = qrBitmap != null;
                    if (!on)
                        status.Text = T("Desligado.", "Off.");
                    else if (url != null)
                        status.Text = T("Ligado. Pronto para usar.", "On. Ready to use.");
                    else if (m_Tunnel.Status == "starting")
                        status.Text = T("Criando o link (pode levar alguns segundos)...", "Creating the link (a few seconds)...");
                    else
                        status.Text = T("Problema no túnel: ", "Tunnel problem: ") + m_Tunnel.Status;
                };
                toggle.Click += (s, e) =>
                {
                    if (m_Remote.Running)
                    {
                        m_Tunnel.Stop();
                        m_Remote.Stop();
                    }
                    else
                    {
                        string password = pw.Text.Trim();
                        if (password.Length < 4)
                        {
                            MessageBox.Show(form, T("A senha precisa ter pelo menos 4 caracteres.", "The password needs at least 4 characters."),
                                StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                        if (!CloudflareTunnel.Available)
                        {
                            if (MessageBox.Show(form, T("Falta o componente que cria o link (cloudflared, programa oficial e gratuito da Cloudflare, ~20 MB).\n\nBaixar agora do site oficial (github.com/cloudflare/cloudflared)? Só é preciso uma vez.",
                                "The component that creates the link is missing (cloudflared, the official free Cloudflare program, ~20 MB).\n\nDownload it now from the official site (github.com/cloudflare/cloudflared)? This is needed only once."),
                                StringResources.Ares, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                                return;
                            Cursor oldCursor = form.Cursor;
                            form.Cursor = Cursors.WaitCursor;
                            status.Text = T("Baixando...", "Downloading...");
                            status.Refresh();
                            string dlError = CloudflareTunnel.Download();
                            form.Cursor = oldCursor;
                            if (dlError != null)
                            {
                                MessageBox.Show(form, T("Não consegui baixar: ", "Download failed: ") + dlError,
                                    StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Error);
                                refresh();
                                return;
                            }
                        }
                        Ares.CommonGUI.Preferences.Set("RemotePassword", password);
                        if (!m_Remote.Start(8766, password))
                        {
                            MessageBox.Show(form, T("Não consegui iniciar o servidor local: ", "Could not start the local server: ") + m_Remote.LastError,
                                StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                        string error;
                        if (!m_Tunnel.Start(m_Remote.Port, out error))
                        {
                            m_Remote.Stop();
                            MessageBox.Show(form, T("Não consegui iniciar o túnel: ", "Could not start the tunnel: ") + error,
                                StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    refresh();
                };
                copy.Click += (s, e) =>
                {
                    try { Clipboard.SetText(link.Text); } catch (Exception swallowed) { Ares.Settings.ErrorLog.Note(swallowed); }
                };
                open.Click += (s, e) => OpenInBrowser(link.Text);

                Timer tick = new Timer();
                tick.Interval = 500;
                tick.Tick += (s, e) => refresh();
                tick.Start();

                Button close = new Button();
                close.Text = T("Fechar", "Close");
                close.DialogResult = DialogResult.OK;
                close.SetBounds(780 - 16 - 100, 470 - 12 - 34, 100, 34);

                form.Controls.AddRange(new Control[] { intro, pwLabel, pw, show, copyPw, toggle, status, linkLabel, link, copy, open, qr, copyQr, saveQr, warn, close });
                form.AcceptButton = close;
                refresh();
                Ares.CommonGUI.ThemeManager.Apply(form);
                form.ShowDialog(this);
                tick.Stop();
                tick.Dispose();
                qr.Image = null;
                if (qrBitmap != null)
                    qrBitmap.Dispose();
            }
        }
    }
}
