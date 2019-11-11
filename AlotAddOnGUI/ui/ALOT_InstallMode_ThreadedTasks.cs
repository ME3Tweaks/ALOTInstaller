using AlotAddOnGUI.classes;
using AlotAddOnGUI.music;
using AlotAddOnGUI.ui;
using ByteSizeLib;
using Flurl.Http;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Taskbar;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Serilog;
using SlavaGu.ConsoleAppLauncher;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Xml.Linq;
using ME3Explorer.Packages;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;

namespace AlotAddOnGUI
{
    public partial class MainWindow : MetroWindow
    {
        private bool STAGE_DONE_REACHED = false;
        private bool TELEMETRY_IS_FULL_NEW_INSTALL;
        private const int RESULT_TEXTURE_EXPORT_FIX_FAILED = -38;
        private const int RESULT_MARKERCHECK_FAILED = -39;

        //Codes should be > -38 (e.g. -37)
        private const int RESULT_ME1LAA_FAILED = -43;
        private const int RESULT_UNKNOWN_ERROR = -51;
        private const int RESULT_BIOGAME_MISSING = -53;
        private const int RESULT_SET_READWRITE_FAILED = -54;

        private const string SHOW_ALL_STAGE_LABELS = "SHOW_ALL_STAGE_LABELS";
        private const string HIDE_STAGE_OF_STAGE_LABEL = "HIDE_STAGE_OF_STAGE_LABEL";

        private void MusicIcon_Click(object sender, RoutedEventArgs e)
        {
            if (MusicIsPlaying)
            {
                MusicPaused = !MusicPaused;
                if (MusicPaused)
                {
                    try
                    {
                        waveOut.Pause();
                        MusicButtonIcon.Kind = MahApps.Metro.IconPacks.PackIconModernKind.SoundMute;
                        Utilities.WriteRegistryKey(Registry.CurrentUser, REGISTRY_KEY, SETTINGSTR_SOUND, !MusicPaused);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Exception attempting to pause music: " + ex.Message);
                    }
                }
                else
                {
                    try
                    {
                        waveOut.Play();
                        MusicButtonIcon.Kind = MahApps.Metro.IconPacks.PackIconModernKind.Sound3;
                        Utilities.WriteRegistryKey(Registry.CurrentUser, REGISTRY_KEY, SETTINGSTR_SOUND, !MusicPaused);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Exception attempting to play music: " + ex.Message);
                    }
                }
            }
        }
        private void MusicPlaybackStopped(object sender, NAudio.Wave.StoppedEventArgs e)
        {
            if (MusicIsPlaying)
            {
                vorbisStream.Position = 0;
            }
            else
            {
                waveOut.Stop();
                waveOut.Dispose();
                waveOut = null;
            }
        }
        private string GetMusicDirectory()
        {
            return EXE_DIRECTORY + "Data\\music\\";
        }
        private void newTipTimer_Tick(object sender, EventArgs e)
        {
            // code goes here
            if (TIPS_LIST.Count > 1)
            {
                string currentTip = InstallingOverlay_Tip.Text;
                string newTip = InstallingOverlay_Tip.Text;

                while (currentTip == newTip)
                {
                    int r = RANDOM.Next(TIPS_LIST.Count);
                    newTip = TIPS_LIST[r];
                }
                InstallingOverlay_Tip.Text = newTip;
            }
        }

        private void InstallALOT(int game, List<AddonFile> filesToInstall, string sourcePathOverride = null)
        {
            if (filesToInstall == null) filesToInstall = new List<AddonFile>();

            MEM_INSTALL_TIME_SECONDS = 0;
            ADDONFILES_TO_INSTALL = filesToInstall;
            WARN_USER_OF_EXIT = true;
            InstallingOverlay_TopLabel.Text = "Preparing installer";
            InstallWorker = new BackgroundWorker();
            InstallWorker.DoWork += InstallALOTContextBased;
            InstallWorker.WorkerReportsProgress = true;
            InstallWorker.ProgressChanged += InstallWorker_ProgressChanged;
            InstallWorker.RunWorkerCompleted += InstallCompleted;
            INSTALLING_THREAD_GAME = game;
            WindowButtonCommandsOverlayBehavior = WindowCommandsOverlayBehavior.Flyouts;
            InstallingOverlayFlyout.Theme = FlyoutTheme.Dark;

            //Set BG for this game
            string bgPath = "images/me" + game + "_bg.jpg";
            if (MEUITM_INSTALLER_MODE) bgPath = "images/meuitm.jpg";
            if (DateTime.Now.Month == 4 && DateTime.Now.Day == 1)
            {
                bgPath = "images/me" + game + "_bg_alt.jpg";
            }
            ImageBrush background = new ImageBrush(new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), bgPath)));
            background.Stretch = Stretch.UniformToFill;
            InstallingOverlayFlyout.Background = background;
            Button_InstallDone.Visibility = System.Windows.Visibility.Hidden;
            Installing_Spinner.Visibility = System.Windows.Visibility.Visible;
            Installing_Checkmark.Visibility = System.Windows.Visibility.Hidden;
            PreventFileRefresh = true;
            HeaderLabel.Text = "Installing MEMs...";
            AddonFilesLabel.Text = "Running in installer mode.";
            InstallingOverlay_Tip.Visibility = System.Windows.Visibility.Visible;
            //InstallingOverlay_StageOfStageLabel.Visibility = System.Windows.Visibility.Visible;
            //InstallingOverlay_OverallProgressLabel.Visibility = System.Windows.Visibility.Visible;
            InstallingOverlay_OverallProgressLabel.Text = "";
            InstallingOverlay_StageOfStageLabel.Text = "Getting ready";
            InstallingOverlay_BottomLabel.Text = "Please wait";
            Button_InstallViewLogs.Visibility = System.Windows.Visibility.Collapsed;


            SolidColorBrush backgroundShadeBrush = null;
            switch (INSTALLING_THREAD_GAME)
            {
                case 1:
                    backgroundShadeBrush = new SolidColorBrush(Color.FromArgb(0x77, 0, 0, 0));
                    break;
                case 2:
                    backgroundShadeBrush = new SolidColorBrush(Color.FromArgb(0x55, 0, 0, 0));
                    break;
                case 3:
                    backgroundShadeBrush = new SolidColorBrush(Color.FromArgb(0x55, 0, 0, 0));
                    break;
            }
            InstallingOverlayFlyout_Border.Background = backgroundShadeBrush;
            TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.Normal, this);

            bool playMusic = false;
            foreach (AddonFile af in ADDONFILES_TO_INSTALL)
            {
                if (af.ALOTVersion > 0 || af.MEUITM)
                {
                    playMusic = true;
                    break;
                }
            }

            MusicPaused = true; //will set to false if music is to start playing

            //Set music
            if (playMusic)
            {
                string musfile = GetMusicDirectory() + "me" + game + ".ogg";
                if (File.Exists(musfile))
                {
                    MusicIsPlaying = true;
                    waveOut = new WaveOut();
                    vorbisStream = new NAudio.Vorbis.VorbisWaveReader(musfile);
                    LoopStream ls = new LoopStream(vorbisStream);
                    fadeoutProvider = new FadeInOutSampleProvider(ls.ToSampleProvider());
                    try
                    {
                        waveOut.Init(fadeoutProvider);
                        InstallingOverlay_MusicButton.Visibility = Visibility.Visible;
                        if (Utilities.GetRegistrySettingBool(SETTINGSTR_SOUND) ?? true)
                        {
                            MusicButtonIcon.Kind = MahApps.Metro.IconPacks.PackIconModernKind.Sound3;
                            fadeoutProvider.BeginFadeIn(2000);
                            waveOut.Play();
                            MusicPaused = false;
                        }
                        else
                        {
                            MusicButtonIcon.Kind = MahApps.Metro.IconPacks.PackIconModernKind.SoundMute;
                            waveOut.Pause();
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error("Error initializing audio device and UI:" + e.Message);
                        InstallingOverlay_MusicButton.Visibility = Visibility.Collapsed;
                        MusicPaused = true;
                        MusicIsPlaying = false;
                    }
                }
                else
                {
                    InstallingOverlay_MusicButton.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                InstallingOverlay_MusicButton.Visibility = Visibility.Collapsed;
            }
            REPACK_GAME_FILES = ((INSTALLING_THREAD_GAME == 2 && Checkbox_RepackME2GameFiles.IsChecked.Value && ME2_REPACK_MANIFEST_ENABLED) || (INSTALLING_THREAD_GAME == 3 && Checkbox_RepackME3GameFiles.IsChecked.Value && ME3_REPACK_MANIFEST_ENABLED));
            Log.Information("Repack option enabled: " + REPACK_GAME_FILES);
            SetInstallFlyoutState(true);

            //Load Tips
            TIPS_LIST = new List<string>();
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AlotAddOnGUI.ui.installtips.xml"))
            using (StreamReader reader = new StreamReader(stream))
            {
                string result = reader.ReadToEnd();
                try
                {
                    XElement rootElement = XElement.Parse(result);
                    IEnumerable<XElement> xNames;

                    xNames = rootElement.Element("me" + INSTALLING_THREAD_GAME).Descendants("tip");

                    foreach (XElement element in xNames)
                    {
                        TIPS_LIST.Add(element.Value);
                    }
                }
                catch
                {
                    //no tips.
                }
            }
            InstallingOverlay_FullStageOfStageLabel.Visibility = System.Windows.Visibility.Collapsed;
            //InstallingOverlay_OverallProgressLabel.Visibility = System.Windows.Visibility.Visible;
            InstallingOverlay_Tip.Text = "";
            tipticker = new System.Windows.Threading.DispatcherTimer();
            tipticker.Tick += newTipTimer_Tick;
            tipticker.Interval = new TimeSpan(0, 0, 20);
            tipticker.Start();
            newTipTimer_Tick(null, null);
            InstallWorker.RunWorkerAsync(getOutputDir(INSTALLING_THREAD_GAME));
        }

        private async void InstallWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            ThreadCommand tc = (ThreadCommand)e.UserState;
            switch (tc.Command)
            {
                case UPDATE_STAGE_OF_STAGE_LABEL:
                    InstallingOverlay_StageOfStageLabel.Text = "Stage " + CURRENT_STAGE_NUM + " of " + STAGE_COUNT;
                    break;
                case SHOW_ALL_STAGE_LABELS:
                    InstallingOverlay_StageOfStageLabel.Visibility = System.Windows.Visibility.Visible;
                    InstallingOverlay_OverallProgressLabel.Visibility = System.Windows.Visibility.Visible;
                    InstallingOverlay_FullStageOfStageLabel.Visibility = Visibility.Visible;
                    break;
                case HIDE_STAGE_OF_STAGE_LABEL:
                    InstallingOverlay_FullStageOfStageLabel.Visibility = System.Windows.Visibility.Collapsed;
                    break;
                case HIDE_ALL_STAGE_LABELS:
                    InstallingOverlay_StageOfStageLabel.Visibility = System.Windows.Visibility.Collapsed;
                    //InstallingOverlay_OverallLabel.Visibility = System.Windows.Visibility.Collapsed;
                    InstallingOverlay_FullStageOfStageLabel.Visibility = Visibility.Collapsed;
                    break;
                case SHOW_ORIGIN_FLYOUT:
                    var uriSource = new Uri(@"images/origin/me" + tc.Data + "update.png", UriKind.Relative);
                    OriginWarning_Image.Source = new BitmapImage(uriSource);
                    OriginWarningFlyout.IsOpen = true;
                    break;
                case UPDATE_OVERALL_TASK:
                    InstallingOverlay_TopLabel.Text = (string)tc.Data;
                    break;
                case UPDATE_CURRENTTASK_NAME:
                    InstallingOverlay_BottomLabel.Text = (string)tc.Data;
                    break;
                case HIDE_TIPS:
                    InstallingOverlay_Tip.Visibility = Visibility.Collapsed;
                    break;
                case UPDATE_CURRENT_STAGE_PROGRESS:
                    int oldTaskProgress = CurrentTaskPercent;
                    if (tc.Data is string str)
                    {
                        CurrentTaskPercent = Convert.ToInt32(str);
                    }
                    else
                    {
                        CurrentTaskPercent = (int)tc.Data;
                    }
                    if (((CurrentTaskPercent != oldTaskProgress && CurrentTaskPercent > 0) || CurrentTaskPercent == 0) && CurrentTaskPercent <= 100)
                    {
                        InstallingOverlay_BottomLabel.Text = CurrentTask + " " + CurrentTaskPercent + "%";
                        if (CURRENT_STAGE_NUM > 0)
                        {
                            int progressval = ProgressWeightPercentages.SubmitProgress(CURRENT_STAGE_NUM, CurrentTaskPercent);
                            InstallingOverlay_OverallProgressLabel.Text = "(" + progressval.ToString() + "%)";
                            TaskbarManager.Instance.SetProgressValue(progressval, 100);
                        }
                    }
                    break;
                case SET_OVERALL_PROGRESS:
                    //InstallingOverlay_BottomLabel.Text = CurrentTask + " " + CurrentTaskPercent + "%";
                    //TaskbarManager.Instance.SetProgressValue((int)tc.Data, 100);
                    int progress = ProgressWeightPercentages.GetOverallProgress();
                    InstallingOverlay_OverallProgressLabel.Text = "(" + progress.ToString() + "%)";
                    TaskbarManager.Instance.SetProgressValue(progress, 100);
                    break;
                case UPDATE_PROGRESSBAR_INDETERMINATE:
                    //Install_ProgressBar.IsIndeterminate = (bool)tc.Data;
                    break;
                case ERROR_OCCURED:
                    Build_ProgressBar.IsIndeterminate = false;
                    ProgressBarValue = 0;
                    //await this.ShowMessageAsync("Error building Addon MEM Package", "An error occured building the addon. The logs will provide more information. The error message given is:\n" + (string)tc.Data);
                    break;
                case SHOW_DIALOG:
                    KeyValuePair<string, string> messageStr = (KeyValuePair<string, string>)tc.Data;
                    await this.ShowMessageAsync(messageStr.Key, messageStr.Value);
                    break;
                default:
                    Debug.WriteLine("Unknown threadcommand command: " + tc.Command);
                    break;
            }
        }

        private void InstallALOTContextBased(object sender, DoWorkEventArgs e)
        {
            CURRENT_STAGE_CONTEXT = null;
            CURRENT_STAGE_NUM = 0;
            STAGE_COUNT = 0;
            STAGE_DONE_REACHED = false;
            CurrentTask = "";
            Log.Information("InstallWorker Thread starting for ME" + INSTALLING_THREAD_GAME);
            using (var md5 = MD5.Create())
            {
                try
                {
                    using (var stream = File.OpenRead(Utilities.GetGameEXEPath(INSTALLING_THREAD_GAME)))
                    {
                        byte[] hashbytes = md5.ComputeHash(stream);
                        string hashstr = BitConverter.ToString(hashbytes).Replace("-", "").ToLower();
                        Utilities.LogGameSourceByHash(INSTALLING_THREAD_GAME, hashstr);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("Could not hash executable: " + ex.Message);
                }
            }
            Analytics.TrackEvent("Started installation for ME" + INSTALLING_THREAD_GAME);
            ProgressWeightPercentages.ClearTasks();
            ALOTVersionInfo versionInfo = Utilities.GetInstalledALOTInfo(INSTALLING_THREAD_GAME);
            TELEMETRY_IS_FULL_NEW_INSTALL = versionInfo == null;
            Log.Information("Setting biogame directory to read-write");
            string biogamepath = Utilities.GetGamePath(INSTALLING_THREAD_GAME) + "\\BIOGame";
            if (!Directory.Exists(biogamepath))
            {
                e.Result = RESULT_BIOGAME_MISSING;
                return;
            }
            bool filesSetRWOK = Utilities.MakeAllFilesInDirReadWrite(Utilities.GetGamePath(INSTALLING_THREAD_GAME) + "\\BIOGame");
            if (!filesSetRWOK)
            {
                e.Result = RESULT_SET_READWRITE_FAILED;
                return;
            }
            Log.Information("Files being installed in this installation session:");
            AddonFile alotMainFile = null;
            AddonFile alotUpdateFile = null;
            AddonFile meuitmFile = null;
            foreach (AddonFile af in ADDONFILES_TO_INSTALL)
            {
                Log.Information(" - " + af.FriendlyName);
                if (af.MEUITM)
                {
                    meuitmFile = af;
                    Log.Information("InstallWorker: We are installing MEUITM v" + af.MEUITMVer + " in this pass.");
                }
                if (af.ALOTVersion > 0)
                {
                    alotMainFile = af;
                    Log.Information("InstallWorker: We are installing ALOT v" + af.ALOTVersion + " in this pass.");
                }
                if (af.ALOTUpdateVersion > 0)
                {
                    alotUpdateFile = af;
                    Log.Information("InstallWorker: We are installing ALOT Update v" + af.ALOTUpdateVersion + " in this pass.");
                }
            }

            string primary = "";
            if (alotMainFile != null)
            {
                if (alotUpdateFile != null)
                {
                    //ALOT 6.3
                    primary = "ALOT " + alotMainFile.ALOTVersion + "." + alotUpdateFile.ALOTUpdateVersion;
                }
                else
                {
                    //ALOT 6.0
                    primary = "ALOT " + alotMainFile.ALOTVersion + ".0";
                }
            }
            else if (alotUpdateFile != null)
            {
                if (versionInfo != null)
                {
                    //this case should not be reached
                    primary = "ALOT " + versionInfo.ALOTVER + "." + alotUpdateFile.ALOTUpdateVersion + " update";
                }
                else
                {
                    primary = "ALOT for ME" + INSTALLING_THREAD_GAME + " update " + alotUpdateFile.ALOTUpdateVersion;
                }
            }

            if (meuitmFile != null)
            {
                if (primary != "")
                {
                    primary += " & MEUITM";
                }
                else
                {
                    primary = "MEUITM";
                }
            }

            if (primary == "")
            {
                primary = "texture mods";
            }


            MAINTASK_TEXT = "Installing " + primary + " for Mass Effect" + GetGameNumberSuffix(INSTALLING_THREAD_GAME);
            InstallWorker.ReportProgress(completed, new ThreadCommand(UPDATE_OVERALL_TASK, MAINTASK_TEXT));
            string exe = BINARY_DIRECTORY + MEM_EXE_NAME;
            string args = "";
            int processResult = 0;
            if (versionInfo == null)
            {
                Log.Information("Checking for previously installed ALOT files...");
                CurrentTask = "Checking existing files for ALOT marker";
                InstallWorker.ReportProgress(0, new ThreadCommand(UPDATE_CURRENTTASK_NAME, CurrentTask));
                args = "--check-for-markers --gameid " + INSTALLING_THREAD_GAME + " --ipc";
                RunAndTimeMEMContextBased_Install(exe, args, InstallWorker, false);
                processResult = BACKGROUND_MEM_PROCESS.ExitCode ?? 1;
                if (processResult != 0 || BACKGROUND_MEM_PROCESS_ERRORS.Count > 0)
                {
                    Log.Error("Previous markers were found, or MEM crashed. Aborting installation.");
                    e.Result = RESULT_MARKERCHECK_FAILED;
                    InstallWorker.ReportProgress(0, new ThreadCommand(HIDE_TIPS));
                    return;
                }

                if (INSTALLING_THREAD_GAME == 2 || INSTALLING_THREAD_GAME == 3)
                {
                    string dlcPath = Path.Combine(Utilities.GetGamePath(INSTALLING_THREAD_GAME), "BIOGame", "DLC");
                    if (Directory.Exists(dlcPath) && ((ME2DLCRequiringTextureExportFixes != null && INSTALLING_THREAD_GAME == 2) || (ME3DLCRequiringTextureExportFixes != null && INSTALLING_THREAD_GAME == 3)))
                    {
                        var allfolders = Directory.EnumerateDirectories(dlcPath).Select(x => Path.GetFileName(x).ToUpperInvariant()).ToList();
                        var directories = (INSTALLING_THREAD_GAME == 2 ? ME2DLCRequiringTextureExportFixes : ME3DLCRequiringTextureExportFixes).Intersect(allfolders).ToList();
                        foreach (string dir in directories)
                        {
                            CurrentTask = "Fixing texture exports in " + dir;
                            Log.Information("DLC marked for texture exports fix by MEM: " + dir);
                            InstallWorker.ReportProgress(0, new ThreadCommand(UPDATE_CURRENTTASK_NAME, CurrentTask));
                            args = "--fix-textures-property --gameid " + INSTALLING_THREAD_GAME + " --filter \"" + dir + "\" --ipc";
                            RunAndTimeMEMContextBased_Install(exe, args, InstallWorker, false);
                            processResult = BACKGROUND_MEM_PROCESS.ExitCode ?? 1;
                            if (processResult != 0 || BACKGROUND_MEM_PROCESS_ERRORS.Count > 0)
                            {
                                Log.Error("Fixing texture exports failed. Aborting installation.");
                                e.Result = RESULT_TEXTURE_EXPORT_FIX_FAILED;
                                InstallWorker.ReportProgress(0, new ThreadCommand(HIDE_TIPS));
                                return;
                            }
                        }
                    }
                }
            }

            InstallWorker.ReportProgress(completed, new ThreadCommand(SHOW_ALL_STAGE_LABELS));

            int overallProgress = 0;
            stopwatch = Stopwatch.StartNew();
            Log.Information("InstallWorker(): Running MassEffectModderNoGui");
            CurrentTaskPercent = -1;
            string outputDir = getOutputDir(INSTALLING_THREAD_GAME, false);
            args = "--install-mods --gameid " + INSTALLING_THREAD_GAME + " --input \"" + (CustomMEMInstallSource ?? outputDir) + "\" --ipc";
            if (CustomMEMInstallSource == null)
            {
                args += " --alot-mode --verify";
            }
            if (REPACK_GAME_FILES)
            {
                args += " --repack-mode";
            }

            //Comment the following 2 lines and uncomment the next 3 to skip installation step and simulate OK
            RunAndTimeMEMContextBased_Install(exe, args, InstallWorker, true);
            processResult = BACKGROUND_MEM_PROCESS.ExitCode ?? 1;

            //MEM_INSTALL_TIME_SECONDS = 61;
            //processResult = 0;
            //STAGE_DONE_REACHED = true;

            if (!STAGE_DONE_REACHED)
            {
                if (processResult != 0)
                {
                    Log.Error("MassEffectModderNoGui process exited with non-zero code: " + processResult);
                    Log.Warning("MEMNoGui exited in stage context: " + CURRENT_STAGE_CONTEXT);
                    Stage stage = ProgressWeightPercentages.Stages.Where(x => x.StageName == CURRENT_STAGE_CONTEXT).FirstOrDefault();
                    if (stage != null)
                    {
                        //check if it has background errors (handled errors)
                        if (BACKGROUND_MEM_PROCESS_ERRORS.Count > 0 && stage.FailureInfos.Count() > 1)// has more than just standard crash failure info
                        {
                            StageFailure sf = stage.FailureInfos.Where(x => BACKGROUND_MEM_PROCESS_ERRORS[0] == x.FailureIPCTrigger).FirstOrDefault();
                            if (sf != null)
                            {
                                //we hit a known failure trigger
                                e.Result = sf.FailureResultCode;
                            }
                            else
                            {
                                Log.Error("BACKGROUND_MEM_PROCESS_ERRORS contains an unknown item: " + BACKGROUND_MEM_PROCESS_ERRORS[0]);
                            }
                        }
                        else if (BACKGROUND_MEM_PROCESS_ERRORS.Count > 0)
                        {
                            //has output errors but we have no handlers for this trigger
                            Log.Error("BACKGROUND_MEM_PROCESS_ERRORS contains an unknown item: " + BACKGROUND_MEM_PROCESS_ERRORS[0]);
                            e.Result = stage.getDefaultFailure().FailureResultCode;
                        }
                        else
                        {
                            e.Result = stage.getDefaultFailure().FailureResultCode;
                        }
                    }
                    else
                    {
                        //MEM exited without handled error
                        if (stage != null)
                        {
                            e.Result = stage.getDefaultFailure().FailureResultCode;
                        }
                        else
                        {
                            e.Result = RESULT_UNKNOWN_ERROR;
                        }
                    }
                }
                else
                {
                    Log.Error("MEM exited during unknown stage context with code 0: " + STAGE_CONTEXT);
                    e.Result = RESULT_UNKNOWN_ERROR;
                }
                InstallWorker.ReportProgress(0, new ThreadCommand(HIDE_TIPS));
                return;
            }

            //Exited OK, continue installation.


            //Apply ALOT-verified Mod Manager mods that we support.
            InstallWorker.ReportProgress(0, new ThreadCommand(HIDE_STAGE_OF_STAGE_LABEL));


            string gameDirectory = Utilities.GetGamePath(INSTALLING_THREAD_GAME);
            foreach (var modAddon in ADDONFILES_TO_BUILD.Where(x => x.IsModManagerMod))
            {
                InstallWorker.ReportProgress(0, new ThreadCommand(SET_OVERALL_PROGRESS, 0));
                InstallWorker.ReportProgress(0, new ThreadCommand(UPDATE_OVERALL_TASK, "Installing " + modAddon.FriendlyName));

                void progressUpdate(int percent)
                {
                    InstallWorker.ReportProgress(0, new ThreadCommand(UPDATE_CURRENTTASK_NAME, "Extracting " + percent + "%"));
                }
                var stagingPath = Directory.CreateDirectory(Path.Combine(gameDirectory, "ModExtractStaging")).FullName;

                //progress, overwrite, recursive
                var extractionProgressApp = Run7zWithProgressCallback($"x -bsp2 \"{modAddon.GetFile()}\" -aoa -r -o\"{stagingPath}\"", modAddon, progressUpdate);
                while (extractionProgressApp.State == AppState.Running)
                {
                    Thread.Sleep(100); //Hacky... but it works
                }
                InstallWorker.ReportProgress(0, new ThreadCommand(UPDATE_CURRENTTASK_NAME, "Installing files"));

                foreach (var extractionRedirect in modAddon.ExtractionRedirects)
                {
                    if (extractionRedirect.OptionalRequiredDLC != null)
                    {
                        //Check DLC directory for requirement
                        if (INSTALLING_THREAD_GAME == 1 && !Directory.Exists(Path.Combine(gameDirectory, "DLC", extractionRedirect.OptionalRequiredDLC)))
                        {
                            continue;
                        }
                        if ((INSTALLING_THREAD_GAME == 2 || INSTALLING_THREAD_GAME == 3) && !Directory.Exists(Path.Combine(gameDirectory, "BioGame", "DLC", extractionRedirect.OptionalRequiredDLC)))
                        {
                            continue;
                        }
                    }

                    var rootPath = Path.Combine(stagingPath, extractionRedirect.ArchiveRootPath);
                    var filesToMove = Directory.GetFiles(rootPath, "*", SearchOption.AllDirectories);
                    var ingameDestination = Directory.CreateDirectory(Path.Combine(gameDirectory, extractionRedirect.RelativeDestinationDirectory)).FullName;
                    foreach (var file in filesToMove)
                    {
                        string relativePath = file.Substring(rootPath.Length + 1);
                        string finalDestinationPath = Path.Combine(ingameDestination, relativePath);
                        if (File.Exists(finalDestinationPath))
                        {
                            Log.Information("Deleting existing file before move: " + finalDestinationPath);
                            File.Delete(finalDestinationPath);
                        }

                        Log.Information($"Moving staged file into game directory: {file} -> {finalDestinationPath}");
                        Directory.CreateDirectory(Directory.GetParent(finalDestinationPath).FullName);
                        File.Move(file, finalDestinationPath);
                    }

                    //if (extractionRedirect.IsDLC)
                    //{
                    //    //Write a _metacmm.txt file
                    //    var metacmm = Path.Combine(ingameDestination, "_metacmm.txt");
                    //    string contents = $"{extractionRedirect.DLCFriendlyName}\n{extractionRedirect.ModVersion}\nALOT Installer {System.Reflection.Assembly.GetEntryAssembly().GetName().Version}\n{Guid.NewGuid().ToString()}";
                    //    File.WriteAllText(metacmm, contents);
                    //}
                }

                Utilities.DeleteFilesAndFoldersRecursively(stagingPath);
            }

            //Final stages
            InstallWorker.ReportProgress(0, new ThreadCommand(HIDE_ALL_STAGE_LABELS));
            overallProgress = ProgressWeightPercentages.SubmitProgress(CURRENT_STAGE_NUM, 100);
            InstallWorker.ReportProgress(0, new ThreadCommand(SET_OVERALL_PROGRESS, overallProgress));
            InstallWorker.ReportProgress(0, new ThreadCommand(UPDATE_OVERALL_TASK, "Finishing installation"));
            //things like soft shadows, reshade
            bool hasSoftShadowsMEUITM = false;
            foreach (AddonFile af in ADDONFILES_TO_INSTALL)
            {
                if (af.CopyFiles != null)
                {
                    foreach (CopyFile cf in af.CopyFiles)
                    {
                        if (cf.IsSelectedForInstallation())
                        {
                            CurrentTask = "Installing non-texture file modifications for mods";
                            InstallWorker.ReportProgress(0, new ThreadCommand(UPDATE_CURRENTTASK_NAME, CurrentTask));
                            string stagedPath = getOutputDir(INSTALLING_THREAD_GAME) + af.BuildID + "_" + cf.ID + "_" + Path.GetFileName(cf.InArchivePath);
                            string installationPath = Path.Combine(Utilities.GetGamePath(INSTALLING_THREAD_GAME), cf.GameDestinationPath);
                            File.Copy(stagedPath, installationPath, true);
                            Log.Information("Installed copyfile: " + cf.ChoiceTitle + ", " + stagedPath + " to " + installationPath);
                        }
                    }
                }

                if (af.ZipFiles != null)
                {
                    foreach (ZipFile zf in af.ZipFiles)
                    {
                        if (zf.IsSelectedForInstallation())
                        {
                            CurrentTask = "Installing non-texture file modifications for mods";
                            InstallWorker.ReportProgress(0, new ThreadCommand(UPDATE_CURRENTTASK_NAME, CurrentTask));
                            string stagedPath = getOutputDir(INSTALLING_THREAD_GAME) + af.BuildID + "_" + zf.ID + "_" + Path.GetFileName(zf.InArchivePath);
                            string installationPath = Path.Combine(Utilities.GetGamePath(INSTALLING_THREAD_GAME), zf.GameDestinationPath);

                            string path = BINARY_DIRECTORY + "7z.exe";
                            string extractargs = "x \"" + stagedPath + "\" -aoa -r -o\"" + installationPath + "\"";
                            int extractcode = Utilities.runProcess(path, extractargs);
                            if (extractcode == 0)
                            {
                                Log.Information("Installed zipfile: " + zf.ChoiceTitle + ", " + stagedPath + " to " + installationPath);
                            }
                            else
                            {
                                Log.Error("Extraction of " + zf.ChoiceTitle + " failed with code " + extractcode);
                            }
                            if (INSTALLING_THREAD_GAME == 1 && zf.DeleteShaders)
                            {
                                string documents = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                                string localusershaderscache = Path.Combine(documents, @"BioWare\Mass Effect\Published\CookedPC\LocalShaderCache-PC-D3D-SM3.upk");
                                if (File.Exists(localusershaderscache))
                                {
                                    File.Delete(localusershaderscache);
                                    Log.Information("Deleted user localshadercache: " + localusershaderscache);
                                }
                                else
                                {
                                    Log.Warning("unable to delete user local shadercache, it does not exist: " + localusershaderscache);
                                }
                                string gamelocalshadercache = Path.Combine(Utilities.GetGamePath(INSTALLING_THREAD_GAME), @"BioGame\CookedPC\LocalShaderCache-PC-D3D-SM3.upk");
                                if (File.Exists(gamelocalshadercache))
                                {
                                    File.Delete(gamelocalshadercache);
                                    Log.Information("Deleted game localshadercache: " + gamelocalshadercache);
                                }
                                else
                                {
                                    Log.Warning("Unable to delete game localshadercache, it does not exist: " + gamelocalshadercache);
                                }
                            }

                            //MEUITM SPECIFIC FIX
                            //REMOVE ONCE THIS IS FIXED IN FUTURE MEUITM
                            if (af == meuitmFile && !zf.MEUITMSoftShadows)
                            {
                                //reshade
                                if (File.Exists(Utilities.GetGamePath(INSTALLING_THREAD_GAME) + "\\Binaries\\d3d9.ini"))
                                {
                                    try
                                    {
                                        IniFile shaderConf = new IniFile(Utilities.GetGamePath(INSTALLING_THREAD_GAME) + "\\Binaries\\d3d9.ini");
                                        shaderConf.Write("TextureSearchPaths", Utilities.GetGamePath(INSTALLING_THREAD_GAME) + "\\Binaries\\reshade-shaders\\Textures", "GENERAL");
                                        shaderConf.Write("EffectSearchPaths", Utilities.GetGamePath(INSTALLING_THREAD_GAME) + "\\Binaries\\reshade-shaders\\Shaders", "GENERAL");
                                        shaderConf.Write("PresetFiles", Utilities.GetGamePath(INSTALLING_THREAD_GAME) + "\\Binaries\\MassEffect.ini", "GENERAL");
                                        Log.Information("Corrected MEUITM shader ini");
                                    }
                                    catch (Exception ex)
                                    {
                                        Log.Error("Error fixing MEUITM shader ini: " + ex.Message);
                                    }
                                }
                            }

                            if (zf.MEUITMSoftShadows)
                            {
                                hasSoftShadowsMEUITM = true;
                            }
                        }
                    }
                }
            }
            //Apply LOD
            Log.Information("Updating LOD information");
            CurrentTask = "Updating Mass Effect" + GetGameNumberSuffix(INSTALLING_THREAD_GAME) + "'s graphics settings";
            InstallWorker.ReportProgress(0, new ThreadCommand(UPDATE_CURRENTTASK_NAME, CurrentTask));

            args = "--apply-lods-gfx --gameid " + INSTALLING_THREAD_GAME;
            if (hasSoftShadowsMEUITM)
            {
                args += " --soft-shadows-mode --meuitm-mode";
            }
            RunAndTimeMEMContextBased_Install(exe, args, InstallWorker, false);
            processResult = BACKGROUND_MEM_PROCESS.ExitCode ?? 6000;
            if (processResult != 0)
            {
                Log.Error("Applying lods failed, return code was not 0: " + processResult);
                Log.Error("Graphics settings may not have been applied to game config files.");
            }

            if (INSTALLING_THREAD_GAME == 1)
            {
                //Apply ME1 LAA
                CurrentTask = "Installing fixes for Mass Effect";
                InstallWorker.ReportProgress(0, new ThreadCommand(UPDATE_CURRENTTASK_NAME, CurrentTask));

                args = "--apply-me1-laa";
                RunAndTimeMEMContextBased_Install(exe, args, InstallWorker, false);
                processResult = BACKGROUND_MEM_PROCESS.ExitCode ?? 1;
                if (processResult != 0)
                {
                    Log.Error("Error setting ME1 to large address aware/bootable: " + processResult);
                    e.Result = RESULT_ME1LAA_FAILED;
                    return;
                }
                Utilities.RemoveRunAsAdminXPSP3FromME1();
            }
            else if (INSTALLING_THREAD_GAME == 2)
            {
                if (File.Exists("EXPERIMENTAL_FIX_ME2CONTROLLER"))
                {
                    string gamePath = Path.Combine(Utilities.GetGamePath(2));
                    string biopcharPath = Path.Combine(gamePath, "BIOGame", "CookedPC", "BioP_Char.pcc");
                    if (File.Exists(biopcharPath))
                    {
                        Utilities.CompactFile(biopcharPath);
                        Utilities.TagWithALOTMarker(biopcharPath);
                        Analytics.TrackEvent("Applied ME2Controller compaction fix");
                    }
                }
            }
            //else if (INSTALLING_THREAD_GAME == 3)
            //{
            //    string dlcPath = Path.Combine(Utilities.GetGamePath(3), "BIOGame", "DLC");

            //    //Fix for PEOM Hammer 505
            //    if (File.Exists("EXPERIMENTAL_FIX_PEOM"))
            //    {
            //        var peomHammer505 = Path.Combine(dlcPath, "DLC_CON_PEOM", "CookedPCConsole", "BioD_PEOM_505_HammerAssault.pcc");
            //        if (File.Exists(peomHammer505))
            //        {
            //            Log.Information("Applying fix to Priority Earth: Overhaul Mod");
            //            CurrentTask = "Applying fix to Priority Earth: Overhaul Mod";
            //            InstallWorker.ReportProgress(0, new ThreadCommand(UPDATE_CURRENTTASK_NAME, CurrentTask));
            //            //This file needs recompacted to fix unknown engine issue due to MEM modifications to file. Not sure why
            //            //This might not actually work, it seems to only work for some users
            //            Utilities.CompactFile(peomHammer505);
            //            Utilities.TagWithALOTMarker(peomHammer505);
            //            Analytics.TrackEvent("Applied PEOM compaction fix");
            //        }
            //    }
            //}
            Utilities.TurnOffOriginAutoUpdate();

            //Create/Update Marker File
            bool showMarkerFailedMessage = false;
            if (CustomMEMInstallSource == null)
            {
                int meuitmFlag = (meuitmFile != null) ? meuitmFile.MEUITMVer : (versionInfo != null ? versionInfo.MEUITMVER : 0);
                short alotMainVersionFlag = (alotMainFile != null) ? alotMainFile.ALOTVersion : (versionInfo != null ? versionInfo.ALOTVER : (short)0); //we should not see it write 0... hopefully

                //Update Marker
                byte updateVersion = 0;
                if (alotUpdateFile != null)
                {
                    updateVersion = alotUpdateFile.ALOTUpdateVersion;
                }
                else
                {
                    updateVersion = versionInfo != null ? versionInfo.ALOTUPDATEVER : (byte)0;
                }

                //Write Marker
                ALOTVersionInfo newVersion = new ALOTVersionInfo(alotMainVersionFlag, updateVersion, 0, meuitmFlag);
                Log.Information("Writing or updating MEMI marker with info: " + newVersion.ToString());
                try
                {
                    Utilities.CreateMarkerFile(INSTALLING_THREAD_GAME, newVersion);
                    ALOTVersionInfo test = Utilities.GetInstalledALOTInfo(INSTALLING_THREAD_GAME);
                    if (test == null || test.ALOTVER != newVersion.ALOTVER || test.ALOTUPDATEVER != newVersion.ALOTUPDATEVER || test.MEUITMVER != newVersion.MEUITMVER)
                    {
                        //Marker file written was bad
                        Log.Error("Marker file was not properly written!");
                        if (test == null)
                        {
                            Log.Error("Marker file does not indicate anything was installed.");
                        }
                        else
                        {
                            if (test.ALOTVER != newVersion.ALOTVER)
                            {
                                Log.Error("Marker file does not show that ALOT was installed, but we detect some version was installed.");
                            }
                            if (test.ALOTUPDATEVER != newVersion.ALOTUPDATEVER)
                            {
                                Log.Error("Marker file does not show that ALOT update was applied or installed to our current version");
                            }
                            if (test.MEUITMVER != newVersion.MEUITMVER)
                            {
                                Log.Error("Marker file does not show that MEUITM was applied or installed to our current installation when it should have been");
                            }
                        }
                        showMarkerFailedMessage = true;
                    }
                    else
                    {
                        Log.Information("Reading information back from disk, should match above: " + test.ToString());
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("Marker file was unable to be written due to an exception: " + ex.Message);
                    Log.Error("An error like this occuring could indicate significant other issues");
                    Crashes.TrackError(ex);
                }
            }
            //Install Binkw32
            Utilities.InstallBinkw32Bypass(INSTALLING_THREAD_GAME);
            if (INSTALLING_THREAD_GAME == 3)
            {
                Utilities.InstallME3ASIs();
            }

            if (INSTALLING_THREAD_GAME == 1 && ADDONFILES_TO_BUILD.Any(x => x.InstallME1DLCASI))
            {
                //Install ME1 DLC enabler
                Log.Information("Installing ME1 DLC enabler asi...");
                try
                {
                    string path = Utilities.GetGamePath(1);
                    path = Directory.CreateDirectory(Path.Combine(path, "Binaries", "asi")).FullName;
                    path = Path.Combine(path, "ME1-DLC-ModEnabler-v1.0.asi");
                    File.WriteAllBytes(path, AlotAddOnGUI.Properties.Resources.ME1_DLC_ModEnabler_v1_0);
                    Log.Information("Installed ME1-DLC-ModEnabler-v1.0.asi");
                }
                catch (Exception ex)
                {
                    Log.Error("Failed to ME1 DLC Enable ASI! " + ex.Message);
                    Crashes.TrackError(ex);
                }
            }



            //If MAIN alot file is here, move it back to downloaded_mods
            if (alotMainFile != null && alotMainFile.UnpackedSingleFilename != null)
            {
                //ALOT was just installed. We are going to move it back to mods folder
                string extractedName = alotMainFile.UnpackedSingleFilename;
                string source = getOutputDir(INSTALLING_THREAD_GAME) + "000_" + extractedName;
                string dest = DOWNLOADED_MODS_DIRECTORY + "\\" + extractedName;
                if (Path.GetPathRoot(source) == Path.GetPathRoot(dest))
                {
                    Log.Information("ALOT MAIN FILE - Unpacked - moving to texture library from install dir: " + extractedName);
                    if (File.Exists(source))
                    {
                        try
                        {
                            if (File.Exists(dest))
                            {
                                File.Delete(dest);
                            }
                            File.Move(source, dest);
                            Log.Information("Moved main alot file back to import library " + DOWNLOADED_MODS_DIRECTORY);
                            //Delete original
                            dest = DOWNLOADED_MODS_DIRECTORY + "\\" + alotMainFile.Filename;
                            if (File.Exists(dest) && Path.GetExtension(source) != Path.GetExtension(dest)) //do not delete if it is same extension as it's mem and mem not 7z and mem
                            {
                                Log.Information("Deleting original alot archive file from import library");
                                File.Delete(dest);
                                Log.Information("Deleted original alot archive file from import library");
                            }
                            if (alotMainFile != null)
                            {
                                alotMainFile.Staged = false;
                            }

                        }
                        catch (Exception ex)
                        {
                            Log.Error("Exception attempting to move file back! " + ex.Message);
                            Log.Error("Skipping moving file back.");
                        }
                    }
                    else
                    {
                        Log.Error("ALOT MAIN FILE - Unpacked - does not match the singlefilename! Not moving back. " + extractedName);
                    }
                }
                else
                {
                    Log.Information("ALOT main was copied from import library on another partition or network share. Not moving back.");
                }
            }

            if (showMarkerFailedMessage)
            {
                KeyValuePair<string, string> dialog = new KeyValuePair<string, string>("Marker file not properly written", "The 'MEMI Marker' file that ALOT Installer uses to track the installation status of ALOT/MEUITM could not be written. The installation status of ALOT/MEUITM for this game will not be accurate, however installation has completed. Please check the logs for more information.");
                InstallWorker.ReportProgress(0, new ThreadCommand(SHOW_DIALOG, dialog));
            }

            InstallWorker.ReportProgress(0, new ThreadCommand(HIDE_ALL_STAGE_LABELS));

            string taskString = "Installation of " + primary + " for Mass Effect" + GetGameNumberSuffix(INSTALLING_THREAD_GAME);
            InstallWorker.ReportProgress(0, new ThreadCommand(UPDATE_OVERALL_TASK, taskString));
            InstallWorker.ReportProgress(0, new ThreadCommand(UPDATE_CURRENTTASK_NAME, "has completed"));
            if (INSTALLING_THREAD_GAME == 1 || INSTALLING_THREAD_GAME == 2)
            {
                //Check if origin
                string originTouchupFile = Utilities.GetGamePath(INSTALLING_THREAD_GAME) + "\\__Installer\\Touchup.exe";
                if (File.Exists(originTouchupFile))
                {
                    //origin based
                    InstallWorker.ReportProgress(0, new ThreadCommand(SHOW_ORIGIN_FLYOUT, INSTALLING_THREAD_GAME));
                }
            }
            Analytics.TrackEvent("Finished installation for ME" + INSTALLING_THREAD_GAME);

            e.Result = INSTALL_OK;
        }

        private void InstallCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            int telemetryfailedcode = 1; //will be set 
            InstallingOverlay_MusicButton.Visibility = Visibility.Collapsed;
            if (MusicIsPlaying)
            {
                MusicIsPlaying = false;
                if (MusicPaused)
                {
                    waveOut.Stop();
                    waveOut.Dispose();
                }
                else
                {
                    fadeoutProvider.BeginFadeOut(3000);
                }
            }

            WARN_USER_OF_EXIT = false;
            Log.Information("InstallCompleted() with code " + e.Result);
            InstallingOverlay_OverallProgressLabel.Visibility = System.Windows.Visibility.Collapsed;

            tipticker.Stop();
            InstallingOverlay_FullStageOfStageLabel.Visibility = System.Windows.Visibility.Collapsed;
            Button_InstallViewLogs.Visibility = System.Windows.Visibility.Collapsed;
            switch (INSTALLING_THREAD_GAME)
            {
                case 1:
                    CURRENTLY_INSTALLED_ME1_ALOT_INFO = Utilities.GetInstalledALOTInfo(1);
                    break;
                case 2:
                    CURRENTLY_INSTALLED_ME2_ALOT_INFO = Utilities.GetInstalledALOTInfo(2);
                    break;
                case 3:
                    CURRENTLY_INSTALLED_ME3_ALOT_INFO = Utilities.GetInstalledALOTInfo(3);
                    break;
            }

            if (e.Result != null)
            {
                int result = (int)e.Result;
                string gameName = "Mass Effect" + GetGameNumberSuffix(INSTALLING_THREAD_GAME);
                if (result == INSTALL_OK)
                {
                    telemetryfailedcode = 0;
                }
                else
                {
                    telemetryfailedcode = result;
                }
                switch (result)
                {
                    case INSTALL_OK:
                        {
                            var uriSource = new Uri(@"images/greencheck_large.png", UriKind.Relative);
                            Installing_Checkmark.Source = new BitmapImage(uriSource);
                            Log.Information("Installation result: OK");
                            HeaderLabel.Text = "Installation has completed.";
                            AddonFilesLabel.Text = "Thanks for using ALOT Installer.";
                            InstallingOverlay_Tip.Text = "Do not install any new DLC or mods that add/replace pcc, sfm, or upk files from here on out - doing so will break your game!";
                            break;
                        }
                    case RESULT_ME1LAA_FAILED:
                        {
                            InstallingOverlay_TopLabel.Text = "Failed to set Mass Effect to LAA";
                            InstallingOverlay_BottomLabel.Text = "Check the logs for details";
                            HeaderLabel.Text = "ME1 LAA fix failed. ME1 may be unstable.";
                            break;
                        }
                    case RESULT_BIOGAME_MISSING:
                        {
                            InstallingOverlay_TopLabel.Text = "BIOGame directory is missing";
                            InstallingOverlay_BottomLabel.Text = "Game needs to be reinstalled, see logs";
                            HeaderLabel.Text = "BIOGame directory is missing. This means the installation is completely unusable.\nCheck logs for more information about this.";
                            break;
                        }
                    case RESULT_SET_READWRITE_FAILED:
                        {
                            InstallingOverlay_TopLabel.Text = "Setting files read/write failed";
                            InstallingOverlay_BottomLabel.Text = "Game may be nested too deep or mod not properly installed";
                            HeaderLabel.Text = "Error occured setting files to read/write - this is typically a sign that a mod is improperly installed\nor the game is nested too deep in the filesystem. This is due to a limitation in the Windows API.\nReview the log for more information on what the problematic files are.";
                            break;
                        }
                    case RESULT_MARKERCHECK_FAILED:
                        {
                            InstallingOverlay_TopLabel.Text = "Previously modified ALOT files found";
                            InstallingOverlay_BottomLabel.Text = "Game needs to be fully deleted and reinstalled";
                            HeaderLabel.Text = "Files that were previously modified by ALOT Installer were detected, even though this is a new install.\nThese files will not work and must be removed by deleting the game and reinstalling.\nThese files may exist due to a failed previous installation.";
                            break;
                        }
                    case RESULT_UNKNOWN_ERROR:
                        {
                            InstallingOverlay_TopLabel.Text = "Unknown error has occured";
                            InstallingOverlay_BottomLabel.Text = "Check installation logs for more details";
                            HeaderLabel.Text = "An unknown error occured during installation.";
                            break;
                        }
                    default:
                        {
                            //this could probably be linq'd
                            StageFailure stagefailure = null;
                            foreach (Stage stage in ProgressWeightPercentages.Stages)
                            {
                                foreach (StageFailure sf in stage.FailureInfos)
                                {
                                    if (sf.FailureResultCode == result)
                                    {
                                        stagefailure = sf;
                                        break;
                                    }
                                }
                                if (stagefailure != null)
                                {
                                    break;
                                }
                            }
                            if (stagefailure != null)
                            {
                                InstallingOverlay_TopLabel.Text = stagefailure.FailureTopText;
                                InstallingOverlay_BottomLabel.Text = stagefailure.FailureBottomText;
                                HeaderLabel.Text = stagefailure.FailureHeaderText;
                            }
                            else
                            {
                                InstallingOverlay_TopLabel.Text = "Unknown error has occured";
                                InstallingOverlay_BottomLabel.Text = "Check installation logs for more details";
                                HeaderLabel.Text = "An unknown error occured during installation.";
                            }
                            break;
                        }
                }
                if (result != INSTALL_OK)
                {
                    InstallingOverlay_Tip.Visibility = Visibility.Collapsed;
                    Button_InstallViewLogs.Visibility = System.Windows.Visibility.Visible;
                    Log.Error("Installation result: Error occured");
                    var uriSource = new Uri(@"images/redx_large.png", UriKind.Relative);
                    Installing_Checkmark.Source = new BitmapImage(uriSource);
                    AddonFilesLabel.Text = "Check the logs for more detailed information.";
                }
            }
            else
            {
                //Null or not OK
                Button_InstallViewLogs.Visibility = System.Windows.Visibility.Visible;
                InstallingOverlay_TopLabel.Text = "Unknown installation error has occured";
                InstallingOverlay_BottomLabel.Text = "Check the logs for details";

                var uriSource = new Uri(@"images/redx_large.png", UriKind.Relative);
                Installing_Checkmark.Source = new BitmapImage(uriSource);
                Log.Error("Installation result: Error occured");
                HeaderLabel.Text = "Installation failed! Check the logs for more detailed information";
                AddonFilesLabel.Text = "Check the logs for more detailed information.";
            }
            int Game = INSTALLING_THREAD_GAME;
            List<AddonFile> addonFilesInstalled = ADDONFILES_TO_INSTALL;
            INSTALLING_THREAD_GAME = 0;
            CustomMEMInstallSource = null;
            ADDONFILES_TO_INSTALL = null;
            CURRENT_STAGE_NUM = 0;
            PreventFileRefresh = false;
            UpdateALOTStatus();
            Installing_Spinner.Visibility = System.Windows.Visibility.Collapsed;
            Installing_Checkmark.Visibility = System.Windows.Visibility.Visible;
            Button_InstallDone.Visibility = System.Windows.Visibility.Visible;
            TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.NoProgress, this);
            TaskbarManager.Instance.SetProgressValue(0, 0);
            var helper = new FlashWindowHelper(System.Windows.Application.Current);
            helper.FlashApplicationWindow();

            //Installation telemetry
            if (TELEMETRY_IS_FULL_NEW_INSTALL)
            {
                BackgroundWorker telemetryworker = new BackgroundWorker();
                telemetryworker.DoWork += (s, events) =>
                {
                    try
                    {
                        var OS = Environment.OSVersion.Version.ToString();
                        var memVersionString = FileVersionInfo.GetVersionInfo(BINARY_DIRECTORY + MEM_EXE_NAME);
                        int memVersionUsed = memVersionString.FileMajorPart;
                        int installerVersionUsed = System.Reflection.Assembly.GetEntryAssembly().GetName().Version.Build;
                        int diskType = -3; //Cannot detect due to OS version is -2
                        var gamePath = Utilities.GetGamePath(Game);
                        string pathroot = Path.GetPathRoot(gamePath);
                        pathroot = pathroot.Substring(0, 1);
                        if (pathroot == @"\")
                        {
                            diskType = -2; //-2 = UNC
                        }
                        else if (Utilities.IsWindows10OrNewer())
                        {
                            diskType = DiskTypeDetector.GetPartitionDiskBackingType(pathroot);
                        }

                        var processorName = "Unable to fetch";
                        uint processorSpeedMhz = 0;
                        uint processorCoreCount = 0;
                        var memoryAmount = Utilities.GetInstalledRamAmount() / 1024;

                        int officialDLCCount = 0;
                        switch (Game)
                        {
                            case 1:
                                gamePath = Path.Combine(gamePath, "DLC");
                                break;
                            case 2:
                            case 3:
                                gamePath = Path.Combine(gamePath, "BIOGame", "DLC");
                                break;
                        }
                        if (Directory.Exists(gamePath))
                        {
                            var directories = Directory.EnumerateDirectories(gamePath);
                            foreach (string dir in directories)
                            {
                                string value = Path.GetFileName(dir);
                                string dlcname = DiagnosticsWindow.InteralGetDLCName(value);
                                if (dlcname != null) { officialDLCCount++; }
                            }
                        }

                        ManagementObjectSearcher mosProcessor = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
                        foreach (ManagementObject moProcessor in mosProcessor.Get())
                        {
                            if (moProcessor["name"] != null)
                            {
                                processorName = moProcessor["name"].ToString().Trim();
                            }
                            if (moProcessor["maxclockspeed"] != null)
                            {
                                processorSpeedMhz = (uint)moProcessor["maxclockspeed"];
                            }
                            if (moProcessor["numberofcores"] != null)
                            {
                                processorCoreCount = (uint)moProcessor["numberofcores"];
                            }
                        }
                        Log.Information("Sending installation telemetry");
                        "https://me3tweaks.com/alotinstaller/installationtelemetry.php".PostUrlEncodedAsync(new { game = Game, memversion = memVersionUsed, installerversion = installerVersionUsed, processor = processorName, processor_corecount = processorCoreCount, processor_speed = processorSpeedMhz, memory = memoryAmount, installation_time = MEM_INSTALL_TIME_SECONDS, alladdonfiles = MainWindow.TELEMETRY_ALL_ADDON_FILES ? 1 : 0, officialdlccount = officialDLCCount, disktype = diskType, failed = telemetryfailedcode, os = OS });//.ReceiveString();
                        Log.Information("Installation telemetry has been submitted");
                        Analytics.TrackEvent("Addon Files Used", new Dictionary<string, string>()
                        {
                            {"All addon files (exluding optionals)" ,MainWindow.TELEMETRY_ALL_ADDON_FILES.ToString() },
                        });
                    }
                    catch (FlurlHttpTimeoutException)
                    {
                        Log.Warning("Timeout occured while attempting to upload installation telemetry.");
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Error occured while attempting to upload installation telemetry: " + ex.Message);
                    }
                };
                telemetryworker.RunWorkerAsync();
            }
            else
            {
                Log.Information("This is not a full installation, not submitting telemetry");
            }
        }

        public ConsoleApp Run7zWithProgressCallback(string args, AddonFile af, Action<int> progressCallback)
        {
            Log.Information("Running 7z progress process: 7z " + args);
            ConsoleApp ca = new ConsoleApp(MainWindow.BINARY_DIRECTORY + "7z.exe", args);
            ca.ConsoleOutput += (o, args2) =>
            {
                if (args2.IsError && args2.Line.Trim() != "" && !args2.Line.Trim().StartsWith("0M"))
                {
                    string line = args2.Line.Trim();
                    int percentIndex = line.IndexOf("%");
                    if (percentIndex > 0)
                    {
                        progressCallback?.Invoke(int.Parse(line.Substring(0, percentIndex)));
                    }
                    else
                    {
                        Log.Error("StdError from 7z: " + args2.Line.Trim());
                    }
                }
                else
                {
                    if (args2.Line.Trim() != "")
                    {
                        Log.Information("Realtime Process Output: " + args2.Line);
                    }
                }
            };

            ca.Run();
            return ca;
        }

        private void RunAndTimeMEMContextBased_Install(string exe, string args, BackgroundWorker installWorker, bool isMainInstall = false)
        {
            Stopwatch sw = Stopwatch.StartNew();
            runMEM_InstallContextBased(exe, args, InstallWorker);
            while (BACKGROUND_MEM_PROCESS.State == AppState.Running)
            {
                Thread.Sleep(END_OF_PROCESS_POLL_INTERVAL);
            }
            sw.Stop();
            int minutes = (int)sw.Elapsed.TotalMinutes;
            double fsec = 60 * (sw.Elapsed.TotalMinutes - minutes);
            int sec = (int)fsec;
            if (isMainInstall)
            {
                MEM_INSTALL_TIME_SECONDS = (minutes * 60) + sec;
            }
            Log.Information("Process complete - finished in " + minutes + " minutes " + sec + " seconds");
        }

        /// <summary>
        /// Process handler for MEM in install mode. This should not be called directly except by RunAndTimeMEM.
        /// </summary>
        /// <param name="exe"></param>
        /// <param name="args"></param>
        /// <param name="worker"></param>
        /// <param name="acceptedIPC"></param>
        private void runMEM_InstallContextBased(string exe, string args, BackgroundWorker worker, List<string> acceptedIPC = null)
        {
            Debug.WriteLine("Running process: " + exe + " " + args);
            Log.Information("Running process: " + exe + " " + args);


            BACKGROUND_MEM_PROCESS = new ConsoleApp(exe, args);
            BACKGROUND_MEM_PROCESS_ERRORS = new List<string>();
            BACKGROUND_MEM_PROCESS_PARSED_ERRORS = new List<string>();
            BACKGROUND_MEM_PROCESS.ConsoleOutput += (o, args2) =>
            {
                string str = args2.Line;
                if (DEBUG_LOGGING)
                {
                    Utilities.WriteDebugLog(str);
                }
                if (str.StartsWith("[IPC]", StringComparison.Ordinal)) //needs culture ordinal check??
                {
                    string command = str.Substring(5);
                    int endOfCommand = command.IndexOf(' ');
                    if (endOfCommand > 0)
                    {
                        command = command.Substring(0, endOfCommand);
                    }
                    if (acceptedIPC == null || acceptedIPC.Contains(command))
                    {
                        string param = str.Substring(endOfCommand + 5).Trim();
                        switch (command)
                        {
                            case "STAGE_ADD":
                                {
                                    STAGE_COUNT++;
                                    Log.Information("Adding stage added to install stages queue: " + param);
                                    ProgressWeightPercentages.AddTask(param, INSTALLING_THREAD_GAME);
                                    break;
                                }
                            case "STAGE_WEIGHT":
                                string[] parameters = param.Split(' ');
                                try
                                {
                                    double scale = Utilities.GetDouble(parameters[1], 1);
                                    Log.Information("Reweighting stage " + parameters[0] + " by " + parameters[1]);
                                    ProgressWeightPercentages.ScaleCurrentTaskWeight(CURRENT_STAGE_NUM - 1, scale);
                                }
                                catch (Exception e)
                                {
                                    Log.Information("STAGE_WEIGHT parameter invalid: " + e);
                                }
                                break;
                            case "STAGE_CONTEXT":
                                {
                                    if (param == "STAGE_DONE")
                                    {
                                        //We're done!
                                        STAGE_DONE_REACHED = true;
                                        return;
                                    }
                                    if (CURRENT_STAGE_CONTEXT != null)
                                    {
                                        Log.Warning("Stage " + CURRENT_STAGE_NUM + " has completed: " + CURRENT_STAGE_CONTEXT);
                                        int overallProgress = ProgressWeightPercentages.SubmitProgress(CURRENT_STAGE_NUM, 100);
                                        worker.ReportProgress(completed, new ThreadCommand(SET_OVERALL_PROGRESS, overallProgress));
                                    }
                                    else
                                    {
                                        //context is null, we are now starting
                                        ProgressWeightPercentages.ScaleWeights();
                                    }

                                    //clear errors so we can context switch error handling
                                    BACKGROUND_MEM_PROCESS_ERRORS = new List<string>();
                                    BACKGROUND_MEM_PROCESS_PARSED_ERRORS = new List<string>();


                                    Interlocked.Increment(ref CURRENT_STAGE_NUM);
                                    CurrentTaskPercent = 0;
                                    worker.ReportProgress(completed, new ThreadCommand(UPDATE_CURRENT_STAGE_PROGRESS, CurrentTaskPercent));
                                    worker.ReportProgress(completed, new ThreadCommand(UPDATE_STAGE_OF_STAGE_LABEL, param));
                                    CURRENT_STAGE_CONTEXT = param;
                                    Stage stage = ProgressWeightPercentages.Stages.Where(x => x.StageName == CURRENT_STAGE_CONTEXT).FirstOrDefault();
                                    CurrentTask = stage != null ? stage.TaskName : CURRENT_STAGE_CONTEXT;

                                    int progressval = ProgressWeightPercentages.SubmitProgress(CURRENT_STAGE_NUM, 0);
                                    worker.ReportProgress(completed, new ThreadCommand(UPDATE_CURRENTTASK_NAME, CurrentTask));
                                    break;
                                }
                            case "PROCESSING_FILE":
                                Log.Information("MEMNoGui processing file: " + param);
                                break;
                            case "TASK_PROGRESS":
                                worker.ReportProgress(completed, new ThreadCommand(UPDATE_CURRENT_STAGE_PROGRESS, param));
                                break;
                            case "SET_STAGE_LABEL":
                                worker.ReportProgress(completed, new ThreadCommand(UPDATE_ADDONUI_CURRENTTASK, param));
                                break;
                            case "HIDE_STAGES":
                                worker.ReportProgress(completed, new ThreadCommand(HIDE_ALL_STAGE_LABELS));
                                break;
                            case "ERROR_FILEMARKER_FOUND":
                                Log.Error("File was previously modified by ALOT: " + param);
                                BACKGROUND_MEM_PROCESS_ERRORS.Add(param);
                                break;
                            case "ERROR":
                                Log.Error("ERROR IPC from MEM: " + param);
                                BACKGROUND_MEM_PROCESS_ERRORS.Add(param);
                                break;
                            case "PROCESSING_TEXTURE_INSTALL":
                                Log.Information("Installing texture: " + param);
                                BACKGROUND_MEM_PROCESS_ERRORS.Add(param);
                                break;
                            default:
                                //check if IPC is a stage failure
                                StageFailure failure = null;
                                foreach (Stage stage in ProgressWeightPercentages.Stages)
                                {
                                    foreach (StageFailure sf in stage.FailureInfos)
                                    {
                                        if (sf.FailureIPCTrigger != null && sf.FailureIPCTrigger == command)
                                        {
                                            failure = sf;
                                            break;
                                        }
                                    }
                                    if (failure != null)
                                    {
                                        break;
                                    }
                                }
                                if (failure != null)
                                {
                                    if (failure.Warning)
                                    {
                                        Log.Warning("MEM warning IPC received: " + failure.FailureIPCTrigger + ": " + failure.FailureTopText);
                                        Log.Warning(" >> " + str);
                                    }
                                    else
                                    {
                                        Log.Error("A fail condition IPC has been received: " + failure.FailureIPCTrigger + ": " + failure.FailureTopText);
                                        Log.Error(" >> " + str);
                                        BACKGROUND_MEM_PROCESS_ERRORS.Add(failure.FailureIPCTrigger);
                                    }
                                }
                                else
                                {
                                    Log.Information("Unknown IPC command: " + command);
                                }
                                break;
                        }
                    }
                }
                else
                {
                    if (str.Trim() != "")
                    {
                        if (str.StartsWith("Exception occured") ||
                            str.StartsWith("Program crashed"))
                        {
                            Log.Error("MEM process output: " + str);
                        }
                        else
                        {
                            Log.Information("MEM process output: " + str);
                        }
                    }
                }
            };
            BACKGROUND_MEM_PROCESS.Run();
        }
    }
}