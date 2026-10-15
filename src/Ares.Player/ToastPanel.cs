using System;
using System.Drawing;
using System.Windows.Forms;

namespace Ares.Player
{
    /// <summary>
    /// Small, non-intrusive notification shown in a corner of the Player window instead of a pop-up window.
    /// Disappears by itself; a click opens the full message list.
    /// </summary>
    public class ToastPanel : Control
    {
        private readonly Timer m_Timer = new Timer();
        private string m_Text = string.Empty;
        private bool m_IsError;
        private int m_Count;

        public event EventHandler Activated;

        public ToastPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Visible = false;
            Cursor = Cursors.Hand;
            Size = new Size(470, 78);
            m_Timer.Interval = 6000;
            m_Timer.Tick += (s, e) => { m_Timer.Stop(); Visible = false; m_Count = 0; };
        }

        public void ShowToast(string text, bool isError)
        {
            bool isNew = !Visible;
            m_Count = Visible ? m_Count + 1 : 1;
            m_Text = text ?? string.Empty;
            m_IsError = isError;
            PositionInParent();
            Visible = true;
            BringToFront();
            Invalidate();
            m_Timer.Stop();
            m_Timer.Start();
            if (isNew)
                ToastSound.Play(isError);
        }

        public void PositionInParent()
        {
            if (Parent == null)
                return;
            float scale = Math.Max(1f, DeviceDpi / 96f);
            int w = Math.Min((int)(470 * scale), Math.Max(200, Parent.ClientSize.Width - 24));
            Size = new Size(w, (int)(78 * scale));
            Location = new Point(Parent.ClientSize.Width - Width - 12, Parent.ClientSize.Height - Height - 12);
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            m_Timer.Stop();
            Visible = false;
            m_Count = 0;
            if (Activated != null)
                Activated(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var theme = Ares.CommonGUI.ThemeManager.Current;
            bool dark = theme != null && !theme.IsClassic && theme.IsDark;
            Color back = dark ? Color.FromArgb(45, 48, 58) : Color.FromArgb(255, 252, 235);
            Color fore = dark ? Color.FromArgb(235, 235, 240) : Color.FromArgb(40, 40, 40);
            Color accent = m_IsError ? Color.FromArgb(220, 80, 80) : Color.FromArgb(230, 170, 50);
            Graphics g = e.Graphics;
            g.Clear(back);
            using (var accentBrush = new SolidBrush(accent))
                g.FillRectangle(accentBrush, 0, 0, 6, Height);
            using (var pen = new Pen(dark ? Color.FromArgb(90, 95, 110) : Color.FromArgb(210, 200, 160)))
                g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            string text = m_Count > 1 ? m_Text + "  (+" + (m_Count - 1) + ")" : m_Text;
            var rect = new Rectangle(16, 6, Width - 26, Height - 12);
            using (Font big = new Font(Font.FontFamily, Font.Size + 1.5f, FontStyle.Regular))
            {
                TextRenderer.DrawText(g, text, big, rect, fore,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                m_Timer.Dispose();
            base.Dispose(disposing);
        }
    }
}

namespace Ares.Player
{
    /// <summary>
    /// A soft two-note chime for the small notifications. It is made in memory and played through Windows on the default
    /// device, only for the person at the PC. While the sound of Apolo is being sent to Discord / the virtual microphone /
    /// VDO (Windows then captures everything this program plays) the chime stays silent, so that nobody else hears it.
    /// </summary>
    internal static class ToastSound
    {
        private static System.Media.SoundPlayer s_Info;
        private static System.Media.SoundPlayer s_Error;

        public static void Play(bool isError)
        {
            try
            {
                if (!Ares.CommonGUI.Preferences.GetBool("ToastSound", true) || AppAudioCapture.AnyActive)
                    return;
                System.Media.SoundPlayer player;
                if (isError)
                    player = s_Error ?? (s_Error = Make(new double[] { 523.25, 392.0 }));
                else
                    player = s_Info ?? (s_Info = Make(new double[] { 783.99, 987.77 }));
                player.Play();
            }
            catch (Exception)
            {
            }
        }

        private static System.Media.SoundPlayer Make(double[] notes)
        {
            const int rate = 44100;
            double noteSeconds = 0.16;
            int perNote = (int)(rate * noteSeconds);
            int tail = (int)(rate * 0.18);
            int total = perNote * notes.Length + tail;
            short[] samples = new short[total];
            for (int n = 0; n < notes.Length; n++)
            {
                int start = n * perNote;
                int length = perNote + (n == notes.Length - 1 ? tail : 0);
                for (int i = 0; i < length && start + i < total; i++)
                {
                    double t = i / (double)rate;
                    double attack = Math.Min(1.0, i / (rate * 0.008));
                    double decay = Math.Exp(-t * 14.0);
                    double value = Math.Sin(2 * Math.PI * notes[n] * t) * attack * decay * 0.16;
                    int mixed = samples[start + i] + (int)(value * short.MaxValue);
                    samples[start + i] = (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, mixed));
                }
            }
            System.IO.MemoryStream ms = new System.IO.MemoryStream();
            System.IO.BinaryWriter w = new System.IO.BinaryWriter(ms);
            int dataBytes = total * 2;
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + dataBytes);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16);
            w.Write((short)1);
            w.Write((short)1);
            w.Write(rate);
            w.Write(rate * 2);
            w.Write((short)2);
            w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            w.Write(dataBytes);
            foreach (short s in samples)
                w.Write(s);
            w.Flush();
            ms.Position = 0;
            System.Media.SoundPlayer player = new System.Media.SoundPlayer(ms);
            player.Load();
            return player;
        }
    }
}
