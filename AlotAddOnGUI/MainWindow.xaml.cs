using AlotAddOnGUI.classes;
using ByteSizeLib;
using MahApps.Metro;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using Microsoft.WindowsAPICodePack.Taskbar;
using Octokit;
using Serilog;
using SlavaGu.ConsoleAppLauncher;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Navigation;
using System.Windows.Threading;
using System.Xml.Linq;

namespace AlotAddOnGUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow, INotifyPropertyChanged
    {
        private AutoResetEvent _outputWaitHandle;
        private AutoResetEvent _errorWaitHandle;

        public ConsoleApp BACKGROUND_MEM_PROCESS = null;
        public bool BACKGROUND_MEM_RUNNING = false;
        ProgressDialogController updateprogresscontroller;
        public const string UPDATE_OPERATION_LABEL = "UPDATE_OPERATION_LABEL";
        public const string UPDATE_PROGRESSBAR_INDETERMINATE = "SET_PROGRESSBAR_DETERMINACY";
        public const string INCREMENT_COMPLETION_EXTRACTION = "INCREMENT_COMPLETION_EXTRACTION";
        public const string SHOW_DIALOG = "SHOW_DIALOG";
        public const string ERROR_OCCURED = "ERROR_OCCURED";
        public const string BINARY_DIRECTORY = "bin\\";
        private bool errorOccured = false;
        private bool UsingBundledManifest = false;
        private List<string> BlockingMods;

        private DispatcherTimer backgroundticker;
        private DispatcherTimer tipticker;
        private int completed = 0;
        //private int addonstoinstall = 0;
        private int CURRENT_GAME_BUILD = 0; //set when extraction is run/finished
        private int ADDONSTOINSTALL_COUNT = 0;
        private bool PreventFileRefresh = false;
        public static readonly string REGISTRY_KEY = @"SOFTWARE\ALOTAddon";
        public static readonly string ME3_BACKUP_REGISTRY_KEY = @"SOFTWARE\Mass Effect 3 Mod Manager";

        private BackgroundWorker BuildWorker = new BackgroundWorker();
        private BackgroundWorker BackupWorker = new BackgroundWorker();
        private BackgroundWorker InstallWorker = new BackgroundWorker();
        private BackgroundWorker ImportWorker = new BackgroundWorker();
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChangedEventHandler handler = PropertyChanged;

            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        private const string MEM_EXE_NAME = "MassEffectModderNoGui.exe";

        private BindingList<AddonFile> addonfiles;
        NotifyIcon nIcon = new NotifyIcon();
        private const string MEM_OUTPUT_DIR = "MEM_Packages";
        private const string MEM_OUTPUT_DISPLAY_DIR = "MEM_Packages";

        private const string MEM_STAGING_DIR = "MEM_PACKAGE_STAGING";
        private string EXE_DIRECTORY = System.AppDomain.CurrentDomain.BaseDirectory;
        private string STAGING_DIRECTORY = System.AppDomain.CurrentDomain.BaseDirectory + MEM_STAGING_DIR + "\\";
        private bool me1Installed;
        private bool me2Installed;
        private bool me3Installed;

        private static readonly string SETTINGSTR_HIDENONRELEVANTFILES = "HideNonRelevantFiles";
        private BindingList<AddonFile> alladdonfiles;
        private readonly string PRIMARY_HEADER = "Download the listed files, then drag and drop the files onto this window. Do not extract any of the files.\nOnce all files for your game are ready, you can build the addon.";
        private const string SETTINGSTR_IMPORTASMOVE = "ImportAsMove";
        public const string SETTINGSTR_BETAMODE = "BetaMode";
        private List<string> BACKGROUND_MEM_PROCESS_ERRORS;
        private const string SHOW_DIALOG_YES_NO = "SHOW_DIALOG_YES_NO";
        private bool CONTINUE_BACKUP_EVEN_IF_VERIFY_FAILS = false;
        private bool ERROR_SHOWING = false;
        private int PREBUILT_MEM_INDEX; //will increment to 10 when run
        private bool SHOULD_HAVE_OUTPUT_FILE;

        public bool USING_BETA { get; private set; }
        public bool SpaceSaving { get; private set; }
        public StringBuilder BACKGROUND_MEM_STDOUT { get; private set; }
        public int BACKUP_THREAD_GAME { get; private set; }
        private bool _showME1Files = true;
        private bool _showME2Files = true;
        private bool _showME3Files = true;
        private bool Loading = true;
        private int LODLIMIT = 0;

        public bool ShowME1Files
        {
            get { return _showME1Files; }
            set
            {
                _showME1Files = value;
                OnPropertyChanged();
                if (!Loading)
                {
                    ApplyFiltering();
                }
            }
        }
        public bool ShowME2Files
        {
            get { return _showME2Files; }
            set
            {
                _showME2Files = value;
                OnPropertyChanged();
                if (!Loading)
                {
                    ApplyFiltering();
                }
            }
        }
        public bool ShowME3Files
        {
            get { return _showME3Files; }
            set
            {
                _showME3Files = value;
                OnPropertyChanged();
                if (!Loading)
                {
                    ApplyFiltering();
                }
            }
        }


        public MainWindow()
        {
            Log.Information("MainWindow() is starting");
            InitializeComponent();
            LoadSettings();
            Title = "ALOT Installer " + System.Reflection.Assembly.GetEntryAssembly().GetName().Version;
            HeaderLabel.Text = "Preparing application...";
            AddonFilesLabel.Text = "Please wait";
        }

        /// <summary>
        /// Executes a task in the future
        /// </summary>
        /// <param name="action">Action to run</param>
        /// <param name="timeoutInMilliseconds">Delay in ms</param>
        /// <returns></returns>
        public async Task Execute(Action action, int timeoutInMilliseconds)
        {
            await Task.Delay(timeoutInMilliseconds);
            action();
        }

        private async void RunApplicationUpdater2()
        {
            AddonFilesLabel.Text = "Checking for application updates";
            var versInfo = System.Reflection.Assembly.GetEntryAssembly().GetName().Version;
            var client = new GitHubClient(new ProductHeaderValue("ALOTAddonGUI"));
            try
            {
                var releases = await client.Repository.Release.GetAll("Mgamerz", "ALOTAddonGUI");
                if (releases.Count > 0)
                {
                    //The release we want to check is always the latest, so [0]
                    Release latest = null;
                    Version latestVer = new Version("0.0.0.0");
                    foreach (Release r in releases)
                    {
                        if (!USING_BETA && r.Prerelease)
                        {
                            continue;
                        }
                        Version releaseVersion = new Version(r.TagName);
                        if (releaseVersion > latestVer)
                        {
                            latest = r;
                            latestVer = releaseVersion;
                        }
                    }
                    if (latest != null)
                    {
                        Version releaseName = new Version(latest.TagName);
                        if (versInfo < releaseName && latest.Assets.Count > 0)
                        {
                            string versionInfo = "";
                            if (latest.Prerelease)
                            {
                                versionInfo += "This is a beta build. You are receiving this update because you have opted into Beta Mode in settings.\n\n";
                            }
                            versionInfo += "Release date: " + latest.PublishedAt.Value.ToLocalTime().ToString();
                            MetroDialogSettings mds = new MetroDialogSettings();
                            mds.AffirmativeButtonText = "Update";
                            mds.NegativeButtonText = "Later";
                            mds.DefaultButtonFocus = MessageDialogResult.Affirmative;

                            MessageDialogResult result = await this.ShowMessageAsync("Update Available", "ALOT Addon Builder " + releaseName + " is available. You are currently using version " + versInfo.ToString() + ".\n========================\n" + versionInfo + "\n" + latest.Body + "\n========================\nInstall the update?", MessageDialogStyle.AffirmativeAndNegative, mds);
                            if (result == MessageDialogResult.Affirmative)
                            {

                                //there's an update
                                updateprogresscontroller = await this.ShowProgressAsync("Installing Update", "ALOT Addon Installer is updating. Please wait...", true);
                                updateprogresscontroller.SetIndeterminate();
                                WebClient downloadClient = new WebClient();

                                downloadClient.Headers["Accept"] = "application/vnd.github.v3+json";
                                downloadClient.Headers["user-agent"] = "ALOTAddonGUI";
                                string temppath = Path.GetTempPath();
                                downloadClient.DownloadProgressChanged += (s, e) =>
                                {
                                    Log.Information("Program update download percent: " + e.ProgressPercentage);
                                    updateprogresscontroller.SetProgress((double)e.ProgressPercentage / 100);
                                };
                                updateprogresscontroller.Canceled += async (s, e) =>
                                {
                                    if (downloadClient != null)
                                    {
                                        downloadClient.CancelAsync();
                                        await updateprogresscontroller.CloseAsync();
                                        Log.Information("Application update was in progress but was canceled.");
                                        await FetchManifest();
                                    }
                                };
                                downloadClient.DownloadFileCompleted += UnzipSelfUpdate;
                                string downloadPath = temppath + "ALOTAddonGUI_Update" + Path.GetExtension(latest.Assets[0].BrowserDownloadUrl);
                                //DEBUG ONLY
                                Uri downloadUri = new Uri(latest.Assets[0].BrowserDownloadUrl);
                                downloadClient.DownloadFileAsync(downloadUri, downloadPath, new KeyValuePair<ProgressDialogController, string>(updateprogresscontroller, downloadPath));
                            }
                            else
                            {
                                AddonFilesLabel.Text = "Application update declined";
                                Log.Warning("Application update was declined");
                                await FetchManifest();
                            }
                        }
                        else
                        {
                            //up to date
                            AddonFilesLabel.Text = "Application up to date";
                            Log.Information("Application is up to date.");
                            await FetchManifest();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error("Error checking for update: " + e);
                await FetchManifest();
            }
        }

        private async void RunMEMUpdaterGUI()
        {
            int fileVersion = 0;
            if (File.Exists(BINARY_DIRECTORY + "MassEffectModder.exe"))
            {
                var versInfo = FileVersionInfo.GetVersionInfo(BINARY_DIRECTORY + "MassEffectModder.exe");
                fileVersion = versInfo.FileMajorPart;
                Button_MEM_GUI.Content = "LAUNCH MEM v" + fileVersion;
            }
            try
            {
                var client = new GitHubClient(new ProductHeaderValue("ALOTAddonGUI"));
                var user = await client.Repository.Release.GetAll("MassEffectModder", "MassEffectModder");
                if (user.Count > 0)
                {
                    //The release we want to check is always the latest, so [0]
                    Release latest = user[0];
                    int releaseNameInt = Convert.ToInt32(latest.TagName);
                    if (fileVersion < releaseNameInt && latest.Assets.Count > 0)
                    {
                        //there's an update
                        //updateprogresscontroller = await this.ShowProgressAsync("Installing Update", "Mass Effect Modder is updating. Please wait...", true);
                        //updateprogresscontroller.SetIndeterminate();
                        WebClient downloadClient = new WebClient();

                        downloadClient.Headers["Accept"] = "application/vnd.github.v3+json";
                        downloadClient.Headers["user-agent"] = "ALOTAddonGUI";
                        string temppath = Path.GetTempPath();
                        /*downloadClient.DownloadProgressChanged += (s, e) =>
                        {
                            updateprogresscontroller.SetProgress((double)e.ProgressPercentage / 100);
                        };*/
                        downloadClient.DownloadFileCompleted += UnzipMEMGUIUpdate;
                        string downloadPath = temppath + "MEMGUI_Update" + Path.GetExtension(latest.Assets[0].BrowserDownloadUrl);
                        downloadClient.DownloadFileAsync(new Uri(latest.Assets[0].BrowserDownloadUrl), downloadPath, downloadPath);
                    }
                    else
                    {
                        //up to date
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error("Error checking for MEM GUI update: " + e.Message);
                ShowStatus("Error checking for MEM update");
            }
        }

        private async void RunMEMUpdater2()
        {
            int fileVersion = 0;
            if (File.Exists(BINARY_DIRECTORY + "MassEffectModderNoGui.exe"))
            {
                var versInfo = FileVersionInfo.GetVersionInfo(BINARY_DIRECTORY + "MassEffectModderNoGui.exe");
                fileVersion = versInfo.FileMajorPart;
            }

            Label_MEMVersion.Content = "MEM (No GUI) Version: " + fileVersion;
            try
            {
                var client = new GitHubClient(new ProductHeaderValue("ALOTAddonGUI"));
                var user = await client.Repository.Release.GetAll("MassEffectModder", "MassEffectModderNoGui");
                if (user.Count > 0)
                {
                    //The release we want to check is always the latest, so [0]
                    Release latest = user[0];
                    int releaseNameInt = Convert.ToInt32(latest.TagName);
                    if (fileVersion < releaseNameInt && latest.Assets.Count > 0)
                    {
                        //there's an update
                        updateprogresscontroller = await this.ShowProgressAsync("Installing Update", "Mass Effect Modder (No GUI) is updating. Please wait...", true);
                        updateprogresscontroller.SetIndeterminate();
                        WebClient downloadClient = new WebClient();

                        downloadClient.Headers["Accept"] = "application/vnd.github.v3+json";
                        downloadClient.Headers["user-agent"] = "ALOTAddonGUI";
                        string temppath = Path.GetTempPath();
                        downloadClient.DownloadProgressChanged += (s, e) =>
                        {
                            updateprogresscontroller.SetProgress((double)e.ProgressPercentage / 100);
                        };

                        downloadClient.DownloadFileCompleted += UnzipProgramUpdate;
                        string downloadPath = temppath + "MEM_Update" + Path.GetExtension(latest.Assets[0].BrowserDownloadUrl);
                        downloadClient.DownloadFileAsync(new Uri(latest.Assets[0].BrowserDownloadUrl), downloadPath, new KeyValuePair<ProgressDialogController, string>(updateprogresscontroller, downloadPath));
                    }
                    else
                    {
                        //up to date
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error("Error checking for MEM update: " + e.Message);
                ShowStatus("Error checking for MEM (NOGUI) update");
            }
        }

        private void UnzipSelfUpdate(object sender, AsyncCompletedEventArgs e)
        {
            KeyValuePair<ProgressDialogController, string> kp = (KeyValuePair<ProgressDialogController, string>)e.UserState;
            if (e.Cancelled)
            {
                // delete the partially-downloaded file
                if (File.Exists(kp.Value))
                {
                    File.Delete(kp.Value);
                }
                return;
            }
            Log.Information("Applying update to program UnzipSelfUpdate()");
            if (File.Exists(kp.Value))
            {
                kp.Key.SetIndeterminate();
                kp.Key.SetTitle("Extracting ALOT Addon Installer Update");
                string path = BINARY_DIRECTORY + "7z.exe";
                string args = "x \"" + kp.Value + "\" -aoa -r -o\"" + System.AppDomain.CurrentDomain.BaseDirectory + "Update\"";
                Log.Information("Extracting update...");
                Utilities.runProcess(path, args);

                File.Delete((string)kp.Value);
                kp.Key.CloseAsync();

                Log.Information("Update Extracted - rebooting to update mode");
                string exe = System.AppDomain.CurrentDomain.BaseDirectory + "Update\\" + System.AppDomain.CurrentDomain.FriendlyName;
                string currentDirNoSlash = System.AppDomain.CurrentDomain.BaseDirectory;
                currentDirNoSlash = currentDirNoSlash.Substring(0, currentDirNoSlash.Length - 1);
                args = "--update-dest \"" + currentDirNoSlash + "\"";
                Utilities.runProcess(exe, args, true);
                Environment.Exit(0);
            }
            else
            {
                kp.Key.CloseAsync();
            }
        }

        private void UnzipProgramUpdate(object sender, AsyncCompletedEventArgs e)
        {
            KeyValuePair<ProgressDialogController, string> kp = (KeyValuePair<ProgressDialogController, string>)e.UserState;
            kp.Key.SetIndeterminate();
            kp.Key.SetTitle("Extracting Tool Update");
            //Extract 7z
            string path = BINARY_DIRECTORY + "7z.exe";

            string args = "x \"" + kp.Value + "\" -aoa -r -o\"" + System.AppDomain.CurrentDomain.BaseDirectory + "bin\"";
            Log.Information("Extracting Tool update...");
            Utilities.runProcess(path, args);
            Log.Information("Extraction complete.");

            File.Delete((string)kp.Value);
            kp.Key.CloseAsync();

            var versInfo = FileVersionInfo.GetVersionInfo(BINARY_DIRECTORY + MEM_EXE_NAME);
            int fileVersion = versInfo.FileMajorPart;
            Label_MEMVersion.Content = "MEM Cmd Version: " + fileVersion;
        }

        private void UnzipMEMGUIUpdate(object sender, AsyncCompletedEventArgs e)
        {

            //Extract 7z
            string path = BINARY_DIRECTORY + "7z.exe";

            string args = "x \"" + e.UserState + "\" -aoa -r -o\"" + System.AppDomain.CurrentDomain.BaseDirectory + "bin\"";
            Log.Information("Extracting MEMGUI update...");
            Utilities.runProcess(path, args);
            Log.Information("Extraction complete.");

            File.Delete((string)e.UserState);
            var versInfo = FileVersionInfo.GetVersionInfo(BINARY_DIRECTORY + "MassEffectModder.exe");
            int fileVersion = versInfo.FileMajorPart;
            ShowStatus("Updated Mass Effect Modder (GUI version) to v" + fileVersion, 3000);
        }

        private async void BuildCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.NoProgress, this);
            Log.Information("Background thread has exited - starting InstallCompleted()");
            int result = (int)e.Result;
            PreventFileRefresh = false;
            SetupButtons();
            Button_Settings.IsEnabled = true;
            switch (result)
            {
                case -2:
                    HeaderLabel.Text = "Installation blocked due to incompatible mods detected in Mass Effect" + getGameNumberSuffix(CURRENT_GAME_BUILD) + ".\nRestore your game to a compatible state and do not install these mods.";
                    AddonFilesLabel.Text = "Installation aborted";
                    string badModsStr = "";
                    foreach (string str in BlockingMods)
                    {
                        badModsStr += "\n - " + str;
                    }
                    string prefix = "The following mods appear to be installed and are";
                    if (BlockingMods.Count != 1)
                    {
                        prefix = "The following mod appears to be installed and is";
                    }
                    await this.ShowMessageAsync("Incompatible mods detected", prefix + "known to be incompatible with ALOT for Mass Effect" + getGameNumberSuffix(CURRENT_GAME_BUILD) + ". Restore your game to an unmodified state, and then install compatible versions of these mods (or do not install them at all)." + badModsStr);
                    ADDONFILES_TO_BUILD = null;
                    PreventFileRefresh = false;
                    break;
                case -1:
                default:
                    HeaderLabel.Text = "An error occured building the Addon. The logs directory will have more information on what happened.";
                    AddonFilesLabel.Text = "Addon not successfully built";
                    PreventFileRefresh = true; //don't udpate the ticker
                    ADDONFILES_TO_BUILD = null;
                    break;
                case 1:
                case 2:
                case 3:
                    if (errorOccured)
                    {
                        HeaderLabel.Text = "Addon built with errors.\nThe Addon was built but some files did not process correctly and were skipped.\nThe MEM packages for the addon have been placed into the " + MEM_OUTPUT_DISPLAY_DIR + " directory.";
                        AddonFilesLabel.Text = "MEM Packages placed in the " + MEM_OUTPUT_DISPLAY_DIR + " folder";
                        await this.ShowMessageAsync("ALOT Addon for Mass Effect" + getGameNumberSuffix(result) + " was built, but had errors", "Some files had errors occured during the build process. These files were skipped. Your game may look strange in some parts if you use the built Addon. You should report this to the developers on Discord.");
                        ADDONFILES_TO_BUILD = null;

                    }
                    else
                    {

                        //flash
                        var helper = new FlashWindowHelper(System.Windows.Application.Current);
                        // Flashes the window and taskbar 5 times and stays solid 
                        // colored until user focuses the main window
                        helper.FlashApplicationWindow();

                        HeaderLabel.Text = "Ready to install new textures";
                        AddonFilesLabel.Text = "MEM Packages placed in the " + MEM_OUTPUT_DISPLAY_DIR + " folder";
                        MetroDialogSettings mds = new MetroDialogSettings();
                        mds.AffirmativeButtonText = "Install Now";

                        mds.NegativeButtonText = "OK";
                        mds.DefaultButtonFocus = MessageDialogResult.Affirmative;
                        var buildResult = await this.ShowMessageAsync("ALOT Addon for Mass Effect" + getGameNumberSuffix(result) + " has been built", "You can install ALOT and these files right now, or you can wait until later to install them manually via MEM.", MessageDialogStyle.AffirmativeAndNegative, mds);
                        if (buildResult == MessageDialogResult.Affirmative)
                        {
                            InstallALOT(result);
                        }
                        else
                        {
                            ADDONFILES_TO_BUILD = null;
                        }
                    }
                    errorOccured = false;
                    break;
            }

        }

        private void SetInstallFlyoutState(bool open)
        {
            InstallingOverlayFlyout.IsOpen = open;
            if (open)
            {
                BorderThickness = new Thickness(0, 0, 0, 0);
            }
            else
            {
                BorderThickness = new Thickness(1, 1, 1, 1);

            }
        }

        private async void BuildProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (e.UserState is null)
            {
                Build_ProgressBar.Value = e.ProgressPercentage;
                TaskbarManager.Instance.SetProgressValue(e.ProgressPercentage, 100);
            }
            else
            {
                ThreadCommand tc = (ThreadCommand)e.UserState;
                switch (tc.Command)
                {
                    case UPDATE_OPERATION_LABEL:
                        AddonFilesLabel.Text = (string)tc.Data;
                        break;
                    case UPDATE_HEADER_LABEL:
                        HeaderLabel.Text = (string)tc.Data;
                        break;
                    case UPDATE_PROGRESSBAR_INDETERMINATE:
                        Build_ProgressBar.IsIndeterminate = (bool)tc.Data;
                        break;
                    case ERROR_OCCURED:

                        Build_ProgressBar.IsIndeterminate = false;
                        Build_ProgressBar.Value = 0;
                        if (!ERROR_SHOWING)
                        {
                            ERROR_SHOWING = true;
                            await this.ShowMessageAsync("Error building Addon MEM Package", "An error occured building the addon. The logs will provide more information. The error message given is:\n" + (string)tc.Data);
                            ERROR_SHOWING = false;
                        }
                        break;
                    case SHOW_DIALOG:
                        KeyValuePair<string, string> messageStr = (KeyValuePair<string, string>)tc.Data;
                        await this.ShowMessageAsync(messageStr.Key, messageStr.Value);
                        break;
                    case INCREMENT_COMPLETION_EXTRACTION:
                        Interlocked.Increment(ref completed);
                        Build_ProgressBar.Value = (completed / (double)ADDONSTOINSTALL_COUNT) * 100;
                        break;
                }
            }
        }

        private void BuildAddon(object sender, DoWorkEventArgs e)
        {
            BlockingMods = new List<string>();
            if (CURRENT_GAME_BUILD < 3)
            {
                string exe = BINARY_DIRECTORY + MEM_EXE_NAME;
                string args = "-detect-bad-mods " + CURRENT_GAME_BUILD + " -ipc";
                runMEM_DetectBadMods(exe, args, null);
                while (BACKGROUND_MEM_PROCESS.State == AppState.Running)
                {
                    Thread.Sleep(250);
                }
                if (BACKGROUND_MEM_PROCESS_ERRORS.Count > 0)
                {
                    BlockingMods = BACKGROUND_MEM_PROCESS_ERRORS;
                    e.Result = -2;
                    return;
                }
                else
                {
                    Log.Information("No blocking mods were found.");
                }
            }

            string outDir = getOutputDir((int)e.Argument);
            if (Directory.Exists(outDir)) //Prompt for reinstall or rebuild
            {
                bool deletedOutput = Utilities.DeleteFilesAndFoldersRecursively(outDir);
                if (!deletedOutput)
                {
                    KeyValuePair<string, string> messageStr = new KeyValuePair<string, string>("Unable to cleanup existing output directory", "An error occured deleting the existing output directory. Something may still be accessing it. Close other programs and try again.");
                    BuildWorker.ReportProgress(0, new ThreadCommand(SHOW_DIALOG, messageStr));
                    e.Result = -1; //1 = Error
                    return;
                }
            }
            Directory.CreateDirectory(outDir);


            bool result = ExtractAddons((int)e.Argument); //arg is game id.

            e.Result = result ? (int)e.Argument : -1; //-1 = Build Error
        }

        // Tick handler    
        private void CheckImportLibrary_Tick(object sender, EventArgs e)
        {
            if (PreventFileRefresh)
            {
                return;
            }
            // code to execute periodically
            if (addonfiles != null)
            {
                //Console.WriteLine("Checking for files existence...");
                string basepath = EXE_DIRECTORY + @"Downloaded_Mods\";
                int numdone = 0;

                int numME1Files = 0;
                int numME2Files = 0;
                int numME3Files = 0;
                int numME1FilesReady = 0;
                int numME2FilesReady = 0;
                int numME3FilesReady = 0;
                foreach (AddonFile af in addonfiles)
                {
                    if (af.Game_ME1) numME1Files++;
                    if (af.Game_ME2) numME2Files++;
                    if (af.Game_ME3) numME3Files++;
                    bool ready = File.Exists(basepath + af.Filename);
                    if (!ready && af.UnpackedSingleFilename != null)
                    {
                        //Check for single file
                        ready = File.Exists(basepath + af.UnpackedSingleFilename);
                    }
                    if (af.Ready != ready) //ensure the file applies to something
                    {
                        af.Ready = ready;
                    }

                    if (af.Ready)
                    {
                        if (af.Game_ME1) numME1FilesReady++;
                        if (af.Game_ME2) numME2FilesReady++;
                        if (af.Game_ME3) numME3FilesReady++;
                    }
                    numdone += ready ? 1 : 0;
                    System.Windows.Application.Current.Dispatcher.Invoke(
                    () =>
                    {
                        // Code to run on the GUI thread.
                        Build_ProgressBar.Value = (int)(((double)numdone / addonfiles.Count) * 100);
                        string tickerText = "";
                        tickerText += ShowME1Files ? "ME1: " + numME1FilesReady + "/" + numME1Files : "ME1: N/A";
                        tickerText += " - ";
                        tickerText += ShowME2Files ? "ME2 : " + numME2FilesReady + "/" + numME2Files : "ME2: N/A";
                        tickerText += " - ";
                        tickerText += ShowME3Files ? "ME3 : " + numME3FilesReady + "/" + numME3Files : "ME3: N/A";
                        AddonFilesLabel.Text = tickerText;
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
            var resources = Assembly.GetExecutingAssembly().GetManifestResourceNames();
            if (EXE_DIRECTORY.Length > 95)
            {
                Log.Fatal("EXE is nested too deep for Addon to build properly (" + EXE_DIRECTORY.Length + " chars) due to Windows API limitations.");
                await this.ShowMessageAsync("ALOT Installer is too deep in the filesystem", "ALOT Installer can have issues extracting and building the Addon if nested too deeply in the filesystem. This is an issue with Windows file path limitations. Move the ALOT Installer directory up a few folders on your filesystem. A good place to put ALOT Installer is in Documents.");
                Environment.Exit(1);
            }

            bool hasWriteAccess = await testWriteAccess();
            if (hasWriteAccess) RunApplicationUpdater2();
        }

        private async void SetupButtons()
        {
            string me1Path = Utilities.GetGamePath(1);
            string me2Path = Utilities.GetGamePath(2);
            string me3Path = Utilities.GetGamePath(3);

            //int installedGames = 5;
            me1Installed = (me1Path != null);
            me2Installed = (me2Path != null);
            me3Installed = (me3Path != null);

            Switch_ME1Filter.IsEnabled = ShowME1Files = me1Installed;// me1Installed;
            Switch_ME2Filter.IsEnabled = ShowME2Files = me2Installed;
            Switch_ME3Filter.IsEnabled = ShowME3Files = me3Installed;

            Log.Information("ME1 Installed: " + me1Installed);
            Log.Information("ME2 Installed: " + me2Installed);
            Log.Information("ME3 Installed: " + me3Installed);

            ValidateGameBackup(1);
            ValidateGameBackup(2);
            ValidateGameBackup(3);

            if (me1Installed || me2Installed || me3Installed)
            {
                if (backgroundticker == null)
                {
                    backgroundticker = new DispatcherTimer();
                    backgroundticker.Tick += new EventHandler(CheckImportLibrary_Tick);
                    backgroundticker.Interval = new TimeSpan(0, 0, 5); // execute every 5s
                    backgroundticker.Start();
                    BuildWorker = new BackgroundWorker();
                    BuildWorker.DoWork += BuildAddon;
                    BuildWorker.ProgressChanged += BuildProgressChanged;
                    BuildWorker.RunWorkerCompleted += BuildCompleted;
                    BuildWorker.WorkerReportsProgress = true;
                }

                if (!me1Installed)
                {
                    Log.Information("ME1 not installed - disabling ME1 install");
                    Button_InstallME1.IsEnabled = false;
                    Button_InstallME1.ToolTip = "Mass Effect is not installed. To build the addon for ME1 the game must already be installed";
                    Button_InstallME1.Content = "ME1 Not Installed";
                }
                else
                {
                    Button_InstallME1.IsEnabled = true;
                    Button_InstallME1.ToolTip = "Click to build ALOT Addon and install ALOT for Mass Effect";
                    Button_InstallME1.Content = "Install ALOT for ME1";
                }

                if (!me2Installed)
                {
                    Log.Information("ME2 not installed - disabling ME2 install");
                    Button_InstallME2.IsEnabled = false;
                    Button_InstallME2.ToolTip = "Mass Effect 2 is not installed. To build the addon for ME2 the game must already be installed";
                    Button_InstallME2.Content = "ME2 Not Installed";
                }
                else
                {
                    Button_InstallME2.IsEnabled = true;
                    Button_InstallME2.ToolTip = "Click to build ALOT Addon and install ALOT for Mass Effect 2";
                    Button_InstallME2.Content = "Install ALOT for ME2";
                }

                if (!me3Installed)
                {
                    Log.Information("ME3 not installed - disabling ME3 install");
                    Button_InstallME3.IsEnabled = false;
                    Button_InstallME3.ToolTip = "Mass Effect 3 is not installed. To build the addon for ME3 the game must already be installed";
                    Button_InstallME3.Content = "ME3 Not Installed";
                }
                else
                {
                    Button_InstallME3.IsEnabled = true;
                    Button_InstallME3.ToolTip = "Click to build ALOT Addon and install ALOT for Mass Effect 3";
                    Button_InstallME3.Content = "Install ALOT for ME3";
                }
            }
            else
            {
                Log.Error("No trilogy games are installed. Can't build an addon. Shutting down...");
                await this.ShowMessageAsync("None of the Mass Effect Trilogy games are installed", "ALOT Addon Builder requires at least one of the trilogy games to be installed before you can use it.");
                Environment.Exit(1);
            }
        }

        private bool ValidateGameBackup(int game)
        {
            switch (game)
            {
                case 1:
                    {
                        string path = Utilities.GetGameBackupPath(1);
                        if (path != null)
                        {
                            Button_ME1Backup.Content = "Restore ME1";
                            Button_ME1Backup.ToolTip = "Click to restore game from " + Environment.NewLine + path;
                        }
                        else
                        {
                            Button_ME1Backup.Content = "Backup ME1";
                            Button_ME1Backup.ToolTip = "Click to backup game";
                        }
                        Button_ME1Backup.ToolTip += Environment.NewLine + "Game is installed at " + Environment.NewLine + Utilities.GetGamePath(1, true);
                        return path != null;
                    }
                case 2:
                    {
                        string path = Utilities.GetGameBackupPath(2);
                        if (path != null)
                        {
                            Button_ME2Backup.Content = "Restore ME2";
                            Button_ME2Backup.ToolTip = "Click to restore game from " + Environment.NewLine + path;
                        }
                        else
                        {
                            Button_ME2Backup.Content = "Backup ME2";
                            Button_ME2Backup.ToolTip = "Click to backup game";
                        }
                        Button_ME2Backup.ToolTip += Environment.NewLine + "Game is installed at " + Environment.NewLine + Utilities.GetGamePath(2, true);
                        return path != null;
                    }
                case 3:
                    {
                        string path = Utilities.GetGameBackupPath(3);
                        if (path != null)
                        {
                            Button_ME3Backup.Content = "Restore ME3";
                            Button_ME3Backup.ToolTip = "Click to restore game from " + Environment.NewLine + path;
                        }
                        else
                        {
                            Button_ME3Backup.Content = "Backup ME3";
                            Button_ME3Backup.ToolTip = "Click to backup game";
                        }
                        Button_ME3Backup.ToolTip += Environment.NewLine + "Game is installed at " + Environment.NewLine + Utilities.GetGamePath(3, true);

                        return path != null;
                    }
                default:
                    return false;
            }
        }

        private async Task FetchManifest()
        {
            {
                using (WebClient webClient = new WebClient())
                {
                    webClient.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);
                    Log.Information("Fetching latest manifest from github");
                    Build_ProgressBar.IsIndeterminate = true;
                    AddonFilesLabel.Text = "Downloading latest addon manifest";
                    try
                    {
                        //File.Copy(@"C:\Users\mgame\Downloads\Manifest.xml", EXE_DIRECTORY + @"manifest.xml");
                        string url = "https://raw.githubusercontent.com/Mgamerz/AlotAddOnGUI/master/manifest.xml";
                        if (USING_BETA)
                        {
                            Log.Information("In BETA mode.");
                            url = "https://raw.githubusercontent.com/Mgamerz/AlotAddOnGUI/master/manifest-beta.xml";
                            Title += " BETA MODE ";
                        }
                        await webClient.DownloadFileTaskAsync(url, @"manifest-new.xml");
                        File.Delete(EXE_DIRECTORY + @"manifest.xml");
                        File.Move(EXE_DIRECTORY + @"manifest-new.xml", EXE_DIRECTORY + @"manifest.xml");
                        Log.Information("Manifest fetched.");
                    }
                    catch (WebException e)
                    {
                        File.Delete(EXE_DIRECTORY + @"manifest-new.xml");
                        Log.Error("Exception occured getting manifest from server: " + e.ToString());
                        if (!File.Exists(EXE_DIRECTORY + @"manifest.xml") && File.Exists(EXE_DIRECTORY + @"manifest-bundled.xml"))
                        {
                            Log.Information("Reading bundled manifest instead.");
                            File.Delete(EXE_DIRECTORY + @"manifest.xml");
                            File.Copy(EXE_DIRECTORY + @"manifest-bundled.xml", EXE_DIRECTORY + @"manifest.xml");
                            UsingBundledManifest = true;
                        }
                    }
                    if (!File.Exists(EXE_DIRECTORY + @"manifest.xml"))
                    {
                        Log.Fatal("No local manifest exists to use, exiting...");
                        await this.ShowMessageAsync("No Manifest Available", "An error occured downloading the manifest for addon. Information that is required to build the addon is not available. Check the program logs.");
                        Environment.Exit(1);
                    }
                    Button_Settings.IsEnabled = true;
                    readManifest();

                    Log.Information("readManifest() has completed. Switching over to user control");
                    FirstRunFlyout.IsOpen = true;
                    Loading = false;
                    Build_ProgressBar.IsIndeterminate = false;
                    HeaderLabel.Text = PRIMARY_HEADER;
                    AddonFilesLabel.Text = "Scanning...";
                    CheckImportLibrary_Tick(null, null);
                    RunMEMUpdater2();
                    UpdateALOTStatus();

                    RunMEMUpdaterGUI();
                }
            }
        }

        private void UpdateALOTStatus()
        {
            CURRENTLY_INSTALLED_ME1_ALOT_INFO = Utilities.GetInstalledALOTInfo(1);
            CURRENTLY_INSTALLED_ME2_ALOT_INFO = Utilities.GetInstalledALOTInfo(2);
            CURRENTLY_INSTALLED_ME3_ALOT_INFO = Utilities.GetInstalledALOTInfo(3);


            string me1ver = "";
            string me2ver = "";
            string me3ver = "";


            if (CURRENTLY_INSTALLED_ME1_ALOT_INFO != null)
            {
                if (CURRENTLY_INSTALLED_ME1_ALOT_INFO.ALOTVER > 0)
                {
                    me1ver =CURRENTLY_INSTALLED_ME1_ALOT_INFO.ALOTVER + "." + CURRENTLY_INSTALLED_ME1_ALOT_INFO.ALOTUPDATEVER;
                }
                else
                {
                    me1ver = "Installed, unable to detect version";
                }
            }
            else
            {
                me1ver = "Not Installed";
            }

            if (CURRENTLY_INSTALLED_ME2_ALOT_INFO != null)
            {
                if (CURRENTLY_INSTALLED_ME2_ALOT_INFO.ALOTVER > 0)
                {
                    me2ver = CURRENTLY_INSTALLED_ME2_ALOT_INFO.ALOTVER + "." + CURRENTLY_INSTALLED_ME2_ALOT_INFO.ALOTUPDATEVER;
                }
                else
                {
                    me2ver = "Installed, unable to detect version";
                }
            }
            else
            {
                me2ver = "Not Installed";
            }

            if (CURRENTLY_INSTALLED_ME3_ALOT_INFO != null)
            {
                if (CURRENTLY_INSTALLED_ME3_ALOT_INFO.ALOTVER > 0)
                {
                    me3ver = CURRENTLY_INSTALLED_ME3_ALOT_INFO.ALOTVER + "." + CURRENTLY_INSTALLED_ME3_ALOT_INFO.ALOTUPDATEVER;
                }
                else
                {
                    me3ver = "Installed, unable to detect version";
                }
            }
            else
            {
                me3ver = "Not Installed";
            }

            string me1ToolTip = CURRENTLY_INSTALLED_ME1_ALOT_INFO != null ? "ALOT detected as installed" : "ALOT not detected as installed";
            string me2ToolTip = CURRENTLY_INSTALLED_ME2_ALOT_INFO != null ? "ALOT detected as installed" : "ALOT not detected as installed";
            string me3ToolTip = CURRENTLY_INSTALLED_ME3_ALOT_INFO != null ? "ALOT detected as installed" : "ALOT not detected as installed";

            string message1 = "ME1: " + me1ver;
            string message2 = "ME2: " + me2ver;
            string message3 = "ME3: " + me3ver;

            Label_ALOTStatus_ME1.Content = message1;
            Label_ALOTStatus_ME2.Content = message2;
            Label_ALOTStatus_ME3.Content = message3;

            Label_ALOTStatus_ME1.ToolTip = me1ToolTip;
            Label_ALOTStatus_ME2.ToolTip = me2ToolTip;
            Label_ALOTStatus_ME3.ToolTip = me3ToolTip;

            Button_ME1_ShowLODOptions.Visibility = CURRENTLY_INSTALLED_ME1_ALOT_INFO != null ? Visibility.Visible : Visibility.Collapsed;
            foreach (AddonFile af in addonfiles)
            {
                af.ReadyStatusText = af.ReadyStatusText; //update description
            }
        }

        private async void readManifest()
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
                                Showing = false,
                                ProcessAsModFile = e.Attribute("processasmodfile") != null ? (bool)e.Attribute("processasmodfile") : false,
                                Author = (string)e.Attribute("author"),
                                FriendlyName = (string)e.Attribute("friendlyname"),
                                Game_ME1 = e.Element("games") != null ? (bool)e.Element("games").Attribute("me1") : false,
                                Game_ME2 = e.Element("games") != null ? (bool)e.Element("games").Attribute("me2") : false,
                                Game_ME3 = e.Element("games") != null ? (bool)e.Element("games").Attribute("me3") : false,
                                Filename = (string)e.Element("file").Attribute("filename"),
                                Tooltipname = e.Attribute("tooltipname") != null ? (string)e.Attribute("tooltipname") : (string)e.Attribute("friendlyname"),
                                DownloadLink = (string)e.Element("file").Attribute("downloadlink"),
                                ALOTVersion = e.Attribute("alotversion") != null ? Convert.ToInt16((string)e.Attribute("alotversion")) : (short)0,
                                ALOTUpdateVersion = e.Attribute("alotupdateversion") != null ? Convert.ToByte((string)e.Attribute("alotupdateversion")) : (byte)0,
                                UnpackedSingleFilename = e.Element("file").Attribute("unpackedsinglefilename") != null ? (string)e.Element("file").Attribute("unpackedsinglefilename") : null,
                                ALOTMainVersionRequired = e.Attribute("appliestomainversion") != null ? Convert.ToInt16((string)e.Attribute("appliestomainversion")) : (short)0,
                                Ready = false,
                                PackageFiles = e.Elements("packagefile")
                                    .Select(r => new PackageFile
                                    {
                                        SourceName = (string)r.Attribute("sourcename"),
                                        DestinationName = (string)r.Attribute("destinationname"),
                                        TPFSource = (string)r.Attribute("tpfsource"),
                                        MoveDirectly = r.Attribute("movedirectly") != null ? true : false,
                                        CopyDirectly = r.Attribute("copydirectly") != null ? true : false,
                                        Delete = r.Attribute("delete") != null ? true : false,
                                        ME1 = r.Attribute("me1") != null ? true : false,
                                        ME2 = r.Attribute("me2") != null ? true : false,
                                        ME3 = r.Attribute("me3") != null ? true : false,
                                        Processed = false
                                    }).ToList(),
                            }).ToList();
                if (!version.Equals(""))
                {
                    Title += " - Manifest version " + version;
                    if (UsingBundledManifest)
                    {
                        Title += " (Bundled)";
                        Log.Information("Using bundled manifest. Something might be wrong...");
                    }

                }
                //throw new Exception("Test error.");
            }
            catch (Exception e)
            {
                Log.Error("Error has occured parsing the XML!");
                Log.Error(e.Message);
                MessageDialogResult result = await this.ShowMessageAsync("Error reading Addon manifest", "An error occured while reading the manifest file for buildable addons. This may indicate a network failure or a packaging failure by Mgamerz - Please submit an issue to github (http://github.com/mgamerz/alotaddongui/issues) and include the most recent log file from the logs directory.\n\n" + e.Message, MessageDialogStyle.Affirmative);
                AddonFilesLabel.Text = "Error parsing manifest XML! Check the logs.";
                return;
            }
            linqlist = linqlist.OrderBy(o => o.Author).ThenBy(x => x.FriendlyName).ToList();
            alladdonfiles = new BindingList<AddonFile>(linqlist);
            addonfiles = alladdonfiles;
            //get list of installed games
            SetupButtons();

            foreach (AddonFile af in addonfiles)
            {
                //Set Game
                foreach (PackageFile pf in af.PackageFiles)
                {
                    //Damn I did not think this one through very well
                    af.Game_ME1 |= pf.ME1;
                    af.Game_ME2 |= pf.ME2;
                    af.Game_ME3 |= pf.ME3;
                    if (!af.Game_ME1 && !af.Game_ME2 && !af.Game_ME3)
                    {
                        af.Game_ME1 = af.Game_ME2 = af.Game_ME3 = true; //if none is set, then its set to all
                    }
                }
            }
            UpdateALOTStatus();
            ApplyFiltering(); //sets data source and separators            
        }

        private void ApplyFiltering()
        {

            BindingList<AddonFile> newList = new BindingList<AddonFile>();
            foreach (AddonFile af in alladdonfiles)
            {
                bool shouldDisplay = ((af.Game_ME1 && ShowME1Files) || (af.Game_ME2 && ShowME2Files) || (af.Game_ME3 && ShowME3Files));

                if (shouldDisplay)
                {
                    newList.Add(af);
                }
            }
            addonfiles = newList;
            lvUsers.ItemsSource = addonfiles;
            CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(lvUsers.ItemsSource);
            PropertyGroupDescription groupDescription = new PropertyGroupDescription("Author");
            view.GroupDescriptions.Add(groupDescription);
            CheckImportLibrary_Tick(null, null);
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
                if (await InitInstall(2))
                {
                    Loading = true; //prevent refresh when filtering
                    ShowME1Files = false;
                    ShowME2Files = true;
                    ShowME3Files = false;
                    Loading = false;
                    ApplyFiltering();
                    Button_InstallME2.Content = "Building...";
                    CURRENT_GAME_BUILD = 2;
                    TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.Normal, this);
                    BuildWorker.RunWorkerAsync(2);
                }
            }
        }

        private async void Button_ME1Backup_Click(object sender, RoutedEventArgs e)
        {
            if (ValidateGameBackup(1))
            {
                //Game is backed up
                MetroDialogSettings settings = new MetroDialogSettings();
                settings.NegativeButtonText = "Cancel";
                settings.AffirmativeButtonText = "Restore";
                MessageDialogResult result = await this.ShowMessageAsync("Restoring Mass Effect to unmodified state", "Restoring Mass Effect will wipe out all mods and put your game back to an unmodified state. Are you sure you want to do this?", MessageDialogStyle.AffirmativeAndNegative, settings);
                if (result == MessageDialogResult.Affirmative)
                {
                    //RESTORE
                    RestoreGame(1);
                }
            }
            else
            {
                //MEM - VERIFY VANILLA FOR BACKUP
                BackupGame(1);
            }
        }
        private async void Button_ME2Backup_Click(object sender, RoutedEventArgs e)
        {
            if (ValidateGameBackup(2))
            {
                //Game is backed up
                MetroDialogSettings settings = new MetroDialogSettings();
                settings.NegativeButtonText = "Cancel";
                settings.AffirmativeButtonText = "Restore";
                MessageDialogResult result = await this.ShowMessageAsync("Restoring ME2 to unmodified state", "Restoring Mass Effect 2 will wipe out all mods and put your game back to an unmodified state. Are you sure you want to do this?", MessageDialogStyle.AffirmativeAndNegative, settings);
                if (result == MessageDialogResult.Affirmative)
                {
                    //RESTORE
                    RestoreGame(2);
                }
            }
            else
            {
                //MEM - VERIFY VANILLA FOR BACKUP
                BackupGame(2);
            }
        }

        private async void Button_ME3Backup_Click(object sender, RoutedEventArgs e)
        {
            if (ValidateGameBackup(3))
            {
                //Game is backed up
                MetroDialogSettings settings = new MetroDialogSettings();
                settings.NegativeButtonText = "Cancel";
                settings.AffirmativeButtonText = "Restore";
                MessageDialogResult result = await this.ShowMessageAsync("Restoring game to unmodified state", "Restoring your game will wipe out all mods and put your game back to an unmodified state. Are you sure you want to do this?", MessageDialogStyle.AffirmativeAndNegative, settings);
                if (result == MessageDialogResult.Affirmative)
                {
                    //RESTORE
                    RestoreGame(3);
                }
            }
            else
            {
                //Addon-Precheck
                List<string> folders = ME3Constants.getStandardDLCFolders();
                string me3DLCPath = ME3Constants.GetDLCPath();
                List<string> dlcFolders = new List<string>();
                foreach (string s in Directory.GetDirectories(me3DLCPath))
                {
                    dlcFolders.Add(s.Remove(0, me3DLCPath.Length + 1)); //+1 for the final \\
                }
                var hasCustomDLC = dlcFolders.Except(folders);
                if (hasCustomDLC.Count() > 0)
                {
                    //Game is modified
                    string message = "Additional folders in the DLC directory were detected:";
                    foreach (string str in hasCustomDLC)
                    {
                        message += "\n - " + str;
                    }

                    message += "\n\nThis installation cannot be used for backup as it has been modified.";
                    await this.ShowMessageAsync("Mass Effect 3 is modified", message);
                    return;
                }
                //MEM - VERIFY VANILLA FOR BACKUP

                BackupGame(3);

            }
        }

        private async void BackupGame(int game)
        {
            if (Utilities.GetInstalledALOTInfo(game) != null)
            {
                //Game is modified via ALOT flag
                await this.ShowMessageAsync("ALOT is installed", "You cannot backup an installation that has ALOT already installed. If you have a backup, you can restore it by clicking the game backup button in the Settings menu. Otherwise, delete your game folder and redownload it.");
                return;
            }

            var openFolder = new CommonOpenFileDialog();
            openFolder.IsFolderPicker = true;
            openFolder.Title = "Select backup destination";
            openFolder.AllowNonFileSystemItems = false;
            openFolder.EnsurePathExists = true;
            if (openFolder.ShowDialog() != CommonFileDialogResult.Ok)
            {
                return;
            }
            var dir = openFolder.FileName;
            if (!Directory.Exists(dir))
            {
                await this.ShowMessageAsync("Directory does not exist", "The backup destination directory does not exist: " + dir);
                return;
            }
            if (!Utilities.IsDirectoryEmpty(dir))
            {
                await this.ShowMessageAsync("Directory is not empty", "The backup destination directory must be empty.");
                return;
            }
            BackupWorker = new BackgroundWorker();
            BackupWorker.DoWork += BackupGame;
            BackupWorker.WorkerReportsProgress = true;
            BackupWorker.ProgressChanged += BackupWorker_ProgressChanged;
            BackupWorker.RunWorkerCompleted += BackupCompleted;
            BACKUP_THREAD_GAME = game;
            SettingsFlyout.IsOpen = false;
            PreventFileRefresh = true;
            HeaderLabel.Text = "Backing up Mass Effect" + (game == 1 ? "" : " " + game) + "...\nDo not close the application until this process completes.";
            BackupWorker.RunWorkerAsync(dir);
            Button_InstallME1.IsEnabled = Button_InstallME2.IsEnabled = Button_InstallME3.IsEnabled = Button_Settings.IsEnabled = false;
            ShowStatus("Verifying game data before backup", 6000);
            // get all the directories in selected dirctory
        }

        private void BackupCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            string destPath = (string)e.Result;
            if (destPath != null)
            {
                //Write registry key
                switch (BACKUP_THREAD_GAME)
                {
                    case 1:
                    case 2:
                        Utilities.WriteRegistryKey(Registry.CurrentUser, REGISTRY_KEY, "ME" + BACKUP_THREAD_GAME + "VanillaBackupLocation", destPath);
                        break;
                    case 3:
                        Utilities.WriteRegistryKey(Registry.CurrentUser, ME3_BACKUP_REGISTRY_KEY, "VanillaCopyLocation", destPath);
                        break;
                }
                ValidateGameBackup(BACKUP_THREAD_GAME);
                AddonFilesLabel.Text = "Backup completed.";
            }
            else
            {
                AddonFilesLabel.Text = "Backup failed! Check the logs.";
            }
            Button_Settings.IsEnabled = true;
            SetupButtons();
            PreventFileRefresh = false;
            ValidateGameBackup(BACKUP_THREAD_GAME);
            BACKUP_THREAD_GAME = -1;
            HeaderLabel.Text = PRIMARY_HEADER;
        }

        private string getGameNumberSuffix(int gameNumber)
        {
            return gameNumber == 1 ? "" : " " + gameNumber;
        }

        private async void Button_InstallME3_Click(object sender, RoutedEventArgs e)
        {
            if (await InstallPrecheck(3))
            {
                if (await InitInstall(3))
                {
                    Loading = true; //prevent refresh when filtering
                    ShowME1Files = false;
                    ShowME2Files = false;
                    ShowME3Files = true;
                    Loading = false;
                    ApplyFiltering();
                    Button_InstallME3.Content = "Building...";
                    CURRENT_GAME_BUILD = 3;
                    TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.Normal, this);

                    BuildWorker.RunWorkerAsync(3);
                }
            }
        }

        private async Task<bool> InstallPrecheck(int game)
        {
            CheckImportLibrary_Tick(null, null);
            int nummissing = 0;
            bool oneisready = false;
            ALOTVersionInfo installedInfo = Utilities.GetInstalledALOTInfo(game);
            bool blockDueToMissingALOTFile = installedInfo == null; //default value
            int installedALOTUpdateVersion = (installedInfo == null) ? 0 : installedInfo.ALOTUPDATEVER;
            bool blockDueToMissingALOTUpdateFile = false; //default value

            foreach (AddonFile af in addonfiles)
            {
                if ((af.Game_ME1 && game == 1) || (af.Game_ME2 && game == 2) || (af.Game_ME3 && game == 3))
                {
                    if (blockDueToMissingALOTFile)
                    {
                        if (af.ALOTVersion > 0 && af.Ready)
                        {
                            //Do not block as ALOT file is ready and will be installed
                            blockDueToMissingALOTFile = false;

                        }
                        else if (af.ALOTVersion > 0 && !af.Ready)
                        {
                            Log.Warning("Installation for ME" + game + " being blocked due to ALOT main file not installed and is required.");
                            break;
                        }
                    }

                    if (af.ALOTUpdateVersion > installedALOTUpdateVersion)
                    {

                        if (!af.Ready)
                        {
                            blockDueToMissingALOTUpdateFile = true;
                            Log.Warning("Installation for ME" + game + " being blocked due to ALOT Update available that is not ready");
                            break;
                        }
                    }

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

            if (blockDueToMissingALOTFile)
            {
                await this.ShowMessageAsync("ALOT main file is missing", "ALOT's main file for Mass Effect" + getGameNumberSuffix(game) + " is not imported. This file must be imported to run the installer when ALOT is not installed.");
                return false;
            }

            if (blockDueToMissingALOTUpdateFile)
            {
                if (installedInfo == null)
                {
                    await this.ShowMessageAsync("ALOT update file is missing", "ALOT for Mass Effect" + getGameNumberSuffix(game) + " has an update file, but it not currently imported. This update must be imported in order to install ALOT for the first time so you have the most up to date installation.");
                }
                else
                {
                    await this.ShowMessageAsync("ALOT update file is missing", "ALOT for Mass Effect" + getGameNumberSuffix(game) + " has an update available that is not yet applied. This update must be imported in order to continue.");
                }
                return false;
            }

            if (nummissing == 0)
            {
                return true;
            }

            if (!oneisready)
            {
                await this.ShowMessageAsync("No files available for building", "There are no files available or relevant in the Downloaded_Mods folder to install for Mass Effect" + getGameNumberSuffix(game) + ".");
                return false;
            }

            MessageDialogResult result = await this.ShowMessageAsync(nummissing + " file" + (nummissing != 1 ? "s are" : " is") + " missing", "Some files for the Mass Effect" + getGameNumberSuffix(game) + " addon are missing - do you want to build the addon without these files?", MessageDialogStyle.AffirmativeAndNegative);
            return result == MessageDialogResult.Affirmative;
        }



        /// <summary>
        /// Returns the mem output dir with a \ on the end.
        /// </summary>
        /// <param name="game">Game number to get path for</param>
        /// <returns></returns>
        private String getOutputDir(int game, bool trailingSlash = true)
        {
            string ret = EXE_DIRECTORY + MEM_OUTPUT_DIR + "\\ME" + game;
            if (trailingSlash)
            {
                ret += "\\";
            }
            return ret;
        }

        private async void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            string fname = (string)((Hyperlink)e.Source).Tag;

            try
            {
                Log.Information("Opening URL: " + e.Uri.ToString());
                System.Diagnostics.Process.Start(e.Uri.ToString());
                this.nIcon.Visible = true;
                //this.WindowState = System.Windows.WindowState.Minimized;
                this.nIcon.Icon = Properties.Resources.tooltiptrayicon;
                this.nIcon.ShowBalloonTip(14000, "Directions", "Download the file with filename: \"" + fname + "\"", ToolTipIcon.Info);
            }
            catch (Exception other)
            {
                Log.Error("Exception opening browser - handled. The error was " + other.Message);
                System.Windows.Clipboard.SetText(e.Uri.ToString());
                await this.ShowMessageAsync("Unable to open web browser", "Unable to open your default web browser. Open your browser and paste the link (already copied to clipboard) into your URL bar. Download the file named " + fname + ", then drag and drop it onto this program's interface.");
            }
        }

        private async Task<bool> InitInstall(int game)
        {
            AddonFilesLabel.Text = "Preparing to install...";
            Build_ProgressBar.IsIndeterminate = true;
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
                await this.ShowMessageAsync("Error occured while preparing directories", "ALOT Installer was unable to cleanup some directories. Make sure all file explorer windows are closed that may be open in the working directories.");
                return false;
            }

            PreventFileRefresh = true;
            Button_InstallME1.IsEnabled = false;
            Button_InstallME2.IsEnabled = false;
            Button_InstallME3.IsEnabled = false;
            Button_Settings.IsEnabled = false;

            Directory.CreateDirectory(MEM_STAGING_DIR);


            HeaderLabel.Text = "Preparing to build ALOT Addon for Mass Effect " + game + ".\nDon't close this window until the process completes.";
            // Install_ProgressBar.IsIndeterminate = true;
            return true;
        }

        private async void File_Drop_BackgroundThread(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                if (PreventFileRefresh)
                {
                    ShowStatus("Dropping files onto interface not available during operation", 5000);
                    return;
                }
                // Note that you can have more than one file.
                string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
                if (files.Count() > 0)
                {
                    //don't know how you can drop less than 1 files but whatever
                    //This code is for failsafe in case somehow library file exists but is not detect properly, like user moved file but something is running
                    string file = files[0];
                    string basepath = System.AppDomain.CurrentDomain.BaseDirectory + @"Downloaded_Mods\";

                    if (file.ToLower().StartsWith(basepath))
                    {
                        ShowStatus("Can't import files from Downloaded_Mods", 5000);
                        return;
                    }
                }
                Log.Information("Files dropped:");
                foreach (String file in files)
                {
                    Log.Information(" -" + file);
                }
                List<Tuple<AddonFile, string, string>> filesToImport = new List<Tuple<AddonFile, string, string>>();
                // Assuming you have one file that you care about, pass it off to whatever
                // handling code you have defined.
                List<string> noMatchFiles = new List<string>();
                long totalBytes = 0;
                foreach (string file in files)
                {
                    string fname = Path.GetFileName(file);
                    //remove (1) and such
                    string fnameWithoutExtension = Path.GetFileNameWithoutExtension(file);
                    if (fnameWithoutExtension.EndsWith(")"))
                    {
                        if (fnameWithoutExtension.LastIndexOf("(") >= fnameWithoutExtension.Length - 3)
                        {
                            //it's probably a copy
                            fname = fnameWithoutExtension.Remove(fnameWithoutExtension.LastIndexOf("("), fnameWithoutExtension.LastIndexOf(")") - fnameWithoutExtension.LastIndexOf("(") + 1).Trim() + Path.GetExtension(file);
                            Log.Information("File Drag/Drop corrected to " + fname);
                        }
                    }

                    bool hasMatch = false;
                    foreach (AddonFile af in addonfiles)
                    {
                        if (af.ALOTVersion > 0 && af.Game_ME3)
                        {
                            Debug.WriteLine("BREAK");
                        }

                        bool isUnpackedSingleFile = af.UnpackedSingleFilename != null && af.UnpackedSingleFilename.Equals(fname, StringComparison.InvariantCultureIgnoreCase);

                        if (isUnpackedSingleFile || af.Filename.Equals(fname, StringComparison.InvariantCultureIgnoreCase))
                        {
                            hasMatch = true;
                            if (af.Ready == false)
                            {
                                //Copy file to directory
                                string basepath = System.AppDomain.CurrentDomain.BaseDirectory + @"Downloaded_Mods\";
                                string destination = basepath + ((isUnpackedSingleFile) ? af.UnpackedSingleFilename : af.Filename);
                                //Log.Information("Copying dragged file to downloaded mods directory: " + file);
                                //File.Copy(file, destination, true);
                                filesToImport.Add(Tuple.Create(af, file, destination));
                                totalBytes += new System.IO.FileInfo(file).Length;
                                Debug.WriteLine("new TotalBytes: " + totalBytes);
                                //filesimported.Add(af);
                                //timer_Tick(null, null);
                                break;
                            }
                        }
                    }
                    if (!hasMatch)
                    {
                        noMatchFiles.Add(file);
                        Log.Information("Dragged file does not match any addon manifest file: " + file);
                    }
                } //END LOOP
                if (noMatchFiles.Count > 0)
                {
                    if (noMatchFiles.Count == 1)
                    {
                        ShowStatus("Not a supported file: " + Path.GetFileName(noMatchFiles[0]));
                    }
                    else
                    {
                        ShowStatus(noMatchFiles.Count + " files were dropped that aren't supported files");
                    }
                }

                if (noMatchFiles.Count == 0 && filesToImport.Count == 0)
                {
                    ShowStatus("All dropped files are already imported");

                }

                if (filesToImport.Count > 0)
                {
                    MetroDialogSettings settings = new MetroDialogSettings();
                    ProgressDialogController updateprogresscontroller = await this.ShowProgressAsync("Importing files", "ALOT Installer is importing files, please wait...", false, settings);
                    updateprogresscontroller.SetIndeterminate();
                    ImportFiles(filesToImport, new List<string>(), updateprogresscontroller, 0, totalBytes);
                }
            }
        }


        private async void ImportFiles(List<Tuple<AddonFile, string, string>> filesToImport, List<string> importedFiles, ProgressDialogController progressController, long processedBytes, long totalBytes)
        {
            PreventFileRefresh = true;
            Tuple<AddonFile, string, string> fileToImport = filesToImport[0];
            progressController.SetMessage("ALOT Installer is importing files, please wait...\nImporting " + fileToImport.Item1.FriendlyName);
            filesToImport.RemoveAt(0);


            if ((bool)Checkbox_MoveFilesAsImport.IsChecked)
            {
                //MOVE
                await Utilities.MoveAsync(fileToImport.Item2, fileToImport.Item3);
                //Update progressbar
                processedBytes += new System.IO.FileInfo(fileToImport.Item3).Length;
                double progress = (((double)processedBytes / totalBytes));
                progressController.SetProgress(progress);
                importedFiles.Add(fileToImport.Item1.FriendlyName);
                if (filesToImport.Count > 0)
                {
                    ImportFiles(filesToImport, importedFiles, progressController, processedBytes, totalBytes);
                }
                else
                {
                    //imports finished
                    await progressController.CloseAsync();
                    string detailsMessage = "The following files were just imported to ALOT Installer. The files have been moved to the Downloaded_Mods folder.";
                    foreach (string af in importedFiles)
                    {
                        detailsMessage += "\n - " + af;
                    }
                    PreventFileRefresh = false; //allow refresh

                    string originalTitle = importedFiles.Count + " file" + (importedFiles.Count != 1 ? "s" : "") + " imported";
                    string originalMessage = importedFiles.Count + " file" + (importedFiles.Count != 1 ? "s have" : " has") + " been moved into the Downloaded_Mods directory.";

                    ShowImportFinishedMessage(originalTitle, originalMessage, detailsMessage);
                }
            }
            else
            {
                //COPY
                WebClient downloadClient = new WebClient();
                long preDownloadStartBytes = processedBytes;
                downloadClient.DownloadProgressChanged += (s, e) =>
                {
                    long currentBytes = preDownloadStartBytes;
                    currentBytes += e.BytesReceived;
                    double progress = (((double)currentBytes / totalBytes));
                    progressController.SetProgress(progress);
                };
                downloadClient.DownloadFileCompleted += async (s, e) =>
                {
                    processedBytes += new System.IO.FileInfo(fileToImport.Item3).Length;
                    importedFiles.Add(fileToImport.Item1.FriendlyName);
                    if (filesToImport.Count > 0)
                    {
                        ImportFiles(filesToImport, importedFiles, progressController, processedBytes, totalBytes);
                    }
                    else
                    {
                        //imports finished
                        await progressController.CloseAsync();
                        string detailsMessage = "The following files were just imported to ALOT Installer:";
                        foreach (string af in importedFiles)
                        {
                            detailsMessage += "\n - " + af;
                        }
                        PreventFileRefresh = false; //allow refresh

                        string originalTitle = importedFiles.Count + " file" + (importedFiles.Count != 1 ? "s" : "") + " imported";
                        string originalMessage = importedFiles.Count + " file" + (importedFiles.Count != 1 ? "s" : "") + " have been copied into the Downloaded_Mods directory.";

                        ShowImportFinishedMessage(originalTitle, originalMessage, detailsMessage);
                    }
                };
                downloadClient.DownloadFileAsync(new Uri(fileToImport.Item2), fileToImport.Item3);
            }
        }

        private async void ShowImportFinishedMessage(string originalTitle, string originalMessage, string detailsMessage)
        {
            MetroDialogSettings settings = new MetroDialogSettings();
            settings.NegativeButtonText = "OK";
            settings.AffirmativeButtonText = "Details";
            MessageDialogResult result = await this.ShowMessageAsync(originalTitle, originalMessage, MessageDialogStyle.AffirmativeAndNegative, settings);
            if (result == MessageDialogResult.Affirmative)
            {
                await this.ShowMessageAsync(originalTitle, detailsMessage);
            }
        }

        private void ShowStatus(string v, int msOpen = 6000)
        {
            StatusFlyout.AutoCloseInterval = msOpen;
            StatusLabel.Text = v;
            StatusFlyout.IsOpen = true;
        }

        private void Button_Settings_Click(object sender, RoutedEventArgs e)
        {
            SettingsFlyout.IsOpen = true;
        }

        private async Task<bool> testWriteAccess()
        {
            try
            {
                using (var file = File.Create("write_permissions_test")) ;
                File.Delete("write_permissions_test");
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                await this.ShowMessageAsync("Running from write-protected directory", "Your user account doesn't have write permissions to the current directory. Move ALOT Addon Builder to somewhere where yours does, like the Documents folder.");
                Environment.Exit(1);
                return false;
            }
            catch (Exception e)
            {
                //do nothing with other ones, I guess.
                Log.Error("Permissions test failure: " + e.Message);
            }
            return true;
        }

        private async void Button_InstallME1_Click(object sender, RoutedEventArgs e)
        {
            if (await InstallPrecheck(1))
            {
                if (await InitInstall(1))
                {
                    Loading = true; //prevent refresh when filtering
                    ShowME1Files = true;
                    ShowME2Files = false;
                    ShowME3Files = false;
                    Loading = false;
                    ApplyFiltering();
                    Button_InstallME1.Content = "Building...";
                    CURRENT_GAME_BUILD = 1;
                    TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.Normal, this);
                    BuildWorker.RunWorkerAsync(1);
                }
            }
        }

        private void Button_ViewLog_Click(object sender, RoutedEventArgs e)
        {
            var directory = new DirectoryInfo("logs");
            FileInfo latestlogfile = directory.GetFiles().OrderByDescending(f => f.LastWriteTime).First();
            if (latestlogfile != null)
            {
                ProcessStartInfo psi = new ProcessStartInfo(EXE_DIRECTORY + "logs\\" + latestlogfile.ToString());
                psi.UseShellExecute = true;
                Process.Start(psi);
            }
        }

        private void LoadSettings()
        {
            bool importasmove = Utilities.GetRegistrySettingBool(SETTINGSTR_IMPORTASMOVE) ?? true;
            USING_BETA = Utilities.GetRegistrySettingBool(SETTINGSTR_BETAMODE) ?? false;

            Checkbox_BetaMode.IsChecked = USING_BETA;
            Checkbox_MoveFilesAsImport.IsChecked = importasmove;

            if (USING_BETA)
            {
                ThemeManager.ChangeAppStyle(System.Windows.Application.Current,
                                                    ThemeManager.GetAccent("Crimson"),
                                                    ThemeManager.GetAppTheme("BaseDark")); // or appStyle.Item1
            }
        }

        private void Button_ReportIssue_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://discord.gg/w4Smese");
        }


        private void RestoreGame(int game)
        {
            BackupWorker = new BackgroundWorker();
            BackupWorker.DoWork += RestoreGame;
            BackupWorker.WorkerReportsProgress = true;
            BackupWorker.ProgressChanged += BackupWorker_ProgressChanged;
            BackupWorker.RunWorkerCompleted += RestoreCompleted;
            BACKUP_THREAD_GAME = game;
            SettingsFlyout.IsOpen = false;
            Button_Settings.IsEnabled = false;
            PreventFileRefresh = true;
            HeaderLabel.Text = "Restoring Mass Effect" + (game == 1 ? "" : " " + game) + "...\nDo not close the application until this process completes.";
            Button_InstallME1.IsEnabled = Button_InstallME2.IsEnabled = Button_InstallME3.IsEnabled = Button_Settings.IsEnabled = false;
            TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.Normal, this);
            TaskbarManager.Instance.SetProgressValue(0, 0);
            BackupWorker.RunWorkerAsync();
        }

        private async void RestoreCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.NoProgress, this);

            bool result = (bool)e.Result;
            if (result)
            {
                AddonFilesLabel.Text = "Restore completed.";
                await this.ShowMessageAsync("Restore completed", "Mass Effect" + getGameNumberSuffix(BACKUP_THREAD_GAME) + " has been restored back to an unmodified state from backup.");
            }
            else
            {
                AddonFilesLabel.Text = "Restore failed! Check the logs.";
            }
            SetupButtons();
            UpdateALOTStatus();
            Button_Settings.IsEnabled = true;
            PreventFileRefresh = false;

            BACKUP_THREAD_GAME = -1;
            HeaderLabel.Text = PRIMARY_HEADER;
        }

        private async void Checkbox_BetaMode_Click(object sender, RoutedEventArgs e)
        {
            bool isEnabling = (bool)Checkbox_BetaMode.IsChecked;
            if (isEnabling)
            {
                MessageDialogResult result = await this.ShowMessageAsync("Enabling BETA mode", "Enabling BETA mode will enable the beta manifest as well as beta features and beta updates. These builds are for testing, and may not be stable (and will sometimes outright crash). Unless you're OK with this you should stay in normal mode.\nEnable BETA mode?", MessageDialogStyle.AffirmativeAndNegative);
                if (result == MessageDialogResult.Negative)
                {
                    Checkbox_BetaMode.IsChecked = false;
                }
            }
            Utilities.WriteRegistryKey(Registry.CurrentUser, REGISTRY_KEY, SETTINGSTR_BETAMODE, ((bool)Checkbox_BetaMode.IsChecked ? 1 : 0));
            USING_BETA = (bool)Checkbox_BetaMode.IsChecked;
            if (isEnabling && Checkbox_BetaMode.IsChecked.Value)
            {
                System.Windows.Forms.Application.Restart();
                Environment.Exit(0);
            }
        }

        private void Button_MEMVersion_Click(object sender, RoutedEventArgs e)
        {
            ShowStatus("Starting MassEffectModder.exe", 3000);
            SettingsFlyout.IsOpen = false;
            string ini = BINARY_DIRECTORY + "Installer.ini";
            if (File.Exists(ini))
            {
                File.Delete(ini);
            }

            string exe = BINARY_DIRECTORY + "MassEffectModder.exe";
            string args = "";// "-install-addon-file \""+path+"\" -game "+result;
            Utilities.runProcess(exe, args, true);
        }

        private void InstallingOverlayFlyout_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            //Allow installing UI overlay to be window drag
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void Button_InstallDone_Click(object sender, RoutedEventArgs e)
        {
            SetInstallFlyoutState(false);
        }

        private void Checkbox_MoveFilesAsImport_Click(object sender, RoutedEventArgs e)
        {
            bool settingVal = (bool)Checkbox_MoveFilesAsImport.IsChecked;
            Utilities.WriteRegistryKey(Registry.CurrentUser, REGISTRY_KEY, SETTINGSTR_IMPORTASMOVE, ((bool)Checkbox_MoveFilesAsImport.IsChecked ? 1 : 0));
        }

        private async void Window_Closing(object sender, CancelEventArgs e)
        {
            if (WARN_USER_OF_EXIT)
            {
                Log.Information("User is attempting to close program while installer is running.");
                e.Cancel = true;

                MetroDialogSettings mds = new MetroDialogSettings();
                mds.AffirmativeButtonText = "Yes";
                mds.NegativeButtonText = "No";
                mds.DefaultButtonFocus = MessageDialogResult.Negative;

                MessageDialogResult result = await this.ShowMessageAsync("Closing ALOT Installer may leave game in a broken state", "MEM is currently installing textures. Closing the program will likely leave your game in an unplayable, broken state. Are you sure you want to exit?", MessageDialogStyle.AffirmativeAndNegative, mds);
                if (result == MessageDialogResult.Affirmative)
                {
                    Log.Error("User has chosen to kill MEM and close program. Game will likely be broken.");
                    WARN_USER_OF_EXIT = false;
                    Close();
                }
            }
        }

        private void ContextMenu_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            System.Windows.Controls.ListView source = e.Source as System.Windows.Controls.ListView;
            var selectedItem = source.SelectedItem;
            var originalSource = e.OriginalSource;
            var items = source.Items;
        }

        private void Button_InstallerLOD4k_Click(object sender, RoutedEventArgs e)
        {
            LODLIMIT = 4;
            Panel_ME1LODLimit.Visibility = Visibility.Collapsed;
        }

        private void Button_InstallerLOD2k_Click(object sender, RoutedEventArgs e)
        {
            LODLIMIT = 2;
            Panel_ME1LODLimit.Visibility = Visibility.Collapsed;
        }

        private void Button_ME12K_Click(object sender, RoutedEventArgs e)
        {
            Log.Information("Using 2K textures for ME1 (button click)");
            Panel_SettingsME1LOD.Visibility = Visibility.Collapsed;
            Button_ME1_ShowLODOptions.Content = "Using 2K Textures";
            string exe = BINARY_DIRECTORY + MEM_EXE_NAME;
            string args = "-apply-lods-gfx 1 -limit2k";
            Utilities.runProcess(exe, args, true);
        }

        private void Button_ME14K_Click(object sender, RoutedEventArgs e)
        {
            Panel_SettingsME1LOD.Visibility = Visibility.Collapsed;
            Button_ME1_ShowLODOptions.Content = "Using 4K Textures";
            Log.Information("Using 4K textures for ME1 (button click)");
            string exe = BINARY_DIRECTORY + MEM_EXE_NAME;
            string args = "-apply-lods-gfx 1";
            Utilities.runProcess(exe, args, true);
        }

        private void Button_ToggleME1LODPanel(object sender, RoutedEventArgs e)
        {
            if (Panel_SettingsME1LOD.Visibility == Visibility.Visible)
            {
                Panel_SettingsME1LOD.Visibility = Visibility.Collapsed;
            }
            else
            {
                Panel_SettingsME1LOD.Visibility = Visibility.Visible;
            }
        }

        private void InstallingOverlayoutFlyout_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (this.WindowState == System.Windows.WindowState.Normal)
            {
                this.WindowState = System.Windows.WindowState.Maximized;
            }
            else
            {
                this.WindowState = System.Windows.WindowState.Normal;
            }
        }

        private void ShowFirstTime(object sender, RoutedEventArgs e)
        {
            FirstRunFlyout.IsOpen = true;
        }

        private void Button_FirstTimeRun_Click(object sender, RoutedEventArgs e)
        {
            FirstRunFlyout.IsOpen = false;
        }
    }
}