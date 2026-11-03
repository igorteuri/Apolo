using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace Apolo.Tests
{
    // Runs before any test: all the data of Apolo (prefs.ini, errors.log, icons ...) goes into a temporary folder,
    // never into the real %APPDATA%\Apolo of the person running the tests.
    internal static class TestDataFolder
    {
        public static readonly string Root = Path.Combine(Path.GetTempPath(), "apolo-tests-data-" + Guid.NewGuid().ToString("N"));

        [ModuleInitializer]
        internal static void Init()
        {
            Directory.CreateDirectory(Root);
            Environment.SetEnvironmentVariable("APOLO_DATA_DIR", Root);
            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                try { Directory.Delete(Root, true); } catch (Exception) { }
            };
        }
    }
}
