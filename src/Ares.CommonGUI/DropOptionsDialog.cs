using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Ares.CommonGUI
{
    /// <summary>How the files of a folder are put together in one element.</summary>
    public enum PlaylistKind
    {
        /// <summary>Music: random order. Sounds: one random sound.</summary>
        Random = 0,
        /// <summary>Music and sounds: in the order of the files.</summary>
        Sequential = 1,
        /// <summary>Sounds only: all files at the same time.</summary>
        Parallel = 2
    }

    /// <summary>Choices made when folders / files are dropped into Apolo (same options as the Editor offers for new elements).</summary>
    public class DropOptions
    {
        /// <summary>true = sound effects ("Sounds"), false = music.</summary>
        public bool Sounds;
        /// <summary>true = all files of a folder in one element (playlist / random sound), false = one element (button) per file.</summary>
        public bool OneElement;
        /// <summary>Kind of playlist when OneElement is set.</summary>
        public PlaylistKind Kind = PlaylistKind.Random;
        /// <summary>Music lists: repeat forever.</summary>
        public bool Repeat = true;
        /// <summary>Volume of the files, in percent.</summary>
        public int Volume = 100;
        /// <summary>Fade in / fade out of each file, in milliseconds.</summary>
        public int FadeInMs;
        public int FadeOutMs;
        /// <summary>Music: smooth transition from one track to the next.</summary>
        public bool CrossFade;
    }

    public class DropOptionsDialog : Form
    {
        // remembered for the next drop
        private static DropOptions s_Last = new DropOptions();

        private RadioButton m_Music, m_Sounds, m_Each, m_One;
        private RadioButton m_KindRandom, m_KindSequential, m_KindParallel;
        private CheckBox m_Repeat, m_CrossFade;
        private NumericUpDown m_Volume, m_FadeIn, m_FadeOut;
        private GroupBox m_KindBox, m_EffectBox;

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
                var result = new DropOptions
                {
                    Sounds = dlg.m_Sounds.Checked,
                    OneElement = dlg.m_One.Checked,
                    Kind = dlg.m_KindParallel.Checked && dlg.m_Sounds.Checked ? PlaylistKind.Parallel
                        : (dlg.m_KindSequential.Checked ? PlaylistKind.Sequential : PlaylistKind.Random),
                    Repeat = dlg.m_Repeat.Checked,
                    Volume = (int)dlg.m_Volume.Value,
                    FadeInMs = (int)(dlg.m_FadeIn.Value * 1000),
                    FadeOutMs = (int)(dlg.m_FadeOut.Value * 1000),
                    CrossFade = dlg.m_CrossFade.Checked
                };
                s_Last = result;
                return result;
            }
        }

        /// <summary>The next drop starts with "sound effects, one button per file" (used by the quick effects board).</summary>
        public static void PreferEffects()
        {
            s_Last = new DropOptions { Sounds = true, OneElement = false, Kind = PlaylistKind.Random, Repeat = false };
        }

        private static string Tx(string pt, string en)
        {
            return IsPt ? pt : en;
        }

        private DropOptionsDialog(int groups, int files)
        {
            bool pt = IsPt;
            Text = pt ? "Adicionar ao Apolo" : "Add to Apolo";
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

            // two quick choices which fill in everything below
            var presets = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 8) };
            var presetMusic = new Button { AutoSize = true, Text = pt ? "\u266B  Trilha: uma playlist por pasta" : "\u266B  Soundtrack: one playlist per folder" };
            var presetEffects = new Button { AutoSize = true, Text = pt ? "\u26A1  Efeitos: um botão por arquivo" : "\u26A1  Effects: one button per file" };
            presets.Controls.Add(presetMusic);
            presets.Controls.Add(presetEffects);
            layout.Controls.Add(presets);

            var typeBox = new GroupBox { Text = pt ? "Tipo de áudio" : "Kind of audio", AutoSize = true, Dock = DockStyle.Fill };
            var typeFlow = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
            m_Music = new RadioButton { AutoSize = true, Text = pt ? "Música (toca em segundo plano, uma de cada vez)" : "Music (background, one at a time)", Checked = !s_Last.Sounds };
            m_Sounds = new RadioButton { AutoSize = true, Text = pt ? "Efeitos sonoros (podem tocar junto com a música)" : "Sound effects (can play over the music)", Checked = s_Last.Sounds };
            typeFlow.Controls.Add(m_Music);
            typeFlow.Controls.Add(m_Sounds);
            typeBox.Controls.Add(typeFlow);
            layout.Controls.Add(typeBox);

            var structBox = new GroupBox { Text = pt ? "Como organizar" : "How to organize", AutoSize = true, Dock = DockStyle.Fill };
            var structFlow = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
            m_Each = new RadioButton
            {
                AutoSize = true,
                Checked = !s_Last.OneElement,
                Text = pt ? "Cada arquivo vira um botão" : "Each file becomes its own button"
            };
            m_One = new RadioButton
            {
                AutoSize = true,
                Checked = s_Last.OneElement,
                Text = pt ? "Todos os arquivos de cada pasta num botão só (playlist)"
                          : "All files of each folder in one button (playlist)"
            };
            structFlow.Controls.Add(m_Each);
            structFlow.Controls.Add(m_One);
            structBox.Controls.Add(structFlow);
            layout.Controls.Add(structBox);

            m_KindBox = new GroupBox { Text = pt ? "Tipo de playlist" : "Playlist kind", AutoSize = true, Dock = DockStyle.Fill };
            var kindFlow = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
            m_KindRandom = new RadioButton { AutoSize = true, Checked = s_Last.Kind == PlaylistKind.Random };
            m_KindSequential = new RadioButton { AutoSize = true, Checked = s_Last.Kind == PlaylistKind.Sequential };
            m_KindParallel = new RadioButton { AutoSize = true, Checked = s_Last.Kind == PlaylistKind.Parallel };
            kindFlow.Controls.Add(m_KindRandom);
            kindFlow.Controls.Add(m_KindSequential);
            kindFlow.Controls.Add(m_KindParallel);
            m_KindBox.Controls.Add(kindFlow);
            layout.Controls.Add(m_KindBox);

            m_EffectBox = new GroupBox { Text = pt ? "Ajustes" : "Settings", AutoSize = true, Dock = DockStyle.Fill };
            var table = new TableLayoutPanel { AutoSize = true, ColumnCount = 3, Dock = DockStyle.Fill };
            m_Volume = new NumericUpDown { Minimum = 0, Maximum = 100, Value = Math.Max(0, Math.Min(100, s_Last.Volume)), Width = 64 };
            m_FadeIn = new NumericUpDown { Minimum = 0, Maximum = 60, DecimalPlaces = 1, Increment = 0.5m, Value = Math.Min(60, s_Last.FadeInMs / 1000m), Width = 64 };
            m_FadeOut = new NumericUpDown { Minimum = 0, Maximum = 60, DecimalPlaces = 1, Increment = 0.5m, Value = Math.Min(60, s_Last.FadeOutMs / 1000m), Width = 64 };
            AddRow(table, 0, "Volume", m_Volume, "%");
            AddRow(table, 1, pt ? "Aparecer aos poucos (fade in)" : "Fade in", m_FadeIn, pt ? "seg." : "sec.");
            AddRow(table, 2, pt ? "Sumir aos poucos (fade out)" : "Fade out", m_FadeOut, pt ? "seg." : "sec.");
            m_Repeat = new CheckBox { AutoSize = true, Checked = s_Last.Repeat, Text = pt ? "Repetir sem parar" : "Repeat forever" };
            m_CrossFade = new CheckBox { AutoSize = true, Checked = s_Last.CrossFade, Text = pt ? "Transição suave entre uma música e a próxima (crossfade)" : "Smooth transition between tracks (crossfade)" };
            table.Controls.Add(m_Repeat, 0, 3);
            table.SetColumnSpan(m_Repeat, 3);
            table.Controls.Add(m_CrossFade, 0, 4);
            table.SetColumnSpan(m_CrossFade, 3);
            m_EffectBox.Controls.Add(table);
            layout.Controls.Add(m_EffectBox);

            var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 0) };
            var cancel = new Button { Text = pt ? "Cancelar" : "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);
            layout.Controls.Add(buttons);
            AcceptButton = ok;
            CancelButton = cancel;

            presetMusic.Click += (s, e) =>
            {
                m_Music.Checked = true;
                m_One.Checked = true;
                m_KindRandom.Checked = true;
            };
            presetEffects.Click += (s, e) =>
            {
                m_Sounds.Checked = true;
                m_Each.Checked = true;
            };

            EventHandler update = (s, e) => UpdateState();
            m_Music.CheckedChanged += update;
            m_Sounds.CheckedChanged += update;
            m_Each.CheckedChanged += update;
            m_One.CheckedChanged += update;
            UpdateState();
        }

        private static void AddRow(TableLayoutPanel table, int row, string label, Control box, string unit)
        {
            table.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 12, 6) }, 0, row);
            table.Controls.Add(box, 1, row);
            table.Controls.Add(new Label { Text = unit, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(4, 6, 3, 6) }, 2, row);
        }

        private void UpdateState()
        {
            bool sounds = m_Sounds.Checked;
            bool one = m_One.Checked;
            m_KindBox.Enabled = one;
            if (sounds)
            {
                m_KindRandom.Text = Tx("Sorteia um som a cada vez", "A random sound each time");
                m_KindSequential.Text = Tx("Um depois do outro, na ordem", "One after the other, in order");
                m_KindParallel.Text = Tx("Todos ao mesmo tempo", "All at the same time");
                m_KindParallel.Visible = true;
            }
            else
            {
                m_KindRandom.Text = Tx("Aleatória (as músicas tocam em ordem sorteada)", "Random (tracks play in random order)");
                m_KindSequential.Text = Tx("Em sequência (na ordem da pasta)", "In sequence (in the order of the folder)");
                m_KindParallel.Visible = false;
                if (m_KindParallel.Checked)
                    m_KindRandom.Checked = true;
            }
            m_Repeat.Enabled = !sounds && one;
            m_CrossFade.Enabled = !sounds;
        }
    }
}
