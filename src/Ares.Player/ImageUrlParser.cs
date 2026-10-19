using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;

namespace Ares.Player
{
    /// <summary>
    /// Finds the address of a picture in whatever the user pasted: a clean link, a link inside a text, a link with
    /// spaces in it, without "https://", with a typo in the beginning, a link of a search page ("imgurl=...")
    /// or a markdown / html picture.
    /// </summary>
    public static class ImageUrlParser
    {
        private static readonly string[] s_Extensions = { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp" };

        /// <summary>Extensions to try when a link has none (some sites show the picture when it is asked for with one).</summary>
        public static string[] GuessExtensions
        {
            get { return new[] { ".png", ".jpg", ".webp", ".gif" }; }
        }

        private static readonly Regex s_WithScheme = new Regex(
            "(?<![A-Za-z0-9])(?:h?t{1,2}ps?[:;]?[/\\\\]{1,2}|www\\.)[^\\s\"'<>()\\[\\]]+", RegexOptions.IgnoreCase);

        private static readonly Regex s_Bare = new Regex(
            "(?<![A-Za-z0-9@/.\\-])(?:[a-z0-9\\-]+\\.)+[a-z]{2,}/[^\\s\"'<>()\\[\\]]+", RegexOptions.IgnoreCase);

        private static readonly Regex s_Scheme = new Regex("^h?t{1,2}ps?[:;]?[/\\\\]{1,2}", RegexOptions.IgnoreCase);

        public static bool HasImageExtension(string url)
        {
            if (string.IsNullOrEmpty(url))
                return false;
            string path = PathOf(url).ToLowerInvariant();
            foreach (string ext in s_Extensions)
            {
                if (path.EndsWith(ext, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        // the address without "?query" and "#part"
        private static string PathOf(string url)
        {
            int q = url.IndexOfAny(new[] { '?', '#' });
            return q >= 0 ? url.Substring(0, q) : url;
        }

        /// <summary>The addresses which can be meant, the most likely first (the ones which end like a picture come first).</summary>
        public static List<string> Candidates(string text)
        {
            List<string> found = new List<string>();
            if (string.IsNullOrWhiteSpace(text))
                return found;
            string t = WebUtility.HtmlDecode(text.Replace('\u00A0', ' ').Replace("\u200B", ""));
            Collect(t, found);
            if (found.Count == 0)
            {
                // spaces in the middle of the link: "https:// site.com / a.png"
                Collect(Regex.Replace(t, "\\s+", ""), found);
            }
            List<string> pictures = new List<string>();
            List<string> others = new List<string>();
            foreach (string url in found)
            {
                if (HasImageExtension(url))
                    pictures.Add(url);
                else
                    others.Add(url);
            }
            pictures.AddRange(others);
            return pictures;
        }

        private static void Collect(string t, List<string> found)
        {
            MatchCollection matches = s_WithScheme.Matches(t);
            foreach (Match m in matches)
            {
                string raw = m.Value;
                string url;
                if (raw.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                {
                    url = "https://" + raw;
                }
                else
                {
                    Match scheme = s_Scheme.Match(raw);
                    string secure = scheme.Value.IndexOf('s') >= 0 || scheme.Value.IndexOf('S') >= 0 ? "https://" : "http://";
                    url = secure + raw.Substring(scheme.Length);
                }
                AddClean(found, url);
                // spaces inside the name of the file: take the rest of the line
                int lineEnd = t.IndexOf('\n', m.Index);
                string tail = (lineEnd >= 0 ? t.Substring(m.Index, lineEnd - m.Index) : t.Substring(m.Index)).Trim();
                if (tail.Length > 4 && tail.IndexOf(' ') >= 0 && tail.IndexOf("http", 4, StringComparison.OrdinalIgnoreCase) < 0 && !raw.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                {
                    string trimmed = TrimEnd(tail);
                    if (HasImageExtension(trimmed))
                    {
                        Match scheme = s_Scheme.Match(trimmed);
                        string secure = scheme.Value.IndexOf('s') >= 0 || scheme.Value.IndexOf('S') >= 0 ? "https://" : "http://";
                        string rest = trimmed.Substring(scheme.Length);
                        AddClean(found, secure + Regex.Replace(rest, "\\s+", "%20"));
                        AddClean(found, secure + Regex.Replace(rest, "\\s+", ""));
                    }
                }
            }
            if (found.Count == 0)
            {
                foreach (Match m in s_Bare.Matches(t))
                    AddClean(found, "https://" + m.Value);
            }
        }

        private static string TrimEnd(string url)
        {
            return url.TrimEnd('.', ',', ';', ':', '!', '?', '\'', '"', '\\', ' ');
        }

        private static void AddClean(List<string> found, string url)
        {
            url = TrimEnd(url).Replace("\u200B", "");
            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri) || uri.Host.IndexOf('.') < 0)
                return;
            // the link of a search page carries the real address of the picture
            foreach (string key in new[] { "imgurl", "mediaurl", "image_url", "img_url" })
            {
                Match m = Regex.Match(url, "[?&]" + key + "=([^&#]+)", RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    string inner;
                    try { inner = Uri.UnescapeDataString(m.Groups[1].Value); }
                    catch (Exception) { continue; }
                    if (inner.StartsWith("http", StringComparison.OrdinalIgnoreCase) && !found.Contains(inner))
                        found.Add(inner);
                }
            }
            int hash = url.IndexOf('#');
            if (hash > 0)
                url = url.Substring(0, hash);
            if (!found.Contains(url))
                found.Add(url);
        }

        /// <summary>The picture a web page announces for itself (og:image / twitter:image), as a full address; null if none.</summary>
        public static string FindPageImage(string html, string pageUrl)
        {
            if (string.IsNullOrEmpty(html))
                return null;
            string[] patterns =
            {
                "<meta[^>]+(?:property|name)\\s*=\\s*[\"'](?:og:image(?::secure_url)?|twitter:image(?::src)?)[\"'][^>]*content\\s*=\\s*[\"']([^\"']+)[\"']",
                "<meta[^>]+content\\s*=\\s*[\"']([^\"']+)[\"'][^>]*(?:property|name)\\s*=\\s*[\"'](?:og:image(?::secure_url)?|twitter:image(?::src)?)[\"']"
            };
            foreach (string pattern in patterns)
            {
                Match m = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (!m.Success)
                    continue;
                string value = WebUtility.HtmlDecode(m.Groups[1].Value.Trim());
                Uri result;
                Uri baseUri;
                if (Uri.TryCreate(value, UriKind.Absolute, out result))
                    return result.ToString();
                if (Uri.TryCreate(pageUrl, UriKind.Absolute, out baseUri) && Uri.TryCreate(baseUri, value, out result))
                    return result.ToString();
            }
            return null;
        }
    }

    /// <summary>Looks at the first bytes of a download to tell what it is.</summary>
    public static class ImageBytes
    {
        /// <summary>"png", "jpg", "gif", "bmp", "webp", "html", "svg" or null (unknown).</summary>
        public static string Detect(byte[] d)
        {
            if (d == null || d.Length < 4)
                return null;
            if (d[0] == 0x89 && d[1] == 0x50 && d[2] == 0x4E && d[3] == 0x47)
                return "png";
            if (d[0] == 0xFF && d[1] == 0xD8 && d[2] == 0xFF)
                return "jpg";
            if (d[0] == 'G' && d[1] == 'I' && d[2] == 'F' && d[3] == '8')
                return "gif";
            if (d[0] == 'B' && d[1] == 'M')
                return "bmp";
            if (d.Length > 12 && d[0] == 'R' && d[1] == 'I' && d[2] == 'F' && d[3] == 'F' && d[8] == 'W' && d[9] == 'E' && d[10] == 'B' && d[11] == 'P')
                return "webp";
            int n = Math.Min(d.Length, 600);
            string head = System.Text.Encoding.UTF8.GetString(d, 0, n).TrimStart('\uFEFF', ' ', '\r', '\n', '\t').ToLowerInvariant();
            if (head.StartsWith("<svg") || (head.StartsWith("<?xml") && head.Contains("<svg")))
                return "svg";
            if (head.StartsWith("<!doctype html") || head.StartsWith("<html") || head.Contains("<head") || head.Contains("<body"))
                return "html";
            return null;
        }
    }
}
