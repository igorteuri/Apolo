/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 "Propriedades da playlist": the settings which the old Editor offered, inside the Player:
 repetition, delays, general volume, chance / volume / delay of every item, order, add and remove files.
*/
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Ares.Data;

namespace Ares.Player
{
    internal class ItemSetting
    {
        public int Id;               // 0 for a file added in the dialog
        public string Title;
        public bool IsFile;
        public int Chance = 100;
        public int Volume = 100;
        public double DelaySeconds;
        public string NewPath;
    }

    internal class PlaylistSettings
    {
        public string TypeName = "";
        public bool Repeatable;
        public bool Delayable;
        public bool ShowChance;
        public bool ShowItemDelay;
        public bool Reorderable;
        public bool HasItems;
        public int RepeatCount = 1;           // -1: forever
        public double IntervalFixed;
        public double IntervalRandom;
        public double StartFixed;
        public double StartRandom;
        public bool SetsMusicVolume;
        public int MusicVolume = 100;
        public bool SetsSoundVolume;
        public int SoundVolume = 100;
        public SoundFileType FileType = SoundFileType.Music;
        public List<ItemSetting> Items = new List<ItemSetting>();
        public List<int> RemovedIds = new List<int>();
    }

    internal static class PlaylistEditing
    {
        public static PlaylistSettings Read(IElement element)
        {
            PlaylistSettings s = new PlaylistSettings();
            if (element == null)
                return s;
            s.TypeName = TypeName(element);
            IRepeatableElement repeat = element as IRepeatableElement;
            if (repeat != null)
            {
                s.Repeatable = true;
                s.RepeatCount = repeat.RepeatCount;
                s.IntervalFixed = repeat.FixedIntermediateDelay.TotalSeconds;
                s.IntervalRandom = repeat.MaximumRandomIntermediateDelay.TotalSeconds;
            }
            IDelayableElement delay = element as IDelayableElement;
            if (delay != null)
            {
                s.Delayable = true;
                s.StartFixed = delay.FixedStartDelay.TotalSeconds;
                s.StartRandom = delay.MaximumRandomStartDelay.TotalSeconds;
            }
            s.SetsMusicVolume = element.SetsMusicVolume;
            s.MusicVolume = Clamp(element.MusicVolume);
            s.SetsSoundVolume = element.SetsSoundVolume;
            s.SoundVolume = Clamp(element.SoundVolume);
            IGeneralElementContainer container = element as IGeneralElementContainer;
            if (container != null && !(element is IMacro))
            {
                s.HasItems = true;
                s.ShowChance = element is IElementContainer<IChoiceElement>;
                s.ShowItemDelay = element is ISequentialContainer || element is IElementContainer<IParallelElement>;
                s.Reorderable = element is ISequentialContainer;
                bool typeKnown = false;
                foreach (IContainerElement ce in container.GetGeneralElements())
                {
                    IElement inner = ce.InnerElement;
                    ItemSetting item = new ItemSetting { Id = ce.Id };
                    IFileElement file = inner as IFileElement;
                    item.IsFile = file != null;
                    item.Title = file != null ? file.FileName : (inner != null ? inner.Title : "?");
                    if (file != null)
                    {
                        item.Volume = Clamp(file.Effects.Volume);
                        if (!typeKnown)
                        {
                            s.FileType = file.SoundFileType;
                            typeKnown = true;
                        }
                    }
                    IChoiceElement choice = ce as IChoiceElement;
                    if (choice != null)
                        item.Chance = choice.RandomChance;
                    IDelayableElement itemDelay = ce as IDelayableElement;
                    if (itemDelay != null)
                        item.DelaySeconds = itemDelay.FixedStartDelay.TotalSeconds;
                    s.Items.Add(item);
                }
                if (!typeKnown && element is IMusicList)
                    s.FileType = SoundFileType.Music;
                else if (!typeKnown)
                    s.FileType = SoundFileType.SoundEffect;
            }
            return s;
        }

        public static bool HasAnything(PlaylistSettings s)
        {
            return s.Repeatable || s.Delayable || s.HasItems;
        }

        private static int Clamp(int v)
        {
            return Math.Max(0, Math.Min(100, v));
        }

        public static string TypeName(IElement element)
        {
            if (element is IRandomBackgroundMusicList)
                return Loc.T("Lista de músicas aleatória", "Random music list");
            if (element is ISequentialBackgroundMusicList)
                return Loc.T("Lista de músicas sequencial", "Sequential music list");
            if (element is IBackgroundSounds || element is IBackgroundSoundChoice)
                return Loc.T("Sons de fundo", "Background sounds");
            if (element is IMacro)
                return "Macro";
            if (element is IElementContainer<IChoiceElement>)
                return Loc.T("Lista aleatória", "Random list");
            if (element is ISequentialContainer)
                return Loc.T("Lista sequencial", "Sequential list");
            if (element is IElementContainer<IParallelElement>)
                return Loc.T("Lista paralela", "Parallel list");
            if (element is IFileElement)
                return Loc.T("Arquivo de som", "Sound file");
            return "";
        }

        /// <summary>Writes the settings into the element (called on the element of the open project).</summary>
        public static void Apply(IElement element, PlaylistSettings s)
        {
            IRepeatableElement repeat = element as IRepeatableElement;
            if (repeat != null && s.Repeatable)
            {
                repeat.RepeatCount = s.RepeatCount;
                repeat.FixedIntermediateDelay = TimeSpan.FromSeconds(Math.Max(0, s.IntervalFixed));
                repeat.MaximumRandomIntermediateDelay = TimeSpan.FromSeconds(Math.Max(0, s.IntervalRandom));
            }
            IDelayableElement delay = element as IDelayableElement;
            if (delay != null && s.Delayable)
            {
                delay.FixedStartDelay = TimeSpan.FromSeconds(Math.Max(0, s.StartFixed));
                delay.MaximumRandomStartDelay = TimeSpan.FromSeconds(Math.Max(0, s.StartRandom));
            }
            element.SetsMusicVolume = s.SetsMusicVolume;
            element.MusicVolume = s.MusicVolume;
            element.SetsSoundVolume = s.SetsSoundVolume;
            element.SoundVolume = s.SoundVolume;

            IGeneralElementContainer container = element as IGeneralElementContainer;
            if (container == null || !s.HasItems)
                return;
            // removed files
            foreach (int id in s.RemovedIds)
                container.RemoveElement(id);
            // changed values of the files which stay
            Dictionary<int, IContainerElement> byId = new Dictionary<int, IContainerElement>();
            foreach (IContainerElement ce in container.GetGeneralElements())
                byId[ce.Id] = ce;
            foreach (ItemSetting item in s.Items)
            {
                IContainerElement ce;
                if (item.Id == 0 || !byId.TryGetValue(item.Id, out ce))
                    continue;
                IChoiceElement choice = ce as IChoiceElement;
                if (choice != null)
                    choice.RandomChance = Math.Max(0, item.Chance);
                IDelayableElement itemDelay = ce as IDelayableElement;
                if (itemDelay != null && s.ShowItemDelay)
                    itemDelay.FixedStartDelay = TimeSpan.FromSeconds(Math.Max(0, item.DelaySeconds));
                IFileElement file = ce.InnerElement as IFileElement;
                if (file != null)
                    file.Effects.Volume = Clamp(item.Volume);
            }
            // added files
            List<ItemSetting> added = new List<ItemSetting>();
            foreach (ItemSetting item in s.Items)
            {
                if (item.Id != 0 || string.IsNullOrEmpty(item.NewPath))
                    continue;
                IFileElement fe = DataModule.ElementFactory.CreateFileElement(item.NewPath, s.FileType);
                fe.Effects.Volume = Clamp(item.Volume);
                IElement wrapper = container.AddGeneralElement(fe);
                // the new item gets the values from the dialog
                foreach (IContainerElement ce in container.GetGeneralElements())
                {
                    if (ce.InnerElement == fe || (wrapper != null && ce.Id == wrapper.Id))
                    {
                        item.Id = ce.Id;
                        IChoiceElement choice = ce as IChoiceElement;
                        if (choice != null)
                            choice.RandomChance = Math.Max(0, item.Chance);
                        break;
                    }
                }
            }
            // order (only for sequential lists)
            ISequentialContainer sequential = element as ISequentialContainer;
            if (sequential != null)
            {
                List<int> wanted = s.Items.Where(i => i.Id != 0).Select(i => i.Id).ToList();
                for (int target = 0; target < wanted.Count; target++)
                {
                    IList<ISequentialElement> current = sequential.GetElements();
                    int at = -1;
                    for (int i = 0; i < current.Count; i++)
                    {
                        if (current[i].Id == wanted[target])
                        {
                            at = i;
                            break;
                        }
                    }
                    if (at >= 0 && at != target)
                        sequential.MoveElements(at, at, target - at);
                }
            }
        }
    }

    internal class PlaylistPropertiesDialog : Form
    {
        private readonly PlaylistSettings m_S;
        private RadioButton m_Once, m_Forever;
        private NumericUpDown m_Count, m_IntFixed, m_IntRandom, m_StartFixed, m_StartRandom;
        private CheckBox m_SetMusic, m_SetSound;
        private TrackBar m_MusicBar, m_SoundBar;
        private DataGridView m_Grid;
        private Button m_Up, m_Down, m_Remove, m_Add;

        public PlaylistPropertiesDialog(string title, PlaylistSettings settings)
        {
            m_S = settings;
            Text = Loc.T("Propriedades da playlist", "Playlist properties") + " - " + title;
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(700, 640);
            MinimumSize = new Size(620, 560);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            MinimizeBox = false;
            MaximizeBox = true;
            try { Icon = Ares.CommonGUI.ApoloBrand.GetIcon(32, Ares.CommonGUI.ApoloState.Playing, 0); } catch (Exception swallowed) { Ares.Settings.ErrorLog.Note(swallowed); }

            FlowLayoutPanel top = new FlowLayoutPanel();
            top.Dock = DockStyle.Top;
            top.FlowDirection = FlowDirection.TopDown;
            top.WrapContents = false;
            top.AutoSize = true;
            top.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            top.Padding = new Padding(8, 8, 8, 0);

            Label type = new Label();
            type.AutoSize = true;
            type.Font = new Font(Font, FontStyle.Bold);
            type.Text = Loc.T("Tipo: ", "Type: ") + m_S.TypeName;
            top.Controls.Add(type);

            if (m_S.Repeatable)
                top.Controls.Add(BuildRepeatGroup());
            if (m_S.Delayable)
                top.Controls.Add(BuildStartGroup());
            top.Controls.Add(BuildVolumeGroup());

            Panel bottom = new Panel();
            bottom.Dock = DockStyle.Bottom;
            bottom.Height = 44;
            Button ok = new Button();
            ok.Text = "OK";
            ok.DialogResult = DialogResult.OK;
            ok.SetBounds(ClientSize.Width - 190, 8, 85, 28);
            ok.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            ok.Click += (s, e) => Commit();
            Button cancel = new Button();
            cancel.Text = Loc.T("Cancelar", "Cancel");
            cancel.DialogResult = DialogResult.Cancel;
            cancel.SetBounds(ClientSize.Width - 98, 8, 88, 28);
            cancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            bottom.Controls.Add(ok);
            bottom.Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;

            Controls.Add(bottom);
            if (m_S.HasItems)
                Controls.Add(BuildItemsPanel());
            Controls.Add(top);
        }

        private static NumericUpDown Num(decimal max, int decimals, decimal value)
        {
            NumericUpDown n = new NumericUpDown();
            n.Minimum = 0;
            n.Maximum = max;
            n.DecimalPlaces = decimals;
            n.Increment = decimals > 0 ? 0.5m : 1;
            n.Width = 70;
            n.Value = Math.Max(0, Math.Min(max, value));
            return n;
        }

        private static Label Lbl(string text)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.Margin = new Padding(3, 7, 3, 0);
            return l;
        }

        private GroupBox Group(string title, int height)
        {
            GroupBox g = new GroupBox();
            g.Text = title;
            g.Size = new Size(680, height);
            return g;
        }

        private GroupBox BuildRepeatGroup()
        {
            GroupBox g = Group(Loc.T("Repetição", "Repetition"), 86);
            FlowLayoutPanel row1 = new FlowLayoutPanel { Location = new Point(8, 18), Size = new Size(660, 30) };
            m_Once = new RadioButton { Text = Loc.T("Tocar", "Play"), AutoSize = true, Margin = new Padding(3, 5, 3, 0) };
            m_Count = Num(999, 0, m_S.RepeatCount > 0 ? m_S.RepeatCount : 1);
            m_Forever = new RadioButton { Text = Loc.T("Repetir sem parar", "Repeat forever"), AutoSize = true, Margin = new Padding(20, 5, 3, 0) };
            row1.Controls.Add(m_Once);
            row1.Controls.Add(m_Count);
            row1.Controls.Add(Lbl(Loc.T("vezes", "times")));
            row1.Controls.Add(m_Forever);
            m_Forever.Checked = m_S.RepeatCount == -1;
            m_Once.Checked = !m_Forever.Checked;
            m_Forever.CheckedChanged += (s, e) => m_Count.Enabled = m_Once.Checked;
            m_Count.Enabled = m_Once.Checked;
            FlowLayoutPanel row2 = new FlowLayoutPanel { Location = new Point(8, 50), Size = new Size(660, 30) };
            m_IntFixed = Num(3600, 1, (decimal)m_S.IntervalFixed);
            m_IntRandom = Num(3600, 1, (decimal)m_S.IntervalRandom);
            row2.Controls.Add(Lbl(Loc.T("Pausa entre repetições:", "Pause between repetitions:")));
            row2.Controls.Add(m_IntFixed);
            row2.Controls.Add(Lbl(Loc.T("s  + aleatório até", "s  + random up to")));
            row2.Controls.Add(m_IntRandom);
            row2.Controls.Add(Lbl("s"));
            g.Controls.Add(row1);
            g.Controls.Add(row2);
            return g;
        }

        private GroupBox BuildStartGroup()
        {
            GroupBox g = Group(Loc.T("Início", "Start"), 56);
            FlowLayoutPanel row = new FlowLayoutPanel { Location = new Point(8, 18), Size = new Size(660, 30) };
            m_StartFixed = Num(3600, 1, (decimal)m_S.StartFixed);
            m_StartRandom = Num(3600, 1, (decimal)m_S.StartRandom);
            row.Controls.Add(Lbl(Loc.T("Esperar antes de começar:", "Wait before starting:")));
            row.Controls.Add(m_StartFixed);
            row.Controls.Add(Lbl(Loc.T("s  + aleatório até", "s  + random up to")));
            row.Controls.Add(m_StartRandom);
            row.Controls.Add(Lbl("s"));
            g.Controls.Add(row);
            return g;
        }

        private GroupBox BuildVolumeGroup()
        {
            GroupBox g = Group(Loc.T("Volume geral ao iniciar", "General volume when it starts"), 86);
            m_SetMusic = new CheckBox { Text = Loc.T("Definir o volume da música", "Set the music volume"), Location = new Point(10, 20), Size = new Size(240, 22), Checked = m_S.SetsMusicVolume };
            m_MusicBar = new TrackBar { Minimum = 0, Maximum = 100, TickFrequency = 10, Location = new Point(255, 14), Size = new Size(410, 30), Value = m_S.MusicVolume, Enabled = m_S.SetsMusicVolume };
            m_SetMusic.CheckedChanged += (s, e) => m_MusicBar.Enabled = m_SetMusic.Checked;
            m_SetSound = new CheckBox { Text = Loc.T("Definir o volume dos sons", "Set the sound volume"), Location = new Point(10, 52), Size = new Size(240, 22), Checked = m_S.SetsSoundVolume };
            m_SoundBar = new TrackBar { Minimum = 0, Maximum = 100, TickFrequency = 10, Location = new Point(255, 46), Size = new Size(410, 30), Value = m_S.SoundVolume, Enabled = m_S.SetsSoundVolume };
            m_SetSound.CheckedChanged += (s, e) => m_SoundBar.Enabled = m_SetSound.Checked;
            g.Controls.Add(m_SetMusic);
            g.Controls.Add(m_MusicBar);
            g.Controls.Add(m_SetSound);
            g.Controls.Add(m_SoundBar);
            return g;
        }

        private Panel BuildItemsPanel()
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Fill;
            p.Padding = new Padding(8, 4, 8, 4);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Bottom;
            buttons.Height = 36;
            m_Add = new Button { Text = Loc.T("Adicionar arquivos...", "Add files..."), Size = new Size(160, 28) };
            m_Remove = new Button { Text = Loc.T("Remover", "Remove"), Size = new Size(90, 28) };
            m_Up = new Button { Text = Loc.T("Subir", "Up"), Size = new Size(70, 28), Enabled = m_S.Reorderable };
            m_Down = new Button { Text = Loc.T("Descer", "Down"), Size = new Size(70, 28), Enabled = m_S.Reorderable };
            m_Add.Click += (s, e) => AddFiles();
            m_Remove.Click += (s, e) => RemoveSelected();
            m_Up.Click += (s, e) => MoveSelected(-1);
            m_Down.Click += (s, e) => MoveSelected(1);
            buttons.Controls.Add(m_Add);
            buttons.Controls.Add(m_Remove);
            buttons.Controls.Add(m_Up);
            buttons.Controls.Add(m_Down);

            m_Grid = new DataGridView();
            m_Grid.Dock = DockStyle.Fill;
            m_Grid.AllowUserToAddRows = false;
            m_Grid.AllowUserToDeleteRows = false;
            m_Grid.AllowUserToResizeRows = false;
            m_Grid.RowHeadersVisible = false;
            m_Grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            m_Grid.MultiSelect = true;
            m_Grid.EditMode = DataGridViewEditMode.EditOnEnter;
            m_Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "name", HeaderText = Loc.T("Nome", "Name"), ReadOnly = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, SortMode = DataGridViewColumnSortMode.NotSortable });
            if (m_S.ShowChance)
                m_Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "chance", HeaderText = Loc.T("Chance", "Chance"), Width = 70, SortMode = DataGridViewColumnSortMode.NotSortable });
            m_Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "volume", HeaderText = Loc.T("Volume %", "Volume %"), Width = 80, SortMode = DataGridViewColumnSortMode.NotSortable });
            if (m_S.ShowItemDelay)
                m_Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "delay", HeaderText = Loc.T("Espera (s)", "Wait (s)"), Width = 80, SortMode = DataGridViewColumnSortMode.NotSortable });
            m_Grid.CellValidating += Grid_CellValidating;
            FillGrid();

            p.Controls.Add(m_Grid);
            p.Controls.Add(buttons);
            return p;
        }

        private void Grid_CellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            string name = m_Grid.Columns[e.ColumnIndex].Name;
            if (name == "name")
                return;
            string text = (e.FormattedValue ?? "").ToString().Trim().Replace(',', '.');
            double v;
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out v) || v < 0 || (name == "volume" && v > 100))
            {
                e.Cancel = true;
                m_Grid.Rows[e.RowIndex].ErrorText = Loc.T("Valor inválido", "Invalid value");
            }
            else
            {
                m_Grid.Rows[e.RowIndex].ErrorText = "";
            }
        }

        private void FillGrid()
        {
            m_Grid.Rows.Clear();
            foreach (ItemSetting item in m_S.Items)
            {
                int row = m_Grid.Rows.Add();
                DataGridViewRow r = m_Grid.Rows[row];
                r.Tag = item;
                r.Cells["name"].Value = item.Title;
                if (m_S.ShowChance)
                    r.Cells["chance"].Value = item.Chance.ToString(CultureInfo.InvariantCulture);
                if (item.IsFile)
                    r.Cells["volume"].Value = item.Volume.ToString(CultureInfo.InvariantCulture);
                else
                    r.Cells["volume"].ReadOnly = true;
                if (m_S.ShowItemDelay)
                    r.Cells["delay"].Value = item.DelaySeconds.ToString("0.#", CultureInfo.InvariantCulture);
            }
        }

        // takes the values from the cells back into the items (before the list changes and before OK)
        private void SyncFromGrid()
        {
            if (m_Grid == null)
                return;
            m_Grid.EndEdit();
            foreach (DataGridViewRow r in m_Grid.Rows)
            {
                ItemSetting item = r.Tag as ItemSetting;
                if (item == null)
                    continue;
                double v;
                if (m_S.ShowChance && Parse(r.Cells["chance"].Value, out v))
                    item.Chance = (int)Math.Round(v);
                if (item.IsFile && Parse(r.Cells["volume"].Value, out v))
                    item.Volume = Math.Max(0, Math.Min(100, (int)Math.Round(v)));
                if (m_S.ShowItemDelay && Parse(r.Cells["delay"].Value, out v))
                    item.DelaySeconds = v;
            }
        }

        private static bool Parse(object value, out double v)
        {
            string text = value == null ? "" : value.ToString().Trim().Replace(',', '.');
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out v);
        }

        private void AddFiles()
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Multiselect = true;
                dlg.Title = Loc.T("Adicionar arquivos de áudio", "Add audio files");
                dlg.Filter = Loc.T("Áudio|*.mp3;*.ogg;*.opus;*.wav;*.flac;*.m4a;*.aac;*.wma|Todos os arquivos|*.*", "Audio|*.mp3;*.ogg;*.opus;*.wav;*.flac;*.m4a;*.aac;*.wma|All files|*.*");
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;
                SyncFromGrid();
                foreach (string file in dlg.FileNames)
                {
                    m_S.Items.Add(new ItemSetting
                    {
                        Id = 0,
                        Title = System.IO.Path.GetFileName(file),
                        IsFile = true,
                        NewPath = file,
                        Volume = 100,
                        Chance = 100
                    });
                }
                FillGrid();
            }
        }

        private void RemoveSelected()
        {
            SyncFromGrid();
            List<ItemSetting> doomed = new List<ItemSetting>();
            foreach (DataGridViewRow r in m_Grid.SelectedRows)
            {
                ItemSetting item = r.Tag as ItemSetting;
                if (item != null)
                    doomed.Add(item);
            }
            if (doomed.Count == 0)
                return;
            foreach (ItemSetting item in doomed)
            {
                if (item.Id != 0)
                    m_S.RemovedIds.Add(item.Id);
                m_S.Items.Remove(item);
            }
            FillGrid();
        }

        private void MoveSelected(int offset)
        {
            if (m_Grid.SelectedRows.Count != 1)
                return;
            SyncFromGrid();
            ItemSetting item = m_Grid.SelectedRows[0].Tag as ItemSetting;
            int index = m_S.Items.IndexOf(item);
            int target = index + offset;
            if (item == null || index < 0 || target < 0 || target >= m_S.Items.Count)
                return;
            m_S.Items.RemoveAt(index);
            m_S.Items.Insert(target, item);
            FillGrid();
            m_Grid.ClearSelection();
            m_Grid.Rows[target].Selected = true;
        }

        private void Commit()
        {
            SyncFromGrid();
            if (m_S.Repeatable)
            {
                m_S.RepeatCount = m_Forever.Checked ? -1 : (int)m_Count.Value;
                m_S.IntervalFixed = (double)m_IntFixed.Value;
                m_S.IntervalRandom = (double)m_IntRandom.Value;
            }
            if (m_S.Delayable)
            {
                m_S.StartFixed = (double)m_StartFixed.Value;
                m_S.StartRandom = (double)m_StartRandom.Value;
            }
            m_S.SetsMusicVolume = m_SetMusic.Checked;
            m_S.MusicVolume = m_MusicBar.Value;
            m_S.SetsSoundVolume = m_SetSound.Checked;
            m_S.SoundVolume = m_SoundBar.Value;
        }
    }

    public partial class Player
    {
        private void EditPlaylistProperties()
        {
            IMode mode; int index;
            IModeElement me = MenuElement(out mode, out index);
            if (me == null || m_Project == null)
                return;
            PlaylistSettings settings = PlaylistEditing.Read(me.StartElement);
            if (!PlaylistEditing.HasAnything(settings))
            {
                Ares.CommonGUI.MessageDialog.Show(this, T("Esta playlist não tem propriedades para editar.", "This playlist has no properties to edit."),
                    StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (PlaylistPropertiesDialog dlg = new PlaylistPropertiesDialog(me.Title, settings))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;
            }
            int modeIndex = ModeIndexByTitle(mode.Title);
            ApplyProjectEdit(() => PlaylistEditing.Apply(m_Project.GetModes()[modeIndex].GetElements()[index].StartElement, settings), null);
        }
    }
}
