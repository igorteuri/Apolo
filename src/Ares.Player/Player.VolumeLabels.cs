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
            WireVolumeLabel(label2, overallVolumeBar, overallVolumeBar_Scroll);
            WireVolumeLabel(label3, musicVolumeBar, musicVolumeBar_Scroll);
            WireVolumeLabel(label4, soundVolumeBar, soundVolumeBar_Scroll);
        }

        private void WireVolumeLabel(Label label, Ares.CommonGUI.ModernSlider bar, EventHandler changed)
        {
            string name = label.Text.TrimEnd(':', ' ');
            int before = 50; // the value to come back to after muting
            PercentText percent = new PercentText(bar);
            label.Parent.Controls.Add(percent);
            // the percentage sits right after the name, in the highlight color
            Action place = () =>
            {
                Size size = TextRenderer.MeasureText(name + " ", label.Font, new Size(int.MaxValue, int.MaxValue),
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                percent.Font = label.Font;
                percent.SetBounds(label.Left + size.Width, label.Top, Math.Max(10, label.Width - size.Width), label.Height);
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
        }

        // the "40%" of a volume slider: the highlight color of the theme, a little translucent (red when the volume is almost off)
        private class PercentText : Control
        {
            private readonly Ares.CommonGUI.ModernSlider m_Bar;

            public PercentText(Ares.CommonGUI.ModernSlider bar)
            {
                m_Bar = bar;
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
                TextRenderer.DrawText(e.Graphics, m_Bar.Value + "%", Font, ClientRectangle, color,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            }
        }
    }
}
