/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 Search of free pictures and free audio on the internet (no account needed):
   pictures: Openverse, Wikimedia Commons
   audio:    Openverse, Internet Archive, Freesound (only with a key of the user: %APPDATA%\Apolo\freesound.key)
 Every result keeps the author and the licence, so the credit can be saved with the file.
*/
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace Ares.Player
{
    internal class MediaHit
    {
        public string Source = "";
        public string Title = "";
        public string Author = "";
        public string License = "";
        public string LicenseUrl = "";
        public string PageUrl = "";
        public string ThumbUrl = "";
        public string DownloadUrl = "";   // "ia:<identifier>" for the Internet Archive (resolved when downloading)
        public string Ext = "";
        public double Duration;           // seconds, 0 = unknown

        public string Credit
        {
            get
            {
                string s = Title;
                if (Author.Length > 0)
                    s += " - " + Author;
                if (License.Length > 0)
                    s += " (" + License + ")";
                return s;
            }
        }
    }

    internal static class OpenMedia
    {
        private static string UserAgent
        {
            get
            {
                string v = "";
                try { v = System.Reflection.Assembly.GetEntryAssembly().GetName().Version.ToString(3); } catch (Exception) { }
                return "Apolo/" + v + " (https://github.com/igorteuri/Apolo)";
            }
        }

        public static string FreesoundKeyFile
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Path.Combine("Apolo", "freesound.key")); }
        }

        public static string FreesoundKey
        {
            get
            {
                try
                {
                    if (File.Exists(FreesoundKeyFile))
                        return File.ReadAllText(FreesoundKeyFile).Trim();
                }
                catch (Exception) { }
                return "";
            }
        }

        private static WebClient NewClient()
        {
            try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; } catch (Exception) { }
            WebClient c = new WebClient();
            c.Encoding = Encoding.UTF8;
            c.Headers[HttpRequestHeader.UserAgent] = UserAgent;
            try { c.Proxy = WebRequest.GetSystemWebProxy(); } catch (Exception) { }
            return c;
        }

        private static object GetJson(string url)
        {
            using (WebClient c = NewClient())
            {
                string text = c.DownloadString(url);
                JavaScriptSerializer js = new JavaScriptSerializer();
                js.MaxJsonLength = int.MaxValue;
                return js.DeserializeObject(text);
            }
        }

        private static string SafeName(string name)
        {
            StringBuilder sb = new StringBuilder();
            char[] bad = Path.GetInvalidFileNameChars();
            foreach (char c in name)
                sb.Append(Array.IndexOf(bad, c) >= 0 ? '-' : c);
            string s = Regex.Replace(sb.ToString(), "\\s+", " ").Trim().TrimEnd('.', ' ');
            return s.Length == 0 ? "sem nome" : s;
        }

        public static byte[] GetBytes(string url)
        {
            using (WebClient c = NewClient())
                return c.DownloadData(url);
        }

        public static void DownloadFile(string url, string path)
        {
            using (WebClient c = NewClient())
                c.DownloadFile(url, path);
        }

        // ---- small helpers for the loose json ----

        private static Dictionary<string, object> Obj(object o)
        {
            return o as Dictionary<string, object>;
        }

        private static object[] Arr(object o)
        {
            object[] a = o as object[];
            if (a != null)
                return a;
            System.Collections.ArrayList list = o as System.Collections.ArrayList;
            return list != null ? list.ToArray() : null;
        }

        private static object Child(object o, string key)
        {
            Dictionary<string, object> d = Obj(o);
            object v;
            if (d != null && d.TryGetValue(key, out v))
                return v;
            return null;
        }

        private static string Str(object o, string key)
        {
            object v = Child(o, key);
            if (v == null)
                return "";
            object[] a = Arr(v);
            if (a != null)
                return a.Length > 0 && a[0] != null ? Convert.ToString(a[0], System.Globalization.CultureInfo.InvariantCulture) : "";
            return Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string StripHtml(string s)
        {
            if (string.IsNullOrEmpty(s))
                return "";
            s = Regex.Replace(s, "<[^>]*>", "");
            s = WebUtility.HtmlDecode(s);
            return Regex.Replace(s, @"\s+", " ").Trim();
        }

        private static string Enc(string s)
        {
            return Uri.EscapeDataString(s ?? "");
        }

        // "by" + "4.0" -> "CC BY 4.0"
        private static string LicenseName(string code, string version)
        {
            code = (code ?? "").Trim().ToLowerInvariant();
            if (code.Length == 0)
                return "";
            if (code == "cc0")
                return "CC0";
            if (code == "pdm")
                return "Public Domain";
            string s = "CC " + code.ToUpperInvariant();
            if (!string.IsNullOrEmpty(version))
                s += " " + version;
            return s;
        }

        // "https://creativecommons.org/licenses/by-nc/4.0/" -> "CC BY-NC 4.0"
        private static string LicenseFromUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return "";
            Match m = Regex.Match(url, @"creativecommons\.org/licenses/([a-z\-]+)/([0-9.]+)", RegexOptions.IgnoreCase);
            if (m.Success)
                return LicenseName(m.Groups[1].Value, m.Groups[2].Value);
            if (url.IndexOf("publicdomain/zero", StringComparison.OrdinalIgnoreCase) >= 0)
                return "CC0";
            if (url.IndexOf("publicdomain", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Public Domain";
            return url;
        }

        // ---- pictures ----

        public static List<MediaHit> SearchImages(string query, bool commercialOnly, out string problems)
        {
            List<MediaHit> hits = new List<MediaHit>();
            StringBuilder errors = new StringBuilder();
            try { SearchOpenverseImages(query, commercialOnly, hits); }
            catch (Exception ex) { errors.Append("Openverse: " + ex.Message + " "); }
            try { SearchCommonsImages(query, hits); }
            catch (Exception ex) { errors.Append("Wikimedia: " + ex.Message + " "); }
            problems = errors.ToString().Trim();
            return hits;
        }

        private static void SearchOpenverseImages(string query, bool commercialOnly, List<MediaHit> hits)
        {
            string url = "https://api.openverse.org/v1/images/?q=" + Enc(query) + "&page_size=24"
                + (commercialOnly ? "&license_type=commercial" : "");
            object root = GetJson(url);
            object[] results = Arr(Child(root, "results"));
            if (results == null)
                return;
            foreach (object r in results)
            {
                MediaHit h = new MediaHit();
                h.Source = "Openverse";
                h.Title = Str(r, "title");
                h.Author = Str(r, "creator");
                h.License = LicenseName(Str(r, "license"), Str(r, "license_version"));
                h.LicenseUrl = Str(r, "license_url");
                h.PageUrl = Str(r, "foreign_landing_url");
                string id = Str(r, "id");
                h.ThumbUrl = Str(r, "thumbnail");
                if (h.ThumbUrl.Length == 0 && id.Length > 0)
                    h.ThumbUrl = "https://api.openverse.org/v1/images/" + id + "/thumb/";
                h.DownloadUrl = h.ThumbUrl;
                if (h.ThumbUrl.Length > 0)
                    hits.Add(h);
            }
        }

        private static void SearchCommonsImages(string query, List<MediaHit> hits)
        {
            string url = "https://commons.wikimedia.org/w/api.php?action=query&generator=search&gsrsearch=" + Enc(query + " filetype:bitmap")
                + "&gsrnamespace=6&gsrlimit=24&prop=imageinfo&iiprop=url%7Cextmetadata&iiurlwidth=480&format=json";
            object root = GetJson(url);
            Dictionary<string, object> pages = Obj(Child(Child(root, "query"), "pages"));
            if (pages == null)
                return;
            foreach (KeyValuePair<string, object> page in pages)
            {
                object[] infos = Arr(Child(page.Value, "imageinfo"));
                if (infos == null || infos.Length == 0)
                    continue;
                object info = infos[0];
                object meta = Child(info, "extmetadata");
                MediaHit h = new MediaHit();
                h.Source = "Wikimedia";
                string title = Str(page.Value, "title");
                h.Title = Regex.Replace(Regex.Replace(title, "^File:", ""), @"\.[A-Za-z0-9]{3,4}$", "").Replace('_', ' ');
                h.Author = StripHtml(Str(Child(meta, "Artist"), "value"));
                h.License = StripHtml(Str(Child(meta, "LicenseShortName"), "value"));
                h.LicenseUrl = Str(Child(meta, "LicenseUrl"), "value");
                h.PageUrl = Str(info, "descriptionurl");
                h.ThumbUrl = Str(info, "thumburl");
                h.DownloadUrl = h.ThumbUrl;
                if (h.ThumbUrl.Length > 0)
                    hits.Add(h);
            }
        }

        // ---- audio ----

        public static List<MediaHit> SearchAudio(string query, bool effects, bool commercialOnly, out string problems)
        {
            List<MediaHit> hits = new List<MediaHit>();
            StringBuilder errors = new StringBuilder();
            try { SearchOpenverseAudio(query, effects, commercialOnly, hits); }
            catch (Exception ex)
            {
                // Openverse now asks for a registration for most requests: not worth bothering the person with it
                if (ex.Message.IndexOf("401", StringComparison.Ordinal) < 0)
                    errors.Append("Openverse: " + ex.Message + " ");
            }
            try { SearchCommons(query, commercialOnly, hits); }
            catch (Exception ex) { errors.Append("Wikimedia Commons: " + ex.Message + " "); }
            if (!effects)
            {
                try { SearchArchive(query, commercialOnly, hits); }
                catch (Exception ex) { errors.Append("Internet Archive: " + ex.Message + " "); }
            }
            if (FreesoundKey.Length > 0)
            {
                try { SearchFreesound(query, commercialOnly, hits); }
                catch (Exception ex) { errors.Append("Freesound: " + ex.Message + " "); }
            }
            problems = errors.ToString().Trim();
            return hits;
        }

        private static void SearchOpenverseAudio(string query, bool effects, bool commercialOnly, List<MediaHit> hits)
        {
            string url = "https://api.openverse.org/v1/audio/?q=" + Enc(query) + "&page_size=24&category=" + (effects ? "sound_effect" : "music")
                + (commercialOnly ? "&license_type=commercial" : "");
            object root = GetJson(url);
            object[] results = Arr(Child(root, "results"));
            if (results == null)
                return;
            foreach (object r in results)
            {
                MediaHit h = new MediaHit();
                h.Source = "Openverse";
                h.Title = Str(r, "title");
                h.Author = Str(r, "creator");
                h.License = LicenseName(Str(r, "license"), Str(r, "license_version"));
                h.LicenseUrl = Str(r, "license_url");
                h.PageUrl = Str(r, "foreign_landing_url");
                h.DownloadUrl = Str(r, "url");
                h.Ext = Str(r, "filetype");
                double ms;
                if (double.TryParse(Str(r, "duration"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out ms))
                    h.Duration = ms / 1000.0;
                if (h.DownloadUrl.Length > 0)
                    hits.Add(h);
            }
        }

        // Wikimedia Commons: free audio files, no key needed
        private static void SearchCommons(string query, bool commercialOnly, List<MediaHit> hits)
        {
            string url = "https://commons.wikimedia.org/w/api.php?action=query&format=json&generator=search&gsrnamespace=6&gsrlimit=24"
                + "&gsrsearch=" + Enc(query + " filetype:audio")
                + "&prop=imageinfo&iiprop=url%7Cextmetadata%7Cmime";
            object root = GetJson(url);
            Dictionary<string, object> pages = Obj(Child(Child(root, "query"), "pages"));
            if (pages == null)
                return;
            foreach (KeyValuePair<string, object> kv in pages)
            {
                object[] infos = Arr(Child(kv.Value, "imageinfo"));
                if (infos == null || infos.Length == 0)
                    continue;
                object info = infos[0];
                object meta = Child(info, "extmetadata");
                string lic = StripHtml(Str(Child(meta, "LicenseShortName"), "value"));
                if (commercialOnly && lic.IndexOf("NC", StringComparison.Ordinal) >= 0)
                    continue;
                MediaHit h = new MediaHit();
                h.Source = "Wikimedia Commons";
                string title = Str(kv.Value, "title");
                if (title.StartsWith("File:", StringComparison.Ordinal))
                    title = title.Substring(5);
                int dot = title.LastIndexOf('.');
                if (dot > 0)
                    title = title.Substring(0, dot);
                h.Title = title;
                h.Author = StripHtml(Str(Child(meta, "Artist"), "value"));
                h.License = lic;
                h.LicenseUrl = Str(Child(meta, "LicenseUrl"), "value");
                h.PageUrl = Str(info, "descriptionurl");
                h.DownloadUrl = Str(info, "url");
                int d2 = h.DownloadUrl.LastIndexOf('.');
                h.Ext = d2 > 0 ? h.DownloadUrl.Substring(d2 + 1).ToLowerInvariant() : "ogg";
                if (h.DownloadUrl.Length > 0)
                    hits.Add(h);
            }
        }

        private static void SearchArchive(string query, bool commercialOnly, List<MediaHit> hits)
        {
            string q = "(" + query + ") AND mediatype:audio AND licenseurl:(*creativecommons.org* OR *publicdomain*)";
            if (commercialOnly)
                q += " AND NOT licenseurl:(*by-nc*)";
            string url = "https://archive.org/advancedsearch.php?q=" + Enc(q)
                + "&fl%5B%5D=identifier&fl%5B%5D=title&fl%5B%5D=creator&fl%5B%5D=licenseurl&rows=24&page=1&output=json";
            object root = GetJson(url);
            object[] docs = Arr(Child(Child(root, "response"), "docs"));
            if (docs == null)
                return;
            foreach (object d in docs)
            {
                string id = Str(d, "identifier");
                if (id.Length == 0)
                    continue;
                MediaHit h = new MediaHit();
                h.Source = "Internet Archive";
                h.Title = Str(d, "title");
                h.Author = Str(d, "creator");
                h.LicenseUrl = Str(d, "licenseurl");
                h.License = LicenseFromUrl(h.LicenseUrl);
                h.PageUrl = "https://archive.org/details/" + id;
                h.DownloadUrl = "ia:" + id;
                hits.Add(h);
            }
        }

        private static void SearchFreesound(string query, bool commercialOnly, List<MediaHit> hits)
        {
            string url = "https://freesound.org/apiv2/search/text/?query=" + Enc(query)
                + "&fields=id,name,username,license,previews,duration,url&page_size=24&token=" + Enc(FreesoundKey);
            object root = GetJson(url);
            object[] results = Arr(Child(root, "results"));
            if (results == null)
                return;
            foreach (object r in results)
            {
                string licUrl = Str(r, "license");
                if (commercialOnly && licUrl.IndexOf("by-nc", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                MediaHit h = new MediaHit();
                h.Source = "Freesound";
                h.Title = Str(r, "name");
                h.Author = Str(r, "username");
                h.LicenseUrl = licUrl;
                h.License = LicenseFromUrl(licUrl);
                h.PageUrl = Str(r, "url");
                h.DownloadUrl = Str(Child(r, "previews"), "preview-hq-mp3");
                h.Ext = "mp3";
                double sec;
                if (double.TryParse(Str(r, "duration"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out sec))
                    h.Duration = sec;
                if (h.DownloadUrl.Length > 0)
                    hits.Add(h);
            }
        }

        // Internet Archive: the file to download is found in the metadata of the item
        private static string ResolveArchive(string identifier, out string ext)
        {
            ext = "mp3";
            object root = GetJson("https://archive.org/metadata/" + Enc(identifier));
            object[] files = Arr(Child(root, "files"));
            string best = null, bestExt = null;
            int bestScore = -1;
            if (files != null)
            {
                foreach (object f in files)
                {
                    string name = Str(f, "name");
                    string lower = name.ToLowerInvariant();
                    int score = -1;
                    if (lower.EndsWith(".mp3")) score = 3;
                    else if (lower.EndsWith(".ogg")) score = 2;
                    else if (lower.EndsWith(".flac") || lower.EndsWith(".wav")) score = 1;
                    if (score < 0 || Str(f, "source") == "derivative" && score < 3 && best != null)
                        continue;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = name;
                        bestExt = Path.GetExtension(name).TrimStart('.');
                    }
                }
            }
            if (best == null)
                throw new Exception("sem arquivo de áudio neste item");
            ext = bestExt;
            string[] parts = best.Split('/');
            for (int i = 0; i < parts.Length; i++)
                parts[i] = Enc(parts[i]);
            return "https://archive.org/download/" + Enc(identifier) + "/" + string.Join("/", parts);
        }

        /// <summary>Downloads an audio hit into the folder; returns the full path of the file.</summary>
        public static string DownloadAudio(MediaHit hit, string folder)
        {
            string url = hit.DownloadUrl;
            string ext = hit.Ext;
            if (url.StartsWith("ia:"))
                url = ResolveArchive(url.Substring(3), out ext);
            if (string.IsNullOrEmpty(ext))
            {
                try { ext = Path.GetExtension(new Uri(url).AbsolutePath).TrimStart('.'); } catch (Exception) { ext = ""; }
            }
            if (string.IsNullOrEmpty(ext) || ext.Length > 5)
                ext = "mp3";
            ext = ext.ToLowerInvariant();
            if (ext == "mpeg") ext = "mp3";
            Directory.CreateDirectory(folder);
            string name = SafeName(hit.Author.Length > 0 ? hit.Title + " - " + hit.Author : hit.Title);
            if (name.Length > 120)
                name = name.Substring(0, 120).TrimEnd();
            string path = Path.Combine(folder, name + "." + ext);
            for (int i = 2; File.Exists(path) && i < 1000; i++)
                path = Path.Combine(folder, name + " (" + i + ")." + ext);
            DownloadFile(url, path);
            return path;
        }

        /// <summary>Notes the credit of a downloaded file in "creditos.txt" of the folder and in the tags of the file.</summary>
        public static void SaveCredit(MediaHit hit, string path)
        {
            string line = Path.GetFileName(path) + "\t" + hit.Title + "\t" + hit.Author + "\t" + hit.License + "\t" + hit.PageUrl;
            try
            {
                string file = Path.Combine(Path.GetDirectoryName(path), "creditos.txt");
                File.AppendAllText(file, line.Replace("\r", " ").Replace("\n", " ") + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception) { }
            try
            {
                string text = (hit.License.Length > 0 ? hit.License + ": " : "") + hit.Title + (hit.Author.Length > 0 ? " - " + hit.Author : "")
                    + (hit.PageUrl.Length > 0 ? " - " + hit.PageUrl : "");
                string lic = hit.License.Length > 0 ? hit.License + " (" + hit.Source + ")" : hit.Source;
                Ares.Tags.AudioTags.WriteCredit(path, lic, text);
                Ares.Tags.AudioTags.Write(path, hit.Title, hit.Author, "");
            }
            catch (Exception) { }
        }
    }
}
