using System;
using System.Collections.Generic;
using System.Linq;
using Ares.AppCore;
using Ares.Data;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;

namespace Apolo.Desktop
{
    /// <summary>
    /// The floating pad: the buttons of one folder in a small window which stays on top of the others (like a virtual Stream Deck).
    /// It does not take the focus away from the game, and closes after a click (unless it is pinned).
    /// </summary>
    public sealed class PadWindow : Window
    {
        private readonly AppHost m_Host;
        private readonly WrapPanel m_Buttons = new WrapPanel();
        private readonly ComboBox m_Folders = new ComboBox { MinWidth = 180 };
        private readonly CheckBox m_Pin = new CheckBox();
        private bool m_Filling;

        public PadWindow(AppHost host, string folder)
        {
            m_Host = host;
            Title = AppHost.T("Pad flutuante", "Floating pad");
            Width = 560;
            Height = 380;
            Topmost = true;
            ShowActivated = false;
            ShowInTaskbar = false;
            Opacity = OpacityPercent / 100.0;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Position = new PixelPoint(80, 80);

            m_Pin.Content = AppHost.T("Fixar", "Pin");
            m_Pin.IsChecked = !Ares.CommonGUI.Preferences.GetBool("PadCloseOnClick", true);
            m_Pin.IsCheckedChanged += (s, e) => Ares.CommonGUI.Preferences.SetBool("PadCloseOnClick", m_Pin.IsChecked != true);

            Slider opacity = new Slider { Minimum = 40, Maximum = 100, Value = OpacityPercent, Width = 110 };
            opacity.PropertyChanged += (s, e) =>
            {
                if (e.Property == Slider.ValueProperty)
                {
                    Opacity = opacity.Value / 100.0;
                    Ares.CommonGUI.Preferences.Set("PadOpacity", ((int)opacity.Value).ToString());
                }
            };

            StackPanel bar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 0, 0, 8) };
            bar.Children.Add(m_Folders);
            bar.Children.Add(m_Pin);
            bar.Children.Add(new TextBlock { Text = AppHost.T("Transparência", "Transparency"), VerticalAlignment = VerticalAlignment.Center, Classes = { "dim" } });
            bar.Children.Add(opacity);

            Grid grid = new Grid { Margin = new Thickness(10), RowDefinitions = new RowDefinitions("Auto,*") };
            grid.Children.Add(bar);
            ScrollViewer scroll = new ScrollViewer { Content = m_Buttons, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
            Grid.SetRow(scroll, 1);
            grid.Children.Add(scroll);
            Content = grid;

            FillFolders(folder);
            m_Folders.SelectionChanged += (s, e) =>
            {
                if (!m_Filling)
                    FillButtons();
            };
            FillButtons();
        }

        private static int OpacityPercent
        {
            get
            {
                int value;
                if (int.TryParse(Ares.CommonGUI.Preferences.Get("PadOpacity", "85"), out value))
                    return Math.Max(40, Math.Min(100, value));
                return 85;
            }
        }

        private void FillFolders(string wanted)
        {
            m_Filling = true;
            m_Folders.Items.Clear();
            if (m_Host.Project != null)
            {
                foreach (IMode mode in m_Host.Project.GetModes())
                    m_Folders.Items.Add(mode.Title);
            }
            int index = wanted != null ? m_Folders.Items.IndexOf(wanted) : -1;
            m_Folders.SelectedIndex = index >= 0 ? index : (m_Folders.Items.Count > 0 ? 0 : -1);
            m_Filling = false;
        }

        private void FillButtons()
        {
            m_Buttons.Children.Clear();
            string title = m_Folders.SelectedItem as string;
            IMode mode = title != null ? m_Host.ModeByTitle(title) : null;
            if (mode == null)
                return;
            foreach (IModeElement element in mode.GetElements())
            {
                if (!element.IsVisibleInPlayer)
                    continue;
                int id = element.Id;
                string spec = ButtonDecor.Get(m_Host.ProjectPath, mode.Title, element.Title);
                string icon = ButtonDecor.IconName(spec);
                string color = ButtonDecor.GetColor(m_Host.ProjectPath, mode.Title, element.Title);
                StackPanel face = new StackPanel { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center };
                if (icon != null)
                {
                    face.Children.Add(new Rectangle
                    {
                        Width = 30, Height = 30,
                        Fill = IconCache.Brush(color) ?? new SolidColorBrush(Avalonia.Media.Color.Parse("#ECEDF3")),
                        OpacityMask = IconCache.Mask(icon)
                    });
                }
                face.Children.Add(new TextBlock { Text = element.Title, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxLines = 2, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 110 });
                Button button = new Button { Width = 120, Height = 84, Margin = new Thickness(4), Content = face, HorizontalContentAlignment = HorizontalAlignment.Center };
                button.Click += (s, e) =>
                {
                    if (m_Host.Control != null)
                    {
                        m_Host.Control.SetMode(mode.Title);
                        m_Host.Control.SwitchElement(id);
                    }
                    if (m_Pin.IsChecked != true)
                        Close();
                };
                m_Buttons.Children.Add(button);
            }
        }
    }
}
