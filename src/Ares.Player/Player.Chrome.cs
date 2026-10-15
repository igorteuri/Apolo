/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 The window without its own title bar: the tab bar is at the very top, with the buttons to minimize, maximize and close
 at its right end, like a browser. The window keeps its normal frame on the left, right and bottom (resizing, snapping
 to the sides of the screen and the shadow keep working); only the title bar is taken away, and the empty part of the
 tab bar moves the window.

 To go back to the normal title bar: Preferences key "CustomTitleBar" = false.
*/
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Ares.Player
{
    public partial class Player
    {
        private const int WM_NCCALCSIZE = 0x0083;
        private const int WM_NCHITTEST = 0x0084;
        private const int HTCLIENT = 1;
        private const int HTCAPTION = 2;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int SM_CYSIZEFRAME = 33;
        private const int SM_CXPADDEDBORDER = 92;
        private const int TopBand = 5;
        private const int CornerSize = 12;

        private bool m_CustomChrome = Ares.CommonGUI.Preferences.GetBool("CustomTitleBar", true);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int index);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

        // called when the tab bar exists: connects the three buttons
        private void InitCustomChrome()
        {
            if (m_TabBar == null)
                return;
            m_TabBar.CaptionButtons = m_CustomChrome;
            if (!m_CustomChrome)
                return;
            m_TabBar.MinimizeRequested += () => WindowState = FormWindowState.Minimized;
            m_TabBar.MaximizeRequested += () =>
                WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
            m_TabBar.CloseRequested += () => Close();
            SizeChanged += (s, e) => m_TabBar.Maximized = WindowState == FormWindowState.Maximized;
            m_TabBar.Maximized = WindowState == FormWindowState.Maximized;
            // the window already exists: ask Windows to calculate the frame again
            if (IsHandleCreated)
                SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0004 | 0x0010 | 0x0020);
            else
                HandleCreated += (s, e) => SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0004 | 0x0010 | 0x0020);
        }

        protected override void WndProc(ref Message m)
        {
            if (m_CustomChrome && m_TabBar != null)
            {
                if (m.Msg == WM_NCCALCSIZE && m.WParam != IntPtr.Zero)
                {
                    // the new client area starts at the top of the window (no title bar); on the other sides the normal frame stays
                    int top = Marshal.ReadInt32(m.LParam, 4);
                    base.WndProc(ref m);
                    int newTop = top;
                    if (WindowState == FormWindowState.Maximized)
                        newTop += GetSystemMetrics(SM_CYSIZEFRAME) + GetSystemMetrics(SM_CXPADDEDBORDER);
                    Marshal.WriteInt32(m.LParam, 4, newTop);
                    return;
                }
                if (m.Msg == WM_NCHITTEST)
                {
                    base.WndProc(ref m);
                    if (m.Result == new IntPtr(HTCLIENT) && WindowState != FormWindowState.Maximized)
                    {
                        long lp = m.LParam.ToInt64();
                        Point client = PointToClient(new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF)));
                        if (client.Y >= 0 && client.Y < TopBand)
                        {
                            if (client.X < CornerSize)
                                m.Result = new IntPtr(HTTOPLEFT);
                            else if (client.X >= ClientSize.Width - CornerSize)
                                m.Result = new IntPtr(HTTOPRIGHT);
                            else
                                m.Result = new IntPtr(HTTOP);
                            return;
                        }
                    }
                    if (m.Result == new IntPtr(HTCLIENT))
                    {
                        long lp2 = m.LParam.ToInt64();
                        Point client2 = PointToClient(new Point((short)(lp2 & 0xFFFF), (short)((lp2 >> 16) & 0xFFFF)));
                        if (m_TabBar.Visible && m_TabBar.Bounds.Contains(client2) && m_TabBar.IsCaptionAt(new Point(client2.X - m_TabBar.Left, client2.Y - m_TabBar.Top)))
                            m.Result = new IntPtr(HTCAPTION);
                    }
                    return;
                }
            }
            base.WndProc(ref m);
        }
    }
}
