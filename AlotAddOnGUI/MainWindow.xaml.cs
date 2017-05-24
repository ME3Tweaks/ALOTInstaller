using MahApps.Metro.Controls;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Navigation;
using System.Windows.Threading;
using System.Xml.Linq;

namespace AlotAddOnGUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        public const string UPDATE_OPERATION_LABEL = "UPDATE_OPERATION_LABEL";
        public const string UPDATE_PROGRESSBAR_INDETERMINATE = "SET_PROGRESSBAR_DETERMINACY";
        public const string BINARY_DIRECTORY = "bin\\";

        private bool Installing = false;
        private readonly BackgroundWorker InstallWorker = new BackgroundWorker();
        private List<AddonFile> addonfiles;
        NotifyIcon nIcon = new NotifyIcon();

        public MainWindow()
        {
            Log.Logger = new LoggerConfiguration()
                   .MinimumLevel.Debug()
                  .WriteTo.LiterateConsole()
                .WriteTo.RollingFile("logs\\alotaddoninstaller-{Date}.txt")
              .CreateLogger();

            Log.Information("Logger Started for ALOT Installer.");
            Log.Information("Program Version: " + System.Reflection.Assembly.GetEntryAssembly().GetName().Version);

            InitializeComponent();
            DispatcherTimer dt = new DispatcherTimer();
            dt.Tick += new EventHandler(timer_Tick);
            dt.Interval = new TimeSpan(0, 0, 5); // execute every 5s
            dt.Start();

            InstallWorker.DoWork += InstallAddon;
            InstallWorker.ProgressChanged += InstallProgressChanged;
            InstallWorker.RunWorkerCompleted += InstallCompleted;
            InstallWorker.WorkerReportsProgress = true;

            
        }

        private void InstallCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            HeaderLabel.Text = "Installation completed!";
        }

        private void InstallProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (e.UserState is null)
            {
                Install_ProgressBar.Value = e.ProgressPercentage;
            }
            else
            {
                ThreadCommand tc = (ThreadCommand)e.UserState;
                switch (tc.Command)
                {
                    case UPDATE_OPERATION_LABEL:
                        AddonFilesLabel.Content = (string)tc.Data;
                        break;
                    case UPDATE_PROGRESSBAR_INDETERMINATE:
                        Install_ProgressBar.IsIndeterminate = (bool)tc.Data;
                        break;
                }
            }
        }

        private void InstallAddon(object sender, DoWorkEventArgs e)
        {
            ExtractAddons((int)e.Argument); //arg is game id.
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
                    //Console.WriteLine("Checking for file: " + basepath + af.Filename);

                    //af.AssociatedCheckBox.ToolTip = af.AssociatedCheckBox.IsEnabled ? "File is downloaded and ready for install" : "Required file is missing: " + af.Filename;
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
                    webClient.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);
                    Log.Information("Fetching latest manifest from github");
                    Install_ProgressBar.IsIndeterminate = true;
                    AddonFilesLabel.Content = "Downloading latest installer manifest";
                    File.Delete(@"manifest.xml");
                    await webClient.DownloadFileTaskAsync("https://rawgit.com/Mgamerz/AlotAddOnGUI/master/manifest.xml", @"manifest.xml");
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
            //This is inefficient, but workable since we are using a small dataset.
            lvUsers.ItemsSource = addonfiles;
            CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(lvUsers.ItemsSource);
            PropertyGroupDescription groupDescription = new PropertyGroupDescription("Author");
            view.GroupDescriptions.Add(groupDescription);

            //bool groupfound = false;
            //foreach (AddonFileAuthorGroup group in addonfileauthorgroups)
            //{
            //    if (group.Author.Equals(addon.Author))
            //    {
            //        group.Files.Add(addon);
            //        groupfound = true;
            //        break;
            //    }
            //}
            //if (!groupfound)
            //{
            //    AddonFileAuthorGroup group = new AddonFileAuthorGroup();
            //    group.Author = addon.Author;
            //    group.Files = new List<AddonFile>();
            //    group.Files.Add(addon);
            //    addonfileauthorgroups.Add(group);
            //}

        }

        public class AddonFileAuthorGroup
        {
            public string Author { get; set; }
            public List<AddonFile> Files { get; set; }
        }

        public class AddonFile
        {
            public string Author { get; set; }
            public string FriendlyName { get; set; }
            public bool Game_ME2 { get; set; }
            public bool Game_ME3 { get; set; }
            public string Filename { get; set; }
            public string DownloadLink { get; set; }
            public System.Windows.Controls.CheckBox AssociatedCheckBox { get; set; }
            public bool ExistenceChecked { get; set; }
            public bool SelectedForInstall { get; set; }
        }

        public class ThreadCommand
        {
            public ThreadCommand(string command, object data)
            {
                this.Command = command;
                this.Data = data;
            }
            public string Command;
            public object Data;
        }

        private void Button_InstallME2_Click(object sender, RoutedEventArgs e)
        {
            InitInstall();
            InstallWorker.RunWorkerAsync(2);
        }

        private void Button_InstallME3_Click(object sender, RoutedEventArgs e)
        {
            InitInstall();
            InstallWorker.RunWorkerAsync(3);
        }

        private void ExtractAddons(int game)
        {

            Log.Information("Extracting Addons for Mass Effect " + game);

            string basepath = System.AppDomain.CurrentDomain.BaseDirectory + @"Downloaded_Mods\";
            string destinationpath = System.AppDomain.CurrentDomain.BaseDirectory + @"Extracted_Mods\";
            Log.Information("Created Destination Path");

            Directory.CreateDirectory(destinationpath);

            List<AddonFile> addonstoinstall = new List<AddonFile>();
            foreach (AddonFile af in addonfiles)
            {
                if (af.SelectedForInstall && (game == 2 ? af.Game_ME2 : af.Game_ME3))
                {
                    Log.Information("Adding Addon to installation list: " + af.FriendlyName);
                    addonstoinstall.Add(af);
                }
            }

            int completed = 0;
            InstallWorker.ReportProgress(completed, new ThreadCommand(UPDATE_OPERATION_LABEL, "Extracting Mods..."));
            foreach (AddonFile af in addonstoinstall)
            {
                Log.Information("Processing extraction on " + af.Filename);

                string fileextension = System.IO.Path.GetExtension(af.Filename);

                switch (fileextension)
                {
                    case ".7z":
                    case ".zip":
                    case ".rar":
                        {
                            string exe = BINARY_DIRECTORY + "7z.exe";
                            string args = "x Downloaded_Mods\\" + af.Filename + " -aoa -r -oExtracted_Mods\\" + System.IO.Path.GetFileNameWithoutExtension(af.Filename);
                            runProcess(exe, args);
                            completed++;
                            int progress = (int)((float)completed / (float)addonstoinstall.Count * 100);
                            InstallWorker.ReportProgress(progress);
                            break;
                        }
                    case ".tpf":
                        {

                            File.Copy("Downloaded_Mods\\" + af.Filename, "Extracted_Mods\\" + af.Filename, true);
                            completed++;
                            int progress = (int)((float)completed / (float)addonstoinstall.Count * 100);
                            InstallWorker.ReportProgress(progress);
                            break;
                        }
                    case ".mod":
                        break;
                }
            }
            InstallWorker.ReportProgress(completed, new ThreadCommand(UPDATE_OPERATION_LABEL, "Extracting TPFs..."));
            Thread.Sleep(2000);

            InstallWorker.ReportProgress(completed, new ThreadCommand(UPDATE_OPERATION_LABEL, "Extracting MOD files..."));
            Thread.Sleep(3000);

            InstallWorker.ReportProgress(completed, new ThreadCommand(UPDATE_OPERATION_LABEL, "Removing Duplicates..."));
            Thread.Sleep(7000);

            InstallWorker.ReportProgress(completed, new ThreadCommand(UPDATE_OPERATION_LABEL, "Preparing to create MEM package..."));
            Thread.Sleep(5000);
            InstallWorker.ReportProgress(completed, new ThreadCommand(UPDATE_OPERATION_LABEL, "Creating MEM Package..."));
            InstallWorker.ReportProgress(completed, new ThreadCommand(UPDATE_PROGRESSBAR_INDETERMINATE, true));
        }

        private int runProcess(string exe, string args)
        {
            Log.Information("Running process: " + exe + " " + args);
            Process p = new Process();
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.FileName = exe;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.Arguments = args;
            p.Start();
            p.WaitForExit();
            Thread.Sleep(1500);
            return p.ExitCode;
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(e.Uri.ToString());
            this.nIcon.Visible = true;
            //this.WindowState = System.Windows.WindowState.Minimized;
            this.nIcon.Icon = new Icon(@"../../images/info.ico");
            string fname = (string) ((Hyperlink)e.Source).Tag;
            this.nIcon.ShowBalloonTip(14000, "Downloading ALOT Addon File", "Download the file named \""+fname+"\"", ToolTipIcon.Info);
        }

        private void Downloadlink_Clicked(object sender, RoutedEventArgs e)
        {
            this.nIcon.Visible = true;
            //this.WindowState = System.Windows.WindowState.Minimized;
            this.nIcon.Icon = new Icon(@"../../images/info.ico");
            this.nIcon.ShowBalloonTip(14000, "Downloading ALOT Addon File", "Download the file named XXX", ToolTipIcon.Info);
        }

        private void InitInstall()
        {
            Installing = true;
            Button_InstallME2.IsEnabled = false;
            Button_InstallME3.IsEnabled = false;
            AddonFilesLabel.Content = "Preparing to install...";
            HeaderLabel.Text = "Now installing ALOT AddOn. Don't close this window until the process completes. It will take a few minutes to install.";
            foreach (AddonFile af in addonfiles)
            {
                af.SelectedForInstall = af.AssociatedCheckBox.IsChecked.Value;
                af.AssociatedCheckBox.IsEnabled = false; //disable clicks
            }
            // Install_ProgressBar.IsIndeterminate = true;
        }
    }
}
