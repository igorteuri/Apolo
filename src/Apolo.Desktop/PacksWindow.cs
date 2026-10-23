using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ares.Player;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;

namespace Apolo.Desktop
{
    /// <summary>The ready-made content packs (the list is on a release of the repository, see docs/PACOTES.md): choose, download, open.</summary>
    public sealed class PacksWindow : Window
    {
        // the release "pacotes" of the repository holds pacotes.ini and the .apkg files
        public const string CatalogUrl = "https://github.com/igorteuri/Apolo/releases/download/pacotes/pacotes.ini";

        private readonly AppHost m_Host;
        private readonly ListBox m_List = new ListBox();
        private readonly TextBlock m_Info = new TextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        private readonly TextBlock m_Status = new TextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        private readonly ProgressBar m_Bar = new ProgressBar { Minimum = 0, Maximum = 100, Height = 8 };
        private readonly Button m_Download = new Button { IsEnabled = false, Padding = new Thickness(18, 6) };
        private readonly List<PackInfo> m_Packs = new List<PackInfo>();
        private readonly CancellationTokenSource m_Cancel = new CancellationTokenSource();
        private bool m_Busy;

        /// <summary>The packs found in the list (for the tests).</summary>
        public IList<PackInfo> Packs { get { return m_Packs; } }

        /// <summary>The list could not be read (no internet, ...).</summary>
        public bool Failed { get; private set; }

        /// <summary>The project file of the pack which was downloaded and unpacked (null if none).</summary>
        public string ImportedProject { get; private set; }

        public PacksWindow(AppHost host)
        {
            m_Host = host;
            Title = AppHost.T("Pacotes de conteúdo prontos", "Ready-made content packs");
            Width = 600;
            Height = 520;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            CanResize = false;

            m_Download.Content = AppHost.T("Baixar e abrir", "Download and open");
            m_Status.Text = AppHost.T("Procurando os pacotes...", "Looking for the packs...");
            Grid grid = new Grid { Margin = new Thickness(18), RowDefinitions = new RowDefinitions("Auto,150,*,Auto,Auto,Auto") };
            grid.Children.Add(new TextBlock
            {
                Text = AppHost.T("Escolha um pacote para baixar. Ele vira um projeto novo, com as pastas, os botões, as imagens e as músicas.",
                    "Choose a pack to download. It becomes a new project, with the folders, buttons, pictures and music."),
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            });
            Grid.SetRow(m_List, 1);
            grid.Children.Add(m_List);
            ScrollViewer infoScroll = new ScrollViewer { Content = m_Info, Margin = new Thickness(0, 10, 0, 6) };
            Grid.SetRow(infoScroll, 2);
            grid.Children.Add(infoScroll);
            Grid.SetRow(m_Status, 3);
            grid.Children.Add(m_Status);
            m_Bar.Margin = new Thickness(0, 6, 0, 10);
            Grid.SetRow(m_Bar, 4);
            grid.Children.Add(m_Bar);
            Button close = new Button { Content = AppHost.T("Fechar", "Close"), Padding = new Thickness(18, 6), HorizontalAlignment = HorizontalAlignment.Right };
            close.Click += (s, e) => Close();
            Grid buttons = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
            buttons.Children.Add(m_Download);
            Grid.SetColumn(close, 2);
            buttons.Children.Add(close);
            Grid.SetRow(buttons, 5);
            grid.Children.Add(buttons);
            Content = grid;

            m_List.SelectionChanged += (s, e) =>
            {
                PackInfo p = Selected();
                m_Info.Text = p == null ? "" : p.Description.Replace("\\n", "\n");
                m_Download.IsEnabled = p != null && !m_Busy;
            };
            m_Download.Click += (s, e) => Download();
            Opened += (s, e) => LoadCatalog();
            Closing += (s, e) =>
            {
                if (m_Busy)
                    e.Cancel = true;   // the download is still running
                else
                    m_Cancel.Cancel();
            };
        }

        /// <summary>Starts the download of a pack of the list (for the tests).</summary>
        public void StartDownloadForTest(int index)
        {
            m_List.SelectedIndex = index;
            Download();
        }

        /// <summary>What the window says about the download (for the tests).</summary>
        public string StatusText { get { return m_Status.Text; } }

        private PackInfo Selected()
        {
            return m_List.SelectedIndex >= 0 && m_List.SelectedIndex < m_Packs.Count ? m_Packs[m_List.SelectedIndex] : null;
        }

        private async void LoadCatalog()
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("Apolo");
                    string text = await client.GetStringAsync(CatalogUrl, m_Cancel.Token);
                    m_Packs.AddRange(PackCatalog.Parse(text, CatalogUrl));
                }
                m_List.ItemsSource = m_Packs.Select(p =>
                {
                    string size = PackCatalog.FormatSize(p.Bytes);
                    return size.Length > 0 ? p.Title + "   (" + size + ")" : p.Title;
                }).ToList();
                m_Status.Text = m_Packs.Count > 0 ? AppHost.T("Escolha um pacote da lista.", "Choose a pack from the list.")
                    : AppHost.T("Nenhum pacote disponível por enquanto.", "No packs available yet.");
                if (m_Packs.Count > 0)
                    m_List.SelectedIndex = 0;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Failed = true;
                m_Status.Text = AppHost.T("Não consegui buscar a lista de pacotes. Confira a internet e tente de novo. (", "Could not get the list of packs. Check the internet and try again. (")
                    + ex.Message + ")";
            }
        }

        private async void Download()
        {
            PackInfo pack = Selected();
            if (pack == null || m_Busy)
                return;
            m_Busy = true;
            m_Download.IsEnabled = false;
            m_List.IsEnabled = false;
            string temp = Path.Combine(Path.GetTempPath(), "apolo-pacote-" + PackCatalog.SafeName(pack.Id) + ".apkg.part");
            try
            {
                m_Status.Text = AppHost.T("Baixando...", "Downloading...");
                m_Bar.Value = 0;
                CancellationToken token = m_Cancel.Token;
                string address = pack.Url;
                long expected = pack.Bytes;
                // the download runs away from the window; the window is only told about each percent
                await Task.Run(async () =>
                {
                    using (HttpClient client = new HttpClient { Timeout = TimeSpan.FromHours(2) })
                    {
                        client.DefaultRequestHeaders.UserAgent.ParseAdd("Apolo");
                        using (HttpResponseMessage response = await client.GetAsync(address, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false))
                        {
                            response.EnsureSuccessStatusCode();
                            long total = response.Content.Headers.ContentLength ?? expected;
                            using (Stream source = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
                            using (FileStream target = File.Create(temp))
                            {
                                byte[] buffer = new byte[262144];
                                long done = 0;
                                int last = -1;
                                int n;
                                while ((n = await source.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) > 0)
                                {
                                    await target.WriteAsync(buffer, 0, n, token).ConfigureAwait(false);
                                    done += n;
                                    int percent = total > 0 ? (int)(done * 100 / total) : 0;
                                    if (percent != last)
                                    {
                                        last = percent;
                                        int shown = percent;
                                        Dispatcher.UIThread.Post(() =>
                                        {
                                            m_Bar.Value = shown;
                                            m_Status.Text = AppHost.T("Baixando... ", "Downloading... ") + shown + "%";
                                        });
                                    }
                                }
                            }
                        }
                    }
                }, token);
                string problem = PackCatalog.Verify(temp, pack, AppHost.IsPt);
                if (problem != null)
                {
                    TryDelete(temp);
                    m_Status.Text = problem;
                    return;
                }
                m_Status.Text = AppHost.T("Abrindo o pacote...", "Unpacking...");
                TaskCompletionSource<Exception> done2 = new TaskCompletionSource<Exception>();
                m_Host.Import(temp, error => done2.TrySetResult(error));
                Exception importError = await done2.Task;
                TryDelete(temp);
                if (importError != null)
                {
                    m_Status.Text = AppHost.T("Não consegui abrir o pacote: ", "Could not unpack the pack: ") + importError.Message;
                    return;
                }
                ImportedProject = m_Host.ImportedProject;
                m_Busy = false;
                Close();
                return;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                TryDelete(temp);
                m_Status.Text = AppHost.T("O download não terminou. Tente de novo. ", "The download did not finish. Try again. ") + ex.Message;
            }
            finally
            {
                m_Busy = false;
                m_List.IsEnabled = true;
                m_Download.IsEnabled = Selected() != null;
            }
        }

        private static void TryDelete(string file)
        {
            try { if (File.Exists(file)) File.Delete(file); } catch (Exception) { }
        }
    }
}
