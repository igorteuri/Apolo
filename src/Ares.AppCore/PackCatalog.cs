/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 The list of ready-made content packs (packs.ini) that Apolo can download, kept free of any window code so that it can be
 tested automatically. Each pack is a section of a plain text file:

   [pacote-inicial]
   titulo = Pacote inicial
   descricao = Pastas e botões prontos para começar.
   arquivo = pacote-inicial.apkg
   bytes = 123456789
   sha256 = 0123...abcd
*/
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Ares.Player
{
    public class PackInfo
    {
        public string Id = "";
        public string Title = "";
        public string Description = "";
        /// <summary>Full address of the .apkg (made from the "arquivo" line and the address of the list).</summary>
        public string Url = "";
        public long Bytes;
        /// <summary>Optional check of the download (hex, any case).</summary>
        public string Sha256 = "";
    }

    public static class PackCatalog
    {
        /// <summary>Reads the list. Packs without a title or a usable file are left out.</summary>
        public static List<PackInfo> Parse(string text, string catalogUrl)
        {
            List<PackInfo> result = new List<PackInfo>();
            if (string.IsNullOrEmpty(text))
                return result;
            PackInfo current = null;
            Dictionary<string, string> values = null;
            foreach (string raw in text.Replace("\r", "").Split('\n'))
            {
                string line = raw.Trim().TrimStart('﻿');
                if (line.Length == 0 || line[0] == '#' || line[0] == ';')
                    continue;
                if (line[0] == '[' && line[line.Length - 1] == ']')
                {
                    Finish(result, current, values, catalogUrl);
                    current = new PackInfo();
                    current.Id = line.Substring(1, line.Length - 2).Trim();
                    values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    continue;
                }
                int eq = line.IndexOf('=');
                if (eq <= 0 || values == null)
                    continue;
                values[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
            }
            Finish(result, current, values, catalogUrl);
            return result;
        }

        private static string Get(Dictionary<string, string> values, string key)
        {
            string v;
            return values.TryGetValue(key, out v) ? v : "";
        }

        private static void Finish(List<PackInfo> result, PackInfo pack, Dictionary<string, string> values, string catalogUrl)
        {
            if (pack == null || values == null)
                return;
            pack.Title = Get(values, "titulo");
            if (pack.Title.Length == 0)
                pack.Title = Get(values, "title");
            pack.Description = Get(values, "descricao");
            if (pack.Description.Length == 0)
                pack.Description = Get(values, "description");
            string file = Get(values, "arquivo");
            if (file.Length == 0)
                file = Get(values, "file");
            pack.Url = ResolveUrl(file, catalogUrl);
            long bytes;
            if (long.TryParse(Get(values, "bytes"), NumberStyles.Integer, CultureInfo.InvariantCulture, out bytes) && bytes > 0)
                pack.Bytes = bytes;
            pack.Sha256 = Get(values, "sha256").Replace(" ", "");
            if (pack.Title.Length > 0 && pack.Url.Length > 0)
                result.Add(pack);
        }

        /// <summary>The address of a pack file: a full https address as it is, a bare name beside the list. Anything else is empty.</summary>
        public static string ResolveUrl(string file, string catalogUrl)
        {
            if (string.IsNullOrEmpty(file))
                return "";
            if (file.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return file;
            if (file.IndexOf(':') >= 0 || file.IndexOf('/') >= 0 || file.IndexOf('\\') >= 0 || file.Contains(".."))
                return "";
            int slash = (catalogUrl ?? "").LastIndexOf('/');
            if (slash < 0)
                return "";
            return catalogUrl.Substring(0, slash + 1) + Uri.EscapeDataString(file);
        }

        /// <summary>A file name for the pack on this computer (letters, digits, dash).</summary>
        public static string SafeName(string id)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (char c in id ?? "")
                sb.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '-');
            string name = sb.ToString().Trim('-');
            return name.Length == 0 ? "pacote" : name;
        }

        public static string FormatSize(long bytes)
        {
            if (bytes <= 0)
                return "";
            if (bytes < 1024 * 1024)
                return Math.Max(1, bytes / 1024).ToString(CultureInfo.InvariantCulture) + " KB";
            double mb = bytes / (1024.0 * 1024.0);
            if (mb < 1024)
                return mb.ToString(mb < 10 ? "0.0" : "0", CultureInfo.InvariantCulture) + " MB";
            return (mb / 1024.0).ToString("0.0", CultureInfo.InvariantCulture) + " GB";
        }

        /// <summary>Checks a downloaded pack (size and signature); null when it looks right, otherwise a message for the person.</summary>
        public static string Verify(string file, PackInfo pack, bool portuguese)
        {
            try
            {
                long length = new FileInfo(file).Length;
                if (length < 1024)
                    return portuguese ? "O arquivo baixado está incompleto." : "The downloaded file is incomplete.";
                if (pack.Bytes > 0 && length != pack.Bytes)
                    return portuguese ? "O arquivo baixado não tem o tamanho esperado. Tente de novo." : "The downloaded file does not have the expected size. Try again.";
                if (pack.Sha256.Length > 0)
                {
                    using (FileStream fs = File.OpenRead(file))
                    using (System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create())
                    {
                        System.Text.StringBuilder hex = new System.Text.StringBuilder();
                        foreach (byte b in sha.ComputeHash(fs))
                            hex.Append(b.ToString("x2"));
                        if (!string.Equals(hex.ToString(), pack.Sha256, System.StringComparison.OrdinalIgnoreCase))
                            return portuguese ? "O arquivo baixado não confere com o esperado (assinatura diferente). Tente de novo."
                                : "The downloaded file does not match what is expected (different signature). Try again.";
                    }
                }
                return null;
            }
            catch (System.Exception ex)
            {
                return ex.Message;
            }
        }
    }
}
