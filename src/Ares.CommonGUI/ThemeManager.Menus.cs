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
    // Menus and toolbars: renderer, color table, list view headers, menu tidying.
    public static partial class ThemeManager
    {
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
            if (lv.View != View.Details || lv.OwnerDraw || lv.CheckBoxes)
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
                if (e.Item is ToolStripStatusLabel)
                {
                    // the status bar chooses the color of each part itself (dim, highlight ...)
                }
                else if (e.Item.Tag is string && (string)e.Item.Tag == "AccentText")
                    e.TextColor = Current.Accent;
                else
                    e.TextColor = e.Item.Enabled ? Current.Text : Current.TextDim;
                base.OnRenderItemText(e);
            }

            protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
            {
                e.ArrowColor = e.Item.Enabled ? Current.Text : Current.TextDim;
                base.OnRenderArrow(e);
            }

            // the "selected" mark of a menu item: just a tick in the highlight color (the default one sits in a box with a brown fill)
            protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
            {
                Rectangle r = e.ImageRectangle;
                int s = Math.Min(r.Width, r.Height);
                int x = r.X + (r.Width - s) / 2;
                int y = r.Y + (r.Height - s) / 2;
                System.Drawing.Drawing2D.SmoothingMode old = e.Graphics.SmoothingMode;
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (Pen tick = new Pen(e.Item.Enabled ? Current.Accent : Current.TextDim, 2f))
                {
                    tick.StartCap = tick.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                    tick.LineJoin = System.Drawing.Drawing2D.LineJoin.Round;
                    e.Graphics.DrawLines(tick, new PointF[]
                    {
                        new PointF(x + s * 0.22f, y + s * 0.54f),
                        new PointF(x + s * 0.43f, y + s * 0.74f),
                        new PointF(x + s * 0.80f, y + s * 0.30f)
                    });
                }
                e.Graphics.SmoothingMode = old;
            }

            protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
            {
                DrawButtonBackground(e);
                if (e.Item is MenuHintButton)
                {
                    // small chevron at the right: a right click offers more options
                    Rectangle b = new Rectangle(Point.Empty, e.Item.Size);
                    int cx = b.Right - 7, cy = b.Top + b.Height / 2;
                    System.Drawing.Drawing2D.SmoothingMode old = e.Graphics.SmoothingMode;
                    e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    using (Pen p = new Pen(e.Item.Enabled ? Current.TextDim : Color.FromArgb(90, Current.TextDim), 1.5f))
                        e.Graphics.DrawLines(p, new Point[] { new Point(cx - 3, cy - 1), new Point(cx, cy + 2), new Point(cx + 3, cy - 1) });
                    e.Graphics.SmoothingMode = old;
                }
            }

            private void DrawButtonBackground(ToolStripItemRenderEventArgs e)
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
                if (button != null && button.Enabled && (button.Selected || button.Pressed))
                {
                    // hover / pressed: drawn here with the theme colors (the standard drawing can leave a light system fill)
                    Rectangle r = new Rectangle(1, 1, button.Width - 3, button.Height - 3);
                    using (SolidBrush fill = new SolidBrush(button.Pressed ? Current.Selection : Current.Hover))
                        e.Graphics.FillRectangle(fill, r);
                    using (Pen border = new Pen(button.Pressed ? Current.Accent : Current.Border))
                        e.Graphics.DrawRectangle(border, r);
                    return;
                }
                base.OnRenderButtonBackground(e);
            }

            protected override void OnRenderDropDownButtonBackground(ToolStripItemRenderEventArgs e)
            {
                ToolStripItem item = e.Item;
                if (item.Selected || item.Pressed)
                {
                    Rectangle r = new Rectangle(1, 1, item.Width - 3, item.Height - 3);
                    using (SolidBrush fill = new SolidBrush(item.Pressed ? Current.Selection : Current.Hover))
                        e.Graphics.FillRectangle(fill, r);
                }
            }

            protected override void OnRenderToolStripStatusLabelBackground(ToolStripItemRenderEventArgs e)
            {
                // the dividers of the status bar are only a hint: a thin line at 30% of the border color
                ToolStripStatusLabel label = e.Item as ToolStripStatusLabel;
                if (label == null || label.BorderSides == ToolStripStatusLabelBorderSides.None)
                    return;
                Rectangle r = new Rectangle(Point.Empty, label.Size);
                int inset = Math.Max(3, r.Height / 5);
                using (Pen pen = new Pen(Color.FromArgb(77, Current.TextDim)))
                {
                    if ((label.BorderSides & ToolStripStatusLabelBorderSides.Right) != 0)
                        e.Graphics.DrawLine(pen, r.Right - 1, r.Top + inset, r.Right - 1, r.Bottom - inset);
                    if ((label.BorderSides & ToolStripStatusLabelBorderSides.Left) != 0)
                        e.Graphics.DrawLine(pen, r.Left, r.Top + inset, r.Left, r.Bottom - inset);
                }
            }

            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                // no border around menu strips / toolbars / status strips
                if (e.ToolStrip is ToolStripDropDown)
                {
                    // one clean line in the border color (the default one leaves light pixels at the edges)
                    using (Pen pen = new Pen(Current.Border))
                        e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
                }
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
    }
}
