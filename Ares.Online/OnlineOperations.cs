/*
    Copyright (c) 2015  [Joerg Ruedenauer]
  
    This file is part of Ares.

    Ares is free software; you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation; either version 2 of the License, or
    (at your option) any later version.

    Ares is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with Ares; if not, write to the Free Software
    Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA
 */
using System;
using System.Windows.Forms;
using System.ComponentModel;
using System.Text.RegularExpressions;

using Ares.CommonGUI;

namespace Ares.Online
{
    public class OnlineOperations
    {
        public static void ShowHelppage(String url, Form owner)
        {
            try
            {
               System.Diagnostics.Process.Start(url);
            }
            catch (Exception ex)
            {
                MessageBox.Show(owner, String.Format(StringResources.OpenHelpError, ex.Message), StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private const String RepositoryUrl = "https://github.com/igorteuri/Apolo";
        private const String LatestReleaseApi = "https://api.github.com/repos/igorteuri/Apolo/releases/latest";

        public static void ShowHomepage(Form owner)
        {
            String url = RepositoryUrl;
            try
            {
                System.Diagnostics.Process.Start(url);
            }
            catch (Exception ex)
            {
                MessageBox.Show(owner, String.Format(StringResources.OpenHomepageError, ex.Message), StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public static void CheckForUpdate(Form parent, bool verbose)
        {
            DoCheckForUpdate(parent, true, verbose, false);
        }

        public static void CheckForUpdateMGPlugin(Form parent)
        {
            DoCheckForUpdate(parent, false, true, false);
        }

        public static void CheckForNews(Form parent, bool manual)
        {
            String url = GetUrlBase();
            if (!System.Threading.Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName.StartsWith("de", StringComparison.InvariantCultureIgnoreCase))
            {
                url += "en/";
            }
            url += "ares_news.html";
            FileDownloader<bool> downloader = new FileDownloader<bool>(parent, manual, manual);
            downloader.Download(url, StringResources.SearchingNews, NewsDownloaded);
        }

        private static bool IsPt
        {
            get { return System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt"; }
        }

        private static string Tx(string pt, string en)
        {
            return IsPt ? pt : en;
        }

        private static Version ParseVersion(string text)
        {
            if (String.IsNullOrEmpty(text))
                return null;
            Match m = Regex.Match(text, @"(\d+)(?:\.(\d+))?(?:\.(\d+))?(?:\.(\d+))?");
            if (!m.Success)
                return null;
            int[] parts = new int[4];
            for (int i = 0; i < 4; ++i)
            {
                if (m.Groups[i + 1].Success)
                    int.TryParse(m.Groups[i + 1].Value, out parts[i]);
            }
            return new Version(parts[0], parts[1], parts[2], parts[3]);
        }

        private static string JsonValue(string json, string name)
        {
            Match m = Regex.Match(json, "\"" + name + "\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            return m.Success ? m.Groups[1].Value.Replace("\\/", "/") : null;
        }

        /// <summary>Looks for a newer version among the releases of Apolo on GitHub (silent unless verbose).</summary>
        private static void CheckApoloRelease(Form parent, bool verbose)
        {
            Version current = System.Reflection.Assembly.GetEntryAssembly() != null
                ? System.Reflection.Assembly.GetEntryAssembly().GetName().Version : null;
            System.Net.WebClient client = new System.Net.WebClient();
            client.Headers.Add("User-Agent", "Apolo");
            client.Encoding = System.Text.Encoding.UTF8;
            try
            {
                System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12;
            }
            catch (Exception)
            {
            }
            client.DownloadStringCompleted += (sender, e) =>
            {
                client.Dispose();
                Action show = () =>
                {
                    try
                    {
                        if (e.Error != null || e.Cancelled)
                        {
                            if (verbose)
                                MessageBox.Show(parent, Tx("Não consegui consultar as versões publicadas. Veja se há internet ou se já existe alguma versão publicada em ", "Could not look for new versions. Check the internet connection, or whether a version was published at ") + RepositoryUrl + "/releases",
                                    StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return;
                        }
                        string tag = JsonValue(e.Result, "tag_name");
                        string page = JsonValue(e.Result, "html_url") ?? (RepositoryUrl + "/releases");
                        Version latest = ParseVersion(tag);
                        if (latest == null || current == null)
                            return;
                        if (latest > current)
                        {
                            if (MessageBox.Show(parent,
                                Tx("Há uma nova versão do Apolo: " + tag + " (você tem " + current.ToString(3) + ").\r\n\r\nAbrir a página de download?",
                                   "A new version of Apolo is available: " + tag + " (you have " + current.ToString(3) + ").\r\n\r\nOpen the download page?"),
                                StringResources.Ares, MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                            {
                                System.Diagnostics.Process.Start(page);
                            }
                        }
                        else if (verbose)
                        {
                            MessageBox.Show(parent, Tx("Você já está com a versão mais recente do Apolo (" + current.ToString(3) + ").",
                                "You already have the latest version of Apolo (" + current.ToString(3) + ")."),
                                StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                    catch (Exception)
                    {
                        // an update notice is never worth an error
                    }
                };
                if (parent != null && !parent.IsDisposed && parent.IsHandleCreated)
                    parent.BeginInvoke(show);
            };
            try
            {
                client.DownloadStringAsync(new Uri(LatestReleaseApi));
            }
            catch (Exception)
            {
                client.Dispose();
            }
        }

        private static void DoCheckForUpdate(Form parent, bool mainProgram, bool verbose, bool directDownload)
        {
            if (mainProgram)
            {
                CheckApoloRelease(parent, verbose || directDownload);
                return;
            }
            String url = GetUrlBase() + "ares_version.txt";
            FileDownloader<bool> downloader = new FileDownloader<bool>(parent, verbose, verbose);
            if (mainProgram && directDownload)
                downloader.Download(url, StringResources.SearchingVersion, DownloadMainSetup);
            else if (mainProgram && !directDownload)
                downloader.Download(url, StringResources.SearchingVersion, VersionDownloadedMain);
            else if (directDownload)
                downloader.Download(url, StringResources.SearchingVersion, DownloadPluginSetup);
            else
                downloader.Download(url, StringResources.SearchingVersion, VersionDownloadedPlugin);
        }

		private static bool IsLinux
		{
		    get
		    {
		        int p = (int) Environment.OSVersion.Platform;
		        return (p == 4) || (p == 6) || (p == 128);
		    }
		}

        public static void DownloadLatestSetup(Form parent, bool main)
        {
            DoCheckForUpdate(parent, main, true, true);
        }

        public static void DownloadSetup(Form parent, String version, bool main)
        {
            DoDownloadSetup(parent, version, main, true);
        }

		private static void DoDownloadSetup(Form parent, String version, bool main, bool askForClose) 
        {
            String urlBase = "http://sourceforge.net/projects/aresrpg/files/";
            String urlAppendix = "/download";
            String windowsPart = "-Setup.exe";
            String pluginPart = "-MGPlugin.zip";
            bool isWindows = !IsLinux;
            String url = urlBase + version;
            if (main)
            {
                if (isWindows)
                {
                    url += "/Ares-" + version + windowsPart + urlAppendix;
                }
                else
                {
                    url += "/Ares_Portable_Linux_x" + (Environment.Is64BitOperatingSystem ? "64_" : "86_") + version + ".zip" + urlAppendix;
                }
            }
            else
                url += "/Ares-" + version + pluginPart + urlAppendix;
            try
            {
                System.Diagnostics.Process.Start(url);
                if (main && askForClose && MessageBox.Show(parent, StringResources.CloseAres, StringResources.Ares, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    parent.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(parent, String.Format(StringResources.DownloadError, ex.Message), StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void ChangeLogDownloadedMain(System.Net.DownloadStringCompletedEventArgs result, Form parent, String version)
        {
            ChangeLogDownloaded(result, parent, version, true);
        }

        private static void ChangeLogDownloadedPlugin(System.Net.DownloadStringCompletedEventArgs result, Form parent, String version)
        {
            ChangeLogDownloaded(result, parent, version, false);
        }

        private static void DownloadMainSetup(System.Net.DownloadStringCompletedEventArgs result, Form parent, bool verbose)
        {
            DownloadSetupDirectly(result, parent, verbose, true);
        }

        private static void DownloadPluginSetup(System.Net.DownloadStringCompletedEventArgs result, Form parent, bool verbose)
        {
            DownloadSetupDirectly(result, parent, verbose, false);
        }

        private static void DownloadSetupDirectly(System.Net.DownloadStringCompletedEventArgs result, Form parent, bool verbose, bool main)
        {
            Exception ex = result.Error;
            System.Version newVersion = null;
            if (ex == null)
            {
                String versionString = result.Result + ".0";
                try
                {
                    newVersion = new Version(versionString);
                }
                catch (Exception e)
                {
                    ex = e;
                }
            }
            if (ex != null)
            {
                MessageBox.Show(parent, String.Format(StringResources.DownloadError, result.Error.Message), StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            DoDownloadSetup(parent, result.Result, main, false);
        }

        private static void NewsDownloaded(System.Net.DownloadStringCompletedEventArgs result, Form parent, bool manual)
        {
            if (result.Cancelled)
                return;
            if (result.Error != null)
                return;
            if (String.IsNullOrEmpty(result.Result))
                return;
            bool hasNews = false;
            bool showDialog = false;
            String fileName = Ares.CommonGUI.Preferences.DataFolder;
            fileName = System.IO.Path.Combine(fileName, "ares_news.html");
            if (!manual && System.IO.File.Exists(fileName))
            {
                String fileContent = String.Empty;
                try
                {
                    using (System.IO.StreamReader reader = new System.IO.StreamReader(fileName))
                    {
                        fileContent = reader.ReadToEnd();
                        hasNews = fileContent != result.Result;
                        showDialog = hasNews;
                    }
                }
                catch (System.IO.IOException /*ex*/)
                {
                    hasNews = true;
                    showDialog = true;
                }
            }
            else if (!manual)
            {
                hasNews = true;
                showDialog = false;
            }
            else
            {
                hasNews = true;
                showDialog = true;
            }
            if (hasNews)
            {
                try
                {
                    using (System.IO.StreamWriter writer = new System.IO.StreamWriter(fileName))
                    {
                        writer.Write(result.Result);
                        writer.Flush();
                    }
                }
                catch (System.IO.IOException /*ex*/)
                {
                }
                if (showDialog)
                {
                    NewsDialog dialog = new NewsDialog();
                    dialog.SetNews(result.Result);
                    dialog.ShowDialog(parent);
                }
            }
        }

        private static void ChangeLogDownloaded(System.Net.DownloadStringCompletedEventArgs result, Form parent, String version, bool mainProgram)
        {
            if (result.Cancelled)
                return;
            String text = result.Error != null ? String.Format(StringResources.ChangeLogError, result.Error.Message) : result.Result;
            ChangeLogDialog dialog = new ChangeLogDialog(text);
            DialogResult res = dialog.ShowDialog(parent);
            if (res == DialogResult.OK)
            {
                DownloadSetup(parent, version, mainProgram);
            }
        }

        private static String GetUrlBase()
        {
            return "http://aresrpg.sourceforge.net/";
        }
        
        private static void VersionDownloadedMain(System.Net.DownloadStringCompletedEventArgs result, Form parent, bool verbose)
        {
            VersionDownloaded(result, parent, verbose, true);
        }

        private static void VersionDownloadedPlugin(System.Net.DownloadStringCompletedEventArgs result, Form parent, bool verbose)
        {
            VersionDownloaded(result, parent, verbose, false);
        }
        
        private static void VersionDownloaded(System.Net.DownloadStringCompletedEventArgs result, Form parent, bool verbose, bool mainProgram)
        {
            Exception ex = result.Error;
            System.Version newVersion = null;
            if (ex == null)
            {
                String versionString = result.Result + ".0";
                try
                {
                    newVersion = new Version(versionString);
                }
                catch (Exception e)
                {
                    ex = e;
                }
            }
            if (ex != null)
            {
                MessageBox.Show(parent, String.Format(StringResources.DownloadError, result.Error.Message), StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            Version currentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            if (currentVersion < newVersion && (!verbose || !result.Cancelled))
            {
                NewVersionDialog dialog = new NewVersionDialog();
                DialogResult res = dialog.ShowDialog(parent);
                if (res == DialogResult.Cancel)
                    return;
                if (dialog.ShowChangeLog)
                {
                    String url = GetUrlBase() + StringResources.ChangeLogFile;
                    FileDownloader<String> downloader = new FileDownloader<String>(parent, result.Result, true);
                    if (mainProgram)
                        downloader.Download(url, StringResources.GettingChangeLog, ChangeLogDownloadedMain);
                    else
                        downloader.Download(url, StringResources.GettingChangeLog, ChangeLogDownloadedPlugin);
                }
                else
                {
                    DownloadSetup(parent, result.Result, mainProgram);
                }
            }
            else if (verbose && !result.Cancelled)
            {
                MessageBox.Show(parent, StringResources.NoNewVersion, StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private class FileDownloader<T>
        {
			private System.Windows.Forms.Form mParent;
            private String mUrl;
            private T mParam;
            private String mText;
            private Action<System.Net.DownloadStringCompletedEventArgs, Form, T> mCompletedFct;
            private ProgressMonitor mMonitor;
            private System.Net.WebClient mWebClient;
            private bool mShowDialog;

			public FileDownloader(System.Windows.Forms.Form parent, T t, bool showDialog)
            {
                mParent = parent;
                mParam = t;
                mShowDialog = showDialog;
            }

            public void Download(String url, String text, Action<System.Net.DownloadStringCompletedEventArgs, Form, T> completedFct)
            {
                mUrl = url;
                mText = text;
                mCompletedFct = completedFct;

                mWebClient = new System.Net.WebClient();
                mWebClient.DownloadProgressChanged += new System.Net.DownloadProgressChangedEventHandler(webClient_DownloadProgressChanged);
                mWebClient.DownloadStringCompleted += new System.Net.DownloadStringCompletedEventHandler(webClient_DownloadStringCompleted);
                System.Uri uri;
                bool result = System.Uri.TryCreate(url, UriKind.Absolute, out uri);
                if (!result)
                {
                    MessageBox.Show(mParent, "Internal error: url \"" + url + "\" is invalid.", StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    // use bg thread because first DwnloadStringAsync call is slow due to proxy detection
                    if (mShowDialog)
                    {
                        mMonitor = new ProgressMonitor(mParent, text);
                    }
                    System.Threading.ThreadPool.QueueUserWorkItem(new System.Threading.WaitCallback((object o) =>
                        {
                            try
                            {
                                mWebClient.Proxy = System.Net.WebRequest.GetSystemWebProxy();
                                mWebClient.DownloadStringAsync((Uri)o);
                            }
                            catch (Exception) // On some computers, .NET throws a NullReferenceException in GetSystemWebProxy()
                            {
                                if (mMonitor != null)
                                    mMonitor.Close(new Action(() => { }));
                            }
                        }), uri);
                }
            }

            void webClient_DownloadStringCompleted(object sender, System.Net.DownloadStringCompletedEventArgs e)
            {
                if (mMonitor != null)
                    mMonitor.Close(new Action(() => { mCompletedFct(e, mParent, mParam); }));
				else if (mParent.IsInvokeRequired())
                    mParent.Invoke(new Action(() => { mCompletedFct(e, mParent, mParam); }));
                else
                    mCompletedFct(e, mParent, mParam);
            }

            void webClient_DownloadProgressChanged(object sender, System.Net.DownloadProgressChangedEventArgs e)
            {
                if (mMonitor != null)
                {
                    mMonitor.SetProgress(e.ProgressPercentage);
                    if (mMonitor.Canceled)
                    {
                        mWebClient.CancelAsync();
                    }
                    }
            }

        }

        private class ProgressMonitor
        {
            private System.Timers.Timer timer;
            private bool canceled;
            private bool closed;
            private ProgressDialog dialog;
			private System.Windows.Forms.Form parent;

            private const int DELAY = 700;

            private Object syncObject = new object();

			public ProgressMonitor(System.Windows.Forms.Form parent, String text)
            {
                dialog = new ProgressDialog(text);
                closed = false;
                canceled = false;
                this.parent = parent;
                timer = new System.Timers.Timer(DELAY);
                timer.AutoReset = false;
                timer.Elapsed += new System.Timers.ElapsedEventHandler(timer_Elapsed);
                timer.Start();
            }

            private void timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
            {
                lock (syncObject)
                {
                    if (closed)
                        return;
                }
				if (parent.IsInvokeRequired())
                {
                    parent.Invoke(new Action(() => { OpenDialog(); }));
                }
                else
                {
                    OpenDialog();
                }
            }

            private void OpenDialog()
            {
                lock (syncObject)
                {
                    if (closed)
                        return;
                }
                DialogClosed(dialog.ShowDialog(parent));
            }

            private void DialogClosed(DialogResult result)
            {
                lock (syncObject)
                {
                    if (!closed)
                        canceled = true;
                }
            }

            public bool Canceled
            {
                get
                {
                    lock (syncObject)
                    {
                        return canceled;
                    }
                }
            }

            public void Close(Action action)
            {
                lock (syncObject)
                {
                    closed = true;
                    if (timer.Enabled)
                    {
                        timer.Stop();
                    }
                }
				if (dialog != null && dialog.IsInvokeRequired())
                {
                    dialog.Invoke(new Action(() => { Close(action); }));
                }
                else
                {
                    if (dialog != null && dialog.Visible)
                    {
                        dialog.Visible = false;
                        dialog.Close();
                    }
                    timer.Dispose();
                    action();
                }
            }

            public void SetProgress(int progress)
            {
				if (dialog.IsInvokeRequired())
                {
                    dialog.Invoke(new Action(() => { dialog.SetProgress(progress); }));
                }
                else
                {
                    dialog.SetProgress(progress);
                }
            }
        }

    }
}
