using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Ares.Player
{
    // Settings for sending the Ares audio to other people through VDO.Ninja (https://vdo.ninja).
    // Ares plays on a virtual audio cable; the VDO.Ninja page (shown in the Ares web window)
    // captures that cable as its microphone and streams it to the listeners.
    internal class ShareDialog : Form
    {
        private TextBox m_IdBox;
        private TextBox m_LabelBox;
        private ComboBox m_DeviceBox;
        private NumericUpDown m_BitrateBox;
        private CheckBox m_ProBox;
        private TextBox m_PushBox;
        private TextBox m_ViewBox;
        private bool m_Loading = true;

        private static bool IsPt
        {
            get { return System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt"; }
        }

        private static string T(string pt, string en)
        {
            return IsPt ? pt : en;
        }

        public ShareDialog()
        {
            Text = T("Compartilhar via VDO.Ninja", "Share via VDO.Ninja");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Font;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(12);

            TableLayoutPanel table = new TableLayoutPanel();
            table.AutoSize = true;
            table.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            table.ColumnCount = 2;
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            table.Dock = DockStyle.Fill;
            Controls.Add(table);

            Label info = new Label();
            info.AutoSize = true;
            info.MaximumSize = new Size(560, 0);
            info.Margin = new Padding(0, 0, 0, 10);
            info.Text = T(
                "Como funciona: o Apolo tem um microfone virtual próprio, o \"Apolo Audio\". Ele leva para o VDO.Ninja (dentro do Apolo) exatamente o som que o Apolo está tocando, qualquer que seja o dispositivo de saída escolhido, e envia para quem abrir o link do ouvinte. Não precisa instalar nenhum cabo virtual (exige Windows 10 versão 2004 ou o Windows 11).\n\n"
                + "1. Deixe a entrada de áudio como \"Apolo Audio\" (ou escolha outro microfone/cabo, se preferir).\n"
                + "2. Ajuste as opções abaixo e clique em \"Aplicar\". A transmissão começa sozinha.\n"
                + "3. Mande o link do ouvinte para seus jogadores.",
                "How it works: Apolo has its own virtual microphone, \"Apolo Audio\". It carries exactly the sound Apolo is playing, whatever output device is chosen, to VDO.Ninja (inside Apolo) and sends it to everyone who opens the listener link. No virtual cable is needed (requires Windows 10 version 2004 or Windows 11).\n\n"
                + "1. Leave the audio input as \"Apolo Audio\" (or choose another microphone/cable if you prefer).\n"
                + "2. Adjust the options below and click \"Apply\". The broadcast starts by itself.\n"
                + "3. Send the listener link to your players.");
            table.Controls.Add(info, 0, 0);
            table.SetColumnSpan(info, 2);

            m_IdBox = AddRow(table, 1, T("ID da transmissão:", "Stream ID:"), new TextBox());
            m_LabelBox = AddRow(table, 2, T("Nome exibido:", "Display name:"), new TextBox());
            m_DeviceBox = new ComboBox();
            m_DeviceBox.DropDownStyle = ComboBoxStyle.DropDown;
            FillDevices();
            AddRow(table, 3, T("Entrada de áudio (a que será enviada):", "Audio input (the one that is sent):"), m_DeviceBox);
            m_BitrateBox = new NumericUpDown();
            m_BitrateBox.Minimum = 32;
            m_BitrateBox.Maximum = 510;
            m_BitrateBox.Value = 128;
            AddRow(table, 4, T("Qualidade (kbps):", "Quality (kbps):"), m_BitrateBox);
            m_ProBox = new CheckBox();
            m_ProBox.AutoSize = true;
            m_ProBox.Text = T("Áudio de alta qualidade (sem filtros de voz, estéreo)", "High quality audio (no voice filters, stereo)");
            table.Controls.Add(m_ProBox, 1, 5);

            m_PushBox = AddRow(table, 6, T("Link de envio:", "Sender link:"), new TextBox());
            m_ViewBox = AddRow(table, 7, T("Link do ouvinte:", "Listener link:"), new TextBox());
            m_PushBox.ReadOnly = true;
            m_ViewBox.ReadOnly = true;
            m_PushBox.Width = 380;
            m_ViewBox.Width = 380;
            m_IdBox.Width = 380;
            m_LabelBox.Width = 380;
            m_DeviceBox.Width = 380;

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.AutoSize = true;
            buttons.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            buttons.Margin = new Padding(0, 10, 0, 0);
            Button apply = MakeButton(T("Aplicar", "Apply"), (s, e) => { DialogResult = DialogResult.OK; Close(); });
            buttons.Controls.Add(apply);
            buttons.Controls.Add(MakeButton(T("Copiar link do ouvinte", "Copy listener link"), (s, e) => CopyText(m_ViewBox.Text)));
            buttons.Controls.Add(MakeButton(T("Copiar link de envio", "Copy sender link"), (s, e) => CopyText(m_PushBox.Text)));
            buttons.Controls.Add(MakeButton(T("Atualizar dispositivos", "Refresh devices"), (s, e) => FillDevices()));
            buttons.Controls.Add(MakeButton(T("Novo ID", "New ID"), (s, e) => { m_IdBox.Text = NewId(); }));
            buttons.Controls.Add(MakeButton(T("Cancelar", "Cancel"), (s, e) => { DialogResult = DialogResult.Cancel; Close(); }));
            AcceptButton = apply;
            table.Controls.Add(buttons, 0, 8);
            table.SetColumnSpan(buttons, 2);

            // load saved values
            string id = Ares.CommonGUI.Preferences.Get("VdoId", "");
            if (string.IsNullOrEmpty(id))
                id = NewId();
            m_IdBox.Text = id;
            m_LabelBox.Text = Ares.CommonGUI.Preferences.Get("VdoLabel", T("Música", "Music"));
            m_DeviceBox.Text = Ares.CommonGUI.Preferences.Get("VdoDevice", "");
            if (m_DeviceBox.Text.Trim().Length == 0)
                m_DeviceBox.Text = AutoDevice();
            int kbps;
            if (!int.TryParse(Ares.CommonGUI.Preferences.Get("VdoBitrate", "128"), out kbps) || kbps < 32 || kbps > 510)
                kbps = 128;
            m_BitrateBox.Value = kbps;
            m_ProBox.Checked = Ares.CommonGUI.Preferences.GetBool("VdoPro", true);
            m_Loading = false;

            EventHandler changed = (s, e) => UpdateLinks();
            m_IdBox.TextChanged += changed;
            m_LabelBox.TextChanged += changed;
            m_DeviceBox.TextChanged += changed;
            m_BitrateBox.ValueChanged += changed;
            m_ProBox.CheckedChanged += changed;
            UpdateLinks();
        }

        // ------------------------------------------------------------------ audio input devices

        /// <summary>Names of the active Windows recording devices, as the browser shows them, e.g. "CABLE Output (VB-Audio Virtual Cable)".</summary>
        public static List<string> GetInputDevices()
        {
            List<string> result = new List<string>();
            try
            {
                // Ares is a 32-bit program: look at the 64-bit registry view, where Windows keeps the audio devices
                string path = @"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Capture";
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (RegistryKey capture = baseKey.OpenSubKey(path))
                {
                    if (capture == null)
                        return result;
                    foreach (string id in capture.GetSubKeyNames())
                    {
                        try
                        {
                            using (RegistryKey dev = capture.OpenSubKey(id))
                            {
                                object state = dev.GetValue("DeviceState");
                                if (state == null || Convert.ToInt32(state) != 1)
                                    continue;
                                using (RegistryKey props = dev.OpenSubKey("Properties"))
                                {
                                    if (props == null)
                                        continue;
                                    string desc = props.GetValue("{a45c254e-df1c-4efd-8020-67d146a850e0},2") as string;
                                    string adapter = props.GetValue("{b3f8fa53-0004-438e-9003-51a46e139bfc},6") as string;
                                    if (string.IsNullOrEmpty(desc))
                                        continue;
                                    string name = string.IsNullOrEmpty(adapter) ? desc : desc + " (" + adapter + ")";
                                    if (!result.Contains(name))
                                        result.Add(name);
                                }
                            }
                        }
                        catch (Exception)
                        {
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
            result.Sort(StringComparer.CurrentCultureIgnoreCase);
            return result;
        }

        /// <summary>Best guess for the device to send: a virtual cable output if there is one.</summary>
        public static string AutoDevice()
        {
            // Apolo has its own virtual microphone; no cable is needed
            return VirtualMic.DeviceName;
        }

        private void FillDevices()
        {
            string current = m_DeviceBox.Text;
            m_DeviceBox.Items.Clear();
            m_DeviceBox.Items.Add(VirtualMic.DeviceName);
            foreach (string d in GetInputDevices())
                m_DeviceBox.Items.Add(d);
            m_DeviceBox.Text = current;
        }

        private static TC AddRow<TC>(TableLayoutPanel table, int row, string caption, TC control) where TC : Control
        {
            Label label = new Label();
            label.AutoSize = true;
            label.Text = caption;
            label.Anchor = AnchorStyles.Left;
            label.Margin = new Padding(0, 6, 10, 6);
            table.Controls.Add(label, 0, row);
            control.Anchor = AnchorStyles.Left;
            control.Margin = new Padding(0, 3, 0, 3);
            table.Controls.Add(control, 1, row);
            return control;
        }

        private static Button MakeButton(string text, EventHandler onClick)
        {
            Button b = new Button();
            b.Text = text;
            b.AutoSize = true;
            b.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            b.Padding = new Padding(6, 2, 6, 2);
            b.Click += onClick;
            return b;
        }

        private static string NewId()
        {
            const string chars = "abcdefghijkmnpqrstuvwxyz23456789";
            Random rnd = new Random();
            StringBuilder sb = new StringBuilder("apolo");
            for (int i = 0; i < 8; ++i)
                sb.Append(chars[rnd.Next(chars.Length)]);
            return sb.ToString();
        }

        public static string CleanStreamId(string s)
        {
            return CleanId(s ?? "");
        }

        private static string CleanId(string s)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in s)
            {
                if (char.IsLetterOrDigit(c) && c < 128)
                    sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>Sender link built from the saved settings (null if there is no stream id yet).</summary>
        public static string BuildPushUrl()
        {
            string id = CleanId(Ares.CommonGUI.Preferences.Get("VdoId", ""));
            if (id.Length == 0)
            {
                id = NewId();
                Ares.CommonGUI.Preferences.Set("VdoId", id);
            }
            string label = Ares.CommonGUI.Preferences.Get("VdoLabel", T("Música", "Music")).Trim();
            string device = Ares.CommonGUI.Preferences.Get("VdoDevice", "").Trim();
            if (device.Length == 0)
                device = AutoDevice();
            int kbps;
            if (!int.TryParse(Ares.CommonGUI.Preferences.Get("VdoBitrate", "128"), out kbps) || kbps < 32 || kbps > 510)
                kbps = 128;
            bool pro = Ares.CommonGUI.Preferences.GetBool("VdoPro", true);
            return BuildPush(id, label, device, kbps, pro);
        }

        public static string BuildViewUrl()
        {
            BuildPushUrl();
            return BuildView(CleanId(Ares.CommonGUI.Preferences.Get("VdoId", "")), Ares.CommonGUI.Preferences.Get("VdoLabel", T("Música", "Music")).Trim(), ReadKbps());
        }

        private static int ReadKbps()
        {
            int kbps;
            if (!int.TryParse(Ares.CommonGUI.Preferences.Get("VdoBitrate", "128"), out kbps) || kbps < 32 || kbps > 510)
                kbps = 128;
            return kbps;
        }

        private static string BuildView(string id, string label, int kbps)
        {
            StringBuilder view = new StringBuilder("https://vdo.ninja/?view=" + id + "&vd=0&style=2&meterstyle=1");
            if (label.Length > 0)
                view.Append("&l=").Append(Uri.EscapeDataString(label));
            view.Append("&proaudio=1&volumecontrols&audiobitrate=").Append(kbps.ToString());
            return view.ToString();
        }

        private static string BuildPush(string id, string label, string device, int kbps, bool pro)
        {
            StringBuilder push = new StringBuilder("https://vdo.ninja/?push=" + id + "&vd=0&webcam&autostart");
            if (label.Length > 0)
                push.Append("&l=").Append(Uri.EscapeDataString(label));
            // the virtual microphone of Apolo is found by its first word; other devices by their whole name
            if (device == VirtualMic.DeviceName)
                device = "Apolo";
            device = device.Replace(' ', '_');
            if (device.Length > 0)
                push.Append("&ad=").Append(Uri.EscapeDataString(device));
            if (pro)
                push.Append("&proaudio=1");
            push.Append("&audiobitrate=").Append(kbps.ToString());
            return push.ToString();
        }

        private void UpdateLinks()
        {
            string id = CleanId(m_IdBox.Text);
            if (id.Length == 0)
            {
                m_PushBox.Text = "";
                m_ViewBox.Text = "";
                return;
            }
            m_PushBox.Text = BuildPush(id, m_LabelBox.Text.Trim(), m_DeviceBox.Text.Trim(), (int)m_BitrateBox.Value, m_ProBox.Checked);
            m_ViewBox.Text = BuildView(id, m_LabelBox.Text.Trim(), (int)m_BitrateBox.Value);
            if (!m_Loading)
                SavePrefs(id);
        }

        private void SavePrefs(string id)
        {
            Ares.CommonGUI.Preferences.Set("VdoId", id);
            Ares.CommonGUI.Preferences.Set("VdoLabel", m_LabelBox.Text.Trim());
            Ares.CommonGUI.Preferences.Set("VdoDevice", m_DeviceBox.Text.Trim());
            Ares.CommonGUI.Preferences.Set("VdoBitrate", ((int)m_BitrateBox.Value).ToString());
            Ares.CommonGUI.Preferences.SetBool("VdoPro", m_ProBox.Checked);
        }

        private void CopyText(string text)
        {
            if (text.Length == 0)
                return;
            try
            {
                Clipboard.SetText(text);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Apolo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
