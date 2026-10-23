using System;
using System.IO;
using System.Linq;
using Ares.AppCore;
using Ares.CommonGUI;
using Ares.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Apolo.Desktop.Tests
{
    public class EditTests : IDisposable
    {
        private readonly string m_Dir = Path.Combine(Path.GetTempPath(), "apolo-edit-" + Guid.NewGuid().ToString("N"));

        public EditTests()
        {
            Directory.CreateDirectory(m_Dir);
        }

        public void Dispose()
        {
            try { Directory.Delete(m_Dir, true); } catch (Exception) { }
        }

        private string Touch(string name)
        {
            string path = Path.Combine(m_Dir, "musicas", name);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
            return path;
        }

        private AppHost NewProjectWithTwoButtons()
        {
            AppHost host = new AppHost();
            host.Start(true);
            host.CreateProject(Path.Combine(m_Dir, "p.apolo"), "P");
            host.NewFolder("Pasta A", null);
            DropScan scan = DropScan.Read(new[] { Touch("um.mp3"), Touch("dois.mp3") });
            host.ImportDropped(scan, new DropOptions { Layout = DropLayout.Here, OneElement = false }, "Pasta A");
            return host;
        }

        [AvaloniaFact]
        public void RenameMoveAndDeleteButtons_AreSaved_AndTheLookFollows()
        {
            AppHost host = NewProjectWithTwoButtons();
            IMode mode = host.ModeByTitle("Pasta A");
            Assert.Equal(new[] { "dois", "um" }, mode.GetElements().Select(e => e.Title).OrderBy(x => x).ToArray());
            int id = mode.GetElements().First(e => e.Title == "um").Id;

            host.SetButtonIcon(id, "lucide:music");
            host.SetButtonColor(id, "#8E5BD1");
            host.RenameButton(id, "primeiro");
            Assert.Equal("lucide:music", ButtonDecor.Get(host.ProjectPath, "Pasta A", "primeiro"));
            Assert.Equal("#8E5BD1", ButtonDecor.GetColor(host.ProjectPath, "Pasta A", "primeiro"));
            Assert.Null(ButtonDecor.Get(host.ProjectPath, "Pasta A", "um"));

            host.MoveButton(id, 1);
            host.MoveButton(id, -1);
            host.MoveButton(id, -1);
            Assert.Equal("primeiro", mode.GetElements()[0].Title);

            host.DeleteButton(id);
            Assert.Single(mode.GetElements());
            Assert.Null(ButtonDecor.Get(host.ProjectPath, "Pasta A", "primeiro"));

            // all in the file
            IProject again = DataModule.ProjectManager.LoadProject(Path.Combine(m_Dir, "p.apolo"));
            Assert.Single(again.GetModes().First(m => m.Title == "Pasta A").GetElements());
            DataModule.ProjectManager.UnloadProject(again);
            host.Dispose();
        }

        [AvaloniaFact]
        public void FoldersCanBeCreatedNestedRenamedAndDeleted()
        {
            AppHost host = NewProjectWithTwoButtons();
            host.NewFolder("Filha", "Pasta A");
            Assert.Equal("Pasta A", host.Tree.Find("Filha").Parent.Title);
            host.SetFolderColor("Pasta A", "#C0392B");

            Assert.Equal("Mae", host.RenameFolder("Pasta A", "Mae"));
            Assert.NotNull(host.ModeByTitle("Mae"));
            Assert.Null(host.ModeByTitle("Pasta A"));
            Assert.Equal("#C0392B", ButtonDecor.GetColor(host.ProjectPath, "Mae", ""));
            Assert.Equal("Mae", host.Tree.Find("Filha").Parent.Title);

            host.DeleteFolder("Mae");
            Assert.Null(host.ModeByTitle("Mae"));
            // the subfolder stays, one level up
            Assert.NotNull(host.ModeByTitle("Filha"));
            Assert.Equal(host.Tree.Root, host.Tree.Find("Filha").Parent);
            Assert.Null(ButtonDecor.GetColor(host.ProjectPath, "Mae", ""));
            host.Dispose();
        }

        [AvaloniaFact]
        public void TheLookWindow_ShowsIconsAndColors()
        {
            AppearanceWindow window = new AppearanceWindow("Teste", "lucide:swords", "#C0392B");
            window.Show();
            string dir = Environment.GetEnvironmentVariable("APOLO_SHOT_DIR");
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
                window.CaptureRenderedFrame().Save(Path.Combine(dir, "icone-e-cor.png"));
            }
            Assert.False(window.Applied);
            window.Close();
        }
    }
}
