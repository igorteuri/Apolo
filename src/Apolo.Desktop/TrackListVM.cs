using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ares.Data;
using Ares.Player;
using Avalonia.Media;
using Avalonia.Threading;

namespace Apolo.Desktop
{
    /// <summary>A line of the track list: the file of a playlist with the data of its tags.</summary>
    public sealed class TrackRowVM : Observable
    {
        public IFileElement File { get; private set; }
        public int Original { get; set; }
        public string FullPath { get; private set; }
        public int FileId { get { return File.Id; } }

        private int m_Number;
        public int Number { get { return m_Number; } set { Set(ref m_Number, value); } }
        public string Kind { get { return File.SoundFileType == SoundFileType.Music ? "♫" : "⚡"; } }
        public string Title { get { return File.Title ?? ""; } }
        public string FilePath { get { return File.FilePath ?? ""; } }

        public bool Loaded { get; set; }
        private string m_Artist = "";
        public string Artist { get { return m_Artist; } set { Set(ref m_Artist, value ?? ""); } }
        private string m_Album = "";
        public string Album { get { return m_Album; } set { Set(ref m_Album, value ?? ""); } }
        private double m_Seconds;
        public double Seconds { get { return m_Seconds; } set { if (Set(ref m_Seconds, value)) Raise("Duration"); } }
        public string Duration
        {
            get
            {
                if (m_Seconds <= 0)
                    return "";
                int t = (int)Math.Round(m_Seconds);
                return t >= 3600 ? (t / 3600) + ":" + ((t / 60) % 60).ToString("00") + ":" + (t % 60).ToString("00") : (t / 60) + ":" + (t % 60).ToString("00");
            }
        }

        private int m_Plays;
        public int Plays { get { return m_Plays; } set { Set(ref m_Plays, value); } }

        private char m_Rights;
        public char Rights { get { return m_Rights; } set { if (Set(ref m_Rights, value)) { Raise("RightsText"); Raise("RightsBrush"); } } }
        private string m_RightsWhy = "";
        public string RightsWhy { get { return m_RightsWhy; } set { Set(ref m_RightsWhy, value ?? ""); } }
        public string RightsText { get { return RightsCheck.Label(m_Rights); } }
        public IBrush RightsBrush
        {
            get
            {
                switch (m_Rights)
                {
                    case 'G': return new SolidColorBrush(Color.Parse("#46BE5A"));
                    case 'Y': return new SolidColorBrush(Color.Parse("#EBBE28"));
                    case 'R': return new SolidColorBrush(Color.Parse("#E14646"));
                    default: return new SolidColorBrush(Color.Parse("#9AA0B4"));
                }
            }
        }

        private bool m_Playing;
        public bool IsPlaying { get { return m_Playing; } set { if (Set(ref m_Playing, value)) Raise("Back"); } }
        public IBrush Back { get { return m_Playing ? new SolidColorBrush(Color.Parse("#3B3158")) : Brushes.Transparent; } }

        public TrackRowVM(IFileElement file, string fullPath)
        {
            File = file;
            FullPath = fullPath;
        }
    }

    /// <summary>The list of the files of a button (like the panel of tracks of the Windows program).</summary>
    public sealed class TrackListVM : Observable
    {
        private readonly AppHost m_Host;
        private readonly List<TrackRowVM> m_All = new List<TrackRowVM>();
        private static readonly Dictionary<string, Ares.Tags.AudioTagInfo> s_TagCache =
            new Dictionary<string, Ares.Tags.AudioTagInfo>(StringComparer.OrdinalIgnoreCase);
        private int m_Generation;

        public ObservableCollection<TrackRowVM> Rows { get; private set; }
        public int ElementId { get; private set; }

        public TrackListVM(AppHost host)
        {
            m_Host = host;
            Rows = new ObservableCollection<TrackRowVM>();
            ElementId = -1;
            m_Title = AppHost.T("Clique num botão para ver as músicas", "Click a button to see its tracks");
        }

        private string m_Title;
        private string m_Notice;
        /// <summary>A message which replaces the title for a while (null = none).</summary>
        public string Title2 { get { return m_Notice; } set { m_Notice = value; Raise("Title"); } }
        public string Title { get { return m_Notice ?? m_Title; } private set { if (Set(ref m_Title, value)) Raise("Title"); } }

        private bool m_Visible = true;
        public bool Visible { get { return m_Visible; } set { Set(ref m_Visible, value); } }

        private string m_Search = "";
        public string Search
        {
            get { return m_Search; }
            set { if (Set(ref m_Search, value ?? "")) Refill(); }
        }

        private TrackRowVM m_Selected;
        public TrackRowVM Selected { get { return m_Selected; } set { Set(ref m_Selected, value); } }

        // ---- sorting: a second click on the column reverses, a third goes back to the order of the playlist

        private string m_SortColumn = "";
        private bool m_SortAscending = true;
        public static readonly string[] Columns = { "Number", "Kind", "Title", "Artist", "Album", "Duration", "Plays", "Rights", "File" };

        public string HeaderOf(string column)
        {
            string text;
            switch (column)
            {
                case "Number": text = "#"; break;
                case "Kind": text = AppHost.T("Tipo", "Kind"); break;
                case "Title": text = AppHost.T("Título", "Title"); break;
                case "Artist": text = AppHost.T("Artista", "Artist"); break;
                case "Album": text = AppHost.T("Álbum", "Album"); break;
                case "Duration": text = AppHost.T("Duração", "Duration"); break;
                case "Plays": text = AppHost.T("Tocadas", "Plays"); break;
                case "Rights": text = AppHost.T("Direitos", "Rights"); break;
                default: text = AppHost.T("Arquivo", "File"); break;
            }
            if (column == m_SortColumn)
                text += m_SortAscending ? "  ▲" : "  ▼";
            return text;
        }

        public void SortBy(string column)
        {
            if (m_SortColumn != column)
            {
                m_SortColumn = column;
                m_SortAscending = true;
            }
            else if (m_SortAscending)
            {
                m_SortAscending = false;
            }
            else
            {
                m_SortColumn = "";
                m_SortAscending = true;
            }
            Raise("Headers");
            Refill();
        }

        public string[] Headers { get { return Columns.Select(HeaderOf).ToArray(); } }

        private IEnumerable<TrackRowVM> Sorted(IEnumerable<TrackRowVM> rows)
        {
            if (m_SortColumn.Length == 0)
                return rows;
            bool asc = m_SortAscending;
            StringComparer text = StringComparer.CurrentCultureIgnoreCase;
            switch (m_SortColumn)
            {
                case "Number": return asc ? rows.OrderBy(r => r.Original) : rows.OrderByDescending(r => r.Original);
                case "Title": return asc ? rows.OrderBy(r => r.Title, text) : rows.OrderByDescending(r => r.Title, text);
                case "Artist": return asc ? rows.OrderBy(r => r.Artist, text) : rows.OrderByDescending(r => r.Artist, text);
                case "Album": return asc ? rows.OrderBy(r => r.Album, text) : rows.OrderByDescending(r => r.Album, text);
                case "Duration": return asc ? rows.OrderBy(r => r.Seconds) : rows.OrderByDescending(r => r.Seconds);
                case "Plays": return asc ? rows.OrderBy(r => r.Plays) : rows.OrderByDescending(r => r.Plays);
                case "Kind": return asc ? rows.OrderBy(r => (int)r.File.SoundFileType) : rows.OrderByDescending(r => (int)r.File.SoundFileType);
                case "Rights": return asc ? rows.OrderBy(r => RightsCheck.Rank(r.Rights)) : rows.OrderByDescending(r => RightsCheck.Rank(r.Rights));
                default: return asc ? rows.OrderBy(r => r.FilePath, text) : rows.OrderByDescending(r => r.FilePath, text);
            }
        }

        // ---- loading

        private static string BaseDirectory(IFileElement file)
        {
            return file.SoundFileType == SoundFileType.Music
                ? Ares.Settings.Settings.Instance.MusicDirectory : Ares.Settings.Settings.Instance.SoundDirectory;
        }

        public static string FullPathOf(IFileElement file)
        {
            try
            {
                return System.IO.Path.Combine(BaseDirectory(file) ?? "", file.FilePath ?? "");
            }
            catch (Exception)
            {
                return file.FilePath ?? "";
            }
        }

        /// <summary>The files of the playlist of a button: the ones of the container itself, or else the ones inside of it.</summary>
        public static List<IFileElement> FilesOf(IElement start)
        {
            List<IFileElement> files = new List<IFileElement>();
            IGeneralElementContainer container = start as IGeneralElementContainer;
            if (container != null)
                files.AddRange(container.GetFileElements());
            if (files.Count == 0)
                Collect(start, files, 0);
            return files;
        }

        private static void Collect(IElement element, List<IFileElement> into, int depth)
        {
            if (element == null || depth > 12 || into.Count > 5000)
                return;
            IFileElement file = element as IFileElement;
            if (file != null)
            {
                into.Add(file);
                return;
            }
            IReferenceElement reference = element as IReferenceElement;
            if (reference != null)
            {
                Collect(DataModule.ElementRepository.GetElement(reference.ReferencedId), into, depth + 1);
                return;
            }
            IGeneralElementContainer container = element as IGeneralElementContainer;
            if (container != null)
            {
                foreach (IFileElement f in container.GetFileElements())
                    Collect(f, into, depth + 1);
                foreach (IContainerElement inner in container.GetGeneralElements())
                    Collect(inner, into, depth + 1);
                if (into.Count > 0)
                    return;
            }
            IContainerElement wrapper = element as IContainerElement;
            if (wrapper != null && !ReferenceEquals(wrapper.InnerElement, element))
                Collect(wrapper.InnerElement, into, depth + 1);
        }

        public void Load(int elementId, string title)
        {
            IModeElement element = FindElement(elementId);
            ElementId = elementId;
            m_ElementTitle = title;
            if (!string.Equals(m_LoadedFor, elementId.ToString()))
                m_Search = "";
            m_LoadedFor = elementId.ToString();
            Raise("Search");
            m_All.Clear();
            if (element != null)
            {
                int n = 0;
                foreach (IFileElement file in FilesOf(element.StartElement))
                {
                    n++;
                    string full = FullPathOf(file);
                    TrackRowVM row = new TrackRowVM(file, full) { Original = n };
                    row.Plays = PlayCounts.Get(full);
                    char code;
                    string why;
                    if (RightsCheck.TryGetChecked(full, out code, out why))
                    {
                        row.Rights = code;
                        row.RightsWhy = why;
                    }
                    ApplyCached(row);
                    m_All.Add(row);
                }
            }
            Refill();
            LoadTags();
        }

        private string m_ElementTitle = "";
        private string m_LoadedFor = "";

        private IModeElement FindElement(int id)
        {
            if (m_Host.Project == null)
                return null;
            foreach (IMode mode in m_Host.Project.GetModes())
            {
                IModeElement found = mode.GetElements().FirstOrDefault(e => e.Id == id);
                if (found != null)
                    return found;
            }
            return null;
        }

        private void Refill()
        {
            string q = m_Search.Trim();
            TrackRowVM keep = m_Selected;
            List<TrackRowVM> shown = new List<TrackRowVM>();
            foreach (TrackRowVM row in m_All)
            {
                if (q.Length > 0 && row.Title.IndexOf(q, StringComparison.CurrentCultureIgnoreCase) < 0
                    && row.Artist.IndexOf(q, StringComparison.CurrentCultureIgnoreCase) < 0)
                    continue;
                shown.Add(row);
            }
            Rows.Clear();
            int number = 0;
            foreach (TrackRowVM row in Sorted(shown))
            {
                row.Number = ++number;
                Rows.Add(row);
            }
            Selected = keep != null && Rows.Contains(keep) ? keep : null;
            Title = m_ElementId_IsNone() ? AppHost.T("Clique num botão para ver as músicas", "Click a button to see its tracks")
                : m_ElementTitle + "  ·  " + m_All.Count + " " + AppHost.T("arquivos", "files");
        }

        private bool m_ElementId_IsNone()
        {
            return ElementId < 0;
        }

        // ---- the tags are read in the background; the cells fill in as they arrive

        private static void ApplyCached(TrackRowVM row)
        {
            Ares.Tags.AudioTagInfo info;
            lock (s_TagCache)
            {
                if (!s_TagCache.TryGetValue(row.FullPath, out info))
                    return;
            }
            Fill(row, info);
        }

        private static void Fill(TrackRowVM row, Ares.Tags.AudioTagInfo info)
        {
            row.Loaded = true;
            if (row.Rights == '\0')
            {
                char code;
                string why;
                RightsCheck.Quick(info, row.FullPath, out code, out why);
                row.Rights = code;
                row.RightsWhy = why;
            }
            row.Artist = info.artist;
            row.Album = info.album;
            row.Seconds = info.duration;
        }

        private void LoadTags()
        {
            int generation = Interlocked.Increment(ref m_Generation);
            List<TrackRowVM> todo = m_All.Where(r => !r.Loaded).ToList();
            if (todo.Count == 0)
                return;
            Task.Run(() =>
            {
                int done = 0;
                List<KeyValuePair<TrackRowVM, Ares.Tags.AudioTagInfo>> batch = new List<KeyValuePair<TrackRowVM, Ares.Tags.AudioTagInfo>>();
                foreach (TrackRowVM row in todo)
                {
                    if (generation != m_Generation)
                        return;
                    Ares.Tags.AudioTagInfo info = null;
                    try
                    {
                        if (System.IO.File.Exists(row.FullPath))
                            info = Ares.Tags.AudioTags.Read(row.FullPath);
                    }
                    catch (Exception)
                    {
                    }
                    if (info == null)
                        info = new Ares.Tags.AudioTagInfo();
                    lock (s_TagCache)
                        s_TagCache[row.FullPath] = info;
                    batch.Add(new KeyValuePair<TrackRowVM, Ares.Tags.AudioTagInfo>(row, info));
                    done++;
                    if (done % 12 == 0 || done == todo.Count)
                    {
                        List<KeyValuePair<TrackRowVM, Ares.Tags.AudioTagInfo>> send = batch;
                        batch = new List<KeyValuePair<TrackRowVM, Ares.Tags.AudioTagInfo>>();
                        Dispatcher.UIThread.Post(() => Arrived(generation, send));
                    }
                }
            });
        }

        private void Arrived(int generation, List<KeyValuePair<TrackRowVM, Ares.Tags.AudioTagInfo>> batch)
        {
            if (generation != m_Generation)
                return;
            foreach (KeyValuePair<TrackRowVM, Ares.Tags.AudioTagInfo> pair in batch)
                Fill(pair.Key, pair.Value);
            // sorted by something which has just arrived: sort again
            if (m_SortColumn == "Artist" || m_SortColumn == "Album" || m_SortColumn == "Duration" || m_SortColumn == "Rights")
                Refill();
        }

        // ---- playing

        /// <summary>Marks the file which plays now and counts a play when the music changed.</summary>
        public void MusicChanged(int musicId)
        {
            foreach (TrackRowVM row in m_All)
                row.IsPlaying = row.FileId == musicId;
        }

        public void CountPlay(int musicId)
        {
            TrackRowVM row = m_All.FirstOrDefault(r => r.FileId == musicId);
            IFileElement file = row != null ? row.File : DataModule.ElementRepository.GetElement(musicId) as IFileElement;
            if (file == null)
                return;
            int n = PlayCounts.Add(FullPathOf(file));
            if (row != null)
                row.Plays = n;
        }

        /// <summary>Plays the file: starts the playlist if it is not playing, then goes to the file.</summary>
        public void Play(TrackRowVM row)
        {
            if (row == null || m_Host.Control == null)
                return;
            IModeElement element = FindElement(ElementId);
            if (element == null)
                return;
            bool active = m_Host.Control.CurrentModeElements.Any(e => e.Id == element.Id);
            if (active)
            {
                m_Host.Control.SelectMusicElement(row.FileId);
                return;
            }
            m_Host.Control.SwitchElement(element.Id);
            int fileId = row.FileId;
            DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                m_Host.Control.SelectMusicElement(fileId);
            };
            timer.Start();
        }

        /// <summary>The check of the rights on the internet for the rows of the list (some seconds each); calls back for each one.</summary>
        public void CheckRights(Action finished)
        {
            List<TrackRowVM> todo = m_All.Where(r => r.Rights == '\0').ToList();
            Task.Run(() =>
            {
                foreach (TrackRowVM row in todo)
                {
                    char code;
                    string why;
                    try
                    {
                        RightsCheck.Full(row.FullPath, Ares.Tags.AudioTags.Read(row.FullPath), out code, out why);
                    }
                    catch (Exception ex)
                    {
                        code = '\0';
                        why = ex.Message;
                    }
                    TrackRowVM r = row;
                    char c = code;
                    string w = why;
                    Dispatcher.UIThread.Post(() => { r.Rights = c; r.RightsWhy = w; });
                }
                Dispatcher.UIThread.Post(finished);
            });
        }
    }
}
