using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Ares.CommonGUI;

namespace Ares.Editor
{
    /// <summary>
    /// Modern (Lucide) icons for the toolbars, menus and trees of the Editor - the same icon set as the player.
    /// Icons are looked up by the name of the toolbar / menu item.
    /// </summary>
    internal static class EditorIcons
    {
        private static readonly Dictionary<string, string> s_Names = new Dictionary<string, string>
        {
            // file
            { "newButton", "file-plus" }, { "openButton", "folder-open" }, { "saveButton", "save" },
            { "undoButton", "undo-2" }, { "redoButton", "redo-2" },
            { "newToolStripMenuItem", "file-plus" }, { "openToolStripMenuItem", "folder-open" },
            { "importToolStripMenuItem", "download" }, { "importToolStripMenuItem1", "download" },
            { "importToolStripMenuItem2", "download" }, { "importToolStripMenuItem4", "download" },
            { "saveToolStripMenuItem", "save" }, { "saveAsToolStripMenuItem", "copy" },
            { "exportToolStripMenuItem", "upload" }, { "exportToolStripMenuItem1", "upload" },
            { "exportToolStripMenuItem2", "upload" }, { "exportToolStripMenuItem3", "upload" },
            { "recentMenuItem", "clock" }, { "exitToolStripMenuItem", "log-out" },
            // edit
            { "undoToolStripMenuItem", "undo-2" }, { "redoToolStripMenuItem", "redo-2" },
            { "editToolStripMenuItem", "pencil" }, { "editToolStripMenuItem1", "pencil" },
            { "renameToolStripMenuItem", "pencil-line" }, { "renameToolStripMenuItem1", "pencil-line" },
            { "renameToolStripMenuItem2", "pencil-line" }, { "renameToolStripMenuItem3", "pencil-line" },
            { "renameToolStripMenuItem4", "pencil-line" },
            { "deleteToolStripMenuItem", "trash" }, { "deleteToolStripMenuItem1", "trash" },
            { "deleteToolStripMenuItem2", "trash" }, { "deleteToolStripMenuItem3", "trash" },
            { "toolStripMenuItem4", "scissors" }, { "toolStripMenuItem6", "scissors" },
            { "toolStripMenuItem9", "scissors" }, { "toolStripMenuItem11", "scissors" },
            { "copyToolStripMenuItem", "copy" }, { "copyToolStripMenuItem1", "copy" },
            { "toolStripMenuItem5", "copy" }, { "toolStripMenuItem7", "copy" },
            { "toolStripMenuItem10", "copy" }, { "toolStripMenuItem12", "copy" },
            { "pasteToolStripMenuItem", "clipboard-paste" }, { "toolStripMenuItem3", "clipboard-paste" },
            { "toolStripMenuItem8", "clipboard-paste" }, { "toolStripMenuItem13", "clipboard-paste" },
            { "toolStripMenuItem15", "link" }, { "toolStripMenuItem16", "link" },
            // add
            { "toolStripMenuItem1", "folder-plus" },
            { "addScenarioToolStripMenuItem", "folder-plus" },
            { "addMusicByTagsToolStripMenuItem", "tags" }, { "musicByTagsToolStripItem", "tags" },
            { "addRandomPlaylistToolStripMenuItem", "shuffle" }, { "addRandomPlaylistToolStripMenuItem1", "shuffle" },
            { "addSequentialPlaylistToolStripMenuItem", "list-ordered" }, { "addSequentialPlaylistToolStripMenuItem1", "list-ordered" },
            { "addBackgroundSoundsToolStripMenuItem", "audio-lines" }, { "addBackgroundSoundsToolStripMenuItem1", "audio-lines" },
            { "webRadioMenuItem", "radio" }, { "webRadioItem2", "radio" },
            { "toolStripMenuItem14", "wand-sparkles" },
            { "addParallelElementToolStripMenuItem", "layers" }, { "addParallelElementToolStripMenuItem1", "layers" },
            { "addSequentialElementToolStripMenuItem", "list-ordered" }, { "addSequentialElementToolStripMenuItem1", "list-ordered" },
            { "addChoiceListToolStripMenuItem", "shuffle" }, { "addChoiceListToolStripMenuItem1", "shuffle" },
            { "addSoundChoiceToolStripMenuItem", "dices" },
            // keys, sorting, starting
            { "toolStripMenuItem2", "keyboard" }, { "selectContainerKeyMenuItem", "keyboard" },
            { "selectElementKeyMenuItem", "keyboard" }, { "selectBGSoundsKeyMenuItem", "keyboard" },
            { "setKeyButton", "keyboard" },
            { "sortModesToolStripMenuItem", "arrow-up-down" }, { "toolStripMenuItem17", "arrow-up-down" },
            { "sortModesByNameAscMenuItem", "arrow-down" }, { "sortModesByNameDescMenuItem", "arrow-up" },
            { "sortModesByKeyAscMenuItem", "arrow-down" }, { "sortModesByKeyDescMenuItem", "arrow-up" },
            { "sortElementsByNameAscMenuItem", "arrow-down" }, { "sortElementsByNameDescMenuItem", "arrow-up" },
            { "sortElementsByKeyAscMenuItem", "arrow-down" }, { "sortElementsByKeyDescMenuItem", "arrow-up" },
            { "modeElementStartingToolStripMenuItem", "play" }, { "modeElementStartingToolStripMenuItem1", "play" },
            { "modeElementStartingToolStripMenuItem2", "play" },
            { "moveUpButton", "arrow-up" }, { "moveDownButton", "arrow-down" },
            // player controls
            { "playButton", "play" }, { "stopButton", "square" },
            { "playToolStripMenuItem", "play" }, { "stopToolStripMenuItem", "square" },
            { "startPlayerToolStripMenuItem", "play" }, { "stopAllToolStipMenuItem", "square" },
            // search, tags, files
            { "refreshButton", "refresh-cw" }, { "searchButton", "search" }, { "showInfoButton", "info" },
            { "tagButton", "tag" }, { "tagFilterButton", "tags" }, { "projectFilterButton", "funnel" },
            { "prevPageButton", "chevron-left" }, { "nextPageButton", "chevron-right" },
            { "searchSimilarItem", "search" }, { "audioSourceSearchToolStripMenuItem", "search" },
            { "downloadMenuItem", "download" }, { "downloadToMenuItem", "folder-open" },
            { "editTagsMenuItem", "tags" }, { "tagsToolStripMenuItem", "tags" }, { "tagsMenuItem", "tags" },
            // view, tools, help
            { "projectExplorerToolStripMenuItem", "list-tree" }, { "fileExplorerToolStripMenuItem", "folder-tree" },
            { "soundFileExplorerToolStripMenuItem", "audio-lines" }, { "projectErrorsToolStripMenuItem", "triangle-alert" },
            { "volumesToolStripMenuItem", "volume-2" }, { "usedFilesToolStripMenuItem", "files" },
            { "usedKeysToolStripMenuItem", "keyboard" },
            { "settingsToolStripMenuItem", "settings" }, { "showTipsMenuItem", "lightbulb" },
            { "helpOnlineToolStripMenuItem", "circle-question-mark" }, { "checkForUpdateToolStripMenuItem", "refresh-cw" },
            { "aboutToolStripMenuItem", "info" }
        };

        /// <summary>Icons for the buttons of a toolbar.</summary>
        public static void Toolbar(ToolStrip strip)
        {
            if (strip == null)
                return;
            var items = new Dictionary<ToolStripItem, string>();
            foreach (ToolStripItem item in strip.Items)
            {
                string icon;
                if (item.Name != null && s_Names.TryGetValue(item.Name, out icon))
                    items[item] = icon;
            }
            IconLibrary.Bind(strip, items);
        }

        /// <summary>Icons for the entries of the drop-downs of a menu bar.</summary>
        public static void Menu(ToolStrip menu)
        {
            if (menu != null)
                IconLibrary.BindMenu(menu, s_Names, false);
        }

        /// <summary>Icons for the entries of a context menu.</summary>
        public static void ContextMenu(ContextMenuStrip menu)
        {
            if (menu != null)
                IconLibrary.BindMenu(menu, s_Names, true);
        }

        // ---------------------------------------------------------------------------------
        // Docked windows: theme + icons
        // ---------------------------------------------------------------------------------

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, object> s_Polished =
            new System.Runtime.CompilerServices.ConditionalWeakTable<object, object>();

        private static bool FirstTime(object o)
        {
            object dummy;
            if (s_Polished.TryGetValue(o, out dummy))
                return false;
            s_Polished.Add(o, o);
            return true;
        }

        /// <summary>
        /// Gives a docked window (tool window or element editor) the current look: theme, toolbar / menu icons, play and stop buttons.
        /// Safe to call several times.
        /// </summary>
        public static void Polish(Form form)
        {
            if (form == null || form.IsDisposed)
                return;
            try
            {
                ThemeManager.Apply(form);
                if (FirstTime(form))
                {
                    // hidden windows are not part of the control tree, so they have to follow theme changes themselves
                    ThemeManager.Changed += (s, e) =>
                    {
                        try
                        {
                            if (!form.IsDisposed)
                                ThemeManager.Apply(form);
                        }
                        catch (Exception) { }
                    };
                    foreach (ToolStrip strip in FindControls<ToolStrip>(form))
                    {
                        if (strip is MenuStrip || strip is ContextMenuStrip || strip is StatusStrip)
                            continue;
                        Toolbar(strip);
                    }
                    var seen = new HashSet<ContextMenuStrip>();
                    foreach (Control c in FindControls<Control>(form))
                    {
                        if (c.ContextMenuStrip != null && seen.Add(c.ContextMenuStrip))
                            ContextMenu(c.ContextMenuStrip);
                    }
                    for (Type t = form.GetType(); t != null && t != typeof(object); t = t.BaseType)
                    {
                        foreach (System.Reflection.FieldInfo fi in t.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.DeclaredOnly))
                        {
                            if (typeof(ContextMenuStrip).IsAssignableFrom(fi.FieldType))
                            {
                                ContextMenuStrip menu = fi.GetValue(form) as ContextMenuStrip;
                                if (menu != null && seen.Add(menu))
                                    ContextMenu(menu);
                            }
                        }
                    }
                    foreach (Button b in FindControls<Button>(form))
                    {
                        string icon;
                        if (b.Image != null && (b.Name == "playButton" || b.Name == "stopButton") && s_Names.TryGetValue(b.Name, out icon))
                            BindButton(b, icon);
                    }
                }
            }
            catch (Exception ex)
            {
                ThemeManager.Log("polish " + form.GetType().Name, ex);
            }
        }

        private static void BindButton(Button b, string icon)
        {
            Action apply = () =>
            {
                Size size = IconLibrary.MenuIconSize();
                Bitmap bmp = IconLibrary.Get(icon, size);
                if (bmp == null)
                    return;
                Image old = b.Image;
                b.ImageAlign = ContentAlignment.MiddleCenter;
                b.Image = bmp;
                if (old != null)
                    old.Dispose();
            };
            apply();
            ThemeManager.Changed += (s, e) =>
            {
                try
                {
                    if (!b.IsDisposed)
                        apply();
                }
                catch (Exception) { }
            };
        }

        private static List<T> FindControls<T>(Control root) where T : Control
        {
            var result = new List<T>();
            var stack = new Stack<Control>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                Control c = stack.Pop();
                T t = c as T;
                if (t != null)
                    result.Add(t);
                foreach (Control child in c.Controls)
                    stack.Push(child);
            }
            return result;
        }

        // ---------------------------------------------------------------------------------
        // Icons for trees and lists
        // ---------------------------------------------------------------------------------

        // colors of the icons
        public const int Normal = 0;
        public const int Accent = 1;
        public const int Warm = 2;
        public const int Dim = 3;
        public const int Error = 4;
        public const int Warning = 5;

        private static Bitmap s_ErrorIcon, s_WarningIcon;
        private static bool s_StatusHooked;

        /// <summary>Icon for the lines of the message list (cached; do not dispose).</summary>
        public static Bitmap StatusIcon(bool error)
        {
            if (!s_StatusHooked)
            {
                s_StatusHooked = true;
                ThemeManager.Changed += (s, e) => { s_ErrorIcon = null; s_WarningIcon = null; };
            }
            Size size = IconLibrary.MenuIconSize();
            if (s_ErrorIcon == null || s_ErrorIcon.Width != size.Width)
            {
                s_ErrorIcon = IconLibrary.Get("circle-x", size, ColorOf(Error));
                s_WarningIcon = IconLibrary.Get("triangle-alert", size, ColorOf(Warning));
            }
            return error ? s_ErrorIcon : s_WarningIcon;
        }

        public sealed class Spec
        {
            public string Name;
            public int Color;
            public Spec(string name, int color) { Name = name; Color = color; }
        }

        private static Color ColorOf(int kind)
        {
            ThemePalette p = ThemeManager.Current;
            switch (kind)
            {
                case Accent: return p != null && !p.IsClassic ? p.Accent : Color.FromArgb(0x2B, 0x6C, 0xB0);
                case Warm: return Color.FromArgb(0xE0, 0x9A, 0x3E);
                case Dim: return p != null && !p.IsClassic ? p.TextDim : Color.Gray;
                case Error: return Color.FromArgb(0xE5, 0x5B, 0x5B);
                case Warning: return Color.FromArgb(0xE8, 0xB3, 0x3D);
                default: return IconLibrary.ThemeColor();
            }
        }

        public static void Fill(ImageList list, IList<Spec> specs)
        {
            Size size = IconLibrary.MenuIconSize();
            list.ColorDepth = ColorDepth.Depth32Bit;
            list.Images.Clear();
            list.ImageSize = size;
            foreach (Spec spec in specs)
            {
                Bitmap bmp = IconLibrary.Get(spec.Name, size, ColorOf(spec.Color));
                if (bmp == null)
                    bmp = new Bitmap(size.Width, size.Height);
                // the list keeps a reference to the bitmap (it must not be disposed here)
                list.Images.Add(bmp);
            }
        }

        /// <summary>Fills the list now and again whenever the theme changes.</summary>
        public static void Track(ImageList list, IList<Spec> specs)
        {
            Fill(list, specs);
            ThemeManager.Changed += (s, e) =>
            {
                try { Fill(list, specs); }
                catch (Exception) { }
            };
        }
    }
}
