using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Apolo.Desktop
{
    /// <summary>A small window with a title, a text and an OK button.</summary>
    public sealed class MessageWindow : Window
    {
        public MessageWindow(string title, string text)
        {
            Title = title;
            Width = 480;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            CanResize = false;
            Button ok = new Button { Content = "OK", HorizontalAlignment = HorizontalAlignment.Right, Padding = new Thickness(26, 6) };
            ok.Click += (s, e) => Close();
            StackPanel panel = new StackPanel { Margin = new Thickness(20), Spacing = 16 };
            panel.Children.Add(new TextBlock { Text = text, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
            panel.Children.Add(ok);
            Content = panel;
        }
    }
}
