using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Ares.AppCore;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Platform.Storage;

namespace Apolo.Desktop
{
    /// <summary>The words of the menus (Portuguese or English by the language of the system).</summary>
    public static class Texts
    {
        public static string Tracks { get { return AppHost.T("Ver faixas (F9)", "Show tracks (F9)"); } }
        public static string SwitchKind { get { return AppHost.T("Trocar música / efeito sonoro", "Switch music / sound effect"); } }
        public static string Rename { get { return AppHost.T("Renomear...", "Rename..."); } }
        public static string Look { get { return AppHost.T("Ícone e cor...", "Icon and color..."); } }
        public static string MoveBefore { get { return AppHost.T("Mover para antes", "Move earlier"); } }
        public static string MoveAfter { get { return AppHost.T("Mover para depois", "Move later"); } }
        public static string Delete { get { return AppHost.T("Excluir", "Delete"); } }
        public static string NewFolder { get { return AppHost.T("Nova pasta...", "New folder..."); } }
        public static string NewSubfolder { get { return AppHost.T("Nova subpasta...", "New subfolder..."); } }
        public static string AddHere { get { return AppHost.T("Adicionar músicas aqui...", "Add music here..."); } }
    }

    /// <summary>Asks for a line of text.</summary>
    public sealed class PromptWindow : Window
    {
        private readonly TextBox m_Box = new TextBox();

        public string Result { get; private set; }

        public PromptWindow(string title, string label, string initial)
        {
            Title = title;
            Width = 440;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            CanResize = false;
            m_Box.Text = initial ?? "";
            StackPanel panel = new StackPanel { Margin = new Thickness(20), Spacing = 8 };
            panel.Children.Add(new TextBlock { Text = label });
            panel.Children.Add(m_Box);
            Button ok = new Button { Content = "OK", Padding = new Thickness(26, 6), IsDefault = true };
            Button cancel = new Button { Content = AppHost.T("Cancelar", "Cancel"), Padding = new Thickness(22, 6), IsCancel = true };
            ok.Click += (s, e) => { Result = (m_Box.Text ?? "").Trim(); Close(); };
            cancel.Click += (s, e) => Close();
            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            panel.Children.Add(buttons);
            Content = panel;
            Opened += (s, e) => { m_Box.Focus(); m_Box.SelectAll(); };
        }

        /// <summary>The text typed, or null if the person cancelled or left it empty.</summary>
        public static async Task<string> Ask(Window owner, string title, string label, string initial)
        {
            PromptWindow window = new PromptWindow(title, label, initial);
            await window.ShowDialog(owner);
            return string.IsNullOrEmpty(window.Result) ? null : window.Result;
        }
    }

    /// <summary>A question with yes and no.</summary>
    public sealed class ConfirmWindow : Window
    {
        public bool Yes { get; private set; }

        public ConfirmWindow(string title, string text)
        {
            Title = title;
            Width = 460;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            CanResize = false;
            StackPanel panel = new StackPanel { Margin = new Thickness(20), Spacing = 12 };
            panel.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap });
            Button yes = new Button { Content = AppHost.T("Sim", "Yes"), Padding = new Thickness(26, 6), IsDefault = true };
            Button no = new Button { Content = AppHost.T("Não", "No"), Padding = new Thickness(26, 6), IsCancel = true };
            yes.Click += (s, e) => { Yes = true; Close(); };
            no.Click += (s, e) => Close();
            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
            buttons.Children.Add(yes);
            buttons.Children.Add(no);
            panel.Children.Add(buttons);
            Content = panel;
        }

        public static async Task<bool> Ask(Window owner, string title, string text)
        {
            ConfirmWindow window = new ConfirmWindow(title, text);
            await window.ShowDialog(owner);
            return window.Yes;
        }
    }

    /// <summary>Chooses the icon (or a picture) and the color of a button or of a folder.</summary>
    public sealed class AppearanceWindow : Window
    {
        private static readonly string[] Colors =
        {
            "#2E8B57", "#3FA34D", "#D4A72C", "#E08A1E", "#C0392B", "#D6336C", "#8E5BD1", "#2D7DD2",
            "#2B9FD9", "#1ABC9C", "#7A8B99", "#6B7280", "#8D6E63", "#ECEDF3"
        };

        private string m_Icon;
        private string m_Color;
        private readonly Panel m_IconPanel = new WrapPanel();
        private readonly TextBox m_Search = new TextBox { Watermark = AppHost.T("Procurar ícone...", "Search icon..."), Margin = new Thickness(0, 6, 0, 6) };
        private readonly TextBlock m_Chosen = new TextBlock { Classes = { "dim" } };

        public bool Applied { get; private set; }
        public bool IconChanged { get; private set; }
        public bool ColorChanged { get; private set; }
        /// <summary>The new setting of the icon: "lucide:name", "lib:file" or null for none.</summary>
        public string Icon { get { return m_Icon; } }
        /// <summary>The new color "#RRGGBB" or null for none.</summary>
        public string Color { get { return m_Color; } }

        public AppearanceWindow(string title, string currentIcon, string currentColor)
        {
            Title = title;
            Width = 620;
            Height = 640;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            m_Icon = currentIcon;
            m_Color = currentColor;

            StackPanel colors = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            foreach (string hex in Colors)
            {
                string value = hex;
                Button swatch = new Button
                {
                    Width = 30, Height = 30, Padding = new Thickness(0),
                    Background = IconCache.Brush(hex), CornerRadius = new CornerRadius(15)
                };
                swatch.Click += (s, e) => { m_Color = value; ColorChanged = true; UpdateChosen(); };
                colors.Children.Add(swatch);
            }
            Button noColor = new Button { Content = AppHost.T("Sem cor", "No color"), Padding = new Thickness(10, 4) };
            noColor.Click += (s, e) => { m_Color = null; ColorChanged = true; UpdateChosen(); };
            colors.Children.Add(noColor);

            Button picture = new Button { Content = AppHost.T("Escolher imagem...", "Choose a picture..."), Padding = new Thickness(12, 5) };
            picture.Click += async (s, e) =>
            {
                IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = AppHost.T("Escolher imagem", "Choose a picture"),
                    AllowMultiple = false,
                    FileTypeFilter = new[] { new FilePickerFileType(AppHost.T("Imagens", "Pictures")) { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.webp" } } }
                });
                if (files.Count > 0)
                {
                    m_Icon = ButtonDecor.ImportPicture(files[0].Path.LocalPath);
                    IconChanged = true;
                    UpdateChosen();
                }
            };
            Button noIcon = new Button { Content = AppHost.T("Sem ícone", "No icon"), Padding = new Thickness(12, 5) };
            noIcon.Click += (s, e) => { m_Icon = null; IconChanged = true; UpdateChosen(); };
            StackPanel iconRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 10, 0, 0) };
            iconRow.Children.Add(picture);
            iconRow.Children.Add(noIcon);

            FillIcons("");
            m_Search.TextChanged += (s, e) => FillIcons(m_Search.Text ?? "");

            Button ok = new Button { Content = AppHost.T("Aplicar", "Apply"), Padding = new Thickness(24, 6), IsDefault = true };
            Button cancel = new Button { Content = AppHost.T("Cancelar", "Cancel"), Padding = new Thickness(22, 6), IsCancel = true };
            ok.Click += (s, e) => { Applied = true; Close(); };
            cancel.Click += (s, e) => Close();
            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);

            Grid grid = new Grid { Margin = new Thickness(18), RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,*,Auto") };
            TextBlock colorLabel = new TextBlock { Text = AppHost.T("Cor", "Color"), FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 0, 0, 6) };
            grid.Children.Add(colorLabel);
            Grid.SetRow(colors, 1);
            grid.Children.Add(colors);
            Grid.SetRow(iconRow, 2);
            grid.Children.Add(iconRow);
            Grid.SetRow(m_Search, 3);
            grid.Children.Add(m_Search);
            ScrollViewer scroll = new ScrollViewer { Content = m_IconPanel };
            Grid.SetRow(scroll, 4);
            grid.Children.Add(scroll);
            StackPanel bottom = new StackPanel();
            bottom.Children.Add(m_Chosen);
            bottom.Children.Add(buttons);
            Grid.SetRow(bottom, 5);
            grid.Children.Add(bottom);
            Content = grid;
            UpdateChosen();
        }

        private void UpdateChosen()
        {
            string icon = m_Icon == null ? AppHost.T("nenhum", "none") : (ButtonDecor.IconName(m_Icon) ?? AppHost.T("imagem", "picture"));
            m_Chosen.Text = AppHost.T("Ícone: ", "Icon: ") + icon + "   |   " + AppHost.T("Cor: ", "Color: ") + (m_Color ?? AppHost.T("nenhuma", "none"));
        }

        private void FillIcons(string filter)
        {
            m_IconPanel.Children.Clear();
            IEnumerable<Uri> assets = AssetLoader.GetAssets(new Uri("avares://Apolo.Desktop/Icons/"), null);
            List<string> names = assets.Select(u => Path.GetFileNameWithoutExtension(u.AbsolutePath)).Distinct().OrderBy(n => n).ToList();
            foreach (string name in names)
            {
                if (filter.Length > 0 && name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                string value = name;
                Button button = new Button { Width = 48, Height = 48, Margin = new Thickness(2), Padding = new Thickness(0) };
                button.Content = new Avalonia.Controls.Shapes.Rectangle
                {
                    Width = 26, Height = 26, Fill = new SolidColorBrush(Avalonia.Media.Color.Parse("#ECEDF3")), OpacityMask = IconCache.Mask(name)
                };
                ToolTip.SetTip(button, name);
                button.Click += (s, e) => { m_Icon = "lucide:" + value; IconChanged = true; UpdateChosen(); };
                m_IconPanel.Children.Add(button);
            }
        }
    }
}
