using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using Ares.Data;
using Ares.ModelInfo;
using Xunit;

namespace Apolo.Tests
{
    // A pack (.apkg) made on Windows (paths with "\") is unpacked and read the same way on Linux and macOS.
    [Collection("DataModule")]
    public class PackImportTests : IDisposable
    {
        private readonly string m_Dir = Path.Combine(Path.GetTempPath(), "apolo-pack-" + Guid.NewGuid().ToString("N"));

        private sealed class Quiet : IProgressMonitor
        {
            public bool Canceled { get { return false; } }
            public void IncreaseProgress(double percent) { }
            public void IncreaseProgress(double percent, string text) { }
            public void SetProgress(int percent, string text) { }
            public void SetIndeterminate(string text) { }
        }

        private sealed class Yes : IMessageBoxProvider
        {
            public MessageBoxResult ShowYesNoCancelBox(string prompt) { return MessageBoxResult.Yes; }
        }

        public PackImportTests()
        {
            Directory.CreateDirectory(m_Dir);
        }

        public void Dispose()
        {
            try { Directory.Delete(m_Dir, true); } catch (Exception) { }
        }

        [Fact]
        public void APackWithWindowsPaths_IsUnpackedIntoTheMusicFolder_AndTheProjectFindsTheFiles()
        {
            string music = Path.Combine(m_Dir, "Musicas");
            string sounds = Path.Combine(m_Dir, "Sons");
            Directory.CreateDirectory(music);
            Directory.CreateDirectory(sounds);
            Ares.Settings.Settings.Instance.InitializeWithoutSharedMemory(m_Dir);
            Ares.Settings.Settings.Instance.MusicDirectory = music;
            Ares.Settings.Settings.Instance.SoundDirectory = sounds;

            // the project, as the Windows program writes it: the path of the file with a backslash
            IProject project = DataModule.ProjectManager.CreateProject("Pacote");
            IMode mode = project.AddMode("Pasta");
            var list = DataModule.ElementFactory.CreateRandomBackgroundMusicList("Lista");
            list.AddElement(DataModule.ElementFactory.CreateFileElement("Pacote Teste\\Ambiente\\faixa.mp3", SoundFileType.Music));
            mode.AddElement(DataModule.ElementFactory.CreateModeElement("Lista", list));
            string projectFile = Path.Combine(m_Dir, "origem.apolo");
            DataModule.ProjectManager.SaveProject(project, projectFile);
            DataModule.ProjectManager.UnloadProject(project);

            string pack = Path.Combine(m_Dir, "teste.apkg");
            using (ZipArchive zip = ZipFile.Open(pack, ZipArchiveMode.Create))
            {
                zip.CreateEntryFromFile(projectFile, "origem.apolo");
                ZipArchiveEntry song = zip.CreateEntry("Music/Pacote Teste/Ambiente/faixa.mp3");
                using (Stream s = song.Open())
                    s.Write(new byte[] { 1, 2, 3, 4 }, 0, 4);
                ZipArchiveEntry icons = zip.CreateEntry("Icons/icons.txt");
                using (StreamWriter w = new StreamWriter(icons.Open()))
                    w.Write("Pasta\tLista\tlucide:music\r\n");
            }

            string target = Path.Combine(m_Dir, "aberto.apolo");
            Exception failure = null;
            ManualResetEvent done = new ManualResetEvent(false);
            Importer.Import(new Quiet(), pack, target, true, new Yes(), (error, cancelled) => { failure = error; done.Set(); });
            Assert.True(done.WaitOne(TimeSpan.FromSeconds(30)), "the import did not end");
            Assert.Null(failure);

            Assert.True(File.Exists(target));
            Assert.True(File.Exists(Path.Combine(music, "Pacote Teste", "Ambiente", "faixa.mp3")), "the music was not unpacked in the music folder");
            Assert.True(File.Exists(target + ".icons"));

            IProject loaded = DataModule.ProjectManager.LoadProject(target);
            IFileElement file = loaded.GetModes()[0].GetElements()[0].StartElement is IGeneralElementContainer
                ? ((IGeneralElementContainer)loaded.GetModes()[0].GetElements()[0].StartElement).GetFileElements().First()
                : null;
            Assert.NotNull(file);
            // the file of the project is found in the music folder on every system
            Assert.True(File.Exists(Path.Combine(music, file.FilePath)), "the project points to " + file.FilePath);
            DataModule.ProjectManager.UnloadProject(loaded);
        }
    }
}
