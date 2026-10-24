using System;
using System.IO;
using Ares.Data;
using Xunit;

namespace Apolo.Tests
{
    // the data module keeps its elements in static singletons, so these tests must not run side by side
    [Collection("DataModule")]
    public class ProjectFileTests : IDisposable
    {
        private readonly string m_Dir;

        public ProjectFileTests()
        {
            m_Dir = Path.Combine(Path.GetTempPath(), "apolo-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_Dir);
        }

        public void Dispose()
        {
            try { Directory.Delete(m_Dir, true); } catch (IOException) { }
        }

        private static IModeElement AddButton(IMode mode, string title)
        {
            IModeElement element = DataModule.ElementFactory.CreateModeElement(title, DataModule.ElementFactory.CreateSequentialContainer(title));
            mode.AddElement(element);
            return element;
        }

        private IProject SaveAndReload(IProject project, string name)
        {
            string file = Path.Combine(m_Dir, name);
            DataModule.ProjectManager.SaveProject(project, file);
            DataModule.ProjectManager.UnloadProject(project);
            return DataModule.ProjectManager.LoadProject(file);
        }

        [Fact]
        public void SavedProject_KeepsTitleAndModes()
        {
            IProject project = DataModule.ProjectManager.CreateProject("Campanha");
            project.AddMode("Taverna");
            project.AddMode("Masmorra");

            IProject loaded = SaveAndReload(project, "a.apolo");

            Assert.Equal("Campanha", loaded.Title);
            Assert.Equal(2, loaded.GetModes().Count);
            Assert.Equal("Taverna", loaded.GetModes()[0].Title);
            Assert.Equal("Masmorra", loaded.GetModes()[1].Title);
            DataModule.ProjectManager.UnloadProject(loaded);
        }

        [Fact]
        public void SavedProject_KeepsOrderOfButtons()
        {
            IProject project = DataModule.ProjectManager.CreateProject("Ordem");
            IMode mode = project.AddMode("Modo");
            AddButton(mode, "Primeiro");
            AddButton(mode, "Segundo");
            AddButton(mode, "Terceiro");

            IProject loaded = SaveAndReload(project, "b.apolo");

            var elements = loaded.GetModes()[0].GetElements();
            Assert.Equal(3, elements.Count);
            Assert.Equal("Primeiro", elements[0].Title);
            Assert.Equal("Segundo", elements[1].Title);
            Assert.Equal("Terceiro", elements[2].Title);
            DataModule.ProjectManager.UnloadProject(loaded);
        }

        [Fact]
        public void MovedAndRemovedButtons_AreSavedInTheNewOrder()
        {
            IProject project = DataModule.ProjectManager.CreateProject("Mover");
            IMode mode = project.AddMode("Modo");
            IModeElement a = AddButton(mode, "A");
            IModeElement b = AddButton(mode, "B");
            IModeElement c = AddButton(mode, "C");

            mode.RemoveElement(a);
            mode.InsertElement(1, a);   // B, A, C
            mode.RemoveElement(c);      // B, A

            IProject loaded = SaveAndReload(project, "c.apolo");

            var elements = loaded.GetModes()[0].GetElements();
            Assert.Equal(2, elements.Count);
            Assert.Equal("B", elements[0].Title);
            Assert.Equal("A", elements[1].Title);
            DataModule.ProjectManager.UnloadProject(loaded);
        }

        [Fact]
        public void SaveClearsChangedFlagAndStoresFileName()
        {
            IProject project = DataModule.ProjectManager.CreateProject("Flag");
            string file = Path.Combine(m_Dir, "d.apolo");

            DataModule.ProjectManager.SaveProject(project, file);

            Assert.False(project.Changed);
            Assert.Equal(file, project.FileName);
            Assert.True(File.Exists(file));
            DataModule.ProjectManager.UnloadProject(project);
        }

        [Fact]
        public void SavingAgain_KeepsThePreviousVersionAsBak_AndLeavesNoTempFile()
        {
            string file = Path.Combine(m_Dir, "bak.apolo");
            IProject project = DataModule.ProjectManager.CreateProject("Primeira");
            DataModule.ProjectManager.SaveProject(project, file);
            string first = File.ReadAllText(file);

            project.AddMode("Nova pasta");
            DataModule.ProjectManager.SaveProject(project);

            Assert.True(File.Exists(file + ".bak"));
            Assert.Equal(first, File.ReadAllText(file + ".bak"));
            Assert.NotEqual(first, File.ReadAllText(file));
            Assert.False(File.Exists(file + ".tmp"));
            DataModule.ProjectManager.UnloadProject(project);
        }

        [Fact]
        public void DamagedFile_OpensTheBackup()
        {
            string file = Path.Combine(m_Dir, "dano.apolo");
            IProject project = DataModule.ProjectManager.CreateProject("Boa");
            DataModule.ProjectManager.SaveProject(project, file);
            project.AddMode("Pasta");
            DataModule.ProjectManager.SaveProject(project); // the first version is now the .bak
            DataModule.ProjectManager.UnloadProject(project);
            File.WriteAllText(file, "<?xml version=\"1.0\"?><Project><oops");

            IProject recovered = DataModule.ProjectManager.LoadProject(file);
            Assert.True(Ares.Data.ProjectRecovery.UsedBackup);
            Assert.Equal("Boa", recovered.Title);
            Assert.True(File.Exists(file + ".corrompido"));
            DataModule.ProjectManager.UnloadProject(recovered);
        }

        [Fact]
        public void SaveWithoutFileName_Throws()
        {
            IProject project = DataModule.ProjectManager.CreateProject("Sem nome");
            Assert.Throws<ArgumentException>(() => DataModule.ProjectManager.SaveProject(project));
            DataModule.ProjectManager.UnloadProject(project);
        }

        [Fact]
        public void FileWithoutProject_IsRejected()
        {
            string file = Path.Combine(m_Dir, "e.apolo");
            File.WriteAllText(file, "<?xml version=\"1.0\" encoding=\"utf-8\"?><ExportedElements />");

            Assert.ThrowsAny<Exception>(() => DataModule.ProjectManager.LoadProject(file));
        }
    }
}
