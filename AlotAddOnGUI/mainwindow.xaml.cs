using AlotAddOnGUI.classes;
using AlotAddOnGUI.ui;
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
using System.IO.Compression;
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
using System.Windows.Media.Animation;
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
        private System.Windows.Controls.CheckBox[] buildOptionCheckboxes;
        public ConsoleApp BACKGROUND_MEM_PROCESS = null;
        public bool BACKGROUND_MEM_RUNNING = false;
        ProgressDialogController updateprogresscontroller;
        public const string UPDATE_OPERATION_LABEL = "UPDATE_OPERATION_LABEL";
        public const string UPDATE_PROGRESSBAR_INDETERMINATE = "SET_PROGRESSBAR_DETERMINACY";
        public const string INCREMENT_COMPLETION_EXTRACTION = "INCREMENT_COMPLETION_EXTRACTION";
        public const string SHOW_DIALOG = "SHOW_DIALOG";
        public const string ERROR_OCCURED = "ERROR_OCCURED";
        public static string EXE_DIRECTORY = System.AppDomain.CurrentDomain.BaseDirectory;
        public static string BINARY_DIRECTORY = EXE_DIRECTORY + "Data\\bin\\";
        private bool errorOccured = false;
        private bool UsingBundledManifest = false;
        private List<string> BlockingMods;
        private AddonFile meuitmFile;
        private DispatcherTimer backgroundticker;
        private DispatcherTimer tipticker;
        private int completed = 0;
        //private int addonstoinstall = 0;
        private int CURRENT_GAME_BUILD = 0; //set when extraction is run/finished
        private int ADDONSTOBUILD_COUNT = 0;
        private bool PreventFileRefresh = false;
        public static readonly string REGISTRY_KEY = @"SOFTWARE\ALOTAddon";
        public static readonly string ME3_BACKUP_REGISTRY_KEY = @"SOFTWARE\Mass Effect 3 Mod Manager";

        private BackgroundWorker BuildWorker = new BackgroundWorker();
        private BackgroundWorker BackupWorker = new BackgroundWorker();
        private BackgroundWorker InstallWorker = new BackgroundWorker();
        private BackgroundWorker ImportWorker = new BackgroundWorker();
        public event PropertyChangedEventHandler PropertyChanged;
        List<string> PendingUserFiles = new List<string>();
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChangedEventHandler handler = PropertyChanged;

            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        public const string MEM_EXE_NAME = "MassEffectModderNoGui.exe";

        private BindingList<AddonFile> addonfiles;
        NotifyIcon nIcon = new NotifyIcon();
        private const string MEM_OUTPUT_DIR = "Data\\MEM_Packages";
        private const string MEM_OUTPUT_DISPLAY_DIR = "Data\\MEM_Packages";

        private const string ADDON_STAGING_DIR = "ADDON_STAGING";
        private const string USER_STAGING_DIR = "USER_STAGING";

        private string ADDON_FULL_STAGING_DIRECTORY = System.AppDomain.CurrentDomain.BaseDirectory + "Data\\" + ADDON_STAGING_DIR + "\\";
        private string USER_FULL_STAGING_DIRECTORY = System.AppDomain.CurrentDomain.BaseDirectory + "Data\\" + USER_STAGING_DIR + "\\";

        private bool me1Installed;
        private bool me2Installed;
        private bool me3Installed;
        private bool RefreshListOnUserImportClose = false;
        private List<string> musicpackmirrors;
        private BindingList<AddonFile> alladdonfiles;
        private readonly string PRIMARY_HEADER = "Download the listed files for your game as listed below. You can filter per-game in the settings.\nDo not extract or rename any files you download. Drop them onto this interface to import them.";
        private const string SETTINGSTR_IMPORTASMOVE = "ImportAsMove";
        public const string SETTINGSTR_BETAMODE = "BetaMode";
        public const string SETTINGSTR_DOWNLOADSFOLDER = "DownloadsFolder";
        private List<string> BACKGROUND_MEM_PROCESS_ERRORS;
        private List<string> BACKGROUND_MEM_PROCESS_PARSED_ERRORS;
        private const string SHOW_DIALOG_YES_NO = "SHOW_DIALOG_YES_NO";
        private bool CONTINUE_BACKUP_EVEN_IF_VERIFY_FAILS = false;
        private bool ERROR_SHOWING = false;
        private int PREBUILT_MEM_INDEX; //will increment to 10 when run
        private bool SHOULD_HAVE_OUTPUT_FILE;
        public bool USING_BETA { get; private set; }
        public bool SOUND_SETTING { get; private set; }
        public StringBuilder BACKGROUND_MEM_STDOUT { get; private set; }
        public int BACKUP_THREAD_GAME { get; private set; }
        private bool _showME1Files = true;
        private bool _showME2Files = true;
        private bool _showME3Files = true;
        private bool Loading = true;
        private int LODLIMIT = 0;
        private FrameworkElement[] fadeInItems;
        private List<FrameworkElement> currentFadeInItems = new List<FrameworkElement>();
        private bool ShowReadyFilesOnly = false;
        internal AddonDownloadAssistant DOWNLOAD_ASSISTANT_WINDOW;
        public string DOWNLOADS_FOLDER;
        private int RefreshesUntilRealRefresh;
        private bool ShowBuildingOnly;
        private WebClient downloadClient;
        private string MANIFEST_LOC = EXE_DIRECTORY + @"Data\\manifest.xml";
        private string MANIFEST_BUNDLED_LOC = EXE_DIRECTORY + @"Data\\manifest-bundled.xml";

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
            Log.Information("Checking for application updates from gitub");
            AddonFilesLabel.Text = "Checking for application updates";
            var versInfo = System.Reflection.Assembly.GetEntryAssembly().GetName().Version;
            var client = new GitHubClient(new ProductHeaderValue("ALOTAddonGUI"));
            try
            {
                var releases = await client.Repository.Release.GetAll("Mgamerz", "ALOTAddonGUI");
                if (releases.Count > 0)
                {
                    Log.Information("Fetched application releases from github");

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
                        Log.Information("Latest available: " + latest.TagName);

                        Version releaseName = new Version(latest.TagName);
                        if (versInfo < releaseName && latest.Assets.Count > 0)
                        {
                            Log.Information("Latest release is applicable to us.");
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
                                Log.Information("Downloading update for application");

                                //there's an update
                                updateprogresscontroller = await this.ShowProgressAsync("Installing Update", "ALOT Installer is updating. Please wait...", true);
                                updateprogresscontroller.SetIndeterminate();
                                WebClient downloadClient = new WebClient();

                                downloadClient.Headers["Accept"] = "application/vnd.github.v3+json";
                                downloadClient.Headers["user-agent"] = "ALOTAddonGUI";
                                string temppath = Path.GetTempPath();
                                int downloadProgress = 0;
                                downloadClient.DownloadProgressChanged += (s, e) =>
                                {
                                    if (downloadProgress != e.ProgressPercentage)
                                    {
                                        Log.Information("Program update download percent: " + e.ProgressPercentage);
                                    }
                                    downloadProgress = e.ProgressPercentage;
                                    updateprogresscontroller.SetProgress((double)e.ProgressPercentage / 100);
                                };
                                updateprogresscontroller.Canceled += async (s, e) =>
                                {
                                    if (downloadClient != null)
                                    {
                                        Log.Information("Application update was in progress but was canceled.");
                                        downloadClient.CancelAsync();
                                        await updateprogresscontroller.CloseAsync();
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
                        RunMusicDownloadCheck();
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error("Error checking for MEM GUI update: " + e.Message);
                ShowStatus("Error checking for MEM update");
            }
        }

        private void RunMusicDownloadCheck()
        {
            if (musicpackmirrors.Count() == 0)
            {
                return;
            }
            string me1ogg = GetMusicDirectory() + "me1.ogg";
            string me2ogg = GetMusicDirectory() + "me2.ogg";
            string me3ogg = GetMusicDirectory() + "me3.ogg";

            if (!File.Exists(me1ogg) || !File.Exists(me2ogg) || !File.Exists(me3ogg))
            {
                WebClient downloadClient = new WebClient();

                downloadClient.Headers["user-agent"] = "ALOTInstaller";
                string temppath = Path.GetTempPath();
                downloadClient.DownloadFileCompleted += UnzipMusicUpdate;
                string downloadPath = temppath + "ALOTInstallerMusicPack.7z";
                string mirror = musicpackmirrors[0];
                Log.Information("Downloading music pack from " + mirror);
                try
                {
                    downloadClient.DownloadFileAsync(new Uri(mirror), downloadPath, downloadPath);
                }
                catch (Exception e)
                {
                    Log.Error("Exception downloading music file: " + e.ToString());
                }
            }
        }

        private void UnzipMusicUpdate(object sender, AsyncCompletedEventArgs e)
        {
            if (e.Error == null)
            {
                //Extract 7z
                string path = BINARY_DIRECTORY + "7z.exe";

                string args = "x \"" + (string)e.UserState + "\" -aoa -r -o\"" + GetMusicDirectory() + "\"";
                Log.Information("Extracting Music Pack...");
                Utilities.runProcess(path, args);
                Log.Information("Extraction complete.");

                File.Delete((string)e.UserState);
                ShowStatus("Downloaded music pack", 2000);
            }
            else
            {
                Log.Error("Error occured: " + e.Error.ToString());
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

            Label_MEMVersion.Content = "MEM Cmd Version: " + fileVersion;
            try
            {
                Log.Information("Checking for updates to MEMNOGUI. The local version is " + fileVersion);

                var client = new GitHubClient(new ProductHeaderValue("ALOTAddonGUI"));
                var releases = await client.Repository.Release.GetAll("MassEffectModder", "MassEffectModderNoGui");
                Log.Information("Fetched MEMNOGui releases from github...");
                if (releases.Count > 0)
                {
                    //The release we want to check is always the latest, so [0]
                    Release latest = releases[0];
                    int releaseNameInt = Convert.ToInt32(latest.TagName);
                    if (fileVersion < releaseNameInt && latest.Assets.Count > 0)
                    {
                        Log.Information("MEMNOGUI update available: " + releaseNameInt);

                        //there's an update
                        updateprogresscontroller = await this.ShowProgressAsync("Installing Update", "Mass Effect Modder (Cmd Version) is updating (to v" + releaseNameInt + "). Please wait...", true);
                        updateprogresscontroller.SetIndeterminate();
                        updateprogresscontroller.Canceled += MEMNoGuiUpdateCanceled;
                        downloadClient = new WebClient();

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
                        Log.Information("No updates for MEM NO Gui are available");
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error("Error checking for MEMNOGUI update: " + e.Message);
                ShowStatus("Error checking for MEM (NOGUI) update");
            }
        }

        private void MEMNoGuiUpdateCanceled(object sender, EventArgs e)
        {
            Log.Warning("MEM NO GUI Update has been canceled.");
            if (downloadClient != null && downloadClient.IsBusy)
            {
                downloadClient.CancelAsync();
            }
            if (updateprogresscontroller != null && updateprogresscontroller.IsOpen)
            {
                updateprogresscontroller.CloseAsync();
            }
        }

        private void UnzipSelfUpdate(object sender, AsyncCompletedEventArgs e)
        {
            KeyValuePair<ProgressDialogController, string> kp = (KeyValuePair<ProgressDialogController, string>)e.UserState;
            if (e.Cancelled)
            {
                Log.Warning("SelfUpdate was canceled, deleting partial file...");

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
                kp.Key.SetTitle("Extracting ALOT Installer update");
                string path = BINARY_DIRECTORY + "7z.exe";
                string args = "x \"" + kp.Value + "\" -aoa -r -o\"" + EXE_DIRECTORY + "Update\"";
                Log.Information("Extracting update...");
                Utilities.runProcess(path, args);

                File.Delete((string)kp.Value);
                kp.Key.CloseAsync();

                Log.Information("Update Extracted - rebooting to update mode");
                string exe = EXE_DIRECTORY + "Update\\" + System.AppDomain.CurrentDomain.FriendlyName;
                string currentDirNoSlash = EXE_DIRECTORY.Substring(0, EXE_DIRECTORY.Length - 1);
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
            if (e.Cancelled)
            {
                return; //handled by cancel
            }
            KeyValuePair<ProgressDialogController, string> kp = (KeyValuePair<ProgressDialogController, string>)e.UserState;
            kp.Key.SetIndeterminate();
            kp.Key.SetTitle("Extracting MassEffectModderNoGUI Update");
            //Extract 7z
            string path = BINARY_DIRECTORY + "7z.exe";
            string args = "x \"" + kp.Value + "\" -aoa -r -o\"" + BINARY_DIRECTORY + "\"";

            Log.Information("Extracting MassEffectModderNoGUI update...");
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
            string pathWithoutTrailingSlash = BINARY_DIRECTORY.Substring(0, BINARY_DIRECTORY.Length - 1);
            string args = "x \"" + e.UserState + "\" -aoa -r -o\"" + BINARY_DIRECTORY + "\"";
            Log.Information("Extracting MEMGUI update...");
            Utilities.runProcess(path, args);
            Log.Information("Extraction complete.");

            File.Delete((string)e.UserState);
            var versInfo = FileVersionInfo.GetVersionInfo(BINARY_DIRECTORY + "MassEffectModder.exe");
            int fileVersion = versInfo.FileMajorPart;
            Button_MEM_GUI.Content = "LAUNCH MEM v" + fileVersion;
            ShowStatus("Updated Mass Effect Modder (GUI version) to v" + fileVersion, 3000);
            RunMusicDownloadCheck();
        }

        private async void BuildCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            ShowReadyFilesOnly = false;
            TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.NoProgress, this);
            Log.Information("BuildCompleted()");
            int result = (int)e.Result;
            PreventFileRefresh = false;
            SetBottomButtonAvailability();
            Button_Settings.IsEnabled = true;
            Button_DownloadAssistant.IsEnabled = true;


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
                    PreventFileRefresh = false;
                    break;
                case -1:
                default:
                    Log.Error("BuildCompleted() got -1 (or default catch all) result.");
                    HeaderLabel.Text = "An error occured building the Addon.\nView the log (Settings -> Diagnostics -> View Log) for more information.";
                    AddonFilesLabel.Text = "Addon not successfully built";
                    RefreshesUntilRealRefresh = 4;
                    break;
                case 1:
                case 2:
                case 3:
                    if (errorOccured)
                    {
                        HeaderLabel.Text = "Addon built with errors.\nThe Addon was built but some files did not process correctly and were skipped.\nThe MEM packages for the addon have been placed into the " + MEM_OUTPUT_DISPLAY_DIR + " directory.";
                        AddonFilesLabel.Text = "MEM Packages placed in the " + MEM_OUTPUT_DISPLAY_DIR + " folder";
                        await this.ShowMessageAsync("ALOT Addon for Mass Effect" + getGameNumberSuffix(result) + " was built, but had errors", "Some files had errors occured during the build process. These files were skipped. Your game may look strange in some parts if you use the built Addon. You should report this to the developers on Discord.");
                    }
                    else
                    {

                        //flash
                        var helper = new FlashWindowHelper(System.Windows.Application.Current);
                        // Flashes the window and taskbar 5 times and stays solid 
                        // colored until user focuses the main window
                        helper.FlashApplicationWindow();

                        //Is Alot Installed?
                        ALOTVersionInfo currentAlotInfo = GetCurrentALOTInfo(CURRENT_GAME_BUILD);
                        bool readyToInstallALOT = false;
                        foreach (AddonFile af in ADDONFILES_TO_BUILD)
                        {
                            if (af.ALOTVersion > 0)
                            {
                                readyToInstallALOT = true;
                            }
                        }
                        long fullsize = Utilities.DirSize(new DirectoryInfo(getOutputDir(CURRENT_GAME_BUILD)));
                        ulong freeBytes, diskSize, totalFreeBytes;
                        Utilities.GetDiskFreeSpaceEx(Utilities.GetGamePath(CURRENT_GAME_BUILD), out freeBytes, out diskSize, out totalFreeBytes);
                        Log.Information("We will need around " + ByteSize.FromBytes(fullsize) + " to install this texture set. The free space is " + ByteSize.FromBytes(freeBytes));

                        if (freeBytes < (ulong)fullsize)
                        {
                            //not enough disk space for build
                            HeaderLabel.Text = "Not enough free space to install textures for Mass Effect" + getGameNumberSuffix(CURRENT_GAME_BUILD) + ".";
                            AddonFilesLabel.Text = "MEM Packages placed in the " + MEM_OUTPUT_DISPLAY_DIR + " folder";
                            await this.ShowMessageAsync("Not enough free space for install", "There is not enough disk space on " + Path.GetPathRoot(Utilities.GetGamePath(CURRENT_GAME_BUILD)) + " to install. You will need " + ByteSize.FromBytes(fullsize) + " of free space to install.");
                            errorOccured = false;
                            break;
                        }

                        if (readyToInstallALOT || currentAlotInfo != null) //not installed
                        {
                            HeaderLabel.Text = "Ready to install new textures";
                            AddonFilesLabel.Text = "MEM Packages placed in the " + MEM_OUTPUT_DISPLAY_DIR + " folder";
                            MetroDialogSettings mds = new MetroDialogSettings();
                            mds.AffirmativeButtonText = "Install Now";

                            mds.NegativeButtonText = "Install Later";
                            mds.DefaultButtonFocus = MessageDialogResult.Affirmative;
                            var buildResult = await this.ShowMessageAsync("Ready to install textures", "You can install these textures now, or you can manually install them with MEM. The files have been placed into the Data\\MEM_Packages subdirectory.", MessageDialogStyle.AffirmativeAndNegative, mds);
                            if (buildResult == MessageDialogResult.Affirmative)
                            {
                                bool run = true;
                                while (Utilities.isGameRunning(CURRENT_GAME_BUILD))
                                {
                                    run = false;
                                    await this.ShowMessageAsync("Mass Effect" + getGameNumberSuffix(CURRENT_GAME_BUILD) + " is running", "Please close Mass Effect" + getGameNumberSuffix(CURRENT_GAME_BUILD) + " to continue.");
                                    if (!Utilities.isGameRunning(CURRENT_GAME_BUILD))
                                    {
                                        run = true;
                                        break;
                                    }
                                }
                                if (run)
                                {
                                    InstallALOT(result, ADDONFILES_TO_BUILD);
                                }
                            }
                            else
                            {
                                HeaderLabel.Text = PRIMARY_HEADER;
                            }
                        }
                        else
                        {
                            await this.ShowMessageAsync("Addon(s) have been built", "Your textures have been built into MEM files, ready for installation. Due to ALOT not being installed, you will have to install these manually. The files have been placed into the MEM_Packages subdirectory.");
                        }
                    }
                    errorOccured = false;
                    break;
            }
            foreach (AddonFile af in ADDONFILES_TO_BUILD)
            {
                af.ReadyStatusText = null;
                af.Building = false;
                af.SetIdle();
            }
            ShowBuildingOnly = false;
            BUILD_ALOT = false;
            BUILD_ADDON_FILES = false;
            BUILD_USER_FILES = false;
            ApplyFiltering();
            CURRENT_GAME_BUILD = 0; //reset
        }

        private ALOTVersionInfo GetCurrentALOTInfo(int game)
        {
            switch (game)
            {
                case 1:
                    return CURRENTLY_INSTALLED_ME1_ALOT_INFO;
                case 2:
                    return CURRENTLY_INSTALLED_ME2_ALOT_INFO;
                case 3:
                    return CURRENTLY_INSTALLED_ME3_ALOT_INFO;
                default:
                    return null; // could be bad.
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
            if (RefreshesUntilRealRefresh > 0)
            {
                RefreshesUntilRealRefresh--;
                return;
            }
            // code to execute periodically
            if (addonfiles != null)
            {
                //Console.WriteLine("Checking for files existence...");
                string basepath = DOWNLOADED_MODS_DIRECTORY + "\\";
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
                    if (af.UserFile)
                    {
                        ready = File.Exists(af.UserFilePath);
                        af.Staged = false;
                    }
                    else if (!ready && af.UnpackedSingleFilename != null)
                    {
                        //Check for single file
                        ready = File.Exists(basepath + af.UnpackedSingleFilename);
                        af.Staged = false;
                    }

                    if (!ready && af.UnpackedSingleFilename != null && af.ALOTVersion > 0)
                    {
                        int game = 0;
                        if (af.Game_ME1)
                        {
                            game = 1;
                        }
                        else if (af.Game_ME2)
                        {
                            game = 2;
                        }
                        else if (af.Game_ME3)
                        {
                            game = 3;
                        }
                        //Check for staged file
                        ready = File.Exists(getOutputDir(game) + "000_" + af.UnpackedSingleFilename);
                        if (ready)
                        {
                            af.Staged = true;
                        }
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
                    else
                    {
                        af.Staged = false;
                    }
                    numdone += ready ? 1 : 0;
                    System.Windows.Application.Current.Dispatcher.Invoke(
                    () =>
                    {
                        // Code to run on the GUI thread.
                        Build_ProgressBar.Value = (int)(((double)numdone / addonfiles.Count) * 100);
                        string tickerText = "";
                        tickerText += ShowME1Files ? "ME1: " + numME1FilesReady + "/" + numME1Files + " imported" : "ME1: N/A";
                        tickerText += " - ";
                        tickerText += ShowME2Files ? "ME2: " + numME2FilesReady + "/" + numME2Files + " imported" : "ME2: N/A";
                        tickerText += " - ";
                        tickerText += ShowME3Files ? "ME3: " + numME3FilesReady + "/" + numME3Files + " imported" : "ME3: N/A";
                        AddonFilesLabel.Text = tickerText;
                    });
                }

            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            fadeInItems = new FrameworkElement[] { FirstRun_MainContent, FirstRunText_TitleBeta, FirstRunText_BetaSummary };
            buildOptionCheckboxes = new System.Windows.Controls.CheckBox[] { Checkbox_BuildOptionALOT, Checkbox_BuildOptionUser, Checkbox_BuildOptionAddon };
            if (EXE_DIRECTORY.Length > 95)
            {
                Log.Fatal("ALOT Installer is nested too deep for Addon to build properly (" + EXE_DIRECTORY.Length + " chars) due to Windows API limitations.");
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
                    BuildWorker.ProgressChanged += BuildWorker_ProgressChanged;
                    BuildWorker.RunWorkerCompleted += BuildCompleted;
                    BuildWorker.WorkerReportsProgress = true;
                }

                Button_DownloadAssistant.IsEnabled = true;

                SetBottomButtonAvailability();
            }
            else
            {
                Log.Error("No trilogy games are installed. Can't build an addon. Shutting down...");
                await this.ShowMessageAsync("None of the Mass Effect Trilogy games are installed", "ALOT Installer requires at least one of the trilogy games to be installed before you can use it.");
                Environment.Exit(1);
            }
        }

        private void SetBottomButtonAvailability()
        {
            string me1Path = Utilities.GetGamePath(1);
            string me2Path = Utilities.GetGamePath(2);
            string me3Path = Utilities.GetGamePath(3);

            //int installedGames = 5;
            me1Installed = (me1Path != null);
            me2Installed = (me2Path != null);
            me3Installed = (me3Path != null);

            if (!me1Installed)
            {
                Log.Information("ME1 not installed - disabling ME1 install");
                Button_InstallME1.IsEnabled = false;
                Button_InstallME1.ToolTip = "Mass Effect is not installed. To install textures for ME1 the game must already be installed";
                Button_InstallME1.Content = "ME1 Not Installed";
            }
            else
            {
                Button_InstallME1.IsEnabled = true;
                Button_InstallME1.ToolTip = "Click to install build and install textures for Mass Effect";
                Button_InstallME1.Content = "Install for ME1";
            }

            if (!me2Installed)
            {
                Log.Information("ME2 not installed - disabling ME2 install");
                Button_InstallME2.IsEnabled = false;
                Button_InstallME2.ToolTip = "Mass Effect 2 is not installed. To install textures for ME2 the game must already be installed";
                Button_InstallME2.Content = "ME2 Not Installed";
            }
            else
            {
                Button_InstallME2.IsEnabled = true;
                Button_InstallME2.ToolTip = "Click to install build and install textures for Mass Effect 2";
                Button_InstallME2.Content = "Install for ME2";
            }

            if (!me3Installed)
            {
                Log.Information("ME3 not installed - disabling ME3 install");
                Button_InstallME3.IsEnabled = false;
                Button_InstallME3.ToolTip = "Mass Effect 3 is not installed. To install texturesn for ME3 the game must already be installed";
                Button_InstallME3.Content = "ME3 Not Installed";
            }
            else
            {
                Button_InstallME3.IsEnabled = true;
                Button_InstallME3.ToolTip = "Click to install build and install textures for Mass Effect 3";
                Button_InstallME3.Content = "Install for ME3";
            }
        }

        private bool ValidateGameBackup(int game)
        {
            switch (game)
            {
                case 1:
                    {
                        string me1path = Utilities.GetGamePath(1, true);
                        string path = Utilities.GetGameBackupPath(1);
                        if (path != null)
                        {
                            Button_ME1Backup.Content = "Restore ME1";
                            Button_ME1Backup.ToolTip = "Click to restore game from " + Environment.NewLine + path;
                        }
                        else
                        {
                            if (Directory.Exists(me1path))
                            {
                                Button_ME1Backup.Content = "Backup ME1";
                                Button_ME1Backup.ToolTip = "Click to backup game";
                            }
                            else
                            {
                                Button_ME1Backup.Content = "ME1 NOT INSTALLED";
                                Button_ME1Backup.IsEnabled = false;
                            }
                        }
                        Button_ME1Backup.ToolTip += Environment.NewLine + "Game is installed at " + Environment.NewLine + Utilities.GetGamePath(1, true);
                        return path != null;
                    }
                case 2:
                    {
                        string path = Utilities.GetGameBackupPath(2);
                        string me2path = Utilities.GetGamePath(2, true);

                        if (path != null)
                        {
                            Button_ME2Backup.Content = "Restore ME2";
                            Button_ME2Backup.ToolTip = "Click to restore game from " + Environment.NewLine + path;
                        }
                        else
                        {
                            if (Directory.Exists(me2path))
                            {
                                Button_ME2Backup.Content = "Backup ME2";
                                Button_ME2Backup.ToolTip = "Click to backup game";
                            }
                            else
                            {
                                Button_ME2Backup.Content = "ME2 NOT INSTALLED";
                                Button_ME2Backup.IsEnabled = false;
                            }

                        }
                        Button_ME2Backup.ToolTip += Environment.NewLine + "Game is installed at " + Environment.NewLine + Utilities.GetGamePath(2, true);
                        return path != null;
                    }
                case 3:
                    {
                        string me3path = Utilities.GetGamePath(3, true);

                        string path = Utilities.GetGameBackupPath(3);
                        if (path != null)
                        {
                            Button_ME3Backup.Content = "Restore ME3";
                            Button_ME3Backup.ToolTip = "Click to restore game from " + Environment.NewLine + path;
                        }
                        else
                        {
                            if (Directory.Exists(me3path))
                            {
                                Button_ME3Backup.Content = "Backup ME3";
                                Button_ME3Backup.ToolTip = "Click to backup game";
                            }
                            else
                            {
                                Button_ME3Backup.Content = "ME3 NOT INSTALLED";
                                Button_ME3Backup.IsEnabled = false;
                            }
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
                    if (!File.Exists("DEV_MODE"))
                    {
                        try
                        {
                            //File.Copy(@"C:\Users\mgame\Downloads\Manifest.xml", MANIFEST_LOC);
                            string url = "https://raw.githubusercontent.com/Mgamerz/AlotAddOnGUI/master/manifest.xml";
                            if (USING_BETA)
                            {
                                Log.Information("In BETA mode.");
                                url = "https://raw.githubusercontent.com/Mgamerz/AlotAddOnGUI/master/manifest-beta.xml";
                                Title += " BETA MODE";
                            }
                            webClient.DownloadStringCompleted += async (sender, e) =>
                            {
                                if (e.Error == null)
                                {
                                    string pageSourceCode = e.Result;
                                    if (Utilities.TestXMLIsValid(pageSourceCode))
                                    {
                                        Log.Information("Manifest fetched.");
                                        File.WriteAllText(MANIFEST_LOC, pageSourceCode);
                                        //Legacy stuff
                                        if (File.Exists(EXE_DIRECTORY + @"manifest-new.xml"))
                                        {
                                            File.Delete(MANIFEST_LOC);
                                        }
                                        ManifestDownloaded();
                                    }
                                    else
                                    {
                                        Log.Error("Response from server was not valid XML! " + pageSourceCode);
                                        if (!File.Exists(MANIFEST_LOC) && File.Exists(MANIFEST_BUNDLED_LOC))
                                        {
                                            Log.Information("Reading bundled manifest instead.");
                                            File.Delete(MANIFEST_LOC);
                                            File.Copy(MANIFEST_BUNDLED_LOC, MANIFEST_LOC);
                                            UsingBundledManifest = true;
                                            ManifestDownloaded();
                                        }
                                        else
                                        {
                                            Log.Error("Local manifest also doesn't exist! No manifest is available.");
                                            await this.ShowMessageAsync("No Manifest Available", "An error occured downloading the manifest for ALOT Installer. There is no local bundled version available. Information that is required to build and install ALOT is not available. Check the program logs.");
                                            Environment.Exit(1);
                                        }
                                    }
                                }
                                else
                                {
                                    Log.Error("Exception occured getting manifest from server: " + e.Error.ToString());
                                    if (!File.Exists(MANIFEST_LOC) && File.Exists(MANIFEST_BUNDLED_LOC))
                                    {
                                        Log.Information("Reading bundled manifest instead.");
                                        File.Delete(MANIFEST_LOC);
                                        File.Copy(MANIFEST_BUNDLED_LOC, MANIFEST_LOC);
                                        UsingBundledManifest = true;
                                        ManifestDownloaded();
                                    }
                                    else
                                    {
                                        Log.Fatal("No local manifest exists to use, exiting...");
                                        await this.ShowMessageAsync("No Manifest Available", "An error occured downloading the manifest for ALOT Installer. There is no local bundled version available. Information that is required to build and install ALOT is not available. Check the program logs.");
                                        Environment.Exit(1);
                                    }
                                }
                                //do something with results 
                            };
                            Debug.WriteLine(DateTime.Now);
                            webClient.DownloadStringAsync(new Uri(url));
                        }
                        catch (WebException e)
                        {
                            Log.Error("WebException occured getting manifest from server: " + e.ToString());
                            if (!File.Exists(MANIFEST_LOC) && File.Exists(MANIFEST_BUNDLED_LOC))
                            {
                                Log.Information("Reading bundled manifest instead.");
                                File.Delete(MANIFEST_LOC);
                                File.Copy(MANIFEST_BUNDLED_LOC, MANIFEST_LOC);
                                UsingBundledManifest = true;
                                ManifestDownloaded();
                            }
                        }
                        //}
                        //catch (Exception e)
                        //{
                        //    Debug.WriteLine(DateTime.Now);
                        //    Log.Error("Other Exception occured getting manifest from server/reading manifest: " + e.ToString());
                        //    if (!File.Exists(MANIFEST_LOC) && File.Exists(MANIFEST_BUNDLED_LOC))
                        //    {
                        //        Log.Information("Reading bundled manifest instead.");
                        //        File.Delete(MANIFEST_LOC);
                        //        File.Copy(MANIFEST_BUNDLED_LOC, MANIFEST_LOC);
                        //        UsingBundledManifest = true;
                        //    }
                        //}
                    }
                    else
                    {
                        Log.Information("DEV_MODE file found. Not using online manifest.");
                        UsingBundledManifest = true;
                        Title += " DEV MODE";
                        ManifestDownloaded();
                    }

                    //if (!File.Exists(MANIFEST_LOC))
                    //{
                    //    Log.Fatal("No local manifest exists to use, exiting...");
                    //    await this.ShowMessageAsync("No Manifest Available", "An error occured downloading the manifest for addon. Information that is required to build the addon is not available. Check the program logs.");
                    //    Environment.Exit(1);
                    //}

                }
            }
        }

        private void ManifestDownloaded()
        {
            Button_Settings.IsEnabled = true;
            readManifest();

            Log.Information("readManifest() has completed. Switching over to user control");
            bool? CheckOutputDirectories = Utilities.GetRegistrySettingBool("CheckOutputDirectoriesOnManifestLoad");
            //if (CheckOutputDirectories != null && CheckOutputDirectories.Value)
            //{
            CheckOutputDirectoriesForUnpackedSingleFiles();
            //}

            Loading = false;
            Build_ProgressBar.IsIndeterminate = false;
            HeaderLabel.Text = PRIMARY_HEADER;
            AddonFilesLabel.Text = "Scanning...";
            CheckImportLibrary_Tick(null, null);
            RunMEMUpdater2();
            UpdateALOTStatus();
            RunMEMUpdaterGUI();
            //beta only for now.
            bool? hasShownFirstRun = Utilities.GetRegistrySettingBool("HasRunFirstRun");
            if (hasShownFirstRun == null || !(bool)hasShownFirstRun)
            {
                playFirstTimeAnimation();
            }
            else
            {
                PerformRAMCheck();
            }
        }

        private void CheckOutputDirectoriesForUnpackedSingleFiles(int game = 0)
        {
            bool ReImportedFiles = false;
            foreach (AddonFile af in alladdonfiles)
            {
                if (af.Ready && !af.Staged)
                {
                    continue;
                }

                //File is not ready. Might be missing single file...
                if (af.UnpackedSingleFilename != null)
                {
                    int i = 0;
                    if (af.Game_ME1) i = 1;
                    if (af.Game_ME2) i = 2;
                    if (af.Game_ME3) i = 3;
                    string outputPath = getOutputDir(i);

                    string importedFilePath = DOWNLOADED_MODS_DIRECTORY + "\\" + af.UnpackedSingleFilename;
                    string outputFilename = outputPath + "000_" + af.UnpackedSingleFilename; //This only will work for ALOT right now. May expand if it becomes more useful.
                    if (File.Exists(outputFilename) && (game == 0 || game == i))
                    {

                        Log.Information("Re-importing extracted single file: " + outputFilename);
                        try
                        {
                            File.Move(outputFilename, importedFilePath);
                            ReImportedFiles = true;
                            af.Staged = false;
                            af.ReadyStatusText = null;
                        }
                        catch (Exception e)
                        {
                            Log.Error("Failed to reimport file! " + e.Message);
                        }
                    }
                }
            }
            Utilities.WriteRegistryKey(Registry.CurrentUser, REGISTRY_KEY, "CheckOutputDirectoriesOnManifestLoad", false);

            if (ReImportedFiles)
            {
                ShowStatus("Re-imported files due to shutdown during build or install");
            }
        }

        private async void PerformRAMCheck()
        {
            long ramAmountKb = Utilities.GetInstalledRamAmount();
            long installedRamGB = ramAmountKb / 1048576L;
            if (installedRamGB < 7.98)
            {
                await this.ShowMessageAsync("System memory is less than 8 GB", "Building and installing textures uses considerable amounts of memory. Installation will be significantly slower on systems with less than 8 GB for Mass Effect 3, or 6 GB for Mass Effect and Mass Effect 2.");
            }
            Debug.WriteLine("Ram Amount, KB: " + ramAmountKb);
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
                    bool meuitminstalled = CURRENTLY_INSTALLED_ME1_ALOT_INFO.MEUITMVER > 0;
                    me1ver = CURRENTLY_INSTALLED_ME1_ALOT_INFO.ALOTVER + "." + CURRENTLY_INSTALLED_ME1_ALOT_INFO.ALOTUPDATEVER + (meuitminstalled ? ", MEUITM" : "");
                }
                else
                {
                    if (CURRENTLY_INSTALLED_ME1_ALOT_INFO.MEUITMVER > 0)
                    {
                        me1ver = "Not Installed, MEUITM Installed";

                    }
                    else
                    {
                        me1ver = "Installed, unable to detect version";
                    }
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

            Button_ME1_ShowLODOptions.Visibility = (CURRENTLY_INSTALLED_ME1_ALOT_INFO != null && CURRENTLY_INSTALLED_ME1_ALOT_INFO.ALOTVER > 0) ? Visibility.Visible : Visibility.Collapsed;

            foreach (AddonFile af in alladdonfiles)
            {
                af.ReadyStatusText = null; //update description
            }
        }

        private void playFirstTimeAnimation()
        {
            foreach (FrameworkElement tb in fadeInItems)
            {
                tb.Opacity = 0;
            }
            Button_FirstRun_Dismiss.Opacity = 0;
            FirstRunFlyout.IsOpen = true;
            currentFadeInItems = fadeInItems.ToList();
            #region Fade in
            // Create a storyboard to contain the animations.
            Storyboard storyboard = new Storyboard();
            TimeSpan duration = new TimeSpan(0, 0, 2);

            // Create a DoubleAnimation to fade the not selected option control
            DoubleAnimation animation = new DoubleAnimation();

            animation.From = 0.0;
            animation.To = 1.0;
            animation.BeginTime = new TimeSpan(0, 0, 2);
            animation.Duration = new Duration(duration);
            animation.Completed += new EventHandler(ItemFadeInComplete_Chain);

            FrameworkElement item = currentFadeInItems[0];
            currentFadeInItems.RemoveAt(0);
            // Configure the animation to target de property Opacity
            Storyboard.SetTargetName(animation, item.Name);
            Storyboard.SetTargetProperty(animation, new PropertyPath(OpacityProperty));
            // Add the animation to the storyboard
            storyboard.Children.Add(animation);

            // Begin the storyboard
            storyboard.Begin(this);

            #endregion
        }

        private void ItemFadeInComplete_Chain(object sender, EventArgs e)
        {
            Storyboard storyboard = new Storyboard();
            TimeSpan duration = new TimeSpan(0, 0, 0, 0, 700);

            // Create a DoubleAnimation to fade the not selected option control
            DoubleAnimation animation = new DoubleAnimation();

            animation.From = 0.0;
            animation.To = 1.0;
            animation.Duration = new Duration(duration);

            System.Windows.FrameworkElement item;
            if (currentFadeInItems.Count > 0)
            {
                item = currentFadeInItems[0];
                animation.Completed += new EventHandler(ItemFadeInComplete_Chain);
                currentFadeInItems.RemoveAt(0);
            }
            else
            {
                item = Button_FirstRun_Dismiss;
            }

            // Configure the animation to target de property Opacity
            Storyboard.SetTargetName(animation, item.Name);
            Storyboard.SetTargetProperty(animation, new PropertyPath(OpacityProperty));
            // Add the animation to the storyboard
            storyboard.Children.Add(animation);

            // Begin the storyboard
            storyboard.Begin(this);
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
            musicpackmirrors = new List<string>();

            alladdonfiles = new BindingList<AddonFile>(); //prevents crashes
            try
            {
                XElement rootElement = XElement.Load(MANIFEST_LOC);
                string version = (string)rootElement.Attribute("version") ?? "";
                musicpackmirrors = rootElement.Elements("musicpackmirror").Select(xe => xe.Value).ToList();
                linqlist = (from e in rootElement.Elements("addonfile")
                            select new AddonFile
                            {
                                Showing = false,
                                FileSize = e.Element("file").Attribute("size") != null ? Convert.ToInt64((string)e.Element("file").Attribute("size")) : 0L,
                                MEUITM = e.Attribute("meuitm") != null ? (bool)e.Attribute("meuitm") : false,
                                ProcessAsModFile = e.Attribute("processasmodfile") != null ? (bool)e.Attribute("processasmodfile") : false,
                                Author = (string)e.Attribute("author"),
                                FriendlyName = (string)e.Attribute("friendlyname"),
                                Optional = e.Attribute("optional") != null ? (bool)e.Attribute("optional") : false,
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
                                FileMD5 = (string)e.Element("file").Attribute("md5"),
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
                MessageDialogResult result = await this.ShowMessageAsync("Error reading file manifest", "An error occured while reading the manifest file for installation. This may indicate a network failure or a packaging failure by Mgamerz - Please submit an issue to github (http://github.com/mgamerz/alotaddongui/issues) and include the most recent log file from the logs directory.\n\n" + e.Message, MessageDialogStyle.Affirmative);
                AddonFilesLabel.Text = "Error parsing manifest XML! Check the logs.";
                return;
            }
            linqlist = linqlist.OrderBy(o => o.Author).ThenBy(x => x.FriendlyName).ToList();



            alladdonfiles = new BindingList<AddonFile>(linqlist);
            addonfiles = alladdonfiles;
            //get list of installed games
            SetupButtons();
            int meuitmindex = -1;
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
                    if (af.MEUITM)
                    {
                        meuitmindex = alladdonfiles.IndexOf(af);
                        meuitmFile = af;
                    }
                }
            }
            UpdateALOTStatus();
            if (meuitmindex >= 0)
            {
                alladdonfiles.RemoveAt(meuitmindex);
            }

            ApplyFiltering(); //sets data source and separators            
        }

        private void ApplyFiltering()
        {
            BindingList<AddonFile> newList = new BindingList<AddonFile>();
            if (CURRENTLY_INSTALLED_ME1_ALOT_INFO == null || CURRENTLY_INSTALLED_ME1_ALOT_INFO.MEUITMVER == 0)
            {
                //show MEUITM
                if (meuitmFile != null && alladdonfiles.IndexOf(meuitmFile) < 0)
                {
                    alladdonfiles.Add(meuitmFile);
                    alladdonfiles = new BindingList<AddonFile>(alladdonfiles.OrderBy(o => o.Author).ThenBy(x => x.FriendlyName).ToList());
                }
            }
            foreach (AddonFile af in alladdonfiles)
            {
                if (ShowBuildingOnly)
                {
                    if (af.Building)
                    {
                        newList.Add(af);
                    }
                }
                else
                {
                    if (!af.Ready && ShowReadyFilesOnly)
                    { continue; }
                    bool shouldDisplay = ((af.Game_ME1 && ShowME1Files) || (af.Game_ME2 && ShowME2Files) || (af.Game_ME3 && ShowME3Files));
                    if (shouldDisplay)
                    {
                        newList.Add(af);
                    }
                }
            }
            addonfiles = newList;
            lvUsers.ItemsSource = addonfiles;
            CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(lvUsers.ItemsSource);
            PropertyGroupDescription groupDescription = new PropertyGroupDescription("Author");
            view.GroupDescriptions.Add(groupDescription);
            CheckImportLibrary_Tick(null, null);

            if (DOWNLOAD_ASSISTANT_WINDOW != null)
            {
                List<AddonFile> notReadyAddonFiles = new List<AddonFile>();
                foreach (AddonFile af in addonfiles)
                {
                    if (!af.Ready && !af.UserFile)
                    {
                        notReadyAddonFiles.Add(af);
                    }
                }
                DOWNLOAD_ASSISTANT_WINDOW.setNewMissingAddonfiles(notReadyAddonFiles);
            }
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
                ShowBuildOptions(2);
            }
        }

        private void ShowBuildOptions(int game)
        {
            CURRENT_GAME_BUILD = game;
            Loading = true; //preven 1;
            ShowME1Files = game == 1;
            ShowME2Files = game == 2;
            ShowME3Files = game == 3;
            Loading = false;
            ShowReadyFilesOnly = true;
            ApplyFiltering();
            ALOTVersionInfo installedInfo = Utilities.GetInstalledALOTInfo(game);
            bool alotInstalled = installedInfo != null; //default value
            bool alotavailalbleforinstall = false;
            foreach (AddonFile af in alladdonfiles)
            {
                if (af.ALOTVersion > 0 && af.Ready)
                {
                    //i forgot to put code here... hmm.
                }
            }
            int installedALOTUpdateVersion = (installedInfo == null) ? 0 : installedInfo.ALOTUPDATEVER;
            if (installedInfo == null || installedInfo.ALOTVER == 0) //not installed or mem installed
            {
                Checkbox_BuildOptionAddon.IsChecked = true;
                Checkbox_BuildOptionAddon.IsEnabled = false;
            }
            else
            {
                Checkbox_BuildOptionAddon.IsChecked = false;
                Checkbox_BuildOptionAddon.IsEnabled = true;
            }

            //installedInfo = null -> MEUITM, ALOT not installed
            //installedInfo = X ver = 0, meuitmver = 0 -> ALOT Installed via MEM Installer
            //installedInfo = X ver > 0, mueitmver = 0 -> ALOT installed with ALOT installer
            //installedInfo = X, ver = 0, meuitmver > 0 -> MEUITM installed via MEM Installer, no alot (or maybe old one.)
            //installedInfo = X, ver > 0, meuitmver > 0-> MEUITM installed, alot  installed via alot installer
            //

            bool hasApplicableUserFile = false;
            bool checkAlotBox = false;
            int installingALOTver = 0;

            bool blockALOTInstallDueToMainVersionDiff = false;
            foreach (AddonFile af in addonfiles)
            {
                if ((af.Game_ME1 && game == 1) || (af.Game_ME2 && game == 2) || (af.Game_ME3 && game == 3))
                {
                    if (af.UserFile && af.Ready)
                    {
                        hasApplicableUserFile = true;
                    }
                    if (installedInfo != null && installedInfo.ALOTVER != 0 && af.ALOTVersion > installedInfo.ALOTVER)
                    {
                        //alot installed same version
                        Log.Information("ALOT main version " + af.ALOTVersion + " blocked from installing because it is different main version than the currently installed one.");
                        blockALOTInstallDueToMainVersionDiff = true;
                        installingALOTver = af.ALOTVersion;
                        continue;
                    }
                    if (af.ALOTVersion > 0)
                    {
                        alotavailalbleforinstall = true;
                        if (!alotInstalled)
                        {
                            checkAlotBox = true;
                        }
                    }
                }
            }

            Checkbox_BuildOptionALOT.IsChecked = checkAlotBox;
            Checkbox_BuildOptionALOT.IsEnabled = !checkAlotBox && alotavailalbleforinstall;

            Checkbox_BuildOptionUser.IsChecked = hasApplicableUserFile;
            Checkbox_BuildOptionUser.IsEnabled = hasApplicableUserFile;

            bool hasOneOption = false;
            foreach (System.Windows.Controls.CheckBox cb in buildOptionCheckboxes)
            {
                if (cb.IsEnabled)
                {
                    hasOneOption = true;
                    break;
                }
            }

            if (hasOneOption)
            {
                Label_WhatToBuildAndInstall.Text = "Choose what to install for Mass Effect" + getGameNumberSuffix(CURRENT_GAME_BUILD) + ".";
                if (blockALOTInstallDueToMainVersionDiff)
                {
                    Label_WhatToBuildAndInstall.Text = "Imported ALOT file (" + installingALOTver + ".0) cannot be installed over the current installation (" + installedInfo.ALOTVER + "." + installedInfo.ALOTUPDATEVER + ")." + System.Environment.NewLine + Label_WhatToBuildAndInstall.Text;
                }
                else if (alotInstalled && installedInfo.ALOTVER > 0)
                {
                    Label_WhatToBuildAndInstall.Text = "ALOT is already installed. " + Label_WhatToBuildAndInstall.Text;
                }
                WhatToBuildFlyout.IsOpen = true;
                Button_BuildAndInstall.IsEnabled = true;
            }
            else
            {
                //Run button 
                Button_BuildAndInstall_Click(null, null);
            }
        }

        private async void Button_ME1Backup_Click(object sender, RoutedEventArgs e)
        {
            if (BACKUP_THREAD_GAME > 0)
            {
                return;
            }
            if (ValidateGameBackup(1))
            {
                if (Utilities.isGameRunning(1))
                {
                    await this.ShowMessageAsync("Mass Effect" + getGameNumberSuffix(1) + " is running", "Please close Mass Effect" + getGameNumberSuffix(1) + " before attempting restore.");
                    return;
                }

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
            if (BACKUP_THREAD_GAME > 0)
            {
                return;
            }
            if (ValidateGameBackup(2))
            {
                if (Utilities.isGameRunning(2))
                {
                    await this.ShowMessageAsync("Mass Effect" + getGameNumberSuffix(2) + " is running", "Please close Mass Effect" + getGameNumberSuffix(2) + " before attempting restore.");
                    return;
                }

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
            if (BACKUP_THREAD_GAME > 0)
            {
                return;
            }
            if (ValidateGameBackup(3))
            {

                if (Utilities.isGameRunning(3))
                {
                    await this.ShowMessageAsync("Mass Effect" + getGameNumberSuffix(3) + " is running", "Please close Mass Effect" + getGameNumberSuffix(3) + " before attempting restore.");
                    return;
                }
                //Game is backed up
                MetroDialogSettings settings = new MetroDialogSettings();
                settings.NegativeButtonText = "Cancel";
                settings.AffirmativeButtonText = "Restore";
                MessageDialogResult result = await this.ShowMessageAsync("Restoring game from backup", "Restoring your game will wipe out all mods and put your game back to an unmodified state. Are you sure you want to do this?", MessageDialogStyle.AffirmativeAndNegative, settings);
                if (result == MessageDialogResult.Affirmative)
                {
                    //RESTORE
                    RestoreGame(3);
                }
            }
            else
            {
                //Backup-Precheck
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
            Button_InstallME1.IsEnabled = Button_InstallME2.IsEnabled = Button_InstallME3.IsEnabled = Button_Settings.IsEnabled = Button_DownloadAssistant.IsEnabled = false;
            ShowStatus("Verifying game data before backup", 6000);
            // get all the directories in selected dirctory
        }

        private void BackupCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.NoProgress, this);
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
            Button_DownloadAssistant.IsEnabled = true;
            SetBottomButtonAvailability();
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
                ShowBuildOptions(3);
            }
        }

        private async Task<bool> InstallPrecheck(int game)
        {
            CheckOutputDirectoriesForUnpackedSingleFiles();
            CheckImportLibrary_Tick(null, null); //get all ready files
            Loading = true; //prevent 1;
            ShowME1Files = game == 1;
            ShowME2Files = game == 2;
            ShowME3Files = game == 3;
            Loading = false;
            ShowReadyFilesOnly = false;
            ApplyFiltering();
            //Check game has been run at least once
            string configFile = IniSettingsHandler.GetConfigIniPath(game);
            if (game == 1 && !File.Exists(configFile))
            {
                //game has not been run yet.
                Log.Error("Config file missing for Mass Effect " + game + ". Blocking install");
                await this.ShowMessageAsync("Mass Effect" + getGameNumberSuffix(game) + " has not been run yet", "Mass Effect" + getGameNumberSuffix(game) + " must be run at least once in order for the game to generate default configuration files for this installer to edit. Start the game, and exit at the main menu to generate them.");
                // return false;
            }

            int nummissing = 0;
            bool oneisready = false;
            ALOTVersionInfo installedInfo = Utilities.GetInstalledALOTInfo(game);
            if (installedInfo == null)
            {
                //Check for backup
                string backupPath = Utilities.GetGameBackupPath(game);
                if (backupPath == null)
                {
                    //No backup
                    MetroDialogSettings mds = new MetroDialogSettings();
                    mds.AffirmativeButtonText = "Backup";
                    mds.NegativeButtonText = "Continue";
                    mds.DefaultButtonFocus = MessageDialogResult.Affirmative;
                    MessageDialogResult result = await this.ShowMessageAsync("Mass Effect" + getGameNumberSuffix(game) + " not backed up", "You should create a backup of your game before installing ALOT. In the event something goes wrong, you can quickly restore back to an unmodified state. Backups only work if you game is unmodified. Create a backup before install?", MessageDialogStyle.AffirmativeAndNegative, mds);
                    if (result == MessageDialogResult.Affirmative)
                    {
                        BackupGame(game);
                        return false;
                    }
                }
            }
            bool blockDueToMissingALOTFile = installedInfo == null; //default value
            int installedALOTUpdateVersion = (installedInfo == null) ? 0 : installedInfo.ALOTUPDATEVER;
            bool blockDueToMissingALOTUpdateFile = false; //default value
            string blockDueToBadImportedFile = null; //default vaule
            bool manifestHasALOTMainFile = false;
            bool manifestHasUpdateAvailable = false;
            foreach (AddonFile af in alladdonfiles)
            {
                if ((af.Game_ME1 && game == 1) || (af.Game_ME2 && game == 2) || (af.Game_ME3 && game == 3))
                {
                    if (af.ALOTVersion > 0)
                    {
                        manifestHasALOTMainFile = true;
                    }
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
                        manifestHasUpdateAvailable = true;
                        if (!af.Ready)
                        {
                            blockDueToMissingALOTUpdateFile = true;
                            Log.Warning("Installation for ME" + game + " being blocked due to ALOT Update available that is not ready");
                            break;
                        }
                    }

                    if (!af.Ready && !af.Optional)
                    {
                        nummissing++;
                    }
                    else
                    {
                        if (af.Ready)
                        {
                            FileInfo fi = new FileInfo(af.GetFile());
                            if (!af.IsCurrentlySingleFile() && af.FileSize > 0 && af.FileSize != fi.Length)
                            {
                                Log.Error(af.GetFile() + " has wrong size: " + fi.Length + ", manifest specifies " + af.FileSize);
                                blockDueToBadImportedFile = af.GetFile();
                                break;
                            }
                            oneisready = true;
                        }
                    }
                }
            }

            if (blockDueToMissingALOTFile && manifestHasALOTMainFile)
            {
                await this.ShowMessageAsync("ALOT main file is missing", "ALOT's main file for Mass Effect" + getGameNumberSuffix(game) + " is not imported. This file must be imported to run the installer when ALOT is not installed.");
                return false;
            }

            if (blockDueToMissingALOTUpdateFile && manifestHasUpdateAvailable)
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

            if (blockDueToBadImportedFile != null)
            {
                await this.ShowMessageAsync("Corrupt/Bad file detected", "The file " + blockDueToBadImportedFile + " is not the correct size. This file may be corrupt or the wrong version, or was renamed in an attempt to make the program accept this file. Remove this file from Download_Mods.");
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
            //if alot is already installed we don't need to show missing message, unless installed via MEM directly
            if (installedInfo == null || installedInfo.ALOTVER == 0)
            {
                MessageDialogResult result = await this.ShowMessageAsync(nummissing + " file" + (nummissing != 1 ? "s are" : " is") + " missing", "Some files for the Mass Effect" + getGameNumberSuffix(game) + " Addon are missing - do you want to build the addon without these files?", MessageDialogStyle.AffirmativeAndNegative);
                return result == MessageDialogResult.Affirmative;
            }
            else
            {
                return true;
            }
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

        private async Task<bool> InitBuild(int game)
        {
            Log.Information("InitBuild() started.");

            AddonFilesLabel.Text = "Preparing to build texture packages...";
            CheckOutputDirectoriesForUnpackedSingleFiles(game);
            Build_ProgressBar.IsIndeterminate = true;
            Log.Information("Deleting any pre-existing extraction and staging directories.");
            string destinationpath = EXTRACTED_MODS_DIRECTORY;
            try
            {
                if (Directory.Exists(destinationpath))
                {
                    Utilities.DeleteFilesAndFoldersRecursively(destinationpath);
                }

                if (Directory.Exists(ADDON_FULL_STAGING_DIRECTORY))
                {
                    Utilities.DeleteFilesAndFoldersRecursively(ADDON_FULL_STAGING_DIRECTORY);
                }
                if (Directory.Exists(USER_FULL_STAGING_DIRECTORY))
                {
                    Utilities.DeleteFilesAndFoldersRecursively(USER_FULL_STAGING_DIRECTORY);
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
            Button_DownloadAssistant.IsEnabled = false;
            Button_Settings.IsEnabled = false;

            Directory.CreateDirectory(ADDON_FULL_STAGING_DIRECTORY);
            Directory.CreateDirectory(USER_FULL_STAGING_DIRECTORY);

            HeaderLabel.Text = "Preparing to build ALOT Addon for Mass Effect " + game + ".\nDon't close this window until the process completes.";
            // Install_ProgressBar.IsIndeterminate = true;
            Utilities.WriteRegistryKey(Registry.CurrentUser, REGISTRY_KEY, "CheckOutputDirectoriesOnManifestLoad", true);
            return true;
        }

        private void File_Drop_BackgroundThread(object sender, System.Windows.DragEventArgs e)
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

                PerformImportOperation(files);
            }
        }

        private void PerformImportOperation(string[] files, bool acceptUserFiles = true)
        {
            if (files.Count() > 0)
            {
                //don't know how you can drop less than 1 files but whatever
                //This code is for failsafe in case somehow library file exists but is not detect properly, like user moved file but something is running
                string file = files[0];
                string basepath = DOWNLOADED_MODS_DIRECTORY + "\\";

                if (file.ToLower().StartsWith(basepath))
                {
                    ShowStatus("Can't import files from Downloaded_Mods", 5000);
                    return;
                }
            }
            Log.Information("Files queued for import checks:");
            foreach (String file in files)
            {
                Log.Information(" - " + file);
            }
            List<Tuple<AddonFile, string, string>> filesToImport = new List<Tuple<AddonFile, string, string>>();
            // Assuming you have one file that you care about, pass it off to whatever
            // handling code you have defined.
            List<string> noMatchFiles = new List<string>();
            long totalBytes = 0;
            List<string> acceptableUserFiles = new List<string>();
            List<string> alreadyImportedFiles = new List<string>();
            List<string> badSizeFiles = new List<string>();

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
                foreach (AddonFile af in alladdonfiles)
                {
                    if (af.UserFile)
                    {
                        if (af.UserFilePath == file && af.Ready)
                        {
                            alreadyImportedFiles.Add(file);
                            hasMatch = true;
                            break;
                        }
                        continue; //don't check these
                    }
                    bool isUnpackedSingleFile = af.UnpackedSingleFilename != null && af.UnpackedSingleFilename.Equals(fname, StringComparison.InvariantCultureIgnoreCase) && File.Exists(file); //make sure not folder with same name.

                    if (isUnpackedSingleFile || af.Filename.Equals(fname, StringComparison.InvariantCultureIgnoreCase) && File.Exists(file)) //make sure folder not with same name
                    {
                        hasMatch = true;
                        if (af.Ready == false)
                        {
                            //Check size as validation
                            if (!isUnpackedSingleFile && af.FileSize > 0)
                            {
                                FileInfo fi = new FileInfo(file);
                                if (fi.Length != af.FileSize)
                                {
                                    Log.Error("File to import has the wrong size: " + file + ", it should have size " + af.FileSize + ", but file to import is size " + fi.Length);
                                    badSizeFiles.Add(file);
                                    hasMatch = true;
                                    continue;
                                }
                            }


                            //Copy file to directory
                            string basepath = DOWNLOADED_MODS_DIRECTORY + "\\";
                            string destination = basepath + ((isUnpackedSingleFile) ? af.UnpackedSingleFilename : af.Filename);
                            //Log.Information("Copying dragged file to downloaded mods directory: " + file);
                            //File.Copy(file, destination, true);
                            filesToImport.Add(Tuple.Create(af, file, destination));
                            totalBytes += new System.IO.FileInfo(file).Length;
                            //filesimported.Add(af);
                            //timer_Tick(null, null);
                            break;
                        }
                    }
                }
                if (!hasMatch)
                {
                    string extension = Path.GetExtension(file).ToLower();
                    switch (extension)
                    {
                        case ".7z":
                        case ".rar":
                        case ".zip":
                        case ".tpf":
                        case ".mem":
                        case ".mod":
                            if (acceptUserFiles)
                            {
                                acceptableUserFiles.Add(file);
                            }
                            break;
                        case ".dds":
                        case ".png":
                        case ".jpg":
                        case ".jpeg":
                        case ".tga":
                        case ".bmp":
                            string filename = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                            if (!filename.Contains("0x"))
                            {
                                Log.Error("Texture filename not valid: " + Path.GetFileName(file) + " Texture filename must include texture CRC (0xhhhhhhhh)");
                                continue;
                            }
                            int idx = filename.IndexOf("0x");
                            if (filename.Length - idx < 10)
                            {
                                Log.Error("Texture filename not valid: " + Path.GetFileName(file) + " Texture filename must include texture CRC (0xhhhhhhhh)");
                                continue;
                            }
                            uint crc;
                            string crcStr = filename.Substring(idx + 2, 8);
                            try
                            {
                                crc = uint.Parse(crcStr, System.Globalization.NumberStyles.HexNumber);
                            }
                            catch
                            {
                                Log.Error("Texture filename not valid: " + Path.GetFileName(file) + " Texture filename must include texture CRC (0xhhhhhhhh)");
                                continue;
                            }
                            //File has hash
                            acceptableUserFiles.Add(file);
                            break;
                        default:
                            Log.Information("Dragged file does not match any addon manifest file and is not acceptable extension: " + file);
                            noMatchFiles.Add(file);
                            break;
                    }
                }
            } //END LOOP
            if (filesToImport.Count == 0 && acceptableUserFiles.Count > 0)
            {
                PendingUserFiles = acceptableUserFiles;
                LoadUserFileSelection(PendingUserFiles[0]);
                UserTextures_Flyout.IsOpen = true;
            }

            string statusMessage = "";
            statusMessage += "Already imported: " + alreadyImportedFiles.Count;
            if (noMatchFiles.Count > 0)
            {
                statusMessage += " | ";
                statusMessage += "Not supported: " + noMatchFiles.Count;
            }

            if (badSizeFiles.Count > 0)
            {
                statusMessage += " | ";
                statusMessage += "Corrupt/Bad files: " + badSizeFiles.Count;
            }
            if (noMatchFiles.Count > 0 || alreadyImportedFiles.Count > 0 || badSizeFiles.Count > 0)
            {
                ShowStatus(statusMessage);
            }

            //if (noMatchFiles.Count == 0 && filesToImport.Count == 0 && acceptableUserFiles.Count == 0)
            //{
            //    ShowStatus("All dropped files are already imported");
            //}

            if (filesToImport.Count > 0)
            {
                ImportFiles(filesToImport, new List<string>(), null, 0, totalBytes);
            }
        }

        private async void ImportFiles(List<Tuple<AddonFile, string, string>> filesToImport, List<string> importedFiles, ProgressDialogController progressController, long processedBytes, long totalBytes)
        {
            PreventFileRefresh = true;
            string importingfrom = Path.GetPathRoot(filesToImport[0].Item2);
            string importingto = Path.GetPathRoot(EXE_DIRECTORY);
            if ((bool)Checkbox_MoveFilesAsImport.IsChecked && importingfrom == importingto)
            {
                if (DOWNLOAD_ASSISTANT_WINDOW != null)
                {
                    DOWNLOAD_ASSISTANT_WINDOW.ShowStatus("Importing...");
                }
                ImportWorker = new BackgroundWorker();
                ImportWorker.DoWork += ImportFilesAsMove;
                ImportWorker.RunWorkerCompleted += ImportCompleted;
                ImportWorker.RunWorkerAsync(filesToImport);

            }
            else
            {
                Tuple<AddonFile, string, string> fileToImport = filesToImport[0];
                filesToImport.RemoveAt(0);
                //COPY
                if (progressController == null)
                {
                    MetroDialogSettings settings = new MetroDialogSettings();
                    progressController = await this.ShowProgressAsync("Importing files", "ALOT Installer is importing files, please wait...\nImporting " + fileToImport.Item1.FriendlyName, false, settings);
                    progressController.SetIndeterminate();
                    progressController.SetCancelable(true);
                }
                else
                {
                    progressController.SetMessage("ALOT Installer is importing files, please wait...\nImporting " + fileToImport.Item1.FriendlyName);
                    if (DOWNLOAD_ASSISTANT_WINDOW != null)
                    {
                        DOWNLOAD_ASSISTANT_WINDOW.ShowStatus("Importing " + importedFiles.Count + " file" + (importedFiles.Count == 1 ? "s" : ""));
                    }
                }
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
                        string originalMessage = importedFiles.Count + " file" + (importedFiles.Count != 1 ? "s have" : " has") + " been copied into the Downloaded_Mods directory.";

                        ShowImportFinishedMessage(originalTitle, originalMessage, detailsMessage);
                        if (DOWNLOAD_ASSISTANT_WINDOW != null)
                        {
                            DOWNLOAD_ASSISTANT_WINDOW.ShowStatus(importedFiles.Count + " file" + (importedFiles.Count != 1 ? "s were" : " was") + " imported");
                        }
                    }
                };
                downloadClient.DownloadFileAsync(new Uri(fileToImport.Item2), fileToImport.Item3);
            }
        }

        private void ImportFilesAsMove(object sender, DoWorkEventArgs e)
        {
            List<Tuple<AddonFile, string, string>> filesToImport = (List<Tuple<AddonFile, string, string>>)e.Argument;
            List<string> completedItems = new List<string>();
            while (filesToImport.Count > 0)
            {
                Tuple<AddonFile, string, string> fileToImport = filesToImport[0];
                filesToImport.RemoveAt(0);
                Log.Information("Importing via move: " + fileToImport.Item2);
                if (File.Exists(fileToImport.Item3))
                {
                    File.Delete(fileToImport.Item3);
                }
                File.Move(fileToImport.Item2, fileToImport.Item3);
                Log.Information("Imported via move: " + fileToImport.Item2);
                completedItems.Add(fileToImport.Item1.FriendlyName);
            }
            e.Result = completedItems;
        }

        private void ImportCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e != null)
            {
                if (e.Error != null)
                {
                    //An error has occured

                }
                else
                if (e.Result != null)
                {
                    List<string> importedFiles = (List<string>)e.Result;
                    //imports finished
                    string detailsMessage = "The following files were just imported to ALOT Installer. The files have been moved to the Downloaded_Mods folder.";
                    foreach (string af in importedFiles)
                    {
                        detailsMessage += "\n - " + af;
                    }

                    if (DOWNLOAD_ASSISTANT_WINDOW != null)
                    {
                        DOWNLOAD_ASSISTANT_WINDOW.ShowStatus(importedFiles.Count + " file" + (importedFiles.Count != 1 ? "s were" : " was") + " imported");
                    }
                    PreventFileRefresh = false; //allow refresh

                    string originalTitle = importedFiles.Count + " file" + (importedFiles.Count != 1 ? "s" : "") + " imported";
                    string originalMessage = importedFiles.Count + " file" + (importedFiles.Count != 1 ? "s have" : " has") + " been moved into the Downloaded_Mods directory.";
                    CheckImportLibrary_Tick(null, null);
                    ShowImportFinishedMessage(originalTitle, originalMessage, detailsMessage);
                }
                PreventFileRefresh = false;
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
                await this.ShowMessageAsync("Running from write-protected directory", "Your user account doesn't have write permissions to the current directory. Move ALOT Installer to somewhere where yours does, like the Documents folder.");
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
                ShowBuildOptions(1);
            }
        }

        private void Button_ViewLog_Click(object sender, RoutedEventArgs e)
        {
            var directory = new DirectoryInfo("logs");
            FileInfo latestlogfile = directory.GetFiles("alotinstaller*.txt").OrderByDescending(f => f.LastWriteTime).First();
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
            DOWNLOADS_FOLDER = Utilities.GetRegistrySettingString(SETTINGSTR_DOWNLOADSFOLDER);
            if (DOWNLOADS_FOLDER == null)
            {
                DOWNLOADS_FOLDER = KnownFolders.GetPath(KnownFolder.Downloads);
            }
            Checkbox_BetaMode.IsChecked = USING_BETA;
            Checkbox_MoveFilesAsImport.IsChecked = importasmove;

            if (USING_BETA)
            {
                ThemeManager.ChangeAppStyle(System.Windows.Application.Current,
                                                    ThemeManager.GetAccent("Crimson"),
                                                    ThemeManager.GetAppTheme("BaseDark")); // or appStyle.Item1
            }
            Button_ChangeDownloadFolder.ToolTip = "Changes folder where download assistant will import from.\nThis should be your browser's download folder.\nThe current directory is\n" + DOWNLOADS_FOLDER;

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
            Button_DownloadAssistant.IsEnabled = false;
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
            if (e.Result != null)
            {
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
                SetBottomButtonAvailability();
                UpdateALOTStatus();

                foreach (AddonFile af in alladdonfiles)
                {
                    if (af.ALOTVersion > 0 || af.ALOTUpdateVersion > 0)
                    {
                        af.ReadyStatusText = null; //fire property reset
                        af.SetIdle();
                    }
                }
            }
            else
            {
                AddonFilesLabel.Text = "Restore failed! Check the logs.";
                SetBottomButtonAvailability();
                UpdateALOTStatus();
            }

            Button_Settings.IsEnabled = true;
            Button_DownloadAssistant.IsEnabled = true;
            PreventFileRefresh = false;

            BACKUP_THREAD_GAME = -1;
            HeaderLabel.Text = PRIMARY_HEADER;

            if (CURRENTLY_INSTALLED_ME1_ALOT_INFO != null && CURRENTLY_INSTALLED_ME1_ALOT_INFO.MEUITMVER == 0)
            {
                if (meuitmFile != null)
                {
                    int index = alladdonfiles.IndexOf(meuitmFile);
                    if (index < 0)
                    {
                        //add back in
                        alladdonfiles.Add(meuitmFile);
                        alladdonfiles = new BindingList<AddonFile>(alladdonfiles.OrderBy(o => o.Author).ThenBy(x => x.FriendlyName).ToList());
                    }
                }
            }

            ApplyFiltering();
        }

        private async void Checkbox_BetaMode_Click(object sender, RoutedEventArgs e)
        {
            bool isEnabling = (bool)Checkbox_BetaMode.IsChecked;
            bool restart = true;
            if (isEnabling)
            {
                MessageDialogResult result = await this.ShowMessageAsync("Enabling BETA mode", "Enabling BETA mode will enable the beta manifest as well as beta features and beta updates. These builds are for testing, and may not be stable (and will sometimes outright crash). Unless you're OK with this you should stay in normal mode.\nEnable BETA mode?", MessageDialogStyle.AffirmativeAndNegative);
                if (result == MessageDialogResult.Negative)
                {
                    Checkbox_BetaMode.IsChecked = false;
                    restart = false;
                }
            }
            Utilities.WriteRegistryKey(Registry.CurrentUser, REGISTRY_KEY, SETTINGSTR_BETAMODE, ((bool)Checkbox_BetaMode.IsChecked ? 1 : 0));
            USING_BETA = (bool)Checkbox_BetaMode.IsChecked;
            if (restart)
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
            bool isClosing = true;
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
                else
                {
                    isClosing = false;
                }
            }

            if (isClosing)
            {
                if (DOWNLOAD_ASSISTANT_WINDOW != null)
                {
                    DOWNLOAD_ASSISTANT_WINDOW.SHUTTING_DOWN = true;
                    DOWNLOAD_ASSISTANT_WINDOW.Close();
                }

                if (BuildWorker.IsBusy || InstallWorker.IsBusy)
                {
                    //We should add indicator that we closed while busy
                    Utilities.WriteRegistryKey(Registry.CurrentUser, REGISTRY_KEY, "CheckOutputDirectoriesOnManifestLoad", true);
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

        private void Button_FirstTimeRunDismiss_Click(object sender, RoutedEventArgs e)
        {
            Utilities.WriteRegistryKey(Registry.CurrentUser, REGISTRY_KEY, "HasRunFirstRun", true);
            FirstRunFlyout.IsOpen = false;
        }

        private void Button_ManualFileME1_Click(object sender, RoutedEventArgs e)
        {
            AddUserFileAndQueue(1);
        }

        private void AddUserFileAndQueue(int game)
        {
            string file = PendingUserFiles[0];
            AddonFile af = new AddonFile();
            switch (game)
            {
                case 1:
                    af.Game_ME1 = true;
                    break;
                case 2:
                    af.Game_ME2 = true;
                    break;
                case 3:
                    af.Game_ME3 = true;
                    break;
            }
            af.UserFile = true;
            af.DownloadLink = "http://example.com";
            af.Author = "User Supplied Files (ME" + game + ")";
            af.FriendlyName = Path.GetFileNameWithoutExtension(file);
            af.Filename = Path.GetFileName(file);

            af.UserFilePath = file;
            alladdonfiles.Add(af);
            PendingUserFiles.RemoveAt(0);
            RefreshListOnUserImportClose = true;
            if (PendingUserFiles.Count <= 0)
            {
                UserTextures_Flyout.IsOpen = false;
                Button_ManualFileME1.IsEnabled = Button_ManualFileME2.IsEnabled = Button_ManualFileME3.IsEnabled = false;
            }
            else
            {
                LoadUserFileSelection(PendingUserFiles[0]);
            }
        }

        private void LoadUserFileSelection(string v)
        {
            UserTextures_Title.Text = "Select which game " + Path.GetFileName(v) + " applies to";
        }

        private void UserTextures_Flyout_IsOpenChanged(object sender, RoutedEventArgs e)
        {
            if (UserTextures_Flyout.IsOpen == false)
            {
                if (RefreshListOnUserImportClose)
                {
                    ApplyFiltering();
                    RefreshListOnUserImportClose = false;
                    PendingUserFiles.Clear();
                }
            }
            else
            {
                Button_ManualFileME1.IsEnabled = Button_ManualFileME2.IsEnabled = Button_ManualFileME3.IsEnabled = true;
            }
        }

        private void Button_ManualFileME3_Click(object sender, RoutedEventArgs e)
        {
            AddUserFileAndQueue(3);

        }

        private void Button_ManualFileME2_Click(object sender, RoutedEventArgs e)
        {
            AddUserFileAndQueue(2);
        }

        public void ImportFromDownloadsFolder()
        {
            if (Directory.Exists(DOWNLOADS_FOLDER))
            {
                Log.Information("Looking for files to import from: " + DOWNLOADS_FOLDER);
                List<string> filelist = new List<string>();
                List<AddonFile> addonFilesNotReady = new List<AddonFile>();
                foreach (AddonFile af in alladdonfiles)
                {
                    if (!af.Ready)
                    {
                        addonFilesNotReady.Add(af);
                    }
                }
                Log.Information("Number of files not ready: " + addonFilesNotReady.Count);
                string[] files = Directory.GetFiles(DOWNLOADS_FOLDER);
                foreach (string file in files)
                {
                    string fname = Path.GetFileName(file); //we do not check duplicates with (1) etc
                    foreach (AddonFile af in addonFilesNotReady)
                    {
                        if (fname == af.Filename)
                        {
                            filelist.Add(file);
                            break;
                        }
                    }
                }
                SettingsFlyout.IsOpen = false;
                if (filelist.Count > 0)
                {
                    Log.Information("Found this many files to import from downloads folder:" + filelist.Count);

                    PerformImportOperation(filelist.ToArray(), false);
                }
                else
                {
                    if (DOWNLOAD_ASSISTANT_WINDOW != null)
                    {
                        DOWNLOAD_ASSISTANT_WINDOW.ShowStatus("No files found for importing");
                    }
                    Log.Information("Did not find any files for importing in: " + DOWNLOADS_FOLDER);
                    ShowStatus("No files found for importing in " + DOWNLOADS_FOLDER);
                }
            }
            else
            {
                Log.Information("Downloads folder does not exist: " + DOWNLOADS_FOLDER);
            }
        }

        private async void Button_BuildAndInstall_Click(object sender, RoutedEventArgs e)
        {
            bool oneOptionChecked = false;
            foreach (System.Windows.Controls.CheckBox cb in buildOptionCheckboxes)
            {
                if ((bool)cb.IsChecked)
                {
                    oneOptionChecked = true;
                    break;
                }
            }
            Button_BuildAndInstall.IsEnabled = false;
            WhatToBuildFlyout.IsOpen = false;
            if (oneOptionChecked)
            {
                if (CURRENT_GAME_BUILD > 0 && CURRENT_GAME_BUILD < 4)
                {
                    if (await InitBuild(CURRENT_GAME_BUILD))
                    {

                        Loading = true; //prevent refresh when filtering
                        ShowME1Files = CURRENT_GAME_BUILD == 1;
                        ShowME2Files = CURRENT_GAME_BUILD == 2;
                        ShowME3Files = CURRENT_GAME_BUILD == 3;
                        Loading = false;

                        switch (CURRENT_GAME_BUILD)
                        {
                            case 1:
                                Button_InstallME1.Content = "Building...";
                                break;
                            case 2:
                                Button_InstallME2.Content = "Building...";
                                break;
                            case 3:
                                Button_InstallME3.Content = "Building...";
                                break;
                        }
                        BUILD_ALOT = Checkbox_BuildOptionALOT.IsChecked.Value;
                        BUILD_ADDON_FILES = Checkbox_BuildOptionAddon.IsChecked.Value;
                        BUILD_USER_FILES = Checkbox_BuildOptionUser.IsChecked.Value;

                        TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.Normal, this);
                        BuildWorker.RunWorkerAsync(CURRENT_GAME_BUILD);
                    }
                    else
                    {
                        ShowReadyFilesOnly = false;
                        ApplyFiltering();
                        CURRENT_GAME_BUILD = 0;
                        Log.Warning("Install was aborted due to initinstall returning false");
                    }
                }
                else
                {
                    ShowReadyFilesOnly = false;
                    ApplyFiltering();
                    CURRENT_GAME_BUILD = 0;
                }
            }
        }

        private void Button_BuildAndInstallCancel_Click(object sender, RoutedEventArgs e)
        {
            ShowReadyFilesOnly = false;
            ApplyFiltering();
            WhatToBuildFlyout.IsOpen = false;
            CURRENT_GAME_BUILD = 0;
        }

        private void Expander_Expanded(object sender, RoutedEventArgs e)
        {
            ((Expander)sender).BringIntoView();
        }

        private void Button_ZipLog_Click(object sender, RoutedEventArgs e)
        {
            var directory = new DirectoryInfo("logs");
            FileInfo latestlogfile = directory.GetFiles("alotinstaller*.txt").OrderByDescending(f => f.LastWriteTime).First();
            if (latestlogfile != null)
            {
                string zipPath = "logs\\" + Path.GetFileNameWithoutExtension(latestlogfile.FullName) + ".zip";
                if (File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                }

                string zipStaged = latestlogfile.FullName + "_forZip";
                File.Copy(latestlogfile.FullName, zipStaged, true);
                string copyPath = zipPath;

                using (ZipArchive zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                {
                    zip.CreateEntryFromFile(zipStaged, "logs\\" + Path.GetFileName(latestlogfile.FullName), CompressionLevel.Optimal);
                }
                File.Delete(zipStaged);
                Utilities.OpenAndSelectFileInExplorer(zipPath);
                ShowStatus("If uploading log to ALOT Discord, drag and drop the .zip file onto the Discord interface.");
            }
        }

        private void Button_OpenAddonAssistantWindow_Click(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists(DOWNLOADS_FOLDER))
            {

                if (!Utilities.IsWindowOpen<AddonDownloadAssistant>())
                {
                    List<AddonFile> notReadyAddonFiles = new List<AddonFile>();
                    foreach (AddonFile af in addonfiles)
                    {
                        if (!af.Ready && !af.UserFile)
                        {
                            notReadyAddonFiles.Add(af);
                        }
                    }
                    DOWNLOAD_ASSISTANT_WINDOW = new AddonDownloadAssistant(this, notReadyAddonFiles);
                    DOWNLOAD_ASSISTANT_WINDOW.Show();
                }
            }
            else
            {
                ShowStatus("Download directory is not set to valid folder. Set a valid one in settings.");
            }
        }

        private void Button_ChangeDownloadFolder_Click(object sender, RoutedEventArgs e)
        {
            var openFolder = new CommonOpenFileDialog();
            openFolder.IsFolderPicker = true;
            openFolder.Title = "Select Downloads Folder where files from your browser are downloaded to";
            openFolder.AllowNonFileSystemItems = false;
            openFolder.EnsurePathExists = true;
            if (Directory.Exists(DOWNLOADS_FOLDER))
            {
                openFolder.InitialDirectory = DOWNLOADS_FOLDER;
            }
            if (openFolder.ShowDialog() != CommonFileDialogResult.Ok)
            {
                return;
            }
            var dir = openFolder.FileName;
            if (!Directory.Exists(dir))
            {
                //await this.ShowMessageAsync("Directory does not exist", "The backup destination directory does not exist: " + dir);
                return;
            }
            Utilities.WriteRegistryKey(Registry.CurrentUser, REGISTRY_KEY, SETTINGSTR_DOWNLOADSFOLDER, dir);
            DOWNLOADS_FOLDER = dir;
            Button_ChangeDownloadFolder.ToolTip = "Changes folder where download assistant will import from.\nThis should be your browser's download folder.\nThe current directory is\n" + DOWNLOADS_FOLDER;
        }

        private void DownloadAssisant_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!Button_DownloadAssistant.IsEnabled)
            {
                if (DOWNLOAD_ASSISTANT_WINDOW != null)
                {
                    DOWNLOAD_ASSISTANT_WINDOW.SHUTTING_DOWN = false;
                    DOWNLOAD_ASSISTANT_WINDOW.Close();
                }
            }
        }

        private void Button_GenerateDiagnostics_Click(object sender, RoutedEventArgs e)
        {
            DiagnosticsWindow dw = new DiagnosticsWindow();
            dw.ShowDialog();
        }
    }
}