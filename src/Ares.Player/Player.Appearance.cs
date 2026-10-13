/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 Looks of the buttons and folders:
   - emoji for a button (picker with search) and "automatic icon": the emoji is guessed from the title of the button;
     the same for all buttons of the project at once (Extras menu), with a preview;
   - highlight color of a button or of a folder.
*/
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Ares.Data;

namespace Ares.Player
{
    public partial class Player
    {
        private void InitAppearanceMenu()
        {
            ToolStripMenuItem item = new ToolStripMenuItem(T("Ícones automáticos nos botões...", "Automatic icons for the buttons..."));
            item.Click += (s, e) => ShowAutoIcons();
            extrasToolStripMenuItem.DropDownItems.Add(item);
        }

        private void ShowAutoIcons()
        {
            if (m_Project == null || string.IsNullOrEmpty(m_CurrentProjectPath))
            {
                MessageBox.Show(this, T("Abra um projeto primeiro.", "Open a project first."), StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (AutoIconDialog dlg = new AutoIconDialog(this))
                dlg.ShowDialog(this);
            UpdateElementsPanel();
        }

        private void ChooseButtonEmoji()
        {
            IMode mode; int index;
            IModeElement me = MenuElement(out mode, out index);
            if (me == null || string.IsNullOrEmpty(m_CurrentProjectPath))
                return;
            EmojiEntry entry = EmojiPickerDialog.Pick(this, "");
            if (entry == null)
                return;
            ApplyEmoji(mode.Title, me.Title, entry);
            UpdateElementsPanel();
        }

        private void AutoIconForButton()
        {
            IMode mode; int index;
            IModeElement me = MenuElement(out mode, out index);
            if (me == null || string.IsNullOrEmpty(m_CurrentProjectPath))
                return;
            EmojiEntry entry = EmojiCatalog.Match(me.Title);
            if (entry == null)
            {
                MessageBox.Show(this, T("Não encontrei um emoji para o nome \"" + me.Title + "\". Escolha um na lista.",
                    "No emoji fits the name \"" + me.Title + "\". Choose one from the list."), StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Information);
                entry = EmojiPickerDialog.Pick(this, "");
                if (entry == null)
                    return;
            }
            ApplyEmoji(mode.Title, me.Title, entry);
            UpdateElementsPanel();
        }

        private bool ApplyEmoji(string modeTitle, string elementTitle, EmojiEntry entry)
        {
            Cursor old = Cursor;
            Cursor = Cursors.WaitCursor;
            try
            {
                string file = EmojiCatalog.GetPicture(entry.Code);
                if (file == null)
                {
                    MessageBox.Show(this, T("Não consegui baixar a imagem do emoji. Confira a internet e tente de novo.",
                        "The emoji picture could not be downloaded. Check the internet and try again."), StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }
                string spec = Ares.CommonGUI.ButtonImageStore.ImportEmoji(m_CurrentProjectPath, file, entry.Code);
                Ares.CommonGUI.ButtonImageStore.Set(m_CurrentProjectPath, modeTitle, elementTitle, spec);
                return true;
            }
            finally
            {
                Cursor = old;
            }
        }

        private void ChooseButtonColor()
        {
            IMode mode; int index;
            IModeElement me = MenuElement(out mode, out index);
            if (me == null || string.IsNullOrEmpty(m_CurrentProjectPath))
                return;
            Color color;
            if (!ColorChoiceDialog.Pick(this, T("Cor do botão \"" + me.Title + "\"", "Color of the button \"" + me.Title + "\""),
                    Ares.CommonGUI.ButtonImageStore.GetColor(m_CurrentProjectPath, mode.Title, me.Title), out color))
                return;
            Ares.CommonGUI.ButtonImageStore.SetColor(m_CurrentProjectPath, mode.Title, me.Title, color);
            UpdateElementsPanel();
        }

        private void ChooseModeColor()
        {
            IMode mode = CurrentMode();
            if (mode == null || string.IsNullOrEmpty(m_CurrentProjectPath))
                return;
            Color color;
            if (!ColorChoiceDialog.Pick(this, T("Cor da pasta \"" + mode.Title + "\"", "Color of the folder \"" + mode.Title + "\""),
                    Ares.CommonGUI.ButtonImageStore.GetColor(m_CurrentProjectPath, mode.Title, null), out color))
                return;
            Ares.CommonGUI.ButtonImageStore.SetColor(m_CurrentProjectPath, mode.Title, null, color);
            modesList.Invalidate();
        }

        // ---- choice of a color ----

        private class ColorChoiceDialog : Form
        {
            private static readonly string[] s_Names =
            {
                "Vermelho", "Laranja", "Amarelo", "Verde", "Verde-água", "Azul", "Índigo", "Roxo", "Rosa", "Marrom", "Cinza", "Dourado"
            };
            private static readonly string[] s_NamesEn =
            {
                "Red", "Orange", "Yellow", "Green", "Teal", "Blue", "Indigo", "Purple", "Pink", "Brown", "Gray", "Gold"
            };
            private static readonly Color[] s_Colors =
            {
                Color.FromArgb(0xC0, 0x39, 0x2B), Color.FromArgb(0xE6, 0x7E, 0x22), Color.FromArgb(0xF1, 0xC4, 0x0F), Color.FromArgb(0x27, 0xAE, 0x60),
                Color.FromArgb(0x16, 0xA0, 0x85), Color.FromArgb(0x29, 0x80, 0xB9), Color.FromArgb(0x5B, 0x5B, 0xD6), Color.FromArgb(0x8E, 0x44, 0xAD),
                Color.FromArgb(0xE8, 0x43, 0x93), Color.FromArgb(0x8D, 0x6E, 0x63), Color.FromArgb(0x95, 0xA5, 0xA6), Color.FromArgb(0xB8, 0x86, 0x0B)
            };

            private Color m_Result = Color.Empty;

            public static bool Pick(IWin32Window owner, string title, Color current, out Color color)
            {
                using (ColorChoiceDialog dlg = new ColorChoiceDialog(title, current))
                {
                    bool ok = dlg.ShowDialog(owner) == DialogResult.OK;
                    color = ok ? dlg.m_Result : Color.Empty;
                    return ok;
                }
            }

            private ColorChoiceDialog(string title, Color current)
            {
                Text = title;
                AutoScaleMode = AutoScaleMode.Font;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;
                ShowInTaskbar = false;
                StartPosition = FormStartPosition.CenterParent;
                ClientSize = new Size(380, 232);

                Label hint = new Label();
                hint.SetBounds(12, 10, 356, 36);
                hint.Text = Player.T("Escolha a cor de destaque. Ela tinge o fundo e a borda, e ajuda a achar o botão ou a pasta.",
                    "Choose the highlight color. It tints the background and the border, and helps to find the button or folder.");
                Controls.Add(hint);

                bool pt = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt";
                ToolTip tip = new ToolTip();
                for (int i = 0; i < s_Colors.Length; i++)
                {
                    Color c = s_Colors[i];
                    // drawn by the control itself: the theme repaints real buttons and would hide the color
                    Swatch b = new Swatch(c, !current.IsEmpty && current.ToArgb() == c.ToArgb());
                    b.SetBounds(12 + (i % 6) * 60, 54 + (i / 6) * 52, 54, 46);
                    tip.SetToolTip(b, pt ? s_Names[i] : s_NamesEn[i]);
                    b.Click += (s, e) =>
                    {
                        m_Result = c;
                        DialogResult = DialogResult.OK;
                        Close();
                    };
                    Controls.Add(b);
                }

                Button other = new Button();
                other.SetBounds(12, 164, 130, 28);
                other.Text = Player.T("Outra cor...", "Other color...");
                other.Click += (s, e) =>
                {
                    using (ColorDialog cd = new ColorDialog())
                    {
                        cd.FullOpen = true;
                        if (!current.IsEmpty)
                            cd.Color = current;
                        if (cd.ShowDialog(this) == DialogResult.OK)
                        {
                            m_Result = Color.FromArgb(255, cd.Color.R, cd.Color.G, cd.Color.B);
                            DialogResult = DialogResult.OK;
                            Close();
                        }
                    }
                };
                Controls.Add(other);

                Button none = new Button();
                none.SetBounds(150, 164, 130, 28);
                none.Text = Player.T("Sem cor", "No color");
                none.Click += (s, e) =>
                {
                    m_Result = Color.Empty;
                    DialogResult = DialogResult.OK;
                    Close();
                };
                Controls.Add(none);

                Button cancel = new Button();
                cancel.SetBounds(288, 164, 80, 28);
                cancel.Text = Player.T("Cancelar", "Cancel");
                cancel.DialogResult = DialogResult.Cancel;
                Controls.Add(cancel);
                CancelButton = cancel;
            }
        }

        private class Swatch : Control
        {
            private readonly Color m_Color;
            private readonly bool m_Current;
            private bool m_Hover;

            public Swatch(Color color, bool current)
            {
                m_Color = color;
                m_Current = current;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                    | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
                Cursor = Cursors.Hand;
                TabStop = true;
            }

            protected override void OnMouseEnter(EventArgs e)
            {
                m_Hover = true;
                Invalidate();
                base.OnMouseEnter(e);
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                m_Hover = false;
                Invalidate();
                base.OnMouseLeave(e);
            }

            protected override bool IsInputKey(System.Windows.Forms.Keys keyData)
            {
                return keyData == System.Windows.Forms.Keys.Enter || keyData == System.Windows.Forms.Keys.Space || base.IsInputKey(keyData);
            }

            protected override void OnKeyDown(KeyEventArgs e)
            {
                if (e.KeyCode == System.Windows.Forms.Keys.Enter || e.KeyCode == System.Windows.Forms.Keys.Space)
                {
                    OnClick(EventArgs.Empty);
                    e.Handled = true;
                }
                base.OnKeyDown(e);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                Color back = Parent != null ? Parent.BackColor : SystemColors.Control;
                g.Clear(back);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle r = new Rectangle(2, 2, Width - 5, Height - 5);
                using (GraphicsPath path = new GraphicsPath())
                {
                    int d = 12;
                    path.AddArc(r.X, r.Y, d, d, 180, 90);
                    path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                    path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                    path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                    path.CloseFigure();
                    using (SolidBrush b = new SolidBrush(m_Color))
                        g.FillPath(b, path);
                    float w = m_Current ? 3f : (m_Hover || Focused ? 2.5f : 1f);
                    Color line = m_Current || m_Hover || Focused ? Color.White : Color.FromArgb(120, 120, 120);
                    using (Pen pen = new Pen(line, w))
                        g.DrawPath(pen, path);
                }
            }
        }

        // ---- choice of an emoji ----

        private class EmojiPickerDialog : Form
        {
            private readonly TextBox m_Search = new TextBox();
            private readonly FlowLayoutPanel m_Panel = new FlowLayoutPanel();
            private readonly ToolTip m_Tip = new ToolTip();
            private readonly Dictionary<string, Bitmap> m_Loaded = new Dictionary<string, Bitmap>();
            private volatile bool m_Closed;
            private EmojiEntry m_Chosen;

            public static EmojiEntry Pick(IWin32Window owner, string query)
            {
                using (EmojiPickerDialog dlg = new EmojiPickerDialog(query))
                    return dlg.ShowDialog(owner) == DialogResult.OK ? dlg.m_Chosen : null;
            }

            private EmojiPickerDialog(string query)
            {
                Text = Player.T("Escolher emoji", "Choose emoji");
                AutoScaleMode = AutoScaleMode.Font;
                ClientSize = new Size(620, 460);
                MinimumSize = new Size(400, 300);
                StartPosition = FormStartPosition.CenterParent;
                ShowInTaskbar = false;
                MaximizeBox = false;
                MinimizeBox = false;

                m_Search.Dock = DockStyle.Top;
                m_Search.Text = query;
                m_Search.TextChanged += (s, e) => Fill();
                Label hint = new Label();
                hint.Dock = DockStyle.Top;
                hint.AutoSize = false;
                hint.Height = 24;
                hint.TextAlign = ContentAlignment.MiddleLeft;
                hint.Text = Player.T("Digite para filtrar (floresta, dragão, taverna, fogo...). Com 3 letras ou mais, busca em todos os emojis. Os desenhos são baixados na primeira vez.",
                    "Type to filter (forest, dragon, tavern, fire...). With 3 letters or more it searches all emojis. The pictures are downloaded the first time.");
                m_Panel.Dock = DockStyle.Fill;
                m_Panel.AutoScroll = true;
                m_Panel.Padding = new Padding(6);
                Controls.Add(m_Panel);
                Controls.Add(hint);
                Controls.Add(m_Search);
                Shown += (s, e) =>
                {
                    Fill();
                    // the big dictionary (all emojis, in Portuguese and English) is loaded in the background
                    EmojiCatalog.PrepareExtendedAsync(() =>
                    {
                        try
                        {
                            BeginInvoke(new Action(() =>
                            {
                                if (!m_Closed && m_Search.Text.Trim().Length >= 3)
                                    Fill();
                            }));
                        }
                        catch (InvalidOperationException)
                        {
                        }
                    });
                };
                FormClosing += (s, e) => m_Closed = true;
                FormClosed += (s, e) =>
                {
                    foreach (Bitmap b in m_Loaded.Values)
                        b.Dispose();
                    m_Loaded.Clear();
                };
            }

            private static Bitmap Small(string file, int size)
            {
                using (FileStream fs = File.OpenRead(file))
                using (Image img = Image.FromStream(fs))
                {
                    Bitmap bmp = new Bitmap(size, size);
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.DrawImage(img, 0, 0, size, size);
                    }
                    return bmp;
                }
            }

            private void Fill()
            {
                m_Panel.SuspendLayout();
                foreach (Control c in new List<Control>(m_Panel.Controls.Cast<Control>()))
                {
                    m_Panel.Controls.Remove(c);
                    c.Dispose();
                }
                List<EmojiEntry> found = EmojiCatalog.Search(m_Search.Text);
                List<KeyValuePair<EmojiEntry, Button>> pending = new List<KeyValuePair<EmojiEntry, Button>>();
                foreach (EmojiEntry entry in found)
                {
                    EmojiEntry en = entry;
                    Button b = new Button();
                    b.Size = new Size(58, 58);
                    b.Margin = new Padding(3);
                    b.ImageAlign = ContentAlignment.MiddleCenter;
                    Bitmap have;
                    if (m_Loaded.TryGetValue(en.Code, out have))
                        b.Image = have;
                    else
                        pending.Add(new KeyValuePair<EmojiEntry, Button>(en, b));
                    m_Tip.SetToolTip(b, en.Label + " (" + string.Join(", ", en.Words.Skip(1).Take(3).ToArray()) + ")");
                    b.Click += (s, e) =>
                    {
                        m_Chosen = en;
                        DialogResult = DialogResult.OK;
                        Close();
                    };
                    m_Panel.Controls.Add(b);
                }
                m_Panel.ResumeLayout();
                if (pending.Count == 0)
                    return;
                string current = m_Search.Text;
                System.Threading.Tasks.Task.Factory.StartNew(() =>
                {
                    foreach (KeyValuePair<EmojiEntry, Button> p in pending)
                    {
                        if (m_Closed || m_Search.IsDisposed)
                            return;
                        Bitmap bmp = null;
                        try
                        {
                            string file = EmojiCatalog.GetPicture(p.Key.Code);
                            if (file != null)
                                bmp = Small(file, 40);
                        }
                        catch (Exception)
                        {
                        }
                        if (bmp == null)
                            continue;
                        Bitmap loaded = bmp;
                        Button button = p.Value;
                        string code = p.Key.Code;
                        try
                        {
                            BeginInvoke(new Action(() =>
                            {
                                if (m_Closed || m_Loaded.ContainsKey(code))
                                {
                                    loaded.Dispose();
                                    return;
                                }
                                m_Loaded[code] = loaded;
                                if (!button.IsDisposed)
                                    button.Image = loaded;
                            }));
                        }
                        catch (InvalidOperationException)
                        {
                            loaded.Dispose();
                            return;
                        }
                    }
                });
            }
        }

        // ---- automatic icons for all buttons of the project ----

        private class AutoIconDialog : Form
        {
            private readonly Player m_Owner;
            private readonly ListView m_List = new ListView();
            private readonly CheckBox m_OnlyEmpty = new CheckBox();
            private readonly Button m_Apply = new Button();
            private readonly Label m_Info = new Label();
            private readonly ProgressBar m_Progress = new ProgressBar();
            private bool m_Busy;

            private class Row
            {
                public string Mode, Element, Existing;
                public EmojiEntry Match;
            }

            public AutoIconDialog(Player owner)
            {
                m_Owner = owner;
                Text = Player.T("Ícones automáticos nos botões", "Automatic icons for the buttons");
                AutoScaleMode = AutoScaleMode.Font;
                ClientSize = new Size(820, 580);
                MinimumSize = new Size(640, 420);
                StartPosition = FormStartPosition.CenterParent;
                ShowInTaskbar = false;
                MinimizeBox = false;

                Label hint = new Label();
                hint.SetBounds(12, 8, 796, 36);
                hint.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                hint.Text = Player.T("O Apolo lê o nome de cada botão (floresta, taverna, dragão, batalha...) e sugere um emoji. Desmarque o que você não quer e clique em \"Aplicar\".",
                    "Apolo reads the name of each button (forest, tavern, dragon, battle...) and suggests an emoji. Untick what you do not want and click \"Apply\".");
                Controls.Add(hint);

                m_OnlyEmpty.SetBounds(12, 48, 796, 22);
                m_OnlyEmpty.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                m_OnlyEmpty.Text = Player.T("Só os botões que ainda não têm imagem nem ícone", "Only buttons that have no picture or icon yet");
                m_OnlyEmpty.Checked = true;
                m_OnlyEmpty.CheckedChanged += (s, e) => Fill();
                Controls.Add(m_OnlyEmpty);

                m_List.View = View.Details;
                m_List.CheckBoxes = true;
                m_List.FullRowSelect = true;
                m_List.GridLines = true;
                m_List.HideSelection = false;
                m_List.Columns.Add(Player.T("Pasta", "Folder"), 220);
                m_List.Columns.Add(Player.T("Botão", "Button"), 260);
                m_List.Columns.Add(Player.T("Emoji sugerido", "Suggested emoji"), 160);
                m_List.Columns.Add(Player.T("Já tem", "Has already"), 110);
                m_List.SetBounds(12, 76, 796, 408);
                m_List.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
                Controls.Add(m_List);

                m_Info.SetBounds(12, 490, 796, 36);
                m_Info.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
                Controls.Add(m_Info);
                m_Progress.SetBounds(12, 530, 796, 6);
                m_Progress.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
                m_Progress.Visible = false;
                Controls.Add(m_Progress);

                m_Apply.SetBounds(12, 542, 160, 30);
                m_Apply.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
                m_Apply.Text = Player.T("Aplicar nos marcados", "Apply to the ticked");
                m_Apply.Click += (s, e) => Apply();
                Controls.Add(m_Apply);
                Button close = new Button();
                close.SetBounds(708, 542, 100, 30);
                close.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
                close.Text = Player.T("Fechar", "Close");
                close.Click += (s, e) => Close();
                Controls.Add(close);

                Fill();
                Shown += (s, e) => EmojiCatalog.PrepareExtendedAsync(() =>
                {
                    try
                    {
                        BeginInvoke(new Action(() =>
                        {
                            if (!m_Busy)
                                Fill();
                        }));
                    }
                    catch (InvalidOperationException)
                    {
                    }
                });
            }

            private void Fill()
            {
                m_List.BeginUpdate();
                m_List.Items.Clear();
                int suggested = 0, total = 0;
                bool onlyEmpty = m_OnlyEmpty.Checked;
                foreach (IMode mode in m_Owner.m_Project.GetModes())
                {
                    foreach (IModeElement me in mode.GetElements())
                    {
                        total++;
                        Row r = new Row();
                        r.Mode = mode.Title;
                        r.Element = me.Title;
                        r.Existing = Ares.CommonGUI.ButtonImageStore.Get(m_Owner.m_CurrentProjectPath, mode.Title, me.Title);
                        r.Match = EmojiCatalog.Match(me.Title);
                        ListViewItem item = new ListViewItem(mode.Title);
                        item.SubItems.Add(me.Title);
                        item.SubItems.Add(r.Match != null ? r.Match.Label : "-");
                        string has = "";
                        if (r.Existing != null)
                            has = r.Existing.StartsWith("lucide:") ? Player.T("ícone", "icon") : Player.T("imagem", "picture");
                        item.SubItems.Add(has);
                        item.Tag = r;
                        item.Checked = r.Match != null && (!onlyEmpty || r.Existing == null);
                        if (item.Checked)
                            suggested++;
                        m_List.Items.Add(item);
                    }
                }
                m_List.EndUpdate();
                m_Info.Text = total + Player.T(" botões; ", " buttons; ") + suggested + Player.T(" com emoji sugerido e marcados.", " with a suggested emoji and ticked.")
                    + " " + Player.T("O que não achou emoji você pode escolher à mão (botão direito no botão > Escolher emoji).",
                        "For the ones without an emoji, choose by hand (right click on the button > Choose emoji).");
            }

            private void Apply()
            {
                if (m_Busy)
                    return;
                List<Row> rows = new List<Row>();
                foreach (ListViewItem item in m_List.Items)
                {
                    Row r = item.Tag as Row;
                    if (item.Checked && r != null && r.Match != null)
                        rows.Add(r);
                }
                if (rows.Count == 0)
                {
                    MessageBox.Show(this, Player.T("Nada marcado.", "Nothing ticked."), StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                m_Busy = true;
                m_Apply.Enabled = false;
                m_Progress.Minimum = 0;
                m_Progress.Maximum = rows.Count;
                m_Progress.Value = 0;
                m_Progress.Visible = true;
                m_Info.Text = Player.T("Baixando os emojis e aplicando...", "Downloading the emojis and applying...");
                string projectPath = m_Owner.m_CurrentProjectPath;
                System.Threading.Tasks.Task.Factory.StartNew(() =>
                {
                    Dictionary<string, string> files = new Dictionary<string, string>();
                    int done = 0;
                    foreach (Row r in rows)
                    {
                        string code = r.Match.Code;
                        if (!files.ContainsKey(code))
                            files[code] = EmojiCatalog.GetPicture(code);
                        int n = ++done;
                        try
                        {
                            BeginInvoke(new Action(() => m_Progress.Value = Math.Min(m_Progress.Maximum, n)));
                        }
                        catch (InvalidOperationException)
                        {
                            return;
                        }
                    }
                    try
                    {
                        BeginInvoke(new Action(() =>
                        {
                            int ok = 0, failed = 0;
                            foreach (Row r in rows)
                            {
                                string file;
                                if (files.TryGetValue(r.Match.Code, out file) && file != null)
                                {
                                    try
                                    {
                                        string spec = Ares.CommonGUI.ButtonImageStore.ImportEmoji(projectPath, file, r.Match.Code);
                                        Ares.CommonGUI.ButtonImageStore.Set(projectPath, r.Mode, r.Element, spec);
                                        ok++;
                                        continue;
                                    }
                                    catch (Exception)
                                    {
                                    }
                                }
                                failed++;
                            }
                            m_Busy = false;
                            m_Apply.Enabled = true;
                            m_Progress.Visible = false;
                            Fill();
                            m_Info.Text = ok + Player.T(" botão(ões) com emoji.", " button(s) with an emoji.")
                                + (failed > 0 ? " " + failed + Player.T(" falharam (internet?).", " failed (internet?).") : "");
                        }));
                    }
                    catch (InvalidOperationException)
                    {
                    }
                });
            }
        }
    }
}
