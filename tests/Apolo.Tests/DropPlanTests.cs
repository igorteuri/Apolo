using System;
using System.IO;
using System.Linq;
using Ares.CommonGUI;
using Xunit;

namespace Apolo.Tests
{
    public class DropPlanTests : IDisposable
    {
        private readonly string m_Root = Path.Combine(Path.GetTempPath(), "apolo-drop-" + Guid.NewGuid().ToString("N"));

        public DropPlanTests()
        {
            Directory.CreateDirectory(m_Root);
        }

        public void Dispose()
        {
            try { Directory.Delete(m_Root, true); } catch (Exception) { }
        }

        private string Dir(string relative)
        {
            string path = Path.Combine(m_Root, relative.Replace((char)92, Path.DirectorySeparatorChar));
            Directory.CreateDirectory(path);
            return path;
        }

        private string File_(string relative)
        {
            string path = Path.Combine(m_Root, relative.Replace((char)92, Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "x");
            return path;
        }

        // Floresta (1 loose song) / Dia (2) / Noite (1)
        private string MakeForest()
        {
            File_("Floresta\\vento.mp3");
            File_("Floresta\\Dia\\passaros.mp3");
            File_("Floresta\\Dia\\riacho.ogg");
            File_("Floresta\\Noite\\coruja.wav");
            File_("Floresta\\Noite\\notas.txt");
            return Path.Combine(m_Root, "Floresta");
        }

        [Fact]
        public void OneAudioFile_IsOneFile()
        {
            string f = File_("a.mp3");
            DropScan scan = DropScan.Read(new[] { f });
            Assert.Equal(DropShape.OneFile, scan.Shape);
            Assert.Equal(1, scan.FileCount);
        }

        [Fact]
        public void SeveralAudioFiles_AreFiles_AndOtherFilesAreIgnored()
        {
            DropScan scan = DropScan.Read(new[] { File_("a.mp3"), File_("b.wav"), File_("c.txt") });
            Assert.Equal(DropShape.Files, scan.Shape);
            Assert.Equal(2, scan.FileCount);
        }

        [Fact]
        public void FolderWithOnlyAudio_IsFolder()
        {
            File_("Taverna\\1.mp3");
            File_("Taverna\\2.mp3");
            DropScan scan = DropScan.Read(new[] { Path.Combine(m_Root, "Taverna") });
            Assert.Equal(DropShape.Folder, scan.Shape);
            Assert.False(scan.HasSubfolders);
            Assert.Equal(2, scan.FileCount);
        }

        [Fact]
        public void FolderWithSubfolders_IsNested_AndEmptySubfoldersDoNotCount()
        {
            string forest = MakeForest();
            Dir("Floresta\\Vazia");
            DropScan scan = DropScan.Read(new[] { forest });
            Assert.Equal(DropShape.NestedFolder, scan.Shape);
            Assert.True(scan.HasSubfolders);
            Assert.Equal(4, scan.FileCount);
            Assert.Equal(2, scan.Folders[0].Subfolders.Count);
            Assert.Equal(3, scan.AudioFolderCount);
        }

        [Fact]
        public void FolderPlusFile_IsSeveral()
        {
            string forest = MakeForest();
            DropScan scan = DropScan.Read(new[] { forest, File_("solta.mp3") });
            Assert.Equal(DropShape.Several, scan.Shape);
        }

        [Fact]
        public void FolderWithoutAudio_IsNothing()
        {
            File_("Docs\\leia.txt");
            DropScan scan = DropScan.Read(new[] { Path.Combine(m_Root, "Docs") });
            Assert.Equal(DropShape.None, scan.Shape);
            Assert.True(scan.IsEmpty);
            Assert.Empty(scan.AvailableLayouts(true));
        }

        [Fact]
        public void Layouts_FlatFolder_OnFolder_HereOrNewFolder()
        {
            File_("Taverna\\1.mp3");
            DropScan scan = DropScan.Read(new[] { Path.Combine(m_Root, "Taverna") });
            Assert.Equal(new[] { DropLayout.Here, DropLayout.Tree }, scan.AvailableLayouts(true).ToArray());
            Assert.Equal(DropLayout.Here, scan.DefaultLayout(true));
        }

        [Fact]
        public void Layouts_LooseFiles_OnFolder_OnlyHere()
        {
            DropScan scan = DropScan.Read(new[] { File_("a.mp3"), File_("b.mp3") });
            Assert.Equal(new[] { DropLayout.Here }, scan.AvailableLayouts(true).ToArray());
        }

        [Fact]
        public void Layouts_NestedFolder_AllWaysOnFolder_DefaultTree()
        {
            DropScan scan = DropScan.Read(new[] { MakeForest() });
            Assert.Equal(new[] { DropLayout.Here, DropLayout.Tree, DropLayout.Compact, DropLayout.Merge },
                scan.AvailableLayouts(true).ToArray());
            Assert.Equal(DropLayout.Tree, scan.DefaultLayout(true));
        }

        [Fact]
        public void Layouts_WithoutDestination_NeverHereOrMerge()
        {
            DropScan scan = DropScan.Read(new[] { MakeForest() });
            Assert.Equal(new[] { DropLayout.Tree, DropLayout.Compact }, scan.AvailableLayouts(false).ToArray());
            Assert.Equal(DropLayout.Tree, scan.DefaultLayout(false));
            DropScan loose = DropScan.Read(new[] { File_("a.mp3") });
            Assert.Equal(new[] { DropLayout.Tree }, loose.AvailableLayouts(false).ToArray());
        }

        [Fact]
        public void Tree_ParentFolderGetsSubfolders()
        {
            DropScan scan = DropScan.Read(new[] { MakeForest() });
            DropPlan plan = DropPlan.Build(scan, DropLayout.Tree, null, new string[0], "Músicas soltas");
            Assert.Equal(new[] { "Floresta", "Dia", "Noite" }, plan.Folders.Select(f => f.Title).ToArray());
            Assert.Null(plan.Folders[0].Parent);
            Assert.Equal("Floresta", plan.Folders[1].Parent);
            Assert.Equal("Floresta", plan.Folders[2].Parent);
            Assert.Equal(1, plan.Folders[0].Groups.Single().Files.Count);
            Assert.Equal(2, plan.Folders[1].Groups.Single().Files.Count);
            Assert.All(plan.Folders, f => Assert.False(f.Existing));
        }

        [Fact]
        public void Tree_OnDestination_NewFoldersGoInsideIt()
        {
            DropScan scan = DropScan.Read(new[] { MakeForest() });
            DropPlan plan = DropPlan.Build(scan, DropLayout.Tree, "Ambientes", new[] { "Ambientes" }, "Músicas soltas");
            Assert.Equal("Ambientes", plan.Folders[0].Parent);
            Assert.Equal("Floresta", plan.Folders[1].Parent);
        }

        [Fact]
        public void Tree_TakenSubfolderName_GetsParentPrefix_ThenNumber()
        {
            DropScan scan = DropScan.Read(new[] { MakeForest() });
            DropPlan plan = DropPlan.Build(scan, DropLayout.Tree, null, new[] { "Dia", "Noite", "Floresta - Noite", "Floresta - Noite (2)" }, "x");
            Assert.Equal("Floresta - Dia", plan.Folders[1].Title);
            Assert.Equal("Floresta - Noite (3)", plan.Folders[2].Title);
            Assert.Equal("Floresta", plan.Folders[2].Parent);
        }

        [Fact]
        public void Compact_SubfoldersBecomeGroupsOfTheParent()
        {
            DropScan scan = DropScan.Read(new[] { MakeForest() });
            DropPlan plan = DropPlan.Build(scan, DropLayout.Compact, null, new string[0], "x");
            PlannedFolder only = Assert.Single(plan.Folders);
            Assert.Equal("Floresta", only.Title);
            Assert.Equal(new[] { "Floresta", "Dia", "Noite" }, only.Groups.Select(g => g.Name).ToArray());
        }

        [Fact]
        public void Here_PutsEveryAudioFolderInTheDestination_WithParentPrefix()
        {
            DropScan scan = DropScan.Read(new[] { MakeForest() });
            DropPlan plan = DropPlan.Build(scan, DropLayout.Here, "Ambientes", new[] { "Ambientes" }, "x");
            PlannedFolder target = Assert.Single(plan.Folders);
            Assert.True(target.Existing);
            Assert.Equal("Ambientes", target.Title);
            Assert.Equal(new[] { "Floresta", "Floresta - Dia", "Floresta - Noite" }, target.Groups.Select(g => g.Name).ToArray());
        }

        [Fact]
        public void Merge_OneGroupPerDroppedFolder_WithAllTheMusicBelow()
        {
            DropScan scan = DropScan.Read(new[] { MakeForest() });
            DropPlan plan = DropPlan.Build(scan, DropLayout.Merge, "Ambientes", new[] { "Ambientes" }, "x");
            DroppedGroup group = Assert.Single(Assert.Single(plan.Folders).Groups);
            Assert.Equal("Floresta", group.Name);
            Assert.Equal(4, group.Files.Count);
        }

        [Fact]
        public void LooseFiles_WithoutDestination_GoToTheLooseFolder_ReusedWhenItExists()
        {
            DropScan scan = DropScan.Read(new[] { File_("a.mp3"), File_("b.mp3") });
            DropPlan fresh = DropPlan.Build(scan, DropLayout.Tree, null, new string[0], "Músicas soltas");
            PlannedFolder created = Assert.Single(fresh.Folders);
            Assert.Equal("Músicas soltas", created.Title);
            Assert.False(created.Existing);
            Assert.True(created.Groups.Single().IsLoose);

            DropPlan again = DropPlan.Build(scan, DropLayout.Tree, null, new[] { "Músicas soltas" }, "Músicas soltas");
            Assert.True(Assert.Single(again.Folders).Existing);
        }

        [Fact]
        public void UnknownDestination_FallsBackToNewFolders()
        {
            DropScan scan = DropScan.Read(new[] { MakeForest() });
            DropPlan plan = DropPlan.Build(scan, DropLayout.Here, "NaoExiste", new[] { "Outra" }, "x");
            Assert.Equal("Floresta", plan.Folders[0].Title);
            Assert.False(plan.Folders[0].Existing);
        }
    }
}
