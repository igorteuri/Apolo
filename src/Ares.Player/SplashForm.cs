using System;
using System.Drawing;
using System.Windows.Forms;

namespace Ares.Player
{
    /// <summary>
    /// Small window shown while Apolo starts, so that the main window can appear only when it is complete
    /// (and not flash with the white colors of Windows). It paints itself and has no controls.
    /// </summary>
    internal sealed class SplashForm : Form
    {
        private static SplashForm s_Instance;

        public static void ShowSplash()
        {
            try
            {
                s_Instance = new SplashForm();
                s_Instance.Show();
                s_Instance.Refresh();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                s_Instance = null;
            }
        }

        public static void CloseSplash()
        {
            SplashForm form = s_Instance;
            s_Instance = null;
            if (form == null || form.IsDisposed)
                return;
            try
            {
                form.Close();
                form.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }

        private readonly float m_Scale;

        private SplashForm()
        {
            m_Scale = Math.Max(1f, Ares.CommonGUI.ThemeManager.ScalePercent / 100f);
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = false;
            ShowIcon = false;
            DoubleBuffered = true;
            ClientSize = new Size((int)(420 * m_Scale), (int)(240 * m_Scale));
            Text = "Apolo";
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Ares.CommonGUI.ThemePalette p = Ares.CommonGUI.ThemeManager.Current;
            bool themed = p != null && !p.IsClassic;
            Color back = themed ? p.Window : Color.FromArgb(27, 29, 34);
            Color text = themed ? p.Text : Color.FromArgb(235, 237, 240);
            Color dim = themed ? p.TextDim : Color.FromArgb(150, 156, 166);
            Color accent = themed ? p.Accent : Color.FromArgb(245, 184, 0);
            Graphics g = e.Graphics;
            g.Clear(back);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            int logo = (int)(96 * m_Scale);
            try
            {
                using (Bitmap bmp = Ares.CommonGUI.ApoloBrand.Render(logo, Ares.CommonGUI.ApoloState.Playing, 0))
                    g.DrawImage(bmp, (ClientSize.Width - logo) / 2, (int)(28 * m_Scale), logo, logo);
            }
            catch (Exception)
            {
            }
            Rectangle nameRect = new Rectangle(0, (int)(136 * m_Scale), ClientSize.Width, (int)(44 * m_Scale));
            using (Font f = new Font("Segoe UI", 24f * m_Scale * 0.75f, FontStyle.Bold))
                TextRenderer.DrawText(g, "Apolo", f, nameRect, text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            Rectangle loadRect = new Rectangle(0, (int)(184 * m_Scale), ClientSize.Width, (int)(24 * m_Scale));
            using (Font f = new Font("Segoe UI", 10f))
                TextRenderer.DrawText(g, System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt" ? "Carregando..." : "Loading...",
                    f, loadRect, dim, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            using (Pen pen = new Pen(accent, 2f))
                g.DrawRectangle(pen, 1, 1, ClientSize.Width - 3, ClientSize.Height - 3);
        }
    }
}
