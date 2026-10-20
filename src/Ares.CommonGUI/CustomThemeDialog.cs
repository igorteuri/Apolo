using System;
using System.Drawing;
using System.Windows.Forms;

namespace Ares.CommonGUI
{
    /// <summary>Window of the "Custom" theme: dark or light background and the accent color (the main color of the program).</summary>
    public class CustomThemeDialog : Form
    {
        private static readonly int[] s_Presets = { 0xFFB627, 0xFF8A3D, 0xFF5A5F, 0xFF6FB5, 0xB488FF, 0x4C9AFF, 0x2EC4B6, 0x3DDC84 };

        private readonly RadioButton m_Dark = new RadioButton();
        private readonly RadioButton m_Light = new RadioButton();
        private readonly Swatch m_Swatch = new Swatch();
        private readonly System.Collections.Generic.List<Swatch> m_Presets = new System.Collections.Generic.List<Swatch>();
        private Color m_Accent;
        private bool m_Loading = true;

        public static void Edit(IWin32Window owner)
        {
            bool oldCustomDark = ThemeManager.CustomDark;
            Color oldCustomAccent = ThemeManager.CustomAccent;
            string oldId = ThemeManager.Current.Id;
            using (CustomThemeDialog dlg = new CustomThemeDialog())
            {
                if (dlg.ShowDialog(owner) == DialogResult.OK)
                    return;
                // cancelled: back to what was before
                ThemeManager.SetCustom(oldCustomDark, oldCustomAccent);
                if (oldId != "custom")
                    ThemeManager.SetTheme(oldId);
            }
        }

        private static string T(string pt, string en)
        {
            return System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt" ? pt : en;
        }

        private CustomThemeDialog()
        {
            Text = T("Tema personalizado", "Custom theme");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(420, 296);
            m_Accent = ThemeManager.CustomAccent;

            Label background = new Label();
            background.Text = T("Fundo", "Background");
            background.SetBounds(16, 14, 380, 20);
            Controls.Add(background);
            m_Dark.Text = T("Escuro", "Dark");
            m_Dark.SetBounds(28, 38, 160, 24);
            m_Light.Text = T("Claro", "Light");
            m_Light.SetBounds(200, 38, 160, 24);
            m_Dark.Checked = ThemeManager.CustomDark;
            m_Light.Checked = !ThemeManager.CustomDark;
            Controls.Add(m_Dark);
            Controls.Add(m_Light);

            Label accent = new Label();
            accent.Text = T("Cor de destaque (a cor principal do programa)", "Accent color (the main color of the program)");
            accent.SetBounds(16, 80, 388, 20);
            Controls.Add(accent);
            for (int i = 0; i < s_Presets.Length; i++)
            {
                Color color = ColorFromRgb(s_Presets[i]);
                // the swatches paint themselves: the theme repaints the BackColor of buttons and panels, which hid the colors
                Swatch preset = new Swatch();
                preset.Fill = color;
                preset.AccessibleName = T("Cor ", "Color ") + "#" + s_Presets[i].ToString("X6");
                preset.SetBounds(20 + i * 46, 106, 38, 32);
                preset.Click += (s, e) => { m_Accent = color; UpdateSwatch(); Apply(); };
                m_Presets.Add(preset);
                Controls.Add(preset);
            }
            m_Swatch.SetBounds(20, 152, 60, 34);
            m_Swatch.Clickable = false;
            m_Swatch.TabStop = false;
            m_Swatch.AccessibleName = T("Cor escolhida", "Chosen color");
            Controls.Add(m_Swatch);
            Button choose = new Button();
            choose.Text = T("Escolher outra cor...", "Choose another color...");
            choose.SetBounds(92, 152, 190, 34);
            choose.Click += (s, e) =>
            {
                using (ColorDialog cd = new ColorDialog())
                {
                    cd.FullOpen = true;
                    cd.Color = m_Accent;
                    if (cd.ShowDialog(this) == DialogResult.OK)
                    {
                        m_Accent = cd.Color;
                        UpdateSwatch();
                        Apply();
                    }
                }
            };
            Controls.Add(choose);

            Label hint = new Label();
            hint.Text = T("A mudança aparece na hora. Para voltar ao visual original do Apolo, escolha o amarelo (primeira cor) com fundo escuro. Cores difíceis de ler são ajustadas sozinhas.",
                "The change shows at once. To get the original Apolo look back, pick the yellow (first color) with a dark background. Colors which would be hard to read are adjusted automatically.");
            hint.SetBounds(16, 198, 388, 56);
            Controls.Add(hint);

            Button ok = new Button();
            ok.Text = T("Pronto", "Done");
            ok.DialogResult = DialogResult.OK;
            ok.SetBounds(ClientSize.Width - 16 - 100 - 8 - 90, 260, 100, 28);
            Controls.Add(ok);
            Button cancel = new Button();
            cancel.Text = T("Cancelar", "Cancel");
            cancel.DialogResult = DialogResult.Cancel;
            cancel.SetBounds(ClientSize.Width - 16 - 90, 260, 90, 28);
            Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;

            m_Dark.CheckedChanged += (s, e) => Apply();
            UpdateSwatch();
            m_Loading = false;
            Shown += (s, e) => Apply();
        }

        private static Color ColorFromRgb(int rgb)
        {
            return Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
        }

        private void UpdateSwatch()
        {
            m_Swatch.Fill = m_Accent;
            foreach (Swatch preset in m_Presets)
                preset.Selected = preset.Fill.ToArgb() == m_Accent.ToArgb();
        }

        /// <summary>A square of color which paints itself (also usable as a button, with the keyboard too).</summary>
        private class Swatch : Control
        {
            private Color m_Fill = Color.Gray;
            private bool m_Selected;
            private bool m_Clickable = true;

            public Swatch()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                    | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.StandardClick, true);
                TabStop = true;
                Cursor = Cursors.Hand;
            }

            public Color Fill
            {
                get { return m_Fill; }
                set { m_Fill = value; Invalidate(); }
            }

            public bool Selected
            {
                get { return m_Selected; }
                set { m_Selected = value; Invalidate(); }
            }

            public bool Clickable
            {
                get { return m_Clickable; }
                set
                {
                    m_Clickable = value;
                    SetStyle(ControlStyles.Selectable, value);
                    Cursor = value ? Cursors.Hand : Cursors.Default;
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                Color back = Parent != null ? Parent.BackColor : SystemColors.Control;
                g.Clear(back);
                // the ring uses the color of the text of the window: it is visible on the dark and on the light background
                Color ring = Parent != null ? Parent.ForeColor : SystemColors.ControlText;
                Rectangle box = new Rectangle(3, 3, Width - 7, Height - 7);
                using (SolidBrush fill = new SolidBrush(m_Fill))
                    g.FillRectangle(fill, box);
                using (Pen border = new Pen(Color.FromArgb(160, ring), 1))
                    g.DrawRectangle(border, box);
                if (m_Selected)
                {
                    using (Pen selected = new Pen(ring, 2))
                        g.DrawRectangle(selected, 1, 1, Width - 3, Height - 3);
                }
                if (Focused && m_Clickable && ShowFocusCues)
                    ControlPaint.DrawFocusRectangle(g, new Rectangle(0, 0, Width, Height), ring, back);
            }

            protected override bool IsInputKey(Keys keyData)
            {
                return keyData == Keys.Space || base.IsInputKey(keyData);
            }

            protected override void OnKeyDown(KeyEventArgs e)
            {
                base.OnKeyDown(e);
                if (m_Clickable && (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter))
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    OnClick(EventArgs.Empty);
                }
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                if (m_Clickable)
                    Focus();
                base.OnMouseDown(e);
            }

            protected override void OnGotFocus(EventArgs e)
            {
                base.OnGotFocus(e);
                Invalidate();
            }

            protected override void OnLostFocus(EventArgs e)
            {
                base.OnLostFocus(e);
                Invalidate();
            }
        }

        private void Apply()
        {
            if (m_Loading)
                return;
            ThemeManager.SetCustom(m_Dark.Checked, m_Accent);
        }
    }
}
