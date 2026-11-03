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
    // Controls drawn by the theme itself: group boxes and cards, list boxes, drop-down lists, rounded buttons, check boxes, text box borders.
    public static partial class ThemeManager
    {
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
            b.MouseEnter += delegate { b.Invalidate(); };
            b.MouseLeave += delegate { b.Invalidate(); };
            b.Invalidate();
        }

        // The mark of a radio button / check box, drawn in the colors of the theme (the system one is a blue picture of another style).
        private static void PaintGlyph(ButtonBase b, Graphics g, ThemePalette p)
        {
            ContentAlignment check = b is CheckBox ? ((CheckBox)b).CheckAlign : ((RadioButton)b).CheckAlign;
            if (check != ContentAlignment.MiddleLeft)
                return;
            bool radio = b is RadioButton;
            int size = radio
                ? RadioButtonRenderer.GetGlyphSize(g, System.Windows.Forms.VisualStyles.RadioButtonState.UncheckedNormal).Width
                : CheckBoxRenderer.GetGlyphSize(g, System.Windows.Forms.VisualStyles.CheckBoxState.UncheckedNormal).Width;
            bool on = b is CheckBox ? ((CheckBox)b).CheckState != CheckState.Unchecked : ((RadioButton)b).Checked;
            bool hot = b.Enabled && b.ClientRectangle.Contains(b.PointToClient(Cursor.Position));
            int top = (b.Height - size) / 2;
            Rectangle cover = new Rectangle(0, top - 1, size + 3, size + 2);
            Rectangle r = new Rectangle(1, top, size - 1, size - 1);
            SmoothingMode old = g.SmoothingMode;
            using (SolidBrush back = new SolidBrush(OpaqueBack(b)))
                g.FillRectangle(back, cover);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color edge = !b.Enabled ? p.Border : (on || hot ? p.Accent : p.ControlBorder);
            Color fill = !b.Enabled ? p.Surface : (on && !radio ? p.Accent : p.Surface);
            using (SolidBrush f = new SolidBrush(fill))
            using (Pen pen = new Pen(edge, 1.5f))
            {
                if (radio)
                {
                    g.FillEllipse(f, r);
                    g.DrawEllipse(pen, r);
                }
                else
                {
                    g.FillRectangle(f, r);
                    g.DrawRectangle(pen, r);
                }
            }
            if (on)
            {
                Color mark = b.Enabled ? (radio ? p.Accent : p.Window) : p.TextDim;
                if (radio)
                {
                    int d = Math.Max(4, size / 2 - 1);
                    using (SolidBrush m = new SolidBrush(mark))
                        g.FillEllipse(m, r.X + (r.Width - d) / 2f, r.Y + (r.Height - d) / 2f, d, d);
                }
                else
                {
                    using (Pen m = new Pen(mark, 2f))
                    {
                        m.StartCap = LineCap.Round;
                        m.EndCap = LineCap.Round;
                        g.DrawLines(m, new PointF[]
                        {
                            new PointF(r.X + r.Width * 0.22f, r.Y + r.Height * 0.52f),
                            new PointF(r.X + r.Width * 0.43f, r.Y + r.Height * 0.74f),
                            new PointF(r.X + r.Width * 0.80f, r.Y + r.Height * 0.28f)
                        });
                    }
                }
            }
            g.SmoothingMode = old;
        }

        // The text of a disabled radio button / check box is blurred by Windows (embossed gray): draw it again, crisp and dim.
        private static void OnDisabledTextPaint(object sender, PaintEventArgs e)
        {
            ButtonBase b = (ButtonBase)sender;
            ThemePalette p = Current;
            if (p == null || p.IsClassic || b.Image != null)
                return;
            PaintGlyph(b, e.Graphics, p);
            if (b.Enabled || string.IsNullOrEmpty(b.Text))
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
            if (IsCard(g))
            {
                PaintCard(g, gr, p);
                return;
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

        /// <summary>A group box with the Tag "Card" looks like the cards of the settings pages: the outline all around, the title inside it.</summary>
        public static bool IsCard(GroupBox g)
        {
            return g.Tag is string && (string)g.Tag == "Card";
        }

        /// <summary>Font of the title of a card group box; its content starts at CardContentTop.</summary>
        public static Font CardTitleFont(Control c)
        {
            return new Font(c.Font.FontFamily, c.Font.Size + 1f, FontStyle.Bold);
        }

        public static int CardContentTop(Control c)
        {
            using (Font f = CardTitleFont(c))
                return 14 + TextRenderer.MeasureText("Ag", f).Height + 8;
        }

        private static void PaintCard(GroupBox g, Graphics gr, ThemePalette p)
        {
            gr.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, g.Width - 1, g.Height - 1);
            int d = 14;
            using (System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath())
            using (Pen pen = new Pen(p.Border, 1.5f))
            {
                path.AddArc(r.X, r.Y, d, d, 180, 90);
                path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                gr.DrawPath(pen, path);
            }
            gr.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.Default;
            if (!string.IsNullOrEmpty(g.Text))
            {
                using (Font f = CardTitleFont(g))
                    TextRenderer.DrawText(gr, g.Text, f, new Point(18, 14), p.Text, TextFormatFlags.NoPrefix);
            }
        }
    }
}
