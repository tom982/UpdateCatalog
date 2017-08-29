using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Newtonsoft.Json;
using UpdateCatalog.Core;
using Delimon.Win32.IO;

namespace UpdateCatalog
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private DispatcherTimer _timer;
        private Stopwatch _sw2;
        private static volatile int _volatileCounter;
        private int _total;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public static string BaseDirectory;
        public static string TempDirectory;
        public static string LinksDirectory;

        public static readonly List<string> Hashes = new List<string>();
        public static readonly Dictionary<string, List<Link>> DownloadLinks = new Dictionary<string, List<Link>>(); 

        public MainWindow()
        {
            InitializeComponent();

            LoadSettings();

            LoadLinks();
            
            // TODO: Download wsus
            // TODO: Extract cab with expand
            // TODO: Extract package.cab with expand
            // TODO: Read links from package.xml with regex
        }

        private void LoadSettings()
        {
            BaseDirectory = Path.GetTempPath() + "UpdateCatalog";
            TempDirectory = BaseDirectory + "\\Temp";
            LinksDirectory = BaseDirectory + "\\Links";

            if (!Directory.Exists(BaseDirectory))
                Directory.CreateDirectory(BaseDirectory);
            if (!Directory.Exists(TempDirectory))
                Directory.CreateDirectory(TempDirectory);
            if (!Directory.Exists(LinksDirectory))
                Directory.CreateDirectory(LinksDirectory);
        }

        private async void LoadLinks()
        {
            // Wsus
            if (!File.Exists(LinksDirectory + "\\wsusscn2.cab"))
            {
                using (WebClient wc = new WebClient())
                {
                    wc.DownloadProgressChanged += client_DownloadProgressChanged;
                    wc.DownloadFileCompleted += client_DownloadFileCompleted;

                    await wc.DownloadFileTaskAsync(new Uri(Urls.Wsus), LinksDirectory + "\\wsusscn2.cab");
                }
            }

            Dispatcher.Invoke(() =>
            {
                lblLinkStatus.Content = "Extracting: wsusscn2.cab";
            });

            Extract.Expand(LinksDirectory + "\\wsusscn2.cab", LinksDirectory, "package.cab");

            Dispatcher.Invoke(() =>
            {
                lblLinkStatus.Content = "Processing: wsusscn2.cab";
            });

            if (!Directory.Exists(LinksDirectory + "\\wsusscn2"))
                Directory.CreateDirectory(LinksDirectory + "\\wsusscn2");

            Extract.Expand(LinksDirectory + "\\wsusscn2\\package.cab", LinksDirectory + "\\wsusscn2");
            File.Copy(LinksDirectory + "\\wsusscn2\\package\\package.xml", LinksDirectory + "\\package.xml", true);
            Tools.EmptyDirectory(LinksDirectory + "\\wsusscn2");
            Directory.Delete(LinksDirectory + "\\wsusscn2", true);

            string packageText = File.ReadAllText(LinksDirectory + "\\package.xml");
            Regex rgx = new Regex(@"http:\/\/([a-z0-9]+[.])*(windowsupdate|microsoft)\.com\/([a-z]{1}\/msdownload\/update\/software\/[a-z]+\/\d{4}\/\d{2}|download\/[a-z0-9]{1}\/[a-z0-9]{1}\/[a-z0-9]{1}\/[a-z0-9]{8}-[a-z0-9]{4}-[a-z0-9]{4}-[a-z0-9]{4}-[a-z0-9]{12})\/windows(?<windowsVersion>\d+\.\d|8-RT|Blue)-(?<kbNumber>KB\d+)(?<version>-v\d+)?-(?<architecture>x64|x86|ia64|arm)(-express)?([_a-z0-9]+)?.(?<filetype>msu|cab|exe)", RegexOptions.IgnoreCase | RegexOptions.Singleline);

            var matches = rgx.Matches(packageText);

            foreach (Match match in matches)
            {
                string kb = match.Groups["kbNumber"].Value.ToLower();
                string url = match.Value.ToLower();

                if (!DownloadLinks.ContainsKey(kb))
                {
                    DownloadLinks.Add(kb, new List<Link>
                    {
                        new Link(url)
                    });   
                }
                else
                {
                    List<Link> oldLinks = DownloadLinks[kb];
                    if (!oldLinks.Select(n => n.Url).Contains(url))
                        oldLinks.Add(new Link(url));
                    DownloadLinks[kb] = oldLinks;
                }

            }

            Dispatcher.Invoke(() =>
            {
                lblLinkStatus.Content = "Ready!";
            });
            // WHD


            // WUD
        }

        void client_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            double bytesIn = double.Parse(e.BytesReceived.ToString());
            double totalBytes = double.Parse(e.TotalBytesToReceive.ToString());
            double percentage = bytesIn / totalBytes * 100;
            pbarlinks.Value = int.Parse(Math.Truncate(percentage).ToString());
        }

        void client_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                lblLinkStatus.Content = "Downloading: wsusscn2.cab";
            });
        }


        private async void Process()
        {
            await Task.Run(() =>
            {
                string directory = @"D:\Libraries\Software\wsusoffline\client\w61-x64\glb";
                string[] msuFiles = Directory.GetFiles(directory, "*.msu", SearchOption.AllDirectories);
                string[] cabFiles = Directory.GetFiles(directory, "*.cab", SearchOption.AllDirectories);
                string[] files = msuFiles.Concat(cabFiles).ToArray();
                _total = files.Length;

                List<Update> updates = new List<Update>();
                Stopwatch sw = new Stopwatch();

                pbar.Dispatcher.Invoke(() =>
                {
                    pbar.Maximum = _total;
                    pbar.Minimum = 0;
                });

                sw.Start();

                ParallelOptions po = new ParallelOptions
                {
                    MaxDegreeOfParallelism = 8,
                    CancellationToken = _cts.Token
                };

                try
                {
                    Parallel.ForEach(files, po, file =>
                    {
                        Update update = Extract.File(file);

                        if (update != null)
                            updates.Add(update);

                        if (string.IsNullOrEmpty(update.Architecture) || string.IsNullOrEmpty(update.KBNumber) || string.IsNullOrEmpty(update.WindowsVersion) || string.IsNullOrEmpty(update.Version) || update.CabFiles.Length == 0)
                            MessageBox.Show(Path.GetFileName(file) + "\r\n" + update.KBNumber + "\r\n" + update.WindowsVersion + "\r\n" + update.Architecture + "\r\n" + update.Version + "\r\n" + update.CabFiles.Length);
                        Interlocked.Increment(ref _volatileCounter);

                        if (!_cts.IsCancellationRequested)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                lblStatus.Content = $"{update.KBNumber.ToUpper()} (Windows {update.WindowsVersion} {update.Architecture})";
                            });
                            Dispatcher.Invoke(() =>
                            {
                                lblCount.Content = _volatileCounter + "/" + _total;
                            });
                            Dispatcher.Invoke(() =>
                            {
                                pbar.Value = _volatileCounter;
                            });
                        }
                    });
                }
                catch (OperationCanceledException)
                {

                }
                finally
                {
                    _cts.Dispose();
                }
                

                sw.Stop();
                _sw2.Stop();
                _timer.Stop();

                dynamic updatesWrapper = new
                {
                    Updates = updates
                };

                string json = JsonConvert.SerializeObject(updatesWrapper);
                string jsonpath = directory + "\\updates.json";

                if (File.Exists(jsonpath))
                    File.Delete(jsonpath);
                File.WriteAllText(jsonpath, json);

                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show("Elapsed: " + sw.ElapsedMilliseconds + "ms\r\nAverage: " + sw.ElapsedMilliseconds / files.Length + "ms/file");
                });
            });
        }


        private void StartTimer()
        {
            _timer = new DispatcherTimer();
            _timer.Tick += timer_Tick;
            _timer.Interval = new TimeSpan(0, 0, 0, 0, 10);
            _sw2 = new Stopwatch();
            _sw2.Start();
            _timer.Start();
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            lblTimer.Content = TimeSpan.FromMilliseconds(_sw2.ElapsedMilliseconds).ToString(@"hh\:mm\:ss\.ff");

            if (_volatileCounter > 5)
                lblETA.Content = TimeSpan.FromMilliseconds((long)(_sw2.ElapsedMilliseconds * (float)_total / _volatileCounter)).ToString(@"m\:ss");
        }

        private async void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            _cts.Cancel();
            await Task.Run(() =>
            {
                while (true)
                {
                    Process[] processes = System.Diagnostics.Process.GetProcessesByName("expand");
                    if (processes.Length == 0)
                        break;
                    Dispatcher.Invoke(() =>
                    {
                        lblStatus.Content = $"Waiting for {processes.Length} expand.exe instance(s) to finish";
                    });
                    Thread.Sleep(100);
                }
            });

            lblStatus.Content = "Operation cancelled";

            ResetWindow();
        }

        private void ResetWindow()
        {
            lblCount.Content = "";
            lblETA.Content = "";
            lblTimer.Content = "";
            _sw2.Stop();
            _timer.Stop();
        }

        private void BtnRun_OnClick(object sender, RoutedEventArgs e)
        {
            StartTimer();
            Process();
            if (File.Exists(BaseDirectory + "\\hashes.txt"))
                File.Delete(BaseDirectory + "\\hashes.txt");
            File.WriteAllLines(BaseDirectory + "\\hashes.txt", Hashes.ToArray());
        }
    }
}
