using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

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
        /// <summary>Folder of the button pictures given to the page (null = the icons folder of Apolo; the tests use a temporary one).</summary>
        public string IconsFolder;

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
        private static byte[] ReadIcon(string folder, string name)
        {
            try
            {
                if (string.IsNullOrEmpty(name))
                    return null;
                string file = System.IO.Path.GetFileName(name);
                if (file != name || !file.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    return null;
                string full = System.IO.Path.Combine(folder ?? Ares.AppCore.ButtonDecor.IconsFolder, file);
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
                byte[] icon = ReadIcon(IconsFolder, request.QueryString["n"]);
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
}
