using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ares.AppCore;
using Ares.Data;
using Ares.Players;
using Ares.Player;

namespace Apolo.Desktop
{
    /// <summary>
    /// The engine of the program without any window: settings, sound output, the open project and what plays.
    /// The windows only look at it and ask it to do things.
    /// </summary>
    public sealed class AppHost : IDisposable
    {
        private Ares.Playing.AudioInit m_Audio;
        private readonly Ares.Settings.BasicSettings m_Basic = new Ares.Settings.BasicSettings();

        public PlayingControl Control { get; private set; }
        public IProject Project { get; private set; }
        public string ProjectPath { get; private set; }
        public ModeTree Tree { get; private set; }
        public string Warning { get; private set; }

        public static bool IsPt
        {
            get { return System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt"; }
        }

        public static string T(string pt, string en)
        {
            return IsPt ? pt : en;
        }

        /// <summary>Reads the settings and opens the sound output. noDevice: without a sound card (tests, screenshots).</summary>
        public void Start(bool noDevice)
        {
            bool found = m_Basic.ReadFromFile();
            Ares.Settings.Settings.Instance.Initialize(Ares.Settings.Settings.PlayerID, found ? m_Basic.GetSettingsDir() : null);
            m_Audio = new Ares.Playing.AudioInit(noDevice ? 0 : -1, s => Warning = s);
            Control = new PlayingControl();
            ApplySettings();
            if (!noDevice)
                UseSavedOutput();
            Ares.Playing.AudioLeveler.Enabled = Ares.CommonGUI.Preferences.GetBool("LevelerOn", true);
            Ares.Playing.SoundRepeat.CrossfadeEnabled = Ares.CommonGUI.Preferences.GetBool("SfxLoopCrossfade", true);
            Tree = new ModeTree();
        }

        // sound cards are saved with their name, because their numbers change when devices are plugged in or removed
        private void UseSavedOutput()
        {
            try
            {
                Ares.Settings.Settings s = Ares.Settings.Settings.Instance;
                int index = Ares.Playing.AudioInit.ResolveIndex(s.OutputDeviceIndex, s.OutputDeviceName);
                if (index != -1)
                    m_Audio.SwitchDevice(index);
                s.OutputDeviceIndex = index;
                s.OutputDeviceName = Ares.Playing.AudioInit.NameOf(index);
            }
            catch (Exception)
            {
                Ares.Settings.Settings.Instance.OutputDeviceIndex = -1;
                Ares.Settings.Settings.Instance.OutputDeviceName = string.Empty;
            }
        }

        /// <summary>Uses the folders of the settings and switches to the chosen sound card; null if all went well, else a message.</summary>
        public string ApplySettings(int outputDevice)
        {
            Control.UpdateDirectories();
            string problem = null;
            Ares.Settings.Settings s = Ares.Settings.Settings.Instance;
            if (outputDevice != s.OutputDeviceIndex)
            {
                try
                {
                    m_Audio.SwitchDeviceKeepPlaying(outputDevice);
                    s.OutputDeviceIndex = outputDevice;
                    s.OutputDeviceName = Ares.Playing.AudioInit.NameOf(outputDevice);
                }
                catch (Ares.Playing.AudioInitException ex)
                {
                    problem = ex.Message;
                }
            }
            SaveSettings();
            return problem;
        }

        private void ApplySettings()
        {
            Ares.Settings.Settings s = Ares.Settings.Settings.Instance;
            Control.UpdateDirectories();
            Control.SetPlayMusicOnAllSpeakers(s.PlayMusicOnAllSpeakers);
            Control.SetFadingOnPreviousNext(s.ButtonMusicFadeMode != 0, s.ButtonMusicFadeMode == 2, s.ButtonMusicFadeTime);
            Control.SetMusicTagFading(s.TagMusicFadeTime, s.TagMusicFadeOnlyOnChange);
            Control.GlobalVolume = s.GlobalVolume;
            Control.MusicVolume = s.MusicVolume;
            Control.SoundVolume = s.SoundVolume;
        }

        public IList<Ares.Settings.RecentFiles.ProjectEntry> Recent
        {
            get { return Ares.Settings.Settings.Instance.RecentFiles.GetFiles(); }
        }

        public void StopAll()
        {
            Ares.Playing.PlayingModule.ProjectPlayer.StopAll();
        }

        /// <summary>Opens a project file (.apolo or .ares). Throws with a message if it cannot be read.</summary>
        public void Open(string path)
        {
            StopAll();
            IProject loaded = DataModule.ProjectManager.LoadProject(path);
            if (Project != null)
                DataModule.ProjectManager.UnloadProject(Project);
            Project = loaded;
            ProjectPath = path;
            Tree = ModeTree.Load(path);
            Tree.Sync(Project.GetModes().Select(m => m.Title).ToList());
            Ares.Playing.PlayingModule.ProjectPlayer.SetProject(Project);
            Ares.Settings.Settings.Instance.RecentFiles.AddFile(new Ares.Settings.RecentFiles.ProjectEntry(path, Project.Title));
            Ares.CommonGUI.Preferences.Set("LastProject", path);
            SaveSettings();
        }

        public void SaveSettings()
        {
            try
            {
                Ares.Settings.Settings s = Ares.Settings.Settings.Instance;
                s.GlobalVolume = Control.GlobalVolume;
                s.MusicVolume = Control.MusicVolume;
                s.SoundVolume = Control.SoundVolume;
                s.WriteToFile(m_Basic.GetSettingsDir());
                m_Basic.WriteToFile();
            }
            catch (Exception)
            {
            }
        }

        /// <summary>The project file made by the last import of a pack.</summary>
        public string ImportedProject { get; private set; }

        private sealed class QuietMonitor : Ares.ModelInfo.IProgressMonitor
        {
            public bool Canceled { get { return false; } }
            public void IncreaseProgress(double percent) { }
            public void IncreaseProgress(double percent, string text) { }
            public void SetProgress(int percent, string text) { }
            public void SetIndeterminate(string text) { }
        }

        private sealed class YesBoxes : Ares.ModelInfo.IMessageBoxProvider
        {
            public Ares.ModelInfo.MessageBoxResult ShowYesNoCancelBox(string prompt) { return Ares.ModelInfo.MessageBoxResult.Yes; }
        }

        /// <summary>Unpacks a pack (.apkg) into Documents/Apolo/Pacotes; "done" is called when it ends (error = null if all went well).</summary>
        public void Import(string packFile, Action<Exception> done)
        {
            string folder = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Apolo"), "Pacotes");
            Directory.CreateDirectory(folder);
            string name = Path.GetFileNameWithoutExtension(packFile);
            string target = Path.Combine(folder, name + ".apolo");
            for (int n = 2; File.Exists(target); n++)
                target = Path.Combine(folder, name + " " + n + ".apolo");
            StopAll();
            Ares.ModelInfo.Importer.Import(new QuietMonitor(), packFile, target, true, new YesBoxes(), (error, cancelled) =>
            {
                ImportedProject = error == null && !cancelled && File.Exists(target) ? target : null;
                done(error);
            });
        }

        public IMode ModeByTitle(string title)
        {
            return Project == null ? null : Project.GetModes().FirstOrDefault(m => m.Title == title);
        }

        public void Dispose()
        {
            try { StopAll(); } catch (Exception) { }
            SaveSettings();
            if (Control != null)
                Control.Dispose();
            if (m_Audio != null)
                m_Audio.Dispose();
        }
    }
}
