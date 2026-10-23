using System;
using Avalonia;
using Avalonia.Headless;
using Apolo.Desktop;

[assembly: AvaloniaTestApplication(typeof(Apolo.Desktop.Tests.TestApp))]

namespace Apolo.Desktop.Tests
{
    public class TestApp
    {
        public static AppBuilder BuildAvaloniaApp()
        {
            // the tests never touch the real settings of the person who runs them
            Environment.SetEnvironmentVariable("APOLO_DATA_DIR", System.IO.Path.Combine(System.IO.Path.GetTempPath(), "apolo-desktop-tests-" + Guid.NewGuid().ToString("N")));
            return AppBuilder.Configure<App>()
                .UseSkia()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
        }
    }
}
