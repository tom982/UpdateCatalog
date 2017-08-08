using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Delimon.Win32.IO;
using Newtonsoft.Json;
using UpdateCatalog.Core;

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

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void Process()
        {
            await Task.Run(() =>
            {
                string directory = @"D:\Libraries\Software\wsusoffline\client\w61-x64\glb\input";
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

                Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = 4 }, file =>
                {
                    Update update = Extract.File(file);
                    updates.Add(update);

                    if (string.IsNullOrEmpty(update.Architecture) || string.IsNullOrEmpty(update.KBNumber) || string.IsNullOrEmpty(update.WindowsVersion) || string.IsNullOrEmpty(update.Version) || update.CabFiles.Length == 0)
                        MessageBox.Show(Path.GetFileName(file) + "\r\n" + update.KBNumber + "\r\n" + update.WindowsVersion + "\r\n" + update.Architecture + "\r\n" + update.Version + "\r\n" + update.CabFiles.Length);
                    Interlocked.Increment(ref _volatileCounter);

                    //MessageBox.Show(Path.GetFileName(file) + "\r\n" + update.KBNumber + "\r\n" + update.WindowsVersion + "\r\n" + update.Architecture);

                    lblStatus.Dispatcher.Invoke(() =>
                    {
                        lblStatus.Content = $"{update.KBNumber.ToUpper()} (Windows {update.WindowsVersion} {update.Architecture})";
                    });
                    lblCount.Dispatcher.Invoke(() =>
                    {
                        lblCount.Content = _volatileCounter + "/" + _total;
                    });
                    pbar.Dispatcher.Invoke(() =>
                    {
                        pbar.Value = _volatileCounter;
                    });
                });

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

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            StartTimer();
            Process();
        }
    }
}
