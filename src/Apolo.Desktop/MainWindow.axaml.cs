using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace Apolo.Desktop
{
    public partial class MainWindow : Window
    {
        private readonly AppHost m_Host;
        private readonly MainViewModel m_Model;
        private DispatcherTimer m_Timer;

        public MainWindow()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public MainWindow(AppHost host, bool restoreLast = true) : this()
        {
            m_Host = host;
            m_Model = new MainViewModel(host);
            DataContext = m_Model;
            Localize();
            m_Timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            m_Timer.Tick += (s, e) => m_Model.Tick();
            m_Timer.Start();
            if (restoreLast)
                Opened += (s, e) => OpenLast();
            AddHandler(DragDrop.DropEvent, OnDrop);
            AddHandler(DragDrop.DragOverEvent, OnDragOver);
        }

        /// <summary>Opens a project (also used by the tests and the screenshots).</summary>
        public void OpenProject(string path)
        {
            try
            {
                m_Host.Open(path);
                m_Model.ProjectOpened();
                RefreshRecentMenu();
            }
            catch (Exception ex)
            {
                new MessageWindow(AppHost.T("Não foi possível abrir o projeto", "The project could not be opened"), ex.Message).Show(this);
            }
        }

        public MainViewModel Model
        {
            get { return m_Model; }
        }

        private void OpenLast()
        {
            string last = Ares.CommonGUI.Preferences.Get("LastProject", "");
            if (!string.IsNullOrEmpty(last) && File.Exists(last))
                OpenProject(last);
            else
                RefreshRecentMenu();
        }

        private T Find<T>(string name) where T : Control
        {
            return this.FindControl<T>(name);
        }

        private void Localize()
        {
            Find<MenuItem>("ProjectMenu").Header = AppHost.T("Projeto", "Project");
            Find<MenuItem>("NewItem").Header = AppHost.T("Novo projeto...", "New project...");
            Find<MenuItem>("OpenItem").Header = AppHost.T("Abrir...", "Open...");
            Find<MenuItem>("UndoItem").Header = AppHost.T("Desfazer", "Undo");
            Find<MenuItem>("RedoItem").Header = AppHost.T("Refazer", "Redo");
            Find<MenuItem>("ViewMenu").Header = AppHost.T("Exibir", "View");
            Find<MenuItem>("TracksItem").Header = AppHost.T("Lista de faixas", "Track list");
            Find<MenuItem>("ZoomMenu").Header = AppHost.T("Tamanho do texto das pastas", "Folder text size");
            Find<MenuItem>("ZoomBigger").Header = AppHost.T("Maior", "Bigger");
            Find<MenuItem>("ZoomSmaller").Header = AppHost.T("Menor", "Smaller");
            Find<MenuItem>("ZoomNormal").Header = AppHost.T("Normal", "Normal");
            UpdateSoftenMark();
            Find<MenuItem>("PanelItem").Header = AppHost.T("Painel de estado", "State panel");
            Find<MenuItem>("PadItem").Header = AppHost.T("Pad flutuante (Ctrl+Alt+P)", "Floating pad (Ctrl+Alt+P)");
            Find<TextBlock>("PanelTitle").Text = AppHost.T("Estado", "State");
            Find<Button>("AddButtonMenu").Content = AppHost.T("+ Adicionar ▾", "+ Add ▾");
            Find<TextBox>("SearchBox").Watermark = AppHost.T("Buscar playlist...", "Search playlist...");
            Find<MenuItem>("NewFolderItem").Header = AppHost.T("Nova pasta...", "New folder...");
            Find<MenuItem>("PacksItem").Header = AppHost.T("Pacotes de conteúdo prontos...", "Ready-made content packs...");
            Find<Button>("WelcomePacks").Content = AppHost.T("Baixar pacotes prontos...", "Download ready-made packs...");
            Find<MenuItem>("ImportFolderItem").Header = AppHost.T("Adicionar pasta de músicas...", "Add a folder of music...");
            Find<MenuItem>("ImportFilesItem").Header = AppHost.T("Adicionar arquivos de áudio...", "Add audio files...");
            Find<MenuItem>("RecentMenu").Header = AppHost.T("Recentes", "Recent");
            Find<MenuItem>("SettingsItem").Header = AppHost.T("Configurações...", "Settings...");
            Find<MenuItem>("QuitItem").Header = AppHost.T("Sair", "Quit");
            Find<MenuItem>("HelpMenu").Header = AppHost.T("Ajuda", "Help");
            Find<MenuItem>("AboutItem").Header = AppHost.T("Sobre o Apolo", "About Apolo");
            Find<TextBlock>("GlobalLabel").Text = AppHost.T("Geral", "Overall");
            Find<TextBlock>("MusicLabel").Text = AppHost.T("Música", "Music");
            Find<TextBlock>("SoundLabel").Text = AppHost.T("Sons", "Sounds");
            Find<TextBlock>("LevelLabel").Text = AppHost.T("Nivelar", "Level");
            Find<TextBlock>("WelcomeTitle").Text = AppHost.T("Bem-vindo ao Apolo", "Welcome to Apolo");
            Find<TextBlock>("WelcomeText").Text = AppHost.T(
                "O Apolo é um controlador de músicas para mesas de RPG. Ele não vem com músicas: abra um projeto seu (.apolo) ou um pacote pronto (.apkg).",
                "Apolo is a music controller for tabletop RPG. It does not come with music: open a project of yours (.apolo) or a ready-made pack (.apkg).");
            Find<Button>("WelcomeOpen").Content = AppHost.T("Abrir um projeto ou pacote...", "Open a project or pack...");
        }

        private void RefreshRecentMenu()
        {
            MenuItem menu = Find<MenuItem>("RecentMenu");
            List<object> items = new List<object>();
            foreach (RecentVM recent in m_Model.Recent)
            {
                string path = recent.Path;
                MenuItem item = new MenuItem { Header = recent.Name };
                item.Click += (s, e) => OpenAny(path);
                items.Add(item);
            }
            menu.ItemsSource = items;
            menu.IsEnabled = items.Count > 0;
        }

        private async void OnOpen(object sender, RoutedEventArgs e)
        {
            IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = AppHost.T("Abrir projeto ou pacote", "Open a project or pack"),
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType(AppHost.T("Projetos e pacotes do Apolo", "Apolo projects and packs")) { Patterns = new[] { "*.apolo", "*.ares", "*.apkg" } },
                    FilePickerFileTypes.All
                }
            });
            if (files.Count > 0)
                OpenAny(files[0].Path.LocalPath);
        }

        private void OpenAny(string path)
        {
            if (path.EndsWith(".apkg", StringComparison.OrdinalIgnoreCase))
                ImportPack(path);
            else
                OpenProject(path);
        }

        // a pack (.apkg) is unpacked into Documents/Apolo/Pacotes and then opened
        private void ImportPack(string path)
        {
            m_Host.Import(path, error =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (error != null)
                        new MessageWindow(AppHost.T("Não foi possível importar o pacote", "The pack could not be imported"), error.Message).Show(this);
                    else if (m_Host.ImportedProject != null)
                        OpenProject(m_Host.ImportedProject);
                });
            });
        }

        // ---- new project and import of audio

        private async void OnNewProject(object sender, RoutedEventArgs e)
        {
            IStorageFile file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = AppHost.T("Novo projeto", "New project"),
                SuggestedFileName = AppHost.T("Meu projeto", "My project"),
                DefaultExtension = "apolo",
                FileTypeChoices = new[] { new FilePickerFileType(AppHost.T("Projeto do Apolo", "Apolo project")) { Patterns = new[] { "*.apolo" } } }
            });
            if (file == null)
                return;
            string path = file.Path.LocalPath;
            if (!path.EndsWith(".apolo", StringComparison.OrdinalIgnoreCase))
                path += ".apolo";
            try
            {
                m_Host.CreateProject(path, Path.GetFileNameWithoutExtension(path));
                m_Model.ProjectOpened();
                RefreshRecentMenu();
            }
            catch (Exception ex)
            {
                new MessageWindow(AppHost.T("Não foi possível criar o projeto", "The project could not be created"), ex.Message).Show(this);
            }
        }

        private async void OnImportFolder(object sender, RoutedEventArgs e)
        {
            IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = AppHost.T("Escolha a pasta com os áudios", "Choose the folder with the audio"),
                AllowMultiple = true
            });
            if (folders.Count > 0)
                ImportPaths(folders.Select(f => f.Path.LocalPath).ToArray());
        }

        private async void OnImportFiles(object sender, RoutedEventArgs e)
        {
            IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = AppHost.T("Escolha os arquivos de áudio", "Choose the audio files"),
                AllowMultiple = true,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType(AppHost.T("Arquivos de áudio", "Audio files")) { Patterns = new[] { "*.mp3", "*.ogg", "*.opus", "*.wav", "*.flac", "*.m4a", "*.aac", "*.wma", "*.aif", "*.aiff" } },
                    FilePickerFileTypes.All
                }
            });
            if (files.Count > 0)
                ImportPaths(files.Select(f => f.Path.LocalPath).ToArray());
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            e.DragEffects = e.Data.Contains(DataFormats.Files) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            IEnumerable<IStorageItem> items = e.Data.GetFiles();
            if (items == null)
                return;
            string[] paths = items.Select(i => i.TryGetLocalPath()).Where(p => !string.IsNullOrEmpty(p)).ToArray();
            if (paths.Length == 0)
                return;
            // a project or a pack dropped on the window is opened; audio is added to the project
            if (paths.Length == 1 && (paths[0].EndsWith(".apolo", StringComparison.OrdinalIgnoreCase) || paths[0].EndsWith(".apkg", StringComparison.OrdinalIgnoreCase)))
                OpenAny(paths[0]);
            else
                ImportPaths(paths);
            e.Handled = true;
        }

        /// <summary>Looks at the files and folders, asks what to do (only what is open) and puts them in the project.</summary>
        public async void ImportPaths(string[] paths)
        {
            Ares.CommonGUI.DropScan scan = Ares.CommonGUI.DropScan.Read(paths);
            if (scan.IsEmpty)
            {
                new MessageWindow(AppHost.T("Adicionar ao Apolo", "Add to Apolo"),
                    AppHost.T("Nenhum arquivo de áudio encontrado no que foi escolhido.", "No audio files found in what was chosen.")).Show(this);
                return;
            }
            string destination = m_Model.SelectedFolder != null && m_Host.Project != null ? m_Model.SelectedFolder.Title : null;
            DropOptionsWindow dialog = new DropOptionsWindow(scan, destination);
            await dialog.ShowDialog(this);
            if (dialog.Result == null)
                return;
            try
            {
                AppHost.ImportSummary summary = m_Host.ImportDropped(scan, dialog.Result, destination);
                m_Model.ProjectChanged(summary.ShowTitle);
                RefreshRecentMenu();
            }
            catch (Exception ex)
            {
                new MessageWindow(AppHost.T("Erro ao adicionar", "Error while adding"), ex.Message).Show(this);
            }
        }

        // ---- editing: buttons

        private static T Context<T>(object sender) where T : class
        {
            MenuItem item = sender as MenuItem;
            return item != null ? item.DataContext as T : null;
        }

        private void AfterEdit(string showTitle)
        {
            m_Model.ProjectChanged(showTitle);
        }

        private string ShownTitle()
        {
            return m_Model.SelectedFolder != null ? m_Model.SelectedFolder.Title : null;
        }

        private void OnCardTracks(object sender, RoutedEventArgs e)
        {
            CardVM card = Context<CardVM>(sender);
            if (card != null)
                m_Model.ShowTracksOf(card.Id, card.Title);
        }

        // ---- undo / redo

        private void OnUndo(object sender, RoutedEventArgs e) { Travel(false); }
        private void OnRedo(object sender, RoutedEventArgs e) { Travel(true); }

        private void Travel(bool redo)
        {
            if (m_Host.Project == null)
                return;
            string title = m_Host.Travel(redo, m_Model.SelectedFolderTitle);
            if (title == null)
                return;
            m_Model.ProjectChanged(title.Length > 0 ? title : null);
        }

        // ---- zoom of the folders, the crossfade of the effects

        private void OnZoomBigger(object sender, RoutedEventArgs e) { m_Model.ChangeFolderZoom(10); }
        private void OnZoomSmaller(object sender, RoutedEventArgs e) { m_Model.ChangeFolderZoom(-10); }
        private void OnZoomNormal(object sender, RoutedEventArgs e) { m_Model.SetFolderZoom(100); }

        private void OnSoften(object sender, RoutedEventArgs e)
        {
            bool on = !Ares.Playing.SoundRepeat.CrossfadeEnabled;
            Ares.Playing.SoundRepeat.CrossfadeEnabled = on;
            Ares.CommonGUI.Preferences.SetBool("SfxLoopCrossfade", on);
            UpdateSoftenMark();
        }

        private void UpdateSoftenMark()
        {
            Find<MenuItem>("SoftenItem").Header = (Ares.Playing.SoundRepeat.CrossfadeEnabled ? "✓  " : "     ")
                + AppHost.T("Suavizar a repetição dos efeitos (crossfade)", "Smooth the repetition of effects (crossfade)");
        }

        // ---- the button "+ Add"

        private void OnAddMenu(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            ContextMenu menu = new ContextMenu();
            MenuItem music = new MenuItem { Header = AppHost.T("Playlist de música...", "Music playlist...") };
            music.Click += (s, a) => AddButtonOfKind(false);
            MenuItem sfx = new MenuItem { Header = AppHost.T("Efeito sonoro (SFX)...", "Sound effect (SFX)...") };
            sfx.Click += (s, a) => AddButtonOfKind(true);
            MenuItem files = new MenuItem { Header = AppHost.T("Vários arquivos (escolher como organizar)...", "Several files (choose how to organize)...") };
            files.Click += OnImportFiles;
            MenuItem folder = new MenuItem { Header = AppHost.T("Pasta do computador (escolher como organizar)...", "Folder of the computer (choose how to organize)...") };
            folder.Click += OnImportFolder;
            menu.Items.Add(music);
            menu.Items.Add(sfx);
            menu.Items.Add(new Separator());
            menu.Items.Add(files);
            menu.Items.Add(folder);
            menu.Open(button);
        }

        private async void AddButtonOfKind(bool sfx)
        {
            IReadOnlyList<IStorageFile> picked = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = sfx ? AppHost.T("Escolher o arquivo do efeito sonoro", "Choose the file of the sound effect")
                    : AppHost.T("Escolher as músicas da playlist", "Choose the tracks of the playlist"),
                AllowMultiple = true,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType(AppHost.T("Arquivos de áudio", "Audio files")) { Patterns = new[] { "*.mp3", "*.ogg", "*.opus", "*.wav", "*.flac", "*.m4a", "*.aac", "*.wma", "*.aif", "*.aiff" } }
                }
            });
            List<string> files = picked.Select(f => f.Path.LocalPath).Where(Ares.CommonGUI.DropScan.IsAudioFile).ToList();
            if (files.Count == 0)
                return;
            string suggestion = files.Count == 1 ? Path.GetFileNameWithoutExtension(files[0]) : Path.GetFileName(Path.GetDirectoryName(files[0]));
            string name = await PromptWindow.Ask(this, sfx ? AppHost.T("Novo efeito sonoro", "New sound effect") : AppHost.T("Nova playlist", "New playlist"),
                AppHost.T("Nome do botão:", "Button name:"), suggestion);
            if (name == null)
                return;
            string shown = m_Host.AddButton(files, name, sfx, m_Model.SelectedFolderTitle);
            m_Model.ProjectChanged(shown);
        }

        private async void OnCardKind(object sender, RoutedEventArgs e)
        {
            CardVM card = Context<CardVM>(sender);
            if (card == null)
                return;
            bool toSfx;
            int count;
            int need = m_Host.KindSwitch(card.Id, out toSfx, out count);
            if (need == 0)
            {
                new MessageWindow(AppHost.T("Trocar o tipo", "Switch the kind"), AppHost.T(
                    "Este botão tem uma estrutura especial (por exemplo, mistura música e efeitos, ou tem vários níveis). Não dá para trocar o tipo automaticamente.",
                    "This button has a special structure (for example it mixes music and effects, or has several levels). Its kind cannot be switched automatically.")).Show(this);
                return;
            }
            if (need == 2)
            {
                string text = toSfx
                    ? AppHost.T("A playlist \"" + card.Title + "\" tem " + count + " músicas.\n\nComo efeito sonoro, TODAS vão tocar ao mesmo tempo quando você clicar no botão. Tem certeza?",
                        "The playlist \"" + card.Title + "\" has " + count + " tracks.\n\nAs a sound effect, ALL of them play at the same time when you click the button. Are you sure?")
                    : AppHost.T("Este efeito sonoro toca " + count + " sons juntos.\n\nComo música, os arquivos passam a tocar um de cada vez (em ordem sorteada). Continuar?",
                        "This sound effect plays " + count + " sounds together.\n\nAs music, the files play one at a time (in random order). Continue?");
                if (!await ConfirmWindow.Ask(this, AppHost.T("Trocar o tipo", "Switch the kind"), text))
                    return;
            }
            string shown = ShownTitle();
            m_Host.ToggleButtonKind(card.Id);
            AfterEdit(shown);
        }

        private void OnListToggle(object sender, RoutedEventArgs e)
        {
            m_Model.ToggleListMode();
        }

        private void OnTrackHeader(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button != null && button.Tag != null)
                m_Model.TrackList.SortBy(button.Tag.ToString());
        }

        private void OnTrackPlay(object sender, RoutedEventArgs e)
        {
            m_Model.TrackList.Play(m_Model.TrackList.Selected);
        }

        private void OnTrackShow(object sender, RoutedEventArgs e)
        {
            TrackRowVM row = m_Model.TrackList.Selected;
            if (row == null)
                return;
            try
            {
                string path = row.FullPath;
                System.Diagnostics.ProcessStartInfo info;
                if (OperatingSystem.IsWindows())
                    info = new System.Diagnostics.ProcessStartInfo("explorer.exe", "/select,\"" + path + "\"");
                else if (OperatingSystem.IsMacOS())
                    info = new System.Diagnostics.ProcessStartInfo("open", "-R \"" + path + "\"");
                else
                    info = new System.Diagnostics.ProcessStartInfo("xdg-open", "\"" + Path.GetDirectoryName(path) + "\"");
                info.UseShellExecute = false;
                System.Diagnostics.Process.Start(info);
            }
            catch (Exception)
            {
            }
        }

        private void OnTrackRights(object sender, RoutedEventArgs e)
        {
            m_Model.TrackList.Title2 = AppHost.T("Verificando direitos na internet...", "Checking rights on the internet...");
            m_Model.TrackList.CheckRights(() => m_Model.TrackList.Title2 = null);
        }

        private void OnTogglePanel(object sender, RoutedEventArgs e)
        {
            m_Model.PanelVisible = !m_Model.PanelVisible;
        }

        private PadWindow m_Pad;

        private void OnPad(object sender, RoutedEventArgs e)
        {
            if (m_Host.Project == null)
                return;
            if (m_Pad != null)
            {
                m_Pad.Close();
                return;
            }
            m_Pad = new PadWindow(m_Host, m_Model.SelectedFolderTitle);
            m_Pad.Closed += (s, a) => m_Pad = null;
            m_Pad.Show();
        }

        private void OnToggleTracks(object sender, RoutedEventArgs e)
        {
            m_Model.TrackList.Visible = !m_Model.TrackList.Visible;
        }

        // the sound cards: the menu is made when it opens, as cards can be plugged in meanwhile
        private void OnOutputClick(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button == null)
                return;
            ContextMenu menu = new ContextMenu();
            int current = Ares.Settings.Settings.Instance.OutputDeviceIndex;
            foreach (Ares.Playing.AudioInit.OutputDevice device in Ares.Playing.AudioInit.GetActiveDevices())
            {
                int index = device.Index;
                MenuItem item = new MenuItem { Header = (index == current ? "✓  " : "     ") + device.Name };
                item.Click += (s, a) => ChangeOutput(index);
                menu.Items.Add(item);
            }
            menu.PlacementTarget = button;
            menu.Placement = PlacementMode.BottomEdgeAlignedRight;
            menu.Open(button);
        }

        private void ChangeOutput(int index)
        {
            string problem = m_Host.ApplySettings(index);
            m_Model.RefreshOutput();
            if (problem != null)
                new MessageWindow("Apolo", problem).Show(this);
        }

        private async void OnCardRename(object sender, RoutedEventArgs e)
        {
            CardVM card = Context<CardVM>(sender);
            if (card == null)
                return;
            string name = await PromptWindow.Ask(this, AppHost.T("Renomear botão", "Rename button"), AppHost.T("Novo nome:", "New name:"), card.Title);
            if (name == null)
                return;
            string shown = ShownTitle();
            m_Host.RenameButton(card.Id, name);
            AfterEdit(shown);
        }

        private async void OnCardLook(object sender, RoutedEventArgs e)
        {
            CardVM card = Context<CardVM>(sender);
            if (card == null)
                return;
            string owner = OwnerOf(card.Id);
            AppearanceWindow window = new AppearanceWindow(card.Title, Ares.AppCore.ButtonDecor.Get(m_Host.ProjectPath, owner, card.Title),
                Ares.AppCore.ButtonDecor.GetColor(m_Host.ProjectPath, owner, card.Title));
            await window.ShowDialog(this);
            if (!window.Applied)
                return;
            if (window.IconChanged)
                m_Host.SetButtonIcon(card.Id, window.Icon);
            if (window.ColorChanged)
                m_Host.SetButtonColor(card.Id, window.Color);
            AfterEdit(ShownTitle());
        }

        private string OwnerOf(int id)
        {
            foreach (Ares.Data.IMode mode in m_Host.Project.GetModes())
            {
                if (mode.GetElements().Any(el => el.Id == id))
                    return mode.Title;
            }
            return "";
        }

        private void OnCardLeft(object sender, RoutedEventArgs e)
        {
            CardVM card = Context<CardVM>(sender);
            if (card == null)
                return;
            m_Host.MoveButton(card.Id, -1);
            AfterEdit(ShownTitle());
        }

        private void OnCardRight(object sender, RoutedEventArgs e)
        {
            CardVM card = Context<CardVM>(sender);
            if (card == null)
                return;
            m_Host.MoveButton(card.Id, 1);
            AfterEdit(ShownTitle());
        }

        private async void OnCardDelete(object sender, RoutedEventArgs e)
        {
            CardVM card = Context<CardVM>(sender);
            if (card == null)
                return;
            if (!await ConfirmWindow.Ask(this, AppHost.T("Excluir botão", "Delete button"),
                AppHost.T("Excluir o botão \"" + card.Title + "\"? (Os arquivos de áudio não são apagados.)", "Delete the button \"" + card.Title + "\"? (The audio files are not deleted.)")))
                return;
            m_Host.DeleteButton(card.Id);
            AfterEdit(ShownTitle());
        }

        // ---- editing: folders

        private async void OnNewFolder(object sender, RoutedEventArgs e)
        {
            await NewFolder(null);
        }

        private async void OnFolderNewChild(object sender, RoutedEventArgs e)
        {
            FolderVM folder = Context<FolderVM>(sender);
            await NewFolder(folder != null ? folder.Title : null);
        }

        private async System.Threading.Tasks.Task NewFolder(string parent)
        {
            if (m_Host.Project == null)
                return;
            string name = await PromptWindow.Ask(this, parent == null ? AppHost.T("Nova pasta", "New folder") : AppHost.T("Nova subpasta", "New subfolder"),
                AppHost.T("Nome da pasta:", "Folder name:"), "");
            if (name == null)
                return;
            string created = m_Host.NewFolder(name, parent);
            AfterEdit(created);
        }

        private async void OnFolderRename(object sender, RoutedEventArgs e)
        {
            FolderVM folder = Context<FolderVM>(sender);
            if (folder == null)
                return;
            string name = await PromptWindow.Ask(this, AppHost.T("Renomear pasta", "Rename folder"), AppHost.T("Novo nome:", "New name:"), folder.Title);
            if (name == null)
                return;
            string result = m_Host.RenameFolder(folder.Title, name);
            AfterEdit(result);
        }

        private async void OnFolderLook(object sender, RoutedEventArgs e)
        {
            FolderVM folder = Context<FolderVM>(sender);
            if (folder == null)
                return;
            AppearanceWindow window = new AppearanceWindow(folder.Title, Ares.AppCore.ButtonDecor.Get(m_Host.ProjectPath, folder.Title, ""),
                Ares.AppCore.ButtonDecor.GetColor(m_Host.ProjectPath, folder.Title, ""));
            await window.ShowDialog(this);
            if (!window.Applied)
                return;
            if (window.IconChanged)
                m_Host.SetFolderIcon(folder.Title, window.Icon);
            if (window.ColorChanged)
                m_Host.SetFolderColor(folder.Title, window.Color);
            AfterEdit(folder.Title);
        }

        private async void OnFolderDelete(object sender, RoutedEventArgs e)
        {
            FolderVM folder = Context<FolderVM>(sender);
            if (folder == null)
                return;
            string extra = folder.Children.Count > 0 ? AppHost.T("\n(As subpastas dela sobem um nível e continuam existindo.)", "\n(Its subfolders move up one level and stay.)") : "";
            if (!await ConfirmWindow.Ask(this, AppHost.T("Excluir pasta", "Delete folder"),
                AppHost.T("Excluir a pasta \"" + folder.Title + "\" e todas as suas playlists? (Os arquivos de áudio não são apagados.)" + extra,
                          "Delete the folder \"" + folder.Title + "\" and all its playlists? (The audio files are not deleted.)" + extra)))
                return;
            m_Host.DeleteFolder(folder.Title);
            AfterEdit(null);
        }

        private void OnFolderAddHere(object sender, RoutedEventArgs e)
        {
            FolderVM folder = Context<FolderVM>(sender);
            if (folder != null)
                m_Model.SelectedFolder = folder;
            OnImportFiles(sender, e);
        }

        private async void OnPacks(object sender, RoutedEventArgs e)
        {
            PacksWindow window = new PacksWindow(m_Host);
            await window.ShowDialog(this);
            if (window.ImportedProject != null)
                OpenProject(window.ImportedProject);
        }

        private void OnSettings(object sender, RoutedEventArgs e)
        {
            new SettingsWindow(m_Host).ShowDialog(this);
        }

        private void OnQuit(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OnAbout(object sender, RoutedEventArgs e)
        {
            new MessageWindow(AppHost.T("Sobre o Apolo", "About Apolo"),
                AppHost.T("Apolo — controlador de músicas para mesas de RPG.\n\nÉ uma modernização do Ares, de Jörg Ruedenauer, a quem pertence o crédito pela base do programa. Software livre, sob a licença GNU GPL (versão 2 ou posterior). Criado por Igor Teuri (Dados Críticos).",
                          "Apolo — a music controller for tabletop RPG.\n\nIt is a modernization of Ares by Jörg Ruedenauer, who deserves the credit for the foundation of the program. Free software under the GNU GPL (version 2 or later). Created by Igor Teuri (Dados Críticos).")).Show(this);
        }

        // a click on the progress bar jumps to that point of the music
        private void OnBarPressed(object sender, PointerPressedEventArgs e)
        {
            ProgressBar bar = sender as ProgressBar;
            if (bar == null || bar.Bounds.Width <= 0 || m_Model.MusicLength <= 1)
                return;
            double fraction = e.GetPosition(bar).X / bar.Bounds.Width;
            m_Model.SeekMusic(Math.Max(0, Math.Min(1, fraction)) * m_Model.MusicLength);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.O && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                OnOpen(this, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (e.Key == Key.N && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                    OnNewFolder(this, new RoutedEventArgs());
                else
                    OnNewProject(this, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (e.Key == Key.Z && e.KeyModifiers.HasFlag(KeyModifiers.Control) && !(e.Source is TextBox))
            {
                Travel(false);
                e.Handled = true;
            }
            else if (e.Key == Key.Y && e.KeyModifiers.HasFlag(KeyModifiers.Control) && !(e.Source is TextBox))
            {
                Travel(true);
                e.Handled = true;
            }
            else if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && (e.Key == Key.OemPlus || e.Key == Key.Add))
            {
                m_Model.ChangeFolderZoom(10);
                e.Handled = true;
            }
            else if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && (e.Key == Key.OemMinus || e.Key == Key.Subtract))
            {
                m_Model.ChangeFolderZoom(-10);
                e.Handled = true;
            }
            else if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && (e.Key == Key.D0 || e.Key == Key.NumPad0))
            {
                m_Model.SetFolderZoom(100);
                e.Handled = true;
            }
            else if (e.Key == Key.F8)
            {
                OnTogglePanel(this, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (e.Key == Key.P && e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.KeyModifiers.HasFlag(KeyModifiers.Alt))
            {
                OnPad(this, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (e.Key == Key.F9)
            {
                OnToggleTracks(this, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (!(e.Source is TextBox) && m_Host != null && m_Host.Control != null)
            {
                // the keys of the buttons (and: Esc stops everything, Left / Right = previous / next music)
                int code = KeyMap.ToCode(e.Key, e.KeyModifiers);
                if (code != 0 && m_Host.Control.KeyReceived(code))
                {
                    m_Model.Tick();
                    e.Handled = true;
                }
            }
            base.OnKeyDown(e);
        }
    }
}
