using System;
using System.IO;
using System.Linq;
using Ares.CommonGUI;
using Ares.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Apolo.Desktop.Tests
{
    public class ImportTests : IDisposable
    {
        private readonly string m_Dir = Path.Combine(Path.GetTempPath(), "apolo-import-" + Guid.NewGuid().ToString("N"));

        public ImportTests()
        {
            Directory.CreateDirectory(m_Dir);
        }

        public void Dispose()
        {
            try { Directory.Delete(m_Dir, true); } catch (Exception) { }
        }

        private string Touch(string relative)
        {
            string path = Path.Combine(m_Dir, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
            return path;
        }

        [AvaloniaFact]
        public void ADroppedFolderWithSubfolders_BecomesFoldersAndPlaylists_AndIsSaved()
        {
            Touch("musicas/Floresta/vento.mp3");
            Touch("musicas/Floresta/Dia/passaros.mp3");
            Touch("musicas/Floresta/Dia/riacho.ogg");
            Touch("musicas/Floresta/Noite/coruja.wav");

            AppHost host = new AppHost();
            host.Start(true);
            host.CreateProject(Path.Combine(m_Dir, "novo.apolo"), "Novo");
            DropScan scan = DropScan.Read(new[] { Path.Combine(m_Dir, "musicas", "Floresta") });
            DropOptions options = new DropOptions { Layout = scan.DefaultLayout(false), OneElement = true, Kind = PlaylistKind.Random, Repeat = true };

            AppHost.ImportSummary summary = host.ImportDropped(scan, options, null);

            Assert.True(summary.NewFolders >= 1);
            Assert.True(summary.Buttons >= 3);
            Assert.NotNull(host.ModeByTitle("Floresta"));
            // the nesting of the folders is kept beside the project
            Assert.True(File.Exists(Path.Combine(m_Dir, "novo.apolo.pastas.json")));

            // everything is in the file: it is read again
            IProject again = DataModule.ProjectManager.LoadProject(Path.Combine(m_Dir, "novo.apolo"));
            Assert.Contains(again.GetModes(), m => m.Title == "Floresta");
            int files = again.GetModes().SelectMany(m => m.GetElements())
                .Select(e => e.StartElement as IGeneralElementContainer).Where(c => c != null).Sum(c => c.GetFileElements().Count);
            Assert.Equal(4, files);
            DataModule.ProjectManager.UnloadProject(again);
            host.Dispose();
        }

        [AvaloniaFact]
        public void TheQuestionWindow_ShowsWhatWasFound()
        {
            Touch("a/b/um.mp3");
            Touch("a/b/dois.mp3");
            DropScan scan = DropScan.Read(new[] { Path.Combine(m_Dir, "a") });
            DropOptionsWindow window = new DropOptionsWindow(scan, null);
            window.Show();
            string dir = Environment.GetEnvironmentVariable("APOLO_SHOT_DIR");
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
                window.CaptureRenderedFrame().Save(Path.Combine(dir, "adicionar.png"));
            }
            Assert.Null(window.Result);
            window.Close();
        }
    }
}
