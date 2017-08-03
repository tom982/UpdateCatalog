using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
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
        public MainWindow()
        {
            InitializeComponent();

            string[] files = Directory.GetFiles("D:\\UC\\input", "*.msu");
            List<Update> updates = new List<Update>();
            Stopwatch sw = new Stopwatch();

            sw.Start();

            bool parallel = true;

            if (parallel)
            {
                Parallel.ForEach(files, file =>
                {
                    updates.Add(Extract.File(file));
                });
            }
            else
            {
                foreach (string file in files)
                    updates.Add(Extract.File(file));
            }

            Parallel.ForEach(files, file =>
            {
                updates.Add(Extract.File(file));
            });

            sw.Stop();

            Clipboard.SetText(JsonConvert.SerializeObject(updates));

            MessageBox.Show("Elapsed: " + sw.ElapsedMilliseconds + "ms\r\nAverage: " + sw.ElapsedMilliseconds / files.Length + "ms/file");
        }
    }
}
