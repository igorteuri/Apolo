using System;
using System.Collections.Generic;
using System.IO;
using Ares.Tags;
using Xunit;

namespace Apolo.Tests
{
    // The tags database (SQLite) must work the same on Windows, Linux and macOS.
    public class TagsDatabaseTests : IDisposable
    {
        private readonly string m_Dir = Path.Combine(Path.GetTempPath(), "apolo-tags-" + Guid.NewGuid().ToString("N"));

        public TagsDatabaseTests()
        {
            Directory.CreateDirectory(m_Dir);
        }

        public void Dispose()
        {
            try { Directory.Delete(m_Dir, true); } catch (Exception) { }
        }

        [Fact]
        public void CreatesTheDatabase_StoresTagsOfFiles_AndExportsAndImportsThem()
        {
            string file = Path.Combine(m_Dir, "a.db");
            ITagsDB db = TagsModule.GetNewTagsDB();
            db.FilesInterface.OpenOrCreateDatabase(file);
            Assert.True(File.Exists(file));

            ITagsDBRead read = db.ReadInterface;
            int language = db.TranslationsInterface.GetIdOfCurrentUILanguage();
            ITagsDBReadByLanguage byLanguage = db.GetReadInterfaceByLanguage(language);
            IList<CategoryForLanguage> categories = byLanguage.GetAllCategories();
            Assert.NotEmpty(categories);
            IList<TagForLanguage> tags = byLanguage.GetAllTags(categories[0].Id);
            Assert.NotEmpty(tags);

            int tagId = tags[0].Id;
            db.WriteInterface.AddFileTags(new List<string> { "Pasta/musica.mp3" }, new List<IList<int>> { new List<int> { tagId } });
            IList<string> found = read.GetAllFilesWithAnyTag(new HashSet<int> { tagId });
            Assert.Contains("Pasta/musica.mp3", found);

            string exported = Path.Combine(m_Dir, "export.jsv");
            db.FilesInterface.ExportDatabase(new List<string> { "Pasta/musica.mp3" }, exported);
            Assert.True(File.Exists(exported));

            string second = Path.Combine(m_Dir, "b.db");
            ITagsDB other = TagsModule.GetNewTagsDB();
            other.FilesInterface.OpenOrCreateDatabase(second);
            using (StringWriter log = new StringWriter())
                other.FilesInterface.ImportDatabase(exported, log);
            Assert.Contains("Pasta/musica.mp3", other.ReadInterface.GetAllFilesWithAnyTag());

            db.FilesInterface.CloseDatabase();
            other.FilesInterface.CloseDatabase();
        }
    }
}
