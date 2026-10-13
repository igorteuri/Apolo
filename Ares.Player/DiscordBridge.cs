/*
 Apolo - a fork of Ares (Copyright (c) Jörg Ruedenauer), distributed under the GPL.
*/
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace Ares.Player
{
    public class DiscordChannelInfo
    {
        public string Id;
        public string Name;
        public override string ToString() { return Name; }
    }

    public class DiscordGuildInfo
    {
        public string Id;
        public string Name;
        public List<DiscordChannelInfo> Channels = new List<DiscordChannelInfo>();
        public override string ToString() { return Name; }
    }

    public enum DiscordState
    {
        Off,        // nothing running
        Starting,   // helper program starting / bot logging in
        Ready,      // bot is online but not in a voice channel
        InVoice     // sending sound to a voice channel
    }

    /// <summary>
    /// Keeps the bot token on this computer only, encrypted for the current Windows user.
    /// </summary>
    public static class DiscordTokenStore
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Apolo.Discord.v1");

        public static string Load()
        {
            try
            {
                string text = Ares.CommonGUI.Preferences.Get("DiscordToken", "");
                if (string.IsNullOrEmpty(text))
                    return "";
                byte[] plain = ProtectedData.Unprotect(Convert.FromBase64String(text), Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plain);
            }
            catch (Exception)
            {
                return "";
            }
        }

        public static void Save(string token)
        {
            try
            {
                if (string.IsNullOrEmpty(token))
                {
                    Ares.CommonGUI.Preferences.Set("DiscordToken", "");
                    return;
                }
                byte[] cipher = ProtectedData.Protect(Encoding.UTF8.GetBytes(token), Entropy, DataProtectionScope.CurrentUser);
                Ares.CommonGUI.Preferences.Set("DiscordToken", Convert.ToBase64String(cipher));
            }
            catch (Exception)
            {
            }
        }
    }

    /// <summary>
    /// Runs the small Node.js helper (folder "discord" next to the program) which keeps the Discord bot online
    /// and plays the captured sound of Apolo in a voice channel. Talks to it through a local socket.
    /// </summary>
    public class DiscordBridge : IDisposable
    {
        private const byte FramePcm = 1;
        private const byte FrameCommand = 2;
        private const byte FrameEvent = 3;

        public event EventHandler Changed;

        private readonly object m_Lock = new object();
        private readonly object m_WriteLock = new object();
        private TcpListener m_Listener;
        private TcpClient m_Client;
        private NetworkStream m_Stream;
        private Process m_Process;
        private string m_Secret;
        private string m_Token;
        private AppAudioCapture m_Capture;
        private readonly StringBuilder m_Output = new StringBuilder();

        public DiscordState State { get; private set; }
        public string UserName { get; private set; }
        public string AppId { get; private set; }
        public string Error { get; private set; }
        public string CaptureError { get; private set; }
        public string JoinedGuild { get; private set; }
        public string JoinedChannel { get; private set; }
        public string LastLog { get; private set; }
        public List<DiscordGuildInfo> Guilds { get; private set; }

        public DiscordBridge()
        {
            Guilds = new List<DiscordGuildInfo>();
        }

        private static string Folder
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "discord"); }
        }

        public static string NodePath { get { return Path.Combine(Folder, "node.exe"); } }
        public static string ScriptPath { get { return Path.Combine(Folder, "discord-bridge.js"); } }

        /// <summary>True when the helper program (Node.js + libraries) is installed next to Apolo.</summary>
        public static bool Available
        {
            get
            {
                return File.Exists(NodePath) && File.Exists(ScriptPath)
                    && Directory.Exists(Path.Combine(Folder, "node_modules"));
            }
        }

        private void RaiseChanged()
        {
            EventHandler h = Changed;
            if (h != null)
            {
                try { h(this, EventArgs.Empty); } catch (Exception) { }
            }
        }

        /// <summary>Starts the helper and logs the bot in. Returns an error text, or null.</summary>
        public string Connect(string token)
        {
            lock (m_Lock)
            {
                if (State != DiscordState.Off)
                    return null;
                if (!Available)
                    return "missing";
                Error = null;
                CaptureError = null;
                UserName = null;
                AppId = null;
                JoinedGuild = JoinedChannel = null;
                Guilds = new List<DiscordGuildInfo>();
                m_Token = token;
                m_Secret = Guid.NewGuid().ToString("N");
                m_Output.Length = 0;
                try
                {
                    m_Listener = new TcpListener(IPAddress.Loopback, 0);
                    m_Listener.Start(1);
                    int port = ((IPEndPoint)m_Listener.LocalEndpoint).Port;
                    ProcessStartInfo info = new ProcessStartInfo(NodePath,
                        "\"" + ScriptPath + "\" " + port + " " + m_Secret);
                    info.WorkingDirectory = Folder;
                    info.UseShellExecute = false;
                    info.CreateNoWindow = true;
                    info.RedirectStandardOutput = true;
                    info.RedirectStandardError = true;
                    m_Process = Process.Start(info);
                    m_Process.OutputDataReceived += OnProcessOutput;
                    m_Process.ErrorDataReceived += OnProcessOutput;
                    m_Process.BeginOutputReadLine();
                    m_Process.BeginErrorReadLine();
                    State = DiscordState.Starting;
                    Thread t = new Thread(AcceptLoop);
                    t.IsBackground = true;
                    t.Name = "Apolo Discord socket";
                    t.Start(m_Listener);
                }
                catch (Exception ex)
                {
                    Error = ex.Message;
                    CleanUp();
                    State = DiscordState.Off;
                    RaiseChanged();
                    return Error;
                }
            }
            RaiseChanged();
            return null;
        }

        private void OnProcessOutput(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Data))
                return;
            lock (m_Output)
            {
                if (m_Output.Length < 4000)
                    m_Output.AppendLine(e.Data);
            }
        }

        private void AcceptLoop(object state)
        {
            TcpListener listener = (TcpListener)state;
            TcpClient client = null;
            try
            {
                // the helper must connect within 20 seconds
                DateTime end = DateTime.UtcNow.AddSeconds(20);
                while (DateTime.UtcNow < end)
                {
                    if (listener.Pending())
                    {
                        client = listener.AcceptTcpClient();
                        break;
                    }
                    Thread.Sleep(50);
                }
                if (client == null)
                    throw new InvalidOperationException("The Discord helper did not start.");
                try { listener.Stop(); } catch (Exception) { }
                client.NoDelay = true;
                lock (m_Lock)
                {
                    if (listener != m_Listener)
                    {
                        client.Close();
                        return;
                    }
                    m_Client = client;
                    m_Stream = client.GetStream();
                }
                ReadLoop(client.GetStream());
            }
            catch (Exception ex)
            {
                lock (m_Lock)
                {
                    if (State != DiscordState.Off && Error == null)
                        Error = ex.Message;
                }
            }
            lock (m_Lock)
            {
                if (m_Listener == listener)
                {
                    // the helper ended on its own
                    if (State != DiscordState.Off && Error == null)
                    {
                        string output;
                        lock (m_Output) { output = m_Output.ToString().Trim(); }
                        Error = output.Length > 0 ? output : "The Discord helper stopped.";
                    }
                    CleanUp();
                    State = DiscordState.Off;
                }
            }
            RaiseChanged();
        }

        private void ReadLoop(NetworkStream stream)
        {
            byte[] head = new byte[5];
            while (true)
            {
                if (!ReadExactly(stream, head, 5))
                    return;
                int len = (head[1] << 24) | (head[2] << 16) | (head[3] << 8) | head[4];
                if (len < 0 || len > 4 * 1024 * 1024)
                    return;
                byte[] payload = new byte[len];
                if (len > 0 && !ReadExactly(stream, payload, len))
                    return;
                if (head[0] == FrameEvent)
                    HandleEvent(Encoding.UTF8.GetString(payload));
            }
        }

        private static bool ReadExactly(NetworkStream stream, byte[] buffer, int count)
        {
            int got = 0;
            while (got < count)
            {
                int n;
                try { n = stream.Read(buffer, got, count - got); }
                catch (Exception) { return false; }
                if (n <= 0)
                    return false;
                got += n;
            }
            return true;
        }

        private static string Str(IDictionary d, string key)
        {
            object o = d.Contains(key) ? d[key] : null;
            return o != null ? o.ToString() : null;
        }

        private void HandleEvent(string json)
        {
            IDictionary ev;
            try { ev = new JavaScriptSerializer().DeserializeObject(json) as IDictionary; }
            catch (Exception) { return; }
            if (ev == null)
                return;
            string name = Str(ev, "ev");
            switch (name)
            {
                case "hello":
                    if (Str(ev, "secret") != m_Secret)
                    {
                        lock (m_Lock) { Error = "wrong secret"; }
                        Disconnect();
                        return;
                    }
                    SendCommand(new Dictionary<string, object> { { "cmd", "login" }, { "token", m_Token } });
                    m_Token = null;
                    break;
                case "fatal":
                    lock (m_Lock) { Error = Str(ev, "message"); }
                    Disconnect();
                    return;
                case "loginError":
                    lock (m_Lock) { Error = Str(ev, "message") ?? "login failed"; }
                    Disconnect();
                    return;
                case "ready":
                    lock (m_Lock)
                    {
                        UserName = Str(ev, "user");
                        AppId = Str(ev, "appId");
                        Guilds = ParseGuilds(ev["guilds"]);
                        if (State == DiscordState.Starting)
                            State = DiscordState.Ready;
                    }
                    break;
                case "guilds":
                    lock (m_Lock) { Guilds = ParseGuilds(ev["guilds"]); }
                    break;
                case "voice":
                    HandleVoice(ev);
                    break;
                case "log":
                    LastLog = Str(ev, "message");
                    break;
            }
            RaiseChanged();
        }

        private void HandleVoice(IDictionary ev)
        {
            string state = Str(ev, "state");
            if (state == "joined")
            {
                lock (m_Lock)
                {
                    JoinedGuild = Str(ev, "guild");
                    JoinedChannel = Str(ev, "channel");
                    Error = null;
                    State = DiscordState.InVoice;
                }
                StartCapture();
            }
            else
            {
                StopCapture();
                lock (m_Lock)
                {
                    JoinedGuild = JoinedChannel = null;
                    if (state == "error")
                        Error = Str(ev, "message");
                    if (State == DiscordState.InVoice)
                        State = DiscordState.Ready;
                }
            }
        }

        private static List<DiscordGuildInfo> ParseGuilds(object value)
        {
            List<DiscordGuildInfo> result = new List<DiscordGuildInfo>();
            IEnumerable list = value as IEnumerable;
            if (list == null)
                return result;
            foreach (object o in list)
            {
                IDictionary g = o as IDictionary;
                if (g == null)
                    continue;
                DiscordGuildInfo guild = new DiscordGuildInfo { Id = Str(g, "id"), Name = Str(g, "name") };
                IEnumerable channels = g["channels"] as IEnumerable;
                if (channels != null)
                {
                    foreach (object c in channels)
                    {
                        IDictionary cd = c as IDictionary;
                        if (cd != null)
                            guild.Channels.Add(new DiscordChannelInfo { Id = Str(cd, "id"), Name = Str(cd, "name") });
                    }
                }
                result.Add(guild);
            }
            return result;
        }

        // ---------- capture of Apolo's own sound ----------

        private void StartCapture()
        {
            StopCapture();
            AppAudioCapture capture = new AppAudioCapture(Process.GetCurrentProcess().Id);
            capture.DataAvailable += SendPcm;
            string error = capture.Start();
            lock (m_Lock)
            {
                if (error != null)
                {
                    CaptureError = error;
                    capture.Dispose();
                }
                else
                {
                    CaptureError = null;
                    m_Capture = capture;
                }
            }
        }

        private void StopCapture()
        {
            AppAudioCapture capture;
            lock (m_Lock)
            {
                capture = m_Capture;
                m_Capture = null;
            }
            if (capture != null)
                capture.Dispose();
        }

        private void SendPcm(byte[] buffer, int count)
        {
            Write(FramePcm, buffer, count);
        }

        // ---------- commands ----------

        private void Write(byte type, byte[] data, int count)
        {
            NetworkStream stream = m_Stream;
            if (stream == null)
                return;
            try
            {
                lock (m_WriteLock)
                {
                    byte[] head = new byte[5];
                    head[0] = type;
                    head[1] = (byte)(count >> 24);
                    head[2] = (byte)(count >> 16);
                    head[3] = (byte)(count >> 8);
                    head[4] = (byte)count;
                    stream.Write(head, 0, 5);
                    if (count > 0)
                        stream.Write(data, 0, count);
                }
            }
            catch (Exception)
            {
            }
        }

        private void SendCommand(Dictionary<string, object> command)
        {
            byte[] body = Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(command));
            Write(FrameCommand, body, body.Length);
        }

        public void Join(string guildId, string channelId)
        {
            lock (m_Lock) { Error = null; }
            SendCommand(new Dictionary<string, object> { { "cmd", "join" }, { "guildId", guildId }, { "channelId", channelId } });
            RaiseChanged();
        }

        public void Leave()
        {
            SendCommand(new Dictionary<string, object> { { "cmd", "leave" } });
        }

        public void RefreshGuilds()
        {
            SendCommand(new Dictionary<string, object> { { "cmd", "refresh" } });
        }

        /// <summary>Takes the bot offline and stops the helper.</summary>
        public void Disconnect()
        {
            StopCapture();
            lock (m_Lock)
            {
                if (m_Stream != null)
                    SendCommand(new Dictionary<string, object> { { "cmd", "quit" } });
                CleanUp();
                State = DiscordState.Off;
                JoinedGuild = JoinedChannel = null;
            }
            RaiseChanged();
        }

        private void CleanUp()
        {
            try { if (m_Listener != null) m_Listener.Stop(); } catch (Exception) { }
            m_Listener = null;
            try { if (m_Client != null) m_Client.Close(); } catch (Exception) { }
            m_Client = null;
            m_Stream = null;
            try
            {
                if (m_Process != null && !m_Process.HasExited)
                {
                    if (!m_Process.WaitForExit(500))
                        m_Process.Kill();
                }
            }
            catch (Exception) { }
            m_Process = null;
            m_Token = null;
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
