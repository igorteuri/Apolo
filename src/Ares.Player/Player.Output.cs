using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Ares.Data;
using Ares.Players;
using Ares.Settings;
using Ares.CommonGUI;

namespace Ares.Player
{
    // The audio output device: the selector in the status bar, its popup and the change of the device.
    public partial class Player
    {
        private string OutputToolTip()
        {
            string lang = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            if (lang == "pt")
                return "Saída de áudio (placa de som)";
            if (lang == "de")
                return "Audioausgabe (Soundkarte)";
            return "Audio output (sound card)";
        }

        private static string ShortName(string name, int maxLength)
        {
            if (name == null)
                return string.Empty;
            return name.Length <= maxLength ? name : name.Substring(0, maxLength - 1) + "\u2026";
        }

        private string DeviceName(int deviceIndex)
        {
            try
            {
                foreach (Ares.Playing.AudioInit.OutputDevice device in Ares.Playing.AudioInit.GetDevices())
                {
                    if (device.Index == deviceIndex)
                        return device.Name;
                }
            }
            catch (Exception swallowed)
            {
                Ares.Settings.ErrorLog.Note(swallowed);
            }
            return deviceIndex.ToString();
        }

        private void UpdateOutputButton()
        {
            if (m_OutputButton == null)
                return;
            int index = Settings.Settings.Instance.OutputDeviceIndex;
            if (index == m_OutputShown)
                return;
            m_OutputShown = index;
            string name = DeviceName(index);
            m_OutputButton.Text = ShortName(name, 28) + "  ▾";
            m_OutputButton.ToolTipText = OutputToolTip() + ": " + name;
        }

        private Form m_OutputPopup;
        private int m_OutputPopupClosedAt;

        // The list of output devices: a small window of our own, in the colors of the theme (the system menu showed a light frame).
        private void ShowOutputPopup()
        {
            if (m_OutputPopup != null)
                return;
            if (unchecked(Environment.TickCount - m_OutputPopupClosedAt) < 250)
                return; // the click which closed it must not open it again
            List<Ares.Playing.AudioInit.OutputDevice> devices;
            try
            {
                devices = Ares.Playing.AudioInit.GetActiveDevices();
            }
            catch (Exception)
            {
                return;
            }
            if (devices.Count == 0)
                return;
            Ares.CommonGUI.ThemePalette p = Ares.CommonGUI.ThemeManager.Current;
            bool themed = p != null && !p.IsClassic;
            Color back = themed ? p.Surface : SystemColors.Window;
            Color fore = themed ? p.Text : SystemColors.WindowText;
            Color accent = themed ? p.Accent : SystemColors.Highlight;
            Color hover = themed ? p.Hover : SystemColors.ControlLight;
            Color line = themed ? p.Accent : SystemColors.ControlDark;
            int current = Settings.Settings.Instance.OutputDeviceIndex;
            int rowHeight = Math.Max(32, Font.Height + 14);

            Form pop = new Form();
            pop.FormBorderStyle = FormBorderStyle.None;
            pop.ShowInTaskbar = false;
            pop.StartPosition = FormStartPosition.Manual;
            pop.BackColor = line;
            pop.Padding = new Padding(1);
            pop.Font = Font;

            int hot = -1;
            ListBox list = new ListBox();
            list.BorderStyle = BorderStyle.None;
            list.DrawMode = DrawMode.OwnerDrawFixed;
            list.ItemHeight = rowHeight;
            list.IntegralHeight = false;
            list.BackColor = back;
            list.Dock = DockStyle.Fill;
            int widest = 0;
            foreach (Ares.Playing.AudioInit.OutputDevice d in devices)
            {
                list.Items.Add(d.Name);
                widest = Math.Max(widest, TextRenderer.MeasureText(d.Name, Font).Width);
            }
            list.DrawItem += (s, e) =>
            {
                if (e.Index < 0)
                    return;
                bool isCurrent = devices[e.Index].Index == current;
                using (SolidBrush b = new SolidBrush(e.Index == hot ? hover : back))
                    e.Graphics.FillRectangle(b, e.Bounds);
                if (isCurrent)
                {
                    using (SolidBrush bar = new SolidBrush(accent))
                        e.Graphics.FillRectangle(bar, e.Bounds.X, e.Bounds.Y + 4, 3, e.Bounds.Height - 8);
                }
                Rectangle text = new Rectangle(e.Bounds.X + 16, e.Bounds.Y, e.Bounds.Width - 24, e.Bounds.Height);
                using (Font f = new Font(Font, isCurrent ? FontStyle.Bold : FontStyle.Regular))
                    TextRenderer.DrawText(e.Graphics, list.Items[e.Index].ToString(), f, text, isCurrent ? accent : fore,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.Left);
            };
            list.MouseMove += (s, e) =>
            {
                int index = list.IndexFromPoint(e.Location);
                if (index != hot)
                {
                    hot = index;
                    list.Invalidate();
                }
            };
            list.MouseLeave += (s, e) => { hot = -1; list.Invalidate(); };
            Action<int> choose = index =>
            {
                if (index < 0 || index >= devices.Count)
                    return;
                int deviceIndex = devices[index].Index;
                pop.Close();
                ChangeOutputDevice(deviceIndex);
            };
            list.MouseClick += (s, e) => choose(list.IndexFromPoint(e.Location));
            list.KeyDown += (s, e) =>
            {
                if (e.KeyCode == System.Windows.Forms.Keys.Escape)
                    pop.Close();
                else if (e.KeyCode == System.Windows.Forms.Keys.Enter)
                    choose(list.SelectedIndex);
            };
            pop.Controls.Add(list);

            Screen screen = Screen.FromControl(this);
            int width = Math.Min(screen.WorkingArea.Width - 20, Math.Max(340, widest + 60));
            int height = Math.Min((int)(screen.WorkingArea.Height * 0.7), devices.Count * rowHeight + 2);
            ToolStrip owner = m_OutputButton.Owner;
            Rectangle button = owner.RectangleToScreen(m_OutputButton.Bounds);
            int x = Math.Max(screen.WorkingArea.X, Math.Min(button.Right - width, screen.WorkingArea.Right - width));
            int y = button.Top - height - 2;
            if (y < screen.WorkingArea.Y)
                y = button.Bottom + 2;
            pop.SetBounds(x, y, width, height);
            for (int i = 0; i < devices.Count; i++)
            {
                if (devices[i].Index == current)
                    list.SelectedIndex = i;
            }
            pop.Deactivate += (s, e) => pop.Close();
            pop.FormClosed += (s, e) =>
            {
                m_OutputPopupClosedAt = Environment.TickCount;
                m_OutputPopup = null;
                pop.Dispose();
                m_OutputButton.Invalidate();
            };
            m_OutputPopup = pop;
            m_OutputButton.Invalidate();
            pop.Show(this);
            list.Focus();
        }

        private void ChangeOutputDevice(int deviceIndex)
        {
            if (deviceIndex == Settings.Settings.Instance.OutputDeviceIndex)
                return;
            try
            {
                m_BassInit.SwitchDeviceKeepPlaying(deviceIndex);
                Settings.Settings.Instance.OutputDeviceIndex = deviceIndex;
                Settings.Settings.Instance.OutputDeviceName = Ares.Playing.AudioInit.NameOf(deviceIndex);
                WriteSettings();
            }
            catch (Ares.Playing.AudioInitException)
            {
                Ares.CommonGUI.MessageDialog.Show(this, String.Format(StringResources.DeviceInitError), StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            UpdateOutputButton();
        }

        private Image CreateSpeakerImage()
        {
            Image img = Ares.CommonGUI.IconLibrary.Get("volume-2", toolStrip1.ImageScalingSize, Ares.CommonGUI.ThemeManager.Current.Accent);
            return img ?? new Bitmap(20, 20);
        }

        private void SetOutputDevice()
        {
            try
            {
                // sound cards are saved with their name, because their numbers change when devices are plugged in or removed
                int index = Ares.Playing.AudioInit.ResolveIndex(Settings.Settings.Instance.OutputDeviceIndex, Settings.Settings.Instance.OutputDeviceName);
                m_BassInit.SwitchDevice(index);
                Settings.Settings.Instance.OutputDeviceIndex = index;
                Settings.Settings.Instance.OutputDeviceName = Ares.Playing.AudioInit.NameOf(index);
            }
            catch (Ares.Playing.AudioInitException)
            {
                Ares.CommonGUI.MessageDialog.Show(this, String.Format(StringResources.DeviceInitError), StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Error);
                Ares.Settings.Settings.Instance.OutputDeviceIndex = -1;
                Ares.Settings.Settings.Instance.OutputDeviceName = String.Empty;
            }
        }
    }
}
