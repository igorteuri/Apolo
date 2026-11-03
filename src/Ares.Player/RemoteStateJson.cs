using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Ares.Player
{
    /// <summary>One button as the remote control page / Stream Deck / Foundry see it.</summary>
    internal sealed class RemoteButton
    {
        public int Id;
        public string Title;
        public bool Active;
        public string Image;   // picture of the icons folder (file name), or null
        public string Color;   // "#RRGGBB", or null
    }

    /// <summary>The buttons of one subfolder of the open folder.</summary>
    internal sealed class RemoteGroup
    {
        public string Title;
        public List<RemoteButton> Buttons = new List<RemoteButton>();
    }

    /// <summary>A sound effect which is playing (the remote control page can pause / stop it on its own).</summary>
    internal sealed class RemoteSound
    {
        public int Handle;
        public string Title;
        public bool Paused;
    }

    /// <summary>What the player is doing, for the remote control page (polled by phones, the Stream Deck and Foundry).</summary>
    internal sealed class RemoteState
    {
        public bool Playing, Paused;
        public string Title = "", Artist = "", List = "";
        public double Position, Length;
        public int RepeatMode;
        public int VolOverall, VolMusic, VolSound;
        public List<string> Modes = new List<string>();
        public List<int[]> Tree = new List<int[]>();   // { index, parent, depth } of every folder
        public int Mode = -1;                          // index of the open folder in Modes
        public List<RemoteButton> Elements = new List<RemoteButton>();
        public List<RemoteGroup> Groups = new List<RemoteGroup>();
        public bool MusicPaused, SoundsPaused;         // the music / the sound effects are paused (the "Paused" flag is for the main button)
        public List<RemoteSound> Sounds = new List<RemoteSound>();
        public List<int> Active = new List<int>();     // ids of everything which plays (also in other folders)
    }

    /// <summary>The JSON text of a RemoteState. Kept free of window code so that it is tested automatically.</summary>
    internal static class RemoteStateJson
    {
        public static string Build(RemoteState s)
        {
            StringBuilder json = new StringBuilder();
            json.Append("{\"playing\":").Append(Bool(s.Playing));
            json.Append(",\"paused\":").Append(Bool(s.Paused));
            json.Append(",\"title\":").Append(JsonText.Quote(s.Title ?? ""));
            json.Append(",\"artist\":").Append(JsonText.Quote(s.Artist ?? ""));
            json.Append(",\"list\":").Append(JsonText.Quote(s.List ?? ""));
            json.Append(",\"position\":").Append(s.Position.ToString("0.0", CultureInfo.InvariantCulture));
            json.Append(",\"length\":").Append(s.Length.ToString("0.0", CultureInfo.InvariantCulture));
            json.Append(",\"repeat\":").Append(Bool(s.RepeatMode != 0));
            json.Append(",\"repeatMode\":").Append(s.RepeatMode.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"vol\":{\"overall\":").Append(s.VolOverall.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"music\":").Append(s.VolMusic.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"sound\":").Append(s.VolSound.ToString(CultureInfo.InvariantCulture)).Append("}");
            json.Append(",\"modes\":[").Append(string.Join(",", s.Modes.Select(m => JsonText.Quote(m ?? "")))).Append("]");
            json.Append(",\"tree\":[").Append(string.Join(",", s.Tree.Select(t =>
                "{\"i\":" + t[0].ToString(CultureInfo.InvariantCulture) + ",\"p\":" + t[1].ToString(CultureInfo.InvariantCulture)
                + ",\"d\":" + t[2].ToString(CultureInfo.InvariantCulture) + "}"))).Append("]");
            json.Append(",\"mode\":").Append(s.Mode.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"elements\":[").Append(string.Join(",", s.Elements.Select(Button))).Append("]");
            json.Append(",\"groups\":[").Append(string.Join(",", s.Groups.Select(g =>
                "{\"title\":" + JsonText.Quote(g.Title ?? "") + ",\"elements\":[" + string.Join(",", g.Buttons.Select(Button)) + "]}"))).Append("]");
            json.Append(",\"musicPaused\":").Append(Bool(s.MusicPaused));
            json.Append(",\"soundsPaused\":").Append(Bool(s.SoundsPaused));
            json.Append(",\"sounds\":[").Append(string.Join(",", s.Sounds.Select(x =>
                "{\"h\":" + x.Handle.ToString(CultureInfo.InvariantCulture) + ",\"title\":" + JsonText.Quote(x.Title ?? "")
                + ",\"paused\":" + Bool(x.Paused) + "}"))).Append("]");
            json.Append(",\"active\":[").Append(string.Join(",", s.Active.Select(a => a.ToString(CultureInfo.InvariantCulture)))).Append("]}");
            return json.ToString();
        }

        private static string Bool(bool b)
        {
            return b ? "true" : "false";
        }

        private static string Button(RemoteButton b)
        {
            StringBuilder json = new StringBuilder();
            json.Append("{\"id\":").Append(b.Id.ToString(CultureInfo.InvariantCulture)).Append(",\"title\":").Append(JsonText.Quote(b.Title ?? ""));
            json.Append(",\"active\":").Append(Bool(b.Active));
            if (!string.IsNullOrEmpty(b.Image))
                json.Append(",\"img\":").Append(JsonText.Quote(b.Image));
            if (!string.IsNullOrEmpty(b.Color))
                json.Append(",\"color\":").Append(JsonText.Quote(b.Color));
            json.Append("}");
            return json.ToString();
        }
    }
}
