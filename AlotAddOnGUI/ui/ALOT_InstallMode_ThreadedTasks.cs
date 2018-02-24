using AlotAddOnGUI.classes;
using AlotAddOnGUI.music;
using AlotAddOnGUI.ui;
using ByteSizeLib;
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
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Xml.Linq;

namespace AlotAddOnGUI
{
    public partial class MainWindow : MetroWindow
    {
        private bool STAGE_DONE_REACHED = false;
        private const int RESULT_UNPACK_FAILED = -40;
        private const int RESULT_SCAN_REMOVE_FAILED = -41;
        private const int RESULT_TEXTUREINSTALL_FAILED = -42;
        private const int RESULT_ME1LAA_FAILED = -43;
        private const int RESULT_TEXTUREINSTALL_NO_TEXTUREMAP = -44;
        private const int RESULT_TEXTUREINSTALL_INVALID_TEXTUREMAP = -45;
        private const int RESULT_REPACK_FAILED = -46;
        private const int RESULT_TEXTUREINSTALL_GAME_FILE_REMOVED = -47;
        private const int RESULT_TEXTUREINSTALL_GAME_FILE_ADDED = -48;
        private const int RESULT_SAVING_FAILED = -49;
        private const int RESULT_REMOVE_MIPMAPS_FAILED = -50;

        private void MusicIcon_Click(object sender, RoutedEventArgs e)
        {
            if (MusicIsPlaying)
            {
                MusicPaused = !MusicPaused;
                Utilities.WriteRegistryKey(Registry.CurrentUser, REGISTRY_KEY, SETTINGSTR_SOUND, !MusicPaused);
                if (MusicPaused)
                {
                    waveOut.Pause();
                    MusicButtonIcon.Kind = MahApps.Metro.IconPacks.PackIconModernKind.SoundMute;
                }
                else
                {
                    MusicButtonIcon.Kind = MahApps.Metro.IconPacks.PackIconModernKind.Sound3;
                    waveOut.Play();
                }
            }
        }

        private void InstallALOT(int game, List<AddonFile> filesToInstall)
        {
            ADDONFILES_TO_INSTALL = filesToInstall;
            WARN_USER_OF_EXIT = true;
            InstallingOverlay_TopLabel.Text = "Preparing installer";
            InstallWorker = new BackgroundWorker();
            if (USING_BETA)
            {
                InstallWorker.DoWork += InstallALOTContextBased;
            }
            else
            {
                InstallWorker.DoWork += InstallALOT;
            }
            InstallWorker.WorkerReportsProgress = true;
            InstallWorker.ProgressChanged += InstallWorker_ProgressChanged;
            InstallWorker.RunWorkerCompleted += InstallCompleted;
            INSTALLING_THREAD_GAME = game;
            WindowButtonCommandsOverlayBehavior = WindowCommandsOverlayBehavior.Flyouts;
            InstallingOverlayFlyout.Theme = FlyoutTheme.Dark;

            //Set BG for this game
            string bgPath = "images/me" + game + "_bg.jpg";
            if (game == 2 && DateTime.Now.Month == 4 && DateTime.Now.Day == 1)
            {
                bgPath = "images/me2_bg_alt.jpg";
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
            InstallingOverlay_StageLabel.Visibility = System.Windows.Visibility.Visible;
            InstallingOverlay_OverallLabel.Visibility = System.Windows.Visibility.Visible;
            InstallingOverlay_OverallLabel.Text = "";
            InstallingOverlay_StageLabel.Text = "Getting ready";
            InstallingOverlay_BottomLabel.Text = "Please wait";
            Button_InstallViewLogs.Visibility = System.Windows.Visibility.Collapsed;


            SolidColorBrush backgroundShadeBrush = null;
            switch (INSTALLING_THREAD_GAME)
            {
                case 1:
                    backgroundShadeBrush = new SolidColorBrush(Color.FromArgb(0x77, 0, 0, 0));
                    Panel_ME1LODLimit.Visibility = System.Windows.Visibility.Collapsed;
                    //LODLIMIT = 0;
                    break;
                case 2:
                    backgroundShadeBrush = new SolidColorBrush(Color.FromArgb(0x55, 0, 0, 0));
                    Panel_ME1LODLimit.Visibility = System.Windows.Visibility.Collapsed;
                    break;
                case 3:
                    backgroundShadeBrush = new SolidColorBrush(Color.FromArgb(0x55, 0, 0, 0));
                    Panel_ME1LODLimit.Visibility = System.Windows.Visibility.Collapsed;
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
            REPACK_GAME_FILES = Checkbox_RepackGameFiles.IsChecked.Value;
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
            InstallingOverlay_OverallLabel.Visibility = System.Windows.Visibility.Visible;
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
                    InstallingOverlay_StageLabel.Text = "Stage " + CURRENT_STAGE_NUM + " of " + STAGE_COUNT;
                    break;
                case HIDE_LOD_LIMIT:
                    Panel_ME1LODLimit.Visibility = System.Windows.Visibility.Collapsed;
                    break;
                case HIDE_STAGES_LABEL:
                    InstallingOverlay_StageLabel.Visibility = System.Windows.Visibility.Collapsed;
                    InstallingOverlay_OverallLabel.Visibility = System.Windows.Visibility.Collapsed;
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
                    if (tc.Data is string)
                    {
                        CurrentTaskPercent = Convert.ToInt32((string)tc.Data);
                    }
                    else
                    {
                        CurrentTaskPercent = (int)tc.Data;
                    }
                    if (((CurrentTaskPercent != oldTaskProgress && CurrentTaskPercent > 0) || CurrentTaskPercent == 0) && CurrentTaskPercent <= 100)
                    {
                        int progressval = ProgressWeightPercentages.SubmitProgress(CURRENT_STAGE_NUM, CurrentTaskPercent);
                        InstallingOverlay_BottomLabel.Text = CurrentTask + " " + CurrentTaskPercent + "%";
                        InstallingOverlay_OverallLabel.Text = "(" + progressval.ToString() + "%)";
                        TaskbarManager.Instance.SetProgressValue(progressval, 100);
                    }
                    break;
                case SET_OVERALL_PROGRESS:
                    //InstallingOverlay_BottomLabel.Text = CurrentTask + " " + CurrentTaskPercent + "%";
                    //TaskbarManager.Instance.SetProgressValue((int)tc.Data, 100);
                    int progress = ProgressWeightPercentages.GetOverallProgress();
                    InstallingOverlay_OverallLabel.Text = "(" + progress.ToString() + "%)";
                    TaskbarManager.Instance.SetProgressValue(progress, 100);
                    break;
                case UPDATE_PROGRESSBAR_INDETERMINATE:
                    //Install_ProgressBar.IsIndeterminate = (bool)tc.Data;
                    break;
                case ERROR_OCCURED:
                    Build_ProgressBar.IsIndeterminate = false;
                    Build_ProgressBar.Value = 0;
                    //await this.ShowMessageAsync("Error building Addon MEM Package", "An error occured building the addon. The logs will provide more information. The error message given is:\n" + (string)tc.Data);
                    break;
                case SHOW_DIALOG:
                    KeyValuePair<string, string> messageStr = (KeyValuePair<string, string>)tc.Data;
                    await this.ShowMessageAsync(messageStr.Key, messageStr.Value);
                    break;
                case SHOW_DIALOG_YES_NO:
                    //ThreadCommandDialogOptions tcdo = (ThreadCommandDialogOptions)tc.Data;
                    //MetroDialogSettings settings = new MetroDialogSettings();
                    //settings.NegativeButtonText = tcdo.NegativeButtonText;
                    //settings.AffirmativeButtonText = tcdo.AffirmativeButtonText;
                    //MessageDialogResult result = await this.ShowMessageAsync(tcdo.title, tcdo.message, MessageDialogStyle.AffirmativeAndNegative, settings);
                    //if (result == MessageDialogResult.Negative)
                    //{
                    //    CONTINUE_BACKUP_EVEN_IF_VERIFY_FAILS = false;
                    //}
                    //else
                    //{
                    //    CONTINUE_BACKUP_EVEN_IF_VERIFY_FAILS = true;
                    //}
                    //tcdo.signalHandler.Set();
                    break;
                case INCREMENT_COMPLETION_EXTRACTION:
                    //Interlocked.Increment(ref completed);
                    //Install_ProgressBar.Value = (completed / (double)ADDONSTOINSTALL_COUNT) * 100;
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
            CurrentTask = "";
            Log.Information("InstallWorker Thread starting for ME" + INSTALLING_THREAD_GAME);
            Log.Information("This installer session is context based and will run in a single instance.");
            ProgressWeightPercentages.ClearTasks();
            ALOTVersionInfo versionInfo = Utilities.GetInstalledALOTInfo(INSTALLING_THREAD_GAME);

            Log.Information("Setting biogame directory to read-write");
            Utilities.MakeAllFilesInDirReadWrite(Utilities.GetGamePath(INSTALLING_THREAD_GAME) + "\\BIOGame");
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
            MAINTASK_TEXT = "Installing " + primary + " for Mass Effect" + getGameNumberSuffix(INSTALLING_THREAD_GAME);
            InstallWorker.ReportProgress(completed, new ThreadCommand(UPDATE_OVERALL_TASK, MAINTASK_TEXT));

            string exe = BINARY_DIRECTORY + MEM_EXE_NAME;
            string args = "";
            int processResult = 0;
            int overallProgress = 0;
            stopwatch = Stopwatch.StartNew();
            Log.Information("InstallWorker(): Running MassEffectModderNoGui");
            CurrentTaskPercent = -1;
            string outputDir = getOutputDir(INSTALLING_THREAD_GAME, false);

            args = "-install-mods " + INSTALLING_THREAD_GAME + " \"" + outputDir + "\"";
            if (REPACK_GAME_FILES)
            {
                args += " -repack";
            }
            args += " -ipc -gui-installer -new-way";
            runMEM_InstallContextBased(exe, args, InstallWorker);
            while (BACKGROUND_MEM_PROCESS.State == AppState.Running)
            {
                Thread.Sleep(END_OF_PROCESS_POLL_INTERVAL);
            }
            processResult = BACKGROUND_MEM_PROCESS.ExitCode ?? 1;
            if (!STAGE_DONE_REACHED)
            {
                if (processResult != 0)
                {
                    Log.Error("MassEffectModderNoGui process exited with non-zero code: " + processResult);
                    Log.Warning("Application exited with in context: " + CURRENT_STAGE_CONTEXT);
                    switch (CURRENT_STAGE_CONTEXT)
                    {
                        case "STAGE_UNPACKDLC":
                            Log.Error("MassEffectModderNoGui exited or crashed while unpacking DLC");
                            e.Result = RESULT_UNPACK_FAILED;
                            break;
                        case "STAGE_SCAN":
                            Log.Error("MassEffectModderNoGui exited or crashed while scanning textures");
                            e.Result = RESULT_SCAN_REMOVE_FAILED;
                            break;
                        case "STAGE_INSTALLTEXTURES":
                            Log.Error("MassEffectModderNoGui exited or crashed while installing textures");
                            if (BACKGROUND_MEM_PROCESS_ERRORS.Count > 0)
                            {
                                switch (BACKGROUND_MEM_PROCESS_ERRORS[0])
                                {
                                    case ERROR_TEXTURE_MAP_MISSING:
                                        e.Result = RESULT_TEXTUREINSTALL_NO_TEXTUREMAP;
                                        break;
                                    case ERROR_TEXTURE_MAP_WRONG:
                                        e.Result = RESULT_TEXTUREINSTALL_INVALID_TEXTUREMAP;
                                        break;
                                    case ERROR_FILE_ADDED:
                                        e.Result = RESULT_TEXTUREINSTALL_GAME_FILE_ADDED;
                                        break;
                                    case ERROR_FILE_REMOVED:
                                        e.Result = RESULT_TEXTUREINSTALL_GAME_FILE_REMOVED;
                                        break;
                                    default:
                                        Log.Error("Background MEM errors has item not handled: " + BACKGROUND_MEM_PROCESS_ERRORS[0]);
                                        e.Result = RESULT_TEXTUREINSTALL_FAILED;
                                        break;
                                }
                            }
                            else
                            {
                                e.Result = RESULT_TEXTUREINSTALL_FAILED;
                            }
                            break;
                        case "STAGE_SAVING":
                            Log.Error("MassEffectModderNoGui exited or crashed while saving packages");
                            e.Result = RESULT_SAVING_FAILED;
                            break;
                        case "STAGE_REMOVEMIPMAPS":
                            Log.Error("MassEffectModderNoGui exited or crashed while removing empty mipmaps");
                            e.Result = RESULT_REMOVE_MIPMAPS_FAILED;
                            break;
                        case "STAGE_REPACK":
                            Log.Error("MassEffectModderNoGui exited or crashed while scanning textures");
                            e.Result = RESULT_REPACK_FAILED;
                            break;
                        default:
                            Log.Error("MEM Exited during unknown stage context: " + STAGE_CONTEXT);
                            break;
                    }

                    e.Result = RESULT_UNPACK_FAILED;
                    InstallWorker.ReportProgress(0, new ThreadCommand(HIDE_TIPS));
                    InstallWorker.ReportProgress(0, new ThreadCommand(HIDE_LOD_LIMIT));
                    return;
                }
            }
            overallProgress = ProgressWeightPercentages.SubmitProgress(CURRENT_STAGE_NUM, 100);
            InstallWorker.ReportProgress(0, new ThreadCommand(SET_OVERALL_PROGRESS, overallProgress));
            //Interlocked.Increment(ref INSTALL_STAGE);
            //InstallWorker.ReportProgress(0, new ThreadCommand(UPDATE_STAGE_LABEL));

            if (false)
            {
                //CONTEXT = UNPACK_DLC
                processResult = BACKGROUND_MEM_PROCESS.ExitCode ?? 1;
                if (processResult != 0)
                {
                    Log.Error("UNPACK RETURN CODE WAS NOT 0: " + processResult);
                    e.Result = RESULT_UNPACK_FAILED;
                    InstallWorker.ReportProgress(0, new ThreadCommand(HIDE_TIPS));
                    InstallWorker.ReportProgress(0, new ThreadCommand(HIDE_LOD_LIMIT));
                    return;
                }

                //CONTEXT = SCAN
                if (processResult != 0)
                {
                    Log.Error("MassEffectModderNoGui exited during Scanning Textures stage with code: " + processResult);
                    e.Result = RESULT_SCAN_REMOVE_FAILED;
                    InstallWorker.ReportProgress(0, new ThreadCommand(HIDE_TIPS));
                    InstallWorker.ReportProgress(0, new ThreadCommand(HIDE_LOD_LIMIT));
                    return;
                }

                //CONTEXT = STAGE_REMOVEMIPMAPS
                if (processResult != 0)
                {
                    Log.Error("MassEffectModderNoGui exited during Scanning Textures stage with code: " + processResult);
                    e.Result = RESULT_SCAN_REMOVE_FAILED;
                    InstallWorker.ReportProgress(0, new ThreadCommand(HIDE_TIPS));
                    InstallWorker.ReportProgress(0, new ThreadCommand(HIDE_LOD_LIMIT));
                    return;
                }

                //Context = STAGE_INSTALLTEXTURES
                processResult = BACKGROUND_MEM_PROCESS.ExitCode ?? 1;
                if (processResult != 0)
                {
                    Log.Error("TEXTURE INSTALLATION RETURN CODE WAS NOT 0: " + processResult);
                    if (BACKGROUND_MEM_PROCESS_ERRORS.Count > 0)
                    {
                        switch (BACKGROUND_MEM_PROCESS_ERRORS[0])
                        {
                            case ERROR_TEXTURE_MAP_MISSING:
                                e.Result = RESULT_TEXTUREINSTALL_NO_TEXTUREMAP;
                                break;
                            case ERROR_TEXTURE_MAP_WRONG:
                                e.Result = RESULT_TEXTUREINSTALL_INVALID_TEXTUREMAP;
                                break;
                            case ERROR_FILE_ADDED:
                                e.Result = RESULT_TEXTUREINSTALL_GAME_FILE_ADDED;
                                break;
                            case ERROR_FILE_REMOVED:
                                e.Result = RESULT_TEXTUREINSTALL_GAME_FILE_REMOVED;
                                break;
                        }
                    }
                }

                //Context = STAGE_REPACK
                if (processResult != 0)
                {
                    Log.Error("REPACKING RETURN CODE WAS NOT 0: " + processResult);
                    if (e.Result == null)
                    {
                        e.Result = RESULT_REPACK_FAILED;
                    }
                    InstallWorker.ReportProgress(0, new ThreadCommand(HIDE_TIPS));
                    InstallWorker.ReportProgress(0, new ThreadCommand(HIDE_LOD_LIMIT));
                    return;
                }
            }

            InstallWorker.ReportProgress(0, new ThreadCommand(UPDATE_OVERALL_TASK, "Finishing installation"));
            InstallWorker.ReportProgress(0, new ThreadCommand(HIDE_STAGES_LABEL));


            //Apply LOD
            CurrentTask = "Updating Mass Effect" + getGameNumberSuffix(INSTALLING_THREAD_GAME) + "'s graphics settings";
            InstallWorker.ReportProgress(0, new ThreadCommand(UPDATE_CURRENTTASK_NAME, CurrentTask));

            InstallWorker.ReportProgress(0, new ThreadCommand(HIDE_LOD_LIMIT, CurrentTask));

            args = "-apply-lods-gfx " + INSTALLING_THREAD_GAME;
            RunAndTimeMEM_Install(exe, args, InstallWorker);
            processResult = BACKGROUND_MEM_PROCESS.ExitCode ?? 6000;
            if (processResult != 0)
            {
                Log.Error("APPLYLOD RETURN CODE WAS NOT 0: " + processResult);
            }

            if (INSTALLING_THREAD_GAME == 1)
            {
                //Apply ME1 LAA
                CurrentTask = "Installing fixes for Mass Effect";
                InstallWorker.ReportProgress(0, new ThreadCommand(UPDATE_CURRENTTASK_NAME, CurrentTask));

                args = "-apply-me1-laa";
                RunAndTimeMEM_Install(exe, args, InstallWorker);
                processResult = BACKGROUND_MEM_PROCESS.ExitCode ?? 1;
                if (processResult != 0)
                {
                    Log.Error("Error setting ME1 to large address aware/bootable without admin: " + processResult);
                    e.Result = RESULT_ME1LAA_FAILED;
                    return;
                }
                Utilities.RemoveRunAsAdminXPSP3FromME1();
                Utilities.InstallIndirectSoundFixForME1();
                string iniPath = IniSettingsHandler.GetConfigIniPath(1);
                if (File.Exists(iniPath))
                {
                    IniFile engineConf = new IniFile(iniPath);
                    engineConf.Write("DeviceName", "Generic Hardware", "ISACTAudio.ISACTAudioDevice");
                }
            }
            Utilities.TurnOffOriginAutoUpdate();

            //Create/Update Marker File
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
            Utilities.CreateMarkerFile(INSTALLING_THREAD_GAME, newVersion);
            ALOTVersionInfo test = Utilities.GetInstalledALOTInfo(INSTALLING_THREAD_GAME);
            if (test == null || test.ALOTVER != newVersion.ALOTVER || test.ALOTUPDATEVER != newVersion.ALOTUPDATEVER)
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
                        Log.Error("Marker file does not show that ALOT Update was applied or installed to our current version");
                    }
                }
            }
            //Install Binkw32
            if (INSTALLING_THREAD_GAME == 2 || INSTALLING_THREAD_GAME == 3)
            {
                Utilities.InstallBinkw32Bypass(INSTALLING_THREAD_GAME);
            }

            //If MAIN alot file is here, move it back to downloaded_mods
            if (alotMainFile != null && alotMainFile.UnpackedSingleFilename != null)
            {
                //ALOT was just installed. We are going to move it back to mods folder
                string extractedName = alotMainFile.UnpackedSingleFilename;
                Log.Information("ALOT MAIN FILE - Unpacked - moving to downloaded_mods from install dir: " + extractedName);
                string source = getOutputDir(INSTALLING_THREAD_GAME) + "000_" + extractedName;
                string dest = DOWNLOADED_MODS_DIRECTORY + "\\" + extractedName;

                if (File.Exists(source))
                {
                    try
                    {
                        if (File.Exists(dest))
                        {
                            File.Delete(dest);
                        }
                        File.Move(source, dest);
                        Log.Information("Moved main alot file back to downloaded_mods");
                        //Delete original
                        dest = DOWNLOADED_MODS_DIRECTORY + "\\" + alotMainFile.Filename;
                        if (File.Exists(dest))
                        {
                            Log.Information("Deleting original alot archive file from downloaded_mods");
                            File.Delete(dest);
                            Log.Information("Deleted original alot archive file from downloaded_mods");
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

            InstallWorker.ReportProgress(0, new ThreadCommand(HIDE_STAGES_LABEL));

            string taskString = "Installation of " + primary + " for Mass Effect" + getGameNumberSuffix(INSTALLING_THREAD_GAME);
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
            e.Result = INSTALL_OK;
        }

        private void MusicPlaybackStopped(object sender, StoppedEventArgs e)
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

        private string GetMusicDirectory()
        {
            return EXE_DIRECTORY + "Data\\music\\";
        }

        private void InstallCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
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
            Log.Information("InstallCompleted()");
            InstallingOverlay_OverallLabel.Visibility = System.Windows.Visibility.Collapsed;

            tipticker.Stop();
            InstallingOverlay_StageLabel.Visibility = System.Windows.Visibility.Collapsed;
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
                string gameName = "Mass Effect" + getGameNumberSuffix(INSTALLING_THREAD_GAME);
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
                    case RESULT_SCAN_REMOVE_FAILED:
                        {
                            InstallingOverlay_TopLabel.Text = "Failed to remove empty mipmaps";
                            InstallingOverlay_BottomLabel.Text = "Check the logs for details";
                            HeaderLabel.Text = "Error occured removing mipmaps from " + gameName;
                            break;
                        }
                    case RESULT_TEXTUREINSTALL_FAILED:
                        {
                            InstallingOverlay_TopLabel.Text = "Failed to install textures";
                            InstallingOverlay_BottomLabel.Text = "Check the logs for details";
                            HeaderLabel.Text = "Error occured installing textures for " + gameName;
                            break;
                        }
                    case RESULT_TEXTUREINSTALL_NO_TEXTUREMAP:
                        {
                            InstallingOverlay_TopLabel.Text = "Failed to install textures";
                            InstallingOverlay_BottomLabel.Text = "Texture map is missing";
                            HeaderLabel.Text = "Texture map missing - revert " + gameName + " to an unmodified game to fix.";
                            break;
                        }
                    case RESULT_TEXTUREINSTALL_GAME_FILE_ADDED:
                        {
                            InstallingOverlay_TopLabel.Text = "Texture installation blocked";
                            InstallingOverlay_BottomLabel.Text = "Game file(s) were added after initial install\nDo not add game files after initial installation";
                            HeaderLabel.Text = "Game files were added after initial installation of ALOT or MEUITM - this is not supported. You will need to revert to an unmodified game to fix.";
                            break;
                        }
                    case RESULT_TEXTUREINSTALL_GAME_FILE_REMOVED:
                        {
                            InstallingOverlay_TopLabel.Text = "Texture installation blocked";
                            InstallingOverlay_BottomLabel.Text = "Game file(s) were removed after initial install\nDo not remove game files after initial installation";
                            HeaderLabel.Text = "Game files were removed after initial installation of ALOT or MEUITM - this is not supported. You will need to revert to an unmodified game to fix.";
                            break;
                        }
                    case RESULT_TEXTUREINSTALL_INVALID_TEXTUREMAP:
                        {
                            InstallingOverlay_TopLabel.Text = "Failed to install textures";
                            InstallingOverlay_BottomLabel.Text = "Texture map is corrupt";
                            HeaderLabel.Text = "Texture map is corrupt - revert " + gameName + " to an unmodified game to fix.";
                            break;
                        }
                    case RESULT_REPACK_FAILED:
                        {
                            InstallingOverlay_TopLabel.Text = "Failed to repack game files";
                            InstallingOverlay_BottomLabel.Text = "Check the logs for details";
                            HeaderLabel.Text = "Failed to repack game files - game may be in an unusable state.";
                            break;
                        }
                    case RESULT_UNPACK_FAILED:
                        {
                            InstallingOverlay_TopLabel.Text = "Failed to unpack DLCs";
                            InstallingOverlay_BottomLabel.Text = "Check the logs for details";
                            HeaderLabel.Text = "Failed to unpack DLC. Restore your game to unmodified or DLC will not work.";
                            break;
                        }
                    case RESULT_REMOVE_MIPMAPS_FAILED:
                        {
                            InstallingOverlay_TopLabel.Text = "Failed to remove empty mipmaps";
                            InstallingOverlay_BottomLabel.Text = "Check the logs for details";
                            HeaderLabel.Text = "Failed to remove empty mipmaps. Restore your game to unmodified. The game is in an unusable state.";
                            break;
                        }
                    case RESULT_SAVING_FAILED:
                        {
                            InstallingOverlay_TopLabel.Text = "Failed to save game packages";
                            InstallingOverlay_BottomLabel.Text = "Check the logs for details";
                            HeaderLabel.Text = "Failed to save game packages. Restore your game to unmodified. The game is in an unusable state.";
                            break;
                        }
                }
                if (result != INSTALL_OK)
                {
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
            INSTALLING_THREAD_GAME = 0;
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
        }

        private void InstallALOT(object sender, DoWorkEventArgs e)
        {
            Log.Information("InstallWorker Thread starting for ME" + INSTALLING_THREAD_GAME);
            ProgressWeightPercentages.ClearTasks();
            ALOTVersionInfo versionInfo = Utilities.GetInstalledALOTInfo(INSTALLING_THREAD_GAME);

            Log.Information("Setting biogame directory to read-write");
            Utilities.MakeAllFilesInDirReadWrite(Utilities.GetGamePath(INSTALLING_THREAD_GAME) + "\\BIOGame");
            Log.Information("Files being installed in this installation session:");
            foreach (AddonFile af in ADDONFILES_TO_INSTALL)
            {
                Log.Information(" - " + af.FriendlyName);
            }


            bool RemoveMipMaps = (versionInfo == null); //remove mipmaps only if alot is not installed
            if (INSTALLING_THREAD_GAME == 1)
            {
                REPACK_GAME_FILES = false;
                STAGE_COUNT = 4; //scan/remove/install/save
            }
            else if (INSTALLING_THREAD_GAME == 2)
            {
                STAGE_COUNT = 4;
                if (REPACK_GAME_FILES)
                {
                    STAGE_COUNT++;
                }
            }
            else
            {
                //me3
                REPACK_GAME_FILES = false; //force off, it does nothing
                STAGE_COUNT = 5;

                if (versionInfo == null)
                {
                    ProgressWeightPercentages.AddTask(ProgressWeightPercentages.JOB_UNPACK);
                }
                else
                {
                    STAGE_COUNT--; //no unpack dlc stage
                }
            }

            if (!RemoveMipMaps)
            {
                STAGE_COUNT -= 2; //Scanning, Removing
            }
            else
            {
                ProgressWeightPercentages.AddTask(ProgressWeightPercentages.JOB_SCAN);
                ProgressWeightPercentages.AddTask(ProgressWeightPercentages.JOB_REMOVE);
                //ProgressWeightPercentages.AddTask(ProgressWeightPercentages.JOB_INSTALLMARKERS);
            }


            ProgressWeightPercentages.AddTask(ProgressWeightPercentages.JOB_INSTALL);
            ProgressWeightPercentages.AddTask(ProgressWeightPercentages.JOB_SAVE);
            if (REPACK_GAME_FILES && INSTALLING_THREAD_GAME != 3)
            {
                ProgressWeightPercentages.AddTask(ProgressWeightPercentages.JOB_REPACK);
            }
            ProgressWeightPercentages.ScaleWeights();
            CURRENT_STAGE_NUM = 0;
            //Checking files for title

            AddonFile alotMainFile = null;
            bool installedALOT = false;
            byte justInstalledUpdate = 0;
            bool installingMEUITM = false;
            //Check if ALOT is in files that will be installed
            foreach (AddonFile af in ADDONFILES_TO_INSTALL)
            {
                if (af.MEUITM)
                {
                    Log.Information("InstallWorker: We are installing MEUITM in this pass.");
                    installingMEUITM = true;
                }
                if (af.ALOTVersion > 0)
                {
                    alotMainFile = af;
                    Log.Information("InstallWorker: We are installing ALOT v" + af.ALOTVersion + " in this pass.");
                    installedALOT = true;
                }
                if (af.ALOTUpdateVersion > 0)
                {
                    Log.Information("InstallWorker: We are installing ALOT Update v" + af.ALOTUpdateVersion + " in this pass.");
                    justInstalledUpdate = af.ALOTUpdateVersion;
                }
            }
            string primary = "";
            if (installedALOT)
            {
                primary = "ALOT";
            }

            if (justInstalledUpdate > 0)
            {
                if (primary != "")
                {
                    primary += ", ";
                }
                primary += "ALOT update";
            }

            if (installingMEUITM)
            {
                if (primary != "")
                {
                    primary += " & ";
                }
                primary += "MEUITM";
            }

            if (primary == "")
            {
                primary = "texture mods";
            }
            MAINTASK_TEXT = "Installing " + primary + " for Mass Effect" + getGameNumberSuffix(INSTALLING_THREAD_GAME);
            InstallWorker.ReportProgress(completed, new ThreadCommand(UPDATE_OVERALL_TASK, MAINTASK_TEXT));


            List<string> acceptedIPC = new List<string>();
            acceptedIPC.Add("TASK_PROGRESS");
            acceptedIPC.Add("PHASE");
            acceptedIPC.Add("ERROR");
            acceptedIPC.Add("PROCESSING_MOD");
            acceptedIPC.Add("PROCESSING_FILE");

            string exe = BINARY_DIRECTORY + MEM_EXE_NAME;
            string args = "";
            int processResult = 0;
            int overallProgress = 0;
            stopwatch = Stopwatch.StartNew();
            if (INSTALLING_THREAD_GAME == 3 && versionInfo == null)
            {
                //Unpack DLC
                Log.Information("InstallWorker(): ME3 -> Unpacking DLC.");
                CurrentTask = "Unpacking DLC";
                CurrentTaskPercent = 0;
                InstallWorker.ReportProgress(0, new ThreadCommand(UPDATE_ADDONUI_CURRENTTASK, CurrentTask));
                InstallWorker.ReportProgress(0, new ThreadCommand(UPDATE_CURRENT_STAGE_PROGRESS, CurrentTaskPercent));
                Interlocked.Increment(ref CURRENT_STAGE_NUM); //unpack-dlcs does not output phase 
                InstallWorker.ReportProgress(0, new ThreadCommand(UPDATE_STAGE_OF_STAGE_LABEL));
                args = "-unpack-dlcs -ipc";
                RunAndTimeMEM_Install(exe, args, InstallWorker);
                processResult = BACKGROUND_MEM_PROCESS.ExitCode ?? 1;
                if (processResult != 0)
                {
                    Log.Error("UNPACK RETURN CODE WAS NOT 0: " + processResult);
                    e.Result = RESULT_UNPACK_FAILED;
                    InstallWorker.ReportProgress(0, new ThreadCommand(HIDE_TIPS));
                    InstallWorker.ReportProgress(0, new ThreadCommand(HIDE_LOD_LIMIT));
                    return;
                }
                overallProgress = ProgressWeightPercentages.SubmitProgress(CURRENT_STAGE_NUM, 100);
                InstallWorker.ReportProgress(0, new ThreadCommand(SET_OVERALL_PROGRESS, overallProgress));
                //Interlocked.Increment(ref INSTALL_STAGE);
                //InstallWorker.ReportProgress(0, new ThreadCommand(UPDATE_STAGE_LABEL));
            }

            //Scan and remove empty MipMaps
            if (RemoveMipMaps)
            {
                Log.Information("InstallWorker(): Performing texture scan, removing empty mipmaps, adding remaining markers");

                args = "-scan-with-remove " + INSTALLING_THREAD_GAME + " -ipc";
                RunAndTimeMEM_Install(exe, args, InstallWorker);
                processResult = BACKGROUND_MEM_PROCESS.ExitCode ?? 6000;
                if (processResult != 0)
                {
                    Log.Error("SCAN/REMOVE RETURN CODE WAS NOT 0: " + processResult);
                    e.Result = RESULT_SCAN_REMOVE_FAILED;
                    InstallWorker.ReportProgress(0, new ThreadCommand(HIDE_TIPS));
                    InstallWorker.ReportProgress(0, new ThreadCommand(HIDE_LOD_LIMIT));
                    return;
                }
                Log.Warning("Stage " + CURRENT_STAGE_NUM + " has completed.");
                overallProgress = ProgressWeightPercentages.SubmitProgress(CURRENT_STAGE_NUM, 100);
                InstallWorker.ReportProgress(0, new ThreadCommand(SET_OVERALL_PROGRESS, overallProgress));
                //scan with remove or install textures will increment this

            }

            //Install Textures
            Interlocked.Increment(ref CURRENT_STAGE_NUM);
            InstallWorker.ReportProgress(0, new ThreadCommand(UPDATE_STAGE_OF_STAGE_LABEL));
            string outputDir = getOutputDir(INSTALLING_THREAD_GAME, false);
            CurrentTask = "Installing textures";
            CurrentTaskPercent = 0;
            InstallWorker.ReportProgress(0, new ThreadCommand(UPDATE_ADDONUI_CURRENTTASK, CurrentTask));
            InstallWorker.ReportProgress(0, new ThreadCommand(UPDATE_CURRENT_STAGE_PROGRESS, CurrentTaskPercent));
            args = "-install-mods " + INSTALLING_THREAD_GAME + " \"" + outputDir + "\"";
            if (REPACK_GAME_FILES && INSTALLING_THREAD_GAME == 2)
            {
                args += " -repack";
            }
            args += " -ipc";
            RunAndTimeMEM_Install(exe, args, InstallWorker);

            processResult = BACKGROUND_MEM_PROCESS.ExitCode ?? 1;
            if (processResult != 0)
            {
                Log.Error("TEXTURE INSTALLATION RETURN CODE WAS NOT 0: " + processResult);
                if (BACKGROUND_MEM_PROCESS_ERRORS.Count > 0)
                {
                    switch (BACKGROUND_MEM_PROCESS_ERRORS[0])
                    {
                        case ERROR_TEXTURE_MAP_MISSING:
                            e.Result = RESULT_TEXTUREINSTALL_NO_TEXTUREMAP;
                            break;
                        case ERROR_TEXTURE_MAP_WRONG:
                            e.Result = RESULT_TEXTUREINSTALL_INVALID_TEXTUREMAP;
                            break;
                        case ERROR_FILE_ADDED:
                            e.Result = RESULT_TEXTUREINSTALL_GAME_FILE_ADDED;
                            break;
                        case ERROR_FILE_REMOVED:
                            e.Result = RESULT_TEXTUREINSTALL_GAME_FILE_REMOVED;
                            break;
                    }
                }
                if (e.Result == null)
                {
                    e.Result = RESULT_TEXTUREINSTALL_FAILED;
                }
                InstallWorker.ReportProgress(0, new ThreadCommand(HIDE_TIPS));
                InstallWorker.ReportProgress(0, new ThreadCommand(HIDE_LOD_LIMIT));
                return;
            }
            Log.Warning("Stage " + CURRENT_STAGE_NUM + " has completed.");
            ProgressWeightPercentages.SubmitProgress(CURRENT_STAGE_NUM, 100);
            InstallWorker.ReportProgress(0, new ThreadCommand(SET_OVERALL_PROGRESS, overallProgress));

            if (REPACK_GAME_FILES)
            {
                CurrentTaskPercent = 0;
                args = "-repack " + INSTALLING_THREAD_GAME + " -ipc";
                RunAndTimeMEM_Install(exe, args, InstallWorker);
                processResult = BACKGROUND_MEM_PROCESS.ExitCode ?? 1;
                if (processResult != 0)
                {
                    Log.Error("REPACKING RETURN CODE WAS NOT 0: " + processResult);
                    if (e.Result == null)
                    {
                        e.Result = RESULT_REPACK_FAILED;
                    }
                    InstallWorker.ReportProgress(0, new ThreadCommand(HIDE_TIPS));
                    InstallWorker.ReportProgress(0, new ThreadCommand(HIDE_LOD_LIMIT));
                    return;
                }
                Log.Warning("Stage " + CURRENT_STAGE_NUM + " has completed.");
            }



            InstallWorker.ReportProgress(0, new ThreadCommand(UPDATE_OVERALL_TASK, "Finishing installation"));
            InstallWorker.ReportProgress(0, new ThreadCommand(HIDE_STAGES_LABEL));


            //Apply LOD
            CurrentTask = "Updating Mass Effect" + getGameNumberSuffix(INSTALLING_THREAD_GAME) + "'s graphics settings";
            InstallWorker.ReportProgress(0, new ThreadCommand(UPDATE_CURRENTTASK_NAME, CurrentTask));

            InstallWorker.ReportProgress(0, new ThreadCommand(HIDE_LOD_LIMIT, CurrentTask));

            args = "-apply-lods-gfx " + INSTALLING_THREAD_GAME;
            RunAndTimeMEM_Install(exe, args, InstallWorker);
            processResult = BACKGROUND_MEM_PROCESS.ExitCode ?? 6000;
            if (processResult != 0)
            {
                Log.Error("APPLYLOD RETURN CODE WAS NOT 0: " + processResult);
            }

            if (INSTALLING_THREAD_GAME == 1)
            {
                //Apply ME1 LAA
                CurrentTask = "Installing fixes for Mass Effect";
                InstallWorker.ReportProgress(0, new ThreadCommand(UPDATE_CURRENTTASK_NAME, CurrentTask));

                args = "-apply-me1-laa";
                RunAndTimeMEM_Install(exe, args, InstallWorker);
                processResult = BACKGROUND_MEM_PROCESS.ExitCode ?? 1;
                if (processResult != 0)
                {
                    Log.Error("Error setting ME1 to large address aware/bootable without admin: " + processResult);
                    e.Result = RESULT_ME1LAA_FAILED;
                    return;
                }
                Utilities.RemoveRunAsAdminXPSP3FromME1();

            }
            Utilities.TurnOffOriginAutoUpdate();

            //Create/Update Marker File
            int meuitmFlag = (installingMEUITM) ? meuitmFile.MEUITMVer : (versionInfo != null ? versionInfo.MEUITMVER : 0);
            short alotMainVersionFlag = (alotMainFile != null) ? alotMainFile.ALOTVersion : (versionInfo != null ? versionInfo.ALOTVER : (short)0); //we should not see it write 0... hopefully

            //Update Marker
            byte updateVersion = 0;
            if (justInstalledUpdate > 0)
            {
                updateVersion = justInstalledUpdate;
            }
            else
            {
                updateVersion = versionInfo != null ? versionInfo.ALOTUPDATEVER : (byte)0;
            }

            //Write Marker
            ALOTVersionInfo newVersion = new ALOTVersionInfo(alotMainVersionFlag, updateVersion, 0, meuitmFlag);
            Utilities.CreateMarkerFile(INSTALLING_THREAD_GAME, newVersion);
            ALOTVersionInfo test = Utilities.GetInstalledALOTInfo(INSTALLING_THREAD_GAME);
            if (test == null || test.ALOTVER != newVersion.ALOTVER || test.ALOTUPDATEVER != newVersion.ALOTUPDATEVER)
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
                        Log.Error("Marker file does not show that ALOT Update was applied or installed to our current version");
                    }
                }
            }
            //Install Binkw32
            if (INSTALLING_THREAD_GAME == 2 || INSTALLING_THREAD_GAME == 3)
            {
                Utilities.InstallBinkw32Bypass(INSTALLING_THREAD_GAME);
            }

            //Install IndirectSound fix
            if (INSTALLING_THREAD_GAME == 1)
            {
                Utilities.InstallIndirectSoundFixForME1();
            }

            //If MAIN alot file is here, move it back to downloaded_mods
            if (installedALOT && alotMainFile.UnpackedSingleFilename != null)
            {
                //ALOT was just installed. We are going to move it back to mods folder
                string extractedName = alotMainFile.UnpackedSingleFilename;

                Log.Information("ALOT MAIN FILE - Unpacked - moving to downloaded_mods from install dir: " + extractedName);
                string source = getOutputDir(INSTALLING_THREAD_GAME) + "000_" + extractedName;
                string dest = DOWNLOADED_MODS_DIRECTORY + "\\" + extractedName;

                if (File.Exists(source))
                {
                    try
                    {
                        if (File.Exists(dest))
                        {
                            File.Delete(dest);
                        }
                        File.Move(source, dest);
                        Log.Information("Moved main alot file back to downloaded_mods");
                        //Delete original
                        dest = DOWNLOADED_MODS_DIRECTORY + "\\" + alotMainFile.Filename;
                        if (File.Exists(dest))
                        {
                            Log.Information("Deleting original alot archive file from downloaded_mods");
                            File.Delete(dest);
                            Log.Information("Deleted original alot archive file from downloaded_mods");
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

            InstallWorker.ReportProgress(0, new ThreadCommand(HIDE_STAGES_LABEL));

            string taskString = "Installation of ALOT for Mass Effect" + getGameNumberSuffix(INSTALLING_THREAD_GAME);
            if (!installedALOT)
            {
                //use different end string

                if (justInstalledUpdate > 0)
                {
                    //installed update
                    taskString = "Installation of ALOT Update for Mass Effect" + getGameNumberSuffix(INSTALLING_THREAD_GAME);
                }
                else
                {
                    //addon or other files
                    taskString = "Installation of texture mods for Mass Effect" + getGameNumberSuffix(INSTALLING_THREAD_GAME);
                }
            }
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
            e.Result = INSTALL_OK;
        }

        private void RunAndTimeMEMContextBased_Install(string exe, string args, BackgroundWorker installWorker)
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
            Log.Information("Process complete - finished in " + minutes + " minutes " + sec + " seconds");
        }

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
                if (str.StartsWith("[IPC]"))
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
                                    int task = -1;
                                    switch (param)
                                    {
                                        case "STAGE_UNPACKDLC":
                                            task = ProgressWeightPercentages.JOB_UNPACK;
                                            break;
                                        case "STAGE_SCAN":
                                            task = ProgressWeightPercentages.JOB_SCAN;
                                            break;
                                        case "STAGE_INSTALLTEXTURES":
                                            task = ProgressWeightPercentages.JOB_INSTALL;
                                            break;
                                        case "STAGE_SAVING":
                                            task = ProgressWeightPercentages.JOB_SAVE;
                                            break;
                                        case "STAGE_REMOVEMIPMAPS":
                                            task = ProgressWeightPercentages.JOB_REMOVE;
                                            break;
                                        case "STAGE_REPACK":
                                            task = ProgressWeightPercentages.JOB_REPACK;
                                            break;
                                        default:
                                            Log.Error("UNKNOWN STAGE_ADD PARAM: " + param);
                                            return;
                                    }
                                    STAGE_COUNT++;
                                    Log.Information("Stage added to install queue: " + param);
                                    ProgressWeightPercentages.AddTask(task);
                                    break;
                                }
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
                                        Log.Warning("Stage " + CURRENT_STAGE_NUM + " has completed.");
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
                                    string task = "";
                                    switch (CURRENT_STAGE_CONTEXT)
                                    {
                                        case "STAGE_UNPACKDLC":
                                            task = "Unpacking DLC";
                                            break;
                                        case "STAGE_SCAN":
                                            task = "Scanning textures";
                                            break;
                                        case "STAGE_INSTALLTEXTURES":
                                            task = "Installing textures";
                                            break;
                                        case "STAGE_SAVING":
                                            task = "Saving packages";
                                            break;
                                        case "STAGE_REMOVEMIPMAPS":
                                            task = "Removing empty mipmaps";
                                            break;
                                        case "STAGE_REPACK":
                                            task = "Repacking game files";
                                            break;
                                        default:
                                            Log.Error("UNKNOWN STAGE_CONTEXT PARAM: " + param);
                                            return;
                                    }
                                    CurrentTask = task;
                                    int progressval = ProgressWeightPercentages.SubmitProgress(CURRENT_STAGE_NUM, 0);
                                    worker.ReportProgress(completed, new ThreadCommand(UPDATE_CURRENTTASK_NAME, task));
                                    InstallingOverlay_BottomLabel.Text = CurrentTask + " " + CurrentTaskPercent + "%";
                                    InstallingOverlay_OverallLabel.Text = "(" + progressval.ToString() + "%)";
                                    break;
                                }
                            case "PROCESSING_FILE":
                                Log.Information("MEMNoGui processing file: " + param);
                                break;
                            case "OVERALL_PROGRESS": //will be removed
                            case "TASK_PROGRESS":
                                worker.ReportProgress(completed, new ThreadCommand(UPDATE_CURRENT_STAGE_PROGRESS, param));
                                break;
                            case "SET_STAGE_LABEL":
                                worker.ReportProgress(completed, new ThreadCommand(UPDATE_ADDONUI_CURRENTTASK, param));
                                break;
                            case "HIDE_STAGES":
                                worker.ReportProgress(completed, new ThreadCommand(HIDE_STAGES_LABEL));
                                break;
                            case ERROR_TEXTURE_MAP_MISSING:
                                Log.Fatal("[FATAL]Texture map is missing! We cannot install textures");
                                BACKGROUND_MEM_PROCESS_ERRORS.Add(ERROR_TEXTURE_MAP_MISSING);
                                break;
                            case ERROR_TEXTURE_MAP_WRONG:
                                Log.Fatal("[FATAL]Texture map is invalid! We cannot install textures");
                                BACKGROUND_MEM_PROCESS_ERRORS.Add(ERROR_TEXTURE_MAP_WRONG);
                                break;
                            case "ERROR_ADDED_FILE":
                                Log.Error("MEMNoGui detects some game file(s) were added since initial texture installation! This is not supported. Installation aborted");
                                BACKGROUND_MEM_PROCESS_ERRORS.Add(ERROR_FILE_ADDED);
                                break;
                            case "ERROR_REMOVED_FILE":
                                Log.Error("MEMNoGui detects some game file(s) were removed since initial texture installation! This is not supported. Installation aborted");
                                BACKGROUND_MEM_PROCESS_ERRORS.Add(ERROR_FILE_REMOVED);
                                break;
                            case "ERROR":
                                Log.Error("Error IPC from MEM: " + param);
                                BACKGROUND_MEM_PROCESS_ERRORS.Add(param);
                                break;
                            case "PROCESSING_TEXTURE_INSTALL":
                                Log.Information("Installing texture: " + param);
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
                    if (str.Trim() != "")
                    {
                        Log.Information("Realtime Process Output: " + str);
                    }
                }
            };
            BACKGROUND_MEM_PROCESS.Run();
        }

        private void RunAndTimeMEM_Install(string exe, string args, BackgroundWorker installWorker)
        {
            Stopwatch sw = Stopwatch.StartNew();

            runMEM_Install(exe, args, InstallWorker);
            while (BACKGROUND_MEM_PROCESS.State == AppState.Running)
            {
                Thread.Sleep(END_OF_PROCESS_POLL_INTERVAL);
            }
            sw.Stop();
            int minutes = (int)sw.Elapsed.TotalMinutes;
            double fsec = 60 * (sw.Elapsed.TotalMinutes - minutes);
            int sec = (int)fsec;
            Log.Information("Process complete - finished in " + minutes + " minutes " + sec + " seconds");
        }

        private void RunAndTimeMEM_InstallContextBased(string exe, string args, BackgroundWorker installWorker)
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
            Log.Information("Process complete - finished in " + minutes + " minutes " + sec + " seconds");
        }

        private void runMEM_Install(string exe, string args, BackgroundWorker worker, List<string> acceptedIPC = null)
        {
            Debug.WriteLine("Running process: " + exe + " " + args);
            Log.Information("Running process: " + exe + " " + args);


            BACKGROUND_MEM_PROCESS = new ConsoleApp(exe, args);
            BACKGROUND_MEM_PROCESS_ERRORS = new List<string>();
            BACKGROUND_MEM_PROCESS_PARSED_ERRORS = new List<string>();
            BACKGROUND_MEM_PROCESS.ConsoleOutput += (o, args2) =>
            {
                string str = args2.Line;
                if (str.StartsWith("[IPC]"))
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
                            //worker.ReportProgress(completed, new ThreadCommand(UPDATE_PROGRESSBAR_INDETERMINATE, false));
                            //int percentInt = Convert.ToInt32(param);
                            //worker.ReportProgress(percentInt);
                            case "PROCESSING_FILE":
                            case "PROCESSING_MOD":
                            case "PROCESSING_TEXTURE_INSTALL":
                                Log.Information("MEM Reports processing file: " + param);
                                //worker.ReportProgress(completed, new ThreadCommand(UPDATE_OPERATION_LABEL, param));
                                break;
                            case "OVERALL_PROGRESS"://This will be removed later
                            case "TASK_PROGRESS":
                                worker.ReportProgress(completed, new ThreadCommand(UPDATE_CURRENT_STAGE_PROGRESS, param));
                                break;
                            case "PHASE":
                                Log.Warning("Stage " + CURRENT_STAGE_NUM + " has completed.");
                                int overallProgress = ProgressWeightPercentages.SubmitProgress(CURRENT_STAGE_NUM, 100);
                                worker.ReportProgress(completed, new ThreadCommand(SET_OVERALL_PROGRESS, overallProgress));
                                Interlocked.Increment(ref CURRENT_STAGE_NUM);
                                worker.ReportProgress(completed, new ThreadCommand(UPDATE_STAGE_OF_STAGE_LABEL, param));
                                break;
                            case "HIDE_STAGES":
                                worker.ReportProgress(completed, new ThreadCommand(HIDE_STAGES_LABEL));
                                break;
                            case ERROR_TEXTURE_MAP_MISSING:
                                Log.Fatal("[FATAL]Texture map is missing! We cannot install textures");
                                BACKGROUND_MEM_PROCESS_ERRORS.Add(ERROR_TEXTURE_MAP_MISSING);
                                break;
                            case ERROR_TEXTURE_MAP_WRONG:
                                Log.Fatal("[FATAL]Texture map is invalid! We cannot install textures");
                                BACKGROUND_MEM_PROCESS_ERRORS.Add(ERROR_TEXTURE_MAP_WRONG);
                                break;
                            case "ERROR_ADDED_FILE":
                                Log.Error("MEM detects some game file(s) were added since initial texture installation! This is not supported. Installation aborted");
                                BACKGROUND_MEM_PROCESS_ERRORS.Add(ERROR_FILE_ADDED);
                                break;
                            case "ERROR_REMOVED_FILE":
                                Log.Error("MEM detects some game file(s) were removed since initial texture installation! This is not supported. Installation aborted");
                                BACKGROUND_MEM_PROCESS_ERRORS.Add(ERROR_FILE_REMOVED);
                                break;
                            case "ERROR":
                                Log.Error("Error IPC from MEM: " + param);
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
                    if (str.Trim() != "")
                    {
                        Log.Information("Realtime Process Output: " + str);
                    }
                }
            };
            BACKGROUND_MEM_PROCESS.Run();
        }
    }
}