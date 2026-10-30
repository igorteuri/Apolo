/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 The leveler part of the "Music" page of the settings: the switch, three ready-made strengths, the four values behind them
 and a live display of what the leveler is doing now. Everything applies at once (so that it can be heard) and is kept.
*/
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Ares.CommonGUI;
using Ares.Playing;

namespace Ares.Player
{
    public partial class MusicPage
    {
        private GroupBox m_LevelBox;
        private CheckBox m_LevelOn;
        private ThemedComboBox m_LevelPreset;
        private ModernSlider m_TargetBar, m_StrengthBar, m_BoostBar, m_CutBar;
        private Label m_TargetText, m_StrengthText, m_BoostText, m_CutText, m_LiveText;
        private GainMeter m_Meter;
        private Timer m_LiveTimer;
        private bool m_LevelListen = true;

        private static string T(string pt, string en)
        {
            return Loc.T(pt, en);
        }

        private void BuildLevelerUi()
        {
            AutoScroll = true;
            // the first group: the fields were placed for a narrow font (the time box was on top of its label)
            // (the designer docks it to Fill: in a tall page it then covered the effects and leveler groups)
            groupBox1.Dock = DockStyle.None;
            groupBox1.SetBounds(0, 0, Math.Max(480, ClientSize.Width), 252);
            groupBox1.Text = T("Transição entre músicas (fade e crossfade)", "Transition between pieces of music (fade and crossfade)");
            // laid out again from the top: the positions of the designer fitted a narrow font (the text "Duração da transição:"
            // was longer than its label and the time box sat on top of it)
            int gy = 28, gx = 14, line = Math.Max(26, Font.Height + 10);
            Control[] stack = { allChannelsCheckBox, label1, noFadeButton, fadeButton, crossFadeButton };
            foreach (Control c in stack)
            {
                c.AutoSize = true;
                c.Location = new Point(c is RadioButton ? gx + 22 : gx, gy);
                gy += c == allChannelsCheckBox ? line + 8 : line;
            }
            // the time row: a flow, so that the box always follows the real width of the text
            FlowLayoutPanel timeRow = new FlowLayoutPanel();
            timeRow.AutoSize = true;
            timeRow.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            timeRow.WrapContents = false;
            timeRow.FlowDirection = FlowDirection.LeftToRight;
            timeRow.Location = new Point(gx, gy + 6);
            groupBox1.Controls.Add(timeRow);
            label3.AutoSize = true;
            label3.Margin = new Padding(0, 5, 14, 0);
            label2.AutoSize = true;
            label2.Margin = new Padding(8, 5, 0, 0);
            crossFadingUpDown.Width = 110;
            crossFadingUpDown.Margin = new Padding(0);
            timeRow.Controls.Add(label3);
            timeRow.Controls.Add(crossFadingUpDown);
            timeRow.Controls.Add(label2);

            int width = groupBox1.Width;
            m_LevelBox = new GroupBox();
            m_LevelBox.Text = T("Nivelador de volume", "Volume leveler");
            m_LevelBox.SetBounds(0, groupBox1.Bottom + 8, width, 400);
            Controls.Add(m_LevelBox);

            m_LevelOn = new CheckBox();
            m_LevelOn.Text = T("Ligar o nivelador", "Turn the leveler on");
            m_LevelOn.SetBounds(14, 26, 320, 26);
            m_LevelOn.Checked = AudioLeveler.Enabled;
            m_LevelOn.CheckedChanged += (s, e) =>
            {
                if (!m_LevelListen)
                    return;
                AudioLeveler.Enabled = m_LevelOn.Checked;
                LevelerSettings.Save();
            };
            m_LevelBox.Controls.Add(m_LevelOn);

            Label presetLabel = new Label();
            presetLabel.Text = T("Intensidade:", "Strength:");
            presetLabel.SetBounds(14, 62, 130, 28);
            presetLabel.TextAlign = ContentAlignment.MiddleLeft;
            m_LevelPreset = new ThemedComboBox();
            m_LevelPreset.DropDownStyle = ComboBoxStyle.DropDownList;
            foreach (LevelerSettings.Preset p in LevelerSettings.Presets)
                m_LevelPreset.Items.Add(T(p.Pt, p.En));
            m_LevelPreset.Items.Add(T("Personalizado", "Custom"));
            m_LevelPreset.SetBounds(150, 62, 300, 28);
            m_LevelPreset.SelectedIndexChanged += (s, e) =>
            {
                if (!m_LevelListen || m_LevelPreset.SelectedIndex < 0 || m_LevelPreset.SelectedIndex >= LevelerSettings.Presets.Length)
                    return;
                LevelerSettings.Apply(LevelerSettings.Presets[m_LevelPreset.SelectedIndex]);
                LevelerSettings.Save();
                ReadLevelerValues();
            };
            m_LevelBox.Controls.Add(presetLabel);
            m_LevelBox.Controls.Add(m_LevelPreset);

            int y = 104;
            m_TargetBar = AddLevelRow(T("Volume alvo", "Target volume"), 10, 36, y, out m_TargetText,
                v => AudioLeveler.TargetDb = -v);
            m_StrengthBar = AddLevelRow(T("Força", "Strength"), 0, 100, y + 38, out m_StrengthText,
                v => AudioLeveler.Strength = v / 100f);
            m_BoostBar = AddLevelRow(T("Aumento máximo", "Maximum boost"), 0, 24, y + 76, out m_BoostText,
                v => AudioLeveler.MaxBoostDb = v);
            m_CutBar = AddLevelRow(T("Redução máxima", "Maximum cut"), 0, 30, y + 114, out m_CutText,
                v => AudioLeveler.MaxCutDb = v);

            Button reset = new Button();
            reset.Text = T("Restaurar o padrão", "Restore default");
            reset.SetBounds(14, y + 158, 190, 34);
            reset.Click += (s, e) =>
            {
                LevelerSettings.Apply(LevelerSettings.Presets[0]);
                LevelerSettings.Save();
                ReadLevelerValues();
            };
            m_LevelBox.Controls.Add(reset);

            Label now = new Label();
            now.Text = T("Agora:", "Now:");
            now.SetBounds(14, y + 206, 60, 26);
            now.TextAlign = ContentAlignment.MiddleLeft;
            m_Meter = new GainMeter();
            m_Meter.SetBounds(80, y + 208, 380, 22);
            m_LiveText = new Label();
            m_LiveText.SetBounds(14, y + 236, width - 40, 24);
            m_LiveText.Tag = "Muted";
            Label help = new Label();
            help.SetBounds(14, y + 270, width - 40, 96);
            help.Tag = "Muted";
            help.Text = T(
                "O nivelador aproxima o volume de músicas altas e baixas, sem estourar. Volume alvo: o nível para onde ele leva o som (mais alto = som mais forte). Força: quanto da diferença ele corrige (100% deixa tudo no mesmo nível). Aumento e redução máximos: o quanto ele pode subir ou baixar o volume. A barra mostra o ajuste feito agora: à direita ele está aumentando, à esquerda diminuindo.",
                "The leveler brings loud and quiet pieces closer in volume, without clipping. Target volume: the level it leads the sound to (higher = louder). Strength: how much of the difference it corrects (100% puts everything at one level). Maximum boost and cut: how far it may raise or lower the volume. The bar shows the adjustment being made now: to the right it is raising, to the left lowering.");
            m_LevelBox.Controls.Add(now);
            m_LevelBox.Controls.Add(m_Meter);
            m_LevelBox.Controls.Add(m_LiveText);
            m_LevelBox.Controls.Add(help);
            m_LevelBox.Height = y + 376;
            GroupBox effects = new GroupBox();
            effects.Text = T("Repetição de efeitos sonoros (loop)", "Repetition of sound effects (loop)");
            effects.SetBounds(0, groupBox1.Bottom + 8, width, 116);
            CheckBox soften = new CheckBox();
            soften.Text = T("Suavizar o ponto em que o efeito volta ao começo", "Smooth the point where the effect returns to its start");
            soften.SetBounds(14, 26, width - 28, 26);
            soften.Checked = SoundRepeat.CrossfadeEnabled;
            soften.CheckedChanged += (s, e) =>
            {
                SoundRepeat.CrossfadeEnabled = soften.Checked;
                Preferences.SetBool("SfxLoopCrossfade", soften.Checked);
            };
            Label softenNote = new Label();
            softenNote.Text = T("Vale só para efeitos sonoros que se repetem: o final se mistura com o começo (crossfade do próprio efeito), sem corte ou estalo. É diferente do crossfade entre músicas, acima.",
                "Only for sound effects that repeat: the end blends into the start (crossfade of the effect itself), without a cut or click. It is not the crossfade between pieces of music, above.");
            softenNote.Tag = "Muted";
            softenNote.SetBounds(14, 54, width - 28, 52);
            effects.Controls.Add(soften);
            effects.Controls.Add(softenNote);
            Controls.Add(effects);
            m_LevelBox.Top = effects.Bottom + 8; // the two kinds of crossfade are together, the leveler comes after
            AutoScrollMinSize = new Size(0, m_LevelBox.Bottom + 8);
            m_EffectsBox = effects;
            m_EffectsSoften = soften;
            m_EffectsNote = softenNote;
            m_LevelHelp = help;
            // the boxes follow the real width of the page (small windows cut off the sliders)
            SizeChanged += (s, e) => FitLevelerWidth();
            FitLevelerWidth();

            ReadLevelerValues();
            AudioLeveler.EnabledChanged += OnLevelerEnabledChanged;
            m_LiveTimer = new Timer();
            m_LiveTimer.Interval = 250;
            m_LiveTimer.Tick += (s, e) => UpdateLive();
            HandleCreated += (s, e) => m_LiveTimer.Start();
            HandleDestroyed += (s, e) => m_LiveTimer.Stop();
            Disposed += (s, e) =>
            {
                AudioLeveler.EnabledChanged -= OnLevelerEnabledChanged;
                m_LiveTimer.Dispose();
            };
        }

        private void OnLevelerEnabledChanged(object sender, EventArgs e)
        {
            if (IsDisposed || m_LevelOn == null)
                return;
            m_LevelListen = false;
            m_LevelOn.Checked = AudioLeveler.Enabled;
            m_LevelListen = true;
        }

        private GroupBox m_EffectsBox;
        private CheckBox m_EffectsSoften;
        private Label m_EffectsNote, m_LevelHelp;
        private readonly List<KeyValuePair<ModernSlider, Label>> m_LevelRows = new List<KeyValuePair<ModernSlider, Label>>();

        private void FitLevelerWidth()
        {
            if (m_LevelBox == null || m_EffectsBox == null)
                return;
            int width = Math.Max(300, ClientSize.Width - 2);
            groupBox1.Width = width;
            m_EffectsBox.Width = width;
            m_LevelBox.Width = width;
            int valueWidth = Math.Max(70, TextRenderer.MeasureText("-36 dB  ", Font).Width + 10);
            foreach (KeyValuePair<ModernSlider, Label> row in m_LevelRows)
            {
                row.Value.SetBounds(width - valueWidth - 14, row.Key.Top, valueWidth, row.Key.Height);
                row.Key.Width = Math.Max(80, row.Value.Left - 8 - row.Key.Left);
            }
            m_Meter.Width = Math.Max(80, width - m_Meter.Left - 20);
            m_LiveText.Width = width - 40;
            m_LevelHelp.Width = width - 40;
            m_EffectsSoften.Width = width - 28;
            m_EffectsNote.Width = width - 28;
        }

        private ModernSlider AddLevelRow(string name, int min, int max, int y, out Label valueText, Action<int> set)
        {
            Label label = new Label();
            label.Text = name;
            label.SetBounds(14, y, 140, 32);
            label.TextAlign = ContentAlignment.MiddleLeft;
            ModernSlider bar = new ModernSlider();
            bar.Minimum = min;
            bar.Maximum = max;
            bar.SetBounds(160, y, 290, 32);
            Label text = new Label();
            text.SetBounds(460, y, 90, 32);
            text.TextAlign = ContentAlignment.MiddleLeft;
            Label shown = text;
            bar.Scroll += (s, e) =>
            {
                if (!m_LevelListen)
                    return;
                set(bar.Value);
                LevelerSettings.Save();
                ShowLevelerValues();
            };
            m_LevelRows.Add(new KeyValuePair<ModernSlider, Label>(bar, text));
            m_LevelBox.Controls.Add(label);
            m_LevelBox.Controls.Add(bar);
            m_LevelBox.Controls.Add(text);
            valueText = shown;
            return bar;
        }

        // the sliders and the texts show the values in force
        private void ReadLevelerValues()
        {
            m_LevelListen = false;
            m_LevelOn.Checked = AudioLeveler.Enabled;
            m_TargetBar.Value = (int)Math.Round(-AudioLeveler.TargetDb);
            m_StrengthBar.Value = (int)Math.Round(AudioLeveler.Strength * 100);
            m_BoostBar.Value = (int)Math.Round(AudioLeveler.MaxBoostDb);
            m_CutBar.Value = (int)Math.Round(AudioLeveler.MaxCutDb);
            m_LevelListen = true;
            ShowLevelerValues();
        }

        private void ShowLevelerValues()
        {
            m_TargetText.Text = (-m_TargetBar.Value) + " dB";
            m_StrengthText.Text = m_StrengthBar.Value + "%";
            m_BoostText.Text = "+" + m_BoostBar.Value + " dB";
            m_CutText.Text = "-" + m_CutBar.Value + " dB";
            int preset = LevelerSettings.CurrentPreset();
            m_LevelListen = false;
            m_LevelPreset.SelectedIndex = preset >= 0 ? preset : LevelerSettings.Presets.Length;
            m_LevelListen = true;
            m_Meter.Range = Math.Max(6f, Math.Max(AudioLeveler.MaxBoostDb, AudioLeveler.MaxCutDb));
        }

        private void UpdateLive()
        {
            if (m_LiveText == null || !Visible)
                return;
            bool active = unchecked(Environment.TickCount - AudioLeveler.ActiveTick) < 1500;
            if (!AudioLeveler.Enabled)
            {
                m_Meter.Value = 0;
                m_LiveText.Text = T("Nivelador desligado: o áudio sai como está nos arquivos.", "Leveler off: the audio goes out as it is in the files.");
            }
            else if (!active)
            {
                m_Meter.Value = 0;
                m_LiveText.Text = T("Nada tocando agora. Toque uma música para ver o nivelador trabalhando.", "Nothing playing now. Play something to see the leveler at work.");
            }
            else
            {
                float gain = AudioLeveler.GainDb;
                m_Meter.Value = gain;
                string what = Math.Abs(gain) < 0.3f ? T("sem ajuste", "no change")
                    : (gain > 0 ? T("aumentando ", "raising ") : T("diminuindo ", "lowering ")) + Math.Abs(gain).ToString("0.0") + " dB";
                m_LiveText.Text = T("Volume do áudio: ", "Audio volume: ") + AudioLeveler.LevelDb.ToString("0") + " dB  ·  " + what;
            }
        }

        // a bar with the zero in the middle: to the right the leveler raises the volume, to the left it lowers it
        private sealed class GainMeter : Control
        {
            private float m_Value;
            private float m_Range = 12f;

            public GainMeter()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
                TabStop = false;
            }

            public float Value
            {
                get { return m_Value; }
                set { if (Math.Abs(m_Value - value) > 0.02f) { m_Value = value; Invalidate(); } }
            }

            public float Range
            {
                get { return m_Range; }
                set { if (m_Range != value) { m_Range = value; Invalidate(); } }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                ThemePalette p = ThemeManager.Current;
                bool themed = p != null && !p.IsClassic;
                Graphics g = e.Graphics;
                g.Clear(Parent != null ? ThemeManager.EffectiveBack(this) : SystemColors.Control);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                Rectangle track = new Rectangle(0, Height / 2 - 5, Width - 1, 10);
                using (SolidBrush back = new SolidBrush(themed ? p.Surface : SystemColors.ControlLight))
                    g.FillRectangle(back, track);
                using (Pen line = new Pen(themed ? p.ControlBorder : SystemColors.ControlDark))
                    g.DrawRectangle(line, track);
                int mid = track.X + track.Width / 2;
                float f = Math.Max(-1f, Math.Min(1f, m_Value / Math.Max(1f, m_Range)));
                int w = (int)Math.Round(Math.Abs(f) * (track.Width / 2 - 1));
                if (w > 0)
                {
                    Rectangle fill = f > 0 ? new Rectangle(mid, track.Y + 1, w, track.Height - 1) : new Rectangle(mid - w, track.Y + 1, w, track.Height - 1);
                    using (SolidBrush b = new SolidBrush(themed ? p.Accent : SystemColors.Highlight))
                        g.FillRectangle(b, fill);
                }
                using (Pen zero = new Pen(themed ? p.Text : SystemColors.ControlText, 2f))
                    g.DrawLine(zero, mid, track.Y - 3, mid, track.Bottom + 3);
            }
        }
    }
}
