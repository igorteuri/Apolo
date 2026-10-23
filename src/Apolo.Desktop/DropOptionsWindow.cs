using System;
using System.Collections.Generic;
using System.Linq;
using Ares.CommonGUI;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Apolo.Desktop
{
    /// <summary>
    /// "What to do with what was dropped": it looks at the files and folders first (DropScan) and asks only what is open:
    /// music or sound effect, where it goes and one button per file or one playlist per folder.
    /// The same questions (and sentences) as the window of the Windows program.
    /// </summary>
    public sealed class DropOptionsWindow : Window
    {
        private static DropOptions s_Last = new DropOptions();

        private readonly DropScan m_Scan;
        private readonly string m_Destination;
        private readonly RadioButton m_Music = new RadioButton { GroupName = "kind" };
        private readonly RadioButton m_Sounds = new RadioButton { GroupName = "kind" };
        private readonly RadioButton m_Each = new RadioButton { GroupName = "struct" };
        private readonly RadioButton m_One = new RadioButton { GroupName = "struct" };
        private readonly List<KeyValuePair<RadioButton, DropLayout>> m_Layouts = new List<KeyValuePair<RadioButton, DropLayout>>();
        private readonly RadioButton m_Random = new RadioButton { GroupName = "list" };
        private readonly RadioButton m_Sequential = new RadioButton { GroupName = "list" };
        private readonly RadioButton m_Parallel = new RadioButton { GroupName = "list" };
        private readonly CheckBox m_Repeat = new CheckBox();
        private readonly CheckBox m_Cross = new CheckBox();
        private readonly NumericUpDown m_Volume = new NumericUpDown { Minimum = 0, Maximum = 100, Increment = 5, FormatString = "0", Width = 110 };
        private readonly StackPanel m_StructBox = new StackPanel { Spacing = 4 };
        private readonly StackPanel m_ListBox = new StackPanel { Spacing = 4 };

        public DropOptions Result { get; private set; }

        public DropOptionsWindow(DropScan scan, string destination)
        {
            m_Scan = scan;
            m_Destination = destination;
            Title = AppHost.T("Adicionar ao Apolo", "Add to Apolo");
            Width = 560;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            CanResize = false;

            StackPanel panel = new StackPanel { Margin = new Thickness(20), Spacing = 8 };
            panel.Children.Add(new TextBlock { Text = DropTexts.Summary(scan), TextWrapping = TextWrapping.Wrap });
            panel.Children.Add(new TextBlock { Text = DropTexts.DestinationText(destination), Classes = { "dim" }, TextWrapping = TextWrapping.Wrap });

            panel.Children.Add(Heading(AppHost.T("O que são esses arquivos?", "What are these files?")));
            m_Music.Content = AppHost.T("Música — toca em segundo plano", "Music — plays in the background");
            m_Sounds.Content = AppHost.T("Efeito sonoro — toca junto com a música", "Sound effect — plays over the music");
            m_Music.IsChecked = !s_Last.Sounds;
            m_Sounds.IsChecked = s_Last.Sounds;
            panel.Children.Add(m_Music);
            panel.Children.Add(m_Sounds);

            // where to put it: only when there is more than one sensible answer
            List<DropLayout> layouts = scan.AvailableLayouts(destination != null);
            if (layouts.Count > 1)
            {
                panel.Children.Add(Heading(AppHost.T("Onde colocar", "Where to put it")));
                DropLayout wanted = scan.DefaultLayout(destination != null);
                foreach (DropLayout option in layouts)
                {
                    string[] parts = DropTexts.LayoutText(scan, option, destination).Split('\u0001');
                    RadioButton radio = new RadioButton { GroupName = "layout", IsChecked = option == wanted };
                    StackPanel content = new StackPanel();
                    content.Children.Add(new TextBlock { Text = parts[0], TextWrapping = TextWrapping.Wrap });
                    if (parts.Length > 1)
                        content.Children.Add(new TextBlock { Text = parts[1], Classes = { "dim" }, FontSize = 12, TextWrapping = TextWrapping.Wrap, MaxWidth = 470 });
                    radio.Content = content;
                    radio.IsCheckedChanged += (s, e) => UpdateState();
                    m_Layouts.Add(new KeyValuePair<RadioButton, DropLayout>(radio, option));
                    panel.Children.Add(radio);
                }
            }

            bool loosePlaylist = scan.Shape == DropShape.Files;
            m_StructBox.Children.Add(Heading(AppHost.T("Como criar os botões", "How to make the buttons")));
            m_Each.Content = AppHost.T("Um botão para cada música", "One button for each file");
            m_One.Content = loosePlaylist
                ? AppHost.T("Um botão só com todas as músicas (playlist)", "One button with all the files (playlist)")
                : AppHost.T("Um botão só por pasta de músicas (playlist)", "One button per folder of music (playlist)");
            m_Each.IsChecked = !s_Last.OneElement;
            m_One.IsChecked = s_Last.OneElement;
            m_StructBox.Children.Add(m_Each);
            m_StructBox.Children.Add(m_One);
            panel.Children.Add(m_StructBox);

            m_ListBox.Children.Add(Heading(AppHost.T("Tipo de playlist", "Playlist kind")));
            m_Random.IsChecked = s_Last.Kind == PlaylistKind.Random;
            m_Sequential.IsChecked = s_Last.Kind == PlaylistKind.Sequential;
            m_Parallel.IsChecked = s_Last.Kind == PlaylistKind.Parallel;
            m_ListBox.Children.Add(m_Random);
            m_ListBox.Children.Add(m_Sequential);
            m_ListBox.Children.Add(m_Parallel);
            m_Repeat.Content = AppHost.T("Repetir sem parar", "Repeat forever");
            m_Repeat.IsChecked = s_Last.Repeat;
            m_ListBox.Children.Add(m_Repeat);
            panel.Children.Add(m_ListBox);

            m_Cross.Content = AppHost.T("Transição suave entre uma música e a próxima (crossfade)", "Smooth transition between tracks (crossfade)");
            m_Cross.IsChecked = s_Last.CrossFade;
            panel.Children.Add(m_Cross);

            StackPanel volume = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            volume.Children.Add(new TextBlock { Text = "Volume (%)", VerticalAlignment = VerticalAlignment.Center });
            m_Volume.Value = Math.Max(0, Math.Min(100, s_Last.Volume));
            volume.Children.Add(m_Volume);
            panel.Children.Add(volume);

            Button ok = new Button { Content = "OK", Padding = new Thickness(26, 6), IsDefault = true };
            Button cancel = new Button { Content = AppHost.T("Cancelar", "Cancel"), Padding = new Thickness(22, 6), IsCancel = true };
            ok.Click += (s, e) => Accept();
            cancel.Click += (s, e) => Close();
            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            panel.Children.Add(buttons);

            m_Music.IsCheckedChanged += (s, e) => UpdateState();
            m_Sounds.IsCheckedChanged += (s, e) => UpdateState();
            m_Each.IsCheckedChanged += (s, e) => UpdateState();
            m_One.IsCheckedChanged += (s, e) => UpdateState();
            Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, MaxHeight = 760 };
            UpdateState();
        }

        private static TextBlock Heading(string text)
        {
            return new TextBlock { Text = text, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 8, 0, 0) };
        }

        private DropLayout SelectedLayout()
        {
            foreach (KeyValuePair<RadioButton, DropLayout> choice in m_Layouts)
            {
                if (choice.Key.IsChecked == true)
                    return choice.Value;
            }
            return m_Scan.DefaultLayout(m_Destination != null);
        }

        private void UpdateState()
        {
            bool sounds = m_Sounds.IsChecked == true;
            bool merge = SelectedLayout() == DropLayout.Merge;
            // a single file has nothing to choose; "one button per dropped folder" is always a playlist
            m_StructBox.IsVisible = !(m_Scan.Shape == DropShape.OneFile) && !merge;
            bool one = (m_One.IsChecked == true && m_StructBox.IsVisible) || merge;
            m_ListBox.IsVisible = one;
            if (sounds)
            {
                m_Random.Content = AppHost.T("Sorteia um som a cada vez", "A random sound each time");
                m_Sequential.Content = AppHost.T("Um depois do outro, na ordem", "One after the other, in order");
                m_Parallel.Content = AppHost.T("Todos ao mesmo tempo", "All at the same time");
                m_Parallel.IsVisible = true;
                m_Repeat.IsVisible = false;
            }
            else
            {
                m_Random.Content = AppHost.T("Aleatória (as músicas tocam em ordem sorteada)", "Random (tracks play in random order)");
                m_Sequential.Content = AppHost.T("Em sequência (na ordem da pasta)", "In sequence (in the order of the folder)");
                m_Parallel.IsVisible = false;
                if (m_Parallel.IsChecked == true)
                    m_Random.IsChecked = true;
                m_Repeat.IsVisible = true;
            }
            m_Cross.IsVisible = !sounds;
        }

        private void Accept()
        {
            DropLayout layout = SelectedLayout();
            bool sounds = m_Sounds.IsChecked == true;
            DropOptions result = new DropOptions
            {
                Sounds = sounds,
                OneElement = m_One.IsChecked == true || layout == DropLayout.Merge,
                Kind = m_Parallel.IsChecked == true && sounds ? PlaylistKind.Parallel
                    : (m_Sequential.IsChecked == true ? PlaylistKind.Sequential : PlaylistKind.Random),
                Repeat = m_Repeat.IsChecked == true,
                Volume = (int)(m_Volume.Value ?? 100),
                CrossFade = m_Cross.IsChecked == true,
                Layout = layout
            };
            s_Last = new DropOptions
            {
                Sounds = result.Sounds,
                OneElement = m_One.IsChecked == true,
                Kind = result.Kind,
                Repeat = result.Repeat,
                Volume = result.Volume,
                CrossFade = result.CrossFade
            };
            Result = result;
            Close();
        }
    }
}
