using System;
using Ares.Ipc;
using Xunit;

namespace Apolo.Tests
{
    // The first copy of the program publishes its window and project in shared memory so that a second start finds it.
    // That must work on every .NET (BinaryFormatter, used before, is gone from .NET 9 on).
    public class InstanceInfoTests
    {
        [Fact]
        public void InfoWrittenByTheFirstCopy_IsReadByTheSecond()
        {
            if (!OperatingSystem.IsWindows())
                return;
            string name = "Apolo.Tests.Info." + Guid.NewGuid().ToString("N");
            using (SharedMemory first = new SharedMemory(name, new ApplicationInfo(new IntPtr(12345), "C:/Projetos/Mesa.apolo")))
            using (SharedMemory second = new SharedMemory(name))
            {
                second.Lock();
                try
                {
                    ApplicationInfo info = (ApplicationInfo)second.GetObject();
                    Assert.Equal(new IntPtr(12345), info.WindowHandle);
                    Assert.Equal("C:/Projetos/Mesa.apolo", info.LoadedProject);
                }
                finally
                {
                    second.Unlock();
                }
            }
        }

        [Fact]
        public void InfoWithoutAProject_KeepsTheNull()
        {
            if (!OperatingSystem.IsWindows())
                return;
            string name = "Apolo.Tests.Info." + Guid.NewGuid().ToString("N");
            using (SharedMemory first = new SharedMemory(name, new ApplicationInfo(IntPtr.Zero, null)))
            using (SharedMemory second = new SharedMemory(name))
            {
                second.Lock();
                try
                {
                    ApplicationInfo info = (ApplicationInfo)second.GetObject();
                    Assert.Null(info.LoadedProject);
                }
                finally
                {
                    second.Unlock();
                }
            }
        }
    }
}
