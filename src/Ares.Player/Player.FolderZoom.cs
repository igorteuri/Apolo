/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 Zoom of the folder list (the size of its text) and the switch for the crossfade of repeated sound effects.
 The zoom is changed with Ctrl + mouse wheel over the list, Ctrl + "+" / "-" / "0" in the list, or in Extras > Folder text size.
*/
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Ares.Player
{
    public partial class Player
    {
        private const int FolderZoomMin = 70;
        private const int FolderZoomMax = 220;
        private float m_FolderBaseSize;
        private float m_FolderAppliedSize;

        private static int FolderZoom
        {
            get
            {
                int value;
                if (int.TryParse(Ares.CommonGUI.Preferences.Get("FolderZoom", "100"), out value))
                    return Math.Max(FolderZoomMin, Math.Min(FolderZoomMax, value));
                return 100;
            }
        }

        private void InitFolderZoom()
        {
            ToolStripMenuItem root = new ToolStripMenuItem(T("Tamanho do texto das pastas", "Folder text size"));
            ToolStripMenuItem bigger = new ToolStripMenuItem(T("Maior", "Bigger") + "   (Ctrl + roda do mouse)");
            ToolStripMenuItem smaller = new ToolStripMenuItem(T("Menor", "Smaller"));
            ToolStripMenuItem normal = new ToolStripMenuItem(T("Normal", "Normal") + "   (Ctrl+0)");
            bigger.Click += (s, e) => ChangeFolderZoom(10);
            smaller.Click += (s, e) => ChangeFolderZoom(-10);
            normal.Click += (s, e) => SetFolderZoom(100);
            root.DropDownItems.AddRange(new ToolStripItem[] { bigger, smaller, normal });
            extrasToolStripMenuItem.DropDownItems.Insert(0, root);

            modesList.MouseWheel += (s, e) =>
            {
                if ((Control.ModifierKeys & Keys.Control) == 0)
                    return;
                ChangeFolderZoom(e.Delta > 0 ? 10 : -10);
                HandledMouseEventArgs handled = e as HandledMouseEventArgs;
                if (handled != null)
                    handled.Handled = true;
            };
            modesList.KeyDown += (s, e) =>
            {
                if (!e.Control)
                    return;
                if (e.KeyCode == Keys.Oemplus || e.KeyCode == Keys.Add)
                    ChangeFolderZoom(10);
                else if (e.KeyCode == Keys.OemMinus || e.KeyCode == Keys.Subtract)
                    ChangeFolderZoom(-10);
                else if (e.KeyCode == Keys.D0 || e.KeyCode == Keys.NumPad0)
                    SetFolderZoom(100);
                else
                    return;
                e.Handled = true;
                e.SuppressKeyPress = true;
            };
            // the theme sets the font of the controls again when the interface size changes
            Ares.CommonGUI.ThemeManager.Changed += (s, e) => BeginInvoke((MethodInvoker)ApplyFolderZoom);
            ApplyFolderZoom();

            // repeated sound effects blend their end into their start (an option, on by default)
            Ares.Playing.SoundRepeat.CrossfadeEnabled = Ares.CommonGUI.Preferences.GetBool("SfxLoopCrossfade", true);
            ToolStripMenuItem soften = new ToolStripMenuItem(T("Suavizar a repetição dos efeitos (crossfade)", "Smooth the repetition of effects (crossfade)"));
            soften.CheckOnClick = true;
            soften.Checked = Ares.Playing.SoundRepeat.CrossfadeEnabled;
            soften.ToolTipText = T("Quando um efeito sonoro repete, o final se mistura com o começo, sem queda brusca de som.",
                "When a sound effect repeats, its end blends into its start, without a sudden drop.");
            soften.CheckedChanged += (s, e) =>
            {
                Ares.Playing.SoundRepeat.CrossfadeEnabled = soften.Checked;
                Ares.CommonGUI.Preferences.SetBool("SfxLoopCrossfade", soften.Checked);
            };
            extrasToolStripMenuItem.DropDownItems.Insert(1, soften);
        }

        private void ChangeFolderZoom(int delta)
        {
            SetFolderZoom(FolderZoom + delta);
        }

        private void SetFolderZoom(int percent)
        {
            Ares.CommonGUI.Preferences.Set("FolderZoom", Math.Max(FolderZoomMin, Math.Min(FolderZoomMax, percent)).ToString());
            ApplyFolderZoom();
        }

        private void ApplyFolderZoom()
        {
            if (modesList == null || modesList.IsDisposed)
                return;
            float current = modesList.Font.Size;
            // a size which is not the one applied here came from the theme: it is the new base
            if (m_FolderBaseSize <= 0 || Math.Abs(current - m_FolderAppliedSize) > 0.01f)
                m_FolderBaseSize = current;
            float wanted = m_FolderBaseSize * FolderZoom / 100f;
            if (Math.Abs(wanted - current) < 0.01f)
            {
                m_FolderAppliedSize = current;
                return;
            }
            Font old = modesList.Font;
            modesList.Font = new Font(old.FontFamily, wanted, old.Style);
            m_FolderAppliedSize = wanted;
            modesList.Invalidate();
        }
    }
}
