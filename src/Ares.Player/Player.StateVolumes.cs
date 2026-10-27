/*
 The three volume sliders also live in the "State" panel (left), bound to the ones in the header: moving one moves the other.
 The header ones are for the day to day, these have more room for precision.
*/
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Ares.Player
{
    public partial class Player
    {
        private Panel m_StateVolumes;
        private const int StateVolumeRow = 30;
        private const int StateCaptionWidth = 100;
        private readonly Control[] m_StateCaptions = new Control[3];
        private readonly Ares.CommonGUI.ModernSlider[] m_StateSliders = new Ares.CommonGUI.ModernSlider[3];
        private bool m_StateLayoutPending;
        private bool m_StateLayouting;
        private string m_StateLayoutKey;

        private void InitStateVolumes(Label[] labels, Ares.CommonGUI.ModernSlider[] bars, EventHandler[] changed, EventHandler[] toggles)
        {
            m_StateVolumes = new Panel();
            m_StateVolumes.Name = "stateVolumes";
            m_StateVolumes.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            m_StateVolumes.Height = 3 * StateVolumeRow + 4;
            for (int i = 0; i < 3; i++)
            {
                Ares.CommonGUI.ModernSlider bar = bars[i];
                EventHandler handler = changed[i];
                PercentText caption = new PercentText(bar, labels[i], labels[i].Text);
                caption.Click += toggles[i];
                Ares.CommonGUI.ModernSlider state = new Ares.CommonGUI.ModernSlider();
                state.Minimum = bar.Minimum;
                state.Maximum = bar.Maximum;
                state.Value = bar.Value;
                // user moves this one: the header slider follows and the volume is applied as if it had been moved
                state.Scroll += (s, e) =>
                {
                    bar.Value = state.Value;
                    handler(bar, EventArgs.Empty);
                };
                // the header slider moved (by hand, by the remote control ...): this one follows
                bar.ValueChanged += (s, e) =>
                {
                    if (state.Value != bar.Value)
                        state.Value = bar.Value;
                };
                m_StateCaptions[i] = caption;
                m_StateSliders[i] = state;
                m_StateVolumes.Controls.Add(caption);
                m_StateVolumes.Controls.Add(state);
            }
            groupBox3.Controls.Add(m_StateVolumes);
            // the panel is moved by the layout of the columns after the anchors have worked: do it once it is quiet
            groupBox3.Layout += (s, e) =>
            {
                // our own SetBounds below raise this event again: ignore it, or the window never stops laying out
                if (m_StateLayoutPending || m_StateLayouting || !IsHandleCreated)
                    return;
                m_StateLayoutPending = true;
                BeginInvoke(new System.Windows.Forms.MethodInvoker(() =>
                {
                    m_StateLayoutPending = false;
                    LayoutStateBottom();
                }));
            };
            LayoutStateBottom();
        }

        // the bottom of the state panel: web audio on the very bottom, the volumes above it, the list of sounds takes the rest
        private void LayoutStateBottom()
        {
            if (m_SoundPanel == null || m_StateLayouting)
                return;
            int web = m_WebAudioPanel != null ? m_WebAudioPanel.Height : 0;
            // nothing changed since the last time: nothing to move
            string key = groupBox3.ClientSize + "|" + web + "|" + m_SoundPanel.Top;
            if (key == m_StateLayoutKey)
                return;
            m_StateLayouting = true;
            try
            {
                PlaceStateBottom(web);
            }
            finally
            {
                m_StateLayouting = false;
                m_StateLayoutKey = groupBox3.ClientSize + "|" + web + "|" + m_SoundPanel.Top;
            }
        }

        private void PlaceStateBottom(int web)
        {
            int bottom = groupBox3.ClientSize.Height - 8;
            if (m_WebAudioPanel != null)
            {
                m_WebAudioPanel.Top = groupBox3.ClientSize.Height - web - 4;
                if (web > 0)
                    bottom = m_WebAudioPanel.Top - 4;
            }
            if (m_StateVolumes != null)
            {
                // too short a panel: the volumes are only in the header
                bool room = groupBox3.ClientSize.Height > m_SoundPanel.Top + 160 + m_StateVolumes.Height;
                m_StateVolumes.Visible = room;
                if (room)
                {
                    int width = Math.Max(120, groupBox3.ClientSize.Width - 16);
                    m_StateVolumes.SetBounds(8, bottom - m_StateVolumes.Height, width, m_StateVolumes.Height);
                    for (int i = 0; i < 3; i++)
                    {
                        m_StateCaptions[i].SetBounds(0, i * StateVolumeRow + 2, StateCaptionWidth, StateVolumeRow);
                        m_StateSliders[i].SetBounds(StateCaptionWidth + 4, i * StateVolumeRow + 2, Math.Max(40, width - StateCaptionWidth - 4), StateVolumeRow);
                    }
                    bottom = m_StateVolumes.Top - 6;
                }
            }
            m_SoundPanel.Height = Math.Max(40, bottom - m_SoundPanel.Top);
        }
    }
}
