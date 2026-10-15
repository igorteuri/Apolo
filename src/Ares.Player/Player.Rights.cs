/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 The "rights" traffic light of the track list: green / yellow / red, only an INDICATION (never a guarantee) of how safe
 a music file is for streams and videos.
   - A quick guess from the tags and the file name (free licence mentioned => green, non-commercial => yellow).
   - The check on the internet (AcoustID + MusicBrainz, the same one of the copyright checker in Extras): a commercial
     release of a big label => red, other commercial release or not found => yellow. The result is kept.
*/
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Ares.Tags;
using Ares.TagsImport;

namespace Ares.Player
{
    internal static class RightsCheck
    {
        private static readonly Regex s_Free = new Regex(
            @"creative\s*commons|\bcc[\s\-]?by\b|\bcc0\b|public\s*domain|dom[ií]nio\s*p[úu]blico|royalty[\s\-]?free|sem\s*direitos|no\s*copyright|copyright[\s\-]?free|free\s*to\s*use|licen[cç]a\s*livre|incompetech|kevin\s*macleod|free\s*music\s*archive|pixabay|freesound|musopen|youtube\s*audio\s*library|biblioteca\s*de\s*[áa]udio|uppbeat|bensound|jamendo",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex s_NonCommercial = new Regex(@"\bby[\s\-]?nc\b|non[\s\-]?commercial|n[ãa]o\s*comercial", RegexOptions.IgnoreCase);
        private static readonly Regex s_Major = new Regex(
            @"universal|\bumg\b|sony|columbia|\brca\b|\bepic\b|warner|\bwmg\b|atlantic|elektra|parlophone|\bemi\b|capitol|virgin|island|interscope|def\s*jam|republic|polydor|geffen|\ba&m\b|motown|\bbmg\b|arista|mercury|decca|deutsche\s*grammophon|som\s*livre|rhino|sbk|roc nation|roadrunner",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Dictionary<string, string[]> s_Cache = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        private static bool s_Loaded;

        private static string T(string pt, string en)
        {
            return System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt" ? pt : en;
        }

        private static string FilePath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Path.Combine("Apolo", "rights.txt")); }
        }

        private static void Load()
        {
            if (s_Loaded)
                return;
            s_Loaded = true;
            try
            {
                if (!File.Exists(FilePath))
                    return;
                foreach (string line in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    string[] parts = line.Split('\t');
                    if (parts.Length >= 3 && parts[1].Length == 1)
                        s_Cache[parts[0]] = new string[] { parts[1], parts[2] };
                }
            }
            catch (Exception)
            {
            }
        }

        /// <summary>The result of an earlier check on the internet, if there is one.</summary>
        public static bool TryGetChecked(string path, out char code, out string why)
        {
            code = '\0';
            why = "";
            lock (s_Cache)
            {
                Load();
                string[] v;
                if (!s_Cache.TryGetValue(path, out v))
                    return false;
                code = v[0][0];
                why = v[1];
                return true;
            }
        }

        private static void Store(string path, char code, string why)
        {
            lock (s_Cache)
            {
                Load();
                s_Cache[path] = new string[] { code.ToString(), why.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ') };
                try
                {
                    string file = FilePath;
                    Directory.CreateDirectory(Path.GetDirectoryName(file));
                    List<string> lines = new List<string>();
                    foreach (KeyValuePair<string, string[]> pair in s_Cache)
                        lines.Add(pair.Key + "\t" + pair.Value[0] + "\t" + pair.Value[1]);
                    File.WriteAllLines(file, lines.ToArray(), Encoding.UTF8);
                }
                catch (Exception)
                {
                }
            }
        }

        private static string TagText(AudioTagInfo tag, string path)
        {
            if (tag == null)
                tag = new AudioTagInfo();
            return tag.copyright + " " + tag.comment + " " + tag.title + " " + tag.artist + " " + tag.album + " " + Path.GetFileNameWithoutExtension(path);
        }

        /// <summary>Quick guess without internet: 'G' or 'Y' when the tags / name mention a free licence, else '\0'.</summary>
        public static void Quick(AudioTagInfo tag, string path, out char code, out string why)
        {
            code = '\0';
            why = "";
            string text = TagText(tag, path);
            Match free = s_Free.Match(text);
            if (!free.Success)
                return;
            if (s_NonCommercial.IsMatch(text))
            {
                code = 'Y';
                why = T("Licença livre indicada (\"" + free.Value + "\"), mas só para uso NÃO comercial.", "Free licence mentioned (\"" + free.Value + "\"), but for NON-commercial use only.");
            }
            else
            {
                code = 'G';
                why = T("Licença livre indicada nas tags ou no nome (\"" + free.Value + "\"). Confira se pede crédito ao autor.",
                    "Free licence mentioned in the tags or name (\"" + free.Value + "\"). Check whether it asks for credit to the author.");
            }
        }

        /// <summary>The check on the internet (takes some seconds); the result is kept for the next time.</summary>
        public static void Full(string path, AudioTagInfo tag, out char code, out string why)
        {
            Quick(tag, path, out code, out why);
            if (code == '\0')
            {
                MusicLookupResult r = MusicLookup.Identify(path, true);
                if (r.Identified)
                {
                    if (!string.IsNullOrEmpty(r.Label) && s_Major.IsMatch(r.Label))
                    {
                        code = 'R';
                        why = T("Lançamento comercial de selo grande: alto risco de bloqueio ou claim em lives e vídeos.",
                            "Commercial release of a major label: high risk of a block or claim on streams and videos.");
                    }
                    else
                    {
                        code = 'Y';
                        why = T("Lançamento comercial" + (string.IsNullOrEmpty(r.Label) ? "" : " (selo independente ou desconhecido)") + ": risco médio, confira a licença antes de usar.",
                            "Commercial release" + (string.IsNullOrEmpty(r.Label) ? "" : " (independent or unknown label)") + ": medium risk, check the licence before using it.");
                    }
                }
                else if (!string.IsNullOrEmpty(r.Error))
                {
                    // no answer from the internet: nothing is kept, so that it can be tried again
                    code = '\0';
                    why = T("Não consegui consultar a internet: ", "Could not reach the internet: ") + r.Error;
                    return;
                }
                else
                {
                    code = 'Y';
                    why = T("Não encontrei na base MusicBrainz. Pode ser música independente, original ou de biblioteca livre: confira de onde veio o arquivo.",
                        "Not found in MusicBrainz. It can be independent, original or from a free library: check where the file came from.");
                }
            }
            Store(path, code, why);
        }

        public static string Label(char code)
        {
            switch (code)
            {
                case 'G': return "\u25CF " + T("Verde", "Green");
                case 'Y': return "\u25CF " + T("Amarelo", "Yellow");
                case 'R': return "\u25CF " + T("Vermelho", "Red");
                default: return "\u25CB " + T("não verificado", "not checked");
            }
        }

        public static System.Drawing.Color ColorOf(char code, System.Drawing.Color normal)
        {
            switch (code)
            {
                case 'G': return System.Drawing.Color.FromArgb(70, 190, 90);
                case 'Y': return System.Drawing.Color.FromArgb(235, 190, 40);
                case 'R': return System.Drawing.Color.FromArgb(225, 70, 70);
                default: return normal;
            }
        }

        public static int Rank(char code)
        {
            return code == 'G' ? 0 : (code == 'Y' ? 1 : (code == 'R' ? 2 : 3));
        }
    }
}
