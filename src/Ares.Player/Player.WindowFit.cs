/*
 Size and place of the main window: it is remembered between runs (also "maximized") and it is always made to fit
 on a screen. Before, the window got the size of the design scaled by the fonts and the text size, even if that was
 bigger than the monitor, and had to be adjusted by hand at every start.
*/
using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace Ares.Player
{
    public partial class Player
    {
        private Timer m_WindowSaveTimer;
        private bool m_WindowRestored;

        private void InitWindowFit()
        {
            Load += (s, e) => RestoreWindowBounds();
            // the theme applies its text size after the load: look again before the window is shown
            Shown += (s, e) => KeepWindowOnScreen();
            m_WindowSaveTimer = new Timer();
            m_WindowSaveTimer.Interval = 700;
            m_WindowSaveTimer.Tick += (s, e) => { m_WindowSaveTimer.Stop(); SaveWindowBounds(); };
            SizeChanged += (s, e) => { if (m_WindowRestored) { m_WindowSaveTimer.Stop(); m_WindowSaveTimer.Start(); } };
            LocationChanged += (s, e) => { if (m_WindowRestored) { m_WindowSaveTimer.Stop(); m_WindowSaveTimer.Start(); } };
            FormClosing += (s, e) => SaveWindowBounds();
        }

        private void SaveWindowBounds()
        {
            try
            {
                if (WindowState == FormWindowState.Minimized)
                    return;
                Rectangle b = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
                Ares.CommonGUI.Preferences.Set("WindowBounds", string.Join(",", new[] { b.X, b.Y, b.Width, b.Height }.Select(v => v.ToString(CultureInfo.InvariantCulture))));
                Ares.CommonGUI.Preferences.SetBool("WindowMaximized", WindowState == FormWindowState.Maximized);
            }
            catch (Exception)
            {
                // remembering the window is a convenience
            }
        }

        private void RestoreWindowBounds()
        {
            try
            {
                Rectangle wanted = Rectangle.Empty;
                string saved = Ares.CommonGUI.Preferences.Get("WindowBounds", "");
                string[] parts = saved.Split(',');
                int x, y, w, h;
                if (parts.Length == 4 && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out x)
                    && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out y)
                    && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out w)
                    && int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out h) && w > 300 && h > 200)
                {
                    wanted = new Rectangle(x, y, w, h);
                }
                if (wanted.IsEmpty)
                    wanted = new Rectangle(Left, Top, Width, Height);
                StartPosition = FormStartPosition.Manual;
                Bounds = FitToScreen(wanted, wasSaved: !string.IsNullOrEmpty(saved));
                if (!m_HideToTray && Ares.CommonGUI.Preferences.GetBool("WindowMaximized", false))
                    WindowState = FormWindowState.Maximized;
            }
            catch (Exception)
            {
                // the window keeps the size it has
            }
            m_WindowRestored = true;
        }

        // after the theme and the text size are applied the window may have grown: bring it back inside the screen
        private void KeepWindowOnScreen()
        {
            try
            {
                if (WindowState != FormWindowState.Normal)
                    return;
                Rectangle fitted = FitToScreen(Bounds, wasSaved: true);
                if (fitted != Bounds)
                    Bounds = fitted;
            }
            catch (Exception)
            {
            }
        }

        // the rectangle moved and, if needed, made smaller so that it lies inside the work area of the screen it is on
        private static Rectangle FitToScreen(Rectangle wanted, bool wasSaved)
        {
            Screen screen = Screen.FromRectangle(wanted);
            Rectangle area = screen.WorkingArea;
            // a saved place on a monitor which is not there any more: use the main one
            Rectangle visible = Rectangle.Intersect(area, wanted);
            if (visible.Width < 200 || visible.Height < 100)
            {
                area = Screen.PrimaryScreen.WorkingArea;
                wasSaved = false;
            }
            int w = Math.Min(wanted.Width, area.Width);
            int h = Math.Min(wanted.Height, area.Height);
            int x = wasSaved ? wanted.X : area.X + (area.Width - w) / 2;
            int y = wasSaved ? wanted.Y : area.Y + (area.Height - h) / 2;
            x = Math.Max(area.Left, Math.Min(x, area.Right - w));
            y = Math.Max(area.Top, Math.Min(y, area.Bottom - h));
            return new Rectangle(x, y, w, h);
        }
    }
}
