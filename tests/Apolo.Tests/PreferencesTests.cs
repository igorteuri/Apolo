using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Ares.CommonGUI;
using Xunit;

namespace Apolo.Tests
{
    // prefs.ini keeps the options of the user (list mode, repeat, volumes ...): it must survive a restart of the program.
    [Collection("Preferences")]
    public class PreferencesTests
    {
        private static string File_ => Path.Combine(Ares.Settings.AppFolders.Root, "prefs.ini");

        // as if Apolo was closed and opened again: the values are read from the file
        private static void Restart()
        {
            typeof(Preferences).GetField("s_Values", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, null);
        }

        [Fact]
        public void TheDataGoesToTheFolderOfTheTests_NotToTheRealOne()
        {
            Assert.StartsWith(TestDataFolder.Root, Ares.Settings.AppFolders.Root);
            string real = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Apolo");
            Assert.NotEqual(real, Ares.Settings.AppFolders.Root);
        }

        [Fact]
        public void Values_SurviveARestart_WithAccentsEqualSignsAndBooleans()
        {
            Preferences.Set("PrefTest.Pasta", "C:\\Músicas do Mestre\\Ação 🐉");
            Preferences.Set("PrefTest.Formula", "a=b=c");
            Preferences.SetBool("PrefTest.Lista", true);
            Preferences.SetBool("PrefTest.Repetir", false);
            Restart();

            Assert.Equal("C:\\Músicas do Mestre\\Ação 🐉", Preferences.Get("PrefTest.Pasta", null));
            Assert.Equal("a=b=c", Preferences.Get("PrefTest.Formula", null));
            Assert.True(Preferences.GetBool("PrefTest.Lista", false));
            Assert.False(Preferences.GetBool("PrefTest.Repetir", true));
            Assert.Equal("padrão", Preferences.Get("PrefTest.NaoExiste", "padrão"));
            Assert.True(Preferences.GetBool("PrefTest.NaoExiste", true));
        }

        [Fact]
        public void ANewLineInAValue_DoesNotCreateOtherOptions()
        {
            Preferences.Set("PrefTest.Nome", "linha 1\r\nPrefTest.Invadida=1");
            Restart();
            Assert.Null(Preferences.Get("PrefTest.Invadida", null));
            Assert.StartsWith("linha 1", Preferences.Get("PrefTest.Nome", null));
        }

        [Fact]
        public void AFileHeldByAnotherProgram_StillGetsTheNewValue()
        {
            Preferences.Set("PrefTest.Segurado", "antes");
            // like the antivirus: the file is open (it can be read and written, not deleted or replaced)
            using (new FileStream(File_, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                Preferences.Set("PrefTest.Segurado", "depois");
            Restart();
            Assert.Equal("depois", Preferences.Get("PrefTest.Segurado", null));
        }

        [Fact]
        public void Saving_LeavesNoTemporaryFileBehind()
        {
            Preferences.Set("PrefTest.Atomico", "1");
            Assert.True(File.Exists(File_));
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(File_), "prefs.ini.*").Where(f => Path.GetFileName(f) != "prefs.ini"));
        }
    }
}
