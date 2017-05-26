using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        private BindingList<AddonFile> addonfiles;
        NotifyIcon nIcon = new NotifyIcon();
        private const string MEM_OUTPUT_DIR = "MEM_Packages";
        private const string MEM_STAGING_DIR = "MEM_PACKAGE_STAGING";
        private string EXE_DIRECTORY = System.AppDomain.CurrentDomain.BaseDirectory;

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
        }

        private void InstallCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            HeaderLabel.Text = "Process completed";
            AddonFilesLabel.Content = "MEM Packages placed in the " + MEM_OUTPUT_DIR + " folder";
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
                string basepath = EXE_DIRECTORY + @"Downloaded_Mods\";
                int numdone = 0;
                foreach (AddonFile af in addonfiles)
                {
                    bool ready = File.Exists(basepath + af.Filename);
                    if (af.Ready != ready)
                    {
                        af.Ready = ready;
                    }
                    numdone += ready ? 1 : 0;
                    System.Windows.Application.Current.Dispatcher.Invoke(
                    () =>
                    {
                        // Code to run on the GUI thread.
                        Install_ProgressBar.Value = (int)(((double)numdone / addonfiles.Count) * 100);
                    });
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
            string me3map = Environment.GetEnvironmentVariable("LocalAppData") + @"\MassEffectModder\me3map.bin";
            string me2map = Environment.GetEnvironmentVariable("LocalAppData") + @"\MassEffectModder\me2map.bin";

            Log.Information("ME2 Texture Map exists: " + File.Exists(me2map));
            Log.Information("ME3 Texture Map exists: " + File.Exists(me3map));
            if (File.Exists(me2map) || File.Exists(me3map))
            {
                DispatcherTimer dt = new DispatcherTimer();
                dt.Tick += new EventHandler(timer_Tick);
                dt.Interval = new TimeSpan(0, 0, 5); // execute every 5s
                dt.Start();

                InstallWorker.DoWork += InstallAddon;
                InstallWorker.ProgressChanged += InstallProgressChanged;
                InstallWorker.RunWorkerCompleted += InstallCompleted;
                InstallWorker.WorkerReportsProgress = true;
                await FetchManifest();

                if (!File.Exists(me2map))
                {
                    Log.Information("ME2 Texture Map missing - disabling ME2 install");
                    Button_InstallME2.IsEnabled = false;
                    Button_InstallME2.ToolTip = "Mass Effect 2 Texture Map not found. To install ALOT for Mass Effect 2 a texture map must be created.";
                    Button_InstallME2.Content = "ME2 Texture Map Missing";
                }
                if (!File.Exists(me3map))
                {
                    Log.Information("ME3 Texture Map missing - disabling ME3 install");
                    Button_InstallME3.IsEnabled = false;
                    Button_InstallME3.ToolTip = "Mass Effect 3 Texture Map not found. To install ALOT for Mass Effect 3 a texture map must be created.";
                    Button_InstallME3.Content = "ME3 Texture Map Missing";
                }
            }
            else
            {
                await this.ShowMessageAsync("No ME2/ME3 Texture Maps Found", "ALOT Addon Builder requires you to build a texture map for ME2 or ME3 before you can use it.\nOne will be created during the main ALOT installation process.");
                Environment.Exit(1);
            }

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
                    timer_Tick(null, null);
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
            var linqlist = (from e in rootElement.Elements("addonfile")
                            select new AddonFile
                            {
                                Author = (string)e.Attribute("author"),
                                FriendlyName = (string)e.Attribute("friendlyname"),
                                Game_ME2 = bool.Parse((string)e.Element("games").Attribute("masseffect2")),
                                Game_ME3 = bool.Parse((string)e.Element("games").Attribute("masseffect3")),
                                Filename = (string)e.Element("file").Attribute("filename"),
                                DownloadLink = (string)e.Element("file").Attribute("downloadlink"),
                                Ready = false,
                                PackageFiles = e.Elements("packagefile")
                                   .Select(r => new PackageFile
                                   {
                                       SourceName = (string)r.Attribute("sourcename"),
                                       DestinationName = (string)r.Attribute("destinationname"),
                                   }).ToList()
                            }).ToList();
            addonfiles = new BindingList<AddonFile>(linqlist);
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

        public sealed class AddonFile : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            private bool m_ready;

            public string Author { get; set; }
            public string FriendlyName { get; set; }
            public bool Game_ME2 { get; set; }
            public bool Game_ME3 { get; set; }
            public string Filename { get; set; }
            public string DownloadLink { get; set; }
            public List<String> Duplicates { get; set; }
            public List<PackageFile> PackageFiles { get; set; }

            public bool Ready
            {

                get { return m_ready; }
                set
                {
                    m_ready = value;
                    OnPropertyChanged(string.Empty);
                }
            }

            private void OnPropertyChanged(string propertyName)
            {
                var handler = PropertyChanged;
                if (handler != null)
                    handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public class PackageFile
        {
            public string SourceName { get; set; }
            public string DestinationName { get; set; }
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
            Button_InstallME2.Content = "Building...";
            InstallWorker.RunWorkerAsync(2);
        }

        private void Button_InstallME3_Click(object sender, RoutedEventArgs e)
        {
            InitInstall();
            Button_InstallME3.Content = "Building...";
            InstallWorker.RunWorkerAsync(3);
        }

        private void ExtractAddons(int game)
        {

            Log.Information("Extracting Addons for Mass Effect " + game);

            string basepath = EXE_DIRECTORY + @"Downloaded_Mods\";
            string destinationpath = EXE_DIRECTORY + @"Extracted_Mods\";
            Log.Information("Created Destination Path");

            Directory.CreateDirectory(destinationpath);

            List<AddonFile> addonstoinstall = new List<AddonFile>();
            foreach (AddonFile af in addonfiles)
            {
                if (af.Ready && (game == 2 ? af.Game_ME2 : af.Game_ME3))
                {
                    Log.Information("Adding Addon to installation list: " + af.FriendlyName);
                    addonstoinstall.Add(af);
                }
            }

            int completed = 0;
            InstallWorker.ReportProgress(completed, new ThreadCommand(UPDATE_OPERATION_LABEL, "Extracting Mods..."));
            bool modextractrequired = false;
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
                            Log.Information("Extracting file: " + af.Filename);
                            string exe = BINARY_DIRECTORY + "7z.exe";
                            string extractpath = EXE_DIRECTORY + "Extracted_Mods\\" + System.IO.Path.GetFileNameWithoutExtension(af.Filename);
                            string args = "x \"" + EXE_DIRECTORY + "Downloaded_Mods\\" + af.Filename + "\" -aoa -r -o\"" + extractpath + "\"";
                            runProcess(exe, args);

                            exe = BINARY_DIRECTORY + "MassEffectModder.exe";
                            args = "-extract-tpf \"" + extractpath + "\" \"" + extractpath + "\"";
                            runProcess(exe, args);


                            List<string> files = new List<string>();
                            foreach (string file in Directory.EnumerateFiles(extractpath, "*.dds", SearchOption.AllDirectories))
                            {
                                files.Add(file);
                            }

                            foreach (string file in files)
                            {
                                Log.Information("Deleting existing file (if any): " + extractpath + "\\" + Path.GetFileName(file));
                                string destination = extractpath + "\\" + Path.GetFileName(file);
                                File.Delete(destination);
                                Log.Information(file + " -> " + destination);
                                File.Move(file, destination);
                            }


                            completed++;
                            int progress = (int)((float)completed / (float)addonstoinstall.Count * 100);
                            InstallWorker.ReportProgress(progress);
                            break;
                        }
                    case ".tpf":
                        {
                            string source = EXE_DIRECTORY + "Downloaded_Mods\\" + af.Filename;
                            string destination = EXE_DIRECTORY + "Extracted_Mods\\" + Path.GetFileName(af.Filename);
                            File.Copy(source, destination, true);

                            completed++;
                            int progress = (int)((float)completed / (float)addonstoinstall.Count * 100);
                            InstallWorker.ReportProgress(progress);
                            break;
                        }
                    case ".mod":
                        {
                            modextractrequired = true;
                            completed++;
                            int progress = (int)((float)completed / (float)addonstoinstall.Count * 100);
                            InstallWorker.ReportProgress(progress);
                            break;
                        }
                    case ".mem":
                        {
                            //Copy to output folder
                            File.Copy(EXE_DIRECTORY + "Downloaded_Mods\\" + af.Filename, EXE_DIRECTORY + MEM_OUTPUT_DIR + "\\" + af.Filename, true);
                            completed++;
                            int progress = (int)((float)completed / (float)addonstoinstall.Count * 100);
                            InstallWorker.ReportProgress(progress);
                            break;
                        }
                }
            }

            //if (tpfextractrequired)
            {
                InstallWorker.ReportProgress(completed, new ThreadCommand(UPDATE_PROGRESSBAR_INDETERMINATE, true));
                InstallWorker.ReportProgress(completed, new ThreadCommand(UPDATE_OPERATION_LABEL, "Extracting TPFs..."));
                InstallWorker.ReportProgress(0);

                Log.Information("Extracting TPF files.");
                string exe = BINARY_DIRECTORY + "MassEffectModder.exe";
                string args = "-extract-tpf \"" + EXE_DIRECTORY + "Extracted_Mods\" \"" + EXE_DIRECTORY + "Extracted_Mods\"";
                runProcess(exe, args);
            }

            InstallWorker.ReportProgress(completed, new ThreadCommand(UPDATE_OPERATION_LABEL, "Extracting MOD files..."));
            if (modextractrequired)
            {
                InstallWorker.ReportProgress(completed, new ThreadCommand(UPDATE_PROGRESSBAR_INDETERMINATE, true));
                InstallWorker.ReportProgress(completed, new ThreadCommand(UPDATE_OPERATION_LABEL, "Extracting MOD files..."));
                InstallWorker.ReportProgress(0);

                Log.Information("Extracting MOD files.");
                string exe = BINARY_DIRECTORY + "MassEffectModder.exe";
                string args = "-extract-mod " + game + " \"" + EXE_DIRECTORY + "Downloaded_Mods\" \"" + EXE_DIRECTORY + "Extracted_Mods\"";
                runProcess(exe, args);
            }

            //InstallWorker.ReportProgress(completed, new ThreadCommand(UPDATE_OPERATION_LABEL, "Removing Duplicates..."));
            //Thread.Sleep(7000);

            InstallWorker.ReportProgress(completed, new ThreadCommand(UPDATE_OPERATION_LABEL, "Preparing to create MEM package..."));
            InstallWorker.ReportProgress(completed, new ThreadCommand(UPDATE_PROGRESSBAR_INDETERMINATE, false));

            //Calculate how many files to install...
            int totalfiles = 0;
            foreach (AddonFile af in addonstoinstall)
            {
                totalfiles += af.PackageFiles.Count;
            }

            basepath = EXE_DIRECTORY + @"Extracted_Mods\";
            string destbasepath = System.AppDomain.CurrentDomain.BaseDirectory + MEM_STAGING_DIR + "\\";
            Directory.CreateDirectory(destbasepath);
            int numcompleted = 0;
            foreach (AddonFile af in addonstoinstall)
            {
                if (af.PackageFiles.Count > 0)
                {
                    foreach (PackageFile pf in af.PackageFiles)
                    {
                        Log.Information("Copying Package File: " + pf.SourceName + "->" + pf.DestinationName);
                        string extractedpath = basepath + Path.GetFileNameWithoutExtension(af.Filename) + "\\" + pf.SourceName;
                        string destination = destbasepath + pf.DestinationName;
                        File.Copy(extractedpath, destination, true);
                        numcompleted++;
                        int progress = (int)((float)numcompleted / (float)totalfiles * 100);
                        InstallWorker.ReportProgress(progress);
                        //  Thread.Sleep(1000);
                    }
                }
            }


            InstallWorker.ReportProgress(0);

            InstallWorker.ReportProgress(completed, new ThreadCommand(UPDATE_OPERATION_LABEL, "Building Addon MEM Package..."));
            InstallWorker.ReportProgress(completed, new ThreadCommand(UPDATE_PROGRESSBAR_INDETERMINATE, true));
            {
                Log.Information("Building MEM Package.");
                string exe = BINARY_DIRECTORY + "MassEffectModder.exe";
                string args = "-convert-to-mem " + game + " \"" + EXE_DIRECTORY + MEM_STAGING_DIR + "\" \"" + EXE_DIRECTORY + MEM_OUTPUT_DIR + "\\ALOT_ME" + game + "_Addon.mem";
                runProcess(exe, args);
            }
            //Directory.Delete(MEM_STAGING_DIR, true);
            //Directory.Delete("Extracted_Mods",true);
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
            //Thread.Sleep(1500);
            return p.ExitCode;
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(e.Uri.ToString());
            this.nIcon.Visible = true;
            //this.WindowState = System.Windows.WindowState.Minimized;
            this.nIcon.Icon = new Icon(@"../../images/info.ico");
            string fname = (string)((Hyperlink)e.Source).Tag;
            this.nIcon.ShowBalloonTip(14000, "Downloading ALOT Addon File", "Download the file named \"" + fname + "\"", ToolTipIcon.Info);
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

            Log.Information("Deleting any pre-existing Extracted_Mods folder.");
            string destinationpath = System.AppDomain.CurrentDomain.BaseDirectory + @"Extracted_Mods\";
            if (Directory.Exists(destinationpath))
            {
                Directory.Delete(destinationpath, true);
            }

            Directory.CreateDirectory(MEM_OUTPUT_DIR);
            Directory.CreateDirectory(MEM_STAGING_DIR);

            AddonFilesLabel.Content = "Preparing to install...";
            HeaderLabel.Text = "Now installing ALOT AddOn. Don't close this window until the process completes. It will take a few minutes to install.";
            // Install_ProgressBar.IsIndeterminate = true;
        }

        private void File_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);

                // Assuming you have one file that you care about, pass it off to whatever
                // handling code you have defined.
                foreach (string file in files)
                {
                    string fname = Path.GetFileName(file);
                    foreach (AddonFile af in addonfiles)
                    {
                        if (af.Filename.Equals(fname, StringComparison.InvariantCultureIgnoreCase) && af.Ready == false)
                        {
                            //Copy file to directory
                            string basepath = System.AppDomain.CurrentDomain.BaseDirectory + @"Downloaded_Mods\";
                            string destination = basepath + af.Filename;
                            Log.Information("Copying dragged file to downloaded mods directory: " + file);
                            File.Copy(file, destination, true);
                            timer_Tick(null, null);
                            break;
                        }
                    }
                }
            }
        }
    }
}
