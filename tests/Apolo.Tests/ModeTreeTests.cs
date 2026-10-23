using System;
using System.IO;
using System.Linq;
using Ares.Player;
using Xunit;

namespace Apolo.Tests
{
    // The nesting of the folders is kept beside the project ("[project].pastas.json"); the file is read the same way on every system.
    public class ModeTreeTests : IDisposable
    {
        private readonly string m_Dir = Path.Combine(Path.GetTempPath(), "apolo-tree-" + Guid.NewGuid().ToString("N"));

        public ModeTreeTests()
        {
            Directory.CreateDirectory(m_Dir);
        }

        public void Dispose()
        {
            try { Directory.Delete(m_Dir, true); } catch (Exception) { }
        }

        [Fact]
        public void SavedTree_IsReadBack_WithAccentsAndNesting()
        {
            string project = Path.Combine(m_Dir, "p.apolo");
            ModeTree tree = new ModeTree();
            ModeNode amb = new ModeNode { Title = "Ambiências", Parent = tree.Root, Collapsed = true };
            tree.Root.Children.Add(amb);
            amb.Children.Add(new ModeNode { Title = "Povoado", Parent = amb });
            tree.Root.Children.Add(new ModeNode { Title = "Humores", Parent = tree.Root });
            tree.Save(project);

            string json = File.ReadAllText(ModeTree.FileFor(project));
            Assert.Contains("Ambiências", json);   // not escaped to \u00EA

            ModeTree again = ModeTree.Load(project);
            Assert.Equal(new[] { "Ambiências", "Humores" }, again.Root.Children.Select(n => n.Title).ToArray());
            Assert.True(again.Root.Children[0].Collapsed);
            Assert.Equal("Povoado", again.Root.Children[0].Children[0].Title);
            Assert.Same(again.Root.Children[0], again.Root.Children[0].Children[0].Parent);
        }

        [Fact]
        public void FileFromTheWindowsProgram_IsRead()
        {
            string project = Path.Combine(m_Dir, "w.apolo");
            File.WriteAllText(ModeTree.FileFor(project), "{\"version\":1,\"nodes\":[{\"title\":\"A\",\"children\":[{\"title\":\"B\"}]},{\"title\":\"C\",\"collapsed\":true}]}");
            ModeTree tree = ModeTree.Load(project);
            Assert.Equal(2, tree.All().Count(n => n.Parent == tree.Root) );
            Assert.Equal("B", tree.Find("A").Children[0].Title);
            Assert.True(tree.Find("C").Collapsed);
        }

        [Fact]
        public void MissingOrBrokenFile_GivesAnEmptyTree()
        {
            Assert.Empty(ModeTree.Load(Path.Combine(m_Dir, "none.apolo")).Root.Children);
            string project = Path.Combine(m_Dir, "bad.apolo");
            File.WriteAllText(ModeTree.FileFor(project), "{ this is not json");
            Assert.Empty(ModeTree.Load(project).Root.Children);
        }
    }
}
