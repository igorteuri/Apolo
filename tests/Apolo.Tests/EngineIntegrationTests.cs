using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Ares.Data;
using Ares.Players;
using Xunit;

namespace Apolo.Tests
{
    // The whole chain on the real engine: project -> player -> mixer -> sound output. On Linux and macOS the output is
    // OpenAL Soft with its "null" driver, on Windows the engine runs without a sound card (nothing is heard in either case).
    [Collection("DataModule")]
    public class EngineIntegrationTests : IDisposable
    {
        [DllImport("libc", EntryPoint = "setenv")]
        private static extern int SetEnv(string name, string value, int overwrite);

        private readonly string m_Dir = Path.Combine(Path.GetTempPath(), "apolo-engine-" + Guid.NewGuid().ToString("N"));

        public EngineIntegrationTests()
        {
            Directory.CreateDirectory(m_Dir);
            Environment.SetEnvironmentVariable("ALSOFT_DRIVERS", "null");
            if (!OperatingSystem.IsWindows())
                SetEnv("ALSOFT_DRIVERS", "null", 1);
        }

        public void Dispose()
        {
            try { Directory.Delete(m_Dir, true); } catch (Exception) { }
        }

        // a sine wave of 0.8 seconds as a WAV file
        private string MakeWave(string name)
        {
            string path = Path.Combine(m_Dir, name);
            int rate = 44100, frames = (int)(rate * 0.8);
            using (BinaryWriter w = new BinaryWriter(File.Create(path)))
            {
                w.Write(new[] { 'R', 'I', 'F', 'F' });
                w.Write(36 + frames * 2);
                w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
                w.Write(new[] { 'd', 'a', 't', 'a' });
                w.Write(frames * 2);
                for (int i = 0; i < frames; i++)
                    w.Write((short)(Math.Sin(i * 2 * Math.PI * 440 / rate) * 9000));
            }
            return path;
        }

        [Fact]
        public void AButtonWithASoundEffect_PlaysAndEnds_OnTheRealEngine()
        {
            Ares.Settings.Settings.Instance.InitializeWithoutSharedMemory(m_Dir);
            string wave = MakeWave("beep.wav");

            IProject created = DataModule.ProjectManager.CreateProject("Teste");
            IChoiceElementContainerHelper.Add(created.AddMode("Pasta"), wave);
            string projectFile = Path.Combine(m_Dir, "teste.apolo");
            DataModule.ProjectManager.SaveProject(created, projectFile);
            DataModule.ProjectManager.UnloadProject(created);
            // as the program does: the project is read from its file
            IProject project = DataModule.ProjectManager.LoadProject(projectFile);
            IMode mode = project.GetModes()[0];

            using (Ares.Playing.AudioInit init = new Ares.Playing.AudioInit(OperatingSystem.IsWindows() ? 0 : -1, s => { }))
            using (PlayingControl control = new PlayingControl())
            {
                control.UpdateDirectories();
                Ares.Playing.PlayingModule.ProjectPlayer.SetProject(project);
                control.SetMode(mode);
                IModeElement button = mode.GetElements().First();
                Assert.NotNull(DataModule.ElementRepository.GetElement(button.Id));
                Assert.IsAssignableFrom<IModeElement>(DataModule.ElementRepository.GetElement(button.Id));
                Assert.True(control.SwitchElement(button.Id));

                bool started = false;
                DateTime limit = DateTime.UtcNow.AddSeconds(5);
                while (DateTime.UtcNow < limit && !started)
                {
                    started = control.GetRunningSounds().Count > 0;
                    Thread.Sleep(20);
                }
                Assert.True(started, "the sound did not start");

                bool ended = false;
                limit = DateTime.UtcNow.AddSeconds(6);
                while (DateTime.UtcNow < limit && !ended)
                {
                    ended = control.GetRunningSounds().Count == 0;
                    Thread.Sleep(50);
                }
                Assert.True(ended, "the sound did not end");
            }
            DataModule.ProjectManager.UnloadProject(project);
        }

        [Fact]
        public void RepeatMode_CyclesAndRepeatsAMusicWhichWasNotSetToRepeat()
        {
            Ares.Settings.Settings.Instance.InitializeWithoutSharedMemory(m_Dir);
            string wave = MakeWave("musica.wav");

            IProject created = DataModule.ProjectManager.CreateProject("Teste");
            IFileElement music = DataModule.ElementFactory.CreateFileElement(wave, SoundFileType.Music);
            created.AddMode("Pasta").AddElement(DataModule.ElementFactory.CreateModeElement("Musica", music));
            string projectFile = Path.Combine(m_Dir, "repetir.apolo");
            DataModule.ProjectManager.SaveProject(created, projectFile);
            DataModule.ProjectManager.UnloadProject(created);
            IProject project = DataModule.ProjectManager.LoadProject(projectFile);
            IMode mode = project.GetModes()[0];

            using (Ares.Playing.AudioInit init = new Ares.Playing.AudioInit(OperatingSystem.IsWindows() ? 0 : -1, s => { }))
            using (PlayingControl control = new PlayingControl())
            {
                control.UpdateDirectories();
                Ares.Playing.PlayingModule.ProjectPlayer.SetProject(project);
                control.SetMode(mode);

                // off -> playlist -> track -> off
                Assert.Equal(0, control.MusicRepeatMode);
                control.CycleMusicRepeatMode();
                Assert.Equal(1, control.MusicRepeatMode);
                control.CycleMusicRepeatMode();
                Assert.Equal(2, control.MusicRepeatMode);
                control.CycleMusicRepeatMode();
                Assert.Equal(0, control.MusicRepeatMode);

                // the music is 0.8 s long and has no repeat of its own: with 'repeat playlist' it is still playing after 2 s
                control.SetMusicRepeatMode(1);
                Assert.True(control.SwitchElement(mode.GetElements().First().Id));
                Thread.Sleep(2000);
                Assert.True(control.CurrentMusicElement != -1, "the music did not repeat");

                // turned off, it ends at the end of the current round
                control.SetMusicRepeatMode(0);
                bool ended = false;
                DateTime limit = DateTime.UtcNow.AddSeconds(4);
                while (DateTime.UtcNow < limit && !ended)
                {
                    ended = control.CurrentMusicElement == -1;
                    Thread.Sleep(50);
                }
                Assert.True(ended, "the music did not end after the repeat was turned off");
            }
            DataModule.ProjectManager.UnloadProject(project);
        }

        private static class IChoiceElementContainerHelper
        {
            public static void Add(IMode mode, string file)
            {
                var choice = DataModule.ElementFactory.CreateChoiceContainer("Bip");
                choice.AddElement(DataModule.ElementFactory.CreateFileElement(file, SoundFileType.SoundEffect));
                mode.AddElement(DataModule.ElementFactory.CreateModeElement("Bip", choice));
            }
        }
    }
}
