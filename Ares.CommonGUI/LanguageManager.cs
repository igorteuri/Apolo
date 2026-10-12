using System;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;

namespace Ares.CommonGUI
{
    /// <summary>User-selectable UI language (auto / pt / en / de), stored in prefs.ini.</summary>
    public static class LanguageManager
    {
        private const string Key = "Language";

        public static string Selected
        {
            get { return Preferences.Get(Key, "auto"); }
        }

        /// <summary>Call at the very start of Main, before any form is created.</summary>
        public static void Apply()
        {
            try
            {
                string lang = Selected;
                if (string.IsNullOrEmpty(lang) || lang == "auto")
                    return;
                CultureInfo ci = new CultureInfo(lang);
                Thread.CurrentThread.CurrentUICulture = ci;
                CultureInfo.DefaultThreadCurrentUICulture = ci;
            }
            catch (Exception)
            {
            }
        }

        private static bool IsPt
        {
            get { return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt"; }
        }

        /// <summary>Adds an "Idioma / Language" submenu to the given menu.</summary>
        public static void AddMenu(ToolStripMenuItem parent, int index)
        {
            ToolStripMenuItem root = new ToolStripMenuItem("Idioma / Language");
            string[,] options = new string[,]
            {
                { "auto", IsPt ? "Automático (idioma do Windows)" : "Automatic (Windows language)" },
                { "pt", "Português" },
                { "en", "English" },
                { "de", "Deutsch" }
            };
            for (int i = 0; i < options.GetLength(0); ++i)
            {
                string code = options[i, 0];
                ToolStripMenuItem item = new ToolStripMenuItem(options[i, 1]);
                item.Tag = code;
                item.Checked = (Selected == code);
                item.Click += (s, e) =>
                {
                    Preferences.Set(Key, code);
                    foreach (ToolStripItem other in root.DropDownItems)
                    {
                        ToolStripMenuItem m = other as ToolStripMenuItem;
                        if (m != null)
                            m.Checked = ((string)m.Tag == code);
                    }
                    MessageBox.Show(IsPt
                        ? "O idioma será aplicado na próxima vez que o programa for aberto."
                        : "The language will be applied the next time the program starts.",
                        "Ares", MessageBoxButtons.OK, MessageBoxIcon.Information);
                };
                root.DropDownItems.Add(item);
            }
            if (index < 0 || index > parent.DropDownItems.Count)
                index = parent.DropDownItems.Count;
            parent.DropDownItems.Insert(index, root);
        }
    }
}
