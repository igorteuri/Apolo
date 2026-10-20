using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Ares.Player
{
    /// <summary>An installed browser extension: what the program knows about it (name, icon, pages of settings).</summary>
    internal class ExtensionEntry
    {
        public string Id;
        public string Name;
        public bool Enabled;
        public string Folder;
        public string OptionsPage;   // relative to the extension, null = none
        public string PopupPage;
        public Bitmap Icon;
        public CoreWebView2BrowserExtension Extension;
    }

    /// <summary>Reads the manifest of the extensions (the folder of each one is remembered when it is installed).</summary>
    internal static class ExtensionCatalog
    {
        private static string MapFile
        {
            get { return Path.Combine(Ares.Settings.AppFolders.Root, "extensions.map"); }
        }

        private static Dictionary<string, string> ReadMap()
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (File.Exists(MapFile))
                {
                    foreach (string line in File.ReadAllLines(MapFile))
                    {
                        string[] parts = line.Split('\t');
                        if (parts.Length == 2)
                            map[parts[0]] = parts[1];
                    }
                }
            }
            catch (Exception)
            {
            }
            return map;
        }

        /// <summary>Remembers where the files of an extension are (so its pages of settings can be found).</summary>
        public static void Remember(string id, string folder)
        {
            try
            {
                Dictionary<string, string> map = ReadMap();
                map[id] = folder;
                Directory.CreateDirectory(Path.GetDirectoryName(MapFile));
                File.WriteAllLines(MapFile, map.Select(p => p.Key + "\t" + p.Value).ToArray());
            }
            catch (Exception)
            {
            }
        }

        private static IDictionary ReadJson(string file)
        {
            try
            {
                if (!File.Exists(file))
                    return null;
                string text = File.ReadAllText(file).TrimStart('﻿');
                return new JavaScriptSerializer().DeserializeObject(text) as IDictionary;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string Str(IDictionary d, string key)
        {
            return d != null && d.Contains(key) ? d[key] as string : null;
        }

        // "__MSG_name__" -> the text of the language of the extension
        private static string Localized(string folder, IDictionary manifest, string text)
        {
            if (text == null || !text.StartsWith("__MSG_", StringComparison.Ordinal) || !text.EndsWith("__", StringComparison.Ordinal))
                return text;
            string key = text.Substring(6, text.Length - 8);
            string lang = System.Globalization.CultureInfo.CurrentUICulture.Name.Replace('-', '_');
            List<string> locales = new List<string> { lang, lang.Split('_')[0], Str(manifest, "default_locale"), "en", "en_US" };
            foreach (string locale in locales)
            {
                if (string.IsNullOrEmpty(locale))
                    continue;
                IDictionary messages = ReadJson(Path.Combine(folder, "_locales", locale, "messages.json"));
                if (messages == null)
                    continue;
                foreach (object k in messages.Keys)
                {
                    if (string.Equals(k as string, key, StringComparison.OrdinalIgnoreCase))
                    {
                        IDictionary item = messages[k] as IDictionary;
                        string message = Str(item, "message");
                        if (!string.IsNullOrEmpty(message))
                            return message;
                    }
                }
            }
            return text;
        }

        private static string FindFolder(CoreWebView2BrowserExtension ext, Dictionary<string, string> map)
        {
            string folder;
            if (map.TryGetValue(ext.Id, out folder) && Directory.Exists(folder))
                return folder;
            // extensions made from .crx files were unpacked by Apolo: the one with the same name is this one
            try
            {
                string root = Path.Combine(Ares.Settings.AppFolders.Root, "Extensions");
                if (Directory.Exists(root))
                {
                    foreach (string dir in Directory.GetDirectories(root))
                    {
                        IDictionary manifest = ReadJson(Path.Combine(dir, "manifest.json"));
                        if (manifest == null)
                            continue;
                        string name = Localized(dir, manifest, Str(manifest, "name"));
                        if (string.Equals(name, ext.Name, StringComparison.OrdinalIgnoreCase))
                            return dir;
                    }
                }
            }
            catch (Exception)
            {
            }
            return null;
        }

        private static string PopupOf(IDictionary manifest)
        {
            foreach (string key in new[] { "action", "browser_action", "page_action" })
            {
                IDictionary action = manifest.Contains(key) ? manifest[key] as IDictionary : null;
                string popup = Str(action, "default_popup");
                if (!string.IsNullOrEmpty(popup))
                    return popup;
            }
            return null;
        }

        private static Bitmap IconOf(string folder, IDictionary manifest)
        {
            List<string> files = new List<string>();
            IDictionary icons = manifest.Contains("icons") ? manifest["icons"] as IDictionary : null;
            if (icons != null)
            {
                foreach (object k in icons.Keys)
                {
                    string file = icons[k] as string;
                    int size;
                    if (file != null && int.TryParse(k as string, out size))
                        files.Add(size.ToString("D4") + "|" + file);
                }
            }
            foreach (string key in new[] { "action", "browser_action", "page_action" })
            {
                IDictionary action = manifest.Contains(key) ? manifest[key] as IDictionary : null;
                if (action == null || !action.Contains("default_icon"))
                    continue;
                string single = action["default_icon"] as string;
                if (single != null)
                    files.Add("0032|" + single);
                IDictionary many = action["default_icon"] as IDictionary;
                if (many != null)
                {
                    foreach (object k in many.Keys)
                    {
                        string file = many[k] as string;
                        int size;
                        if (file != null && int.TryParse(k as string, out size))
                            files.Add(size.ToString("D4") + "|" + file);
                    }
                }
            }
            // the picture closest to 32 px
            foreach (string item in files.OrderBy(f => Math.Abs(int.Parse(f.Substring(0, 4)) - 32)))
            {
                try
                {
                    string path = Path.Combine(folder, item.Substring(5).Replace('/', Path.DirectorySeparatorChar));
                    if (!File.Exists(path))
                        continue;
                    using (FileStream fs = File.OpenRead(path))
                    using (Image img = Image.FromStream(fs))
                        return new Bitmap(img, 20, 20);
                }
                catch (Exception)
                {
                }
            }
            return null;
        }

        public static ExtensionEntry Describe(CoreWebView2BrowserExtension ext)
        {
            ExtensionEntry entry = new ExtensionEntry { Id = ext.Id, Name = ext.Name, Enabled = ext.IsEnabled, Extension = ext };
            entry.Folder = FindFolder(ext, ReadMap());
            if (entry.Folder == null)
                return entry;
            IDictionary manifest = ReadJson(Path.Combine(entry.Folder, "manifest.json"));
            if (manifest == null)
                return entry;
            entry.OptionsPage = Str(manifest, "options_page");
            if (string.IsNullOrEmpty(entry.OptionsPage) && manifest.Contains("options_ui"))
                entry.OptionsPage = Str(manifest["options_ui"] as IDictionary, "page");
            entry.PopupPage = PopupOf(manifest);
            entry.Icon = IconOf(entry.Folder, manifest);
            return entry;
        }
    }

    /// <summary>Small row of buttons, one for each installed extension, at the right of the address bar.</summary>
    internal class ExtensionBar : Panel
    {
        private readonly Func<Task<CoreWebView2Environment>> m_Environment;
        private readonly Action m_Changed;
        private readonly Action m_Manage;
        private readonly ToolTip m_Tip = new ToolTip();
        private List<ExtensionEntry> m_Entries = new List<ExtensionEntry>();

        private static string T(string pt, string en)
        {
            return System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt" ? pt : en;
        }

        public ExtensionBar(Func<Task<CoreWebView2Environment>> environment, Action changed, Action manage)
        {
            m_Environment = environment;
            m_Changed = changed;
            m_Manage = manage;
            Height = 28;
            Width = 0;
            Visible = false;
        }

        public async Task RefreshAsync(CoreWebView2 core)
        {
            try
            {
                IReadOnlyList<CoreWebView2BrowserExtension> list = await core.Profile.GetBrowserExtensionsAsync();
                List<ExtensionEntry> entries = new List<ExtensionEntry>();
                foreach (CoreWebView2BrowserExtension ext in list)
                    entries.Add(ExtensionCatalog.Describe(ext));
                m_Entries = entries;
                Rebuild();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }

        private void Rebuild()
        {
            SuspendLayout();
            foreach (Control old in Controls.Cast<Control>().ToList())
            {
                Controls.Remove(old);
                old.Dispose();
            }
            int x = 0;
            foreach (ExtensionEntry entry in m_Entries)
            {
                ExtensionEntry captured = entry;
                Button b = new Button();
                b.SetBounds(x, 0, 28, 28);
                b.TabStop = true;
                b.Cursor = Cursors.Hand;
                if (entry.Icon != null)
                    b.Image = entry.Icon;
                else
                    b.Text = string.IsNullOrEmpty(entry.Name) ? "?" : entry.Name.Substring(0, 1).ToUpperInvariant();
                b.Padding = new Padding(0);
                b.Click += (s, e) => ShowMenu((Control)s, captured);
                m_Tip.SetToolTip(b, entry.Name + (entry.Enabled ? "" : T(" (desativada)", " (disabled)")));
                Controls.Add(b);
                x += 31;
            }
            Width = Math.Max(0, x);
            Visible = m_Entries.Count > 0;
            ResumeLayout(true);
            Action changed = m_Changed;
            if (changed != null)
                changed();
        }

        private void ShowMenu(Control anchor, ExtensionEntry entry)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            ToolStripItem title = menu.Items.Add(entry.Name);
            title.Enabled = false;
            menu.Items.Add(new ToolStripSeparator());
            ToolStripItem options = menu.Items.Add(T("Configurações da extensão", "Extension settings"), null,
                (s, e) => OpenPage(entry, entry.OptionsPage, true));
            options.Enabled = entry.OptionsPage != null && entry.Enabled;
            if (entry.OptionsPage == null)
                options.ToolTipText = T("Esta extensão não tem página de configurações.", "This extension has no settings page.");
            ToolStripItem popup = menu.Items.Add(T("Abrir a janela da extensão", "Open the extension window"), null,
                (s, e) => OpenPage(entry, entry.PopupPage, false));
            popup.Enabled = entry.PopupPage != null && entry.Enabled;
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(entry.Enabled ? T("Desativar", "Disable") : T("Ativar", "Enable"), null, async (s, e) =>
            {
                try
                {
                    await entry.Extension.EnableAsync(!entry.Enabled);
                    entry.Enabled = !entry.Enabled;
                    Rebuild();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(FindForm(), ex.Message, "Apolo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            });
            menu.Items.Add(T("Remover", "Remove"), null, async (s, e) =>
            {
                if (MessageBox.Show(FindForm(), T("Remover a extensão \"" + entry.Name + "\"?", "Remove the extension \"" + entry.Name + "\"?"), "Apolo",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;
                try
                {
                    await entry.Extension.RemoveAsync();
                    m_Entries.Remove(entry);
                    Rebuild();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(FindForm(), ex.Message, "Apolo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(T("Gerenciar extensões...", "Manage extensions..."), null, (s, e) =>
            {
                Action manage = m_Manage;
                if (manage != null)
                    manage();
            });
            Ares.CommonGUI.ThemeManager.Apply(menu);
            menu.Show(anchor, new Point(0, anchor.Height));
        }

        /// <summary>Opens a page of the extension (settings or window) in a window of its own, with the same browser profile.</summary>
        public void OpenPage(ExtensionEntry entry, string page, bool settings)
        {
            if (string.IsNullOrEmpty(page))
                return;
            string url = "chrome-extension://" + entry.Id + "/" + page.TrimStart('/');
            ExtensionPageWindow.Open(FindForm(), (settings ? T("Configurações: ", "Settings: ") : "") + entry.Name, url, m_Environment, settings ? new Size(900, 700) : new Size(420, 640));
        }
    }

    /// <summary>A window which shows a page of an extension (its settings, or its small window).</summary>
    internal class ExtensionPageWindow : Form
    {
        private readonly WebView2 m_View = new WebView2();
        private readonly Label m_Message = new Label();

        public static void Open(IWin32Window owner, string title, string url, Func<Task<CoreWebView2Environment>> environment, Size size)
        {
            ExtensionPageWindow window = new ExtensionPageWindow(title, url, environment, size);
            window.Show(owner);
        }

        private static string T(string pt, string en)
        {
            return System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt" ? pt : en;
        }

        private ExtensionPageWindow(string title, string url, Func<Task<CoreWebView2Environment>> environment, Size size)
        {
            Text = title;
            ClientSize = size;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            m_View.Dock = DockStyle.Fill;
            Controls.Add(m_View);
            m_Message.Dock = DockStyle.Top;
            m_Message.Height = 40;
            m_Message.Padding = new Padding(8);
            m_Message.Visible = false;
            Controls.Add(m_Message);
            Shown += async (s, e) =>
            {
                try
                {
                    await m_View.EnsureCoreWebView2Async(await environment());
                    CoreWebView2 core = m_View.CoreWebView2;
                    core.NewWindowRequested += (s2, e2) =>
                    {
                        e2.Handled = true;
                        core.Navigate(e2.Uri);
                    };
                    core.NavigationCompleted += (s2, e2) =>
                    {
                        if (!e2.IsSuccess)
                        {
                            m_Message.Text = T("Não foi possível abrir esta página da extensão.", "This page of the extension could not be opened.");
                            m_Message.Visible = true;
                        }
                    };
                    core.Navigate(url);
                }
                catch (Exception ex)
                {
                    m_Message.Text = ex.Message;
                    m_Message.Visible = true;
                }
            };
        }
    }
}
