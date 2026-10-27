/*
 The labels of the three volume sliders in the header show the value ("Música 40%") and mute / restore the slider
 when they are clicked.
*/
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Ares.Player
{
    public partial class Player
    {
        private void InitVolumeLabels()
        {
            EventHandler[] toggles =
            {
                WireVolumeLabel(label2, overallVolumeBar, overallVolumeBar_Scroll),
                WireVolumeLabel(label3, musicVolumeBar, musicVolumeBar_Scroll),
                WireVolumeLabel(label4, soundVolumeBar, soundVolumeBar_Scroll)
            };
            // the same three sliders again in the state panel
            InitStateVolumes(new Label[] { label2, label3, label4 },
                new Ares.CommonGUI.ModernSlider[] { overallVolumeBar, musicVolumeBar, soundVolumeBar },
                new EventHandler[] { overallVolumeBar_Scroll, musicVolumeBar_Scroll, soundVolumeBar_Scroll }, toggles);
        }

        private EventHandler WireVolumeLabel(Label label, Ares.CommonGUI.ModernSlider bar, EventHandler changed)
        {
            string name = label.Text.TrimEnd(':', ' ');
            int before = 50; // the value to come back to after muting
            // the caption (name + percentage) is drawn by one control that takes the place of the label, so that the gap is always right
            PercentText percent = new PercentText(bar, label, name);
            label.Parent.Controls.Add(percent);
            label.Visible = false;
            Action place = () =>
            {
                percent.Font = label.Font;
                percent.SetBounds(label.Left, label.Top, label.Width, label.Height);
                percent.BringToFront();
            };
            Action refresh = () =>
            {
                label.Text = name;
                percent.Invalidate();
            };
            bar.ValueChanged += (s, e) => refresh();
            label.LocationChanged += (s, e) => place();
            label.SizeChanged += (s, e) => place();
            label.FontChanged += (s, e) => place();
            refresh();
            place();
            label.Cursor = Cursors.Hand;
            ToolTip tip = new ToolTip();
            tip.SetToolTip(label, T("Clique para silenciar / voltar ao volume anterior", "Click to mute / go back to the previous volume"));
            EventHandler toggle = (s, e) =>
            {
                if (bar.Value > 0)
                {
                    before = bar.Value;
                    bar.Value = 0;
                }
                else
                {
                    bar.Value = before;
                }
                changed(bar, EventArgs.Empty);
            };
            label.Click += toggle;
            percent.Click += toggle;
            tip.SetToolTip(percent, T("Clique para silenciar / voltar ao volume anterior", "Click to mute / go back to the previous volume"));
            return toggle;
        }

        // the "40%" of a volume slider: the highlight color of the theme, a little translucent (red when the volume is almost off)
        private class PercentText : Control
        {
            private readonly Ares.CommonGUI.ModernSlider m_Bar;
            private readonly Label m_Label;
            private readonly string m_Name;

            public PercentText(Ares.CommonGUI.ModernSlider bar, Label label, string name)
            {
                m_Bar = bar;
                m_Label = label;
                m_Name = name;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
                Cursor = Cursors.Hand;
                TabStop = false;
                Ares.CommonGUI.ThemeManager.Changed += OnThemeChanged;
            }

            private void OnThemeChanged(object sender, EventArgs e)
            {
                if (!IsDisposed)
                    Invalidate();
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    Ares.CommonGUI.ThemeManager.Changed -= OnThemeChanged;
                base.Dispose(disposing);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Color back = Parent != null ? Ares.CommonGUI.ThemeManager.EffectiveBack(this) : SystemColors.Control;
                e.Graphics.Clear(back);
                Ares.CommonGUI.ThemePalette p = Ares.CommonGUI.ThemeManager.Current;
                Color accent = p != null && !p.IsClassic ? p.Accent : SystemColors.Highlight;
                if (m_Bar.Enabled && m_Bar.Value <= m_Bar.LowWarningPercent * (m_Bar.Maximum - m_Bar.Minimum) / 100 + m_Bar.Minimum)
                    accent = Color.FromArgb(226, 60, 52);
                accent = Ares.CommonGUI.ThemeManager.EnsureContrast(accent, back, 3.0);
                // slightly translucent: 88% of the highlight over the background
                Color color = Color.FromArgb(255,
                    (int)Math.Round(back.R + (accent.R - back.R) * 0.88),
                    (int)Math.Round(back.G + (accent.G - back.G) * 0.88),
                    (int)Math.Round(back.B + (accent.B - back.B) * 0.88));
                TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding
                    | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;
                int nameWidth = TextRenderer.MeasureText(e.Graphics, m_Name, Font, new Size(int.MaxValue, int.MaxValue), flags).Width;
                TextRenderer.DrawText(e.Graphics, m_Name, Font, ClientRectangle, p != null && !p.IsClassic ? p.Text : m_Label.ForeColor, flags);
                Rectangle rest = new Rectangle(nameWidth + 5, 0, Math.Max(10, Width - nameWidth - 5), Height);
                TextRenderer.DrawText(e.Graphics, m_Bar.Value + "%", Font, rest, color, flags);
            }
        }
    }
}
