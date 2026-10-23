/*
 Copyright (c) 2015 [Joerg Ruedenauer]
 
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
using System.Collections.Generic;
using Ares.Playing.Audio;

namespace Ares.Playing
{
    // Plays the files of the elements on the audio engine of Apolo (see the Audio folder).
    class FilePlayer : IFilePlayer
    {
        private static AudioChannel GetChannel(int handle)
        {
            AudioEngine engine = AudioEngine.Instance;
            return engine != null ? engine.GetChannel(handle) : null;
        }

        /// <summary>Set by the element player while it starts a sound which will be played again afterwards (a repetition by new plays).</summary>
        [ThreadStatic]
        internal static bool s_RepeatIntent;

        public int PlayFile(ISoundFile file, int fadeInTime, PlayingFinished callback, bool loop)
        {
            AudioEngine engine = AudioEngine.Instance;
            if (engine == null)
            {
                AudioErrors.Last = AudioError.Device;
                ErrorHandling.AudioErrorOccurred(file.Id, StringResources.FilePlayingError);
                return 0;
            }
            IAudioSource source = file.SoundFileType == SoundFileType.WebRadio
                ? AudioSources.OpenRadio(file.Path)
                : AudioSources.OpenFile(file.Path);
            if (source == null)
            {
                ErrorHandling.AudioErrorOccurred(file.Id, StringResources.FilePlayingError);
                return 0;
            }
            Data.IEffects effects = file.Effects;
            AudioChannel channel = new AudioChannel(engine, source, false);
            int handle = channel.Handle;
            channel.Route = DetermineRoute(file, engine.Speakers);
            RunningFileInfo info = new RunningFileInfo();
            info.File = file;
            lock (m_Mutex)
            {
                info.EndAction = new Action(() =>
                {
                    callback(file.Id, handle);
                });
                info.Volume = file.Volume;
            }
            channel.Loop = loop;
            bool cueOutActive = effects != null && effects.CueOut.Active;
            bool cueInActive = effects != null && effects.CueIn.Active;
            if (cueOutActive)
                channel.CueOut = effects.CueOut.Position;
            if (cueInActive)
                channel.LoopStart = effects.CueIn.Position;
            double length = channel.Length;
            // a repeated sound effect is blended with itself: a loop inside the channel, a repetition by new plays with a crossfade
            int selfMs = 0;
            if (SoundRepeat.CrossfadeEnabled && file.SoundFileType == SoundFileType.SoundEffect && length > 0)
                selfMs = (int)Math.Min(SoundRepeat.CrossfadeMs, length * 1000 / 3);
            if (selfMs < 100)
                selfMs = 0;
            int selfFade = !loop && s_RepeatIntent && (effects == null || effects.FadeOutTime == 0) ? selfMs : 0;
            if (loop && selfMs > 0 && (effects == null || effects.FadeOutTime == 0))
                channel.LoopCrossfadeMs = selfMs;
            else if (selfMs > 0 && !loop)
                channel.LoopCrossfadeMs = selfMs; // the repeat button may be switched on later
            SetStartVolume(file, Math.Max(fadeInTime, selfFade), channel, info);
            info.CrossFade = false;
            int fadeOutTime = effects != null ? effects.FadeOutTime : 0;
            bool crossFading = effects != null && effects.CrossFading;
            if (selfFade > 0)
            {
                fadeOutTime = selfFade;
                crossFading = true;
            }
            int settingTime;
            if (!loop && file.SoundFileType == SoundFileType.Music && PlayingModule.ThePlayer.GetMusicCrossFade(out settingTime))
            {
                // the crossfade of the music tab also applies when a song ends by itself
                crossFading = true;
                if (fadeOutTime == 0)
                    fadeOutTime = settingTime;
            }
            if (fadeOutTime != 0 && length > 0)
            {
                double fadeOutSeconds = Math.Min(0.001 * fadeOutTime, length);
                double end = cueOutActive ? effects.CueOut.Position : length;
                channel.FadeAt = Math.Max(0, end - fadeOutSeconds);
                channel.FadeOutMs = (int)(fadeOutSeconds * 1000);
                if (!loop)
                {
                    info.CrossFade = crossFading;
                }
            }
            if (effects != null && (effects.Pitch.Active || effects.Tempo.Active))
            {
                float pitchValue = effects.Pitch.Active ? DetermineIntEffectValue(effects.Pitch) : 0f;
                float tempoValue = effects.Tempo.Active ? DetermineIntEffectValue(effects.Tempo) : 0f;
                channel.SetTempoPitch(tempoValue, pitchValue);
            }
            if (effects != null && effects.Balance.Active)
            {
                SetBalanceEffect(channel, file.Id, effects.Balance);
            }
            if (effects != null && effects.VolumeDB.Active)
            {
                float volumeDB = DetermineIntEffectValue(effects.VolumeDB);
                channel.Gain = (float)Math.Pow(10d, volumeDB / 20);
            }
            if (effects != null && effects.Reverb.Active)
            {
                float linearLevel = (float)Math.Pow(10d, effects.Reverb.Level / 20.0);
                channel.SetReverb(linearLevel, effects.Reverb.Delay);
            }
            if (cueInActive)
            {
                channel.Seek(effects.CueIn.Position);
            }
            channel.Ended = () => FileFinished(handle, true);
            channel.Looped = () => OnLooped(channel, file, info);
            channel.FadeStarted = () => OnFadeStart(channel, file, info);

            lock (m_Mutex)
            {
                m_RunningFiles[handle] = info;
            }
            if (!channel.Start())
            {
                lock (m_Mutex)
                {
                    m_RunningFiles.Remove(handle);
                }
                channel.Stop();
                ErrorHandling.AudioErrorOccurred(file.Id, StringResources.FilePlayingError);
                return 0;
            }
            return handle;
        }

        /// <summary>
        /// Gets the current play position and total length (in seconds) of a running file.
        /// Returns false if the file isn't running or has no known length (e.g. web radio).
        /// </summary>
        public bool GetPlayPosition(int handle, out double positionSeconds, out double lengthSeconds)
        {
            positionSeconds = 0;
            lengthSeconds = 0;
            lock (m_Mutex)
            {
                if (!m_RunningFiles.ContainsKey(handle))
                    return false;
            }
            AudioChannel channel = GetChannel(handle);
            if (channel == null)
                return false;
            double length = channel.Length;
            if (length <= 0)
                return false;
            lengthSeconds = length;
            positionSeconds = Math.Max(0, Math.Min(channel.Position, length));
            return true;
        }

        /// <summary>
        /// Registers what has to be done to completely stop the element which plays a file
        /// (not only the file itself, but also repetitions).
        /// </summary>
        public void SetStopAction(int handle, Action stopAction)
        {
            lock (m_Mutex)
            {
                if (m_RunningFiles.ContainsKey(handle))
                    m_RunningFiles[handle].StopAction = stopAction;
            }
        }

        /// <summary>
        /// Registers how the user interface can ask for / change the repeat state of a running sound.
        /// </summary>
        public void SetRepeatCallbacks(int handle, Func<bool> query, Action<bool> change)
        {
            lock (m_Mutex)
            {
                if (m_RunningFiles.ContainsKey(handle))
                {
                    m_RunningFiles[handle].RepeatQuery = query;
                    m_RunningFiles[handle].RepeatChange = change;
                }
            }
        }

        public bool SetSoundRepeat(int handle, bool repeat)
        {
            Action<bool> change = null;
            lock (m_Mutex)
            {
                if (!m_RunningFiles.ContainsKey(handle))
                    return false;
                change = m_RunningFiles[handle].RepeatChange;
            }
            if (change != null)
                change(repeat);
            SetRepeatFile(handle, repeat);
            return true;
        }

        public List<PlayingFileInfo> GetRunningSounds()
        {
            return GetRunningSounds(0);
        }

        /// <summary>The running sound effects; the handle of the current music is left out.</summary>
        public List<PlayingFileInfo> GetRunningSounds(int excludeHandle)
        {
            return GetRunning(excludeHandle, false);
        }

        /// <summary>All running files, music included (the Editor shows them in its playback panel).</summary>
        public List<PlayingFileInfo> GetRunningFiles()
        {
            return GetRunning(0, true);
        }

        private List<PlayingFileInfo> GetRunning(int excludeHandle, bool includeMusic)
        {
            List<KeyValuePair<int, ISoundFile>> running = new List<KeyValuePair<int, ISoundFile>>();
            lock (m_Mutex)
            {
                foreach (KeyValuePair<int, RunningFileInfo> pair in m_RunningFiles)
                {
                    if (pair.Key != excludeHandle && pair.Value.File != null && (includeMusic || pair.Value.File.SoundFileType == SoundFileType.SoundEffect))
                        running.Add(new KeyValuePair<int, ISoundFile>(pair.Key, pair.Value.File));
                }
            }
            running.Sort((a, b) => a.Key.CompareTo(b.Key));
            List<PlayingFileInfo> result = new List<PlayingFileInfo>();
            foreach (KeyValuePair<int, ISoundFile> item in running)
            {
                double pos, length;
                if (!GetPlayPosition(item.Key, out pos, out length))
                {
                    pos = 0;
                    length = 0;
                }
                string title = item.Value.Path;
                try
                {
                    title = System.IO.Path.GetFileNameWithoutExtension(item.Value.Path);
                }
                catch (ArgumentException)
                {
                }
                bool repeat = false;
                Func<bool> query = null;
                lock (m_Mutex)
                {
                    if (m_RunningFiles.ContainsKey(item.Key))
                        query = m_RunningFiles[item.Key].RepeatQuery;
                }
                if (query != null)
                    repeat = query();
                AudioChannel soundChannel = GetChannel(item.Key);
                bool paused = soundChannel != null && soundChannel.State == ChannelState.Paused;
                result.Add(new PlayingFileInfo { Handle = item.Key, FileId = item.Value.Id, Title = title, Position = pos, Length = length, Repeat = repeat, Paused = paused, IsMusic = item.Value.SoundFileType == SoundFileType.Music });
            }
            return result;
        }

        public bool StopSound(int handle)
        {
            Action action = null;
            lock (m_Mutex)
            {
                if (!m_RunningFiles.ContainsKey(handle))
                    return false;
                action = m_RunningFiles[handle].StopAction;
            }
            if (action != null)
                action();
            else
                StopFile(handle);
            return true;
        }

        /// <summary>Pauses or resumes one single sound.</summary>
        /// <returns>whether the sound is paused afterwards</returns>
        public bool ToggleSoundPause(int handle)
        {
            AudioChannel channel = GetChannel(handle);
            if (channel == null)
                return false;
            if (channel.State == ChannelState.Paused)
            {
                channel.Resume();
                return false;
            }
            return channel.Pause();
        }

        // only the pause of the main button counts for the main button; a sound paused on its own does not
        private bool m_GlobalPause;

        public bool IsAnyPaused()
        {
            if (!m_GlobalPause)
                return false;
            foreach (int handle in GetRunningHandles())
            {
                AudioChannel channel = GetChannel(handle);
                if (channel != null && channel.State == ChannelState.Paused)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Pauses all playing files, or resumes all paused files.
        /// </summary>
        /// <returns>whether files are paused afterwards</returns>
        public bool TogglePause()
        {
            List<AudioChannel> channels = new List<AudioChannel>();
            foreach (int handle in GetRunningHandles())
            {
                AudioChannel channel = GetChannel(handle);
                if (channel != null)
                    channels.Add(channel);
            }
            bool anyPaused = false;
            foreach (AudioChannel channel in channels)
            {
                if (channel.State == ChannelState.Paused)
                {
                    anyPaused = true;
                    break;
                }
            }
            if (anyPaused && m_GlobalPause)
            {
                foreach (AudioChannel channel in channels)
                {
                    channel.Resume();
                }
                m_GlobalPause = false;
                return false;
            }
            bool pausedSomething = false;
            foreach (AudioChannel channel in channels)
            {
                if (channel.State == ChannelState.Playing && channel.Pause())
                    pausedSomething = true;
            }
            m_GlobalPause = pausedSomething;
            return pausedSomething;
        }

        private List<int> GetRunningHandles()
        {
            lock (m_Mutex)
            {
                return new List<int>(m_RunningFiles.Keys);
            }
        }

        /// <summary>
        /// Jumps to a position (in seconds) in a running file.
        /// </summary>
        public bool SetPlayPosition(int handle, double positionSeconds)
        {
            lock (m_Mutex)
            {
                if (!m_RunningFiles.ContainsKey(handle))
                    return false;
            }
            AudioChannel channel = GetChannel(handle);
            if (channel == null)
                return false;
            double lengthSec = channel.Length;
            if (lengthSec <= 0)
                return false;
            // stay slightly before the end so that the file doesn't end immediately in an odd state
            double target = Math.Max(0, Math.Min(positionSeconds, Math.Max(0, lengthSec - 0.2)));
            return channel.Seek(target);
        }

        public void StopFile(int handle)
        {
            StopFile(handle, false, 0, false);
        }

        public void StopFile(int handle, bool fadeOut, int fadeOutTime, bool isCrossFade)
        {
            AudioChannel channel = GetChannel(handle);
            if (channel == null)
            {
                FileFinished(handle, true);
                return;
            }
            if (!fadeOut || fadeOutTime == 0)
            {
                channel.Stop();
                FileFinished(handle, true);
            }
            else
            {
                // the channel stops itself (and tells us) when it is silent
                channel.FadeOutAndStop(fadeOutTime);
                if (isCrossFade)
                {
                    // call finished immediately to start the next file
                    FileFinished(handle, false);
                }
            }
        }

        private static float DetermineVolume(Data.IEffects effects, float baseVolume, out float specificVolume)
        {
            if (!effects.HasRandomVolume)
            {
                specificVolume = effects.Volume / 100.0f;
            }
            else
            {
                specificVolume = (float)(0.01 * ((PlayingModule.Randomizer.NextDouble() * (effects.MaxRandomVolume - effects.MinRandomVolume) + effects.MinRandomVolume)));
            }
            return baseVolume * specificVolume;
        }

        private static float DetermineIntEffectValue(Data.IIntEffect effect)
        {
            if (!effect.Random)
            {
                return effect.FixValue;
            }
            else
            {
                return (float)(PlayingModule.Randomizer.NextDouble() * (effect.MaxRandomValue - effect.MinRandomValue) + effect.MinRandomValue);
            }
        }

        // Speakers: 0 front left, 1 front right, 2 centre, 3 subwoofer, 4 / 5 rear left / right, 6 / 7 centre rears (side)
        private static SpeakerRoute DetermineRoute(ISoundFile file, int speakers)
        {
            if (file.Effects == null || !file.Effects.SpeakerAssignment.Active)
                return SpeakerRoute.Fronts;
            Data.SpeakerAssignment assignment = file.Effects.SpeakerAssignment.Assignment;
            while (true)
            {
                if (file.Effects.SpeakerAssignment.Random)
                {
                    assignment = (Data.SpeakerAssignment)(PlayingModule.Randomizer.Next(1, speakers + 1));
                }
                int[] targets = null;
                bool mono = false;
                int neededNrOfSpeakers = 2;
                switch (assignment)
                {
                    case Data.SpeakerAssignment.LeftFront:
                        targets = new[] { 0 }; mono = true;
                        break;
                    case Data.SpeakerAssignment.RightFront:
                        targets = new[] { 1 }; mono = true;
                        break;
                    case Data.SpeakerAssignment.LeftBack:
                        targets = new[] { 4 }; mono = true;
                        neededNrOfSpeakers = 5;
                        break;
                    case Data.SpeakerAssignment.RightBack:
                        targets = new[] { 5 }; mono = true;
                        neededNrOfSpeakers = 5;
                        break;
                    case Data.SpeakerAssignment.Center:
                        targets = new[] { 2 }; mono = true;
                        neededNrOfSpeakers = 5;
                        break;
                    case Data.SpeakerAssignment.LeftCenterBack:
                        targets = new[] { 6 }; mono = true;
                        neededNrOfSpeakers = 7;
                        break;
                    case Data.SpeakerAssignment.RightCenterBack:
                        targets = new[] { 7 }; mono = true;
                        neededNrOfSpeakers = 7;
                        break;
                    case Data.SpeakerAssignment.Subwoofer:
                        targets = new[] { 3 }; mono = true;
                        neededNrOfSpeakers = 5;
                        break;
                    case Data.SpeakerAssignment.BothFronts:
                        targets = new[] { 0, 1 };
                        break;
                    case Data.SpeakerAssignment.BothRears:
                        targets = new[] { 4, 5 };
                        neededNrOfSpeakers = 5;
                        break;
                    case Data.SpeakerAssignment.BothCenterRears:
                        targets = new[] { 6, 7 };
                        neededNrOfSpeakers = 7;
                        break;
                    case Data.SpeakerAssignment.CenterAndSubwoofer:
                        targets = new[] { 2, 3 };
                        neededNrOfSpeakers = 5;
                        break;
                    case Data.SpeakerAssignment.AllSpeakers:
                        if (speakers > 6)
                            targets = new[] { 0, 1, 4, 5, 2, 3, 6, 7 };
                        else if (speakers > 2)
                            targets = new[] { 0, 1, 4, 5, 2, 3 };
                        else
                            targets = new[] { 0, 1 };
                        break;
                    default:
                        break;
                }
                if (targets == null)
                    return SpeakerRoute.Fronts;
                if (neededNrOfSpeakers > speakers)
                {
                    if (file.Effects.SpeakerAssignment.Random)
                    {
                        // just choose another speaker
                        continue;
                    }
                    else
                    {
                        ErrorHandling.ErrorOccurred(file.Id, StringResources.SpeakerNotAvailable);
                        return SpeakerRoute.Fronts;
                    }
                }
                return new SpeakerRoute { Targets = targets, Mono = mono };
            }
        }

        private bool SetBalanceEffect(AudioChannel channel, int id, Data.IBalanceEffect effect)
        {
            if (!effect.IsPanning)
            {
                float balanceValue = DetermineIntEffectValue(effect);
                // balance is stored as [-10..10], but must be set as [-1..1]
                channel.SetPan(balanceValue / 10.0f);
                return true;
            }
            else
            {
                // first set to start value
                // balance is stored as [-10..10], but must be set as [-1..1]
                channel.SetPan(effect.PanningStart / 10.0f);
                // now slide over all time to end value
                double totalLengthSeconds = channel.Length;
                if (totalLengthSeconds > 0)
                {
                    channel.SlidePan(effect.PanningEnd / 10.0f, totalLengthSeconds);
                }
                return true;
            }
        }

        public void SetVolume(int handle, int volume)
        {
            if (volume < 0 || volume > 100)
                throw new ArgumentException("Invalid volume!");
            AudioChannel channel = GetChannel(handle);
            lock (m_Mutex)
            {
                var info = m_RunningFiles.ContainsKey(handle) ? m_RunningFiles[handle] : null;
                float specificVolume = info != null ? info.Volume : 0.01f;
                specificVolume *= volume * 0.01f;
                if (channel != null)
                    channel.SetVolume(specificVolume);
            }
        }

        public void SetRepeatFile(int handle, bool repeat)
        {
            AudioChannel channel = GetChannel(handle);
            if (channel != null)
                channel.Loop = repeat;
        }

        private bool SetStartVolume(ISoundFile file, int fadeInTime, AudioChannel channel, RunningFileInfo info)
        {
            float volume = file.Volume / 100.0f;
            float specificVolume = 1.0f;
            if (file.Effects != null)
            {
                volume = DetermineVolume(file.Effects, volume, out specificVolume);
            }
            info.Volume = specificVolume;
            if ((file.Effects != null && file.Effects.FadeInTime != 0) || fadeInTime > 0)
            {
                channel.SetVolume(0.0f);
                int maxFadeInTime = file.Effects != null ? Math.Max(file.Effects.FadeInTime, fadeInTime) : fadeInTime;
                channel.SlideVolume(volume, maxFadeInTime);
            }
            else
            {
                channel.SetVolume(volume);
            }
            return true;
        }

        public FilePlayer()
        {
            m_RunningFiles = new Dictionary<int, RunningFileInfo>();
        }

        public void Dispose()
        {
        }

        // a repeated sound starts again
        private void OnLooped(AudioChannel channel, ISoundFile file, RunningFileInfo info)
        {
            if (file.Effects != null && (file.Effects.FadeOutTime != 0 || file.Effects.CueOut.Active))
            {
                SetStartVolume(file, 0, channel, info);
            }
            if (file.Effects != null && file.Effects.Balance.Active && file.Effects.Balance.IsPanning)
            {
                SetBalanceEffect(channel, file.Id, file.Effects.Balance);
            }
        }

        // the position where the fade out begins was reached
        private void OnFadeStart(AudioChannel channel, ISoundFile file, RunningFileInfo info)
        {
            // the fade out itself is started inside the mixer (sample accurate)
            if (info.CrossFade)
            {
                CallEndAction(channel.Handle);
            }
        }

        private void CallEndAction(int channel)
        {
            Action endAction = null;
            lock (m_Mutex)
            {
                if (m_RunningFiles.ContainsKey(channel))
                {
                    var info = m_RunningFiles[channel];
                    endAction = info.EndAction;
                    info.EndAction = null; // call only once
                }
            }
            if (endAction != null)
            {
                endAction();
            }
        }

        private void FileFinished(int channel, bool deleteMemory)
        {
            Action endAction = null;
            lock (m_Mutex)
            {
                if (m_RunningFiles.ContainsKey(channel))
                {
                    var info = m_RunningFiles[channel];
                    if (info.EndAction != null)
                    {
                        endAction = info.EndAction;
                    }
                    m_RunningFiles.Remove(channel);
                }
            }

            if (endAction != null)
            {
                endAction();
            }
        }

        private class RunningFileInfo
        {
            public Action EndAction { get; set; }
            public float Volume { get; set; }
            public bool CrossFade { get; set; }
            public ISoundFile File { get; set; }
            public Action StopAction { get; set; }
            public Func<bool> RepeatQuery { get; set; }
            public Action<bool> RepeatChange { get; set; }
        }

        private Dictionary<int, RunningFileInfo> m_RunningFiles;
        private Object m_Mutex = new Int16();

    }
}
