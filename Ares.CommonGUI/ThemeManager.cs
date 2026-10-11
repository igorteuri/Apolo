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
            new ThemePalette { Id = "classic", NamePt = "Clássico (Windows)", NameEn = "Classic (Windows)", IsClassic = true },
            new ThemePalette
            {
                Id = "dark", NamePt = "Escuro", NameEn = "Dark", IsDark = true,
                Window = ThemePalette.C(0x1B1D21), Surface = ThemePalette.C(0x272A30), Hover = ThemePalette.C(0x3A3F48),
                Border = ThemePalette.C(0x6B7280), Text = ThemePalette.C(0xE6E8EB), TextDim = ThemePalette.C(0x8A9099),
                Accent = ThemePalette.C(0x4C9AFF), Selection = ThemePalette.C(0x2F5D9E)
            },
            new ThemePalette
            {
                Id = "midnight", NamePt = "Meia-noite", NameEn = "Midnight", IsDark = true,
                Window = ThemePalette.C(0x0F1420), Surface = ThemePalette.C(0x182033), Hover = ThemePalette.C(0x26365A),
                Border = ThemePalette.C(0x5B77B0), Text = ThemePalette.C(0xE3EAF7), TextDim = ThemePalette.C(0x7F8CA8),
                Accent = ThemePalette.C(0x6EA8FE), Selection = ThemePalette.C(0x2A4A86)
            },
            new ThemePalette
            {
                Id = "violet", NamePt = "Violeta", NameEn = "Violet", IsDark = true,
                Window = ThemePalette.C(0x17121F), Surface = ThemePalette.C(0x211A2E), Hover = ThemePalette.C(0x33284A),
                Border = ThemePalette.C(0x7B63B5), Text = ThemePalette.C(0xEAE4F5), TextDim = ThemePalette.C(0x9187A8),
                Accent = ThemePalette.C(0xB488FF), Selection = ThemePalette.C(0x5A3E96)
            },
            new ThemePalette
            {
                Id = "light", NamePt = "Claro moderno", NameEn = "Modern light", IsDark = false,
                Window = ThemePalette.C(0xF5F6F8), Surface = ThemePalette.C(0xFFFFFF), Hover = ThemePalette.C(0xE6E9EE),
                Border = ThemePalette.C(0xCDD2DA), Text = ThemePalette.C(0x1F2328), TextDim = ThemePalette.C(0x6B7280),
                Accent = ThemePalette.C(0x2563EB), Selection = ThemePalette.C(0xBFD7FF)
            }
        };

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

        internal static void Log(string where, Exception ex)
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Ares");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "theme.log"),
                    DateTime.Now.ToString("HH:mm:ss") + " " + where + ": " + (ex == null ? "" : ex.GetType().Name + " " + ex.Message) + Environment.NewLine);
            }
            catch (Exception)
            {
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
            Current = s_Palettes.First(p => p.Id == "dark");
            ScalePercent = 110;
            LoadPreferences();
            UpdateRenderer();
            Application.Idle += OnApplicationIdle;
            Log("install: theme=" + Current.Id + " scale=" + ScalePercent, null);
        }

        private static string PreferencesFile
        {
            get
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Ares");
                return Path.Combine(dir, "appearance.ini");
            }
        }

        private static void LoadPreferences()
        {
            try
            {
                if (!File.Exists(PreferencesFile))
                    return;
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
                    else if (key == "scale")
                    {
                        int s;
                        if (int.TryParse(value, out s) && s >= 80 && s <= 200)
                            ScalePercent = s;
                    }
                }
            }
            catch (Exception)
            {
                // use defaults
            }
        }

        private static void SavePreferences()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PreferencesFile));
                File.WriteAllLines(PreferencesFile, new string[] { "theme=" + Current.Id, "scale=" + ScalePercent });
            }
            catch (Exception)
            {
                // not critical
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
                }
                AddAppearanceMenu(f);
            }
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
                b.FlatAppearance.BorderColor = p.Border;
                b.FlatAppearance.MouseOverBackColor = p.Hover;
                b.FlatAppearance.MouseDownBackColor = p.Selection;
                b.BackColor = p.Surface;
                b.ForeColor = p.Text;
            }
            else if (c is CheckBox && ((CheckBox)c).Appearance == Appearance.Button
                || c is RadioButton && ((RadioButton)c).Appearance == Appearance.Button)
            {
                // toggle buttons (used for the element buttons of the player)
                ButtonBase tb2 = (ButtonBase)c;
                tb2.UseVisualStyleBackColor = false;
                tb2.FlatStyle = FlatStyle.Flat;
                tb2.FlatAppearance.BorderSize = 1;
                tb2.FlatAppearance.BorderColor = p.Border;
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
            }
            else if (c is TextBoxBase || c is NumericUpDown || c is DomainUpDown)
            {
                c.BackColor = p.Surface;
                c.ForeColor = p.Text;
                TextBox tb = c as TextBox;
                if (tb != null)
                    tb.BorderStyle = BorderStyle.FixedSingle;
            }
            else if (c is ComboBox)
            {
                ComboBox cb = (ComboBox)c;
                cb.FlatStyle = FlatStyle.Flat;
                cb.BackColor = p.Surface;
                cb.ForeColor = p.Text;
            }
            else if (c is ListView)
            {
                ListView lv = (ListView)c;
                lv.BackColor = p.Surface;
                lv.ForeColor = p.Text;
                lv.BorderStyle = BorderStyle.FixedSingle;
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
            catch (Exception)
            {
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
                    lbx.BorderStyle = o.BorderStyle;
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
            if (Math.Abs(c.Font.Size - size) < 0.01f)
                return;
            try
            {
                c.Font = new Font(orig.Font.FontFamily, size, orig.Font.Style, orig.Font.Unit);
            }
            catch (Exception)
            {
            }
        }

        private static void ApplyToolStripScale(ToolStrip ts, OriginalState orig)
        {
            try
            {
                float factor = ScalePercent / 100f;
                Size target = new Size(
                    (int)Math.Round(orig.ToolStripImageSize.Width * factor),
                    (int)Math.Round(orig.ToolStripImageSize.Height * factor));
                if (ts.ImageScalingSize != target)
                    ts.ImageScalingSize = target;
            }
            catch (Exception)
            {
            }
        }

        // --------------------------------------------------------------------------------
        // Group boxes: the Windows border is always light gray, so we draw it ourselves
        // --------------------------------------------------------------------------------

        private static readonly ConditionalWeakTable<object, object> s_GroupPaint = new ConditionalWeakTable<object, object>();

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
            using (Pen pen = new Pen(p.Border))
            {
                gr.DrawRectangle(pen, 0, top, g.Width - 1, g.Height - top - 1);
            }
            if (hasText)
            {
                int x = 8;
                using (SolidBrush back = new SolidBrush(p.Window))
                {
                    gr.FillRectangle(back, x - 2, 0, ts.Width + 2, ts.Height);
                }
                TextRenderer.DrawText(gr, g.Text, g.Font, new Point(x, 0), p.Text);
            }
        }

        // --------------------------------------------------------------------------------
        // Tab control (owner drawn tab headers)
        // --------------------------------------------------------------------------------

        private static void ApplyTabControl(TabControl tc)
        {
            ThemePalette p = Current;
            tc.ForeColor = p.Text;
            tc.DrawItem -= OnTabDrawItem;
            tc.DrawItem += OnTabDrawItem;
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
            bool selected = e.Index == tc.SelectedIndex;
            Rectangle r = e.Bounds;
            using (SolidBrush back = new SolidBrush(selected ? p.Window : p.Surface))
            {
                e.Graphics.FillRectangle(back, r);
            }
            if (selected)
            {
                using (SolidBrush accent = new SolidBrush(p.Accent))
                {
                    e.Graphics.FillRectangle(accent, new Rectangle(r.X, r.Y, r.Width, 2));
                }
            }
            TextRenderer.DrawText(e.Graphics, tc.TabPages[e.Index].Text, tc.Font, r,
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
            catch (Exception)
            {
            }
        }

        // --------------------------------------------------------------------------------
        // Native (Windows) dark mode support: scroll bars, title bar
        // --------------------------------------------------------------------------------

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string pszSubIdList);

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
                }
                else if (c is TreeView || c is ListBox || c is TextBoxBase || c is ScrollableControl && c.GetType() == typeof(Panel))
                {
                    SetWindowTheme(c.Handle, dark ? "DarkMode_Explorer" : "Explorer", null);
                }
                else if (c is ComboBox)
                {
                    SetWindowTheme(c.Handle, dark ? "DarkMode_CFD" : "Explorer", null);
                }
            }
            catch (Exception)
            {
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
            catch (Exception)
            {
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

            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                // no border around menu strips / toolbars / status strips
                if (e.ToolStrip is ToolStripDropDown)
                    base.OnRenderToolStripBorder(e);
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
            }
            catch (Exception)
            {
            }
        }
    }
}
﻿/*
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
            new ThemePalette { Id = "classic", NamePt = "Clássico (Windows)", NameEn = "Classic (Windows)", IsClassic = true },
            new ThemePalette
            {
                Id = "dark", NamePt = "Escuro", NameEn = "Dark", IsDark = true,
                Window = ThemePalette.C(0x1B1D21), Surface = ThemePalette.C(0x272A30), Hover = ThemePalette.C(0x343840),
                Border = ThemePalette.C(0x3F434C), Text = ThemePalette.C(0xE6E8EB), TextDim = ThemePalette.C(0x8A9099),
                Accent = ThemePalette.C(0x4C9AFF), Selection = ThemePalette.C(0x2F5D9E)
            },
            new ThemePalette
            {
                Id = "midnight", NamePt = "Meia-noite", NameEn = "Midnight", IsDark = true,
                Window = ThemePalette.C(0x0F1420), Surface = ThemePalette.C(0x182033), Hover = ThemePalette.C(0x22304D),
                Border = ThemePalette.C(0x2B3A5C), Text = ThemePalette.C(0xE3EAF7), TextDim = ThemePalette.C(0x7F8CA8),
                Accent = ThemePalette.C(0x6EA8FE), Selection = ThemePalette.C(0x2A4A86)
            },
            new ThemePalette
            {
                Id = "violet", NamePt = "Violeta", NameEn = "Violet", IsDark = true,
                Window = ThemePalette.C(0x17121F), Surface = ThemePalette.C(0x211A2E), Hover = ThemePalette.C(0x2D2440),
                Border = ThemePalette.C(0x3A2F52), Text = ThemePalette.C(0xEAE4F5), TextDim = ThemePalette.C(0x9187A8),
                Accent = ThemePalette.C(0xB488FF), Selection = ThemePalette.C(0x5A3E96)
            },
            new ThemePalette
            {
                Id = "light", NamePt = "Claro moderno", NameEn = "Modern light", IsDark = false,
                Window = ThemePalette.C(0xF5F6F8), Surface = ThemePalette.C(0xFFFFFF), Hover = ThemePalette.C(0xE6E9EE),
                Border = ThemePalette.C(0xCDD2DA), Text = ThemePalette.C(0x1F2328), TextDim = ThemePalette.C(0x6B7280),
                Accent = ThemePalette.C(0x2563EB), Selection = ThemePalette.C(0xBFD7FF)
            }
        };

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

        internal static void Log(string where, Exception ex)
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Ares");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "theme.log"),
                    DateTime.Now.ToString("HH:mm:ss") + " " + where + ": " + (ex == null ? "" : ex.GetType().Name + " " + ex.Message) + Environment.NewLine);
            }
            catch (Exception)
            {
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
            Current = s_Palettes.First(p => p.Id == "dark");
            ScalePercent = 110;
            LoadPreferences();
            UpdateRenderer();
            Application.Idle += OnApplicationIdle;
            Log("install: theme=" + Current.Id + " scale=" + ScalePercent, null);
        }

        private static string PreferencesFile
        {
            get
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Ares");
                return Path.Combine(dir, "appearance.ini");
            }
        }

        private static void LoadPreferences()
        {
            try
            {
                if (!File.Exists(PreferencesFile))
                    return;
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
                    else if (key == "scale")
                    {
                        int s;
                        if (int.TryParse(value, out s) && s >= 80 && s <= 200)
                            ScalePercent = s;
                    }
                }
            }
            catch (Exception)
            {
                // use defaults
            }
        }

        private static void SavePreferences()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PreferencesFile));
                File.WriteAllLines(PreferencesFile, new string[] { "theme=" + Current.Id, "scale=" + ScalePercent });
            }
            catch (Exception)
            {
                // not critical
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
                }
                AddAppearanceMenu(f);
            }
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
                b.FlatAppearance.BorderColor = p.Border;
                b.FlatAppearance.MouseOverBackColor = p.Hover;
                b.FlatAppearance.MouseDownBackColor = p.Selection;
                b.BackColor = p.Surface;
                b.ForeColor = p.Text;
            }
            else if (c is CheckBox || c is RadioButton)
            {
                ButtonBase bb = (ButtonBase)c;
                if (bb.FlatStyle == FlatStyle.System)
                    bb.FlatStyle = FlatStyle.Standard;
                bb.UseVisualStyleBackColor = false;
                bb.BackColor = Color.Transparent;
                bb.ForeColor = p.Text;
            }
            else if (c is GroupBox)
            {
                GroupBox g = (GroupBox)c;
                if (g.FlatStyle == FlatStyle.System)
                    g.FlatStyle = FlatStyle.Standard;
                g.ForeColor = p.Text;
                g.BackColor = Color.Transparent;
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
            }
            else if (c is TextBoxBase || c is NumericUpDown || c is DomainUpDown)
            {
                c.BackColor = p.Surface;
                c.ForeColor = p.Text;
                TextBox tb = c as TextBox;
                if (tb != null)
                    tb.BorderStyle = BorderStyle.FixedSingle;
            }
            else if (c is ComboBox)
            {
                ComboBox cb = (ComboBox)c;
                cb.FlatStyle = FlatStyle.Flat;
                cb.BackColor = p.Surface;
                cb.ForeColor = p.Text;
            }
            else if (c is ListView)
            {
                ListView lv = (ListView)c;
                lv.BackColor = p.Surface;
                lv.ForeColor = p.Text;
                lv.BorderStyle = BorderStyle.FixedSingle;
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
            else if (c is ProgressBar || c is TrackBar || c is PictureBox || c is MenuStrip)
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
            catch (Exception)
            {
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
                    lbx.BorderStyle = o.BorderStyle;
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
            if (Math.Abs(c.Font.Size - size) < 0.01f)
                return;
            try
            {
                c.Font = new Font(orig.Font.FontFamily, size, orig.Font.Style, orig.Font.Unit);
            }
            catch (Exception)
            {
            }
        }

        private static void ApplyToolStripScale(ToolStrip ts, OriginalState orig)
        {
            try
            {
                float factor = ScalePercent / 100f;
                Size target = new Size(
                    (int)Math.Round(orig.ToolStripImageSize.Width * factor),
                    (int)Math.Round(orig.ToolStripImageSize.Height * factor));
                if (ts.ImageScalingSize != target)
                    ts.ImageScalingSize = target;
            }
            catch (Exception)
            {
            }
        }

        // --------------------------------------------------------------------------------
        // Tab control (owner drawn tab headers)
        // --------------------------------------------------------------------------------

        private static void ApplyTabControl(TabControl tc)
        {
            ThemePalette p = Current;
            tc.ForeColor = p.Text;
            tc.DrawItem -= OnTabDrawItem;
            tc.DrawItem += OnTabDrawItem;
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
            bool selected = e.Index == tc.SelectedIndex;
            Rectangle r = e.Bounds;
            using (SolidBrush back = new SolidBrush(selected ? p.Window : p.Surface))
            {
                e.Graphics.FillRectangle(back, r);
            }
            if (selected)
            {
                using (SolidBrush accent = new SolidBrush(p.Accent))
                {
                    e.Graphics.FillRectangle(accent, new Rectangle(r.X, r.Y, r.Width, 2));
                }
            }
            TextRenderer.DrawText(e.Graphics, tc.TabPages[e.Index].Text, tc.Font, r,
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
            catch (Exception)
            {
            }
        }

        // --------------------------------------------------------------------------------
        // Native (Windows) dark mode support: scroll bars, title bar
        // --------------------------------------------------------------------------------

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string pszSubIdList);

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
                }
                else if (c is TreeView || c is ListBox || c is TextBoxBase || c is ScrollableControl && c.GetType() == typeof(Panel))
                {
                    SetWindowTheme(c.Handle, dark ? "DarkMode_Explorer" : "Explorer", null);
                }
                else if (c is ComboBox)
                {
                    SetWindowTheme(c.Handle, dark ? "DarkMode_CFD" : "Explorer", null);
                }
            }
            catch (Exception)
            {
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
            catch (Exception)
            {
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

            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                // no border around menu strips / toolbars / status strips
                if (e.ToolStrip is ToolStripDropDown)
                    base.OnRenderToolStripBorder(e);
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
            }
            catch (Exception)
            {
            }
        }
    }
}
