using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace Apolo.Desktop
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            IClassicDesktopStyleApplicationLifetime desktop = ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            if (desktop != null)
            {
                AppHost host = new AppHost();
                try
                {
                    host.Start(false);
                }
                catch (Exception ex)
                {
                    // without any sound output the program cannot play: say why and leave
                    desktop.MainWindow = new MessageWindow(AppHost.T("Não foi possível iniciar o áudio", "The audio could not be started"), ex.Message);
                    base.OnFrameworkInitializationCompleted();
                    return;
                }
                MainWindow window = new MainWindow(host);
                desktop.MainWindow = window;
                desktop.Exit += (s, e) => host.Dispose();
            }
            base.OnFrameworkInitializationCompleted();
        }
    }
}
