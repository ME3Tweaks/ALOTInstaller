using AlotAddOnGUI.classes;
using ByteSizeLib;
using MahApps.Metro;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using Octokit;
using Serilog;
using SlavaGu.ConsoleAppLauncher;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
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

        private DispatcherTimer backgroundticker;
        private int completed = 0;
        //private int addonstoinstall = 0;
        private int CURRENT_GAME_BUILD = 0; //set when extraction is run/finished
        private int ADDONSTOINSTALL_COUNT = 0;
        private bool Installing = false;
        public static readonly string REGISTRY_KEY = @"SOFTWARE\ALOTAddon";
        public static readonly string ME3_BACKUP_REGISTRY_KEY = @"SOFTWARE\Mass Effect 3 Mod Manager";

        private BackgroundWorker InstallWorker = new BackgroundWorker();
        private BackgroundWorker BackupWorker = new BackgroundWorker();
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
        private string SETTINGSTR_BETAMODE = "BetaMode";
        private bool HIDENONRELEVANTFILES = false;
        private List<string> BACKGROUND_MEM_PROCESS_ERRORS;
        private const string SHOW_DIALOG_YES_NO = "SHOW_DIALOG_YES_NO";
        private bool CONTINUE_BACKUP_EVEN_IF_VERIFY_FAILS = false;
        private bool ERROR_SHOWING = false;
        public bool USING_BETA { get; private set; }
        public bool SpaceSaving { get; private set; }
        public StringBuilder BACKGROUND_MEM_STDOUT { get; private set; }
        public int BACKUP_THREAD_GAME { get; private set; }

        public MainWindow()
        {
            Log.Information("MainWindow() is starting");
            InitializeComponent();
            LoadSettings();
            Title = "ALOT Addon Builder " + System.Reflection.Assembly.GetEntryAssembly().GetName().Version;
            HeaderLabel.Text = "Preparing application...";
            AddonFilesLabel.Text = "Please wait";

        }

        /*private async void RunUpdater()
        {
            Log.Information("Running GHUpdater.");
            AddonFilesLabel.Text = "Checking for application updates";
                                await FetchManifest();

            string updaterpath = EXE_DIRECTORY + BINARY_DIRECTORY + "GHUpdater.exe";
            string temppath = Path.GetTempPath() + "GHUpdater.exe";
            var versInfo = FileVersionInfo.GetVersionInfo(updaterpath);
            String fileVersion = versInfo.FileVersion;
            Log.Information("GHUpdater.exe version: " + fileVersion);

            File.Copy(updaterpath, temppath, true);
            bool stillChecking = true;
            pipe = new AnonymousPipes("GHUPDATE_SERVER", temppath, "", delegate (String msg)
            {
                Dispatcher.Invoke((MethodInvoker)async delegate ()
                {
                    //UI THREAD
                    string[] clientmessage = msg.Split();
                    Debug.WriteLine(clientmessage[0]);
                    switch (clientmessage[0].Trim())
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
                            stillChecking = false;
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
                            stillChecking = false;
                            AddonFilesLabel.Text = "Error occured checking for updates";
                            await FetchManifest();
                            break;
                        case "UPDATE_AVAILABLE":
                            stillChecking = false;
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
                                updateprogresscontroller = await this.ShowProgressAsync("Installing Update", "ALOT Addon Builder is updating. Please wait...", true);
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
                            AddonFilesLabel.Text = "Lost connection to update client";
                        }
                    });
                }
                catch (Exception) { }
            });
            //pipe.SendText("START_UPDATE_CHECK Mgamerz AlotAddOnGUI 1.0.0.0");
            Action killBgThread = new Action(async delegate ()
            {
                if (stillChecking)
                {
                    Log.Error("GHUpdater took too long to respond. Killing application updater and continuing.");
                    pipe.SendText("KILL_UPDATER");
                    pipe.Close();
                    await FetchManifest();
                }
            });

            pipe.SendText("START_UPDATE_CHECK Mgamerz AlotAddOnGUI " + System.Reflection.Assembly.GetEntryAssembly().GetName().Version);
            await Execute(killBgThread, 10000);
        }*/

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

        /*private void RunMEMUpdater()
        {
            Log.Information("Running GHUpdater for MEM.");
            AddonFilesLabel.Text = "Checking for Mass Effect Modder updates";
            string updaterpath = EXE_DIRECTORY + BINARY_DIRECTORY + "GHUpdater.exe";
            string temppath = Path.GetTempPath() + "GHUpdater-MEM.exe";
            try
            {
                File.Copy(updaterpath, temppath, true);
            }
            catch (Exception e)
            {
                //MEM updater file copy failed it seems.
                Log.Error("Error copying MEM updater: " + e.ToString());
                temppath = EXE_DIRECTORY + BINARY_DIRECTORY + "GHUpdater-MEM.exe";
                try
                {
                    File.Copy(updaterpath, temppath, true);
                }
                catch (Exception ex)
                {
                    Log.Error("Can't copy failsafe MEM updater: " + e.ToString());
                }
            }
            if (File.Exists(temppath))
            {
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
                                AddonFilesLabel.Text = "Error occured checking for MEM updates";
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
                                AddonFilesLabel.Text = "MassEffectModder has been updated.";
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
                                AddonFilesLabel.Text = "Lost connection to update client";
                            }
                        });
                    }
                    catch (Exception) { }
                });
                Thread.Sleep(2000);
                var versInfo = FileVersionInfo.GetVersionInfo(BINARY_DIRECTORY + MEM_EXE_NAME);
                int fileVersion = versInfo.FileMajorPart;
                Log.Information("Local Mass Effect Modder version: " + fileVersion);
                pipe.SendText("START_UPDATE_CHECK MassEffectModder MassEffectModder " + fileVersion);
            }
            else
            {
                Log.Error("MEM updater was not able to be extracted! Skipping for now.");
                AddonFilesLabel.Text = "Unable to check for MEM updates (see logs)";
            }
        }*/

        private async void RunApplicationUpdater2()
        {
            AddonFilesLabel.Text = "Checking for application updates";
            var versInfo = System.Reflection.Assembly.GetEntryAssembly().GetName().Version;
            var client = new GitHubClient(new ProductHeaderValue("ALOTAddonGUI"));
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
                                updateprogresscontroller.SetProgress((double)e.ProgressPercentage / 100);
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

        private async void RunMEMUpdaterGUI()
        {
            int fileVersion = 0;
            if (File.Exists(BINARY_DIRECTORY + "MassEffectModder.exe"))
            {
                var versInfo = FileVersionInfo.GetVersionInfo(BINARY_DIRECTORY + "MassEffectModder.exe");
                fileVersion = versInfo.FileMajorPart;
            }

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

        private async void RunMEMUpdater2()
        {
            int fileVersion = 0;
            if (File.Exists(BINARY_DIRECTORY + "MassEffectModderNoGui.exe"))
            {
                var versInfo = FileVersionInfo.GetVersionInfo(BINARY_DIRECTORY + "MassEffectModderNoGui.exe");
                fileVersion = versInfo.FileMajorPart;
            }

            Label_MEMVersion.Content = "MEM (No GUI) Version: " + fileVersion;
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

        private void UnzipSelfUpdate(object sender, AsyncCompletedEventArgs e)
        {
            KeyValuePair<ProgressDialogController, string> kp = (KeyValuePair<ProgressDialogController, string>)e.UserState;
            if (File.Exists(kp.Value))
            {
                kp.Key.SetIndeterminate();
                kp.Key.SetTitle("Extracting ALOT Addon Installer Update");
                string path = BINARY_DIRECTORY + "7z.exe";
                string args = "x \"" + kp.Value + "\" -aoa -r -o\"" + System.AppDomain.CurrentDomain.BaseDirectory + "Update\"";
                Log.Information("Extracting update...");
                runProcess(path, args);

                File.Delete((string)kp.Value);
                kp.Key.CloseAsync();

                Log.Information("Update Extracted - rebooting to update mode");
                string exe = System.AppDomain.CurrentDomain.BaseDirectory + "Update\\" + System.AppDomain.CurrentDomain.FriendlyName;
                string currentDirNoSlash = System.AppDomain.CurrentDomain.BaseDirectory;
                currentDirNoSlash = currentDirNoSlash.Substring(0, currentDirNoSlash.Length - 1);
                args = "--update-dest \"" + currentDirNoSlash + "\"";
                runProcess(exe, args, true);
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
            runProcess(path, args);
            Log.Information("Extraction complete.");

            File.Delete((string)kp.Value);
            kp.Key.CloseAsync();

            var versInfo = FileVersionInfo.GetVersionInfo(BINARY_DIRECTORY + MEM_EXE_NAME);
            int fileVersion = versInfo.FileMajorPart;
            Label_MEMVersion.Content = "MEM Version: " + fileVersion;
        }

        private void UnzipMEMGUIUpdate(object sender, AsyncCompletedEventArgs e)
        {

            //Extract 7z
            string path = BINARY_DIRECTORY + "7z.exe";

            string args = "x \"" + e.UserState + "\" -aoa -r -o\"" + System.AppDomain.CurrentDomain.BaseDirectory + "bin\"";
            Log.Information("Extracting MEMGUI update...");
            runProcess(path, args);
            Log.Information("Extraction complete.");

            File.Delete((string)e.UserState);
            var versInfo = FileVersionInfo.GetVersionInfo(BINARY_DIRECTORY + "MassEffectModder.exe");
            int fileVersion = versInfo.FileMajorPart;
            ShowStatus("Updated Mass Effect Modder (GUI version) to v" + fileVersion, 3000);
        }

        private async void InstallCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            Log.Information("Background thread has exited - starting InstallCompleted()");
            int result = (int)e.Result;
            Installing = false;
            SetupButtons();
            Button_Settings.IsEnabled = true;
            switch (result)
            {
                case -1:
                default:
                    HeaderLabel.Text = "An error occured building the Addon. The logs directory will have more information on what happened.";
                    AddonFilesLabel.Text = "Addon not successfully built";
                    Installing = true; //don't udpate the ticker
                                       //await this.ShowMessageAsync("Error building Addon", "An error occured building the Addon. The files in the logs directory may help diagnose the issue.");
                    break;
                case 1:
                case 2:
                case 3:
                    if (errorOccured)
                    {
                        HeaderLabel.Text = "Addon built with errors.\nThe Addon was built but some files did not process correctly and were skipped.\nThe MEM packages for the addon have been placed into the " + MEM_OUTPUT_DISPLAY_DIR + " directory.";
                        AddonFilesLabel.Text = "MEM Packages placed in the " + MEM_OUTPUT_DISPLAY_DIR + " folder";
                        await this.ShowMessageAsync("ALOT Addon for Mass Effect " + result + " was built, but had errors", "Some files had errors occured during the build process. These files were skipped. Your game may look strange in some parts if you use the built Addon. You should report this to the developers on Discord.\nYou can install the Addon MEM files with Mass Effect Modder after you've installed the main ALOT MEM file.");

                    }
                    else
                    {

                        //flash
                        var helper = new FlashWindowHelper(System.Windows.Application.Current);
                        // Flashes the window and taskbar 5 times and stays solid 
                        // colored until user focuses the main window
                        helper.FlashApplicationWindow();

                        HeaderLabel.Text = "Addon created.\nThe MEM packages for the addon have been placed into the " + MEM_OUTPUT_DISPLAY_DIR + " directory.";
                        AddonFilesLabel.Text = "MEM Packages placed in the " + MEM_OUTPUT_DISPLAY_DIR + " folder";
                        MetroDialogSettings mds = new MetroDialogSettings();
                        mds.AffirmativeButtonText = "Open MEM";

                        mds.NegativeButtonText = "OK";
                        mds.DefaultButtonFocus = MessageDialogResult.Affirmative;
                        var buildResult = await this.ShowMessageAsync("ALOT Addon for Mass Effect " + result + " has been built", "You can install this file by opening MEM and applying it after you have installed ALOT.", MessageDialogStyle.AffirmativeAndNegative, mds);
                        if (buildResult == MessageDialogResult.Affirmative)
                        {
                            //Install
                            string exe = BINARY_DIRECTORY + "MassEffectModder.exe";
                            string args = "";// "-install-addon-file \""+path+"\" -game "+result;

                            if (USING_BETA)
                            {
                                //write install.ini
                                // Or specify a specific name in a specific dir
                                var MyIni = new IniFile(BINARY_DIRECTORY + "Installer.ini");
                                MyIni.Write("GameId", "ME" + result, "Main");
                                MyIni.Write("SourceDir", getOutputDir(result), "Main");
                            }
                            //string filename = "ALOT_ME" + result + "_Addon.mem";
                            //string path = EXE_DIRECTORY + MEM_OUTPUT_DIR + "\\" + filename;
                            runProcess(exe, args, true);
                        }
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
                        AddonFilesLabel.Text = (string)tc.Data;
                        break;
                    case UPDATE_PROGRESSBAR_INDETERMINATE:
                        Install_ProgressBar.IsIndeterminate = (bool)tc.Data;
                        break;
                    case ERROR_OCCURED:

                        Install_ProgressBar.IsIndeterminate = false;
                        Install_ProgressBar.Value = 0;
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
                        Install_ProgressBar.Value = (completed / (double)ADDONSTOINSTALL_COUNT) * 100;
                        break;
                }
            }
        }

        private void BuildAddon(object sender, DoWorkEventArgs e)
        {
            bool result = ExtractAddons((int)e.Argument); //arg is game id.
            e.Result = result ? (int)e.Argument : -1; //1 = Error
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
                        Install_ProgressBar.Value = (int)(((double)numdone / addonfiles.Count) * 100);
                        AddonFilesLabel.Text = "ME1: " + numME1FilesReady + "/" + numME1Files + " - ME2: " + numME2FilesReady + "/" + numME2Files + " - ME3: " + numME3FilesReady + "/" + numME3Files;
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
            Log.Information("Window_Loaded()");
            if (EXE_DIRECTORY.Length > 95)
            {
                Log.Fatal("EXE is nested too deep for Addon to build properly (" + EXE_DIRECTORY.Length + " chars) due to Windows API limitations.");
                await this.ShowMessageAsync("ALOTAddonBuilder is too deep in the filesystem", "ALOT Addon Builder can have issues extracting and building the Addon if nested too deeply in the filesystem. This is an issue with Windows file path limitations. Move the Addon directory up a few folders on your filesystem. A good place to put the Addon is in Documents.");
                Environment.Exit(1);
            }

            bool hasWriteAccess = await testWriteAccess();
            if (hasWriteAccess) RunApplicationUpdater2();
        }

        private async void SetupButtons()
        {
            /*string exe = BINARY_DIRECTORY + MEM_EXE_NAME;
            string args = "-get-installed-games";
            int installedGames = runProcess(exe, args);*/

            string me1Path = Utilities.GetGamePath(1);
            string me2Path = Utilities.GetGamePath(2);
            string me3Path = Utilities.GetGamePath(3);

            //int installedGames = 5;
            me1Installed = (me1Path != null);
            me2Installed = (me2Path != null);
            me3Installed = (me3Path != null);
            Log.Information("ME1 Installed: " + me1Installed);
            Log.Information("ME2 Installed: " + me2Installed);
            Log.Information("ME3 Installed: " + me3Installed);


            if (me1Installed || me2Installed || me3Installed)
            {
                if (backgroundticker == null)
                {
                    backgroundticker = new DispatcherTimer();
                    backgroundticker.Tick += new EventHandler(timer_Tick);
                    backgroundticker.Interval = new TimeSpan(0, 0, 5); // execute every 5s
                    backgroundticker.Start();

                    InstallWorker.DoWork += BuildAddon;
                    InstallWorker.ProgressChanged += InstallProgressChanged;
                    InstallWorker.RunWorkerCompleted += InstallCompleted;
                    InstallWorker.WorkerReportsProgress = true;
                }

                if (!me1Installed)
                {
                    Log.Information("ME1 not installed - disabling ME1 install");
                    Button_InstallME1.IsEnabled = false;
                    Button_InstallME1.ToolTip = "Mass Effect is not installed. To build the addon for ME1 the game must already be installed";
                    Button_InstallME1.Content = "ME1 Not Installed";
                    Button_ME1Backup.IsEnabled = false;
                }
                else
                {
                    Button_InstallME1.IsEnabled = true;
                    Button_InstallME1.ToolTip = "Click to build ALOT Addon for Mass Effect";
                    Button_InstallME1.Content = "Build Addon for ME1";
                    ValidateGameBackup(1);
                }

                if (!me2Installed)
                {
                    Log.Information("ME2 not installed - disabling ME2 install");
                    Button_InstallME2.IsEnabled = false;
                    Button_InstallME2.ToolTip = "Mass Effect 2 is not installed. To build the addon for ME2 the game must already be installed";
                    Button_InstallME2.Content = "ME2 Not Installed";
                    Button_ME2Backup.IsEnabled = false;
                }
                else
                {
                    Button_InstallME2.IsEnabled = true;
                    Button_InstallME2.ToolTip = "Click to build ALOT Addon for Mass Effect 2";
                    Button_InstallME2.Content = "Build Addon for ME2";
                    ValidateGameBackup(2);
                }

                if (!me3Installed)
                {
                    Log.Information("ME3 not installed - disabling ME3 install");
                    Button_InstallME3.IsEnabled = false;
                    Button_InstallME3.ToolTip = "Mass Effect 3 is not installed. To build the addon for ME3 the game must already be installed";
                    Button_InstallME3.Content = "ME3 Not Installed";
                    Button_ME3Backup.IsEnabled = false;
                }
                else
                {
                    Button_InstallME3.IsEnabled = true;
                    Button_InstallME3.ToolTip = "Click to build ALOT Addon for Mass Effect 3";
                    Button_InstallME3.Content = "Build Addon for ME3";
                    ValidateGameBackup(3);
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
                            Button_ME1Backup.Content = "ME1: Backed Up";
                            Button_ME1Backup.ToolTip = "Click to restore game from " + Environment.NewLine + path;
                        }
                        else
                        {
                            Button_ME1Backup.Content = "ME1: Not Backed Up";
                            Button_ME1Backup.ToolTip = "Click to backup game";
                        }
                        Button_ME1Backup.ToolTip += Environment.NewLine + "Game is installed at " + Environment.NewLine + Utilities.GetGamePath(1);
                        return path != null;
                    }
                case 2:
                    {
                        string path = Utilities.GetGameBackupPath(2);
                        if (path != null)
                        {
                            Button_ME2Backup.Content = "ME2: Backed Up";
                            Button_ME2Backup.ToolTip = "Click to restore game from " + Environment.NewLine + path;
                        }
                        else
                        {
                            Button_ME2Backup.Content = "ME2: Not Backed Up";
                            Button_ME2Backup.ToolTip = "Click to backup game";
                        }
                        Button_ME2Backup.ToolTip += Environment.NewLine + "Game is installed at " + Environment.NewLine + Utilities.GetGamePath(2);
                        return path != null;
                    }
                case 3:
                    {
                        string path = Utilities.GetGameBackupPath(3);
                        if (path != null)
                        {
                            Button_ME3Backup.Content = "ME3: Backed Up";
                            Button_ME3Backup.ToolTip = "Click to restore game from " + Environment.NewLine + path;
                        }
                        else
                        {
                            Button_ME3Backup.Content = "ME3: Not Backed Up";
                            Button_ME3Backup.ToolTip = "Click to backup game";
                        }
                        Button_ME3Backup.ToolTip += Environment.NewLine + "Game is installed at " + Environment.NewLine + Utilities.GetGamePath(3);

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
                    Install_ProgressBar.IsIndeterminate = true;
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

                    Install_ProgressBar.IsIndeterminate = false;
                    HeaderLabel.Text = PRIMARY_HEADER;
                    AddonFilesLabel.Text = "Scanning...";
                    timer_Tick(null, null);
                    RunMEMUpdater2();
                    int alotme1ver = detectInstalledALOTVersion(1);
                    int alotme2ver = detectInstalledALOTVersion(2);
                    int alotme3ver = detectInstalledALOTVersion(3);

                    string message1 = "ME1: " + (alotme1ver != 0 ? "Installed, " + alotme1ver + ".x" : "Not installed");
                    string message2 = "ME2: " + (alotme2ver != 0 ? "Installed, " + alotme2ver + ".x" : "Not installed");
                    string message3 = "ME3: " + (alotme3ver != 0 ? "Installed, " + alotme3ver + ".x" : "Not installed");

                    Label_ALOTStatus_ME1.Content = message1;
                    Label_ALOTStatus_ME2.Content = message2;
                    Label_ALOTStatus_ME3.Content = message3;

                    RunMEMUpdaterGUI();
                }
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
                                Ready = false,
                                PackageFiles = e.Elements("packagefile")
                                    .Select(r => new PackageFile
                                    {
                                        ALOTVersion = r.Attribute("alotversion") != null ? (int)r.Attribute("destinationname") : 0,
                                        ALOTUpdate = r.Attribute("alotupdate") != null ? true : false,
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

            ApplyFiltering(); //sets data source and separators            
        }

        private void ApplyFiltering()
        {
            BindingList<AddonFile> newList = new BindingList<AddonFile>();
            foreach (AddonFile af in alladdonfiles)
            {
                bool shouldDisplay = ((af.Game_ME1 && me1Installed) || (af.Game_ME2 && me2Installed) || (af.Game_ME3 && me3Installed));

                if (!HIDENONRELEVANTFILES || shouldDisplay)
                {
                    newList.Add(af);
                }
            }
            addonfiles = newList;
            lvUsers.ItemsSource = addonfiles;
            CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(lvUsers.ItemsSource);
            PropertyGroupDescription groupDescription = new PropertyGroupDescription("Author");
            view.GroupDescriptions.Add(groupDescription);
            timer_Tick(null, null);
        }

        public sealed class AddonFile : INotifyPropertyChanged
        {
            public bool Showing { get; set; }
            public event PropertyChangedEventHandler PropertyChanged;
            private bool m_ready;
            public bool ProcessAsModFile { get; set; }
            public string Author { get; set; }
            public string FriendlyName { get; set; }
            public bool Game_ME1 { get; set; }
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
            public int ALOTVersion { get; set; }
            public bool ALOTUpdate { get; set; }
            public string SourceName { get; set; }
            public string DestinationName { get; set; }
            public bool MoveDirectly { get; set; }
            public bool CopyDirectly { get; set; }
            public bool Delete { get; set; }
            public string TPFSource { get; set; }
            public bool ME1 { get; set; }
            public bool ME2 { get; set; }
            public bool ME3 { get; set; }
            public bool Processed { get; set; }

            internal bool AppliesToGame(int game)
            {
                if (game == 1)
                {
                    return ME1;
                }
                if (game == 2)
                {
                    return ME2;
                }
                if (game == 3)
                {
                    return ME3;
                }
                return false;
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
                if (await InitInstall(2))
                {
                    Button_InstallME2.Content = "Building...";
                    CURRENT_GAME_BUILD = 2;
                    InstallWorker.RunWorkerAsync(2);
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
            if (detectInstalledALOTVersion(game) != 0)
            {
                //Game is modified via ALOT flag
                await this.ShowMessageAsync("ALOT is installed", "You cannot backup an installation that has ALOT already installed. You will need to redownload the game, then back it up.");
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
            Installing = true;
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
            Installing = false;
            ValidateGameBackup(BACKUP_THREAD_GAME);
            BACKUP_THREAD_GAME = -1;
            HeaderLabel.Text = PRIMARY_HEADER;
        }

        private async void BackupWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
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
                        AddonFilesLabel.Text = (string)tc.Data;
                        break;
                    case UPDATE_PROGRESSBAR_INDETERMINATE:
                        Install_ProgressBar.IsIndeterminate = (bool)tc.Data;
                        break;
                    case ERROR_OCCURED:
                        Install_ProgressBar.IsIndeterminate = false;
                        Install_ProgressBar.Value = 0;
                        //await this.ShowMessageAsync("Error building Addon MEM Package", "An error occured building the addon. The logs will provide more information. The error message given is:\n" + (string)tc.Data);
                        break;
                    case SHOW_DIALOG:
                        KeyValuePair<string, string> messageStr = (KeyValuePair<string, string>)tc.Data;
                        await this.ShowMessageAsync(messageStr.Key, messageStr.Value);
                        break;
                    case SHOW_DIALOG_YES_NO:
                        ThreadCommandDialogOptions tcdo = (ThreadCommandDialogOptions)tc.Data;
                        MetroDialogSettings settings = new MetroDialogSettings();
                        settings.NegativeButtonText = tcdo.NegativeButtonText;
                        settings.AffirmativeButtonText = tcdo.AffirmativeButtonText;
                        MessageDialogResult result = await this.ShowMessageAsync(tcdo.title, tcdo.message, MessageDialogStyle.AffirmativeAndNegative, settings);
                        if (result == MessageDialogResult.Negative)
                        {
                            CONTINUE_BACKUP_EVEN_IF_VERIFY_FAILS = false;
                        }
                        else
                        {
                            CONTINUE_BACKUP_EVEN_IF_VERIFY_FAILS = true;
                        }
                        tcdo.signalHandler.Set();
                        break;
                    case INCREMENT_COMPLETION_EXTRACTION:
                        Interlocked.Increment(ref completed);
                        Install_ProgressBar.Value = (completed / (double)ADDONSTOINSTALL_COUNT) * 100;
                        break;
                }
            }
        }

        private void BackupGame(object sender, DoWorkEventArgs e)
        {

            //verify vanilla
            Log.Information("Verifying game: Mass Effect " + BACKUP_THREAD_GAME);
            string exe = BINARY_DIRECTORY + MEM_EXE_NAME;
            string args = "-check-game-data-only-vanilla " + BACKUP_THREAD_GAME + " -ipc";
            List<string> acceptedIPC = new List<string>();
            acceptedIPC.Add("OVERALL_PROGRESS");
            acceptedIPC.Add("ERROR");
            BackupWorker.ReportProgress(completed, new ThreadCommand(UPDATE_OPERATION_LABEL, "Verifying game data..."));

            runMEM2(exe, args, BackupWorker, acceptedIPC);
            while (BACKGROUND_MEM_PROCESS.State == AppState.Running)
            {
                Thread.Sleep(250);
            }
            int buildresult = BACKGROUND_MEM_PROCESS.ExitCode ?? 1;
            if (buildresult != 0)
            {
                string modified = "";
                string gameDir = Utilities.GetGamePath(BACKUP_THREAD_GAME);
                foreach (String error in BACKGROUND_MEM_PROCESS_ERRORS)
                {
                    modified += "\n - " + error.Remove(0, gameDir.Length + 1);
                }
                ThreadCommandDialogOptions tcdo = new ThreadCommandDialogOptions();
                tcdo.signalHandler = new EventWaitHandle(false, EventResetMode.AutoReset);
                tcdo.title = "Game is modified";
                tcdo.message = "Mass Effect" + getGameNumberSuffix(BACKUP_THREAD_GAME) + " has files that do not match what is in the MEM database.\nYou can continue to back this installation up, but it may not be truly unmodified." + modified;
                tcdo.NegativeButtonText = "Abort";
                tcdo.AffirmativeButtonText = "Continue";
                BackupWorker.ReportProgress(completed, new ThreadCommand(SHOW_DIALOG_YES_NO, tcdo));
                tcdo.signalHandler.WaitOne();
                //Thread resumes
                if (!CONTINUE_BACKUP_EVEN_IF_VERIFY_FAILS)
                {
                    e.Result = null;
                    return;
                }
                else
                {
                    CONTINUE_BACKUP_EVEN_IF_VERIFY_FAILS = false; //reset
                }
            }
            string gamePath = Utilities.GetGamePath(BACKUP_THREAD_GAME);
            string backupPath = (string)e.Argument;
            string[] ignoredExtensions = { ".wav", ".pdf" };
            if (gamePath != null)
            {
                CopyDir.CopyAll_ProgressBar(new DirectoryInfo(gamePath), new DirectoryInfo(backupPath), BackupWorker, -1, 0, ignoredExtensions);
            }
            if (BACKUP_THREAD_GAME == 3)
            {
                //Create Mod Manaager vanilla backup marker
                string file = backupPath + "\\cmm_vanilla";
                File.Create(file);
            }
            e.Result = backupPath;
        }

        private string getGameNumberSuffix(int gameNumber)
        {
            return gameNumber == 1 ? "" : " " + gameNumber;
        }

        private void RestoreGame(object sender, DoWorkEventArgs e)
        {
            string gamePath = Utilities.GetGamePath(BACKUP_THREAD_GAME);
            string backupPath = Utilities.GetGameBackupPath(BACKUP_THREAD_GAME);
            BackupWorker.ReportProgress(completed, new ThreadCommand(UPDATE_PROGRESSBAR_INDETERMINATE, true));
            BackupWorker.ReportProgress(completed, new ThreadCommand(UPDATE_OPERATION_LABEL, "Deleting exist game installation"));
            if (Directory.Exists(gamePath))
            {
                Log.Information("Deleting existing game directory: " + gamePath);
                try
                {
                    Utilities.DeleteFilesAndFoldersRecursively(gamePath);
                }
                catch (Exception ex)
                {
                    Log.Error("Exception deleting game directory: " + gamePath + ": " + ex.Message);
                }
            }
            Directory.CreateDirectory(gamePath);
            BackupWorker.ReportProgress(completed, new ThreadCommand(UPDATE_PROGRESSBAR_INDETERMINATE, false));
            BackupWorker.ReportProgress(completed, new ThreadCommand(UPDATE_OPERATION_LABEL, "Restoring game from backup "));
            if (gamePath != null)
            {
                CopyDir.CopyAll_ProgressBar(new DirectoryInfo(backupPath), new DirectoryInfo(gamePath), BackupWorker, -1, 0);
            }
            if (BACKUP_THREAD_GAME == 3)
            {
                //Check for cmmvanilla file and remove it present
                string file = gamePath + "cmm_vanilla";
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            e.Result = true;
        }

        private bool ExtractAddon(AddonFile af)
        {
            string stagingdirectory = System.AppDomain.CurrentDomain.BaseDirectory + MEM_STAGING_DIR + "\\";

            string prefix = "[" + Path.GetFileNameWithoutExtension(af.Filename) + "] ";
            Log.Information(prefix + "Processing extraction on " + af.Filename);
            string fileextension = System.IO.Path.GetExtension(af.Filename);
            ulong freeBytes;
            ulong diskSize;
            ulong totalFreeBytes;
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

                            //get free space for debug purposes
                            Utilities.GetDiskFreeSpaceEx(stagingdirectory, out freeBytes, out diskSize, out totalFreeBytes);
                            Log.Information("[SIZE] ADDONEXTRACTFINISH Free Space on current drive: " + ByteSize.FromBytes(freeBytes) + " " + freeBytes);
                            var moveableFiles = Directory.EnumerateFiles(extractpath) //<--- .NET 4.5
                                .Where(file => file.ToLower().EndsWith("tpf") || file.ToLower().EndsWith("mem"))
                                .ToList();
                            if (moveableFiles.Count() > 0)
                            {
                                //check for copy directly items first, and move them.

                                foreach (string tpf in moveableFiles)
                                {
                                    string name = Path.GetFileName(tpf);
                                    foreach (PackageFile pf in af.PackageFiles)
                                    {
                                        if (pf.MoveDirectly && pf.SourceName == name && pf.AppliesToGame(CURRENT_GAME_BUILD))
                                        {
                                            Log.Information("MoveDirectly specified - moving TPF/MEM to staging: " + name);
                                            File.Move(tpf, STAGING_DIRECTORY + name);
                                            pf.Processed = true; //no more ops on this package file.
                                            break;
                                        }
                                        if (pf.CopyDirectly && pf.SourceName == name && pf.AppliesToGame(CURRENT_GAME_BUILD))
                                        {
                                            Log.Information("CopyDirectly specified - copy TPF/MEM to staging: " + name);
                                            File.Copy(tpf, STAGING_DIRECTORY + name, true);
                                            pf.Processed = true; //We will still extract this as it is a copy step.
                                            break;
                                        }
                                        if (pf.Delete && pf.SourceName == name && pf.AppliesToGame(CURRENT_GAME_BUILD))
                                        {
                                            Log.Information("Delete specified - deleting unused TPF/MEM: " + name);
                                            File.Delete(tpf);
                                            pf.Processed = true; //no more ops on this package file.
                                            break;
                                        }
                                    }
                                }

                                //Extract the TPFs
                                exe = BINARY_DIRECTORY + MEM_EXE_NAME;
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

                                    exe = BINARY_DIRECTORY + MEM_EXE_NAME;
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
                                    if (SpaceSaving)
                                    {
                                        File.Move(memfile, stagingdirectory + "\\" + name + " - ME" + CURRENT_GAME_BUILD + ext);
                                    }
                                    else
                                    {
                                        File.Copy(memfile, stagingdirectory + "\\" + name + " - ME" + CURRENT_GAME_BUILD + ext, true);
                                    }
                                }
                            }

                            //get free space for debug purposes
                            Utilities.GetDiskFreeSpaceEx(stagingdirectory, out freeBytes, out diskSize, out totalFreeBytes);
                            Log.Information("[SIZE] ADDONFULLEXTRACT Free Space on current drive: " + ByteSize.FromBytes(freeBytes) + " " + freeBytes);

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
                            File.Copy(EXE_DIRECTORY + "Downloaded_Mods\\" + af.Filename, getOutputDir(CURRENT_GAME_BUILD) + af.Filename, true);
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
            Utilities.GetDiskFreeSpaceEx(stagingdirectory, out freeBytes, out diskSize, out totalFreeBytes);
            Log.Information("[SIZE] ADDON EXTRACTADDON COMPLETED Free Space on current drive: " + ByteSize.FromBytes(freeBytes) + " " + freeBytes);
            return true;
        }

        private async void Button_InstallME3_Click(object sender, RoutedEventArgs e)
        {
            if (await InstallPrecheck(3))
            {
                if (await InitInstall(3))
                {
                    Button_InstallME3.Content = "Building...";
                    CURRENT_GAME_BUILD = 3;
                    InstallWorker.RunWorkerAsync(3);
                }
            }
        }

        private async Task<bool> InstallPrecheck(int game)
        {
            timer_Tick(null, null);
            int nummissing = 0;
            bool oneisready = false;
            foreach (AddonFile af in addonfiles)
            {
                if ((af.Game_ME1 && game == 1) || (af.Game_ME2 && game == 2) || (af.Game_ME3 && game == 3))
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
            ulong freeBytes;
            ulong diskSize;
            ulong totalFreeBytes;
            bool gotFreeSpace = Utilities.GetDiskFreeSpaceEx(stagingdirectory, out freeBytes, out diskSize, out totalFreeBytes);
            Log.Information("[SIZE] PREBUILD Free Space on current drive: " + ByteSize.FromBytes(freeBytes) + " " + freeBytes);

            string basepath = EXE_DIRECTORY + @"Downloaded_Mods\";
            string destinationpath = EXE_DIRECTORY + @"Extracted_Mods\";
            Log.Information("Created Extracted_Mods folder");

            Directory.CreateDirectory(destinationpath);

            List<AddonFile> addonstoinstall = new List<AddonFile>();
            foreach (AddonFile af in addonfiles)
            {
                if (af.Ready && (game == 1 && af.Game_ME1 || game == 2 && af.Game_ME2 || game == 3 && af.Game_ME3))
                {
                    Log.Information("Adding AddonFile to installation list: " + af.FriendlyName);
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
                string exe = BINARY_DIRECTORY + MEM_EXE_NAME;
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
                string exe = BINARY_DIRECTORY + MEM_EXE_NAME;
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
                        if ((game == 1 && pf.ME1 || game == 2 && pf.ME2 || game == 3 && pf.ME3) && !pf.Processed)
                        {
                            string extractedpath = basepath + Path.GetFileNameWithoutExtension(af.Filename) + "\\" + pf.SourceName;
                            if (File.Exists(extractedpath) && pf.DestinationName != null)
                            {
                                Log.Information("Copying Package File: " + pf.SourceName + "->" + pf.DestinationName);
                                string destination = stagingdirectory + pf.DestinationName;
                                File.Copy(extractedpath, destination, true);
                            }
                            else if (pf.DestinationName == null)
                            {
                                Log.Error("File destination in null. This means there is a problem in the manifest or manifest parser. File: " + pf.SourceName);
                                errorOccured = true;
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

            Utilities.GetDiskFreeSpaceEx(stagingdirectory, out freeBytes, out diskSize, out totalFreeBytes);
            Log.Information("[SIZE] POSTEXTRACT_PRESTAGING Free Space on current drive: " + ByteSize.FromBytes(freeBytes) + " " + freeBytes);

            //COLEANUP EXTRACTION DIR
            Log.Information("Completed staging. Now cleaning up extraction directory");
            InstallWorker.ReportProgress(completed, new ThreadCommand(UPDATE_OPERATION_LABEL, "Cleaning up extraction directory"));
            InstallWorker.ReportProgress(completed, new ThreadCommand(UPDATE_PROGRESSBAR_INDETERMINATE, true));
            InstallWorker.ReportProgress(100);
            try
            {
                Directory.Delete("Extracted_Mods", true);
                Log.Information("Deleted Extracted_Mods directory");
            }
            catch (IOException e)
            {
                Log.Error("Unable to delete extraction directory.");
            }

            Utilities.GetDiskFreeSpaceEx(stagingdirectory, out freeBytes, out diskSize, out totalFreeBytes);
            Log.Information("[SIZE] AFTER_EXTRACTION_CLEANUP Free Space on current drive: " + ByteSize.FromBytes(freeBytes) + " " + freeBytes);

            //BUILD MEM PACKAGE
            InstallWorker.ReportProgress(0);
            InstallWorker.ReportProgress(completed, new ThreadCommand(UPDATE_OPERATION_LABEL, "Building Addon MEM Package..."));
            int buildresult = -2;
            {
                Log.Information("Building MEM Package.");
                string exe = BINARY_DIRECTORY + MEM_EXE_NAME;
                string filename = "ALOT_ME" + game + "_Addon.mem";
                string args = "-convert-to-mem " + game + " \"" + EXE_DIRECTORY + MEM_STAGING_DIR + "\" \"" + getOutputDir(game) + filename + "\" -ipc";

                runMEM2(exe, args, InstallWorker);
                while (BACKGROUND_MEM_PROCESS.State == AppState.Running)
                {
                    Thread.Sleep(250);
                }

                Utilities.GetDiskFreeSpaceEx(stagingdirectory, out freeBytes, out diskSize, out totalFreeBytes);
                Log.Information("[SIZE] POST_MEM_BUILD Free Space on current drive: " + ByteSize.FromBytes(freeBytes) + " " + freeBytes);

                buildresult = BACKGROUND_MEM_PROCESS.ExitCode ?? 1;
                BACKGROUND_MEM_PROCESS = null;
                BACKGROUND_MEM_PROCESS_ERRORS = null;
                if (buildresult != 1)
                {
                    InstallWorker.ReportProgress(completed, new ThreadCommand(UPDATE_OPERATION_LABEL, "Cleaning up staging directories"));
                }

                if (buildresult != 0)
                {
                    Log.Error("Non-Zero return code! Something probably went wrong.");
                }
                if (buildresult == 0 && !File.Exists(getOutputDir(game) + filename))
                {
                    Log.Error("Process went OK but no outputfile... Something probably went wrong.");
                    buildresult = -1;
                }
            }
            //cleanup staging
            InstallWorker.ReportProgress(completed, new ThreadCommand(UPDATE_OPERATION_LABEL, "Cleaning up staging directory"));
            InstallWorker.ReportProgress(completed, new ThreadCommand(UPDATE_PROGRESSBAR_INDETERMINATE, true));
            InstallWorker.ReportProgress(100);
            try
            {
                Directory.Delete(MEM_STAGING_DIR, true);
                Log.Information("Deleted " + MEM_STAGING_DIR);
            }
            catch (IOException e)
            {
                Log.Error("Unable to delete staging directory. Addon should have been built however.\n" + e.ToString());
            }
            Utilities.GetDiskFreeSpaceEx(stagingdirectory, out freeBytes, out diskSize, out totalFreeBytes);
            Log.Information("[SIZE] FINAL Free Space on current drive: " + ByteSize.FromBytes(freeBytes) + " " + freeBytes);
            InstallWorker.ReportProgress(completed, new ThreadCommand(UPDATE_PROGRESSBAR_INDETERMINATE, false));
            CURRENT_GAME_BUILD = 0; //reset
            return buildresult == 0;
        }

        /// <summary>
        /// Returns the mem output dir with a \ on the end.
        /// </summary>
        /// <param name="game">Game number to get path for</param>
        /// <returns></returns>
        private String getOutputDir(int game)
        {
            return EXE_DIRECTORY + MEM_OUTPUT_DIR + "\\ME" + game + "\\";
        }

        private void runMEM2(string exe, string args, BackgroundWorker worker, List<string> acceptedIPC = null)
        {
            Debug.WriteLine("Running process: " + exe + " " + args);
            Log.Information("Running process: " + exe + " " + args);
            BACKGROUND_MEM_PROCESS = new ConsoleApp(exe, args);
            BACKGROUND_MEM_PROCESS_ERRORS = new List<string>();

            BACKGROUND_MEM_PROCESS.ConsoleOutput += (o, args2) =>
            {
                string str = args2.Line;
                if (str.StartsWith("[IPC]"))
                {
                    string command = str.Substring(5);
                    int endOfCommand = command.IndexOf(' ');
                    command = command.Substring(0, endOfCommand);
                    if (acceptedIPC == null || acceptedIPC.Contains(command))
                    {
                        string param = str.Substring(endOfCommand + 5).Trim();
                        switch (command)
                        {
                            case "OVERALL_PROGRESS":
                                worker.ReportProgress(completed, new ThreadCommand(UPDATE_PROGRESSBAR_INDETERMINATE, false));
                                int percentInt = Convert.ToInt32(param);
                                worker.ReportProgress(percentInt);
                                break;
                            case "PROCESSING_FILE":
                                worker.ReportProgress(completed, new ThreadCommand(UPDATE_OPERATION_LABEL, param));
                                break;
                            case "ERROR":
                                Log.Information("[ERROR] Realtime Process Output: " + param);
                                BACKGROUND_MEM_PROCESS_ERRORS.Add(param);
                                break;
                            default:
                                Log.Information("Unknown IPC command: " + command);
                                break;
                        }
                    }
                }
                else
                {
                    Log.Information("Realtime Process Output: " + str);
                }
            };
            BACKGROUND_MEM_PROCESS.Run();
        }

        /*  private int runMEM(string exe, string args)
          {
              Log.Information("Running MEM with IPC: " + exe + " " + args);
              using (Process p = new Process())
              {
                  p.StartInfo.CreateNoWindow = true;
                  p.StartInfo.FileName = exe;
                  p.StartInfo.UseShellExecute = false;
                  p.StartInfo.Arguments = args;
                  p.StartInfo.RedirectStandardOutput = true;
                  p.StartInfo.RedirectStandardError = true;

                  BACKGROUND_MEM_STDOUT = new StringBuilder();

                  _outputWaitHandle = new AutoResetEvent(false);
                  _errorWaitHandle = new AutoResetEvent(false);
                  p.OutputDataReceived += MEM_OutputDataReceived;
                  p.ErrorDataReceived += MEM_ErrorDataReceived;


                  BACKGROUND_MEM_RUNNING = true;
                  BACKGROUND_MEM_PROCESS = p;
                  p.Start();

                  p.BeginOutputReadLine();
                  p.BeginErrorReadLine();

              }
              return 0;
          }

          private void MEM_ErrorDataReceived(object sender, DataReceivedEventArgs e)
          {
              if (e.Data == null)
              {
                  _errorWaitHandle.Set();
              }
              else
              {
                  //error.AppendLine(e.Data);
              }
          }

          private void MEM_OutputDataReceived(object sender, DataReceivedEventArgs e)
          {
              if (e.Data == null)
              {
                  _outputWaitHandle.Set();
              }
              else
              {
                  string[] lines = e.Data.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None);
                  foreach (String str in lines)
                  {
                      if (str.StartsWith("[IPC]"))
                      {
                          string command = str.Substring(5);
                          int endOfCommand = command.IndexOf(' ');
                          command = command.Substring(0, endOfCommand);
                          Debug.WriteLine(command);
                          switch (command)
                          {
                              case "OVERALL_PROGRESS":
                                  InstallWorker.ReportProgress(completed, new ThreadCommand(UPDATE_PROGRESSBAR_INDETERMINATE, false));
                                  string percent = str.Substring(endOfCommand).Trim();
                                  int percentInt = Convert.ToInt32(percent);
                                  InstallWorker.ReportProgress(percentInt);
                                  break;
                              case "PROCESSING_FILE":
                                  break;
                          }
                      }
                      else
                      {
                          BACKGROUND_MEM_STDOUT.AppendLine(str);
                      }
                  }
              }
          }

          private void MEM_Exited(object sender, EventArgs e)
          {
              Log.Information("MEM output: " + BACKGROUND_MEM_STDOUT.ToString());
              BACKGROUND_MEM_RUNNING = false;
          }*/

        private int runProcess(string exe, string args, bool standAlone = false)
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
                    if (!standAlone)
                    {
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
                            Log.Error("Process timed out: " + exe + " " + args);
                            return -1;
                        }
                    }
                    else
                    {
                        return 0; //standalone
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
            this.nIcon.ShowBalloonTip(14000, "Directions", "Download the file with filename: \"" + fname + "\"", ToolTipIcon.Info);
        }

        private async Task<bool> InitInstall(int game)
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
                return false;
            }

            Installing = true;
            Button_InstallME1.IsEnabled = false;
            Button_InstallME2.IsEnabled = false;
            Button_InstallME3.IsEnabled = false;
            Button_Settings.IsEnabled = false;

            Directory.CreateDirectory(getOutputDir(game));
            Directory.CreateDirectory(MEM_STAGING_DIR);

            AddonFilesLabel.Text = "Preparing to install...";
            HeaderLabel.Text = "Building ALOT Addon for Mass Effect " + game + ".\nDon't close this window until the process completes.";
            // Install_ProgressBar.IsIndeterminate = true;
            return true;
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
                List<string> noMatchFiles = new List<string>();
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
                        if (af.Filename.Equals(fname, StringComparison.InvariantCultureIgnoreCase))
                        {
                            hasMatch = true;
                            if (af.Ready == false)
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
                        }
                    }
                    if (!hasMatch)
                    {
                        noMatchFiles.Add(file);
                        Log.Information("Dragged file does not match any addon manifest file: " + file);
                    }
                }
                if (noMatchFiles.Count > 0)
                {
                    if (noMatchFiles.Count == 1)
                    {
                        ShowStatus("Not an addon file: " + Path.GetFileName(noMatchFiles[0]));
                    }
                    else
                    {
                        ShowStatus(noMatchFiles.Count + " files were dropped that aren't Addon files");
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

        private void ShowStatus(string v, int msOpen = 8000)
        {
            ImportFailedFlyout.AutoCloseInterval = msOpen;
            ImportFailedLabel.Content = v;
            ImportFailedFlyout.IsOpen = true;
        }

        private void Button_Settings_Click(object sender, RoutedEventArgs e)
        {
            SettingsFlyout.IsOpen = true;
        }



        private async void Button_About_Click(object sender, RoutedEventArgs e)
        {
            string title = "ALOT Addon Builder " + System.Reflection.Assembly.GetEntryAssembly().GetName().Version + "\n";
            var versInfo = FileVersionInfo.GetVersionInfo(BINARY_DIRECTORY + MEM_EXE_NAME);
            int fileVersion = versInfo.FileMajorPart;

            string credits = "MEM Version: " + fileVersion + "\n" + "\n\nBrought to you by:\n - Mgamerz\n - CreeperLava\n - aquadran\n\nSource code: https://github.com/Mgamerz/AlotAddOnGUI\nLicensed under GPLv3";

            MetroDialogSettings settings = new MetroDialogSettings();
            settings.NegativeButtonText = "OK";
            settings.AffirmativeButtonText = "View log";
            MessageDialogResult result = await this.ShowMessageAsync(title, credits, MessageDialogStyle.AffirmativeAndNegative, settings);
            if (result == MessageDialogResult.Negative)
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
                    Button_InstallME1.Content = "Building...";
                    CURRENT_GAME_BUILD = 1;
                    InstallWorker.RunWorkerAsync(1);
                }
            }
        }

        private int detectInstalledALOTVersion(int gameID)
        {
            const uint MEMI_TAG = 0x494D454D;

            string gamePath = Utilities.GetGamePath(gameID);
            if (gamePath != null)
            {

                if (gameID == 1)
                {
                    gamePath += @"\BioGame\CookedPC\testVolumeLight_VFX.upk";
                }
                if (gameID == 2)
                {
                    gamePath += @"\BioGame\CookedPC\BIOC_Materials.pcc";
                }
                if (gameID == 3)
                {
                    gamePath += @"\BIOGame\CookedPCConsole\adv_combat_tutorial_xbox_D_Int.afc";
                }

                if (File.Exists(gamePath))
                {
                    using (FileStream fs = new FileStream(gamePath, System.IO.FileMode.Open, FileAccess.Read))
                    {
                        fs.SeekEnd();
                        long endPos = fs.Position;
                        fs.Position = endPos - 4;
                        uint memi = fs.ReadUInt32();

                        if (memi == MEMI_TAG)
                        {
                            //ALOT has been installed
                            fs.Position = endPos - 8;
                            int memVersionUsed = fs.ReadInt32();

                            if (memVersionUsed >= 178 && memVersionUsed != 16777472) //default bytes before 178 MEMI Format
                            {
                                fs.Position = endPos - 12;
                                int ALOTVER = fs.ReadInt32();

                                //unused for now
                                fs.Position = endPos - 16;
                                int MEUITMVER = fs.ReadInt32();

                                return ALOTVER;
                            }
                        }
                    }
                }
            }
            return 0;
        }

        private void Checkbox_HideFiles_Click(object sender, RoutedEventArgs e)
        {
            ApplyFiltering();
            Utilities.WriteRegistryKey(Registry.CurrentUser, REGISTRY_KEY, SETTINGSTR_HIDENONRELEVANTFILES, ((bool)Checkbox_HideFiles.IsChecked ? 1 : 0));
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
            USING_BETA = Utilities.GetRegistrySettingBool(SETTINGSTR_BETAMODE) ?? false;
            HIDENONRELEVANTFILES = Utilities.GetRegistrySettingBool(SETTINGSTR_HIDENONRELEVANTFILES) ?? true;

            Checkbox_BetaMode.IsChecked = USING_BETA;
            Checkbox_HideFiles.IsChecked = HIDENONRELEVANTFILES;

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
            Installing = true;
            HeaderLabel.Text = "Restoring Mass Effect" + (game == 1 ? "" : " " + game) + "...\nDo not close the application until this process completes.";
            BackupWorker.RunWorkerAsync();
        }

        private void RestoreCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            bool result = (bool)e.Result;
            if (result)
            {
                AddonFilesLabel.Text = "Restore completed.";
            }
            else
            {
                AddonFilesLabel.Text = "Restore failed! Check the logs.";
            }
            Button_Settings.IsEnabled = true;
            Installing = false;

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
            runProcess(exe, args, true);
        }
    }
}