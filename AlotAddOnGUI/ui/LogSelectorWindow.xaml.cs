using ByteSizeLib;
using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace AlotAddOnGUI.ui
{
    /// <summary>
    /// Interaction logic for LogSelectorWindow.xaml
    /// </summary>
    public partial class LogSelectorWindow : MetroWindow, INotifyPropertyChanged
    {
        MainWindow windowRef;
        private bool UserPressedUpload = false;
        public LogSelectorWindow(MainWindow windowRef)
        {
            this.windowRef = windowRef;
            InitializeComponent();
            var directory = new DirectoryInfo("logs");
            var logfiles = directory.GetFiles("alotinstaller*.txt").OrderByDescending(f => f.LastWriteTime).ToList();
            foreach (var file in logfiles)
            {
                Combobox_LogSelector.Items.Add(new LogItem(file.FullName));
            }
            if (Combobox_LogSelector.Items.Count > 0)
            {
                Combobox_LogSelector.SelectedIndex = 0;
            }
            //if (logfiles.Count() > 0)
            //{
            //    var currentTime = DateTime.Now;
            //    string log = "";
            //    //if (currentTime.Date != windowRef.bootTime.Date && logfiles.Count() > 1)
            //    //{
            //    //    //we need to do last 2 files
            //    //    Log.Information("Log file has rolled over since app was booted - including previous days' log.");
            //    //    File.Copy(logfiles.ElementAt(1).FullName, logfiles.ElementAt(1).FullName + ".tmp");
            //    //    log = File.ReadAllText(logfiles.ElementAt(1).FullName + ".tmp");
            //    //    File.Delete(logfiles.ElementAt(1).FullName + ".tmp");
            //    //    log += "\n";
            //    //}
            //    Log.Information("Staging log file for upload. This is the final log item that should appear in an uploaded log.");
            //    File.Copy(logfiles.ElementAt(0).FullName, logfiles.ElementAt(0).FullName + ".tmp");
            //    log += File.ReadAllText(logfiles.ElementAt(0).FullName + ".tmp");
            //    File.Delete(logfiles.ElementAt(0).FullName + ".tmp");
            //}
        }

        private string _watermarkText;
        public string WatermarkText
        {
            get { return _watermarkText; }
            private set
            {
                if (_watermarkText != value)
                {
                    _watermarkText = value;
                    OnPropertyChanged("WatermarkText");
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string GetSelectedLogText()
        {
            if (UserPressedUpload)
            {
                string logpath = ((LogItem)Combobox_LogSelector.SelectedValue).filepath;
                string temppath = logpath + ".tmp";
                File.Copy(logpath, temppath);
                string log = File.ReadAllText(temppath);
                File.Delete(temppath);
                return log;
            }
            return null; //user clicked close X
        }

        private void Button_SelectLog_Click(object sender, RoutedEventArgs e)
        {
            UserPressedUpload = true;
            Close();
        }

        private void Combobox_LogSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            WatermarkText = Combobox_LogSelector.SelectedIndex == 0 ? "Latest log" : "Older log";
        }

        private void OnPropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(propertyName));
        }

        class LogItem
        {
            public string filepath;
            public LogItem(string filepath)
            {
                this.filepath = filepath;
            }

            public override string ToString()
            {
                return System.IO.Path.GetFileName(filepath) + " - " + ByteSize.FromBytes(new FileInfo(filepath).Length);
            }
        }
    }
}
