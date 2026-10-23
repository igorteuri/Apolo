using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;

namespace Apolo.Desktop
{
    /// <summary>The settings which matter to start: where the music and the sound effects are, and which sound card plays.</summary>
    public sealed class SettingsWindow : Window
    {
        private readonly AppHost m_Host;
        private readonly TextBox m_Music = new TextBox();
        private readonly TextBox m_Sounds = new TextBox();
        private readonly ComboBox m_Device = new ComboBox();
        private List<Ares.Playing.AudioInit.OutputDevice> m_Devices = new List<Ares.Playing.AudioInit.OutputDevice>();

        public SettingsWindow(AppHost host)
        {
            m_Host = host;
            Title = AppHost.T("Configurações", "Settings");
            Width = 640;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            CanResize = false;

            Ares.Settings.Settings settings = Ares.Settings.Settings.Instance;
            m_Music.Text = settings.MusicDirectory;
            m_Sounds.Text = settings.SoundDirectory;

            StackPanel panel = new StackPanel { Margin = new Thickness(20), Spacing = 8 };
            panel.Children.Add(Heading(AppHost.T("Pasta das músicas", "Music folder")));
            panel.Children.Add(Row(m_Music));
            panel.Children.Add(Note(AppHost.T("Onde ficam as músicas dos projetos (os pacotes baixados são abertos aqui).",
                "Where the music of the projects is (packs are unpacked here).")));
            panel.Children.Add(Heading(AppHost.T("Pasta dos efeitos sonoros", "Sound effects folder")));
            panel.Children.Add(Row(m_Sounds));
            panel.Children.Add(Heading(AppHost.T("Saída de áudio", "Audio output")));
            panel.Children.Add(m_Device);
            m_Device.HorizontalAlignment = HorizontalAlignment.Stretch;
            FillDevices();
            panel.Children.Add(Note(AppHost.T("\"Não ouvir pelo PC\" só envia o som para a live (Discord, OBS...).",
                "\"Don't listen on this PC\" only sends the sound to the stream (Discord, OBS...).")));

            Button ok = new Button { Content = AppHost.T("Salvar", "Save"), Padding = new Thickness(22, 6) };
            Button cancel = new Button { Content = AppHost.T("Cancelar", "Cancel"), Padding = new Thickness(22, 6) };
            ok.Click += (s, e) => { Apply(); Close(); };
            cancel.Click += (s, e) => Close();
            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            panel.Children.Add(buttons);
            Content = panel;
        }

        private static TextBlock Heading(string text)
        {
            return new TextBlock { Text = text, FontWeight = Avalonia.Media.FontWeight.SemiBold, Margin = new Thickness(0, 8, 0, 0) };
        }

        private static TextBlock Note(string text)
        {
            return new TextBlock { Text = text, Classes = { "dim" }, FontSize = 12, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        }

        private Control Row(TextBox box)
        {
            Grid grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            Button choose = new Button { Content = AppHost.T("Escolher...", "Choose..."), Margin = new Thickness(8, 0, 0, 0) };
            choose.Click += async (s, e) =>
            {
                IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { AllowMultiple = false });
                if (folders.Count > 0)
                    box.Text = folders[0].Path.LocalPath;
            };
            Grid.SetColumn(choose, 1);
            grid.Children.Add(box);
            grid.Children.Add(choose);
            return grid;
        }

        private void FillDevices()
        {
            try
            {
                m_Devices = Ares.Playing.AudioInit.GetDevices();
            }
            catch (Exception)
            {
                m_Devices = new List<Ares.Playing.AudioInit.OutputDevice>();
            }
            m_Device.ItemsSource = m_Devices.Select(d => d.Name).ToList();
            int current = Ares.Settings.Settings.Instance.OutputDeviceIndex;
            int at = m_Devices.FindIndex(d => d.Index == current);
            m_Device.SelectedIndex = at >= 0 ? at : 0;
        }

        private void Apply()
        {
            Ares.Settings.Settings settings = Ares.Settings.Settings.Instance;
            settings.MusicDirectory = (m_Music.Text ?? "").Trim();
            settings.SoundDirectory = (m_Sounds.Text ?? "").Trim();
            int chosen = m_Device.SelectedIndex;
            int device = chosen >= 0 && chosen < m_Devices.Count ? m_Devices[chosen].Index : -1;
            string error = m_Host.ApplySettings(device);
            if (error != null)
                new MessageWindow(AppHost.T("Saída de áudio", "Audio output"), error).Show();
        }
    }
}
