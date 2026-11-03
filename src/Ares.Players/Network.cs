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
using Ares.Data;
using Ares.Playing;

namespace Ares.Players
{
    public interface INetworkClient
    {
        void KeyReceived(int key);
        void VolumeReceived(Ares.Playing.VolumeTarget target, int value);
        void ClientDataChanged(bool listenAgainAfterDisconnect);
        void ClientConnected();
        String GetProjectsDirectory();
        Ares.Settings.RecentFiles GetLastUsedProjects();
        void ProjectShallChange(String newProjectFile);
        Ares.Data.IProject GetCurrentProject();
        void PlayOtherMusic(Int32 elementId);
        void SwitchElement(Int32 elementId);
        void SetMusicRepeat(bool repeat);
        void SwitchTag(Int32 categoryId, Int32 tagId, bool tagIsActive);
        void DeactivateAllTags();
        void SetTagCategoryCombination(Data.TagCategoryCombination categoryCombination);
        void SetMusicTagsFading(Int32 fadeTime, bool onlyOnChange);
        void SetPlayMusicOnAllSpeakers(bool onAllSpeakers);
        void SetFadingOnPreviousNext(int option, int fadeTime);
        void ChangeMode(String title);
    }

    public interface IUDPBroadcast
    {
        void InitConnectionData();
        void StartUdpBroadcast();
        bool SendUdpPacket();
        void StopUdpBroadcast();
    }

    public interface INetwork
    {
        void ListenForClient();
        void StopListenForClient();

        bool ClientConnected { get; }
        String ClientName { get; }
        void DisconnectClient(bool listenAgain);
        void Shutdown();
        
        void ErrorOccurred(int elementId, string errorMessage);
        void InformClientOfPossibleTags(int languageId, Ares.Data.IProject project);
        void InformClientOfProject(Ares.Data.IProject newProject);
        void InformClientOfEverything(int overallVolume, int musicVolume, int soundVolume, Ares.Data.IMode mode, MusicInfo music,
            System.Collections.Generic.IList<Ares.Data.IModeElement> elements, Ares.Data.IProject project, Int32 musicListId, bool musicRepeat,
            int tagLanguageId, System.Collections.Generic.IList<int> activeTags, Data.TagCategoryCombination categoryCombination, int fadeTime, bool fadeOnlyOnChange,
            bool musicOnAllChannels, int fadeOnPreviousNextOption, int fadeOnPreviousNextTime);
        void InformClientOfVolume(Ares.Playing.VolumeTarget target, int value);
        void InformClientOfFading(int fadeTime, bool fadeOnlyOnChange);
        void InformClientOfImportProgress(int percent, String additionalInfo);
    }

    public interface INetworks : INetwork, IUDPBroadcast
    { }

    /// <summary>
    /// The old Ares network (TCP server and UDP broadcast for the Android controller, and the ServiceStack web remote) is
    /// gone: Apolo has its own remote control (RemoteServer). This keeps the interface the player talks to and does nothing,
    /// exactly what the old classes did since Apolo switched them off at start.
    /// </summary>
    public class Networks : INetworks
    {
        public static readonly int PLAYER_VERSION = 4;

        public Networks(INetworkClient client, bool useLegacy, bool useWeb)
        {
        }

        public bool ClientConnected { get { return false; } }
        public string ClientName { get { return ""; } }
        public void ListenForClient() { }
        public void StopListenForClient() { }
        public void DisconnectClient(bool listenAgain) { }
        public void Shutdown() { }
        public void ErrorOccurred(int elementId, string errorMessage) { }
        public void InformClientOfPossibleTags(int languageId, IProject project) { }
        public void InformClientOfProject(IProject newProject) { }
        public void InformClientOfEverything(int overallVolume, int musicVolume, int soundVolume, IMode mode, MusicInfo music, IList<IModeElement> elements,
            IProject project, int musicListId, bool musicRepeat, int tagLanguageId, IList<int> activeTags, TagCategoryCombination categoryCombination,
            int fadeTime, bool fadeOnlyOnChange, bool musicOnAllChannels, int fadeOnPreviousNextOption, int fadeOnPreviousNextTime) { }
        public void InformClientOfVolume(VolumeTarget target, int value) { }
        public void InformClientOfFading(int fadeTime, bool fadeOnlyOnChange) { }
        public void InformClientOfImportProgress(int percent, String additionalInfo) { }
        public void InitConnectionData() { }
        public void StartUdpBroadcast() { }
        public bool SendUdpPacket() { return true; }
        public void StopUdpBroadcast() { }
    }
}
