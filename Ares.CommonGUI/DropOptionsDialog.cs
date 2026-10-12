using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Ares.CommonGUI
{
    /// <summary>Choices made when folders / files are dropped into Ares.</summary>
    public class DropOptions
    {
        /// <summary>true = sound effects ("Sounds"), false = music.</summary>
        public bool Sounds;
        /// <summary>true = all files of a folder in one element (playlist / random sound), false = one element (button) per file.</summary>
        public bool OneElement;
    }

    public class DropOptionsDialog : Form
    {
        // remembered for the next drop
        private static bool s_LastSounds = false;
        private static bool s_LastOne = false;

        private RadioButton m_Music, m_Sounds, m_Each, m_One;

        public static bool IsPt
        {
            get { return System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt"; }
        }

        public static DropOptions Ask(IWin32Window owner, int folders, int files)
        {
            using (var dlg = new DropOptionsDialog(folders, files))
            {
                if (dlg.ShowDialog(owner) != DialogResult.OK)
                    return null;
                var result = new DropOptions { Sounds = dlg.m_Sounds.Checked, OneElement = dlg.m_One.Checked };
                s_LastSounds = result.Sounds;
                s_LastOne = result.OneElement;
                return result;
            }
        }

        private DropOptionsDialog(int groups, int files)
        {
            bool pt = IsPt;
            Text = pt ? "Adicionar ao Ares" : "Add to Ares";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(12);

            var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Fill };
            Controls.Add(layout);

            var info = new Label
            {
                AutoSize = true,
                Text = pt ? (files + " arquivo(s) de áudio em " + groups + " grupo(s).")
                          : (files + " audio file(s) in " + groups + " group(s)."),
                Margin = new Padding(0, 0, 0, 8)
            };
            layout.Controls.Add(info);

            var typeBox = new GroupBox { Text = pt ? "Tipo" : "Type", AutoSize = true, Dock = DockStyle.Fill };
            var typeFlow = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
            m_Music = new RadioButton { AutoSize = true, Text = pt ? "Música (Music)" : "Music", Checked = !s_LastSounds };
            m_Sounds = new RadioButton { AutoSize = true, Text = pt ? "Efeitos sonoros (Sounds)" : "Sound effects (Sounds)", Checked = s_LastSounds };
            typeFlow.Controls.Add(m_Music);
            typeFlow.Controls.Add(m_Sounds);
            typeBox.Controls.Add(typeFlow);
            layout.Controls.Add(typeBox);

            var structBox = new GroupBox { Text = pt ? "Como organizar" : "How to organize", AutoSize = true, Dock = DockStyle.Fill };
            var structFlow = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
            m_Each = new RadioButton
            {
                AutoSize = true,
                Checked = !s_LastOne,
                Text = pt ? "Cada arquivo vira um botão (Element)" : "Each file becomes its own button (Element)"
            };
            m_One = new RadioButton
            {
                AutoSize = true,
                Checked = s_LastOne,
                Text = pt ? "Todos os arquivos de cada pasta num botão só\n(lista de músicas / som aleatório)"
                          : "All files of each folder in one button\n(playlist / random sound)"
            };
            structFlow.Controls.Add(m_Each);
            structFlow.Controls.Add(m_One);
            structBox.Controls.Add(structFlow);
            layout.Controls.Add(structBox);

            var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 0) };
            var cancel = new Button { Text = pt ? "Cancelar" : "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);
            layout.Controls.Add(buttons);
            AcceptButton = ok;
            CancelButton = cancel;
        }
    }
}
