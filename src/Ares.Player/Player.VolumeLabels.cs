/*
 The labels of the three volume sliders in the header show the value ("Música 40%") and mute / restore the slider
 when they are clicked.
*/
using System;
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
            Action refresh = () => label.Text = name + " " + bar.Value + "%";
            bar.ValueChanged += (s, e) => refresh();
            refresh();
            label.Cursor = Cursors.Hand;
            ToolTip tip = new ToolTip();
            tip.SetToolTip(label, T("Clique para silenciar / voltar ao volume anterior", "Click to mute / go back to the previous volume"));
            label.Click += (s, e) =>
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
        }
    }
}
