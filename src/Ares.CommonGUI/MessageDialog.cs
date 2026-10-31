/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 The message window of the program: same calls as MessageBox.Show, but it is an ordinary form, so the theme paints it
 (the system message box stayed light, with a blue question icon, over the dark windows).
*/
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Ares.CommonGUI
{
    public static class MessageDialog
    {
        public static DialogResult Show(string text)
        {
            return Show(null, text, "Apolo", MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);
        }

        public static DialogResult Show(string text, string caption)
        {
            return Show(null, text, caption, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);
        }

        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons)
        {
            return Show(null, text, caption, buttons, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);
        }

        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            return Show(null, text, caption, buttons, icon, MessageBoxDefaultButton.Button1);
        }

        public static DialogResult Show(IWin32Window owner, string text)
        {
            return Show(owner, text, "Apolo", MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);
        }

        public static DialogResult Show(IWin32Window owner, string text, string caption)
        {
            return Show(owner, text, caption, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);
        }

        public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons)
        {
            return Show(owner, text, caption, buttons, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);
        }

        public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            return Show(owner, text, caption, buttons, icon, MessageBoxDefaultButton.Button1);
        }

        public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon,
            MessageBoxDefaultButton defaultButton)
        {
            try
            {
                using (MessageForm form = new MessageForm(text, caption, buttons, icon, defaultButton))
                    return owner != null ? form.ShowDialog(owner) : form.ShowDialog();
            }
            catch (Exception swallowed)
            {
                // the window itself failed: the system message box still tells the user
                Ares.Settings.ErrorLog.Note(swallowed);
                return owner != null ? MessageBox.Show(owner, text, caption, buttons, icon, defaultButton)
                    : MessageBox.Show(text, caption, buttons, icon, defaultButton);
            }
        }

        private static bool IsPt
        {
            get { return System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt"; }
        }

        private class MessageForm : Form
        {
            private readonly MessageBoxIcon m_Icon;

            public MessageForm(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton)
            {
                m_Icon = icon;
                Text = caption;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                StartPosition = FormStartPosition.CenterParent;
                MaximizeBox = false;
                MinimizeBox = false;
                ShowInTaskbar = false;
                AutoSize = true;
                AutoSizeMode = AutoSizeMode.GrowAndShrink;
                Padding = new Padding(16, 16, 16, 12);

                bool hasIcon = icon != MessageBoxIcon.None;
                TableLayoutPanel layout = new TableLayoutPanel { AutoSize = true, ColumnCount = hasIcon ? 2 : 1, Dock = DockStyle.Fill };
                if (hasIcon)
                {
                    Panel glyph = new Panel { Size = new Size(40, 40), Margin = new Padding(0, 0, 14, 0) };
                    glyph.Paint += PaintGlyph;
                    layout.Controls.Add(glyph);
                }
                Label label = new Label { Text = text, AutoSize = true, MaximumSize = new Size(460, 0), Margin = new Padding(0, 4, 0, 0) };
                layout.Controls.Add(label);

                FlowLayoutPanel row = new FlowLayoutPanel
                {
                    FlowDirection = FlowDirection.RightToLeft,
                    AutoSize = true,
                    Dock = DockStyle.Fill,
                    Margin = new Padding(0, 18, 0, 0)
                };
                string[] names;
                DialogResult[] results;
                switch (buttons)
                {
                    case MessageBoxButtons.OKCancel:
                        names = new[] { "OK", IsPt ? "Cancelar" : "Cancel" };
                        results = new[] { DialogResult.OK, DialogResult.Cancel };
                        break;
                    case MessageBoxButtons.YesNo:
                        names = new[] { IsPt ? "Sim" : "Yes", IsPt ? "Não" : "No" };
                        results = new[] { DialogResult.Yes, DialogResult.No };
                        break;
                    case MessageBoxButtons.YesNoCancel:
                        names = new[] { IsPt ? "Sim" : "Yes", IsPt ? "Não" : "No", IsPt ? "Cancelar" : "Cancel" };
                        results = new[] { DialogResult.Yes, DialogResult.No, DialogResult.Cancel };
                        break;
                    case MessageBoxButtons.RetryCancel:
                        names = new[] { IsPt ? "Tentar de novo" : "Retry", IsPt ? "Cancelar" : "Cancel" };
                        results = new[] { DialogResult.Retry, DialogResult.Cancel };
                        break;
                    default:
                        names = new[] { "OK" };
                        results = new[] { DialogResult.OK };
                        break;
                }
                int def = defaultButton == MessageBoxDefaultButton.Button2 ? 1 : (defaultButton == MessageBoxDefaultButton.Button3 ? 2 : 0);
                if (def >= names.Length)
                    def = 0;
                Button[] made = new Button[names.Length];
                // the row runs right to left: the last button is added first
                for (int i = names.Length - 1; i >= 0; --i)
                {
                    Button b = new Button { Text = names[i], DialogResult = results[i], AutoSize = true, MinimumSize = new Size(84, 30), Padding = new Padding(8, 0, 8, 0) };
                    made[i] = b;
                    row.Controls.Add(b);
                }
                // a window which can only be answered Yes / No closes as "No" (Esc, or the X), like the system one
                DialogResult escape = DialogResult.None;
                foreach (DialogResult r in results)
                {
                    if (r == DialogResult.Cancel) { escape = r; break; }
                }
                if (escape == DialogResult.None)
                {
                    foreach (DialogResult r in results)
                    {
                        if (r == DialogResult.No || r == DialogResult.OK) { escape = r; break; }
                    }
                }
                for (int i = 0; i < results.Length; ++i)
                {
                    if (results[i] == escape)
                        CancelButton = made[i];
                }
                AcceptButton = made[def];
                layout.Controls.Add(row);
                if (hasIcon)
                    layout.SetColumnSpan(row, 2);
                Controls.Add(layout);
                Shown += (s, e) => made[def].Focus();
            }

            // a round sign in the accent color, with a mark inside (the system icons are blue / yellow / red pictures of another style)
            private void PaintGlyph(object sender, PaintEventArgs e)
            {
                ThemePalette p = ThemeManager.Current;
                Color color = p != null ? p.Accent : SystemColors.Highlight;
                string mark;
                switch (m_Icon)
                {
                    case MessageBoxIcon.Error:
                        color = Color.FromArgb(0xE0, 0x5A, 0x5A);
                        mark = "✕";
                        break;
                    case MessageBoxIcon.Warning:
                        color = Color.FromArgb(0xE5, 0xB0, 0x3C);
                        mark = "!";
                        break;
                    case MessageBoxIcon.Question:
                        mark = "?";
                        break;
                    default:
                        mark = "i";
                        break;
                }
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle r = new Rectangle(2, 2, 35, 35);
                using (SolidBrush fill = new SolidBrush(Color.FromArgb(40, color)))
                    g.FillEllipse(fill, r);
                using (Pen pen = new Pen(color, 2f))
                    g.DrawEllipse(pen, r);
                using (Font font = new Font(Font.FontFamily, 15f, FontStyle.Bold))
                    TextRenderer.DrawText(g, mark, font, new Rectangle(0, 0, 40, 40), color,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
        }
    }
}
