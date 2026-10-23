using System;
using System.IO;
using System.Linq;
using System.Threading;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Xunit;

namespace Apolo.Desktop.Tests
{
    public class PacksTests
    {
        // The list of packs is on a release of the repository. Without internet the test has nothing to check.
        [AvaloniaFact]
        public void TheListOfPacks_IsFound_OnTheInternet()
        {
            AppHost host = new AppHost();
            host.Start(true);
            PacksWindow window = new PacksWindow(host);
            window.Show();
            DateTime limit = DateTime.UtcNow.AddSeconds(15);
            while (DateTime.UtcNow < limit)
            {
                Dispatcher.UIThread.RunJobs();
                Thread.Sleep(100);
                if (window.Packs.Count > 0 || window.Failed)
                    break;
            }
            string dir = Environment.GetEnvironmentVariable("APOLO_SHOT_DIR");
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
                window.CaptureRenderedFrame().Save(Path.Combine(dir, "pacotes.png"));
            }
            if (!window.Failed)
                Assert.NotEmpty(window.Packs);
            window.Close();
            host.Dispose();
        }

        // the whole way of a published pack: download, check, unpack, open (big download: only when asked for)
        [AvaloniaFact]
        public void APublishedPack_IsDownloadedAndOpened()
        {
            if (Environment.GetEnvironmentVariable("APOLO_TEST_DOWNLOAD") != "1")
                return;
            AppHost host = new AppHost();
            host.Start(true);
            PacksWindow window = new PacksWindow(host);
            window.Show();
            DateTime limit = DateTime.UtcNow.AddSeconds(20);
            while (DateTime.UtcNow < limit && window.Packs.Count == 0)
            {
                Dispatcher.UIThread.RunJobs();
                Thread.Sleep(100);
            }
            Assert.NotEmpty(window.Packs);
            window.StartDownloadForTest(0);
            limit = DateTime.UtcNow.AddMinutes(10);
            while (DateTime.UtcNow < limit && window.ImportedProject == null && window.IsVisible)
            {
                Dispatcher.UIThread.RunJobs();
                Thread.Sleep(200);
            }
            Assert.True(window.ImportedProject != null, "status: " + window.StatusText);
            host.Open(window.ImportedProject);
            Assert.True(host.Project.GetModes().Count > 0);
            int files = host.Project.GetModes().SelectMany(m => m.GetElements())
                .Select(e => e.StartElement as Ares.Data.IGeneralElementContainer).Where(c => c != null).Sum(c => c.GetFileElements().Count);
            Assert.Equal(68, files);
            // every music file of the project is where the project looks for it
            string music = Ares.Settings.Settings.Instance.MusicDirectory;
            foreach (Ares.Data.IFileElement f in host.Project.GetModes().SelectMany(m => m.GetElements())
                .Select(e => e.StartElement as Ares.Data.IGeneralElementContainer).Where(c => c != null).SelectMany(c => c.GetFileElements()))
                Assert.True(File.Exists(Path.Combine(music, f.FilePath)), f.FilePath);
            host.Dispose();
        }
    }
}
