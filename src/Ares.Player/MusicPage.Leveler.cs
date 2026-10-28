/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 The leveler part of the "Music" page of the settings: the switch, three ready-made strengths, the four values behind them
 and a live display of what the leveler is doing now. Everything applies at once (so that it can be heard) and is kept.
*/
using System;
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
            groupBox1.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            groupBox1.SetBounds(0, 0, Math.Max(480, ClientSize.Width), 252);
            crossFadingUpDown.Width = 90;
            crossFadingUpDown.Left = label3.Right + 8;
            label2.Left = crossFadingUpDown.Right + 6;
            label3.Top = crossFadingUpDown.Top + Math.Max(0, (crossFadingUpDown.Height - label3.Height) / 2);
            label2.Top = label3.Top;

            int width = groupBox1.Width;
            m_LevelBox = new GroupBox();
            m_LevelBox.Text = T("Nivelador de volume", "Volume leveler");
            m_LevelBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
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
            m_Meter.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            m_LiveText = new Label();
            m_LiveText.SetBounds(14, y + 236, width - 40, 24);
            m_LiveText.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            m_LiveText.Tag = "Muted";
            Label help = new Label();
            help.SetBounds(14, y + 270, width - 40, 96);
            help.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
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
            effects.Text = T("Efeitos sonoros", "Sound effects");
            effects.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            effects.SetBounds(0, m_LevelBox.Bottom + 8, width, 96);
            CheckBox soften = new CheckBox();
            soften.Text = T("Suavizar a repetição dos efeitos (crossfade)", "Smooth the repetition of effects (crossfade)");
            soften.SetBounds(14, 26, width - 28, 26);
            soften.Checked = SoundRepeat.CrossfadeEnabled;
            soften.CheckedChanged += (s, e) =>
            {
                SoundRepeat.CrossfadeEnabled = soften.Checked;
                Preferences.SetBool("SfxLoopCrossfade", soften.Checked);
            };
            Label softenNote = new Label();
            softenNote.Text = T("Quando um efeito sonoro repete, o final se mistura com o começo, sem queda brusca de som.",
                "When a sound effect repeats, its end blends into its start, without a sudden drop.");
            softenNote.Tag = "Muted";
            softenNote.SetBounds(14, 54, width - 28, 36);
            effects.Controls.Add(soften);
            effects.Controls.Add(softenNote);
            Controls.Add(effects);
            AutoScrollMinSize = new Size(0, effects.Bottom + 8);

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
            bar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Label text = new Label();
            text.SetBounds(460, y, 90, 32);
            text.Anchor = AnchorStyles.Top | AnchorStyles.Right;
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
