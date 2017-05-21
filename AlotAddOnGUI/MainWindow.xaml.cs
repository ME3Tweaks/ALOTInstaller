using MahApps.Metro.Controls;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Xml.Linq;

namespace AlotAddOnGUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        private bool Installing = false;
        private readonly BackgroundWorker InstallWorker = new BackgroundWorker();
        private List<AddonFile> addonfiles;

        public MainWindow()
        {
            Log.Logger = new LoggerConfiguration()
                   .MinimumLevel.Debug()
                  .WriteTo.LiterateConsole()
                .WriteTo.RollingFile("logs\\alotaddoninstaller-{Date}.txt")
              .CreateLogger();
            Log.Information("Logger Started for ALOT Installer.");
            Log.Information("Program Version: "+ System.Reflection.Assembly.GetEntryAssembly().GetName().Version);

            InitializeComponent();
            //readManifest();
            DispatcherTimer dt = new DispatcherTimer();
            dt.Tick += new EventHandler(timer_Tick);
            dt.Interval = new TimeSpan(0, 0, 5); // execute every hour
            dt.Start();
            //this.Loaded += new RoutedEventHandler(Window_Loaded);
        }
        // Tick handler    
        private void timer_Tick(object sender, EventArgs e)
        {
            if (Installing)
            {
                return;
            }
            // code to execute periodically
            if (addonfiles != null)
            {
                Console.WriteLine("Checking for files existence...");
                string basepath = System.AppDomain.CurrentDomain.BaseDirectory + @"Downloaded_Mods\";

                foreach (AddonFile af in addonfiles)
                {
                    //Check for file existence
                    Console.WriteLine("Checking for file: " + basepath + af.Filename);
                    af.AssociatedCheckBox.IsEnabled = File.Exists(basepath + af.Filename);
                    if (!af.ExistenceChecked)
                    {
                        af.AssociatedCheckBox.IsChecked = af.AssociatedCheckBox.IsEnabled;
                        af.ExistenceChecked = true;
                    }
                    af.AssociatedCheckBox.ToolTip = af.AssociatedCheckBox.IsEnabled ? "File is downloaded and ready for install" : "Required file is missing: " + af.Filename;
                    //
                }
            }
            //Install_ProgressBar.Value = 30;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //what advantages do I have running code here? 
            await FetchManifest();
            //readManifest();
        }

        private async Task FetchManifest()
        {
            {
                using (WebClient webClient = new WebClient())
                {
                    Log.Information("Fetching latest manifest from github");
                    Install_ProgressBar.IsIndeterminate = true;
                    AddonFilesLabel.Content = "Downloading latest installer manifest";
                    File.Delete(@"manifest.xml");
                    await webClient.DownloadFileTaskAsync("https://raw.githubusercontent.com/Mgamerz/AlotAddOnGUI/master/manifest.xml", @"manifest.xml");
                    Log.Information("Manifest fetched.");
                    readManifest();
                    Log.Information("Manifest read. Switching over to user control");

                    Install_ProgressBar.IsIndeterminate = false;
                    AddonFilesLabel.Content = "Addon Files";
                }
            }
        }

        private void readManifest()
        {
            //if (!File.Exists(@"manifest.xml"))
            //{
            //    await FetchManifest();
            //    return;
            //}
            Log.Information("Reading manifest...");
            XElement rootElement = XElement.Load(@"manifest.xml");

            var elemn1 = rootElement.Elements();

            addonfiles = (from e in rootElement.Elements("addonfile")
                          select new AddonFile
                          {
                              Author = (string)e.Attribute("author"),
                              FriendlyName = (string)e.Attribute("friendlyname"),
                              Game_ME2 = bool.Parse((string)e.Element("games").Attribute("masseffect2")),
                              Game_ME3 = bool.Parse((string)e.Element("games").Attribute("masseffect3")),
                              Filename = (string)e.Element("file").Attribute("filename"),
                              DownloadLink = (string)e.Element("file").Attribute("downloadlink"),
                              ExistenceChecked = false
                          }).ToList();
            foreach (AddonFile addon in addonfiles)
            {
                CheckBox cb = new CheckBox();
                cb.Content = addon.FriendlyName;
                cb.ToolTip = "Required file: " + addon.Filename;

                cb.Margin = new Thickness(10, 10, 10, 10);
                addon.AssociatedCheckBox = cb;
                AddonFilesGrid.Children.Add(cb);
            }
        }

        public class AddonFile
        {
            public string Author { get; set; }
            public string FriendlyName { get; set; }
            public bool Game_ME2 { get; set; }
            public bool Game_ME3 { get; set; }
            public string Filename { get; set; }
            public string DownloadLink { get; set; }
            public CheckBox AssociatedCheckBox { get; set; }
            public bool ExistenceChecked { get; set; }
        }

        private void Button_InstallME2_Click(object sender, RoutedEventArgs e)
        {
            InitInstall();
            ExtractAddons(2);
        }

        private void Button_InstallME3_Click(object sender, RoutedEventArgs e)
        {
            InitInstall();
            ExtractAddons(3);
        }

        private void ExtractAddons(int game)
        {
            string basepath = System.AppDomain.CurrentDomain.BaseDirectory + @"Downloaded_Mods\";
            string destinationpath = System.AppDomain.CurrentDomain.BaseDirectory + @"Extracted_Mods\";
            Directory.CreateDirectory(destinationpath);

            List<AddonFile> addonstoinstall = new List<AddonFile>();
            foreach (AddonFile af in addonfiles)
            {
                if (af.AssociatedCheckBox.IsChecked.Value && game == 2 ? af.Game_ME2 : af.Game_ME3)
                {
                    addonstoinstall.Add(af);
                }

            }

        }

        private void InitInstall()
        {
            Installing = true;
            Button_InstallME2.IsEnabled = false;
            Button_InstallME3.IsEnabled = false;
            AddonFilesLabel.Content = "Preparing to install...";
            HeaderLabel.Text = "Now installing ALOT AddOn. Don't close this window until the process completes. It will take a few minutes to install.";
            // Install_ProgressBar.IsIndeterminate = true;
        }
    }
}
