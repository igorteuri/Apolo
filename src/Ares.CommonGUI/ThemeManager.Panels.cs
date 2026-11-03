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
    // Tab controls (owner drawn headers) and the docking panels.
    public static partial class ThemeManager
    {
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
    }
}
