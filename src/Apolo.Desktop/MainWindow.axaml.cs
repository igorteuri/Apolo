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
            base.OnKeyDown(e);
        }
    }
}
