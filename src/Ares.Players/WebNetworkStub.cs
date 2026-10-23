// Stand-in for the old web remote control (src/Ares.Players/WebNetwork.cs), which was built on ServiceStack 4 + Razor
// and does not exist for .NET 8. The remote control of Apolo itself (HttpListener in Player.Remote.cs) is not affected.
using System;

namespace Ares.Players.Web
{
    public class WebNetwork : INetwork
    {
        public WebNetwork(INetworkClient networkClient)
        {
        }

        public void ListenForClient()
        {
            Messages.AddMessage(MessageType.Info, "The old web remote (ServiceStack) is not available in this build.");
        }

        public void StopListenForClient() { }
        public bool ClientConnected { get { return false; } }
        public string ClientName { get { return ""; } }
        public void DisconnectClient(bool listenAgain) { }
        public void Shutdown() { }
        public void ErrorOccurred(int elementId, string errorMessage) { }
        public void InformClientOfPossibleTags(int languageId, Ares.Data.IProject project) { }
        public void InformClientOfProject(Ares.Data.IProject newProject) { }
        public void InformClientOfEverything(int overallVolume, int musicVolume, int soundVolume, Ares.Data.IMode mode, MusicInfo music,
            System.Collections.Generic.IList<Ares.Data.IModeElement> elements, Ares.Data.IProject project, Int32 musicListId, bool musicRepeat,
            int tagLanguageId, System.Collections.Generic.IList<int> activeTags, Data.TagCategoryCombination categoryCombination, int fadeTime, bool fadeOnlyOnChange,
            bool musicOnAllChannels, int fadeOnPreviousNextOption, int fadeOnPreviousNextTime) { }
        public void InformClientOfVolume(Ares.Playing.VolumeTarget target, int value) { }
        public void InformClientOfFading(int fadeTime, bool fadeOnlyOnChange) { }
        public void InformClientOfImportProgress(int percent, String additionalInfo) { }
    }
}
