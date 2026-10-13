using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Ares.Player
{
    // The Editor as a tab of the main window (like the web pages): the Editor program runs as its own process,
    // and its window is placed inside the tab, so there is no second program window.
    public partial class Player
    {
        private EditorHost m_EditorHost;
        private bool m_ShowingEditor;
        private bool m_QuitAfterEditor;

        private static string EditorExePath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Apolo.Editor.exe"); }
        }

        private void ShowEditorTab()
        {
            if (m_EditorHost == null || m_EditorHost.IsDisposed || !m_EditorHost.IsRunning)
            {
                if (!File.Exists(EditorExePath))
                {
                    MessageBox.Show(this, T("O Editor (Apolo.Editor.exe) não foi encontrado ao lado do Apolo.", "The Editor (Apolo.Editor.exe) was not found next to Apolo."),
                        StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (m_EditorHost != null && !m_EditorHost.IsDisposed)
                {
                    Controls.Remove(m_EditorHost);
                    m_EditorHost.Dispose();
                }
                m_EditorHost = new EditorHost(T("Abrindo o Editor...", "Opening the Editor..."));
                m_EditorHost.Dock = DockStyle.Fill;
                m_EditorHost.Visible = false;
                m_EditorHost.Exited += EditorHost_Exited;
                Controls.Add(m_EditorHost);
                string error = m_EditorHost.Start(EditorExePath, m_Project != null ? m_Project.FileName : null);
                if (error != null)
                {
                    Controls.Remove(m_EditorHost);
                    m_EditorHost.Dispose();
                    m_EditorHost = null;
                    MessageBox.Show(this, String.Format(StringResources.EditorStartError, error), StringResources.Ares,
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }
            m_ShowingWeb = false;
            m_ShowingEditor = true;
            if (m_WebWindow != null)
                m_WebWindow.Visible = false;
            m_EditorHost.Visible = true;
            m_EditorHost.BringToFront();
            m_TabBar.SendToBack();
            m_EditorHost.FocusEditor();
            RefreshTabBar();
        }

        private void HideEditorTab()
        {
            m_ShowingEditor = false;
            if (m_EditorHost != null && !m_EditorHost.IsDisposed)
                m_EditorHost.Visible = false;
        }

        private void EditorHost_Exited(object sender, EventArgs e)
        {
            EditorHost host = (EditorHost)sender;
            bool wasShowing = m_ShowingEditor;
            if (m_EditorHost == host)
                m_EditorHost = null;
            Controls.Remove(host);
            host.Dispose();
            if (wasShowing)
            {
                m_ShowingEditor = false;
                ShowHome();
            }
            RefreshTabBar();
            if (!host.Adopted)
            {
                MessageBox.Show(this, T("O Editor não abriu. Se ele já estiver aberto em outra janela, feche-o e tente de novo.",
                    "The Editor did not open. If it is already open in another window, close it and try again."),
                    StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            if (m_QuitAfterEditor)
            {
                m_QuitAfterEditor = false;
                m_Quitting = true;
                Close();
            }
        }

        // closing the program while the Editor is open: the Editor asks about unsaved changes first
        private bool EditorBlocksClosing(FormClosingEventArgs e)
        {
            if (m_EditorHost == null || m_EditorHost.IsDisposed || !m_EditorHost.IsRunning)
                return false;
            if (e.CloseReason != CloseReason.UserClosing)
                return false;
            if (!m_Quitting && CloseToTrayEnabled)
                return false; // only goes to the tray
            e.Cancel = true;
            m_QuitAfterEditor = true;
            ShowEditorTab();
            m_EditorHost.RequestClose();
            return true;
        }

        private int EditorTabIndex
        {
            get { return (m_EditorHost != null && !m_EditorHost.IsDisposed) ? 1 + m_WebWindow.Tabs.Count : -1; }
        }
    }

    /// <summary>Panel which shows the main window of another program inside itself.</summary>
    internal class EditorHost : Panel
    {
        [DllImport("user32.dll")] private static extern IntPtr SetParent(IntPtr child, IntPtr newParent);
        [DllImport("user32.dll", EntryPoint = "GetWindowLong")] private static extern int GetWindowLong32(IntPtr hWnd, int index);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")] private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLong")] private static extern int SetWindowLong32(IntPtr hWnd, int index, int value);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")] private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int index, IntPtr value);
        [DllImport("user32.dll")] private static extern bool MoveWindow(IntPtr hWnd, int x, int y, int w, int h, bool repaint);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int command);
        [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern IntPtr SetFocus(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
        [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int w, int h, uint flags);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

        private const int GWL_STYLE = -16;
        private const int GWL_EXSTYLE = -20;
        private const long WS_CHILD = 0x40000000L;
        private const long WS_VISIBLE = 0x10000000L;
        private const long WS_POPUP = 0x80000000L;
        private const long WS_CAPTION = 0x00C00000L;
        private const long WS_THICKFRAME = 0x00040000L;
        private const long WS_SYSMENU = 0x00080000L;
        private const long WS_MINIMIZEBOX = 0x00020000L;
        private const long WS_MAXIMIZEBOX = 0x00010000L;
        private const long WS_EX_APPWINDOW = 0x00040000L;
        private const long WS_EX_WINDOWEDGE = 0x00000100L;
        private const long WS_EX_CLIENTEDGE = 0x00000200L;
        private const int SW_RESTORE = 9;
        private const int WM_CLOSE = 0x0010;
        private const uint SWP_NOMOVE = 0x2, SWP_NOSIZE = 0x1, SWP_NOZORDER = 0x4, SWP_FRAMECHANGED = 0x20;

        private Process m_Process;
        private IntPtr m_Window = IntPtr.Zero;
        private readonly Timer m_Timer = new Timer();
        private readonly Label m_Waiting;
        private DateTime m_Started;

        public event EventHandler Exited;

        /// <summary>True once the Editor window was placed in the tab.</summary>
        public bool Adopted { get; private set; }

        public EditorHost(string waitingText)
        {
            m_Waiting = new Label();
            m_Waiting.Dock = DockStyle.Fill;
            m_Waiting.TextAlign = ContentAlignment.MiddleCenter;
            m_Waiting.Text = waitingText;
            Controls.Add(m_Waiting);
            m_Timer.Interval = 150;
            m_Timer.Tick += (s, e) => Poll();
        }

        public bool IsRunning
        {
            get { return m_Process != null && !m_Process.HasExited; }
        }

        /// <summary>Starts the Editor; returns null if it started, or the reason why not.</summary>
        public string Start(string exePath, string projectPath)
        {
            try
            {
                ProcessStartInfo info = new ProcessStartInfo(exePath);
                info.UseShellExecute = false;
                info.WorkingDirectory = Path.GetDirectoryName(exePath);
                if (!string.IsNullOrEmpty(projectPath))
                    info.Arguments = "\"" + projectPath + "\"";
                m_Process = Process.Start(info);
                m_Started = DateTime.Now;
                m_Timer.Start();
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private static long GetStyle(IntPtr window, int index)
        {
            return IntPtr.Size == 8 ? GetWindowLongPtr64(window, index).ToInt64() : (long)(uint)GetWindowLong32(window, index);
        }

        private static void SetStyle(IntPtr window, int index, long value)
        {
            if (IntPtr.Size == 8)
                SetWindowLongPtr64(window, index, new IntPtr(value));
            else
                SetWindowLong32(window, index, unchecked((int)(uint)value));
        }

        private void Poll()
        {
            try
            {
                if (m_Process == null)
                    return;
                if (m_Process.HasExited)
                {
                    m_Timer.Stop();
                    m_Window = IntPtr.Zero;
                    EventHandler handler = Exited;
                    if (handler != null)
                        handler(this, EventArgs.Empty);
                    return;
                }
                if (m_Window == IntPtr.Zero)
                {
                    m_Process.Refresh();
                    IntPtr h = m_Process.MainWindowHandle;
                    if (h != IntPtr.Zero && m_Process.MainWindowTitle.Length > 0 && IsHandleCreated)
                        Adopt(h);
                    return;
                }
                if (!IsWindow(m_Window))
                    m_Window = IntPtr.Zero;
            }
            catch (Exception)
            {
                // the process went away while it was asked: the next tick sees HasExited
            }
        }

        private void Adopt(IntPtr window)
        {
            m_Window = window;
            Adopted = true;
            ShowWindow(window, SW_RESTORE);
            long style = GetStyle(window, GWL_STYLE);
            style &= ~(WS_CAPTION | WS_THICKFRAME | WS_POPUP | WS_SYSMENU | WS_MINIMIZEBOX | WS_MAXIMIZEBOX);
            style |= WS_CHILD | WS_VISIBLE;
            SetStyle(window, GWL_STYLE, style);
            long ex = GetStyle(window, GWL_EXSTYLE);
            ex &= ~(WS_EX_APPWINDOW | WS_EX_WINDOWEDGE | WS_EX_CLIENTEDGE);
            SetStyle(window, GWL_EXSTYLE, ex);
            SetParent(window, Handle);
            SetWindowPos(window, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
            m_Waiting.Visible = false;
            Fit();
            if (Visible)
                FocusEditor();
        }

        private void Fit()
        {
            if (m_Window != IntPtr.Zero && ClientSize.Width > 0 && ClientSize.Height > 0)
                MoveWindow(m_Window, 0, 0, ClientSize.Width, ClientSize.Height, true);
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            Fit();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible)
            {
                Fit();
                FocusEditor();
            }
        }

        /// <summary>Gives the keyboard focus to the Editor window.</summary>
        public void FocusEditor()
        {
            if (m_Window == IntPtr.Zero || !IsWindow(m_Window))
                return;
            try
            {
                uint pid;
                uint thread = GetWindowThreadProcessId(m_Window, out pid);
                uint mine = GetCurrentThreadId();
                AttachThreadInput(mine, thread, true);
                SetFocus(m_Window);
                AttachThreadInput(mine, thread, false);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>Asks the Editor to close like a user would (it asks about unsaved changes itself).</summary>
        public void RequestClose()
        {
            if (m_Window != IntPtr.Zero && IsWindow(m_Window))
            {
                PostMessage(m_Window, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            }
            else if (IsRunning)
            {
                try { m_Process.CloseMainWindow(); } catch (Exception) { }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                m_Timer.Stop();
                m_Timer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
