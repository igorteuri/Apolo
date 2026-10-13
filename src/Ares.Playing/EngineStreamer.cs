/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 Streaming to an Icecast / Shoutcast server (the sound which Apolo plays is encoded and sent).
*/
using System;

namespace Ares.Playing
{
    class EngineStreamer : IStreamer
    {
        public void BeginStreaming(StreamingParameters parameters)
        {
            IsStreaming = false;
            ErrorHandling.AddMessage(MessageType.Warning, AudioInit.Text(
                "O envio para servidor Icecast/Shoutcast está temporariamente indisponível nesta versão do Apolo (volta na próxima etapa).",
                "Streaming to an Icecast/Shoutcast server is temporarily unavailable in this version of Apolo (it returns in the next step).",
                "Das Streamen zu einem Icecast/Shoutcast-Server ist in dieser Version von Apolo vorübergehend nicht verfügbar (kommt im nächsten Schritt zurück)."));
        }

        public void EndStreaming()
        {
            IsStreaming = false;
        }

        public bool IsStreaming { get; set; }

        public static EngineStreamer Instance
        {
            get
            {
                if (s_Instance == null)
                    s_Instance = new EngineStreamer();
                return s_Instance;
            }
        }

        private static EngineStreamer s_Instance;
    }
}
