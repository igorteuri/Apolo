// Stand-in for the old "MouseKeyboardActivityMonitor" library (keyboard hooks), for .NET 8.
// Only what Player.cs uses: KeyboardHookListener with Enabled / KeyDown / Replace / Dispose and the hookers
// AppHooker (keys pressed while Apolo has the focus) and GlobalHooker (keys pressed in any program).
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MouseKeyboardActivityMonitor.WinApi
{
    public delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    public abstract class Hooker
    {
        internal abstract IntPtr Install(HookProc proc);
        internal abstract bool IsLowLevel { get; }

        [DllImport("user32.dll", SetLastError = true)]
        protected static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("kernel32.dll")]
        protected static extern uint GetCurrentThreadId();
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        protected static extern IntPtr GetModuleHandle(string name);
    }

    public class AppHooker : Hooker
    {
        internal override bool IsLowLevel { get { return false; } }
        internal override IntPtr Install(HookProc proc)
        {
            return SetWindowsHookEx(2 /* WH_KEYBOARD */, proc, IntPtr.Zero, GetCurrentThreadId());
        }
    }

    public class GlobalHooker : Hooker
    {
        internal override bool IsLowLevel { get { return true; } }
        internal override IntPtr Install(HookProc proc)
        {
            using (Process p = Process.GetCurrentProcess())
            using (ProcessModule m = p.MainModule)
                return SetWindowsHookEx(13 /* WH_KEYBOARD_LL */, proc, GetModuleHandle(m.ModuleName), 0);
        }
    }
}

namespace MouseKeyboardActivityMonitor
{
    using MouseKeyboardActivityMonitor.WinApi;

    public class KeyboardHookListener : IDisposable
    {
        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        private Hooker m_Hooker;
        private IntPtr m_Handle = IntPtr.Zero;
        private bool m_Enabled;
        private readonly HookProc m_Proc; // kept in a field so that the garbage collector does not remove it

        public event KeyEventHandler KeyDown;
        public event KeyEventHandler KeyUp;

        public KeyboardHookListener(Hooker hooker)
        {
            m_Hooker = hooker;
            m_Proc = Callback;
        }

        public bool Enabled
        {
            get { return m_Enabled; }
            set
            {
                if (value == m_Enabled)
                    return;
                m_Enabled = value;
                if (value)
                    m_Handle = m_Hooker.Install(m_Proc);
                else
                    Unhook();
            }
        }

        public void Replace(Hooker hooker)
        {
            bool wasEnabled = m_Enabled;
            Enabled = false;
            m_Hooker = hooker;
            Enabled = wasEnabled;
        }

        private void Unhook()
        {
            if (m_Handle != IntPtr.Zero)
            {
                UnhookWindowsHookEx(m_Handle);
                m_Handle = IntPtr.Zero;
            }
        }

        private IntPtr Callback(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code >= 0)
            {
                int vk;
                bool down;
                if (m_Hooker.IsLowLevel)
                {
                    vk = Marshal.ReadInt32(lParam); // KBDLLHOOKSTRUCT.vkCode
                    int msg = wParam.ToInt32();
                    down = msg == 0x100 || msg == 0x104;
                }
                else
                {
                    vk = wParam.ToInt32();
                    down = (lParam.ToInt64() & 0x80000000L) == 0; // bit 31: 1 = key released
                }
                KeyEventArgs args = new KeyEventArgs((Keys)vk | Control.ModifierKeys);
                KeyEventHandler handler = down ? KeyDown : KeyUp;
                if (handler != null)
                    handler(this, args);
                if (args.Handled)
                    return (IntPtr)1;
            }
            return CallNextHookEx(m_Handle, code, wParam, lParam);
        }

        public void Dispose()
        {
            Enabled = false;
        }
    }
}
