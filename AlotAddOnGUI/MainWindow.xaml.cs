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
using System.Management;
using System.Net;
using System.Reflection;
using System.Text;
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
        private AnonymousPipes pipe;
        ProgressDialogController updateprogresscontroller;
        public const string UPDATE_OPERATION_LABEL = "UPDATE_OPERATION_LABEL";
        public const string UPDATE_PROGRESSBAR_INDETERMINATE = "SET_PROGRESSBAR_DETERMINACY";
        public const string INCREMENT_COMPLETION_EXTRACTION = "INCREMENT_COMPLETION_EXTRACTION";
        public const string SHOW_DEBUG_DIALOG = "SHOW_DEBUG_DIALOG";
        public const string ERROR_OCCURED = "ERROR_OCCURED";
        public const string BINARY_DIRECTORY = "bin\\";
        private bool errorOccured = false;

        private DispatcherTimer backgroundticker;
        private int completed = 0;
        //private int addonstoinstall = 0;
        private int CURRENT_GAME_BUILD = 0; //set when extraction is run/finished
        private int ADDONSTOINSTALL_COUNT = 0;
        private bool Installing = false;
        private readonly BackgroundWorker InstallWorker = new BackgroundWorker();
        private BindingList<AddonFile> addonfiles;
        NotifyIcon nIcon = new NotifyIcon();
        private const string MEM_OUTPUT_DIR = "MEM_Packages";
        private const string MEM_OUTPUT_DISPLAY_DIR = "MEM__Packages";

        private const string MEM_STAGING_DIR = "MEM_PACKAGE_STAGING";
        private string EXE_DIRECTORY = System.AppDomain.CurrentDomain.BaseDirectory;
        private string STAGING_DIRECTORY = System.AppDomain.CurrentDomain.BaseDirectory + MEM_STAGING_DIR + "\\";

        public MainWindow()
        {
            InitializeComponent();
            Title = "ALOT Addon Builder " + System.Reflection.Assembly.GetEntryAssembly().GetName().Version;
            HeaderLabel.Text = "Preparing application...";
        }

        private void RunUpdater()
        {
            Log.Information("Running GHUpdater.");
            AddonFilesLabel.Content = "Checking for application updates";
            string updaterpath = EXE_DIRECTORY + BINARY_DIRECTORY + "GHUpdater.exe";
            string temppath = Path.GetTempPath() + "GHUpdater.exe";
            var versInfo = FileVersionInfo.GetVersionInfo(updaterpath);
            String fileVersion = versInfo.FileVersion;
            Log.Information("GHUpdater.exe version: " + fileVersion);

            File.Copy(updaterpath, temppath, true);
            pipe = new AnonymousPipes("GHUPDATE_SERVER.", temppath, "", delegate (String msg)
            {
                Dispatcher.Invoke((MethodInvoker)async delegate ()
                {
                    //UI THREAD
                    string[] clientmessage = msg.Split();
                    switch (clientmessage[0])
                    {
                        case "UPDATE_DOWNLOAD_COMPLETE":
                            if (updateprogresscontroller != null)
                            {
                                //updateprogresscontroller.SetTitle("Update ready to install");
                                //updateprogresscontroller.SetMessage("Update will install in 5 seconds.");
                                await this.ShowMessageAsync("ALOT Addon Builder update ready", "The program will close and update in the background, then reopen. It should only take a few seconds.");
                                string updatemessage = "EXECUTE_UPDATE_AND_START_PROCESS \"" + System.AppDomain.CurrentDomain.FriendlyName + "\" \"" + EXE_DIRECTORY + "\"";
                                Log.Information("Executing update: " + updatemessage);
                                pipe.SendText(updatemessage);
                                Environment.Exit(0);
                            }
                            break;
                        case "UPDATE_DOWNLOAD_PROGRESS":
                            if (clientmessage.Length != 2)
                            {
                                Log.Warning("UPDATE_DOWNLOAD_PROGRESS message was not length 2 - ignoring message");
                                return;
                            }
                            if (updateprogresscontroller != null)
                            {
                                double value = Double.Parse(clientmessage[1]);
                                updateprogresscontroller.SetProgress(value);
                            }
                            break;
                        case "UP_TO_DATE":
                            Log.Information("GHUpdater reporting app is up to date");
                            Thread.Sleep(250);
                            try
                            {
                                File.Delete(temppath);
                            }
                            catch (Exception e)
                            {
                                Log.Error("Error deleting TEMP GHUpdater.exe: " + e.ToString());
                            }
                            await FetchManifest();
                            break;
                        case "ERROR_CHECKING_FOR_UPDATES":
                            AddonFilesLabel.Content = "Error occured checking for updates";
                            await FetchManifest();
                            break;
                        case "UPDATE_AVAILABLE":
                            if (clientmessage.Length != 2)
                            {
                                Log.Warning("UPDATE_AVAILABLE message was not length 2 - ignoring message");
                                return;
                            }
                            Log.Information("Github Updater reports program update: " + clientmessage[1] + " is available.");
                            MessageDialogResult result = await this.ShowMessageAsync("Update Available", "ALOT Addon Builder " + clientmessage[1] + " is available. Install the update?", MessageDialogStyle.AffirmativeAndNegative);
                            if (result == MessageDialogResult.Affirmative)
                            {
                                pipe.SendText("INITIATE_DOWNLOAD");
                                updateprogresscontroller = await this.ShowProgressAsync("Installing Update", "ALOT Addon Builder is updating. Please wait...");
                                updateprogresscontroller.SetIndeterminate();
                            }
                            else
                            {
                                pipe.SendText("KILL_UPDATER");
                                pipe.Close();
                                try
                                {
                                    File.Delete(temppath);
                                }
                                catch (Exception e)
                                {
                                    Log.Error("Error deleting TEMP GHUpdater.exe: " + e.ToString());
                                }

                                await FetchManifest();
                            }
                            break;
                        default:
                            Log.Error("Unknown message from updater client: " + msg);
                            break;
                    }
                });
            }, delegate ()
            {
                // We're disconnected!
                try
                {

                    Dispatcher.Invoke((MethodInvoker)delegate ()
                    {
                        //UITHREAD
                        var source = PresentationSource.FromVisual(this);
                        if (source == null || source.IsDisposed)
                        {
                            AddonFilesLabel.Content = "Lost connection to update client";
                        }
                    });
                }
                catch (Exception) { }
            });
            //pipe.SendText("START_UPDATE_CHECK Mgamerz AlotAddOnGUI 1.0.0.0");

            pipe.SendText("START_UPDATE_CHECK Mgamerz AlotAddOnGUI " + System.Reflection.Assembly.GetEntryAssembly().GetName().Version);
        }

        private void RunMEMUpdater()
        {
            Log.Information("Running GHUpdater for MEM.");
            AddonFilesLabel.Content = "Checking for Mass Effect Modder updates";
            string updaterpath = EXE_DIRECTORY + BINARY_DIRECTORY + "GHUpdater.exe";
            string temppath = Path.GetTempPath() + "GHUpdater-MEM.exe";
            File.Copy(updaterpath, temppath, true);
            pipe = new AnonymousPipes("GHUPDATE_SERVER_MEM", temppath, "", delegate (String msg)
            {
                Dispatcher.Invoke((MethodInvoker)async delegate ()
                {
                    //UI THREAD
                    string[] clientmessage = msg.Split();
                    switch (clientmessage[0])
                    {
                        case "UPDATE_DOWNLOAD_COMPLETE":
                            if (updateprogresscontroller != null)
                            {
                                //updateprogresscontroller.SetTitle("Update ready to install");
                                //updateprogresscontroller.SetMessage("Update will install in 5 seconds.");
                                //await this.ShowMessageAsync("ALOT Addon Builder update ready", "The program will close and update in the background, then reopen. It should only take a few seconds.");
                                string updatemessage = "EXECUTE_UPDATE \"" + EXE_DIRECTORY + "bin\\\"";
                                Log.Information("Executing update: " + updatemessage);
                                pipe.SendText(updatemessage);
                                //Environment.Exit(0);
                            }
                            break;
                        case "UPDATE_DOWNLOAD_PROGRESS":
                            if (clientmessage.Length != 2)
                            {
                                Log.Warning("UPDATE_DOWNLOAD_PROGRESS message was not length 2 - ignoring message");
                                return;
                            }
                            if (updateprogresscontroller != null)
                            {
                                double value = Double.Parse(clientmessage[1]);
                                updateprogresscontroller.SetProgress(value);
                            }
                            break;
                        case "UP_TO_DATE":
                            Log.Information("GHUpdater reporting MEM is up to date");
                            Thread.Sleep(250);
                            try
                            {
                                File.Delete(temppath);
                            }
                            catch (Exception e)
                            {
                                Log.Error("Error deleting TEMP GHUpdater-MEM.exe: " + e.ToString());
                            }
                            //await FetchManifest();
                            break;
                        case "ERROR_CHECKING_FOR_UPDATES":
                            AddonFilesLabel.Content = "Error occured checking for MEM updates";
                            break;
                        case "UPDATE_AVAILABLE":
                            if (clientmessage.Length != 2)
                            {
                                Log.Warning("UPDATE_AVAILABLE message was not length 2 - ignoring message");
                                return;
                            }
                            Log.Information("Github Updater reports program update: " + clientmessage[1] + " is available.");
                            //MessageDialogResult result = await this.ShowMessageAsync("Update Available", "ALOT Addon Builder " + clientmessage[1] + " is available. Install the update?", MessageDialogStyle.AffirmativeAndNegative);
                            //if (result == MessageDialogResult.Affirmative)
                            //{
                            pipe.SendText("INITIATE_DOWNLOAD");
                            updateprogresscontroller = await this.ShowProgressAsync("Installing Update", "Mass Effect Modder is updating. Please wait...", true);
                            updateprogresscontroller.SetIndeterminate();
                            //}
                            //else
                            //{
                            //    pipe.SendText("KILL_UPDATER");
                            //    pipe.Close();
                            //    Log.Information("User declined update, shutting down updater");
                            //    await FetchManifest();
                            //}
                            break;
                        case "UPDATE_COMPLETED":
                            AddonFilesLabel.Content = "MassEffectModder has been updated.";
                            if (updateprogresscontroller != null)
                            {
                                await updateprogresscontroller.CloseAsync();
                            }
                            try
                            {
                                File.Delete(temppath);
                            }
                            catch (Exception e)
                            {
                                Log.Error("Error deleting TEMP GHUpdater-MEM.exe: " + e.ToString());
                            }
                            break;
                        default:
                            Log.Error("Unknown message from updater client: " + msg);
                            break;
                    }
                });
            }, delegate ()
            {
                // We're disconnected!
                try
                {

                    Dispatcher.Invoke((MethodInvoker)delegate ()
                    {
                        //UITHREAD
                        var source = PresentationSource.FromVisual(this);
                        if (source == null || source.IsDisposed)
                        {
                            AddonFilesLabel.Content = "Lost connection to update client";
                        }
                    });
                }
                catch (Exception) { }
            });
            Thread.Sleep(2000);
            var versInfo = FileVersionInfo.GetVersionInfo(BINARY_DIRECTORY + "MassEffectModder.exe");
            int fileVersion = versInfo.FileMajorPart;
            Log.Information("Local Mass Effect Modder version: " + fileVersion);
            pipe.SendText("START_UPDATE_CHECK MassEffectModder MassEffectModder " + fileVersion);
        }

        private async void InstallCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            Log.Information("Background thread has exited - starting InstallCompleted()");
            int result = (int)e.Result;
            Installing = false;
            SetupButtons();
            switch (result)
            {
                case 1:
                    HeaderLabel.Text = "An error occured building the Addon. The logs directory will have more information.";
                    AddonFilesLabel.Content = "Addon not successfully built";
                    Installing = true; //don't udpate the ticker
                    //await this.ShowMessageAsync("Error building Addon", "An error occured building the Addon. The files in the logs directory may help diagnose the issue.");
                    break;
                case 2:
                case 3:
                    if (errorOccured)
                    {
                        HeaderLabel.Text = "Addon built with errors.\nThe Addon was built but some files did not process correctly and were skipped.\nThe MEM packages for the addon have been placed into the " + MEM_OUTPUT_DISPLAY_DIR + " directory.";
                        AddonFilesLabel.Content = "MEM Packages placed in the " + MEM_OUTPUT_DISPLAY_DIR + " folder";
                        await this.ShowMessageAsync("ALOT Addon for Mass Effect " + result + " was built, but had errors", "Some files had errors occured during the build process. These files were skipped. Your game may look strange in some parts if you use the built Addon. You should report this to the developers on Discord.\nYou can install the Addon MEM files with Mass Effect Modder after you've installed the main ALOT MEM file.");

                    }
                    else
                    {
                        HeaderLabel.Text = "Addon Created.\nThe MEM packages for the addon have been placed into the " + MEM_OUTPUT_DISPLAY_DIR + " directory.";
                        AddonFilesLabel.Content = "MEM Packages placed in the " + MEM_OUTPUT_DISPLAY_DIR + " folder";
                        await this.ShowMessageAsync("ALOT Addon for Mass Effect " + result + " has been built", "You can install the Addon MEM files with Mass Effect Modder after you've installed the main ALOT MEM file.");
                    }
                    errorOccured = false;
                    break;
            }
        }

        private async void InstallProgressChanged(object sender, ProgressChangedEventArgs e)
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
                    case ERROR_OCCURED:
                        Install_ProgressBar.IsIndeterminate = false;
                        Install_ProgressBar.Value = 0;
                        await this.ShowMessageAsync("Error building Addon MEM Package", "An error occured building the addon. The logs will provide more information. The error message given is:\n" + (string)tc.Data);
                        break;
                    case SHOW_DEBUG_DIALOG:
                        await this.ShowMessageAsync("Debugging Dialog", "Extraction completed. Check your MEM_STAGING_DIR for files.");
                        break;
                    case INCREMENT_COMPLETION_EXTRACTION:
                        Interlocked.Increment(ref completed);
                        Install_ProgressBar.Value = (completed / (double)ADDONSTOINSTALL_COUNT) * 100;
                        break;
                }
            }
        }

        private void InstallAddon(object sender, DoWorkEventArgs e)
        {
            bool result = ExtractAddons((int)e.Argument); //arg is game id.
            e.Result = result ? (int)e.Argument : 1; //1 = Error

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
                //Console.WriteLine("Checking for files existence...");
                string basepath = EXE_DIRECTORY + @"Downloaded_Mods\";
                int numdone = 0;
                foreach (AddonFile af in addonfiles)
                {
                    bool ready = File.Exists(basepath + af.Filename);
                    if (af.Ready != ready && (af.Game_ME2 || af.Game_ME3)) //ensure the file applies to something
                    {
                        af.Ready = ready;
                    }
                    numdone += ready ? 1 : 0;
                    System.Windows.Application.Current.Dispatcher.Invoke(
                    () =>
                    {
                        // Code to run on the GUI thread.
                        Install_ProgressBar.Value = (int)(((double)numdone / addonfiles.Count) * 100);
                        AddonFilesLabel.Content = "Addon Files [" + numdone + "/" + addonfiles.Count + "]";
                    });
                    //Check for file existence
                    //Console.WriteLine("Checking for file: " + basepath + af.Filename);

                    //af.AssociatedCheckBox.ToolTip = af.AssociatedCheckBox.IsEnabled ? "File is downloaded and ready for install" : "Required file is missing: " + af.Filename;
                    //
                }

            }
            //Install_ProgressBar.Value = 30;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            RunUpdater();
        }

        private async void SetupButtons()
        {
            string me3map = Environment.GetEnvironmentVariable("LocalAppData") + @"\MassEffectModder\me3map.bin";
            string me2map = Environment.GetEnvironmentVariable("LocalAppData") + @"\MassEffectModder\me2map.bin";

            Log.Information("ME2 Texture Map exists: " + File.Exists(me2map));
            Log.Information("ME3 Texture Map exists: " + File.Exists(me3map));
            if (File.Exists(me2map) || File.Exists(me3map))
            {
                if (backgroundticker == null)
                {
                    backgroundticker = new DispatcherTimer();
                    backgroundticker.Tick += new EventHandler(timer_Tick);
                    backgroundticker.Interval = new TimeSpan(0, 0, 5); // execute every 5s
                    backgroundticker.Start();


                    InstallWorker.DoWork += InstallAddon;
                    InstallWorker.ProgressChanged += InstallProgressChanged;
                    InstallWorker.RunWorkerCompleted += InstallCompleted;
                    InstallWorker.WorkerReportsProgress = true;
                }

                if (!File.Exists(me2map))
                {
                    Log.Information("ME2 Texture Map missing - disabling ME2 install");
                    Button_InstallME2.IsEnabled = false;
                    Button_InstallME2.ToolTip = "Mass Effect 2 Texture Map not found. To install ALOT for Mass Effect 2 a texture map must be created.";
                    Button_InstallME2.Content = "ME2 Texture Map Missing";
                }
                else
                {
                    Button_InstallME2.IsEnabled = true;
                    Button_InstallME2.ToolTip = "Click to build ALOT Addon for Mass Effect 2";
                    Button_InstallME2.Content = "Build Addon for ME2";
                }
                if (!File.Exists(me3map))
                {
                    Log.Information("ME3 Texture Map missing - disabling ME3 install");
                    Button_InstallME3.IsEnabled = false;
                    Button_InstallME3.ToolTip = "Mass Effect 3 Texture Map not found. To install ALOT for Mass Effect 3 a texture map must be created.";
                    Button_InstallME3.Content = "ME3 Texture Map Missing";
                }
                else
                {
                    Button_InstallME3.IsEnabled = true;
                    Button_InstallME3.ToolTip = "Click to build ALOT Addon for Mass Effect 3";
                    Button_InstallME3.Content = "Build Addon for ME3";
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
                    try
                    {
                        await webClient.DownloadFileTaskAsync("https://raw.githubusercontent.com/Mgamerz/AlotAddOnGUI/master/manifest.xml", @"manifest-new.xml");
                        File.Delete(EXE_DIRECTORY + @"manifest.xml");
                        File.Move(EXE_DIRECTORY + @"manifest-new.xml", EXE_DIRECTORY + @"manifest.xml");
                        Log.Information("Manifest fetched.");
                    }
                    catch (WebException e)
                    {
                        Log.Error("Exception occured getting maAttributeom server: " + e.ToString());
                        if (File.Exists(EXE_DIRECTORY + @"manifest.xml"))
                        {
                            Log.Information("Reading existing manifest.");
                        }

                    }
                    if (!File.Exists(EXE_DIRECTORY + @"manifest.xml"))
                    {
                        Log.Fatal("No local manifest exists to use, exiting...");
                        await this.ShowMessageAsync("No Manifest Available", "An error occured downloading the manifest for addon. Information that is required to build the addon is not available. Check the program logs.");
                        Environment.Exit(1);
                    }
                    Button_About.IsEnabled = true;
                    readManifest();

                    Log.Information("readManifest() has completed. Switching over to user control");

                    Install_ProgressBar.IsIndeterminate = false;
                    HeaderLabel.Text = "Download the listed files, then drag and drop the files onto this window.\nOnce all items are ready, build the addon.";
                    AddonFilesLabel.Content = "Scanning...";
                    timer_Tick(null, null);
                    RunMEMUpdater();

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
            List<AddonFile> linqlist = null;
            try
            {
                XElement rootElement = XElement.Load(@"manifest.xml");
                string version = (string)rootElement.Attribute("version") ?? "";
                var elemn1 = rootElement.Elements();
                linqlist = (from e in rootElement.Elements("addonfile")
                            select new AddonFile
                            {
                                ProcessAsModFile = e.Attribute("processasmodfile") != null ? (bool)e.Attribute("processasmodfile") : false,
                                Author = (string)e.Attribute("author"),
                                FriendlyName = (string)e.Attribute("friendlyname"),
                                Game_ME2 = e.Element("games") != null ? (bool)e.Element("games").Attribute("masseffect2") : false,
                                Game_ME3 = e.Element("games") != null ? (bool)e.Element("games").Attribute("masseffect3") : false,
                                Filename = (string)e.Element("file").Attribute("filename"),
                                Tooltipname = e.Attribute("tooltipname") != null ? (string)e.Attribute("tooltipname") : (string)e.Attribute("friendlyname"),
                                DownloadLink = (string)e.Element("file").Attribute("downloadlink"),
                                Ready = false,
                                PackageFiles = e.Elements("packagefile")
                                    .Select(r => new PackageFile
                                    {
                                        SourceName = (string)r.Attribute("sourcename"),
                                        DestinationName = (string)r.Attribute("destinationname"),
                                        ME2Only = r.Attribute("me2only") != null ? true : false,
                                        ME3Only = r.Attribute("me3only") != null ? true : false,
                                    }).ToList(),
                            }).ToList();
                if (!version.Equals(""))
                {
                    Title += " - Manifest version " + version;
                }
            }
            catch (Exception e)
            {
                Log.Error("Error has occured parsing the XML!");
                Log.Error(e.Message);
                AddonFilesLabel.Content = "Error parsing manifest XML! Check the logs.";
                return;
            }
            linqlist = linqlist.OrderBy(o => o.Author).ThenBy(x => x.FriendlyName).ToList();
            addonfiles = new BindingList<AddonFile>(linqlist);

            foreach (AddonFile af in addonfiles)
            {
                //Set Game ME2/ME3
                foreach (PackageFile pf in af.PackageFiles)
                {
                    //Damn I did not think this one through very well
                    af.Game_ME2 |= pf.ME2Only || (!pf.ME2Only && !pf.ME3Only);
                    af.Game_ME3 |= pf.ME3Only || (!pf.ME2Only && !pf.ME3Only);
                }
            }
            lvUsers.ItemsSource = addonfiles;
            CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(lvUsers.ItemsSource);
            PropertyGroupDescription groupDescription = new PropertyGroupDescription("Author");
            view.GroupDescriptions.Add(groupDescription);
            SetupButtons();
        }

        public sealed class AddonFile : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            private bool m_ready;
            public bool ProcessAsModFile { get; set; }
            public string Author { get; set; }
            public string FriendlyName { get; set; }
            public bool Game_ME2 { get; set; }
            public bool Game_ME3 { get; set; }
            public string Filename { get; set; }
            public string Tooltipname { get; set; }
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

            public override string ToString()
            {
                return FriendlyName;
            }
        }

        public class PackageFile
        {
            public string SourceName { get; set; }
            public string DestinationName { get; set; }
            public bool ME2Only { get; set; }
            public bool ME3Only { get; set; }
        }

        public class ThreadCommand
        {
            public ThreadCommand(string command, object data)
            {
                this.Command = command;
                this.Data = data;
            }

            public ThreadCommand(string command)
            {
                this.Command = command;
            }

            public string Command;
            public object Data;
        }

        private async void Button_InstallME2_Click(object sender, RoutedEventArgs e)
        {
            if (await InstallPrecheck(2))
            {
                InitInstall(2);
                Button_InstallME2.Content = "Building...";
                CURRENT_GAME_BUILD = 2;
                InstallWorker.RunWorkerAsync(2);
            }
        }

        private bool ExtractAddon(AddonFile af)
        {
            string stagingdirectory = System.AppDomain.CurrentDomain.BaseDirectory + MEM_STAGING_DIR + "\\";

            string prefix = "[" + Path.GetFileNameWithoutExtension(af.Filename) + "] ";
            Log.Information(prefix + "Processing extraction on " + af.Filename);
            string fileextension = System.IO.Path.GetExtension(af.Filename);
            try
            {
                switch (fileextension)
                {
                    case ".7z":
                    case ".zip":
                    case ".rar":
                        {
                            InstallWorker.ReportProgress(completed, new ThreadCommand(UPDATE_OPERATION_LABEL, "Processing " + af.FriendlyName));

                            Log.Information(prefix + "Extracting file: " + af.Filename);
                            string exe = BINARY_DIRECTORY + "7z.exe";
                            string extractpath = EXE_DIRECTORY + "Extracted_Mods\\" + System.IO.Path.GetFileNameWithoutExtension(af.Filename);
                            string args = "x \"" + EXE_DIRECTORY + "Downloaded_Mods\\" + af.Filename + "\" -aoa -r -o\"" + extractpath + "\"";
                            runProcess(exe, args);
                            if (Directory.GetFiles(extractpath, "*.tpf").Length > 0)
                            {
                                //Extract the TPFs
                                exe = BINARY_DIRECTORY + "MassEffectModder.exe";
                                args = "-extract-tpf \"" + extractpath + "\" \"" + extractpath + "\"";
                                runProcess(exe, args);
                            }
                            string[] modfiles = Directory.GetFiles(extractpath, "*.mod", SearchOption.AllDirectories);
                            if (modfiles.Length > 0)
                            {
                                if (af.ProcessAsModFile)
                                {
                                    //Move to MEM_STAGING

                                    foreach (string modfile in modfiles)
                                    {
                                        Log.Information(prefix + "Copying modfile to staging directory (ProcessAsModFile=true): " + modfile);
                                        File.Copy(modfile, STAGING_DIRECTORY + Path.GetFileName(modfile), true);
                                    }
                                }
                                else
                                {
                                    //Extract the MOD
                                    Log.Information("Extracting modfiles in directory: " + extractpath);

                                    exe = BINARY_DIRECTORY + "MassEffectModder.exe";
                                    args = "-extract-mod " + CURRENT_GAME_BUILD + " \"" + extractpath + "\" \"" + extractpath + "\"";
                                    runProcess(exe, args);
                                }
                            }
                            string[] memfiles = Directory.GetFiles(extractpath, "*.mem");
                            if (memfiles.Length > 0)
                            {
                                //Copy MEM File - append game
                                foreach (string memfile in memfiles)

                                {
                                    Log.Information("-- Subtask: Move MEM file to staging directory" + memfile);

                                    string name = Path.GetFileNameWithoutExtension(memfile);
                                    string ext = Path.GetExtension(memfile);
                                    File.Copy(memfile, stagingdirectory + "\\" + name + " - ME" + CURRENT_GAME_BUILD + ext, true);
                                }
                            }

                            List<string> files = new List<string>();
                            foreach (string file in Directory.EnumerateFiles(extractpath, "*.dds", SearchOption.AllDirectories))
                            {
                                files.Add(file);
                            }

                            foreach (string file in files)
                            {

                                string destination = extractpath + "\\" + Path.GetFileName(file);
                                if (!destination.ToLower().Equals(file.ToLower()))
                                {
                                    Log.Information("Deleting existing file (if any): " + extractpath + "\\" + Path.GetFileName(file));
                                    File.Delete(destination);
                                    Log.Information(file + " -> " + destination);
                                    File.Move(file, destination);
                                }
                                else
                                {
                                    Log.Information("File is already in correct place, no further processing necessary: " + extractpath + "\\" + Path.GetFileName(file));
                                }
                            }
                            InstallWorker.ReportProgress(0, new ThreadCommand(INCREMENT_COMPLETION_EXTRACTION));
                            break;
                        }
                    case ".tpf":
                        {
                            InstallWorker.ReportProgress(completed, new ThreadCommand(UPDATE_OPERATION_LABEL, "Preparing " + af.FriendlyName));

                            string source = EXE_DIRECTORY + "Downloaded_Mods\\" + af.Filename;
                            string destination = EXE_DIRECTORY + "Extracted_Mods\\" + Path.GetFileName(af.Filename);
                            File.Copy(source, destination, true);
                            InstallWorker.ReportProgress(0, new ThreadCommand(INCREMENT_COMPLETION_EXTRACTION));
                            break;
                        }
                    case ".mod":
                        {
                            //Currently not used
                            //InstallWorker.ReportProgress(completed, new ThreadCommand(UPDATE_OPERATION_LABEL, "Preparing " + af.FriendlyName));
                            //completed++;
                            //int progress = (int)((float)completed / (float)addonstoinstall.Count * 100);
                            //InstallWorker.ReportProgress(progress);
                            break;
                        }
                    case ".mem":
                        {
                            InstallWorker.ReportProgress(completed, new ThreadCommand(UPDATE_OPERATION_LABEL, "Preparing " + af.FriendlyName));

                            //Copy to output folder
                            File.Copy(EXE_DIRECTORY + "Downloaded_Mods\\" + af.Filename, EXE_DIRECTORY + MEM_OUTPUT_DIR + "\\" + af.Filename, true);
                            InstallWorker.ReportProgress(0, new ThreadCommand(INCREMENT_COMPLETION_EXTRACTION));
                            break;
                        }
                }
                InstallWorker.ReportProgress(completed, new ThreadCommand(UPDATE_PROGRESSBAR_INDETERMINATE, false));
            }
            catch (Exception e)
            {
                Log.Error("ERROR EXTRACTING AND PROCESSING FILE!");
                Log.Error(e.ToString());
                InstallWorker.ReportProgress(0, new ThreadCommand("ERROR_OCCURED", e.Message));
                return false;
            }
            return true;
        }

        private async void Button_InstallME3_Click(object sender, RoutedEventArgs e)
        {
            if (await InstallPrecheck(3))
            {

                InitInstall(3);
                Button_InstallME3.Content = "Building...";
                CURRENT_GAME_BUILD = 3;
                InstallWorker.RunWorkerAsync(3);
            }
        }

        private async Task<bool> InstallPrecheck(int game)
        {
            timer_Tick(null, null);
            int nummissing = 0;
            bool oneisready = false;
            foreach (AddonFile af in addonfiles)
            {
                if (af.Game_ME2 && game == 2 || af.Game_ME3 && game == 3)
                {
                    if (!af.Ready)
                    {
                        nummissing++;
                    }
                    else
                    {
                        oneisready = true;
                    }
                }
            }

            if (nummissing == 0)
            {
                return true;
            }

            if (!oneisready)
            {
                await this.ShowMessageAsync("No files available for building", "There are no files available in the Downloaded_Mods folder to build an addon for Mass Effect " + game + ".");
                return false;
            }

            MessageDialogResult result = await this.ShowMessageAsync(nummissing + " file" + (nummissing != 1 ? "s are" : " is") + " missing", "Some files for the Mass Effect " + game + " addon are missing - do you want to build the addon without these files?", MessageDialogStyle.AffirmativeAndNegative);
            return result == MessageDialogResult.Affirmative;
        }

        private bool ExtractAddons(int game)
        {

            string stagingdirectory = System.AppDomain.CurrentDomain.BaseDirectory + MEM_STAGING_DIR + "\\";


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
            ADDONSTOINSTALL_COUNT = addonstoinstall.Count;
            int completed = 0;
            InstallWorker.ReportProgress(completed, new ThreadCommand(UPDATE_OPERATION_LABEL, "Extracting Mods..."));
            InstallWorker.ReportProgress(completed, new ThreadCommand(UPDATE_PROGRESSBAR_INDETERMINATE, true));

            InstallWorker.ReportProgress(0);

            bool modextractrequired = false; //not used currently.

            int threads = Environment.ProcessorCount;
            if (threads > 1)
            {
                threads--; //cores - 1
            }
            bool[] results = addonstoinstall.AsParallel().WithDegreeOfParallelism(threads).Select(ExtractAddon).ToArray();
            foreach (bool result in results)
            {
                if (!result)
                {
                    Log.Error("Failed to extract a file! Check previous entries in this log");
                    InstallWorker.ReportProgress(completed, new ThreadCommand(UPDATE_PROGRESSBAR_INDETERMINATE, false));
                    InstallWorker.ReportProgress(completed, new ThreadCommand(UPDATE_OPERATION_LABEL, "Failed to extract a file - check logs"));
                    CURRENT_GAME_BUILD = 0; //reset
                    return false;
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
            Directory.CreateDirectory(stagingdirectory);
            int numcompleted = 0;
            foreach (AddonFile af in addonstoinstall)
            {
                if (af.PackageFiles.Count > 0)
                {
                    foreach (PackageFile pf in af.PackageFiles)
                    {
                        if (game == 2 && pf.ME2Only || game == 3 && pf.ME3Only || (!pf.ME3Only && !pf.ME2Only))
                        {
                            string extractedpath = basepath + Path.GetFileNameWithoutExtension(af.Filename) + "\\" + pf.SourceName;
                            if (File.Exists(extractedpath))
                            {
                                Log.Information("Copying Package File: " + pf.SourceName + "->" + pf.DestinationName);
                                string destination = stagingdirectory + pf.DestinationName;
                                File.Copy(extractedpath, destination, true);
                            }
                            else
                            {
                                Log.Error("File specified by manifest doesn't exist after extraction: " + extractedpath);
                                errorOccured = true;
                            }

                            numcompleted++;
                            int progress = (int)((float)numcompleted / (float)totalfiles * 100);
                            InstallWorker.ReportProgress(progress);
                        }
                        //  Thread.Sleep(1000);
                    }
                }
            }
            Log.Information("Completed staging.");

            InstallWorker.ReportProgress(0);

            InstallWorker.ReportProgress(completed, new ThreadCommand(UPDATE_OPERATION_LABEL, "Building Addon MEM Package... This will take some time."));
            InstallWorker.ReportProgress(completed, new ThreadCommand(UPDATE_PROGRESSBAR_INDETERMINATE, true));
            {
                Log.Information("Building MEM Package.");
                string exe = BINARY_DIRECTORY + "MassEffectModder.exe";
                string args = "-convert-to-mem " + game + " \"" + EXE_DIRECTORY + MEM_STAGING_DIR + "\" \"" + EXE_DIRECTORY + MEM_OUTPUT_DIR + "\\ALOT_ME" + game + "_Addon.mem";
                runProcess(exe, args);
            }

            InstallWorker.ReportProgress(completed, new ThreadCommand(UPDATE_PROGRESSBAR_INDETERMINATE, false));
            InstallWorker.ReportProgress(100);
            try
            {
                Directory.Delete(MEM_STAGING_DIR, true);
                Directory.Delete("Extracted_Mods", true);
            }
            catch (IOException e)
            {
                Log.Error("Unable to delete staging and target directories. Addon should have been built however.\n" + e.ToString());
            }
            CURRENT_GAME_BUILD = 0; //reset
            return true;

        }

        private int runProcess(string exe, string args)
        {
            Log.Information("Running process: " + exe + " " + args);
            using (Process p = new Process())
            {
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.FileName = exe;
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.Arguments = args;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;

                StringBuilder output = new StringBuilder();
                StringBuilder error = new StringBuilder();

                using (AutoResetEvent outputWaitHandle = new AutoResetEvent(false))
                using (AutoResetEvent errorWaitHandle = new AutoResetEvent(false))
                {
                    p.OutputDataReceived += (sender, e) =>
                    {
                        if (e.Data == null)
                        {
                            outputWaitHandle.Set();
                        }
                        else
                        {
                            output.AppendLine(e.Data);
                        }
                    };
                    p.ErrorDataReceived += (sender, e) =>
                    {
                        if (e.Data == null)
                        {
                            errorWaitHandle.Set();
                        }
                        else
                        {
                            error.AppendLine(e.Data);
                        }
                    };

                    p.Start();
                    int timeout = 600000;
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();

                    if (p.WaitForExit(timeout) &&
                        outputWaitHandle.WaitOne(timeout) &&
                        errorWaitHandle.WaitOne(timeout))
                    {
                        // Process completed. Check process.ExitCode here.
                        string outputmsg = "Process output of " + exe + " " + args + ":";
                        if (output.ToString().Length > 0)
                        {
                            outputmsg += "\nStandard:\n" + output.ToString();
                        }
                        if (error.ToString().Length > 0)
                        {
                            outputmsg += "\nError:\n" + error.ToString();
                        }
                        Log.Information(outputmsg);
                        return p.ExitCode;
                    }
                    else
                    {
                        // Timed out.
                        Log.Error("Process timed out: " + exe +" "+args);
                        return -1;
                    }
                    
                }
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(e.Uri.ToString());
            this.nIcon.Visible = true;
            //this.WindowState = System.Windows.WindowState.Minimized;
            this.nIcon.Icon = Properties.Resources.tooltiptrayicon;
            string fname = (string)((Hyperlink)e.Source).Tag;
            this.nIcon.ShowBalloonTip(14000, "Downloading ALOT Addon File", "Download the file titled \"" + fname + "\"", ToolTipIcon.Info);
        }

        private async void InitInstall(int game)
        {
            Log.Information("Deleting any pre-existing Extracted_Mods folder.");
            string destinationpath = System.AppDomain.CurrentDomain.BaseDirectory + @"Extracted_Mods\";
            try
            {
                if (Directory.Exists(destinationpath))
                {
                    Directory.Delete(destinationpath, true);
                }

                if (Directory.Exists(MEM_STAGING_DIR))
                {
                    Directory.Delete(MEM_STAGING_DIR, true);
                }
            }
            catch (System.IO.IOException e)
            {
                Log.Error("Unable to delete staging and target directories.\n" + e.ToString());
                await this.ShowMessageAsync("Error occured while preparing directories", "ALOT Addon Builder was unable to cleanup some directories. Make sure all file explorer windows are closed that may be open in the working directories.");
                return;
            }

            Installing = true;
            Button_InstallME2.IsEnabled = false;
            Button_InstallME3.IsEnabled = false;

            Directory.CreateDirectory(MEM_OUTPUT_DIR);
            Directory.CreateDirectory(MEM_STAGING_DIR);

            AddonFilesLabel.Content = "Preparing to install...";
            HeaderLabel.Text = "Now building the ALOT Addon for Mass Effect " + game + ".\nDon't close this window until the process completes.";
            // Install_ProgressBar.IsIndeterminate = true;
        }

        private async void File_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
                Log.Information("Files dropped:");
                foreach (String file in files)
                {
                    Log.Information(" -" + file);
                }
                List<AddonFile> filesimported = new List<AddonFile>();
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
                            filesimported.Add(af);
                            timer_Tick(null, null);
                            break;
                        }
                        else
                        {
                            //Log.Information("Dragged file does not match addonfile or file is already ready: " + af.Filename);
                        }
                    }
                }
                if (filesimported.Count > 0)
                {
                    string message = "The following files have been imported to ALOT Addon Builder:";
                    foreach (AddonFile af in filesimported)
                    {
                        message += "\n - " + af.FriendlyName;
                    }
                    await this.ShowMessageAsync(filesimported.Count + " file" + (filesimported.Count != 1 ? "s" : "") + " imported", message);
                }
            }
        }

        private async void Button_About_Click(object sender, RoutedEventArgs e)
        {
            string title = "ALOT Addon Builder " + System.Reflection.Assembly.GetEntryAssembly().GetName().Version + "\n";

            string ghupdaterver = "GHUpdater Version: " + FileVersionInfo.GetVersionInfo(BINARY_DIRECTORY + "GHUpdater.exe").FileVersion;


            var versInfo = FileVersionInfo.GetVersionInfo(BINARY_DIRECTORY + "MassEffectModder.exe");
            int fileVersion = versInfo.FileMajorPart;

            string credits = "MEM Version: " + fileVersion + "\n" + ghupdaterver + "\n\nBrought to you by:\n - Mgamerz\n - CreeperLava\n - aquadran\n\nSource code: https://github.com/Mgamerz/AlotAddOnGUI\nLicensed under GPLv3";
            await this.ShowMessageAsync(title, credits);

        }
    }
}
