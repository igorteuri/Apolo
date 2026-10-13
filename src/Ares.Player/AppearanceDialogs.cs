/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 Dialogs to choose a color and an emoji; shared by the Player and the Editor.
*/
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Ares.Player
{
    internal static class Loc
    {
        public static string T(string pt, string en)
        {
            return System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt" ? pt : en;
        }
    }

    // ---- choice of a color ----

    internal class ColorChoiceDialog : Form
    {
        private static readonly string[] s_Names =
        {
            "Vermelho", "Laranja", "Amarelo", "Verde", "Verde-água", "Azul", "Índigo", "Roxo", "Rosa", "Marrom", "Cinza", "Dourado"
        };
        private static readonly string[] s_NamesEn =
        {
            "Red", "Orange", "Yellow", "Green", "Teal", "Blue", "Indigo", "Purple", "Pink", "Brown", "Gray", "Gold"
        };
        private static readonly Color[] s_Colors =
        {
            Color.FromArgb(0xC0, 0x39, 0x2B), Color.FromArgb(0xE6, 0x7E, 0x22), Color.FromArgb(0xF1, 0xC4, 0x0F), Color.FromArgb(0x27, 0xAE, 0x60),
            Color.FromArgb(0x16, 0xA0, 0x85), Color.FromArgb(0x29, 0x80, 0xB9), Color.FromArgb(0x5B, 0x5B, 0xD6), Color.FromArgb(0x8E, 0x44, 0xAD),
            Color.FromArgb(0xE8, 0x43, 0x93), Color.FromArgb(0x8D, 0x6E, 0x63), Color.FromArgb(0x95, 0xA5, 0xA6), Color.FromArgb(0xB8, 0x86, 0x0B)
        };

        private Color m_Result = Color.Empty;

        public static bool Pick(IWin32Window owner, string title, Color current, out Color color)
        {
            using (ColorChoiceDialog dlg = new ColorChoiceDialog(title, current))
            {
                bool ok = dlg.ShowDialog(owner) == DialogResult.OK;
                color = ok ? dlg.m_Result : Color.Empty;
                return ok;
            }
        }

        private ColorChoiceDialog(string title, Color current)
        {
            Text = title;
            AutoScaleMode = AutoScaleMode.Font;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(380, 232);

            Label hint = new Label();
            hint.SetBounds(12, 10, 356, 36);
            hint.Text = Loc.T("Escolha a cor de destaque. Ela tinge o fundo e a borda, e ajuda a achar o botão ou a pasta.",
                "Choose the highlight color. It tints the background and the border, and helps to find the button or folder.");
            Controls.Add(hint);

            bool pt = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt";
            ToolTip tip = new ToolTip();
            for (int i = 0; i < s_Colors.Length; i++)
            {
                Color c = s_Colors[i];
                // drawn by the control itself: the theme repaints real buttons and would hide the color
                Swatch b = new Swatch(c, !current.IsEmpty && current.ToArgb() == c.ToArgb());
                b.SetBounds(12 + (i % 6) * 60, 54 + (i / 6) * 52, 54, 46);
                tip.SetToolTip(b, pt ? s_Names[i] : s_NamesEn[i]);
                b.Click += (s, e) =>
                {
                    m_Result = c;
                    DialogResult = DialogResult.OK;
                    Close();
                };
                Controls.Add(b);
            }

            Button other = new Button();
            other.SetBounds(12, 164, 130, 28);
            other.Text = Loc.T("Outra cor...", "Other color...");
            other.Click += (s, e) =>
            {
                using (ColorDialog cd = new ColorDialog())
                {
                    cd.FullOpen = true;
                    if (!current.IsEmpty)
                        cd.Color = current;
                    if (cd.ShowDialog(this) == DialogResult.OK)
                    {
                        m_Result = Color.FromArgb(255, cd.Color.R, cd.Color.G, cd.Color.B);
                        DialogResult = DialogResult.OK;
                        Close();
                    }
                }
            };
            Controls.Add(other);

            Button none = new Button();
            none.SetBounds(150, 164, 130, 28);
            none.Text = Loc.T("Sem cor", "No color");
            none.Click += (s, e) =>
            {
                m_Result = Color.Empty;
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(none);

            Button cancel = new Button();
            cancel.SetBounds(288, 164, 80, 28);
            cancel.Text = Loc.T("Cancelar", "Cancel");
            cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(cancel);
            CancelButton = cancel;
        }
    }

    internal class Swatch : Control
    {
        private readonly Color m_Color;
        private readonly bool m_Current;
        private bool m_Hover;

        public Swatch(Color color, bool current)
        {
            m_Color = color;
            m_Current = current;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            m_Hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            m_Hover = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override bool IsInputKey(System.Windows.Forms.Keys keyData)
        {
            return keyData == System.Windows.Forms.Keys.Enter || keyData == System.Windows.Forms.Keys.Space || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == System.Windows.Forms.Keys.Enter || e.KeyCode == System.Windows.Forms.Keys.Space)
            {
                OnClick(EventArgs.Empty);
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Color back = Parent != null ? Parent.BackColor : SystemColors.Control;
            g.Clear(back);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(2, 2, Width - 5, Height - 5);
            using (GraphicsPath path = new GraphicsPath())
            {
                int d = 12;
                path.AddArc(r.X, r.Y, d, d, 180, 90);
                path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                using (SolidBrush b = new SolidBrush(m_Color))
                    g.FillPath(b, path);
                float w = m_Current ? 3f : (m_Hover || Focused ? 2.5f : 1f);
                Color line = m_Current || m_Hover || Focused ? Color.White : Color.FromArgb(120, 120, 120);
                using (Pen pen = new Pen(line, w))
                    g.DrawPath(pen, path);
            }
        }
    }

    // ---- choice of an emoji ----

    internal class EmojiPickerDialog : Form
    {
        private readonly TextBox m_Search = new TextBox();
        private readonly FlowLayoutPanel m_Panel = new FlowLayoutPanel();
        private readonly ToolTip m_Tip = new ToolTip();
        private readonly Dictionary<string, Bitmap> m_Loaded = new Dictionary<string, Bitmap>();
        private volatile bool m_Closed;
        private EmojiEntry m_Chosen;

        public static EmojiEntry Pick(IWin32Window owner, string query)
        {
            using (EmojiPickerDialog dlg = new EmojiPickerDialog(query))
                return dlg.ShowDialog(owner) == DialogResult.OK ? dlg.m_Chosen : null;
        }

        private EmojiPickerDialog(string query)
        {
            Text = Loc.T("Escolher emoji", "Choose emoji");
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(620, 460);
            MinimumSize = new Size(400, 300);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            MaximizeBox = false;
            MinimizeBox = false;

            m_Search.Dock = DockStyle.Top;
            m_Search.Text = query;
            m_Search.TextChanged += (s, e) => Fill();
            Label hint = new Label();
            hint.Dock = DockStyle.Top;
            hint.AutoSize = false;
            hint.Height = 24;
            hint.TextAlign = ContentAlignment.MiddleLeft;
            hint.Text = Loc.T("Digite para filtrar (floresta, dragão, taverna, fogo...). Com 3 letras ou mais, busca em todos os emojis. Os desenhos são baixados na primeira vez.",
                "Type to filter (forest, dragon, tavern, fire...). With 3 letters or more it searches all emojis. The pictures are downloaded the first time.");
            m_Panel.Dock = DockStyle.Fill;
            m_Panel.AutoScroll = true;
            m_Panel.Padding = new Padding(6);
            Controls.Add(m_Panel);
            Controls.Add(hint);
            Controls.Add(m_Search);
            Shown += (s, e) =>
            {
                Fill();
                // the big dictionary (all emojis, in Portuguese and English) is loaded in the background
                EmojiCatalog.PrepareExtendedAsync(() =>
                {
                    try
                    {
                        BeginInvoke(new Action(() =>
                        {
                            if (!m_Closed && m_Search.Text.Trim().Length >= 3)
                                Fill();
                        }));
                    }
                    catch (InvalidOperationException)
                    {
                    }
                });
            };
            FormClosing += (s, e) => m_Closed = true;
            FormClosed += (s, e) =>
            {
                foreach (Bitmap b in m_Loaded.Values)
                    b.Dispose();
                m_Loaded.Clear();
            };
        }

        private static Bitmap Small(string file, int size)
        {
            using (FileStream fs = File.OpenRead(file))
            using (Image img = Image.FromStream(fs))
            {
                Bitmap bmp = new Bitmap(size, size);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(img, 0, 0, size, size);
                }
                return bmp;
            }
        }

        private void Fill()
        {
            m_Panel.SuspendLayout();
            foreach (Control c in new List<Control>(m_Panel.Controls.Cast<Control>()))
            {
                m_Panel.Controls.Remove(c);
                c.Dispose();
            }
            List<EmojiEntry> found = EmojiCatalog.Search(m_Search.Text);
            List<KeyValuePair<EmojiEntry, Button>> pending = new List<KeyValuePair<EmojiEntry, Button>>();
            foreach (EmojiEntry entry in found)
            {
                EmojiEntry en = entry;
                Button b = new Button();
                b.Size = new Size(58, 58);
                b.Margin = new Padding(3);
                b.ImageAlign = ContentAlignment.MiddleCenter;
                Bitmap have;
                if (m_Loaded.TryGetValue(en.Code, out have))
                    b.Image = have;
                else
                    pending.Add(new KeyValuePair<EmojiEntry, Button>(en, b));
                m_Tip.SetToolTip(b, en.Label + " (" + string.Join(", ", en.Words.Skip(1).Take(3).ToArray()) + ")");
                b.Click += (s, e) =>
                {
                    m_Chosen = en;
                    DialogResult = DialogResult.OK;
                    Close();
                };
                m_Panel.Controls.Add(b);
            }
            m_Panel.ResumeLayout();
            if (pending.Count == 0)
                return;
            string current = m_Search.Text;
            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                foreach (KeyValuePair<EmojiEntry, Button> p in pending)
                {
                    if (m_Closed || m_Search.IsDisposed)
                        return;
                    Bitmap bmp = null;
                    try
                    {
                        string file = EmojiCatalog.GetPicture(p.Key.Code);
                        if (file != null)
                            bmp = Small(file, 40);
                    }
                    catch (Exception)
                    {
                    }
                    if (bmp == null)
                        continue;
                    Bitmap loaded = bmp;
                    Button button = p.Value;
                    string code = p.Key.Code;
                    try
                    {
                        BeginInvoke(new Action(() =>
                        {
                            if (m_Closed || m_Loaded.ContainsKey(code))
                            {
                                loaded.Dispose();
                                return;
                            }
                            m_Loaded[code] = loaded;
                            if (!button.IsDisposed)
                                button.Image = loaded;
                        }));
                    }
                    catch (InvalidOperationException)
                    {
                        loaded.Dispose();
                        return;
                    }
                }
            });
        }
    }
}
