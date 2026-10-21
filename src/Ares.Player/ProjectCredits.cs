/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 The credits text of a project (authors and licences of the music), kept free of any window code so that it can be tested
 automatically. It has two parts: a text written by hand (the file "[project].creditos.txt", where the licence wording goes)
 and a list of the sounds of the project made from their tags (title, artist, copyright, comment).
 A packed project (.apkg) carries this text, so that the credit travels with the music.
*/
using System;
using System.Collections.Generic;
using System.Text;

namespace Ares.Player
{
    internal class CreditEntry
    {
        public string File = "";
        public string Title = "";
        public string Artist = "";
        public string Copyright = "";
        public string Comment = "";
    }

    internal static class ProjectCredits
    {
        public static string FileFor(string projectPath)
        {
            return projectPath + ".creditos.txt";
        }

        /// <summary>The credits text: the hand-written part first, then one line per sound with what its tags say.</summary>
        public static string Build(string manualText, IEnumerable<CreditEntry> entries)
        {
            StringBuilder sb = new StringBuilder();
            string manual = (manualText ?? "").Trim();
            if (manual.Length > 0)
                sb.Append(manual).Append("\r\n\r\n");
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> lines = new List<string>();
            if (entries != null)
            {
                foreach (CreditEntry e in entries)
                {
                    string line = Line(e);
                    if (line.Length > 0 && seen.Add(line))
                        lines.Add(line);
                }
            }
            if (lines.Count > 0)
            {
                sb.Append("Sons deste projeto (segundo as informações gravadas nos arquivos):\r\n");
                foreach (string line in lines)
                    sb.Append("  - ").Append(line).Append("\r\n");
            }
            return sb.ToString().TrimEnd() + (sb.Length > 0 ? "\r\n" : "");
        }

        private static string Line(CreditEntry e)
        {
            if (e == null)
                return "";
            string title = (e.Title ?? "").Trim();
            if (title.Length == 0)
                title = System.IO.Path.GetFileNameWithoutExtension(e.File ?? "");
            string artist = (e.Artist ?? "").Trim();
            string copyright = (e.Copyright ?? "").Trim();
            string comment = (e.Comment ?? "").Trim();
            // a file with no author and no licence says nothing worth listing
            if (artist.Length == 0 && copyright.Length == 0 && comment.Length == 0)
                return "";
            StringBuilder sb = new StringBuilder(title);
            if (artist.Length > 0)
                sb.Append(" - ").Append(artist);
            if (copyright.Length > 0)
                sb.Append(" [").Append(Single(copyright)).Append("]");
            if (comment.Length > 0 && !string.Equals(comment, copyright, StringComparison.OrdinalIgnoreCase))
                sb.Append(" (").Append(Single(comment)).Append(")");
            return sb.ToString();
        }

        private static string Single(string text)
        {
            string s = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return s.Length > 200 ? s.Substring(0, 200) + "..." : s;
        }

        /// <summary>The text a new credits file starts with, to be completed by hand.</summary>
        public static string Template()
        {
            return "Créditos deste projeto\r\n"
                + "\r\n"
                + "Escreva aqui, para cada artista, o nome, o site e a licença. Exemplo:\r\n"
                + "\r\n"
                + "Músicas de Kevin MacLeod (incompetech.com)\r\n"
                + "Licenciadas sob Creative Commons: By Attribution 4.0\r\n"
                + "https://creativecommons.org/licenses/by/4.0/\r\n";
        }
    }
}
