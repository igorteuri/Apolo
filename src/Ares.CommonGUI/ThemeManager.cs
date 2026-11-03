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
using System.Drawing.Drawing2D;
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
    public static partial class ThemeManager
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
                else if (b.Tag is string && (string)b.Tag == "Danger")
                {
                    // a switch which is "on" and means trouble or silence (the muted tab): red with white text
                    b.BackColor = Color.FromArgb(200, 52, 52);
                    b.ForeColor = Color.White;
                    b.FlatAppearance.MouseOverBackColor = Color.FromArgb(222, 84, 84);
                    b.FlatAppearance.MouseDownBackColor = Color.FromArgb(170, 40, 40);
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
                UpDownBase upDown = c as UpDownBase;
                if (upDown != null)
                    HookUpDownBorder(upDown);
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
                HookSelectAll(lv);
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
                HookSelectAll(lb);
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
