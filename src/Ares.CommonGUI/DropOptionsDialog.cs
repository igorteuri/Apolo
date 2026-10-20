using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Ares.CommonGUI
{
    /// <summary>How the files of a folder are put together in one element.</summary>
    public enum PlaylistKind
    {
        /// <summary>Music: random order. Sounds: one random sound.</summary>
        Random = 0,
        /// <summary>Music and sounds: in the order of the files.</summary>
        Sequential = 1,
        /// <summary>Sounds only: all files at the same time.</summary>
        Parallel = 2
    }

    /// <summary>Choices made when folders / files are dropped into Apolo (same options as the Editor offers for new elements).</summary>
    public class DropOptions
    {
        /// <summary>true = sound effects ("Sounds"), false = music.</summary>
        public bool Sounds;
        /// <summary>true = all files of a folder in one element (playlist / random sound), false = one element (button) per file.</summary>
        public bool OneElement;
        /// <summary>Kind of playlist when OneElement is set.</summary>
        public PlaylistKind Kind = PlaylistKind.Random;
        /// <summary>Music lists: repeat forever.</summary>
        public bool Repeat = true;
        /// <summary>Volume of the files, in percent.</summary>
        public int Volume = 100;
        /// <summary>Fade in / fade out of each file, in milliseconds.</summary>
        public int FadeInMs;
        public int FadeOutMs;
        /// <summary>Music: smooth transition from one track to the next.</summary>
        public bool CrossFade;
        /// <summary>Where the dropped audio goes (only asked when the drop has more than one sensible answer).</summary>
        public DropLayout Layout = DropLayout.Tree;
    }

    public class DropOptionsDialog : Form
    {
        // remembered for the next drop
        private static DropOptions s_Last = new DropOptions();

        private RadioButton m_Music, m_Sounds, m_Each, m_One;
        private RadioButton m_KindRandom, m_KindSequential, m_KindParallel;
        private CheckBox m_Repeat, m_CrossFade;
        private NumericUpDown m_Volume, m_FadeIn, m_FadeOut;
        private GroupBox m_KindBox, m_EffectBox, m_StructBox;
        private readonly DropScan m_Scan;
        private readonly string m_Destination;
        private readonly List<KeyValuePair<RadioButton, DropLayout>> m_LayoutChoices = new List<KeyValuePair<RadioButton, DropLayout>>();

        public static bool IsPt
        {
            get { return System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt"; }
        }

        /// <summary>Asks how a number of folders / files are added (no information on where they go).</summary>
        public static DropOptions Ask(IWin32Window owner, int folders, int files)
        {
            return Run(owner, new DropOptionsDialog(folders, files, null, null));
        }

        /// <summary>
        /// Looks at what was dropped and asks only what is open: where it goes (when there is more than one sensible way)
        /// and whether files become one button each or one playlist per folder.
        /// </summary>
        /// <param name="destination">Title of the folder which receives the drop (the one under the mouse / the open one); null = none.</param>
        public static DropOptions Ask(IWin32Window owner, DropScan scan, string destination)
        {
            return Run(owner, new DropOptionsDialog(scan.AudioFolderCount, scan.FileCount, scan, destination));
        }

        private static DropOptions Run(IWin32Window owner, DropOptionsDialog dlg)
        {
            using (dlg)
            {
                if (dlg.ShowDialog(owner) != DialogResult.OK)
                    return null;
                DropLayout layout = dlg.SelectedLayout();
                var result = new DropOptions
                {
                    Sounds = dlg.m_Sounds.Checked,
                    // "one button per dropped folder" only makes sense as a playlist
                    OneElement = dlg.m_One.Checked || layout == DropLayout.Merge,
                    Kind = dlg.m_KindParallel.Checked && dlg.m_Sounds.Checked ? PlaylistKind.Parallel
                        : (dlg.m_KindSequential.Checked ? PlaylistKind.Sequential : PlaylistKind.Random),
                    Repeat = dlg.m_Repeat.Checked,
                    Volume = (int)dlg.m_Volume.Value,
                    FadeInMs = (int)(dlg.m_FadeIn.Value * 1000),
                    FadeOutMs = (int)(dlg.m_FadeOut.Value * 1000),
                    CrossFade = dlg.m_CrossFade.Checked,
                    Layout = layout
                };
                s_Last = new DropOptions
                {
                    Sounds = result.Sounds,
                    OneElement = dlg.m_One.Checked,
                    Kind = result.Kind,
                    Repeat = result.Repeat,
                    Volume = result.Volume,
                    FadeInMs = result.FadeInMs,
                    FadeOutMs = result.FadeOutMs,
                    CrossFade = result.CrossFade
                };
                return result;
            }
        }

        /// <summary>The next drop starts with "sound effects, one button per file" (used by the quick effects board).</summary>
        public static void PreferEffects()
        {
            s_Last = new DropOptions { Sounds = true, OneElement = false, Kind = PlaylistKind.Random, Repeat = false };
        }

        /// <summary>Big choice button (looks like the playlist buttons of the Player): glyph, name and a short hint.</summary>
        private class KindCard : RadioButton
        {
            public string Glyph = "";
            public string Title = "";
            public string Hint = "";
            private bool m_Hover;

            public KindCard()
            {
                Appearance = Appearance.Button;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                    | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
                float scale = Math.Max(80, ThemeManager.ScalePercent) / 110f;
                Size = new Size((int)Math.Round(190 * scale), (int)Math.Round(60 * scale));
                Margin = new Padding(0, 0, 8, 0);
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

            protected override void OnCheckedChanged(EventArgs e)
            {
                Invalidate();
                base.OnCheckedChanged(e);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                ThemePalette p = ThemeManager.Current;
                bool themed = p != null && !p.IsClassic;
                Color window = themed ? p.Window : SystemColors.Control;
                Color surface = themed ? p.Surface : SystemColors.ControlLight;
                Color hover = themed ? p.Hover : SystemColors.ControlLightLight;
                Color selection = themed ? p.Selection : SystemColors.Highlight;
                Color accent = themed ? p.Accent : SystemColors.Highlight;
                Color border = themed ? p.Border : SystemColors.ControlDark;
                Color text = themed ? p.Text : SystemColors.ControlText;
                Color textOnBack = Checked && !themed ? SystemColors.HighlightText : text;

                Graphics g = e.Graphics;
                g.Clear(Parent != null ? ThemeManager.EffectiveBack(this) : window);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                float scale = Math.Max(80, ThemeManager.ScalePercent) / 110f;
                int radius = (int)Math.Round(8 * scale);
                Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
                using (System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath())
                {
                    int d = radius * 2;
                    path.AddArc(r.X, r.Y, d, d, 180, 90);
                    path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                    path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                    path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                    path.CloseFigure();
                    using (SolidBrush b = new SolidBrush(Checked ? selection : (m_Hover ? hover : surface)))
                        g.FillPath(b, path);
                    using (Pen pen = new Pen(Checked || m_Hover ? accent : border, Checked ? 2f : 1f))
                        g.DrawPath(pen, path);
                }

                if (Focused && ShowFocusCues)
                {
                    using (Pen fp = new Pen(text, 2f))
                        g.DrawRectangle(fp, 4, 4, Width - 9, Height - 9);
                }
                int pad = (int)Math.Round(10 * scale);
                int glyphWidth = (int)Math.Round(30 * scale);
                using (Font gf = new Font("Segoe UI Symbol", 16f * scale, FontStyle.Regular))
                    TextRenderer.DrawText(g, Glyph, gf, new Rectangle(pad, 0, glyphWidth, Height), Checked && !themed ? textOnBack : accent,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                int textLeft = pad + glyphWidth + 4;
                using (Font tf = new Font(Font, FontStyle.Bold))
                    TextRenderer.DrawText(g, Title, tf, new Rectangle(textLeft, Height / 2 - tf.Height - 1, Width - textLeft - pad, tf.Height),
                        textOnBack, TextFormatFlags.Left | TextFormatFlags.Bottom | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
                using (Font hf = new Font(Font.FontFamily, Math.Max(7f, Font.Size - 1.5f), FontStyle.Regular))
                    TextRenderer.DrawText(g, Hint, hf, new Rectangle(textLeft, Height / 2 + 1, Width - textLeft - pad, hf.Height),
                        Color.FromArgb(200, textOnBack), TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            }
        }

        private static string Tx(string pt, string en)
        {
            return IsPt ? pt : en;
        }

        private static string Q(string name)
        {
            return "“" + name + "”";
        }

        private DropLayout SelectedLayout()
        {
            foreach (KeyValuePair<RadioButton, DropLayout> choice in m_LayoutChoices)
            {
                if (choice.Key.Checked)
                    return choice.Value;
            }
            if (m_Scan != null)
                return m_Scan.DefaultLayout(m_Destination != null);
            return DropLayout.Tree;
        }

        // ---- the sentences which tell what was found / what each choice does ----

        private static string Summary(DropScan scan)
        {
            bool pt = IsPt;
            int files = scan.FileCount;
            switch (scan.Shape)
            {
                case DropShape.OneFile:
                    return (pt ? "1 arquivo de áudio: " : "1 audio file: ") + Path.GetFileName(scan.LooseFiles[0]);
                case DropShape.Files:
                    return pt ? files + " arquivos de áudio." : files + " audio files.";
                case DropShape.Folder:
                    return pt ? "Pasta " + Q(scan.Folders[0].Name) + ": " + files + " música(s)."
                              : "Folder " + Q(scan.Folders[0].Name) + ": " + files + " track(s).";
                case DropShape.NestedFolder:
                    int subs = scan.Folders[0].Subfolders.Count;
                    return pt ? "A pasta " + Q(scan.Folders[0].Name) + " tem " + subs + " subpasta(s), com " + files + " música(s) no total."
                              : "The folder " + Q(scan.Folders[0].Name) + " has " + subs + " subfolder(s), with " + files + " track(s) in all.";
                default:
                    int loose = scan.LooseFiles.Count;
                    return pt ? scan.Folders.Count + " pasta(s)" + (loose > 0 ? " e " + loose + " arquivo(s) solto(s)" : "") + ": " + files + " música(s) no total."
                              : scan.Folders.Count + " folder(s)" + (loose > 0 ? " and " + loose + " loose file(s)" : "") + ": " + files + " track(s) in all.";
            }
        }

        private static string DestinationText(string destination)
        {
            if (destination == null)
                return Tx("Vai para pastas novas do projeto.", "It goes into new folders of the project.");
            return Tx("Destino: pasta ", "Destination: folder ") + Q(destination);
        }

        private static string LayoutText(DropScan scan, DropLayout layout, string destination)
        {
            bool pt = IsPt;
            string single = scan.Folders.Count == 1 ? Q(scan.Folders[0].Name) : null;
            switch (layout)
            {
                case DropLayout.Here:
                    if (!scan.HasSubfolders)
                        return pt ? "Colocar na pasta " + Q(destination) : "Put it in the folder " + Q(destination);
                    AudioFolder first = scan.Folders.First(f => !f.IsLeaf);
                    string example = Q(first.Name + " - " + first.Subfolders[0].Name);
                    return pt ? "Tudo na pasta " + Q(destination) + ": um botão por pasta de músicas (ex.: " + example + ")"
                              : "Everything in the folder " + Q(destination) + ": one button per folder of music (e.g. " + example + ")";
                case DropLayout.Tree:
                    if (scan.Folders.Count == 0)
                        return pt ? "Criar a pasta " + Q("Músicas soltas") : "Create the folder " + Q("Loose music");
                    if (!scan.HasSubfolders)
                        return single != null
                            ? (pt ? "Criar uma pasta nova " + single : "Create a new folder " + single)
                            : (pt ? "Criar uma pasta nova para cada pasta arrastada" : "Create a new folder for each dropped folder");
                    return pt ? "Criar as mesmas pastas do computador: cada subpasta vira uma pasta, com seus botões"
                              : "Create the same folders as on the computer: each subfolder becomes a folder with its buttons";
                case DropLayout.Compact:
                    return single != null
                        ? (pt ? "Criar a pasta " + single + " com um botão para cada subpasta" : "Create the folder " + single + " with one button per subfolder")
                        : (pt ? "Criar uma pasta para cada pasta arrastada, com um botão para cada subpasta"
                              : "Create a folder for each dropped folder, with one button per subfolder");
                default:
                    return pt ? "Na pasta " + Q(destination) + ": um botão só por pasta arrastada, com todas as músicas de dentro"
                              : "In the folder " + Q(destination) + ": one button per dropped folder, with all the music inside";
            }
        }

        private DropOptionsDialog(int groups, int files, DropScan scan, string destination)
        {
            m_Scan = scan;
            m_Destination = destination;
            bool pt = IsPt;
            Text = pt ? "Adicionar ao Apolo" : "Add to Apolo";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(12);

            var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Fill };
            Controls.Add(layout);

            var info = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(480, 0),
                Text = scan != null ? Summary(scan)
                    : (pt ? (files + " arquivo(s) de áudio em " + groups + " grupo(s).")
                          : (files + " audio file(s) in " + groups + " group(s).")),
                Margin = new Padding(0, 0, 0, scan != null ? 2 : 8)
            };
            layout.Controls.Add(info);
            if (scan != null)
            {
                layout.Controls.Add(new Label
                {
                    AutoSize = true,
                    MaximumSize = new Size(480, 0),
                    Text = DestinationText(destination),
                    Margin = new Padding(0, 0, 0, 8)
                });
            }

            // first question: is it music or a sound effect? (two big buttons, like the playlist buttons)
            layout.Controls.Add(new Label
            {
                AutoSize = true,
                Text = pt ? "O que são esses arquivos?" : "What are these files?",
                Margin = new Padding(0, 0, 0, 4)
            });
            var typeFlow = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, Dock = DockStyle.Fill, WrapContents = false, Margin = new Padding(0, 0, 0, 8) };
            m_Music = new KindCard
            {
                Glyph = "♫",
                Title = pt ? "Música" : "Music",
                Hint = pt ? "toca em segundo plano" : "plays in the background",
                Checked = !s_Last.Sounds
            };
            m_Sounds = new KindCard
            {
                Glyph = "⚡",
                Title = pt ? "Efeito sonoro" : "Sound effect",
                Hint = pt ? "toca junto com a música" : "plays over the music",
                Checked = s_Last.Sounds
            };
            typeFlow.Controls.Add(m_Music);
            typeFlow.Controls.Add(m_Sounds);
            layout.Controls.Add(typeFlow);

            // the ways to place it: only when there is more than one sensible answer
            if (scan != null)
            {
                List<DropLayout> layouts = scan.AvailableLayouts(destination != null);
                if (layouts.Count > 1)
                {
                    DropLayout wanted = scan.DefaultLayout(destination != null);
                    var layoutBox = new GroupBox { Text = pt ? "Onde colocar" : "Where to put it", AutoSize = true, Dock = DockStyle.Fill };
                    var layoutFlow = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
                    foreach (DropLayout option in layouts)
                    {
                        var radio = new RadioButton
                        {
                            AutoSize = true,
                            MaximumSize = new Size(450, 0),
                            Checked = option == wanted,
                            Text = LayoutText(scan, option, destination)
                        };
                        layoutFlow.Controls.Add(radio);
                        m_LayoutChoices.Add(new KeyValuePair<RadioButton, DropLayout>(radio, option));
                    }
                    layoutBox.Controls.Add(layoutFlow);
                    layout.Controls.Add(layoutBox);
                }
            }

            bool loosePlaylist = scan != null && scan.Shape == DropShape.Files;
            m_StructBox = new GroupBox { Text = pt ? "Como criar os botões" : "How to make the buttons", AutoSize = true, Dock = DockStyle.Fill };
            var structFlow = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
            m_Each = new RadioButton
            {
                AutoSize = true,
                Checked = !s_Last.OneElement,
                Text = pt ? "Um botão para cada música" : "One button for each file"
            };
            m_One = new RadioButton
            {
                AutoSize = true,
                Checked = s_Last.OneElement,
                Text = loosePlaylist
                    ? (pt ? "Um botão só com todas as músicas (playlist)" : "One button with all the files (playlist)")
                    : (pt ? "Um botão só por pasta de músicas (playlist)" : "One button per folder of music (playlist)")
            };
            structFlow.Controls.Add(m_Each);
            structFlow.Controls.Add(m_One);
            m_StructBox.Controls.Add(structFlow);
            layout.Controls.Add(m_StructBox);

            m_KindBox = new GroupBox { Text = pt ? "Tipo de playlist" : "Playlist kind", AutoSize = true, Dock = DockStyle.Fill, Visible = false };
            var kindFlow = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
            m_KindRandom = new RadioButton { AutoSize = true, Checked = s_Last.Kind == PlaylistKind.Random };
            m_KindSequential = new RadioButton { AutoSize = true, Checked = s_Last.Kind == PlaylistKind.Sequential };
            m_KindParallel = new RadioButton { AutoSize = true, Checked = s_Last.Kind == PlaylistKind.Parallel };
            kindFlow.Controls.Add(m_KindRandom);
            kindFlow.Controls.Add(m_KindSequential);
            kindFlow.Controls.Add(m_KindParallel);
            m_KindBox.Controls.Add(kindFlow);
            layout.Controls.Add(m_KindBox);

            m_EffectBox = new GroupBox { Text = pt ? "Ajustes" : "Settings", AutoSize = true, Dock = DockStyle.Fill, Visible = false };
            var table = new TableLayoutPanel { AutoSize = true, ColumnCount = 3, Dock = DockStyle.Fill };
            m_Volume = new NumericUpDown { Minimum = 0, Maximum = 100, Value = Math.Max(0, Math.Min(100, s_Last.Volume)), Width = 64 };
            m_FadeIn = new NumericUpDown { Minimum = 0, Maximum = 60, DecimalPlaces = 1, Increment = 0.5m, Value = Math.Min(60, s_Last.FadeInMs / 1000m), Width = 64 };
            m_FadeOut = new NumericUpDown { Minimum = 0, Maximum = 60, DecimalPlaces = 1, Increment = 0.5m, Value = Math.Min(60, s_Last.FadeOutMs / 1000m), Width = 64 };
            AddRow(table, 0, "Volume", m_Volume, "%");
            AddRow(table, 1, pt ? "Aparecer aos poucos (fade in)" : "Fade in", m_FadeIn, pt ? "seg." : "sec.");
            AddRow(table, 2, pt ? "Sumir aos poucos (fade out)" : "Fade out", m_FadeOut, pt ? "seg." : "sec.");
            m_Repeat = new CheckBox { AutoSize = true, Checked = s_Last.Repeat, Text = pt ? "Repetir sem parar" : "Repeat forever" };
            m_CrossFade = new CheckBox { AutoSize = true, Checked = s_Last.CrossFade, Text = pt ? "Transição suave entre uma música e a próxima (crossfade)" : "Smooth transition between tracks (crossfade)" };
            table.Controls.Add(m_Repeat, 0, 3);
            table.SetColumnSpan(m_Repeat, 3);
            table.Controls.Add(m_CrossFade, 0, 4);
            table.SetColumnSpan(m_CrossFade, 3);
            m_EffectBox.Controls.Add(table);
            layout.Controls.Add(m_EffectBox);

            var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 0) };
            var cancel = new Button { Text = pt ? "Cancelar" : "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true };
            var more = new Button { Text = (pt ? "Mais ajustes " : "More settings ") + "▾", AutoSize = true };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);
            buttons.Controls.Add(more);
            layout.Controls.Add(buttons);
            AcceptButton = ok;
            CancelButton = cancel;

            more.Click += (s, e) =>
            {
                bool show = !m_KindBox.Visible;
                m_KindBox.Visible = show;
                m_EffectBox.Visible = show;
                more.Text = (pt ? "Mais ajustes " : "More settings ") + (show ? "▴" : "▾");
            };
            EventHandler update = (s, e) => UpdateState();
            m_Music.CheckedChanged += update;
            m_Sounds.CheckedChanged += update;
            m_Each.CheckedChanged += update;
            m_One.CheckedChanged += update;
            foreach (KeyValuePair<RadioButton, DropLayout> choice in m_LayoutChoices)
                choice.Key.CheckedChanged += update;
            UpdateState();
        }

        private static void AddRow(TableLayoutPanel table, int row, string label, Control box, string unit)
        {
            table.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 12, 6) }, 0, row);
            table.Controls.Add(box, 1, row);
            table.Controls.Add(new Label { Text = unit, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(4, 6, 3, 6) }, 2, row);
        }

        private void UpdateState()
        {
            bool sounds = m_Sounds.Checked;
            bool merge = SelectedLayout() == DropLayout.Merge;
            // a single file has nothing to choose; "one button per dropped folder" is always a playlist
            m_StructBox.Visible = !(m_Scan != null && m_Scan.Shape == DropShape.OneFile) && !merge;
            bool one = m_One.Checked || merge;
            m_KindBox.Enabled = one;
            if (sounds)
            {
                m_KindRandom.Text = Tx("Sorteia um som a cada vez", "A random sound each time");
                m_KindSequential.Text = Tx("Um depois do outro, na ordem", "One after the other, in order");
                m_KindParallel.Text = Tx("Todos ao mesmo tempo", "All at the same time");
                m_KindParallel.Visible = true;
            }
            else
            {
                m_KindRandom.Text = Tx("Aleatória (as músicas tocam em ordem sorteada)", "Random (tracks play in random order)");
                m_KindSequential.Text = Tx("Em sequência (na ordem da pasta)", "In sequence (in the order of the folder)");
                m_KindParallel.Visible = false;
                if (m_KindParallel.Checked)
                    m_KindRandom.Checked = true;
            }
            m_Repeat.Enabled = !sounds && one;
            m_CrossFade.Enabled = !sounds;
        }
    }
}
