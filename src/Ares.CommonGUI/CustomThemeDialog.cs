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
        private readonly Panel m_Swatch = new Panel();
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
            ClientSize = new Size(420, 262);
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
                Button preset = new Button();
                preset.FlatStyle = FlatStyle.Flat;
                preset.FlatAppearance.BorderSize = 1;
                preset.BackColor = color;
                preset.SetBounds(20 + i * 46, 106, 38, 30);
                preset.TabStop = false;
                preset.Click += (s, e) => { m_Accent = color; UpdateSwatch(); Apply(); };
                Controls.Add(preset);
            }
            m_Swatch.SetBounds(20, 150, 60, 34);
            m_Swatch.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(m_Swatch);
            Button choose = new Button();
            choose.Text = T("Escolher outra cor...", "Choose another color...");
            choose.SetBounds(92, 150, 180, 34);
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
            hint.SetBounds(16, 196, 388, 36);
            Controls.Add(hint);

            Button ok = new Button();
            ok.Text = T("Pronto", "Done");
            ok.DialogResult = DialogResult.OK;
            ok.SetBounds(ClientSize.Width - 16 - 100 - 8 - 90, 224, 100, 28);
            Controls.Add(ok);
            Button cancel = new Button();
            cancel.Text = T("Cancelar", "Cancel");
            cancel.DialogResult = DialogResult.Cancel;
            cancel.SetBounds(ClientSize.Width - 16 - 90, 224, 90, 28);
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
            m_Swatch.BackColor = m_Accent;
        }

        private void Apply()
        {
            if (m_Loading)
                return;
            ThemeManager.SetCustom(m_Dark.Checked, m_Accent);
        }
    }
}
