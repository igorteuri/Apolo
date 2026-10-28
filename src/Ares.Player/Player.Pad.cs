/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 The floating pad: the buttons of one folder in a small see-through window which stays on top of the screen (like a virtual
 Stream Deck). It opens with a global keyboard shortcut (Ctrl+Alt+P unless changed) or from Extras > Floating pad, it does
 not take the focus away from the game, and it closes after a click (unless it is pinned).
*/
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Ares.Data;
using Keys = System.Windows.Forms.Keys;

namespace Ares.Player
{
    public partial class Player
    {
        private const int PadHotkeyId = 0xA901;
        private const Keys PadHotkeyDefault = Keys.Control | Keys.Alt | Keys.P;

        private FloatingPad m_Pad;
        private PadHotkeyWindow m_PadHotkeyWindow;
        private ToolStripMenuItem m_PadShowItem;

        private static Keys PadHotkey
        {
            get
            {
                int value;
                if (int.TryParse(Ares.CommonGUI.Preferences.Get("PadHotkey", ""), out value) && value != 0)
                    return (Keys)value;
                return PadHotkeyDefault;
            }
        }

        private static int PadOpacityPercent
        {
            get
            {
                int value;
                if (int.TryParse(Ares.CommonGUI.Preferences.Get("PadOpacity", "85"), out value))
                    return Math.Max(40, Math.Min(100, value));
                return 85;
            }
        }

        private void InitPad()
        {
            ToolStripMenuItem root = new ToolStripMenuItem(T("Pad flutuante", "Floating pad"));
            m_PadShowItem = new ToolStripMenuItem();
            m_PadShowItem.Click += (s, e) => TogglePad();
            ToolStripMenuItem shortcut = new ToolStripMenuItem(T("Escolher o atalho...", "Choose the shortcut..."));
            shortcut.Click += (s, e) => ChoosePadHotkey();
            ToolStripMenuItem closeOnClick = new ToolStripMenuItem(T("Fechar depois de clicar num botão", "Close after clicking a button"));
            closeOnClick.CheckOnClick = true;
            closeOnClick.Checked = Ares.CommonGUI.Preferences.GetBool("PadCloseOnClick", true);
            closeOnClick.CheckedChanged += (s, e) => Ares.CommonGUI.Preferences.SetBool("PadCloseOnClick", closeOnClick.Checked);
            ToolStripMenuItem opacity = new ToolStripMenuItem(T("Transparência", "Transparency"));
            int[] steps = { 100, 85, 70, 55 };
            string[] names = { T("Nenhuma", "None"), T("Pouca", "Little"), T("Média", "Medium"), T("Muita", "A lot") };
            for (int i = 0; i < steps.Length; i++)
            {
                int percent = steps[i];
                ToolStripMenuItem option = new ToolStripMenuItem(names[i]);
                option.Click += (s, e) =>
                {
                    Ares.CommonGUI.Preferences.Set("PadOpacity", percent.ToString());
                    if (m_Pad != null)
                        m_Pad.Opacity = percent / 100.0;
                };
                opacity.DropDownItems.Add(option);
            }
            opacity.DropDownOpening += (s, e) =>
            {
                int now = PadOpacityPercent;
                for (int i = 0; i < steps.Length; i++)
                    ((ToolStripMenuItem)opacity.DropDownItems[i]).Checked = steps[i] == now;
            };
            m_PadCloseItem = closeOnClick; // its options are on the "Keyboard and pad" page of the settings
            extrasToolStripMenuItem.DropDownOpening += (s, e) =>
            {
                m_PadShowItem.Text = (m_Pad != null ? T("Esconder o pad flutuante", "Hide the floating pad") : T("Mostrar o pad flutuante", "Show the floating pad"))
                    + "   (" + KeyText(PadHotkey) + ")";
            };
            m_PadShowItem.Text = T("Mostrar o pad flutuante", "Show the floating pad") + "   (" + KeyText(PadHotkey) + ")";
            extrasToolStripMenuItem.DropDownItems.Insert(0, m_PadShowItem);

            try
            {
                m_PadHotkeyWindow = new PadHotkeyWindow(() => BeginInvoke((MethodInvoker)TogglePad));
                if (!m_PadHotkeyWindow.Register(PadHotkey))
                {
                    ShowToast(new Ares.Players.Message(Ares.Players.MessageType.Warning,
                        T("O atalho do pad flutuante (" + KeyText(PadHotkey) + ") já é usado por outro programa. Escolha outro em Configurações > Teclado e pad.",
                          "The floating pad shortcut (" + KeyText(PadHotkey) + ") is already used by another program. Choose another in Settings > Keyboard and pad.")));
                }
            }
            catch (Exception swallowed)
            {
                Ares.Settings.ErrorLog.Note(swallowed);
            }
            FormClosed += (s, e) =>
            {
                if (m_PadHotkeyWindow != null)
                    m_PadHotkeyWindow.Dispose();
            };
        }

        private void TogglePad()
        {
            if (m_Pad != null)
            {
                m_Pad.Close();
                return;
            }
            if (m_Project == null || m_Project.GetModes().Count == 0)
            {
                ShowToast(new Ares.Players.Message(Ares.Players.MessageType.Info,
                    T("Abra um projeto com pastas e botões para usar o pad flutuante.", "Open a project with folders and buttons to use the floating pad.")));
                return;
            }
            IMode start = CurrentMode();
            int index = start != null ? new List<IMode>(m_Project.GetModes()).IndexOf(start) : 0;
            m_Pad = new FloatingPad(this, Math.Max(0, index));
            m_Pad.FormClosed += (s, e) => m_Pad = null;
            m_Pad.Show();
        }

        private void ChoosePadHotkey()
        {
            Keys chosen = PadHotkey;
            using (Form dlg = new Form())
            {
                dlg.Text = T("Atalho do pad flutuante", "Floating pad shortcut");
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;
                dlg.ShowInTaskbar = false;
                dlg.KeyPreview = true;
                dlg.ClientSize = new Size(380, 130);
                Label info = new Label { Left = 14, Top = 12, Width = 350, Height = 34,
                    Text = T("Pressione a combinação de teclas (use Ctrl, Alt ou Shift junto com uma tecla). Ela funciona mesmo com o Apolo em segundo plano.",
                             "Press the key combination (use Ctrl, Alt or Shift together with a key). It works even when Apolo is in the background.") };
                Label shown = new Label { Left = 14, Top = 54, Width = 350, Height = 26, Font = new Font(Font, FontStyle.Bold), Text = KeyText(chosen) };
                Button ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Left = 190, Top = 92, Width = 84 };
                Button cancel = new Button { Text = T("Cancelar", "Cancel"), DialogResult = DialogResult.Cancel, Left = 280, Top = 92, Width = 84 };
                Button reset = new Button { Text = T("Padrão", "Default"), Left = 14, Top = 92, Width = 84 };
                reset.Click += (s, e) => { chosen = PadHotkeyDefault; shown.Text = KeyText(chosen); };
                dlg.Controls.AddRange(new Control[] { info, shown, ok, cancel, reset });
                dlg.AcceptButton = null;
                dlg.CancelButton = cancel;
                dlg.KeyDown += (s, e) =>
                {
                    Keys code = e.KeyCode;
                    if (code == Keys.ControlKey || code == Keys.ShiftKey || code == Keys.Menu || code == Keys.None)
                        return;
                    Keys mods = e.Modifiers & (Keys.Control | Keys.Alt | Keys.Shift);
                    if (mods == Keys.None)
                        return;
                    chosen = mods | code;
                    shown.Text = KeyText(chosen);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                };
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;
                if (m_PadHotkeyWindow != null && m_PadHotkeyWindow.Register(chosen))
                {
                    Ares.CommonGUI.Preferences.Set("PadHotkey", ((int)chosen).ToString());
                    ShowToast(new Ares.Players.Message(Ares.Players.MessageType.Info,
                        T("Atalho do pad flutuante: ", "Floating pad shortcut: ") + KeyText(chosen)));
                }
                else
                {
                    if (m_PadHotkeyWindow != null)
                        m_PadHotkeyWindow.Register(PadHotkey);
                    MessageBox.Show(this, T("Essa combinação já é usada por outro programa. Escolha outra.", "That combination is already used by another program. Choose another."),
                        StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        // a hidden window which receives the global shortcut of the pad
        private class PadHotkeyWindow : NativeWindow, IDisposable
        {
            [DllImport("user32.dll")]
            private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);
            [DllImport("user32.dll")]
            private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

            private readonly Action m_Action;
            private bool m_Registered;

            public PadHotkeyWindow(Action action)
            {
                m_Action = action;
                CreateHandle(new CreateParams());
            }

            public bool Register(Keys keys)
            {
                Unregister();
                uint modifiers = 0x4000; // no repeat
                if ((keys & Keys.Alt) != 0)
                    modifiers |= 1;
                if ((keys & Keys.Control) != 0)
                    modifiers |= 2;
                if ((keys & Keys.Shift) != 0)
                    modifiers |= 4;
                m_Registered = RegisterHotKey(Handle, PadHotkeyId, modifiers, (uint)(keys & Keys.KeyCode));
                return m_Registered;
            }

            private void Unregister()
            {
                if (m_Registered)
                {
                    UnregisterHotKey(Handle, PadHotkeyId);
                    m_Registered = false;
                }
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == 0x0312 && m.WParam.ToInt32() == PadHotkeyId)
                {
                    m_Action();
                    return;
                }
                base.WndProc(ref m);
            }

            public void Dispose()
            {
                Unregister();
                if (Handle != IntPtr.Zero)
                    DestroyHandle();
            }
        }

        // a flat button with a line icon of the interface (same icons as the toolbar of the player)
        private class PadIconButton : Control
        {
            private readonly int m_IconPx;
            private string m_IconName;
            private bool m_Hover;
            private bool m_Down;
            private bool m_Toggled;

            public PadIconButton(string iconName, int iconPx)
            {
                m_IconName = iconName;
                m_IconPx = iconPx;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                    | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
                Cursor = Cursors.Hand;
                TabStop = false;
                BackColor = Color.Transparent;
            }

            public int PreferredWidth { get { return m_IconPx + 14; } }
            public int PreferredHeight { get { return m_IconPx + 12; } }

            public string IconName
            {
                get { return m_IconName; }
                set { m_IconName = value; Invalidate(); }
            }

            public bool Toggled
            {
                get { return m_Toggled; }
                set { m_Toggled = value; Invalidate(); }
            }

            protected override void OnMouseEnter(EventArgs e) { m_Hover = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { m_Hover = false; m_Down = false; Invalidate(); base.OnMouseLeave(e); }
            protected override void OnMouseDown(MouseEventArgs e) { m_Down = true; Invalidate(); base.OnMouseDown(e); }
            protected override void OnMouseUp(MouseEventArgs e) { m_Down = false; Invalidate(); base.OnMouseUp(e); }

            protected override void OnPaint(PaintEventArgs e)
            {
                Ares.CommonGUI.ThemePalette p = Ares.CommonGUI.ThemeManager.Current;
                bool themed = p != null && !p.IsClassic;
                Color back = Parent != null ? Ares.CommonGUI.ThemeManager.EffectiveBack(this) : SystemColors.Control;
                e.Graphics.Clear(back);
                if (m_Hover || m_Down || m_Toggled)
                {
                    Color fill = themed ? (m_Down ? p.Selection : (m_Toggled ? p.Selection : p.Hover)) : SystemColors.ControlLight;
                    e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    using (SolidBrush brush = new SolidBrush(fill))
                        e.Graphics.FillRectangle(brush, 1, 1, Width - 2, Height - 2);
                }
                using (Bitmap icon = Ares.CommonGUI.IconLibrary.Get(m_IconName, new Size(m_IconPx, m_IconPx)))
                {
                    if (icon != null)
                        e.Graphics.DrawImage(icon, (Width - m_IconPx) / 2, (Height - m_IconPx) / 2, m_IconPx, m_IconPx);
                }
            }
        }

        // the pad itself: a header (folder, playback, pin, close) and the buttons of the folder
        private class FloatingPad : Form
        {
            [DllImport("user32.dll")]
            private static extern bool ReleaseCapture();
            [DllImport("user32.dll")]
            private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

            private readonly Player m_Player;
            private readonly Panel m_Header = new Panel();
            private readonly Label m_Title = new Label();
            private readonly FlowLayoutPanel m_Flow = new FlowLayoutPanel();
            private readonly System.Windows.Forms.Timer m_Timer = new System.Windows.Forms.Timer();
            private readonly Dictionary<ElementButton, int> m_Ids = new Dictionary<ElementButton, int>();
            private readonly List<PadIconButton> m_HeaderButtons = new List<PadIconButton>();
            private PadIconButton m_Pin;
            private PadIconButton m_PauseIcon;
            private bool m_PauseIconIsPlay;
            private int m_ModeIndex;
            private bool m_Pinned;

            public FloatingPad(Player player, int modeIndex)
            {
                m_Player = player;
                m_ModeIndex = modeIndex;
                FormBorderStyle = FormBorderStyle.None;
                ShowInTaskbar = false;
                TopMost = true;
                StartPosition = FormStartPosition.Manual;
                Opacity = PadOpacityPercent / 100.0;
                Font = player.Font;
                DoubleBuffered = true;
                Padding = new Padding(1);

                Ares.CommonGUI.ThemePalette p = Ares.CommonGUI.ThemeManager.Current;
                bool themed = p != null && !p.IsClassic;
                BackColor = themed ? p.Window : SystemColors.Control;
                ForeColor = themed ? p.Text : SystemColors.ControlText;

                m_Header.Dock = DockStyle.Top;
                int icon = Ares.CommonGUI.IconLibrary.ToolbarIconSize().Width;
                m_Header.Height = (int)Math.Round(icon * 1.6) + icon + 6 * 4 + 12;
                m_Header.BackColor = themed ? p.Surface : SystemColors.ControlLight;
                m_Header.MouseDown += DragStart;

                m_Title.AutoSize = false;
                m_Title.AutoEllipsis = true;
                m_Title.TextAlign = ContentAlignment.MiddleLeft;
                m_Title.Font = new Font(Font, FontStyle.Bold);
                m_Title.ForeColor = ForeColor;
                m_Title.BackColor = Color.Transparent;
                m_Title.MouseDown += DragStart;
                m_Header.Controls.Add(m_Title);

                // arrows and the other small buttons are bigger than the toolbar; the playback buttons use the very same
                // icons and size as the player area of the main window
                AddHeaderButton("chevron-left", 1.6, T("Pasta anterior", "Previous folder"), () => ChangeMode(-1));
                AddHeaderButton("chevron-right", 1.6, T("Próxima pasta", "Next folder"), () => ChangeMode(1));
                AddHeaderButton("skip-back", 1.0, T("Música anterior", "Previous track"), () => m_Player.previousButton_Click(this, EventArgs.Empty));
                m_PauseIcon = AddHeaderButton("pause", 1.0, T("Pausar ou continuar", "Pause or resume"), () => m_Player.m_PauseButton.PerformClick());
                AddHeaderButton("square", 1.0, T("Parar tudo", "Stop all"), () => m_Player.stopButton_Click(this, EventArgs.Empty));
                AddHeaderButton("skip-forward", 1.0, T("Próxima música", "Next track"), () => m_Player.nextButton_Click(this, EventArgs.Empty));
                AddHeaderButton("repeat", 1.0, T("Repetir a música atual", "Repeat the current track"), () => m_Player.repeatButton_Click(this, EventArgs.Empty));
                m_Pin = AddHeaderButton("lock", 1.0, T("Fixar: o pad não fecha depois de um clique", "Pin: the pad stays open after a click"), TogglePin);
                AddHeaderButton("x", 1.2, T("Fechar o pad", "Close the pad"), Close);

                m_Flow.Dock = DockStyle.Fill;
                m_Flow.WrapContents = true;
                m_Flow.FlowDirection = FlowDirection.LeftToRight;
                m_Flow.AutoScroll = true;
                m_Flow.Padding = new Padding(4);
                m_Flow.BackColor = BackColor;
                m_Flow.MouseDown += DragStart;

                Controls.Add(m_Flow);
                Controls.Add(m_Header);

                m_Timer.Interval = 500;
                m_Timer.Tick += (s, e) => RefreshState();
                Rebuild();
                Ares.CommonGUI.ThemeManager.Apply(this);
            }

            protected override bool ShowWithoutActivation { get { return true; } }

            protected override CreateParams CreateParams
            {
                get
                {
                    CreateParams cp = base.CreateParams;
                    cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE: a click does not take the focus from the game
                    cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW: no entry in Alt+Tab
                    return cp;
                }
            }

            protected override void OnShown(EventArgs e)
            {
                base.OnShown(e);
                m_Timer.Start();
            }

            protected override void OnFormClosed(FormClosedEventArgs e)
            {
                m_Timer.Stop();
                m_Timer.Dispose();
                try
                {
                    Ares.CommonGUI.Preferences.Set("PadPos", Left + "," + Top);
                }
                catch (Exception swallowed)
                {
                    Ares.Settings.ErrorLog.Note(swallowed);
                }
                base.OnFormClosed(e);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                Ares.CommonGUI.ThemePalette p = Ares.CommonGUI.ThemeManager.Current;
                Color line = p != null && !p.IsClassic ? p.Accent : SystemColors.Highlight;
                using (Pen pen = new Pen(line))
                    e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }

            private static string T(string pt, string en)
            {
                return Player.T(pt, en);
            }

            private void DragStart(object sender, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left)
                    return;
                ReleaseCapture();
                SendMessage(Handle, 0xA1, 2, 0); // WM_NCLBUTTONDOWN, HTCAPTION: move the window
            }

            private PadIconButton AddHeaderButton(string icon, double factor, string tip, Action click)
            {
                int px = (int)Math.Round(Ares.CommonGUI.IconLibrary.ToolbarIconSize().Width * factor);
                PadIconButton b = new PadIconButton(icon, px);
                b.Click += (s, e) => click();
                new ToolTip().SetToolTip(b, tip);
                m_Header.Controls.Add(b);
                m_HeaderButtons.Add(b);
                return b;
            }

            private void TogglePin()
            {
                m_Pinned = !m_Pinned;
                m_Pin.Toggled = m_Pinned;
            }

            private void ChangeMode(int delta)
            {
                int count = m_Player.m_Project != null ? m_Player.m_Project.GetModes().Count : 0;
                if (count == 0)
                    return;
                m_ModeIndex = (m_ModeIndex + delta + count) % count;
                Rebuild();
            }

            // row 1: arrows, folder name, pin, close; row 2: previous, pause, stop, next, repeat (centered)
            private void LayoutHeader()
            {
                int pad = 6;
                int width = m_Header.Width;
                PadIconButton prev = m_HeaderButtons[0], next = m_HeaderButtons[1], close = m_HeaderButtons[8], pin = m_HeaderButtons[7];
                int row1 = prev.PreferredHeight;
                prev.SetBounds(pad, pad, prev.PreferredWidth, row1);
                close.SetBounds(width - pad - close.PreferredWidth, pad + (row1 - close.PreferredHeight) / 2, close.PreferredWidth, close.PreferredHeight);
                pin.SetBounds(close.Left - 2 - pin.PreferredWidth, pad + (row1 - pin.PreferredHeight) / 2, pin.PreferredWidth, pin.PreferredHeight);
                next.SetBounds(pin.Left - 4 - next.PreferredWidth, pad, next.PreferredWidth, row1);
                m_Title.SetBounds(prev.Right + 6, pad, Math.Max(20, next.Left - prev.Right - 12), row1);
                int total = 0;
                for (int i = 2; i <= 6; i++)
                    total += m_HeaderButtons[i].PreferredWidth + (i < 6 ? 6 : 0);
                int x = Math.Max(pad, (width - total) / 2);
                int y = pad + row1 + pad;
                for (int i = 2; i <= 6; i++)
                {
                    m_HeaderButtons[i].SetBounds(x, y, m_HeaderButtons[i].PreferredWidth, m_HeaderButtons[i].PreferredHeight);
                    x += m_HeaderButtons[i].PreferredWidth + 6;
                }
            }

            protected override void OnResize(EventArgs e)
            {
                base.OnResize(e);
                if (m_HeaderButtons.Count == 9)
                    LayoutHeader();
            }

            private void Rebuild()
            {
                Player pl = m_Player;
                m_Flow.SuspendLayout();
                Control[] old = new Control[m_Flow.Controls.Count];
                m_Flow.Controls.CopyTo(old, 0);
                foreach (Control c in old)
                {
                    m_Flow.Controls.Remove(c);
                    c.Dispose();
                }
                m_Ids.Clear();
                IList<IMode> modes = pl.m_Project != null ? pl.m_Project.GetModes() : null;
                if (modes == null || modes.Count == 0)
                {
                    m_Flow.ResumeLayout();
                    return;
                }
                m_ModeIndex = Math.Max(0, Math.Min(m_ModeIndex, modes.Count - 1));
                IMode mode = modes[m_ModeIndex];
                m_Title.Text = mode.Title;
                List<ShownButton> shown = pl.CollectShownButtons(mode);

                List<string> texts = new List<string>();
                List<string> specs = new List<string>();
                List<int> kinds = new List<int>();
                foreach (ShownButton sb in shown)
                {
                    string spec = Ares.CommonGUI.ButtonImageStore.Get(pl.m_CurrentProjectPath, sb.OwnerTitle, sb.Element.Title);
                    int kind = 0;
                    if (spec != null && (spec.StartsWith("img:") || spec.StartsWith("lib:"))
                        && Ares.CommonGUI.ButtonImageStore.LoadImage(pl.m_CurrentProjectPath, spec) != null)
                        kind = 2;
                    else if (spec != null && spec.StartsWith("lucide:"))
                        kind = 1;
                    texts.Add(sb.Element.Title);
                    specs.Add(spec);
                    kinds.Add(kind);
                }
                Size card = shown.Count > 0 ? ElementButton.ComputeCardSize(texts, kinds, Font) : new Size(150, 54);
                for (int i = 0; i < shown.Count; i++)
                {
                    IModeElement element = shown[i].Element;
                    ElementButton b = new ElementButton();
                    b.Font = Font;
                    b.Text = texts[i];
                    b.AutoCheck = false;
                    if (kinds[i] == 2)
                        b.Picture = Ares.CommonGUI.ButtonImageStore.LoadImage(pl.m_CurrentProjectPath, specs[i]);
                    else if (kinds[i] == 1)
                        b.IconName = specs[i].Substring(7);
                    if (kinds[i] == 2 && specs[i].IndexOf("emoji-", StringComparison.OrdinalIgnoreCase) >= 0)
                        b.PictureContain = true;
                    b.TintColor = Ares.CommonGUI.ButtonImageStore.GetColor(pl.m_CurrentProjectPath, shown[i].OwnerTitle, element.Title);
                    b.Kind = Player.KindsOf(element.StartElement, 0);
                    b.SetBounds(0, 0, card.Width, card.Height);
                    b.Margin = new Padding(4);
                    int id = element.Id;
                    b.Click += (s, e) => PlayButton(id);
                    m_Ids[b] = id;
                    m_Flow.Controls.Add(b);
                }
                if (shown.Count == 0)
                {
                    Label empty = new Label();
                    empty.Text = StringResources.NoElements;
                    empty.AutoSize = true;
                    empty.ForeColor = ForeColor;
                    empty.Margin = new Padding(8);
                    m_Flow.Controls.Add(empty);
                }
                m_Flow.ResumeLayout();

                // size: a few columns, at most about 70% of the height of the screen
                Rectangle area = Screen.FromControl(pl).WorkingArea;
                int n = Math.Max(1, shown.Count);
                int cols = n <= 3 ? n : (n <= 8 ? 4 : (n <= 15 ? 5 : 6));
                int rows = (n + cols - 1) / cols;
                int cellW = card.Width + 8, cellH = card.Height + 8;
                int contentH = rows * cellH + m_Flow.Padding.Vertical;
                int maxH = (int)(area.Height * 0.7) - m_Header.Height;
                bool scroll = contentH > maxH;
                int width = Math.Max(cols * cellW + m_Flow.Padding.Horizontal + (scroll ? SystemInformation.VerticalScrollBarWidth : 0) + 2, 300);
                ClientSize = new Size(width, m_Header.Height + Math.Min(contentH, maxH) + 2);
                LayoutHeader();
                if (!IsHandleCreated || !Visible)
                    Location = StartLocation();
                else
                    KeepOnScreen();
                RefreshState();
            }

            private Point StartLocation()
            {
                string saved = Ares.CommonGUI.Preferences.Get("PadPos", "");
                string[] parts = saved.Split(',');
                int x, y;
                if (parts.Length == 2 && int.TryParse(parts[0], out x) && int.TryParse(parts[1], out y))
                {
                    Rectangle probe = new Rectangle(x, y, Width, Height);
                    foreach (Screen screen in Screen.AllScreens)
                    {
                        Rectangle visible = Rectangle.Intersect(screen.WorkingArea, probe);
                        if (visible.Width >= 80 && visible.Height >= 40)
                            return new Point(x, y);
                    }
                }
                Rectangle area = Screen.FromControl(m_Player).WorkingArea;
                return new Point(area.Left + (area.Width - Width) / 2, area.Top + (area.Height - Height) / 3);
            }

            private void KeepOnScreen()
            {
                Rectangle area = Screen.FromControl(this).WorkingArea;
                int x = Math.Max(area.Left, Math.Min(Left, area.Right - Width));
                int y = Math.Max(area.Top, Math.Min(Top, area.Bottom - Height));
                Location = new Point(x, y);
            }

            private void PlayButton(int id)
            {
                if (m_Player.m_PlayingControl != null)
                    m_Player.m_PlayingControl.SwitchElement(id);
                bool closeAfter = Ares.CommonGUI.Preferences.GetBool("PadCloseOnClick", true) && !m_Pinned;
                if (closeAfter)
                    BeginInvoke((MethodInvoker)Close);
                else
                    RefreshState();
            }

            // the buttons of what is playing are lit; the pad closes if the project was closed
            private void RefreshState()
            {
                if (m_Player.m_Project == null)
                {
                    Close();
                    return;
                }
                if (m_Player.m_PlayingControl == null)
                    return;
                bool paused = m_Player.m_PlayingControl.IsPaused();
                if (m_PauseIcon != null && paused != m_PauseIconIsPlay)
                {
                    m_PauseIconIsPlay = paused;
                    m_PauseIcon.IconName = paused ? "play" : "pause";
                }
                IList<IModeElement> playing = m_Player.m_PlayingControl.CurrentModeElements;
                foreach (KeyValuePair<ElementButton, int> pair in m_Ids)
                {
                    IModeElement current = m_Player.FindTrackModeElement(pair.Value);
                    bool on = current != null && playing.Contains(current);
                    if (pair.Key.Checked != on)
                        pair.Key.Checked = on;
                }
            }
        }
    }
}
