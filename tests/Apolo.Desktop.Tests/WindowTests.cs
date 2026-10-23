using System;
using System.IO;
using System.Linq;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Apolo.Desktop.Tests
{
    public class WindowTests
    {
        // Opens a project in the main window and, when APOLO_SHOT_DIR is set, saves a picture of it (to look at the layout).
        private static void Shot(MainWindow window, string name)
        {
            string dir = Environment.GetEnvironmentVariable("APOLO_SHOT_DIR");
            if (string.IsNullOrEmpty(dir))
                return;
            Directory.CreateDirectory(dir);
            var frame = window.CaptureRenderedFrame();
            frame.Save(Path.Combine(dir, name + ".png"));
        }

        [AvaloniaFact]
        public void TheWindow_ShowsTheWelcome_WhenNoProjectIsOpen()
        {
            AppHost host = new AppHost();
            host.Start(true);
            MainWindow window = new MainWindow(host, false);
            window.Show();
            Assert.True(window.Model.NoProject);
            Shot(window, "boas-vindas");
            window.Close();
            host.Dispose();
        }

        // looks at a big real project (only on the computer which has it): folders, buttons with pictures and colors
        [AvaloniaFact]
        public void ABigProject_ShowsFoldersAndButtons()
        {
            string project = Environment.GetEnvironmentVariable("APOLO_SAMPLE_PROJECT");
            if (string.IsNullOrEmpty(project) || !File.Exists(project))
                return;
            AppHost host = new AppHost();
            host.Start(true);
            MainWindow window = new MainWindow(host, false);
            window.Show();
            window.OpenProject(project);
            Assert.NotEmpty(window.Model.Folders);
            Shot(window, "projeto-aberto");
            foreach (string name in new[] { "Humores", "Clima", "Animais" })
            {
                Apolo.Desktop.FolderVM folder = Find(window.Model.Folders, name);
                if (folder == null)
                    continue;
                window.Model.SelectedFolder = folder;
                Shot(window, "pasta-" + name);
            }
            window.Close();
            host.Dispose();
        }

        private static Apolo.Desktop.FolderVM Find(System.Collections.Generic.IEnumerable<Apolo.Desktop.FolderVM> list, string title)
        {
            foreach (Apolo.Desktop.FolderVM f in list)
            {
                if (f.Title == title)
                    return f;
                Apolo.Desktop.FolderVM inner = Find(f.Children, title);
                if (inner != null)
                    return inner;
            }
            return null;
        }
    }
}
