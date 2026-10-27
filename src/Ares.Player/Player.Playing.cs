/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 Helpers for what the buttons and the "playing now" strip show:
   - what a button plays (music, sound effects or both);
   - whether a playing sound is configured to repeat;
   - how many times each music file was played.
*/
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Ares.Data;


namespace Ares.Player
{
    public partial class Player
    {
        private readonly Dictionary<int, bool> m_ConfiguredRepeat = new Dictionary<int, bool>();
        private readonly HashSet<int> m_RepeatTouched = new HashSet<int>();

        // 1 = music, 2 = sound effects (3 = both)
        private static int KindsOf(IElement element, int depth)
        {
            if (element == null || depth > 12)
                return 0;
            if (element is IFileElement)
                return ((IFileElement)element).SoundFileType == SoundFileType.Music ? 1 : 2;
            if (element is IWebRadioElement || element is IMusicList)
                return 1;
            IReferenceElement reference = element as IReferenceElement;
            if (reference != null)
                return KindsOf(DataModule.ElementRepository.GetElement(reference.ReferencedId), depth + 1);
            int kinds = 0;
            // containers (also the sound lists with repeat): look at the files they hold
            IGeneralElementContainer container = element as IGeneralElementContainer;
            if (container != null)
            {
                foreach (IFileElement file in container.GetFileElements())
                    kinds |= file.SoundFileType == SoundFileType.Music ? 1 : 2;
                if (kinds != 0)
                    return kinds;
                foreach (IContainerElement inner in container.GetGeneralElements())
                    kinds |= KindsOf(inner, depth + 1);
                if (kinds != 0)
                    return kinds;
            }
            IContainerElement wrapper = element as IContainerElement;
            if (wrapper != null && !ReferenceEquals(wrapper.InnerElement, element))
                return KindsOf(wrapper.InnerElement, depth + 1);
            return kinds;
        }

        // whether the file sits in a part of the element which is set to repeat (several times or forever)
        private static bool RepeatConfiguredIn(IElement element, int fileId, bool inherited, int depth)
        {
            if (element == null || depth > 12)
                return false;
            bool repeat = inherited;
            IRepeatableElement repeatable = element as IRepeatableElement;
            if (repeatable != null && (repeatable.RepeatCount == -1 || repeatable.RepeatCount > 1))
                repeat = true;
            IFileElement file = element as IFileElement;
            if (file != null)
                return file.Id == fileId && repeat;
            IReferenceElement reference = element as IReferenceElement;
            if (reference != null)
                return RepeatConfiguredIn(DataModule.ElementRepository.GetElement(reference.ReferencedId), fileId, repeat, depth + 1);
            IGeneralElementContainer container = element as IGeneralElementContainer;
            if (container != null)
            {
                bool holds = false;
                foreach (IFileElement f in container.GetFileElements())
                {
                    if (f.Id == fileId)
                    {
                        holds = true;
                        break;
                    }
                }
                if (!holds)
                    return false;
                if (repeat)
                    return true;
                foreach (IContainerElement inner in container.GetGeneralElements())
                {
                    if (!ReferenceEquals(inner, element) && RepeatConfiguredIn(inner, fileId, repeat, depth + 1))
                        return true;
                }
                return false;
            }
            IContainerElement wrapper = element as IContainerElement;
            if (wrapper != null && !ReferenceEquals(wrapper.InnerElement, element))
                return RepeatConfiguredIn(wrapper.InnerElement, fileId, repeat, depth + 1);
            return false;
        }

        private bool IsRepeatConfigured(int fileId)
        {
            try
            {
                foreach (IModeElement me in m_PlayingControl.CurrentModeElements)
                {
                    if (RepeatConfiguredIn(me.StartElement, fileId, false, 0))
                        return true;
                }
                IMode mode = CurrentMode();
                if (mode != null)
                {
                    foreach (IModeElement me in mode.GetElements())
                    {
                        if (RepeatConfiguredIn(me.StartElement, fileId, false, 0))
                            return true;
                    }
                }
            }
            catch (Exception swallowed)
            {
                Ares.Settings.ErrorLog.Note(swallowed);
            }
            return false;
        }

        // a sound which is set to repeat shows the repeat button as switched on, until the user changes it
        private void ShowConfiguredRepeat(List<Ares.Playing.PlayingFileInfo> sounds)
        {
            HashSet<int> alive = new HashSet<int>();
            foreach (Ares.Playing.PlayingFileInfo info in sounds)
                alive.Add(info.Handle);
            foreach (int h in new List<int>(m_ConfiguredRepeat.Keys))
            {
                if (!alive.Contains(h))
                    m_ConfiguredRepeat.Remove(h);
            }
            m_RepeatTouched.RemoveWhere(h => !alive.Contains(h));
            foreach (Ares.Playing.PlayingFileInfo info in sounds)
            {
                if (info.Repeat || m_RepeatTouched.Contains(info.Handle))
                    continue;
                bool configured;
                if (!m_ConfiguredRepeat.TryGetValue(info.Handle, out configured))
                {
                    configured = IsRepeatConfigured(info.FileId);
                    m_ConfiguredRepeat[info.Handle] = configured;
                }
                if (configured)
                    info.Repeat = true;
            }
        }

        // a music title started: counts one more play
        private void CountMusicPlay(int musicElementId)
        {
            if (musicElementId < 0)
                return;
            try
            {
                IFileElement file = DataModule.ElementRepository.GetElement(musicElementId) as IFileElement;
                if (file == null)
                    return;
                string full = FullPathOf(file);
                int n = PlayCounts.Add(full);
                RefreshTrackPlays(file.Id, n);
            }
            catch (Exception swallowed)
            {
                Ares.Settings.ErrorLog.Note(swallowed);
            }
        }
    }
}
