/*
 Repeat button of the player: three modes, like in a music app.
   0 = off, 1 = the playlist starts over when it ends, 2 = the current track repeats.
 The engine does the work (PlayingControl.MusicRepeatMode); this file only shows the mode on the button.
*/
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Ares.Player
{
    public partial class Player
    {
        private int m_RepeatModeShown = -1;

        private string RepeatModeText(int mode)
        {
            if (mode == 2)
                return T("só esta faixa", "this track only");
            if (mode == 1)
                return T("a playlist toda", "whole playlist");
            return T("desligada", "off");
        }

        private void ShowRepeatMode(int mode)
        {
            if (mode == m_RepeatModeShown)
                return;
            bool first = m_RepeatModeShown == -1;
            m_RepeatModeShown = mode;
            if (!first)
                Ares.CommonGUI.Preferences.Set("MusicRepeatMode", mode.ToString());
            repeatButton.Checked = mode != 0;
            repeatCurrentMusicToolStripMenuItem.Checked = mode != 0;
            repeatButton.ToolTipText = T("Repetir: ", "Repeat: ") + RepeatModeText(mode)
                + T(" (clique para trocar: desligada > playlist > faixa)", " (click to change: off > playlist > track)");
            repeatButton.Invalidate();
        }

        // the "1" in the middle of the arrows when only the current track repeats
        private void RepeatButton_Paint(object sender, PaintEventArgs e)
        {
            if (m_RepeatModeShown != 2)
                return;
            Rectangle r = repeatButton.ContentRectangle;
            int side = repeatButton.Image != null ? repeatButton.Image.Height : Math.Min(r.Width, r.Height);
            float px = Math.Max(8f, side * 0.42f);
            using (Font f = new Font(Font.FontFamily, px, FontStyle.Bold, GraphicsUnit.Pixel))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (SolidBrush b = new SolidBrush(Ares.CommonGUI.IconLibrary.ThemeColor()))
            {
                e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                e.Graphics.DrawString("1", f, b, new RectangleF(r.X, r.Y + 1, r.Width, r.Height), sf);
            }
        }
    }
}
