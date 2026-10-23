using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Ares.AppCore;
using Ares.Data;
using Ares.Player;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Apolo.Desktop
{
    public abstract class Observable : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected bool Set<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;
            field = value;
            Raise(name);
            return true;
        }

        protected void Raise(string name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(name));
        }
    }

    public sealed class RelayCommand : ICommand
    {
        private readonly Action m_Run;
        public RelayCommand(Action run) { m_Run = run; }
        public event EventHandler CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object parameter) { return true; }
        public void Execute(object parameter) { m_Run(); }
    }

    /// <summary>A folder of the list on the left.</summary>
    public sealed class FolderVM : Observable
    {
        public string Title { get; private set; }
        public ModeNode Node { get; private set; }
        public ObservableCollection<FolderVM> Children { get; private set; }
        public IBrush IconMask { get; private set; }
        public IBrush Tint { get; private set; }
        public bool HasIcon { get { return IconMask != null; } }
        private bool m_Expanded;
        public bool IsExpanded { get { return m_Expanded; } set { if (Set(ref m_Expanded, value)) Node.Collapsed = !value; } }

        public FolderVM(ModeNode node, string projectPath)
        {
            Node = node;
            Title = node.Title;
            Children = new ObservableCollection<FolderVM>();
            string spec = ButtonDecor.Get(projectPath, node.Title, "");
            IconMask = IconCache.Mask(ButtonDecor.IconName(spec));
            Tint = IconCache.Brush(ButtonDecor.GetColor(projectPath, node.Title, "")) ?? new SolidColorBrush(Color.Parse("#9AA0B4"));
            m_Expanded = !node.Collapsed;
        }
    }

    /// <summary>The card of a button (a playlist or a sound effect).</summary>
    public sealed class CardVM : Observable
    {
        public int Id { get; private set; }
        public string Title { get; private set; }
        public IBrush IconMask { get; private set; }
        public Bitmap Picture { get; private set; }
        public IBrush Tint { get; private set; }
        public bool HasIcon { get { return IconMask != null && Picture == null; } }
        public bool HasPicture { get { return Picture != null; } }
        public bool IsMusic { get; private set; }
        public bool IsEffect { get; private set; }
        public ICommand Click { get; private set; }

        private bool m_Playing;
        public bool IsPlaying
        {
            get { return m_Playing; }
            set { if (Set(ref m_Playing, value)) { Raise("Edge"); Raise("Face"); } }
        }

        private static readonly IBrush s_PlayingEdge = new SolidColorBrush(Color.Parse("#8E5BD1"));
        private static readonly IBrush s_Face = new SolidColorBrush(Color.Parse("#2A2D38"));
        private static readonly IBrush s_PlayingFace = new SolidColorBrush(Color.Parse("#3B3158"));
        private readonly IBrush m_Edge;

        /// <summary>The border: the color of the button, brighter while it plays.</summary>
        public IBrush Edge { get { return m_Playing ? s_PlayingEdge : m_Edge; } }
        public IBrush Face { get { return m_Playing ? s_PlayingFace : s_Face; } }

        public CardVM(IModeElement element, string ownerTitle, string projectPath, Action<int> play)
        {
            Id = element.Id;
            Title = element.Title;
            int kinds = ElementKinds.Of(element.StartElement);
            IsMusic = (kinds & ElementKinds.Music) != 0;
            IsEffect = (kinds & ElementKinds.Effect) != 0;
            string spec = ButtonDecor.Get(projectPath, ownerTitle, element.Title);
            string name = ButtonDecor.IconName(spec);
            if (name != null)
            {
                IconMask = IconCache.Mask(name);
            }
            else
            {
                Picture = IconCache.File(ButtonDecor.ImagePath(projectPath, spec));
            }
            string color = ButtonDecor.GetColor(projectPath, ownerTitle, element.Title);
            Tint = IconCache.Brush(color) ?? new SolidColorBrush(Color.Parse("#ECEDF3"));
            m_Edge = IconCache.Brush(color, 150) ?? new SolidColorBrush(Color.Parse("#3A3E4E"));
            int id = Id;
            Click = new RelayCommand(() => play(id));
        }
    }

    /// <summary>The buttons of a folder, or of one of its subfolders (with a heading).</summary>
    public sealed class GroupVM
    {
        public string Header { get; set; }
        public bool HasHeader { get { return !string.IsNullOrEmpty(Header); } }
        public ObservableCollection<CardVM> Cards { get; private set; }
        public GroupVM() { Cards = new ObservableCollection<CardVM>(); }
    }

    /// <summary>A sound effect which is playing.</summary>
    public sealed class ChipVM : Observable
    {
        public int Handle { get; set; }
        private string m_Title;
        public string Title { get { return m_Title; } set { Set(ref m_Title, value); } }
        private double m_Progress;
        public double Progress { get { return m_Progress; } set { Set(ref m_Progress, value); } }
        private bool m_Paused;
        public bool Paused { get { return m_Paused; } set { if (Set(ref m_Paused, value)) Raise("PauseMask"); } }
        public IBrush PauseMask { get { return m_Paused ? Glyphs.Play : Glyphs.Pause; } }
        private bool m_Repeat;
        public bool Repeat { get { return m_Repeat; } set { if (Set(ref m_Repeat, value)) Raise("RepeatBrush"); } }
        public IBrush RepeatBrush
        {
            get { return m_Repeat ? (IBrush)new SolidColorBrush(Color.Parse("#8E5BD1")) : new SolidColorBrush(Color.Parse("#9AA0B4")); }
        }
        public ICommand StopCommand { get; set; }
        public ICommand PauseCommand { get; set; }
        public ICommand RepeatCommand { get; set; }
    }

    public sealed class MainViewModel : Observable, IDisposable
    {
        private readonly AppHost m_Host;
        private bool m_Updating;

        public ObservableCollection<FolderVM> Folders { get; private set; }
        public ObservableCollection<GroupVM> Groups { get; private set; }
        public ObservableCollection<ChipVM> Chips { get; private set; }
        public ObservableCollection<RecentVM> Recent { get; private set; }

        public MainViewModel(AppHost host)
        {
            m_Host = host;
            Folders = new ObservableCollection<FolderVM>();
            Groups = new ObservableCollection<GroupVM>();
            Chips = new ObservableCollection<ChipVM>();
            Recent = new ObservableCollection<RecentVM>();
            PauseCommand = new RelayCommand(() => { if (m_Host.Control != null) m_Host.Control.TogglePause(); });
            StopCommand = new RelayCommand(() => Key(Ares.Data.Keys.Escape));
            PreviousCommand = new RelayCommand(() => Key(Ares.Data.Keys.Left));
            NextCommand = new RelayCommand(() => Key(Ares.Data.Keys.Right));
            RepeatCommand = new RelayCommand(() => { m_Host.Control.SetRepeatCurrentMusic(!m_Host.Control.MusicRepeat); });
            if (m_Host.Control != null)
            {
                m_Global = m_Host.Control.GlobalVolume;
                m_Music = m_Host.Control.MusicVolume;
                m_Sound = m_Host.Control.SoundVolume;
            }
            m_Leveler = Ares.Playing.AudioLeveler.Enabled;
            RefreshRecent();
        }

        public ICommand PauseCommand { get; private set; }
        public ICommand StopCommand { get; private set; }
        public ICommand PreviousCommand { get; private set; }
        public ICommand NextCommand { get; private set; }
        public ICommand RepeatCommand { get; private set; }

        private void Key(Ares.Data.Keys key)
        {
            if (m_Host.Control != null)
                m_Host.Control.KeyReceived((int)key);
        }

        // ---- the project

        private string m_ProjectName = AppHost.T("Nenhum projeto aberto", "No project open");
        public string ProjectName { get { return m_ProjectName; } private set { Set(ref m_ProjectName, value); } }

        public bool HasProject { get { return m_Host.Project != null; } }
        public bool NoProject { get { return m_Host.Project == null; } }

        public void ProjectOpened()
        {
            ProjectName = m_Host.Project != null ? m_Host.Project.Title : AppHost.T("Nenhum projeto aberto", "No project open");
            Raise("HasProject");
            Raise("NoProject");
            Folders.Clear();
            BuildFolders(m_Host.Tree.Root, Folders);
            Groups.Clear();
            RefreshRecent();
            FolderVM first = Folders.FirstOrDefault();
            SelectedFolder = first;
        }

        private void BuildFolders(ModeNode parent, ObservableCollection<FolderVM> into)
        {
            foreach (ModeNode node in parent.Children)
            {
                FolderVM vm = new FolderVM(node, m_Host.ProjectPath);
                into.Add(vm);
                BuildFolders(node, vm.Children);
            }
        }

        private void RefreshRecent()
        {
            Recent.Clear();
            foreach (Ares.Settings.RecentFiles.ProjectEntry entry in m_Host.Recent.Take(10))
                Recent.Add(new RecentVM { Name = entry.ProjectName, Path = entry.FilePath });
        }

        private FolderVM m_Selected;
        public FolderVM SelectedFolder
        {
            get { return m_Selected; }
            set
            {
                if (Set(ref m_Selected, value))
                    ShowFolder();
            }
        }

        // the buttons of the folder, then (under a heading each) the ones of its subfolders
        private void ShowFolder()
        {
            Groups.Clear();
            if (m_Selected == null || m_Host.Project == null)
                return;
            Dictionary<string, IMode> byTitle = new Dictionary<string, IMode>(StringComparer.Ordinal);
            foreach (IMode mode in m_Host.Project.GetModes())
            {
                if (!byTitle.ContainsKey(mode.Title))
                    byTitle.Add(mode.Title, mode);
            }
            AddGroup(m_Selected.Node, null, byTitle);
            AddChildren(m_Selected.Node, "", byTitle, 0);
            UpdatePlaying();
        }

        private void AddChildren(ModeNode parent, string prefix, Dictionary<string, IMode> byTitle, int depth)
        {
            if (depth > 32)
                return;
            foreach (ModeNode child in parent.Children)
            {
                string label = prefix.Length == 0 ? child.Title : prefix + " › " + child.Title;
                AddGroup(child, label, byTitle);
                AddChildren(child, label, byTitle, depth + 1);
            }
        }

        private void AddGroup(ModeNode node, string header, Dictionary<string, IMode> byTitle)
        {
            IMode mode;
            if (!byTitle.TryGetValue(node.Title, out mode))
                return;
            GroupVM group = new GroupVM { Header = header };
            foreach (IModeElement element in mode.GetElements())
            {
                if (!element.IsVisibleInPlayer)
                    continue;
                group.Cards.Add(new CardVM(element, mode.Title, m_Host.ProjectPath, Play));
            }
            if (group.Cards.Count > 0 || header == null)
                Groups.Add(group);
        }

        private void Play(int id)
        {
            if (m_Host.Control == null)
                return;
            // the folder which is shown is the active one for the keys of the buttons
            if (m_Selected != null)
                m_Host.Control.SetMode(m_Selected.Title);
            m_Host.Control.SwitchElement(id);
            UpdatePlaying();
        }

        // ---- what plays now (called by a timer)

        private string m_MusicTitle = "";
        public string MusicTitle { get { return m_MusicTitle; } private set { Set(ref m_MusicTitle, value); } }
        private double m_MusicPosition;
        public double MusicPosition { get { return m_MusicPosition; } private set { Set(ref m_MusicPosition, value); } }
        private double m_MusicLength = 1;
        public double MusicLength { get { return m_MusicLength; } private set { Set(ref m_MusicLength, value); } }
        private string m_MusicTime = "";
        public string MusicTime { get { return m_MusicTime; } private set { Set(ref m_MusicTime, value); } }
        private bool m_Paused;
        public bool Paused { get { return m_Paused; } private set { if (Set(ref m_Paused, value)) Raise("PauseMask"); } }
        public IBrush PauseMask { get { return m_Paused ? Glyphs.Play : Glyphs.Pause; } }
        private bool m_RepeatMusic;
        public bool RepeatMusic { get { return m_RepeatMusic; } private set { if (Set(ref m_RepeatMusic, value)) Raise("RepeatBrush"); } }
        public IBrush RepeatBrush
        {
            get { return m_RepeatMusic ? (IBrush)new SolidColorBrush(Color.Parse("#8E5BD1")) : new SolidColorBrush(Color.Parse("#ECEDF3")); }
        }
        public bool HasChips { get { return Chips.Count > 0; } }

        public void Tick()
        {
            if (m_Host.Control == null)
                return;
            UpdatePlaying();
            m_Updating = true;
            try
            {
                Paused = m_Host.Control.IsPaused();
                RepeatMusic = m_Host.Control.MusicRepeat;
                Global = m_Host.Control.GlobalVolume;
                MusicVol = m_Host.Control.MusicVolume;
                SoundVol = m_Host.Control.SoundVolume;
            }
            finally
            {
                m_Updating = false;
            }
            int musicId = m_Host.Control.CurrentMusicElement;
            double position, length;
            if (musicId != -1 && m_Host.Control.GetMusicPosition(out position, out length) && length > 0)
            {
                IElement element = DataModule.ElementRepository.GetElement(musicId);
                IFileElement file = element as IFileElement;
                MusicTitle = file != null ? file.Title : "";
                MusicLength = length;
                MusicPosition = Math.Min(position, length);
                MusicTime = Format(position) + " / " + Format(length);
            }
            else
            {
                MusicTitle = "";
                MusicLength = 1;
                MusicPosition = 0;
                MusicTime = "";
            }
            UpdateChips();
        }

        private static string Format(double seconds)
        {
            TimeSpan t = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return t.TotalHours >= 1 ? string.Format("{0}:{1:00}:{2:00}", (int)t.TotalHours, t.Minutes, t.Seconds) : string.Format("{0}:{1:00}", t.Minutes, t.Seconds);
        }

        private void UpdatePlaying()
        {
            if (m_Host.Control == null)
                return;
            HashSet<int> playing = new HashSet<int>(m_Host.Control.CurrentModeElements.Select(e => e.Id));
            foreach (GroupVM group in Groups)
            {
                foreach (CardVM card in group.Cards)
                    card.IsPlaying = playing.Contains(card.Id);
            }
        }

        private void UpdateChips()
        {
            List<Ares.Playing.PlayingFileInfo> sounds = m_Host.Control.GetRunningSounds();
            bool changed = false;
            foreach (ChipVM chip in Chips.Where(c => sounds.All(s => s.Handle != c.Handle)).ToList())
            {
                Chips.Remove(chip);
                changed = true;
            }
            foreach (Ares.Playing.PlayingFileInfo info in sounds)
            {
                ChipVM chip = Chips.FirstOrDefault(c => c.Handle == info.Handle);
                if (chip == null)
                {
                    int handle = info.Handle;
                    chip = new ChipVM { Handle = handle };
                    chip.StopCommand = new RelayCommand(() => m_Host.Control.StopSound(handle));
                    chip.PauseCommand = new RelayCommand(() => m_Host.Control.ToggleSoundPause(handle));
                    chip.RepeatCommand = new RelayCommand(() =>
                    {
                        ChipVM c = Chips.FirstOrDefault(x => x.Handle == handle);
                        if (c != null)
                            m_Host.Control.SetSoundRepeat(handle, !c.Repeat);
                    });
                    Chips.Add(chip);
                    changed = true;
                }
                chip.Title = info.Title;
                chip.Progress = info.Length > 0 ? Math.Min(1.0, info.Position / info.Length) : 0;
                chip.Paused = info.Paused;
                chip.Repeat = info.Repeat;
            }
            if (changed)
                Raise("HasChips");
        }

        // ---- volumes and the leveler

        private int m_Global = 100, m_Music = 100, m_Sound = 100;
        public int Global
        {
            get { return m_Global; }
            set { if (Set(ref m_Global, value) && !m_Updating && m_Host.Control != null) m_Host.Control.GlobalVolume = value; }
        }
        public int MusicVol
        {
            get { return m_Music; }
            set { if (Set(ref m_Music, value) && !m_Updating && m_Host.Control != null) m_Host.Control.MusicVolume = value; }
        }
        public int SoundVol
        {
            get { return m_Sound; }
            set { if (Set(ref m_Sound, value) && !m_Updating && m_Host.Control != null) m_Host.Control.SoundVolume = value; }
        }

        private bool m_Leveler;
        public bool LevelerOn
        {
            get { return m_Leveler; }
            set
            {
                if (Set(ref m_Leveler, value))
                {
                    Ares.Playing.AudioLeveler.Enabled = value;
                    Ares.CommonGUI.Preferences.SetBool("LevelerOn", value);
                }
            }
        }

        public void SeekMusic(double seconds)
        {
            if (m_Host.Control != null)
                m_Host.Control.SeekMusic(seconds);
        }

        public void Dispose()
        {
        }
    }

    public sealed class RecentVM
    {
        public string Name { get; set; }
        public string Path { get; set; }
    }
}
