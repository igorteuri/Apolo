using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Ares.Data;
using Ares.Players;
using Ares.Settings;
using Ares.CommonGUI;

namespace Ares.Player
{
    // INetworkClient: what controllers and the network ask the player to do.
    public partial class Player
    {
        public void KeyReceived(int key)
        {
            m_PlayingControl.KeyReceived(key);
        }

        public void ChangeMode(String title)
        {
            m_PlayingControl.SetMode(title);
        }

        public void VolumeReceived(Ares.Playing.VolumeTarget target, int value)
        {
            switch (target)
            {
                case Ares.Playing.VolumeTarget.Both:
                    m_PlayingControl.GlobalVolume = value;
                    break;
                case Ares.Playing.VolumeTarget.Music:
                    m_PlayingControl.MusicVolume = value;
                    break;
                case Ares.Playing.VolumeTarget.Sounds:
                    m_PlayingControl.SoundVolume = value;
                    break;
                default:
                    break;
            }
        }

        public void ClientDataChanged(bool listenAgainAfterDisconnect)
        {
            if (listenAgainAfterDisconnect)
                m_Network.StartUdpBroadcast();
            if (this.IsDisposed)
                return;
            this.Invoke(new MethodInvoker(UpdateClientData));
        }

        public void ClientConnected()
        {
            m_Network.StopUdpBroadcast();
            this.Invoke(new MethodInvoker(UpdateClientData));
        }

        public String GetProjectsDirectory()
        {
            String oldPath = m_Project != null ? m_Project.FileName : System.Environment.CurrentDirectory;
            if (m_Project != null)
            {
                oldPath = System.IO.Directory.GetParent(oldPath).FullName;
            }
            return oldPath;
        }

        public Ares.Settings.RecentFiles GetLastUsedProjects()
        {
            return Ares.Settings.Settings.Instance.RecentFiles;
        }

        public void ProjectShallChange(String newProjectPath)
        {
            OpenProjectFromController(newProjectPath);
        }

        public IProject GetCurrentProject()
        {
            return m_Project;
        }

        public void PlayOtherMusic(Int32 id)
        {
            m_PlayingControl.SelectMusicElement(id);
        }

        public void SwitchElement(Int32 id)
        {
            m_PlayingControl.SwitchElement(id);
        }

        public void SetMusicRepeat(bool repeat)
        {
            m_PlayingControl.SetMusicRepeatMode(repeat ? 2 : 0);
        }

        public void SwitchTag(Int32 categoryId, Int32 tagId, bool active)
        {
            if (active)
            {
                m_PlayingControl.AddMusicTag(categoryId, tagId);
            }
            else
            {
                m_PlayingControl.RemoveMusicTag(categoryId, tagId);
            }
        }

        public void DeactivateAllTags()
        {
            m_PlayingControl.RemoveAllMusicTags();
        }

        public void SetTagCategoryCombination(Data.TagCategoryCombination categoryCombination)
        {
            m_PlayingControl.SetMusicTagCategoriesCombination(categoryCombination);
        }

        public void SetMusicTagsFading(Int32 fadeTime, bool onlyOnChange)
        {
            m_PlayingControl.SetMusicTagFading(fadeTime, onlyOnChange);
        }

        public void SetPlayMusicOnAllSpeakers(bool onAllSpeakers)
        {
            m_PlayingControl.SetPlayMusicOnAllSpeakers(onAllSpeakers);
        }

        public void SetFadingOnPreviousNext(int fadeMode, int fadeTime)
        {
            m_PlayingControl.SetFadingOnPreviousNext(fadeMode != 0, fadeMode == 2, fadeTime);
        }

        private bool m_WasConnected = false;

        private void UpdateClientData()
        {
            UpdateClientState();
            if (m_Network.ClientConnected)
            {
                m_Network.InformClientOfEverything(m_PlayingControl.GlobalVolume, m_PlayingControl.MusicVolume,
                    m_PlayingControl.SoundVolume, m_PlayingControl.CurrentMode, MusicInfo.GetInfo(m_PlayingControl.CurrentMusicElement),
                    m_PlayingControl.CurrentModeElements, m_Project, 
                    m_PlayingControl.CurrentMusicList, m_PlayingControl.MusicRepeat,
                    m_TagLanguageId, new List<int>(m_PlayingControl.GetCurrentMusicTags()), m_PlayingControl.GetMusicTagCategoriesCombination(),
                    Settings.Settings.Instance.TagMusicFadeTime, Settings.Settings.Instance.TagMusicFadeOnlyOnChange,
                    Settings.Settings.Instance.PlayMusicOnAllSpeakers, 
                    Settings.Settings.Instance.ButtonMusicFadeMode, Settings.Settings.Instance.ButtonMusicFadeTime);
                m_WasConnected = true;
            }
            else
            {
                if (m_HideToTray && m_WasConnected)
                {
                    m_Quitting = true;
                    Close();
                }
            }
        }
    }
}
