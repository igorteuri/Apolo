using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Ares.Player
{
    /// <summary>One picture of the image bank and the words (Portuguese and English) which call for it.</summary>
    public class BankEntry
    {
        public string Id;
        public string[] Words;
        /// <summary>The name shown to the user ("Água", "Dragão"); empty = the first word.</summary>
        public string Display;

        public string Label
        {
            get
            {
                if (!string.IsNullOrEmpty(Display))
                    return Display;
                if (Words.Length == 0)
                    return Id;
                string w = Words[0];
                return char.ToUpperInvariant(w[0]) + w.Substring(1);
            }
        }
    }

    /// <summary>
    /// The index of the image bank (file "indice.txt": one line "id|word,word,..." per picture) and the choice of the
    /// picture which fits the title of a button.
    /// </summary>
    public class ImageBankIndex
    {
        public readonly List<BankEntry> Entries = new List<BankEntry>();

        /// <summary>Lower case, without accents.</summary>
        public static string Fold(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";
            string d = text.Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder();
            foreach (char c in d)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        public static ImageBankIndex Parse(string text)
        {
            ImageBankIndex index = new ImageBankIndex();
            if (string.IsNullOrEmpty(text))
                return index;
            foreach (string raw in text.Replace("\r", "").Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#')
                    continue;
                int bar = line.IndexOf('|');
                if (bar <= 0)
                    continue;
                string id = line.Substring(0, bar).Trim();
                string rest = line.Substring(bar + 1);
                // "id|words|Name": the third part (optional) is the name shown to the user
                string display = null;
                int bar2 = rest.IndexOf('|');
                if (bar2 >= 0)
                {
                    display = rest.Substring(bar2 + 1).Trim();
                    rest = rest.Substring(0, bar2);
                }
                List<string> words = new List<string>();
                foreach (string w in Fold(rest).Split(','))
                {
                    string word = w.Trim();
                    if (word.Length > 0 && !words.Contains(word))
                        words.Add(word);
                }
                if (id.Length > 0 && words.Count > 0)
                    index.Entries.Add(new BankEntry { Id = id, Words = words.ToArray(), Display = display });
            }
            return index;
        }

        private static string[] Tokens(string folded)
        {
            List<string> list = new List<string>();
            StringBuilder sb = new StringBuilder();
            foreach (char c in folded)
            {
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(c);
                }
                else if (sb.Length > 0)
                {
                    list.Add(sb.ToString());
                    sb.Length = 0;
                }
            }
            if (sb.Length > 0)
                list.Add(sb.ToString());
            return list.ToArray();
        }

        // "dragoes" / "dragao", "ratos" / "rato": the ending of the plural is dropped
        private static string Stem(string word)
        {
            if (word.Length > 4 && word.EndsWith("oes"))
                return word.Substring(0, word.Length - 3) + "ao";
            if (word.Length > 4 && word.EndsWith("es"))
                return word.Substring(0, word.Length - 2);
            if (word.Length > 3 && word.EndsWith("s"))
                return word.Substring(0, word.Length - 1);
            return word;
        }

        /// <summary>The picture for the title of a button, or null when no word fits.</summary>
        public BankEntry Match(string title)
        {
            string folded = Fold(title).Trim();
            if (folded.Length == 0)
                return null;
            string[] tokens = Tokens(folded);
            BankEntry best = null;
            int bestScore = 0;
            for (int e = 0; e < Entries.Count; e++)
            {
                BankEntry entry = Entries[e];
                for (int w = 0; w < entry.Words.Length; w++)
                {
                    string word = entry.Words[w];
                    int kind = 0;
                    int position = 0;
                    if (word == folded)
                    {
                        kind = 100;
                    }
                    else if (word.IndexOf(' ') >= 0 && folded.IndexOf(word, StringComparison.Ordinal) >= 0)
                    {
                        kind = 90;
                    }
                    else
                    {
                        for (int t = 0; t < tokens.Length; t++)
                        {
                            int k = 0;
                            if (tokens[t] == word)
                                k = 80;
                            else if (word.IndexOf(' ') < 0 && Stem(tokens[t]) == Stem(word))
                                k = 70;
                            else if (word.Length >= 5 && tokens[t].StartsWith(word, StringComparison.Ordinal))
                                k = 50;
                            if (k > kind)
                            {
                                kind = k;
                                position = t;
                            }
                        }
                    }
                    if (kind < 50)
                        continue;
                    // the better the match, the earlier it is in the title and the earlier the word is in its list, the better
                    int score = kind * 1000 - position * 20 - w;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = entry;
                    }
                }
            }
            return best;
        }

        /// <summary>The pictures which fit what the user types (a word, or the beginning of one).</summary>
        public List<BankEntry> Search(string query)
        {
            string q = Fold(query).Trim();
            List<BankEntry> result = new List<BankEntry>();
            foreach (BankEntry entry in Entries)
            {
                if (q.Length == 0)
                {
                    result.Add(entry);
                    continue;
                }
                foreach (string word in entry.Words)
                {
                    if (word.StartsWith(q, StringComparison.Ordinal) || (q.Length >= 3 && word.IndexOf(q, StringComparison.Ordinal) >= 0))
                    {
                        result.Add(entry);
                        break;
                    }
                }
            }
            return result;
        }
    }
}
