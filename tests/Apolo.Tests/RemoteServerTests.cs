using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using Ares.Player;
using Xunit;

namespace Apolo.Tests
{
    // The remote control is reached from the internet (Cloudflare tunnel): these tests talk to the real
    // little web server over HTTP and check that nothing works without the password / the key.
    public class RemoteServerTests : IDisposable
    {
        private const string Password = "senha-do-mestre";
        private readonly string m_Icons = Path.Combine(Path.GetTempPath(), "apolo-remote-" + Guid.NewGuid().ToString("N"));
        private readonly RemoteServer m_Server = new RemoteServer();
        private readonly HttpClient m_Http = new HttpClient();
        private readonly List<string> m_Executed = new List<string>();

        public RemoteServerTests()
        {
            Directory.CreateDirectory(m_Icons);
            m_Server.IconsFolder = m_Icons;
            m_Server.Execute = (cmd, arg) => { lock (m_Executed) m_Executed.Add(cmd + "|" + arg); return "ok"; };
            m_Server.Project = () => "[{\"title\":\"Taverna\"}]";
            m_Server.SetState("{\"playing\":\"Chuva\"}");
        }

        public void Dispose()
        {
            m_Server.Stop();
            m_Http.Dispose();
            try { Directory.Delete(m_Icons, true); } catch (Exception) { }
        }

        private static int FreePort()
        {
            TcpListener probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            int port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }

        private string Url(string path)
        {
            return "http://localhost:" + m_Server.Port + path;
        }

        private void Start(string password = Password)
        {
            Assert.True(m_Server.Start(FreePort(), password), m_Server.LastError);
            Assert.True(m_Server.Running);
        }

        private HttpResponseMessage Post(string path, string body, string header = null, string value = null)
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, Url(path));
            request.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/x-www-form-urlencoded");
            if (header != null)
                request.Headers.Add(header, value);
            return m_Http.Send(request);
        }

        private HttpResponseMessage Get(string path, string header = null, string value = null)
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, Url(path));
            if (header != null)
                request.Headers.Add(header, value);
            return m_Http.Send(request);
        }

        private static string Text(HttpResponseMessage response)
        {
            return response.Content.ReadAsStringAsync().Result;
        }

        private string Login()
        {
            HttpResponseMessage response = Post("/api/login", "password=" + Uri.EscapeDataString(Password));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            string token = Text(response);
            Assert.True(token.Length >= 20);
            return token;
        }

        [Fact]
        public void WithoutLogin_NothingIsGiven_AndNoCommandRuns()
        {
            Start();
            Assert.Equal(HttpStatusCode.Unauthorized, Get("/api/state").StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, Get("/api/project").StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, Get("/api/icon?n=a.png").StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, Post("/api/cmd", "cmd=stop").StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, Get("/api/state", "X-Token", "invented-token").StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, Get("/api/state?t=invented-token").StatusCode);
            Assert.Empty(m_Executed);
        }

        [Fact]
        public void WrongPassword_IsRefused_RightOneGivesAToken()
        {
            Start();
            Assert.Equal(HttpStatusCode.Unauthorized, Post("/api/login", "password=errada").StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, Post("/api/login", "").StatusCode);

            string token = Login();
            HttpResponseMessage state = Get("/api/state", "X-Token", token);
            Assert.Equal(HttpStatusCode.OK, state.StatusCode);
            Assert.Equal("{\"playing\":\"Chuva\"}", Text(state));
            Assert.Equal("no-store", state.Headers.CacheControl.ToString());

            // the token can also come in the address (used by the pictures of the page)
            Assert.Equal(HttpStatusCode.OK, Get("/api/project?t=" + Uri.EscapeDataString(token)).StatusCode);
            Assert.Equal("[{\"title\":\"Taverna\"}]", Text(Get("/api/project", "X-Token", token)));
        }

        [Fact]
        public void TooShortPassword_NeverOpens()
        {
            Start("abc");
            Assert.Equal(HttpStatusCode.Unauthorized, Post("/api/login", "password=abc").StatusCode);
        }

        [Fact]
        public void FiveWrongPasswords_LockTheLogin_EvenForTheRightOne()
        {
            Start();
            for (int i = 0; i < 5; i++)
                Assert.Equal(HttpStatusCode.Unauthorized, Post("/api/login", "password=chute" + i).StatusCode);
            HttpResponseMessage locked = Post("/api/login", "password=" + Password);
            Assert.Equal((HttpStatusCode)429, locked.StatusCode);
        }

        [Fact]
        public void Command_ReachesThePlayer_WithAccentsAndSpaces()
        {
            Start();
            string token = Login();
            HttpResponseMessage response = Post("/api/cmd", "cmd=play&arg=" + Uri.EscapeDataString("Ambiências da Taverna") + "+2", "X-Token", token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("ok", Text(response));
            Assert.Equal(new[] { "play|Ambiências da Taverna 2" }, m_Executed.ToArray());

            // a command without name is not given to the player
            Assert.Equal("no", Text(Post("/api/cmd", "arg=x", "X-Token", token)));
            Assert.Single(m_Executed);
        }

        [Fact]
        public void Restart_ForgetsTheOldTokens()
        {
            Start();
            string token = Login();
            m_Server.Stop();
            Assert.False(m_Server.Running);
            Start();
            Assert.Equal(HttpStatusCode.Unauthorized, Get("/api/state", "X-Token", token).StatusCode);
        }

        [Fact]
        public void Icon_OnlyPngFilesOfTheIconsFolder_AreGiven()
        {
            byte[] png = { 0x89, 0x50, 0x4E, 0x47, 1, 2, 3 };
            File.WriteAllBytes(Path.Combine(m_Icons, "dragão.png"), png);
            File.WriteAllText(Path.Combine(m_Icons, "segredo.txt"), "x");
            File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(m_Icons), "fora-" + Path.GetFileName(m_Icons) + ".png"), png);
            try
            {
                Start();
                string token = Login();
                HttpResponseMessage ok = Get("/api/icon?n=" + Uri.EscapeDataString("dragão.png"), "X-Token", token);
                Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
                Assert.Equal("image/png", ok.Content.Headers.ContentType.MediaType);
                Assert.Equal(png, ok.Content.ReadAsByteArrayAsync().Result);

                Assert.Equal(HttpStatusCode.NotFound, Get("/api/icon?n=segredo.txt", "X-Token", token).StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, Get("/api/icon?n=nao-existe.png", "X-Token", token).StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, Get("/api/icon?n=" + Uri.EscapeDataString("..\\fora-" + Path.GetFileName(m_Icons) + ".png"), "X-Token", token).StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, Get("/api/icon?n=" + Uri.EscapeDataString("../fora-" + Path.GetFileName(m_Icons) + ".png"), "X-Token", token).StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, Get("/api/icon", "X-Token", token).StatusCode);
            }
            finally
            {
                File.Delete(Path.Combine(Path.GetDirectoryName(m_Icons), "fora-" + Path.GetFileName(m_Icons) + ".png"));
            }
        }

        [Fact]
        public void UnknownAddress_IsNotFound()
        {
            Start();
            Assert.Equal(HttpStatusCode.NotFound, Get("/api/nada").StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, Get("/api/cmd").StatusCode);   // commands only by POST
        }

        [Fact]
        public void Bridge_NeedsTheKey_AndOnlyRunsAllowedCommands()
        {
            m_Server.Bridge = true;
            m_Server.BridgeKey = () => "chave-foundry";
            m_Server.BridgeAllows = cmd => cmd == "play";
            Start("");

            Assert.Equal(HttpStatusCode.Unauthorized, Get("/foundry/ping").StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, Get("/foundry/ping", "X-Apolo", "chave-errada").StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, Post("/foundry/cmd", "cmd=play&arg=1").StatusCode);
            Assert.Empty(m_Executed);

            HttpResponseMessage ping = Get("/foundry/ping", "X-Apolo", "chave-foundry");
            Assert.Equal("apolo", Text(ping));
            Assert.Equal("*", string.Join(",", ping.Headers.GetValues("Access-Control-Allow-Origin")));
            Assert.Equal("{\"playing\":\"Chuva\"}", Text(Get("/foundry/state", "X-Apolo", "chave-foundry")));

            Assert.Equal("ok", Text(Post("/foundry/cmd", "cmd=play&arg=7", "X-Apolo", "chave-foundry")));
            Assert.Equal("no", Text(Post("/foundry/cmd", "cmd=quit", "X-Apolo", "chave-foundry")));
            Assert.Equal(new[] { "play|7" }, m_Executed.ToArray());

            // the remote control addresses do not exist in the bridge
            Assert.Equal(HttpStatusCode.NotFound, Post("/api/login", "password=x", "X-Apolo", "chave-foundry").StatusCode);
        }

        [Fact]
        public void Bridge_WithAShortKey_RefusesEverything()
        {
            m_Server.Bridge = true;
            m_Server.BridgeKey = () => "abc";
            Start("");
            Assert.Equal(HttpStatusCode.Unauthorized, Get("/foundry/ping", "X-Apolo", "abc").StatusCode);
        }

        [Fact]
        public void Bridge_AnswersTheBrowserPreflight()
        {
            m_Server.Bridge = true;
            m_Server.BridgeKey = () => "chave-foundry";
            Start("");
            HttpResponseMessage response = m_Http.Send(new HttpRequestMessage(HttpMethod.Options, Url("/foundry/cmd")));
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Contains("X-Apolo", string.Join(",", response.Headers.GetValues("Access-Control-Allow-Headers")));
        }
    }
}
