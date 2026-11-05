/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 The buttons which play a YouTube playlist: the button is a normal button of a folder which holds a web radio element whose address
 is the playlist (so it is saved in the project like any other); a click plays the playlist in the YouTube tab of Apolo.
 Here: recognizing the playlist in what the user pastes, the small scripts which run in the YouTube page, and reading what the
 page answers. No window code, so that it can be tested automatically.
*/
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Ares.Player
{
    internal static class YouTubePlaylist
    {
        private static readonly Regex ListParameter = new Regex(@"[?&]list=([A-Za-z0-9_-]{10,80})", RegexOptions.Compiled);
        private static readonly Regex BareId = new Regex(@"^(PL|UU|OLAK5uy_|FL|LL|RD)[A-Za-z0-9_-]{8,70}$", RegexOptions.Compiled);

        private static bool IsYouTubeHost(string host)
        {
            host = (host ?? "").ToLowerInvariant();
            return host == "youtu.be" || host == "youtube.com" || host.EndsWith(".youtube.com", StringComparison.Ordinal);
        }

        /// <summary>The id of the playlist in a pasted address (or the id alone); null when the text is not a YouTube playlist.</summary>
        public static string ExtractId(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;
            text = text.Trim();
            if (BareId.IsMatch(text))
                return text;
            string candidate = text.IndexOf("://", StringComparison.Ordinal) < 0 ? "https://" + text : text;
            Uri uri;
            if (!Uri.TryCreate(candidate, UriKind.Absolute, out uri) || !IsYouTubeHost(uri.Host))
                return null;
            Match m = ListParameter.Match(uri.Query);
            return m.Success ? m.Groups[1].Value : null;
        }

        /// <summary>The address which lists the playlist (its first video is played from there).</summary>
        public static string UrlFor(string id)
        {
            return "https://www.youtube.com/playlist?list=" + id;
        }

        /// <summary>The id when the address is the one made by UrlFor (the address kept in the button), otherwise null.</summary>
        public static string IdOfButtonUrl(string url)
        {
            return ExtractId(url);
        }

        /// <summary>"Title - YouTube" of the page without the site name.</summary>
        public static string CleanTitle(string pageTitle)
        {
            string t = (pageTitle ?? "").Trim();
            if (t.EndsWith(" - YouTube", StringComparison.Ordinal))
                t = t.Substring(0, t.Length - " - YouTube".Length).Trim();
            // "(3) title": the number of notifications in front
            t = Regex.Replace(t, @"^\(\d+\)\s+", "");
            return t;
        }

        // ---- scripts which run in the page ----

        /// <summary>On the page of the playlist: opens the first link to a video of the list (the "play all" button, whatever the language) (which then plays the list). Answers "1" when it did, "0" when the page is not ready.</summary>
        public const string StartScript = @"(function(){var a=document.querySelector('a[href*=""/watch""][href*=""list=""]')||document.querySelector('ytd-playlist-video-renderer a[href*=""/watch""]');if(!a||!a.href)return '0';location.href=a.href;return '1';})()";

        /// <summary>What the page shows now, as JSON text (see Snapshot.Parse).</summary>
        public const string ReadScript = @"(function(){try{var v=document.querySelector('video'),items=[],cur=-1,nodes=document.querySelectorAll('ytd-playlist-panel-video-renderer'),page='other';
if(location.pathname==='/watch')page='watch';else if(location.pathname==='/playlist')page='playlist';
for(var i=0;i<nodes.length;i++){var t=nodes[i].querySelector('#video-title');items.push(((t&&(t.getAttribute('title')||t.textContent))||'').trim());if(nodes[i].hasAttribute('selected'))cur=i;}
var q=new URLSearchParams(location.search);
if(cur<0&&q.get('index')&&nodes.length)cur=Math.min(nodes.length-1,Math.max(0,parseInt(q.get('index'),10)-1));
return JSON.stringify({page:page,list:q.get('list')||'',items:items,index:cur,paused:v?v.paused:true,ended:v?v.ended:false,title:document.title});}catch(e){return JSON.stringify({page:'error'});}})()";

        public const string PauseScript = "(function(){var v=document.querySelector('video');if(v)v.pause();})()";
        public const string PlayScript = "(function(){var v=document.querySelector('video');if(v)v.play();})()";
        public const string NextScript = "(function(){var b=document.querySelector('.ytp-next-button');if(b)b.click();})()";
        public const string PreviousScript = "(function(){var b=document.querySelector('.ytp-prev-button');if(b)b.click();})()";

        /// <summary>Plays the video number "index" (from 0) of the list shown beside the video.</summary>
        public static string JumpScript(int index)
        {
            return "(function(){var n=document.querySelectorAll('ytd-playlist-panel-video-renderer');var a=n[" + index
                + "]&&n[" + index + "].querySelector('a#wc-endpoint');if(a)a.click();})()";
        }

        /// <summary>What the YouTube page answered to ReadScript.</summary>
        public sealed class Snapshot
        {
            public string Page = "other";            // watch, playlist, other, error
            public string ListId = "";
            public List<string> Items = new List<string>();
            public int Index = -1;                   // the video which plays now (from 0), -1 = unknown
            public bool Paused = true;
            public bool Ended;
            public string Title = "";

            /// <summary>Reads the answer of ExecuteScriptAsync (the JSON text may come wrapped once more as a JSON string); null when it is not usable.</summary>
            public static Snapshot Parse(string raw)
            {
                if (string.IsNullOrWhiteSpace(raw) || raw == "null")
                    return null;
                try
                {
                    string json = raw;
                    using (JsonDocument outer = JsonDocument.Parse(raw))
                    {
                        if (outer.RootElement.ValueKind == JsonValueKind.String)
                            json = outer.RootElement.GetString();
                    }
                    using (JsonDocument doc = JsonDocument.Parse(json))
                    {
                        JsonElement root = doc.RootElement;
                        if (root.ValueKind != JsonValueKind.Object)
                            return null;
                        Snapshot s = new Snapshot();
                        s.Page = Text(root, "page", "other");
                        s.ListId = Text(root, "list", "");
                        s.Title = CleanTitle(Text(root, "title", ""));
                        JsonElement items;
                        if (root.TryGetProperty("items", out items) && items.ValueKind == JsonValueKind.Array)
                        {
                            foreach (JsonElement item in items.EnumerateArray())
                                s.Items.Add(item.ValueKind == JsonValueKind.String ? (item.GetString() ?? "") : "");
                        }
                        JsonElement index;
                        if (root.TryGetProperty("index", out index) && index.ValueKind == JsonValueKind.Number)
                            s.Index = index.GetInt32();
                        if (s.Index >= s.Items.Count)
                            s.Index = -1;
                        JsonElement paused;
                        s.Paused = !root.TryGetProperty("paused", out paused) || paused.ValueKind != JsonValueKind.False;
                        JsonElement ended;
                        s.Ended = root.TryGetProperty("ended", out ended) && ended.ValueKind == JsonValueKind.True;
                        return s;
                    }
                }
                catch (JsonException)
                {
                    return null;
                }
                catch (InvalidOperationException)
                {
                    return null;
                }
            }

            private static string Text(JsonElement root, string name, string fallback)
            {
                JsonElement e;
                return root.TryGetProperty(name, out e) && e.ValueKind == JsonValueKind.String ? (e.GetString() ?? fallback) : fallback;
            }

            /// <summary>Whether the page is showing a video of the playlist with this id.</summary>
            public bool IsPlaying(string listId)
            {
                return Page == "watch" && string.Equals(ListId, listId, StringComparison.Ordinal);
            }
        }
    }
}
