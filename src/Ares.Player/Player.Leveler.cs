/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 The switch of the leveler (a gentle compressor that evens out the volume between pieces of music) in the header, beside
 the volume sliders. On by default; the choice is kept. The processing itself is in Ares.Playing (LevelCompressor).
*/
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Ares.Player
{
    public partial class Player
    {
        private void InitLeveler()
        {
            LevelerSettings.Load();
            m_LevelLabel = new Label();
            m_LevelLabel.Name = "levelLabel";
            m_LevelLabel.Text = T("Nivelar", "Level");
            m_LevelLabel.Font = label3.Font;
            m_LevelLabel.AutoSize = false;
            m_LevelToggle = new Ares.CommonGUI.ModernToggle();
            m_LevelToggle.Name = "levelToggle";
            m_LevelToggle.Checked = Ares.Playing.AudioLeveler.Enabled;
            // the settings page can switch it too
            Ares.Playing.AudioLeveler.EnabledChanged += (s, e) =>
            {
                if (m_LevelToggle != null && !m_LevelToggle.IsDisposed && m_LevelToggle.Checked != Ares.Playing.AudioLeveler.Enabled)
                    m_LevelToggle.Checked = Ares.Playing.AudioLeveler.Enabled;
            };
            m_LevelToggle.CheckedChanged += (s, e) =>
            {
                Ares.Playing.AudioLeveler.Enabled = m_LevelToggle.Checked;
                LevelerSettings.Save();
                ShowToast(new Ares.Players.Message(Ares.Players.MessageType.Info,
                    m_LevelToggle.Checked ? T("Nivelador ligado: o volume entre as músicas fica mais parecido.", "Leveler on: the volume between pieces is evened out.")
                                          : T("Nivelador desligado: o áudio sai como está nos arquivos.", "Leveler off: the audio goes out as it is in the files.")));
            };
            ToolTip tip = new ToolTip();
            string help = T("Nivelador de volume (compressor suave): diminui a diferença de volume entre as músicas e evita estouros. Ligado = áudio mais uniforme; desligado = som original dos arquivos.",
                "Volume leveler (gentle compressor): reduces the volume difference between pieces and avoids peaks. On = more even sound; off = original sound of the files.");
            tip.SetToolTip(m_LevelToggle, help);
            tip.SetToolTip(m_LevelLabel, help);
            m_VolumePanel.Controls.Add(m_LevelLabel);
            m_VolumePanel.Controls.Add(m_LevelToggle);
        }
    }
}
