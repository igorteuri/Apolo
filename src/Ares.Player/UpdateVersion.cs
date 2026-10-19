using System;
using System.Text.RegularExpressions;

namespace Ares.Player
{
    /// <summary>
    /// Version numbers for the "new version available" notice. Releases are numbered "0.NN"; the build adds
    /// ".run.0" to it, which is ignored when comparing.
    /// </summary>
    public static class UpdateVersion
    {
        /// <summary>Reads "0.85", "v0.85", "Apolo 0.85" or "0.85.124.0" as major.minor.</summary>
        public static bool TryParse(string text, out Version version)
        {
            version = null;
            if (string.IsNullOrEmpty(text))
                return false;
            Match m = Regex.Match(text, @"(\d+)\.(\d+)");
            if (!m.Success)
                return false;
            int major, minor;
            if (!int.TryParse(m.Groups[1].Value, out major) || !int.TryParse(m.Groups[2].Value, out minor))
                return false;
            version = new Version(major, minor);
            return true;
        }

        /// <summary>true when "latest" is a higher release than "current" (anything unreadable counts as "not newer").</summary>
        public static bool IsNewer(string latest, string current)
        {
            Version a, b;
            if (!TryParse(latest, out a) || !TryParse(current, out b))
                return false;
            return a > b;
        }

        /// <summary>
        /// Reads the small public file which tells the newest version:
        /// {"version": "0.85", "url": "https://..."}. "url" is optional.
        /// </summary>
        public static bool TryReadInfo(string json, out string version, out string url)
        {
            version = null;
            url = null;
            if (string.IsNullOrEmpty(json))
                return false;
            Match v = Regex.Match(json, "\"version\"\\s*:\\s*\"([^\"]+)\"");
            if (!v.Success)
                return false;
            version = v.Groups[1].Value.Trim();
            Match u = Regex.Match(json, "\"url\"\\s*:\\s*\"([^\"]+)\"");
            if (u.Success)
                url = u.Groups[1].Value.Trim().Replace("\\/", "/");
            return version.Length > 0;
        }
    }
}
