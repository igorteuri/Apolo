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
    // Native (Windows) dark mode: scroll bars, title bar, up-down and drop-down frames.
    public static partial class ThemeManager
    {
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
                    // DarkMode_Explorer also gives the list dark scroll bars (with DarkMode_ItemsView they stayed white)
                    SetWindowTheme(c.Handle, dark ? "DarkMode_Explorer" : "Explorer", null);
                    // the column header is a window of its own: it is white unless it gets the dark theme too
                    IntPtr header = SendMessage(c.Handle, 0x101F, IntPtr.Zero, IntPtr.Zero);
                    if (header != IntPtr.Zero)
                        SetWindowTheme(header, dark ? "DarkMode_ItemsView" : "Explorer", null);
                }
                else if (c is TreeView || c is ListBox || c is TextBoxBase || c is Panel || c is UserControl)
                {
                    SetWindowTheme(c.Handle, dark ? "DarkMode_Explorer" : "Explorer", null);
                }
                else if (c is ComboBox)
                {
                    SetWindowTheme(c.Handle, dark ? "DarkMode_CFD" : "Explorer", null);
                    HookDropList((ComboBox)c);
                }
            }
            catch (Exception swallowed)
            {
                Ares.Settings.ErrorLog.Note(swallowed);
            }
        }

        // number boxes draw their own frame in the text color (white on the dark theme): painted over in the border color,
        // and made wide enough for the biggest value (the digits were cut off)
        private sealed class UpDownFrame : NativeWindow
        {
            private readonly UpDownBase m_Box;
            private bool m_Attached;

            public UpDownFrame(UpDownBase box)
            {
                m_Box = box;
            }

            public void Attach()
            {
                if (!m_Attached && m_Box.IsHandleCreated)
                {
                    AssignHandle(m_Box.Handle);
                    m_Attached = true;
                }
            }

            public void Detach()
            {
                if (m_Attached)
                {
                    ReleaseHandle();
                    m_Attached = false;
                }
            }

            public void Redraw()
            {
                if (m_Attached && m_Box.IsHandleCreated)
                    RedrawWindow(m_Box.Handle, IntPtr.Zero, IntPtr.Zero, 0x0401); // RDW_INVALIDATE | RDW_FRAME
            }

            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);
                if (m.Msg != 0x0085) // WM_NCPAINT
                    return;
                ThemePalette p = Current;
                if (p == null || p.IsClassic)
                    return;
                IntPtr dc = GetWindowDC(Handle);
                if (dc == IntPtr.Zero)
                    return;
                try
                {
                    using (Graphics g = Graphics.FromHdc(dc))
                    using (Pen pen = new Pen(m_Box.ContainsFocus ? p.Accent : p.ControlBorder))
                    {
                        g.DrawRectangle(pen, 0, 0, m_Box.Width - 1, m_Box.Height - 1);
                    }
                }
                finally
                {
                    ReleaseDC(Handle, dc);
                }
            }

            [DllImport("user32.dll")]
            private static extern IntPtr GetWindowDC(IntPtr hWnd);

            [DllImport("user32.dll")]
            private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

            [DllImport("user32.dll")]
            private static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprc, IntPtr hrgn, uint flags);
        }

        private static readonly ConditionalWeakTable<UpDownBase, object> s_UpDownHooked = new ConditionalWeakTable<UpDownBase, object>();

        private static void HookUpDownBorder(UpDownBase u)
        {
            NumericUpDown num = u as NumericUpDown;
            if (num != null)
            {
                int digits = Math.Max(num.Maximum.ToString().Length, num.Minimum.ToString().Length);
                int need = TextRenderer.MeasureText(new string('0', digits), num.Font).Width + SystemInformation.VerticalScrollBarWidth + 14;
                if (num.Width < need)
                    num.Width = need;
            }
            object marker;
            if (s_UpDownHooked.TryGetValue(u, out marker))
                return;
            s_UpDownHooked.Add(u, new object());
            u.BorderStyle = BorderStyle.FixedSingle;
            // the frame belongs to the non-client area of the window: it is painted there, over the light system frame
            UpDownFrame frame = new UpDownFrame(u);
            u.HandleCreated += (s, e) => frame.Attach();
            u.HandleDestroyed += (s, e) => frame.Detach();
            if (u.IsHandleCreated)
                frame.Attach();
            u.GotFocus += (s, e) => frame.Redraw();
            u.LostFocus += (s, e) => frame.Redraw();
            // the two small buttons are drawn by the system in light gray: they are painted over in the colors of the theme
            foreach (Control child in u.Controls)
            {
                if (child.GetType().Name != "UpDownButtons")
                    continue;
                Control spin = child;
                spin.Paint += (s, e) =>
                {
                    ThemePalette p = Current;
                    if (p == null || p.IsClassic)
                        return;
                    Graphics g = e.Graphics;
                    Rectangle r = spin.ClientRectangle;
                    using (SolidBrush back = new SolidBrush(p.Surface))
                        g.FillRectangle(back, r);
                    using (Pen line = new Pen(p.ControlBorder))
                    {
                        g.DrawLine(line, r.X, r.Y, r.X, r.Bottom);
                        g.DrawLine(line, r.X, r.Y + r.Height / 2, r.Right, r.Y + r.Height / 2);
                    }
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    using (SolidBrush arrow = new SolidBrush(p.Text))
                    {
                        float cx = r.X + (r.Width + 1) / 2f, h = r.Height / 2f, a = Math.Max(2.5f, Math.Min(r.Width / 4f, h / 3f));
                        g.FillPolygon(arrow, new[] { new PointF(cx - a, h / 2f + a / 2f), new PointF(cx + a, h / 2f + a / 2f), new PointF(cx, h / 2f - a / 2f) });
                        g.FillPolygon(arrow, new[] { new PointF(cx - a, h + h / 2f - a / 2f), new PointF(cx + a, h + h / 2f - a / 2f), new PointF(cx, h + h / 2f + a / 2f) });
                    }
                };
                spin.Invalidate();
            }
        }

        // the list which drops down from a combo box is a window of its own: its scroll bar stays white unless it gets the theme too
        [StructLayout(LayoutKind.Sequential)]
        private struct COMBOBOXINFO
        {
            public int cbSize;
            public Rectangle rcItem, rcButton;
            public int stateButton;
            public IntPtr hwndCombo, hwndItem, hwndList;
        }

        [DllImport("user32.dll")]
        private static extern bool GetComboBoxInfo(IntPtr hwnd, ref COMBOBOXINFO info);

        private static readonly ConditionalWeakTable<ComboBox, object> s_DropHooked = new ConditionalWeakTable<ComboBox, object>();

        private static void HookDropList(ComboBox cb)
        {
            object marker;
            if (s_DropHooked.TryGetValue(cb, out marker))
                return;
            s_DropHooked.Add(cb, new object());
            cb.DropDown += (s, e) =>
            {
                try
                {
                    ThemePalette p = Current;
                    COMBOBOXINFO info = new COMBOBOXINFO();
                    info.cbSize = Marshal.SizeOf(typeof(COMBOBOXINFO));
                    if (p != null && GetComboBoxInfo(cb.Handle, ref info) && info.hwndList != IntPtr.Zero)
                        SetWindowTheme(info.hwndList, !p.IsClassic && p.IsDark ? "DarkMode_Explorer" : "Explorer", null);
                }
                catch (Exception swallowed)
                {
                    Ares.Settings.ErrorLog.Note(swallowed);
                }
            };
        }

        // Ctrl+A selects everything in the lists which allow more than one selected item
        private static readonly ConditionalWeakTable<Control, object> s_SelectAllHooked = new ConditionalWeakTable<Control, object>();

        private static void HookSelectAll(Control c)
        {
            object marker;
            if (s_SelectAllHooked.TryGetValue(c, out marker))
                return;
            s_SelectAllHooked.Add(c, new object());
            c.KeyDown += (s, e) =>
            {
                if (!e.Control || e.KeyCode != Keys.A)
                    return;
                ListView lv = c as ListView;
                ListBox lb = c as ListBox;
                if (lv != null && lv.MultiSelect)
                {
                    lv.BeginUpdate();
                    foreach (ListViewItem item in lv.Items)
                        item.Selected = true;
                    lv.EndUpdate();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
                else if (lb != null && (lb.SelectionMode == SelectionMode.MultiExtended || lb.SelectionMode == SelectionMode.MultiSimple))
                {
                    lb.BeginUpdate();
                    for (int i = 0; i < lb.Items.Count; ++i)
                        lb.SetSelected(i, true);
                    lb.EndUpdate();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };
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
    }
}
