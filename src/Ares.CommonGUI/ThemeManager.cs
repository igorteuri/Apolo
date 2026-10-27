/*
 Copyright (c) 2026 [Ares fork]

 This file is part of Ares.

 Ares is free software; you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation; either version 2 of the License, or
 (at your option) any later version.

 Ares is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 GNU General Public License for more details.
*/
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Ares.CommonGUI
{
    /// <summary>
    /// A color palette for the user interface.
    /// </summary>
    public sealed class ThemePalette
    {
        public string Id;
        public string NamePt;
        public string NameEn;
        public bool IsClassic;   // no changes to the system look
        public bool IsDark;
        public Color Window;     // main background
        public Color Surface;    // inputs, lists, buttons
        public Color Hover;      // hover / pressed
        public Color Border;
        public Color Text;
        public Color TextDim;
        public Color Accent;
        public Color Selection;

        /// <summary>The border of controls (buttons, cards): at least 3:1 against the window, so that they can be seen (WCAG 1.4.11).</summary>
        public Color ControlBorder
        {
            get { return ThemeManager.EnsureContrast(Border, Window, 3.0); }
        }

        public string DisplayName
        {
            get
            {
                string lang = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                return lang == "pt" ? NamePt : NameEn;
            }
        }

        internal static Color C(int rgb)
        {
            return Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
        }
    }

    /// <summary>
    /// Central place for the look of the application: color themes and interface size.
    /// Themes are applied to all open forms (and to every control added later) at runtime.
    /// </summary>
    public static class ThemeManager
    {
        private static readonly List<ThemePalette> s_Palettes = new List<ThemePalette>
        {
            new ThemePalette
            {
                Id = "apolo", NamePt = "Apolo (escuro)", NameEn = "Apolo (dark)", IsDark = true,
                Window = ThemePalette.C(0x16130F), Surface = ThemePalette.C(0x221E19), Hover = ThemePalette.C(0x352E26),
                Border = ThemePalette.C(0x3D362D), Text = ThemePalette.C(0xF2ECE4), TextDim = ThemePalette.C(0x9A9186),
                Accent = ThemePalette.C(0xFFB627), Selection = ThemePalette.C(0x6B4A12)
            },
            new ThemePalette
            {
                Id = "apolo-light", NamePt = "Apolo (claro)", NameEn = "Apolo (light)", IsDark = false,
                Window = ThemePalette.C(0xFAF7F2), Surface = ThemePalette.C(0xFFFFFF), Hover = ThemePalette.C(0xF0E9DD),
                Border = ThemePalette.C(0xE0D6C6), Text = ThemePalette.C(0x2A2118), TextDim = ThemePalette.C(0x7A6E60),
                Accent = ThemePalette.C(0xE8890C), Selection = ThemePalette.C(0xFFDDA0)
            },
            new ThemePalette
            {
                Id = "dark", NamePt = "Escuro", NameEn = "Dark", IsDark = true,
                Window = ThemePalette.C(0x1B1D21), Surface = ThemePalette.C(0x272A30), Hover = ThemePalette.C(0x3A3F48),
                Border = ThemePalette.C(0x3B4049), Text = ThemePalette.C(0xE6E8EB), TextDim = ThemePalette.C(0x8A9099),
                Accent = ThemePalette.C(0x4C9AFF), Selection = ThemePalette.C(0x2F5D9E)
            },
            new ThemePalette
            {
                Id = "midnight", NamePt = "Meia-noite", NameEn = "Midnight", IsDark = true,
                Window = ThemePalette.C(0x0F1420), Surface = ThemePalette.C(0x182033), Hover = ThemePalette.C(0x26365A),
                Border = ThemePalette.C(0x2F3F63), Text = ThemePalette.C(0xE3EAF7), TextDim = ThemePalette.C(0x7F8CA8),
                Accent = ThemePalette.C(0x6EA8FE), Selection = ThemePalette.C(0x2A4A86)
            },
            new ThemePalette
            {
                Id = "violet", NamePt = "Violeta", NameEn = "Violet", IsDark = true,
                Window = ThemePalette.C(0x17121F), Surface = ThemePalette.C(0x211A2E), Hover = ThemePalette.C(0x33284A),
                Border = ThemePalette.C(0x43365E), Text = ThemePalette.C(0xEAE4F5), TextDim = ThemePalette.C(0x9187A8),
                Accent = ThemePalette.C(0xB488FF), Selection = ThemePalette.C(0x5A3E96)
            },
            new ThemePalette
            {
                Id = "light", NamePt = "Claro moderno", NameEn = "Modern light", IsDark = false,
                Window = ThemePalette.C(0xF5F6F8), Surface = ThemePalette.C(0xFFFFFF), Hover = ThemePalette.C(0xE6E9EE),
                Border = ThemePalette.C(0xCDD2DA), Text = ThemePalette.C(0x1F2328), TextDim = ThemePalette.C(0x6B7280),
                Accent = ThemePalette.C(0x2563EB), Selection = ThemePalette.C(0xBFD7FF)
            },
            // "Custom": a light or dark background and an accent color of the user (the fields are filled by RebuildCustom)
            new ThemePalette
            {
                Id = "custom", NamePt = "Personalizado...", NameEn = "Custom...", IsDark = true,
                Window = ThemePalette.C(0x16130F), Surface = ThemePalette.C(0x221E19), Hover = ThemePalette.C(0x352E26),
                Border = ThemePalette.C(0x3D362D), Text = ThemePalette.C(0xF2ECE4), TextDim = ThemePalette.C(0x9A9186),
                Accent = ThemePalette.C(0xFFB627), Selection = ThemePalette.C(0x6B4A12)
            }
        };

        private static bool s_CustomDark = true;
        private static Color s_CustomAccent = ThemePalette.C(0xFFB627);

        /// <summary>The background of the "Custom" theme: true = dark.</summary>
        public static bool CustomDark { get { return s_CustomDark; } }

        /// <summary>The accent color of the "Custom" theme.</summary>
        public static Color CustomAccent { get { return s_CustomAccent; } }

        // ---- contrast (WCAG): text needs 4.5:1, borders and icons which carry meaning 3:1 ----

        private static double Luminance(Color c)
        {
            Func<int, double> f = v =>
            {
                double s = v / 255.0;
                return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
            };
            return 0.2126 * f(c.R) + 0.7152 * f(c.G) + 0.0722 * f(c.B);
        }

        /// <summary>Contrast ratio of two colors (1 to 21).</summary>
        public static double ContrastRatio(Color a, Color b)
        {
            double la = Luminance(a), lb = Luminance(b);
            if (la < lb)
            {
                double t = la;
                la = lb;
                lb = t;
            }
            return (la + 0.05) / (lb + 0.05);
        }

        /// <summary>Moves a color towards white (dark background) or black (light background) until it has the contrast wanted.</summary>
        public static Color EnsureContrast(Color c, Color background, double minimum)
        {
            if (ContrastRatio(c, background) >= minimum)
                return c;
            Color target = Luminance(background) < 0.4 ? Color.White : Color.Black;
            Color result = c;
            for (int i = 1; i <= 40; i++)
            {
                result = MixColor(c, target, i / 40.0);
                if (ContrastRatio(result, background) >= minimum)
                    break;
            }
            return result;
        }

        // the colors which must read well are brought to the minimum contrast (also for the colors the user chooses)
        private static void EnsureReadable()
        {
            foreach (ThemePalette p in s_Palettes)
            {
                p.TextDim = EnsureContrast(EnsureContrast(p.TextDim, p.Window, 4.5), p.Hover, 4.5);
                p.TextDim = EnsureContrast(p.TextDim, p.Surface, 4.5);
                // the accent is used for lines, icons and some texts
                p.Accent = EnsureContrast(p.Accent, p.Window, 3.0);
            }
        }

        private static Color MixColor(Color a, Color b, double t)
        {
            return Color.FromArgb(255, (int)Math.Round(a.R + (b.R - a.R) * t), (int)Math.Round(a.G + (b.G - a.G) * t),
                (int)Math.Round(a.B + (b.B - a.B) * t));
        }

        // the colors of the custom theme: the Apolo background (dark or light) with the accent color of the user
        private static void RebuildCustom()
        {
            ThemePalette custom = s_Palettes.First(p => p.Id == "custom");
            ThemePalette basis = s_Palettes.First(p => p.Id == (s_CustomDark ? "apolo" : "apolo-light"));
            custom.IsDark = s_CustomDark;
            custom.Window = basis.Window;
            custom.Surface = basis.Surface;
            custom.Hover = basis.Hover;
            custom.Border = basis.Border;
            custom.Text = basis.Text;
            custom.TextDim = basis.TextDim;
            custom.Accent = EnsureContrast(s_CustomAccent, custom.Window, 3.0);
            custom.TextDim = EnsureContrast(EnsureContrast(basis.TextDim, custom.Window, 4.5), custom.Hover, 4.5);
            custom.Selection = s_CustomDark ? MixColor(basis.Window, s_CustomAccent, 0.40) : MixColor(Color.White, s_CustomAccent, 0.32);
        }

        /// <summary>Sets the custom theme (dark or light background, accent color), applies it at once and remembers it.</summary>
        public static void SetCustom(bool dark, Color accent)
        {
            s_CustomDark = dark;
            s_CustomAccent = Color.FromArgb(255, accent.R, accent.G, accent.B);
            RebuildCustom();
            ThemePalette custom = s_Palettes.First(p => p.Id == "custom");
            Current = custom;
            SavePreferences();
            UpdateRenderer();
            ApplyToAllForms();
            RaiseChanged();
        }

        private static readonly int[] s_ScaleOptions = new int[] { 100, 110, 125, 150 };

        private class OriginalState
        {
            public Color BackColor;
            public Color ForeColor;
            public bool BackColorWasSet;
            public bool ForeColorWasSet;
            public Font Font;
            public bool ExplicitFont;
            public FlatStyle FlatStyle;
            public bool HasFlatStyle;
            public bool UseVisualStyleBackColor;
            public BorderStyle BorderStyle;
            public bool HasBorderStyle;
            public TabDrawMode TabDrawMode;
            public TabSizeMode TabSizeMode;
            public Size TabItemSize;
            public Size ToolStripImageSize;
        }

        private static readonly ConditionalWeakTable<object, OriginalState> s_Originals = new ConditionalWeakTable<object, OriginalState>();
        private static readonly ConditionalWeakTable<object, object> s_Hooked = new ConditionalWeakTable<object, object>();
        private static readonly ConditionalWeakTable<object, object> s_MenuAdded = new ConditionalWeakTable<object, object>();
        private static bool s_Installed;
        private static bool s_IdleLogged;

        public static void Log(string where, Exception ex)
        {
            try
            {
                string dir = Ares.Settings.AppFolders.Root;
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "theme.log"),
                    DateTime.Now.ToString("HH:mm:ss") + " " + where + ": " + (ex == null ? "" : ex.GetType().Name + " " + ex.Message) + Environment.NewLine);
            }
            catch (Exception swallowed)
            {
                Ares.Settings.ErrorLog.Note(swallowed);
            }
        }

        public static ThemePalette Current { get; private set; }
        public static int ScalePercent { get; private set; }
        public static event EventHandler Changed;

        public static IEnumerable<ThemePalette> Palettes { get { return s_Palettes; } }

        // --------------------------------------------------------------------------------
        // Installation / persistence
        // --------------------------------------------------------------------------------

        /// <summary>
        /// Call once at startup (after Application.EnableVisualStyles).
        /// </summary>
        public static void Install()
        {
            if (s_Installed)
                return;
            s_Installed = true;
            Current = s_Palettes.First(p => p.Id == "apolo");
            ScalePercent = 110;
            RebuildCustom();
            LoadPreferences();
            RebuildCustom();
            EnsureReadable();
            if (s_NeedsThemeMigration)
            {
                // version 2 of the look: Apolo (dark) is the default for everybody once
                Current = s_Palettes.First(p => p.Id == "apolo");
                SavePreferences();
            }
            UpdateRenderer();
            Application.Idle += OnApplicationIdle;
            Log("install: theme=" + Current.Id + " scale=" + ScalePercent, null);
        }

        private static string PreferencesFile
        {
            get
            {
                string dir = Ares.Settings.AppFolders.Root;
                return Path.Combine(dir, "appearance.ini");
            }
        }

        private static bool s_NeedsThemeMigration;

        private static void LoadPreferences()
        {
            try
            {
                if (!File.Exists(PreferencesFile))
                    return;
                s_NeedsThemeMigration = true;
                foreach (string line in File.ReadAllLines(PreferencesFile))
                {
                    int idx = line.IndexOf('=');
                    if (idx <= 0)
                        continue;
                    string key = line.Substring(0, idx).Trim().ToLowerInvariant();
                    string value = line.Substring(idx + 1).Trim();
                    if (key == "theme")
                    {
                        ThemePalette p = s_Palettes.FirstOrDefault(x => x.Id == value);
                        if (p != null)
                            Current = p;
                    }
                    else if (key == "themev")
                    {
                        if (value == "2")
                            s_NeedsThemeMigration = false;
                    }
                    else if (key == "customdark")
                    {
                        s_CustomDark = value != "0";
                    }
                    else if (key == "customaccent")
                    {
                        try
                        {
                            s_CustomAccent = ThemePalette.C(Convert.ToInt32(value.TrimStart('#'), 16));
                        }
                        catch (Exception swallowed)
                        {
                            Ares.Settings.ErrorLog.Note(swallowed);
                        }
                    }
                    else if (key == "scale")
                    {
                        int s;
                        if (int.TryParse(value, out s) && s >= 80 && s <= 200)
                            ScalePercent = s;
                    }
                }
            }
            catch (Exception swallowed)
            {
                // use defaults
                Ares.Settings.ErrorLog.Note(swallowed);
            }
        }

        private static void SavePreferences()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PreferencesFile));
                File.WriteAllLines(PreferencesFile, new string[]
                {
                    "theme=" + Current.Id, "scale=" + ScalePercent, "themev=2",
                    "customdark=" + (s_CustomDark ? "1" : "0"),
                    "customaccent=" + (s_CustomAccent.ToArgb() & 0xFFFFFF).ToString("X6")
                });
            }
            catch (Exception swallowed)
            {
                // not critical
                Ares.Settings.ErrorLog.Note(swallowed);
            }
        }

        public static void SetTheme(string id)
        {
            ThemePalette p = s_Palettes.FirstOrDefault(x => x.Id == id);
            if (p == null || p == Current)
                return;
            Current = p;
            SavePreferences();
            UpdateRenderer();
            ApplyToAllForms();
            RaiseChanged();
        }

        public static void SetScale(int percent)
        {
            if (percent == ScalePercent)
                return;
            ScalePercent = percent;
            SavePreferences();
            ApplyToAllForms();
            RaiseChanged();
        }

        private static void RaiseChanged()
        {
            EventHandler handler = Changed;
            if (handler != null)
                handler(null, EventArgs.Empty);
        }

        private static void ApplyToAllForms()
        {
            foreach (Form f in Application.OpenForms.Cast<Form>().ToList())
            {
                Apply(f);
                f.Invalidate(true);
            }
        }

        // every window gets the Apolo icon (windows of the original Ares code would show the old one)
        private static void SetApoloIcon(Form f)
        {
            try
            {
                string name = f.GetType().FullName;
                if (name == "Ares.Player.Player" || name == "Ares.Editor.MainForm")
                    return; // these set their own (the Player's icon shows the playing state)
                if (f.ShowIcon)
                    f.Icon = ApoloBrand.GetIcon(32, ApoloState.Playing, 0);
            }
            catch (Exception ex)
            {
                Log("window icon", ex);
            }
        }

        private static void OnApplicationIdle(object sender, EventArgs e)
        {
            // pick up forms created since the last time (dialogs, tool windows ...)
            if (!s_IdleLogged)
            {
                s_IdleLogged = true;
                Log("idle: forms=" + Application.OpenForms.Count + " theme=" + Current.Id + " scale=" + ScalePercent, null);
            }
            foreach (Form f in Application.OpenForms.Cast<Form>().ToList())
            {
                object dummy;
                if (!s_Hooked.TryGetValue(f, out dummy))
                {
                    Apply(f);
                    SetApoloIcon(f);
                }
                AddAppearanceMenu(f);
                if (s_Reveal.Contains(f))
                    Reveal(f);
            }
        }

        // A window which is invisible until the theme was applied to it: otherwise it shows for a moment with the
        // white colors of Windows when the program starts. A timer shows it anyway after a short time.
        private static readonly List<Form> s_Reveal = new List<Form>();

        public static void RevealWhenThemed(Form form)
        {
            if (form == null || Current == null || Current.IsClassic)
                return;
            form.Opacity = 0;
            s_Reveal.Add(form);
            Timer fallback = new Timer();
            fallback.Interval = 2500;
            fallback.Tick += delegate
            {
                fallback.Stop();
                fallback.Dispose();
                Reveal(form);
            };
            fallback.Start();
        }

        private static void Reveal(Form form)
        {
            if (s_Reveal.Remove(form) && !form.IsDisposed)
                form.Opacity = 1;
        }

        // --------------------------------------------------------------------------------
        // Applying a theme
        // --------------------------------------------------------------------------------

        /// <summary>
        /// Applies the current theme and scale to a control and all its children.
        /// Can be called safely multiple times.
        /// </summary>
        public static void Apply(Control control)
        {
            if (control == null || control.IsDisposed)
                return;
            if (Current == null)
                return;
            ApplyInternal(control);
        }

        private static void ApplyInternal(Control c)
        {
            try
            {
                HookControl(c);
                ApplyToSingleControl(c);
            }
            catch (Exception ex)
            {
                // theming must never break the application, and one bad control must not stop the others
                Log("apply " + c.GetType().Name + " '" + c.Name + "'", ex);
            }
            foreach (Control child in c.Controls.Cast<Control>().ToList())
            {
                ApplyInternal(child);
            }
        }

        private static void HookControl(Control c)
        {
            object dummy;
            if (s_Hooked.TryGetValue(c, out dummy))
                return;
            s_Hooked.Add(c, new object());
            c.ControlAdded += OnControlAdded;
            if (!(c is Form))
            {
                c.HandleCreated += OnHandleCreated;
            }
            else
            {
                c.HandleCreated += OnFormHandleCreated;
            }
        }

        private static void OnControlAdded(object sender, ControlEventArgs e)
        {
            Apply(e.Control);
        }

        private static void OnHandleCreated(object sender, EventArgs e)
        {
            Control c = sender as Control;
            if (c != null)
                ApplyNativeTheme(c);
        }

        private static void OnFormHandleCreated(object sender, EventArgs e)
        {
            Form f = sender as Form;
            if (f != null)
                SetDarkTitleBar(f);
        }

        private static OriginalState Capture(Control c)
        {
            OriginalState st;
            if (s_Originals.TryGetValue(c, out st))
                return st;
            st = new OriginalState();
            st.BackColor = c.BackColor;
            st.ForeColor = c.ForeColor;
            st.Font = c.Font;
            Control parent = c.Parent;
            st.ExplicitFont = parent == null ? true : !c.Font.Equals(parent.Font);
            if (c is Form && ((Form)c).TopLevel)
                st.ExplicitFont = true;
            ButtonBase bb = c as ButtonBase;
            if (bb != null)
            {
                st.FlatStyle = bb.FlatStyle;
                st.HasFlatStyle = true;
                st.UseVisualStyleBackColor = bb.UseVisualStyleBackColor;
            }
            ComboBox cb = c as ComboBox;
            if (cb != null)
            {
                st.FlatStyle = cb.FlatStyle;
                st.HasFlatStyle = true;
            }
            GroupBox gb = c as GroupBox;
            if (gb != null)
            {
                st.FlatStyle = gb.FlatStyle;
                st.HasFlatStyle = true;
            }
            Label lb = c as Label;
            if (lb != null)
            {
                st.FlatStyle = lb.FlatStyle;
                st.HasFlatStyle = true;
            }
            TextBox tb = c as TextBox;
            if (tb != null)
            {
                st.BorderStyle = tb.BorderStyle;
                st.HasBorderStyle = true;
            }
            ListView lv = c as ListView;
            if (lv != null)
            {
                st.BorderStyle = lv.BorderStyle;
                st.HasBorderStyle = true;
            }
            TreeView tv = c as TreeView;
            if (tv != null)
            {
                st.BorderStyle = tv.BorderStyle;
                st.HasBorderStyle = true;
            }
            ListBox lbx = c as ListBox;
            if (lbx != null)
            {
                st.BorderStyle = lbx.BorderStyle;
                st.HasBorderStyle = true;
            }
            TabControl tc = c as TabControl;
            if (tc != null)
            {
                st.TabDrawMode = tc.DrawMode;
                st.TabItemSize = tc.ItemSize;
                st.TabSizeMode = tc.SizeMode;
            }
            ToolStrip ts = c as ToolStrip;
            if (ts != null)
            {
                st.ToolStripImageSize = ts.ImageScalingSize;
            }
            s_Originals.Add(c, st);
            return st;
        }

        private static void ApplyToSingleControl(Control c)
        {
            OriginalState orig = Capture(c);
            ThemePalette p = Current;
            HookMenus(c);

            try
            {
                ApplyFont(c, orig);
            }
            catch (Exception ex)
            {
                Log("font " + c.GetType().Name + " '" + c.Name + "'", ex);
            }

            if (p.IsClassic)
            {
                RestoreControl(c, orig);
                return;
            }

            // --- generic colors ---------------------------------------------------
            Form form = c as Form;
            if (form != null)
            {
                form.BackColor = p.Window;
                form.ForeColor = p.Text;
                if (form.IsHandleCreated)
                    SetDarkTitleBar(form);
                return;
            }

            if (c is Button)
            {
                Button b = (Button)c;
                b.UseVisualStyleBackColor = false;
                b.FlatStyle = FlatStyle.Flat;
                b.FlatAppearance.BorderColor = p.ControlBorder;
                b.FlatAppearance.MouseOverBackColor = p.Hover;
                b.FlatAppearance.MouseDownBackColor = p.Selection;
                b.BackColor = p.Surface;
                b.ForeColor = p.Text;
                b.FlatAppearance.BorderSize = 0;
                if (b.Tag is string && (string)b.Tag == "Primary")
                {
                    // the main button of a dialog: filled with the highlight color, text in black or white (whichever reads better)
                    b.BackColor = p.Accent;
                    b.ForeColor = ContrastRatio(p.Accent, Color.White) >= ContrastRatio(p.Accent, Color.Black) ? Color.White : Color.Black;
                    b.FlatAppearance.MouseOverBackColor = Color.FromArgb(255,
                        (p.Accent.R * 85 + 255 * 15) / 100, (p.Accent.G * 85 + 255 * 15) / 100, (p.Accent.B * 85 + 255 * 15) / 100);
                    b.FlatAppearance.MouseDownBackColor = p.Accent;
                    if (!b.Font.Bold)
                        b.Font = new Font(b.Font, FontStyle.Bold);
                }
                HookRoundedButton(b);
            }
            else if (c is CheckBox && ((CheckBox)c).Appearance == Appearance.Button
                || c is RadioButton && ((RadioButton)c).Appearance == Appearance.Button)
            {
                // toggle buttons (used for the element buttons of the player)
                ButtonBase tb2 = (ButtonBase)c;
                tb2.UseVisualStyleBackColor = false;
                tb2.FlatStyle = FlatStyle.Flat;
                tb2.FlatAppearance.BorderSize = 1;
                tb2.FlatAppearance.BorderColor = p.ControlBorder;
                tb2.FlatAppearance.MouseOverBackColor = p.Hover;
                tb2.FlatAppearance.MouseDownBackColor = p.Accent;
                tb2.FlatAppearance.CheckedBackColor = p.Selection;
                tb2.BackColor = p.Surface;
                tb2.ForeColor = p.Text;
            }
            else if (c is CheckBox || c is RadioButton)
            {
                ButtonBase bb = (ButtonBase)c;
                if (bb.FlatStyle == FlatStyle.System)
                    bb.FlatStyle = FlatStyle.Standard;
                bb.UseVisualStyleBackColor = false;
                bb.BackColor = Color.Transparent;
                bb.ForeColor = p.Text;
                HookDisabledText(bb);
            }
            else if (c is GroupBox)
            {
                ApplyGroupBox((GroupBox)c);
            }
            else if (c is TrackBar)
            {
                c.BackColor = p.Window;
                c.ForeColor = p.Text;
            }
            else if (c is LinkLabel)
            {
                LinkLabel l = (LinkLabel)c;
                l.BackColor = Color.Transparent;
                l.ForeColor = p.Text;
                l.LinkColor = p.Accent;
                l.ActiveLinkColor = p.Accent;
                l.VisitedLinkColor = p.Accent;
            }
            else if (c is Label)
            {
                Label l = (Label)c;
                if (l.FlatStyle == FlatStyle.System)
                    l.FlatStyle = FlatStyle.Standard;
                l.BackColor = Color.Transparent;
                l.ForeColor = p.Text;
                // labels of the dialogs: "Muted" = secondary text, "Title" = heading of the window
                string role = l.Tag as string;
                if (role == "Muted")
                    l.ForeColor = p.TextDim;
                else if (role == "Title" && !l.Font.Bold)
                    l.Font = new Font(l.Font.FontFamily, l.Font.Size + 1.5f, FontStyle.Bold);
            }
            else if (c is TextBoxBase || c is NumericUpDown || c is DomainUpDown)
            {
                c.BackColor = p.Surface;
                c.ForeColor = p.Text;
                TextBox tb = c as TextBox;
                if (tb != null && !(tb.Tag is string && (string)tb.Tag == "NoThemeBorder"))
                {
                    tb.BorderStyle = BorderStyle.FixedSingle;
                    HookTextBoxBorder(tb);
                }
            }
            else if (c is ComboBox)
            {
                ComboBox cb = (ComboBox)c;
                cb.FlatStyle = FlatStyle.Flat;
                cb.BackColor = p.Surface;
                cb.ForeColor = p.Text;
                ApplyComboLook(cb);
            }
            else if (c is ListView)
            {
                ListView lv = (ListView)c;
                lv.BackColor = p.Surface;
                lv.ForeColor = p.Text;
                lv.BorderStyle = BorderStyle.FixedSingle;
                if (!p.IsClassic)
                {
                    if (lv.GridLines)
                        ApplySoftGrid(lv);
                    else
                        ApplyHeaderLook(lv);
                }
            }
            else if (c is TreeView)
            {
                TreeView tv = (TreeView)c;
                tv.BackColor = p.Surface;
                tv.ForeColor = p.Text;
                tv.LineColor = p.Border;
                tv.BorderStyle = BorderStyle.FixedSingle;
            }
            else if (c is ListBox)
            {
                ListBox lb = (ListBox)c;
                lb.BackColor = p.Surface;
                lb.ForeColor = p.Text;
                lb.BorderStyle = BorderStyle.FixedSingle;
                ApplyListBoxLook(lb);
            }
            else if (c is TabControl)
            {
                ApplyTabControl((TabControl)c);
            }
            else if (c is TabPage)
            {
                TabPage tp = (TabPage)c;
                tp.UseVisualStyleBackColor = false;
                tp.BackColor = p.Window;
                tp.ForeColor = p.Text;
            }
            else if (c is ToolStrip)
            {
                ToolStrip ts = (ToolStrip)c;
                ts.BackColor = p.Window;
                ts.ForeColor = p.Text;
                ApplyToolStripScale(ts, orig);
            }
            else if (c is DataGridView)
            {
                DataGridView dg = (DataGridView)c;
                dg.BackgroundColor = p.Window;
                dg.GridColor = p.Border;
                dg.EnableHeadersVisualStyles = false;
                dg.ColumnHeadersDefaultCellStyle.BackColor = p.Surface;
                dg.ColumnHeadersDefaultCellStyle.ForeColor = p.Text;
                dg.RowHeadersDefaultCellStyle.BackColor = p.Surface;
                dg.RowHeadersDefaultCellStyle.ForeColor = p.Text;
                dg.DefaultCellStyle.BackColor = p.Surface;
                dg.DefaultCellStyle.ForeColor = p.Text;
                dg.DefaultCellStyle.SelectionBackColor = p.Selection;
                dg.DefaultCellStyle.SelectionForeColor = p.Text;
                dg.AlternatingRowsDefaultCellStyle.BackColor = p.Surface;
                dg.AlternatingRowsDefaultCellStyle.ForeColor = p.Text;
                dg.AlternatingRowsDefaultCellStyle.SelectionBackColor = p.Selection;
                dg.AlternatingRowsDefaultCellStyle.SelectionForeColor = p.Text;
                dg.BorderStyle = BorderStyle.None;
                dg.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
                dg.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
                foreach (DataGridViewColumn col in dg.Columns)
                {
                    // styles set on a column win over the grid's default style
                    col.DefaultCellStyle.BackColor = p.Surface;
                    col.DefaultCellStyle.ForeColor = p.Text;
                    col.DefaultCellStyle.SelectionBackColor = p.Selection;
                    col.DefaultCellStyle.SelectionForeColor = p.Text;
                }
            }
            else if (c is PropertyGrid)
            {
                PropertyGrid pg = (PropertyGrid)c;
                pg.BackColor = p.Window;
                pg.ViewBackColor = p.Surface;
                pg.ViewForeColor = p.Text;
                pg.LineColor = p.Border;
                pg.CategoryForeColor = p.Text;
                pg.HelpBackColor = p.Window;
                pg.HelpForeColor = p.Text;
                pg.CommandsBackColor = p.Window;
                pg.CommandsForeColor = p.Text;
            }
            else if (c.GetType().Name == "DockPanel")
            {
                ApplyDockPanel(c);
            }
            else if (c is ProgressBar || c is PictureBox || c is MenuStrip)
            {
                // leave as is (MenuStrip is a ToolStrip and handled above)
            }
            else if (c is ScrollableControl || c is UserControl || c is SplitContainer || c is Panel)
            {
                c.BackColor = p.Window;
                c.ForeColor = p.Text;
            }
            else
            {
                // unknown custom control: only text color if it was explicitly set to something dark
                c.ForeColor = p.Text;
            }

            if (c.IsHandleCreated)
                ApplyNativeTheme(c);
        }

        private static void RestoreControl(Control c, OriginalState o)
        {
            Form form = c as Form;
            if (form != null)
            {
                form.BackColor = o.BackColor;
                form.ForeColor = o.ForeColor;
                if (form.IsHandleCreated)
                    SetDarkTitleBar(form);
                return;
            }
            try
            {
                c.BackColor = o.BackColor;
                c.ForeColor = o.ForeColor;
            }
            catch (Exception swallowed)
            {
                Ares.Settings.ErrorLog.Note(swallowed);
            }
            if (o.HasFlatStyle)
            {
                ButtonBase bb = c as ButtonBase;
                if (bb != null)
                {
                    bb.FlatStyle = o.FlatStyle;
                    bb.UseVisualStyleBackColor = o.UseVisualStyleBackColor;
                }
                ComboBox cb = c as ComboBox;
                if (cb != null)
                    cb.FlatStyle = o.FlatStyle;
                GroupBox gb = c as GroupBox;
                if (gb != null)
                    gb.FlatStyle = o.FlatStyle;
                Label lb = c as Label;
                if (lb != null)
                    lb.FlatStyle = o.FlatStyle;
            }
            if (o.HasBorderStyle)
            {
                TextBox tb = c as TextBox;
                if (tb != null)
                    tb.BorderStyle = o.BorderStyle;
                ListView lv = c as ListView;
                if (lv != null)
                    lv.BorderStyle = o.BorderStyle;
                TreeView tv = c as TreeView;
                if (tv != null)
                    tv.BorderStyle = o.BorderStyle;
                ListBox lbx = c as ListBox;
                if (lbx != null)
                {
                    lbx.BorderStyle = o.BorderStyle;
                    RemoveListBoxLook(lbx);
                }
            }
            TabControl tc = c as TabControl;
            if (tc != null)
            {
                tc.DrawItem -= OnTabDrawItem;
                tc.DrawMode = o.TabDrawMode;
                tc.SizeMode = o.TabSizeMode;
                tc.ItemSize = o.TabItemSize;
            }
            TabPage tp = c as TabPage;
            if (tp != null)
                tp.UseVisualStyleBackColor = true;
            ToolStrip ts = c as ToolStrip;
            if (ts != null)
                ApplyToolStripScale(ts, o);
            if (c.IsHandleCreated)
                ApplyNativeTheme(c);
        }

        // --------------------------------------------------------------------------------
        // Font / size scaling
        // --------------------------------------------------------------------------------

        private static FontFamily s_Segoe;
        private static bool s_SegoeTried;

        // the modern Windows UI font (the designer fonts are the old Microsoft Sans Serif)
        private static FontFamily SegoeUI
        {
            get
            {
                if (!s_SegoeTried)
                {
                    s_SegoeTried = true;
                    try { s_Segoe = new FontFamily("Segoe UI"); }
                    catch (Exception) { s_Segoe = null; }
                }
                return s_Segoe;
            }
        }

        private static void ApplyFont(Control c, OriginalState orig)
        {
            if (!orig.ExplicitFont)
                return;
            if (c is ToolStrip && !(c.Parent == null))
            {
                // toolstrips inherit from the form unless set explicitly
            }
            float factor = ScalePercent / 100f;
            float size = orig.Font.Size * factor;
            FontFamily family = orig.Font.FontFamily;
            string old = family.Name;
            if (Current != null && !Current.IsClassic && SegoeUI != null
                && (old == "Microsoft Sans Serif" || old == "Tahoma" || old == "MS Sans Serif" || old == "MS Shell Dlg"))
                family = SegoeUI;
            if (Math.Abs(c.Font.Size - size) < 0.01f && c.Font.FontFamily.Name == family.Name)
                return;
            try
            {
                c.Font = new Font(family, size, orig.Font.Style, orig.Font.Unit);
            }
            catch (Exception swallowed)
            {
                Ares.Settings.ErrorLog.Note(swallowed);
            }
        }

        private static void ApplyToolStripScale(ToolStrip ts, OriginalState orig)
        {
            try
            {
                // toolbars with Lucide icons size themselves (see IconLibrary)
                if (IconLibrary.IsManaged(ts))
                    return;
                float factor = ScalePercent / 100f;
                Size target = new Size(
                    (int)Math.Round(orig.ToolStripImageSize.Width * factor),
                    (int)Math.Round(orig.ToolStripImageSize.Height * factor));
                if (ts.ImageScalingSize != target)
                    ts.ImageScalingSize = target;
            }
            catch (Exception swallowed)
            {
                Ares.Settings.ErrorLog.Note(swallowed);
            }
        }

        // --------------------------------------------------------------------------------
        // Group boxes: the Windows border is always light gray, so we draw it ourselves
        // --------------------------------------------------------------------------------

        private static readonly ConditionalWeakTable<object, object> s_GroupPaint = new ConditionalWeakTable<object, object>();

        // ---- list boxes: selection in the accent color (instead of the system blue) ----

        private static readonly ConditionalWeakTable<ListBox, object> s_ListLook = new ConditionalWeakTable<ListBox, object>();

        private static void ApplyListBoxLook(ListBox lb)
        {
            if (lb is CheckedListBox)
                return;
            object marker;
            if (s_ListLook.TryGetValue(lb, out marker))
            {
                lb.ItemHeight = Math.Max(22, lb.Font.Height + 8);
                lb.Invalidate();
                return;
            }
            if (lb.DrawMode != DrawMode.Normal)
                return; // somebody draws it already
            s_ListLook.Add(lb, new object());
            lb.DrawMode = DrawMode.OwnerDrawFixed;
            lb.ItemHeight = Math.Max(22, lb.Font.Height + 8);
            lb.DrawItem += OnListBoxDrawItem;
            lb.MouseMove += OnListBoxMouseMove;
            lb.MouseLeave += OnListBoxMouseLeave;
        }

        private static void RemoveListBoxLook(ListBox lb)
        {
            object marker;
            if (!s_ListLook.TryGetValue(lb, out marker))
                return;
            lb.DrawItem -= OnListBoxDrawItem;
            lb.MouseMove -= OnListBoxMouseMove;
            lb.MouseLeave -= OnListBoxMouseLeave;
            s_ListLook.Remove(lb);
            lb.DrawMode = DrawMode.Normal;
        }

        // an item which does not fit in the list shows its whole text when the mouse rests on it
        private static ToolTip s_ListTip;
        private static ListBox s_TipList;
        private static int s_TipIndex = -1;

        private static void OnListBoxMouseMove(object sender, MouseEventArgs e)
        {
            ListBox lb = (ListBox)sender;
            int i = lb.IndexFromPoint(e.Location);
            if (i == s_TipIndex && lb == s_TipList)
                return;
            OnListBoxMouseLeave(sender, EventArgs.Empty);
            if (i < 0 || i >= lb.Items.Count)
                return;
            string text = lb.GetItemText(lb.Items[i]);
            Size size = TextRenderer.MeasureText(text, lb.Font);
            s_TipList = lb;
            s_TipIndex = i;
            if (size.Width <= lb.ClientSize.Width - 14)
                return;
            if (s_ListTip == null)
                s_ListTip = new ToolTip();
            s_ListTip.Show(text, lb, e.X + 14, e.Y + 22, 6000);
        }

        private static void OnListBoxMouseLeave(object sender, EventArgs e)
        {
            if (s_ListTip != null && s_TipList != null)
                s_ListTip.Hide(s_TipList);
            s_TipList = null;
            s_TipIndex = -1;
        }

        private static void OnListBoxDrawItem(object sender, DrawItemEventArgs e)
        {
            ListBox lb = (ListBox)sender;
            ThemePalette p = Current;
            if (e.Index < 0 || p == null || p.IsClassic)
                return;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            using (SolidBrush back = new SolidBrush(selected ? p.Selection : lb.BackColor))
                e.Graphics.FillRectangle(back, e.Bounds);
            if (selected)
            {
                using (SolidBrush bar = new SolidBrush(p.Accent))
                    e.Graphics.FillRectangle(bar, e.Bounds.X, e.Bounds.Y, 3, e.Bounds.Height);
            }
            string text = lb.GetItemText(lb.Items[e.Index]);
            Rectangle r = new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 10, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, text, lb.Font, r, p.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.Left);
        }

        // ---- drop-down lists drawn with the colors of the theme ----

        private static readonly ConditionalWeakTable<ComboBox, object> s_ComboLook = new ConditionalWeakTable<ComboBox, object>();

        private static void ApplyComboLook(ComboBox cb)
        {
            if (cb.DropDownStyle != ComboBoxStyle.DropDownList || cb.DrawMode != DrawMode.Normal)
                return;
            object marker;
            if (s_ComboLook.TryGetValue(cb, out marker))
                return;
            s_ComboLook.Add(cb, new object());
            cb.DrawMode = DrawMode.OwnerDrawFixed;
            cb.ItemHeight = Math.Max(cb.ItemHeight, cb.Font.Height + 4);
            cb.DrawItem += OnComboDrawItem;
        }

        private static void OnComboDrawItem(object sender, DrawItemEventArgs e)
        {
            ComboBox cb = (ComboBox)sender;
            ThemePalette p = Current;
            if (e.Index < 0 || p == null)
                return;
            bool inList = (e.State & DrawItemState.ComboBoxEdit) == 0;
            bool selected = inList && (e.State & DrawItemState.Selected) != 0;
            using (SolidBrush back = new SolidBrush(selected ? p.Selection : p.Surface))
                e.Graphics.FillRectangle(back, e.Bounds);
            Rectangle r = new Rectangle(e.Bounds.X + 4, e.Bounds.Y, e.Bounds.Width - 6, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, cb.GetItemText(cb.Items[e.Index]), cb.Font, r, p.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.Left);
        }

        // ---- rounded flat buttons ----

        private static readonly ConditionalWeakTable<Button, object> s_RoundButtons = new ConditionalWeakTable<Button, object>();

        private static void HookRoundedButton(Button b)
        {
            object marker;
            if (s_RoundButtons.TryGetValue(b, out marker))
            {
                b.Invalidate();
                return;
            }
            s_RoundButtons.Add(b, new object());
            b.Paint += OnRoundedButtonPaint;
            b.Invalidate();
        }

        private static Color OpaqueBack(Control c)
        {
            Control p = c.Parent;
            while (p != null)
            {
                if (p.BackColor.A == 255)
                    return p.BackColor;
                p = p.Parent;
            }
            return Current != null ? Current.Window : SystemColors.Control;
        }

        private static void OnRoundedButtonPaint(object sender, PaintEventArgs e)
        {
            Button b = (Button)sender;
            ThemePalette p = Current;
            if (p == null || p.IsClassic || b.FlatStyle != FlatStyle.Flat || b.Width < 24 || b.Height < 18)
                return;
            Graphics g = e.Graphics;
            int d = 12;
            Rectangle r = new Rectangle(0, 0, b.Width - 1, b.Height - 1);
            using (System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath())
            {
                path.AddArc(r.X, r.Y, d, d, 180, 90);
                path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                using (Region corners = new Region(new Rectangle(0, 0, b.Width, b.Height)))
                using (SolidBrush parentBack = new SolidBrush(OpaqueBack(b)))
                {
                    corners.Exclude(path);
                    g.FillRegion(parentBack, corners);
                }
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                bool hot = b.Focused || b.ClientRectangle.Contains(b.PointToClient(Cursor.Position)) && b.Enabled;
                // the border of a button is seen at 3:1; the focus of the keyboard is a thicker line in the accent color
                using (Pen pen = new Pen(hot ? p.Accent : p.ControlBorder, b.Focused ? 2f : 1f))
                    g.DrawPath(pen, path);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.Default;
                if (!b.Enabled && b.Image == null && !string.IsNullOrEmpty(b.Text))
                {
                    // Windows draws the text of a disabled flat button in a gray which almost vanishes: draw it again, readable but dim
                    Rectangle inner = Rectangle.Inflate(b.ClientRectangle, -3, -3);
                    using (SolidBrush back = new SolidBrush(b.BackColor))
                        g.FillRectangle(back, inner);
                    TextRenderer.DrawText(g, b.Text, b.Font, inner, p.TextDim,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                }
            }
        }

        // ---- disabled check boxes and radio buttons ----

        private static readonly ConditionalWeakTable<ButtonBase, object> s_DisabledText = new ConditionalWeakTable<ButtonBase, object>();

        private static void HookDisabledText(ButtonBase b)
        {
            object marker;
            if (s_DisabledText.TryGetValue(b, out marker))
            {
                b.Invalidate();
                return;
            }
            s_DisabledText.Add(b, new object());
            b.Paint += OnDisabledTextPaint;
            b.EnabledChanged += delegate { b.Invalidate(); };
            b.Invalidate();
        }

        // The text of a disabled radio button / check box is blurred by Windows (embossed gray): draw it again, crisp and dim.
        private static void OnDisabledTextPaint(object sender, PaintEventArgs e)
        {
            ButtonBase b = (ButtonBase)sender;
            ThemePalette p = Current;
            if (p == null || p.IsClassic || b.Enabled || string.IsNullOrEmpty(b.Text) || b.Image != null)
                return;
            ContentAlignment check = b is CheckBox ? ((CheckBox)b).CheckAlign : ((RadioButton)b).CheckAlign;
            if (check != ContentAlignment.MiddleLeft && check != ContentAlignment.TopLeft && check != ContentAlignment.BottomLeft)
                return;
            int glyph = b is CheckBox
                ? CheckBoxRenderer.GetGlyphSize(e.Graphics, System.Windows.Forms.VisualStyles.CheckBoxState.UncheckedDisabled).Width
                : RadioButtonRenderer.GetGlyphSize(e.Graphics, System.Windows.Forms.VisualStyles.RadioButtonState.UncheckedDisabled).Width;
            Rectangle text = new Rectangle(glyph + 4, 0, Math.Max(1, b.Width - glyph - 4), b.Height);
            using (SolidBrush back = new SolidBrush(OpaqueBack(b)))
                e.Graphics.FillRectangle(back, text);
            TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
            if (!b.AutoSize)
                flags |= TextFormatFlags.WordBreak;
            TextRenderer.DrawText(e.Graphics, b.Text, b.Font, text, p.TextDim, flags);
        }

        // ---- border of single-line text boxes ----

        private static readonly ConditionalWeakTable<TextBox, object> s_TextBorders = new ConditionalWeakTable<TextBox, object>();

        /// <summary>Draws the 1 px border of a text box in the border color of the theme (the system border is too faint on the light theme).</summary>
        private sealed class TextBoxBorderPainter : NativeWindow
        {
            private const int WM_NCPAINT = 0x0085;
            private readonly TextBox m_Box;

            [DllImport("user32.dll")]
            private static extern IntPtr GetWindowDC(IntPtr hWnd);

            [DllImport("user32.dll")]
            private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

            [DllImport("user32.dll")]
            private static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprc, IntPtr hrgn, uint flags);

            public TextBoxBorderPainter(TextBox tb)
            {
                m_Box = tb;
                if (tb.IsHandleCreated)
                    AssignHandle(tb.Handle);
                tb.HandleCreated += delegate
                {
                    if (Handle != IntPtr.Zero)
                        ReleaseHandle();
                    AssignHandle(m_Box.Handle);
                };
                tb.HandleDestroyed += delegate
                {
                    if (Handle != IntPtr.Zero)
                        ReleaseHandle();
                };
                tb.Enter += delegate { Redraw(); };
                tb.Leave += delegate { Redraw(); };
            }

            private void Redraw()
            {
                if (m_Box.IsHandleCreated)
                    RedrawWindow(m_Box.Handle, IntPtr.Zero, IntPtr.Zero, 0x0001 | 0x0400);   // RDW_INVALIDATE | RDW_FRAME
            }

            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);
                if (m.Msg == WM_NCPAINT)
                    PaintBorder();
            }

            private void PaintBorder()
            {
                IntPtr dc = IntPtr.Zero;
                try
                {
                    ThemePalette p = Current;
                    if (p == null || p.IsClassic || m_Box.BorderStyle != BorderStyle.FixedSingle || m_Box.Width < 4 || m_Box.Height < 4)
                        return;
                    dc = GetWindowDC(m_Box.Handle);
                    if (dc == IntPtr.Zero)
                        return;
                    using (Graphics g = Graphics.FromHdc(dc))
                    using (Pen pen = new Pen(m_Box.Focused ? p.Accent : p.ControlBorder))
                        g.DrawRectangle(pen, 0, 0, m_Box.Width - 1, m_Box.Height - 1);
                }
                catch (Exception swallowed)
                {
                    Ares.Settings.ErrorLog.Note(swallowed);
                }
                finally
                {
                    if (dc != IntPtr.Zero)
                        ReleaseDC(m_Box.Handle, dc);
                }
            }
        }

        private static void HookTextBoxBorder(TextBox tb)
        {
            object marker;
            if (s_TextBorders.TryGetValue(tb, out marker))
                return;
            s_TextBorders.Add(tb, new TextBoxBorderPainter(tb));
        }

        private static void ApplyGroupBox(GroupBox g)
        {
            if (g.FlatStyle == FlatStyle.System)
                g.FlatStyle = FlatStyle.Standard;
            g.ForeColor = Current.Text;
            g.BackColor = Color.Transparent;
            object dummy;
            if (!s_GroupPaint.TryGetValue(g, out dummy))
            {
                s_GroupPaint.Add(g, new object());
                g.Paint += OnGroupBoxPaint;
                try
                {
                    MethodInfo setStyle = typeof(Control).GetMethod("SetStyle", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (setStyle != null)
                        setStyle.Invoke(g, new object[] { ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true });
                }
                catch (Exception ex)
                {
                    Log("groupbox double buffer", ex);
                }
            }
            g.Invalidate();
        }

        private static void OnGroupBoxPaint(object sender, PaintEventArgs e)
        {
            GroupBox g = sender as GroupBox;
            ThemePalette p = Current;
            if (g == null || p == null || p.IsClassic)
                return;
            Graphics gr = e.Graphics;
            using (SolidBrush back = new SolidBrush(p.Window))
            {
                gr.FillRectangle(back, 0, 0, g.Width, g.Height);
            }
            bool hasText = !string.IsNullOrEmpty(g.Text);
            Size ts = hasText ? TextRenderer.MeasureText(gr, g.Text, g.Font) : Size.Empty;
            int top = hasText ? ts.Height / 2 : 0;
            gr.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath())
            using (Pen pen = new Pen(p.Border))
            {
                int d = 10; // rounded corners
                Rectangle r = new Rectangle(0, top, g.Width - 1, g.Height - top - 1);
                if (r.Width > d && r.Height > d)
                {
                    path.AddArc(r.X, r.Y, d, d, 180, 90);
                    path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                    path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                    path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                    path.CloseFigure();
                    gr.DrawPath(pen, path);
                }
                else
                {
                    gr.DrawRectangle(pen, r);
                }
            }
            gr.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.Default;
            if (hasText)
            {
                int x = 8;
                using (SolidBrush back = new SolidBrush(p.Window))
                {
                    gr.FillRectangle(back, x - 2, 0, ts.Width + 2, ts.Height);
                }
                TextRenderer.DrawText(gr, g.Text, g.Font, new Point(x, 0), p.TextDim);
            }
        }

        // --------------------------------------------------------------------------------
        // Tab control (owner drawn tab headers)
        // --------------------------------------------------------------------------------

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<TabControl, object> s_TabPainters =
            new System.Runtime.CompilerServices.ConditionalWeakTable<TabControl, object>();

        /// <summary>Paints over the light frame which Windows draws around the page of a tab control.</summary>
        private sealed class TabFramePainter : NativeWindow
        {
            private readonly TabControl m_Tab;

            public TabFramePainter(TabControl tc)
            {
                m_Tab = tc;
                if (tc.IsHandleCreated)
                    AssignHandle(tc.Handle);
                tc.HandleCreated += delegate
                {
                    if (Handle != IntPtr.Zero)
                        ReleaseHandle();
                    AssignHandle(m_Tab.Handle);
                };
                tc.HandleDestroyed += delegate
                {
                    if (Handle != IntPtr.Zero)
                        ReleaseHandle();
                };
            }

            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);
                if (m.Msg == 0x000F)   // WM_PAINT
                    PaintFrame();
            }

            private void PaintFrame()
            {
                try
                {
                    ThemePalette p = Current;
                    if (p == null || p.IsClassic || m_Tab.TabCount == 0)
                        return;
                    Rectangle display = m_Tab.DisplayRectangle;
                    int top = 0;
                    for (int i = 0; i < m_Tab.TabCount; ++i)
                        top = Math.Max(top, m_Tab.GetTabRect(i).Bottom);
                    Rectangle client = m_Tab.ClientRectangle;
                    Rectangle area = new Rectangle(0, top, client.Width, client.Height - top);
                    int tabsRight = 0;
                    for (int i = 0; i < m_Tab.TabCount; ++i)
                        tabsRight = Math.Max(tabsRight, m_Tab.GetTabRect(i).Right);
                    Color stripBack = m_Tab.Parent != null ? m_Tab.Parent.BackColor : p.Window;
                    using (Graphics g2 = Graphics.FromHwnd(m_Tab.Handle))
                    using (SolidBrush b2 = new SolidBrush(stripBack))
                    {
                        // the empty part of the tab row, to the right of the last tab
                        g2.FillRectangle(b2, new Rectangle(0, 0, client.Width, top));
                        // the tabs again, without the light edges which Windows draws around them
                        for (int i = 0; i < m_Tab.TabCount; ++i)
                            DrawTab(g2, m_Tab, i, m_Tab.GetTabRect(i));
                    }
                    using (Graphics g = Graphics.FromHwnd(m_Tab.Handle))
                    using (Region r = new Region(area))
                    using (SolidBrush b = new SolidBrush(p.Window))
                    {
                        r.Exclude(display);
                        g.FillRegion(b, r);
                    }
                }
                catch (Exception swallowed)
                {
                    Ares.Settings.ErrorLog.Note(swallowed);
                }
            }
        }

        private static void ApplyTabControl(TabControl tc)
        {
            ThemePalette p = Current;
            tc.ForeColor = p.Text;
            tc.DrawItem -= OnTabDrawItem;
            tc.DrawItem += OnTabDrawItem;
            object painter;
            if (!s_TabPainters.TryGetValue(tc, out painter))
                s_TabPainters.Add(tc, new TabFramePainter(tc));
            tc.DrawMode = TabDrawMode.OwnerDrawFixed;
            tc.SizeMode = TabSizeMode.Fixed;
            int maxWidth = 40;
            foreach (TabPage page in tc.TabPages)
            {
                Size sz = TextRenderer.MeasureText(page.Text, tc.Font);
                maxWidth = Math.Max(maxWidth, sz.Width + 28);
            }
            tc.ItemSize = new Size(maxWidth, Math.Max(26, tc.Font.Height + 12));
            tc.Invalidate();
        }

        private static void OnTabDrawItem(object sender, DrawItemEventArgs e)
        {
            TabControl tc = sender as TabControl;
            ThemePalette p = Current;
            if (tc == null || p == null || p.IsClassic || e.Index < 0 || e.Index >= tc.TabPages.Count)
                return;
            DrawTab(e.Graphics, tc, e.Index, e.Bounds);
        }

        private static void DrawTab(Graphics g, TabControl tc, int index, Rectangle r)
        {
            ThemePalette p = Current;
            bool selected = index == tc.SelectedIndex;
            using (SolidBrush back = new SolidBrush(selected ? p.Window : p.Surface))
            {
                g.FillRectangle(back, r);
            }
            if (selected)
            {
                using (SolidBrush accent = new SolidBrush(p.Accent))
                {
                    g.FillRectangle(accent, new Rectangle(r.X, r.Y, r.Width, 2));
                }
            }
            TextRenderer.DrawText(g, tc.TabPages[index].Text, tc.Font, r,
                selected ? p.Text : p.TextDim,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        // --------------------------------------------------------------------------------
        // Docking panel (WeifenLuo) - done by reflection so that it does not depend on the exact version
        // --------------------------------------------------------------------------------

        private static void ApplyDockPanel(Control dock)
        {
            ThemePalette p = Current;
            dock.BackColor = p.Window;
            dock.ForeColor = p.Text;
            // the area between panes / behind the splitters
            SetProp(dock, "DockBackColor", p.Window);
            // windows that are hidden or not yet docked are not part of the control tree
            try
            {
                System.Collections.IEnumerable contents = GetProp(dock, "Contents") as System.Collections.IEnumerable;
                if (contents != null)
                {
                    foreach (object content in contents)
                    {
                        Form contentForm = content as Form;
                        if (contentForm != null && !contentForm.IsDisposed)
                            Apply(contentForm);
                    }
                }
            }
            catch (Exception swallowed)
            {
                Ares.Settings.ErrorLog.Note(swallowed);
            }
            object skin = GetProp(dock, "Skin");
            if (skin == null)
                return;

            // documents and tool windows tab strips
            foreach (string kind in new string[] { "DocumentGradient", "ToolWindowGradient" })
            {
                string basePath = "DockPaneStripSkin." + kind + ".";
                SetGradient(skin, basePath + "DockStripGradient", p.Window, p.Window, p.TextDim);
                SetGradient(skin, basePath + "ActiveTabGradient", p.Surface, p.Surface, p.Text);
                SetGradient(skin, basePath + "InactiveTabGradient", p.Window, p.Window, p.TextDim);
            }
            SetGradient(skin, "DockPaneStripSkin.ToolWindowGradient.ActiveCaptionGradient", p.Selection, p.Selection, p.Text);
            SetGradient(skin, "DockPaneStripSkin.ToolWindowGradient.InactiveCaptionGradient", p.Surface, p.Surface, p.TextDim);

            // auto hide strip
            SetGradient(skin, "AutoHideStripSkin.DockStripGradient", p.Window, p.Window, p.TextDim);
            SetGradient(skin, "AutoHideStripSkin.TabGradient", p.Surface, p.Surface, p.Text);

            dock.Invalidate(true);
        }

        private static void SetGradient(object root, string path, Color start, Color end, Color text)
        {
            object g = GetPath(root, path);
            if (g == null)
                return;
            SetProp(g, "StartColor", start);
            SetProp(g, "EndColor", end);
            SetProp(g, "TextColor", text);
        }

        private static object GetPath(object root, string path)
        {
            object o = root;
            foreach (string part in path.Split('.'))
            {
                o = GetProp(o, part);
                if (o == null)
                    return null;
            }
            return o;
        }

        private static object GetProp(object o, string name)
        {
            try
            {
                PropertyInfo pi = o.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                return pi == null ? null : pi.GetValue(o, null);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void SetProp(object o, string name, object value)
        {
            try
            {
                PropertyInfo pi = o.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                if (pi != null && pi.CanWrite)
                    pi.SetValue(o, value, null);
            }
            catch (Exception swallowed)
            {
                Ares.Settings.ErrorLog.Note(swallowed);
            }
        }

        // --------------------------------------------------------------------------------
        // Native (Windows) dark mode support: scroll bars, title bar
        // --------------------------------------------------------------------------------

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string pszSubIdList);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private static void ApplyNativeTheme(Control c)
        {
            try
            {
                if (!c.IsHandleCreated)
                    return;
                bool dark = !Current.IsClassic && Current.IsDark;
                if (c is ListView)
                {
                    SetWindowTheme(c.Handle, dark ? "DarkMode_ItemsView" : "Explorer", null);
                    // the column header is a window of its own: it is white unless it gets the dark theme too
                    IntPtr header = SendMessage(c.Handle, 0x101F, IntPtr.Zero, IntPtr.Zero);
                    if (header != IntPtr.Zero)
                        SetWindowTheme(header, dark ? "DarkMode_ItemsView" : "Explorer", null);
                }
                else if (c is TreeView || c is ListBox || c is TextBoxBase || c is Panel)
                {
                    SetWindowTheme(c.Handle, dark ? "DarkMode_Explorer" : "Explorer", null);
                }
                else if (c is ComboBox)
                {
                    SetWindowTheme(c.Handle, dark ? "DarkMode_CFD" : "Explorer", null);
                }
            }
            catch (Exception swallowed)
            {
                Ares.Settings.ErrorLog.Note(swallowed);
            }
        }

        private static void SetDarkTitleBar(Form f)
        {
            try
            {
                if (!f.TopLevel || !f.IsHandleCreated)
                    return;
                int value = (!Current.IsClassic && Current.IsDark) ? 1 : 0;
                // 20 = DWMWA_USE_IMMERSIVE_DARK_MODE (Windows 10 20H1+), 19 for older builds
                if (DwmSetWindowAttribute(f.Handle, 20, ref value, sizeof(int)) != 0)
                    DwmSetWindowAttribute(f.Handle, 19, ref value, sizeof(int));
            }
            catch (Exception swallowed)
            {
                Ares.Settings.ErrorLog.Note(swallowed);
            }
        }

        // --------------------------------------------------------------------------------
        // Menus and toolbars
        // --------------------------------------------------------------------------------

        private static void UpdateRenderer()
        {
            if (Current.IsClassic)
                ToolStripManager.Renderer = new ToolStripProfessionalRenderer();
            else
                ToolStripManager.Renderer = new ThemedRenderer();
        }

        private sealed class ThemedColorTable : ProfessionalColorTable
        {
            public ThemedColorTable()
            {
                UseSystemColors = false;
            }
            private static ThemePalette P { get { return Current; } }

            public override Color MenuItemSelected { get { return P.Hover; } }
            public override Color MenuItemSelectedGradientBegin { get { return P.Hover; } }
            public override Color MenuItemSelectedGradientEnd { get { return P.Hover; } }
            public override Color MenuItemPressedGradientBegin { get { return P.Surface; } }
            public override Color MenuItemPressedGradientMiddle { get { return P.Surface; } }
            public override Color MenuItemPressedGradientEnd { get { return P.Surface; } }
            public override Color MenuItemBorder { get { return P.Border; } }
            public override Color MenuBorder { get { return P.Border; } }
            public override Color ToolStripDropDownBackground { get { return P.Surface; } }
            public override Color ImageMarginGradientBegin { get { return P.Surface; } }
            public override Color ImageMarginGradientMiddle { get { return P.Surface; } }
            public override Color ImageMarginGradientEnd { get { return P.Surface; } }
            public override Color MenuStripGradientBegin { get { return P.Window; } }
            public override Color MenuStripGradientEnd { get { return P.Window; } }
            public override Color ToolStripGradientBegin { get { return P.Window; } }
            public override Color ToolStripGradientMiddle { get { return P.Window; } }
            public override Color ToolStripGradientEnd { get { return P.Window; } }
            public override Color ToolStripBorder { get { return P.Border; } }
            public override Color ToolStripContentPanelGradientBegin { get { return P.Window; } }
            public override Color ToolStripContentPanelGradientEnd { get { return P.Window; } }
            public override Color ToolStripPanelGradientBegin { get { return P.Window; } }
            public override Color ToolStripPanelGradientEnd { get { return P.Window; } }
            public override Color StatusStripGradientBegin { get { return P.Window; } }
            public override Color StatusStripGradientEnd { get { return P.Window; } }
            public override Color ButtonSelectedHighlight { get { return P.Hover; } }
            public override Color ButtonSelectedHighlightBorder { get { return P.Border; } }
            public override Color ButtonSelectedGradientBegin { get { return P.Hover; } }
            public override Color ButtonSelectedGradientMiddle { get { return P.Hover; } }
            public override Color ButtonSelectedGradientEnd { get { return P.Hover; } }
            public override Color ButtonSelectedBorder { get { return P.Border; } }
            public override Color ButtonPressedHighlight { get { return P.Selection; } }
            public override Color ButtonPressedHighlightBorder { get { return P.Accent; } }
            public override Color ButtonPressedGradientBegin { get { return P.Selection; } }
            public override Color ButtonPressedGradientMiddle { get { return P.Selection; } }
            public override Color ButtonPressedGradientEnd { get { return P.Selection; } }
            public override Color ButtonPressedBorder { get { return P.Accent; } }
            public override Color ButtonCheckedHighlight { get { return P.Selection; } }
            public override Color ButtonCheckedHighlightBorder { get { return P.Accent; } }
            public override Color ButtonCheckedGradientBegin { get { return P.Selection; } }
            public override Color ButtonCheckedGradientMiddle { get { return P.Selection; } }
            public override Color ButtonCheckedGradientEnd { get { return P.Selection; } }
            public override Color CheckBackground { get { return P.Selection; } }
            public override Color CheckSelectedBackground { get { return P.Selection; } }
            public override Color CheckPressedBackground { get { return P.Accent; } }
            public override Color SeparatorDark { get { return P.Border; } }
            public override Color SeparatorLight { get { return P.Border; } }
            public override Color GripDark { get { return P.TextDim; } }
            public override Color GripLight { get { return P.Border; } }
            public override Color OverflowButtonGradientBegin { get { return P.Window; } }
            public override Color OverflowButtonGradientMiddle { get { return P.Window; } }
            public override Color OverflowButtonGradientEnd { get { return P.Window; } }
            public override Color RaftingContainerGradientBegin { get { return P.Window; } }
            public override Color RaftingContainerGradientEnd { get { return P.Window; } }
        }

        // --------------------------------------------------------------------------------
        // lists with a grid: the native grid is bright white in the dark themes, so such lists
        // are drawn by hand with a soft line below each row (like the track list)
        // --------------------------------------------------------------------------------

        private static readonly ConditionalWeakTable<ListView, object> s_SoftGrid = new ConditionalWeakTable<ListView, object>();

        // the header of a list: dark, with light letters (the native header stays white with dark letters)
        private static void OnDrawThemedHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            ThemePalette pal = Current;
            using (SolidBrush back = new SolidBrush(pal.Window))
                e.Graphics.FillRectangle(back, e.Bounds);
            using (Pen line = new Pen(Color.FromArgb(110, pal.Border)))
            {
                e.Graphics.DrawLine(line, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
                e.Graphics.DrawLine(line, e.Bounds.Right - 1, e.Bounds.Top + 4, e.Bounds.Right - 1, e.Bounds.Bottom - 5);
            }
            HorizontalAlignment align = e.Header.TextAlign;
            TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix
                | (align == HorizontalAlignment.Right ? TextFormatFlags.Right : align == HorizontalAlignment.Center ? TextFormatFlags.HorizontalCenter : TextFormatFlags.Left);
            Rectangle tr = new Rectangle(e.Bounds.X + 6, e.Bounds.Y, Math.Max(0, e.Bounds.Width - 12), e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, e.Header.Text, ((ListView)sender).Font, tr, pal.Text, flags);
        }

        // lists without a grid (the track list) keep their normal drawing, only the header is drawn by hand
        private static readonly ConditionalWeakTable<ListView, object> s_HeaderLook = new ConditionalWeakTable<ListView, object>();

        private static void ApplyHeaderLook(ListView lv)
        {
            if (lv.View != View.Details || lv.OwnerDraw || lv.CheckBoxes || lv.Groups.Count > 0)
                return;
            object dummy;
            if (s_HeaderLook.TryGetValue(lv, out dummy))
                return;
            s_HeaderLook.Add(lv, new object());
            lv.OwnerDraw = true;
            lv.DrawColumnHeader += OnDrawThemedHeader;
            lv.DrawItem += delegate (object sender, DrawListViewItemEventArgs e) { e.DrawDefault = true; };
            lv.DrawSubItem += delegate (object sender, DrawListViewSubItemEventArgs e) { e.DrawDefault = true; };
        }

        private static void ApplySoftGrid(ListView lv)
        {
            if (!lv.GridLines || lv.View != View.Details)
                return;
            object dummy;
            if (s_SoftGrid.TryGetValue(lv, out dummy))
                return;
            s_SoftGrid.Add(lv, new object());
            lv.GridLines = false;
            lv.OwnerDraw = true;
            lv.DrawItem += delegate (object sender, DrawListViewItemEventArgs e) { };
            lv.DrawColumnHeader += OnDrawThemedHeader;
            lv.DrawSubItem += delegate (object sender, DrawListViewSubItemEventArgs e)
            {
                ThemePalette pal = Current;
                ListView list = (ListView)sender;
                Rectangle b = e.Bounds;
                if (e.ColumnIndex == 0 && list.Columns.Count > 0)
                    b = new Rectangle(b.X, b.Y, list.Columns[0].Width, b.Height);
                using (SolidBrush back = new SolidBrush(e.Item.Selected ? pal.Selection : pal.Surface))
                    e.Graphics.FillRectangle(back, b);
                using (Pen line = new Pen(Color.FromArgb(70, pal.Border)))
                    e.Graphics.DrawLine(line, b.Left, b.Bottom - 1, b.Right, b.Bottom - 1);
                int textLeft = b.X + 6;
                if (e.ColumnIndex == 0 && list.CheckBoxes)
                {
                    Rectangle box = new Rectangle(b.X + 4, b.Y + (b.Height - 13) / 2, 13, 13);
                    using (SolidBrush boxBack = new SolidBrush(pal.Window))
                        e.Graphics.FillRectangle(boxBack, box);
                    using (Pen boxLine = new Pen(pal.TextDim))
                        e.Graphics.DrawRectangle(boxLine, box.X, box.Y, box.Width - 1, box.Height - 1);
                    if (e.Item.Checked)
                    {
                        System.Drawing.Drawing2D.SmoothingMode old = e.Graphics.SmoothingMode;
                        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                        using (Pen tick = new Pen(pal.Accent, 2f))
                        {
                            e.Graphics.DrawLine(tick, box.X + 3, box.Y + 7, box.X + 5, box.Y + 9);
                            e.Graphics.DrawLine(tick, box.X + 5, box.Y + 9, box.X + 10, box.Y + 3);
                        }
                        e.Graphics.SmoothingMode = old;
                    }
                    textLeft = b.X + 24;
                }
                Rectangle tr = new Rectangle(textLeft, b.Y, Math.Max(0, b.Right - textLeft - 4), b.Height);
                TextRenderer.DrawText(e.Graphics, e.SubItem.Text, list.Font, tr, e.SubItem.ForeColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            };
        }

        private sealed class ThemedRenderer : ToolStripProfessionalRenderer
        {
            public ThemedRenderer()
                : base(new ThemedColorTable())
            {
                RoundedEdges = false;
            }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                e.TextColor = e.Item.Enabled ? Current.Text : Current.TextDim;
                base.OnRenderItemText(e);
            }

            protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
            {
                e.ArrowColor = e.Item.Enabled ? Current.Text : Current.TextDim;
                base.OnRenderArrow(e);
            }

            protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
            {
                ToolStripButton button = e.Item as ToolStripButton;
                if (button != null && button.Checked && button.Enabled)
                {
                    // clearly visible "on" state for toggle buttons (repeat, edit mode, ...)
                    Rectangle r = new Rectangle(1, 1, button.Width - 3, button.Height - 3);
                    using (SolidBrush fill = new SolidBrush(Color.FromArgb(120, Current.Accent)))
                        e.Graphics.FillRectangle(fill, r);
                    using (Pen border = new Pen(Current.Accent))
                        e.Graphics.DrawRectangle(border, r);
                    return;
                }
                base.OnRenderButtonBackground(e);
            }

            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                // no border around menu strips / toolbars / status strips
                if (e.ToolStrip is ToolStripDropDown)
                    base.OnRenderToolStripBorder(e);
            }
        }

        // --------------------------------------------------------------------------------
        // menu texts without the trailing "..." (also for items which are added later)
        // --------------------------------------------------------------------------------

        private static readonly ConditionalWeakTable<object, object> s_MenuHooked = new ConditionalWeakTable<object, object>();

        private static void HookMenus(Control c)
        {
            try
            {
                ToolStrip ts = c as ToolStrip;
                if (ts != null)
                    HookStrip(ts);
                if (c.ContextMenuStrip != null)
                    HookStrip(c.ContextMenuStrip);
            }
            catch (Exception swallowed)
            {
                Ares.Settings.ErrorLog.Note(swallowed);
            }
        }

        private static void HookStrip(ToolStrip strip)
        {
            object dummy;
            if (s_MenuHooked.TryGetValue(strip, out dummy))
                return;
            s_MenuHooked.Add(strip, new object());
            StripEllipsis(strip.Items);
            TidyMenu(strip as ToolStripDropDown);
            ContextMenuStrip cms = strip as ContextMenuStrip;
            if (cms != null)
                cms.Opening += delegate { StripEllipsis(cms.Items); TidyMenu(cms); };
        }

        private static void StripEllipsis(ToolStripItemCollection items)
        {
            foreach (ToolStripItem item in items)
            {
                string text = item.Text;
                if (!string.IsNullOrEmpty(text) && item is ToolStripMenuItem)
                {
                    string trimmed = text;
                    if (trimmed.EndsWith("...", StringComparison.Ordinal))
                        trimmed = trimmed.Substring(0, trimmed.Length - 3).TrimEnd();
                    else if (trimmed.EndsWith("\u2026", StringComparison.Ordinal))
                        trimmed = trimmed.Substring(0, trimmed.Length - 1).TrimEnd();
                    if (trimmed != text && trimmed.Length > 0)
                        item.Text = trimmed;
                }
                ToolStripDropDownItem dd = item as ToolStripDropDownItem;
                if (dd != null && dd.HasDropDownItems)
                {
                    object dummy;
                    if (!s_MenuHooked.TryGetValue(dd, out dummy))
                    {
                        s_MenuHooked.Add(dd, new object());
                        ToolStripDropDownItem captured = dd;
                        dd.DropDownOpening += delegate { StripEllipsis(captured.DropDownItems); TidyMenu(captured.DropDown); };
                    }
                    StripEllipsis(dd.DropDownItems);
                    TidyMenu(dd.DropDown);
                }
            }
        }

        // Menus without icons: only a few items had one and the empty column in front of the others looked broken.
        // The column is only kept (for the check mark) when some item can be checked.
        private static void TidyMenu(ToolStripDropDown drop)
        {
            ToolStripDropDownMenu menu = drop as ToolStripDropDownMenu;
            if (menu == null)
                return;
            bool anyCheck = false;
            foreach (ToolStripItem item in menu.Items)
            {
                ToolStripMenuItem mi = item as ToolStripMenuItem;
                if (mi != null && (mi.CheckOnClick || mi.Checked))
                    anyCheck = true;
            }
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = anyCheck;
        }

        // --------------------------------------------------------------------------------
        // "Appearance" menu, added automatically to the main menu of each window
        // --------------------------------------------------------------------------------

        private static void AddAppearanceMenu(Form form)
        {
            try
            {
                MenuStrip menu = form.MainMenuStrip;
                if (menu == null || !form.TopLevel)
                    return;
                object dummy;
                if (s_MenuAdded.TryGetValue(menu, out dummy))
                    return;
                s_MenuAdded.Add(menu, new object());

                bool pt = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt";
                ToolStripMenuItem root = new ToolStripMenuItem(pt ? "Aparência" : "Appearance");

                foreach (ThemePalette palette in s_Palettes)
                {
                    ThemePalette captured = palette;
                    ToolStripMenuItem item = new ToolStripMenuItem(palette.DisplayName);
                    item.Tag = palette.Id;
                    if (captured.Id == "custom")
                        item.Click += delegate { CustomThemeDialog.Edit(form); };
                    else
                        item.Click += delegate { SetTheme(captured.Id); };
                    root.DropDownItems.Add(item);
                }
                root.DropDownItems.Add(new ToolStripSeparator());
                ToolStripMenuItem sizeRoot = new ToolStripMenuItem(pt ? "Tamanho da interface" : "Interface size");
                foreach (int s in s_ScaleOptions)
                {
                    int captured = s;
                    ToolStripMenuItem item = new ToolStripMenuItem(s + "%");
                    item.Tag = s;
                    item.Click += delegate { SetScale(captured); };
                    sizeRoot.DropDownItems.Add(item);
                }
                root.DropDownItems.Add(sizeRoot);

                EventHandler refresh = delegate
                {
                    foreach (ToolStripItem i in root.DropDownItems)
                    {
                        ToolStripMenuItem mi = i as ToolStripMenuItem;
                        if (mi != null && mi.Tag is string)
                            mi.Checked = (string)mi.Tag == Current.Id;
                    }
                    foreach (ToolStripItem i in sizeRoot.DropDownItems)
                    {
                        ToolStripMenuItem mi = i as ToolStripMenuItem;
                        if (mi != null && mi.Tag is int)
                            mi.Checked = (int)mi.Tag == ScalePercent;
                    }
                };
                root.DropDownOpening += refresh;
                sizeRoot.DropDownOpening += refresh;

                menu.Items.Add(root);
                if (AfterAppearanceMenu != null)
                    AfterAppearanceMenu(form, menu);
            }
            catch (Exception swallowed)
            {
                Ares.Settings.ErrorLog.Note(swallowed);
            }
        }

        /// <summary>
        /// The color which is really seen behind a control: the first opaque background above it. (Group boxes are
        /// transparent and draw their own surface, so painting with their BackColor would give black in light themes.)
        /// </summary>
        public static Color EffectiveBack(Control c)
        {
            ThemePalette p = Current;
            bool themed = p != null && !p.IsClassic;
            for (Control x = c != null ? c.Parent : null; x != null; x = x.Parent)
            {
                if (x is GroupBox && themed)
                    return p.Window;
                if (x.BackColor.A == 255)
                    return x.BackColor;
            }
            return themed ? p.Window : SystemColors.Control;
        }

        /// <summary>Called after the Appearance menu was added to a window, so that further top-level menu entries can follow it.</summary>
        public static Action<Form, MenuStrip> AfterAppearanceMenu;
    }
}
﻿
