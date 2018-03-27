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
        private bool TELEMETRY_IS_FULL_NEW_INSTALL;
        private const int RESULT_UNPACK_FAILED = -40;
        private const int RESULT_SCAN_REMOVE_FAILED = -41;
        private const int RESULT_ME1LAA_FAILED = -43;
        private const int RESULT_TEXTUREINSTALL_NO_TEXTUREMAP = -44;
        private const int RESULT_TEXTUREINSTALL_INVALID_TEXTUREMAP = -45;
        private const int RESULT_TEXTUREINSTALL_GAME_FILE_REMOVED = -47;
        private const int RESULT_TEXTUREINSTALL_GAME_FILE_ADDED = -48;
        private const int RESULT_TEXTUREINSTALL_FAILED = -42;

        private const int RESULT_SAVING_FAILED = -49;
        private const int RESULT_REMOVE_MIPMAPS_FAILED = -50;
        private const int RESULT_REPACK_FAILED = -46;
        private const int RESULT_UNKNOWN_ERROR = -51;
        private const int RESULT_SCAN_FAILED = -52;
        private const int RESULT_BIOGAME_MISSING = -53;

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

        private void InstallALOT(int game, List<AddonFile> filesToInstall)
        {
            MEM_INSTALL_TIME_SECONDS = 0;
            ADDONFILES_TO_INSTALL = filesToInstall;
            WARN_USER_OF_EXIT = true;
            InstallingOverlay_TopLabel.Text = "Preparing installer";
            InstallWorker = new BackgroundWorker();
            //if (USING_BETA)
            //{
            InstallWorker.DoWork += InstallALOTContextBased;
            //}
            //else
            //{
            // InstallWorker.DoWork += InstallALOT;
            //}
            InstallWorker.WorkerReportsProgress = true;
            InstallWorker.ProgressChanged += InstallWorker_ProgressChanged;
            InstallWorker.RunWorkerCompleted += InstallCompleted;
            INSTALLING_THREAD_GAME = game;
            WindowButtonCommandsOverlayBehavior = WindowCommandsOverlayBehavior.Flyouts;
            InstallingOverlayFlyout.Theme = FlyoutTheme.Dark;

            //Set BG for this game
            string bgPath = "images/me" + game + "_bg.jpg";
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
            REPACK_GAME_FILES = Checkbox_RepackGameFiles.IsChecked.Value && ((INSTALLING_THREAD_GAME == 2 && ME2_REPACK_MANIFEST_ENABLED) || (INSTALLING_THREAD_GAME == 3 && ME3_REPACK_MANIFEST_ENABLED));
            Log.Information("Repack option enabled: " + REPACK_GAME_FILES);
            //REPACK_GAME_FILES = true;
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
                    ProgressBarValue = 0;
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
            STAGE_DONE_REACHED = false;
            CurrentTask = "";
            Log.Information("InstallWorker Thread starting for ME" + INSTALLING_THREAD_GAME);
            Log.Information("This installer session is context based and will run in a single instance.");
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
            args += " -ipc -alot-mode";
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
                    Log.Warning("Application exited with in context: " + CURRENT_STAGE_CONTEXT);
                    switch (CURRENT_STAGE_CONTEXT)
                    {
                        case "STAGE_UNPACKDLC":
                            Log.Error("MassEffectModderNoGui exited or crashed while unpacking DLC");
                            e.Result = RESULT_UNPACK_FAILED;
                            break;
                        case "STAGE_SCAN":
                            Log.Error("MassEffectModderNoGui exited or crashed while scanning textures");
                            e.Result = RESULT_SCAN_FAILED;
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
                            e.Result = RESULT_UNKNOWN_ERROR;
                            break;
                    }

                    InstallWorker.ReportProgress(0, new ThreadCommand(HIDE_TIPS));
                    InstallWorker.ReportProgress(0, new ThreadCommand(HIDE_LOD_LIMIT));
                    return;
                }
            }
            overallProgress = ProgressWeightPercentages.SubmitProgress(CURRENT_STAGE_NUM, 100);
            InstallWorker.ReportProgress(0, new ThreadCommand(SET_OVERALL_PROGRESS, overallProgress));

            InstallWorker.ReportProgress(0, new ThreadCommand(UPDATE_OVERALL_TASK, "Finishing installation"));
            InstallWorker.ReportProgress(0, new ThreadCommand(HIDE_STAGES_LABEL));

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
                                File.Delete(localusershaderscache);
                                Log.Information("Deleted user localshadercache: " + localusershaderscache);

                                string gamelocalshadercache = Path.Combine(Utilities.GetGamePath(INSTALLING_THREAD_GAME), @"BioGame\CookedPC\LocalShaderCache-PC-D3D-SM3.upk");
                                File.Delete(gamelocalshadercache);
                                Log.Information("Deleted game localshadercache: " + gamelocalshadercache);

                                
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
            CurrentTask = "Updating Mass Effect" + getGameNumberSuffix(INSTALLING_THREAD_GAME) + "'s graphics settings";
            InstallWorker.ReportProgress(0, new ThreadCommand(UPDATE_CURRENTTASK_NAME, CurrentTask));
            InstallWorker.ReportProgress(0, new ThreadCommand(HIDE_LOD_LIMIT, CurrentTask));

            args = "-apply-lods-gfx ";
            if (hasSoftShadowsMEUITM)
            {
                args += "-soft-shadows-mode ";
            }
            args += INSTALLING_THREAD_GAME;
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
            bool showMarkerFailedMessage = false;
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
            }
            //Install Binkw32
            if (INSTALLING_THREAD_GAME == 2 || INSTALLING_THREAD_GAME == 3)
            {
                Utilities.InstallBinkw32Bypass(INSTALLING_THREAD_GAME);
                if (INSTALLING_THREAD_GAME == 3)
                {
                    Utilities.InstallME3LoggerASI();
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
                    Log.Information("ALOT MAIN FILE - Unpacked - moving to downloaded_mods from install dir: " + extractedName);
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
                            if (File.Exists(dest))
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
            int telemetryfailedcode = 1;
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
                            telemetryfailedcode = 0;
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
                    case RESULT_SCAN_FAILED:
                        {
                            InstallingOverlay_TopLabel.Text = "Failed to scan textures";
                            InstallingOverlay_BottomLabel.Text = "Check the logs for details";
                            if (INSTALLING_THREAD_GAME == 3)
                            {
                                HeaderLabel.Text = "Failed to scan textures. Any packed DLC has now been unpacked, it may not pass authentication.";
                            }
                            else
                            {
                                HeaderLabel.Text = "Failed to scan textures. Your game has not been modified.";
                            }
                            break;
                        }
                    case RESULT_BIOGAME_MISSING:
                        {
                            InstallingOverlay_TopLabel.Text = "BIOGame directory is missing";
                            InstallingOverlay_BottomLabel.Text = "Game needs to be reinstalled, see logs";
                            HeaderLabel.Text = "BIOGame directory is missing. This means the installation is completely unusable.\nCheck logs for more information about this.";
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
                    case RESULT_UNKNOWN_ERROR:
                        {
                            InstallingOverlay_TopLabel.Text = "Unknown error has occured";
                            InstallingOverlay_BottomLabel.Text = "Check the logs for more details";
                            HeaderLabel.Text = "Error occured during installation.";
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
            int Game = INSTALLING_THREAD_GAME;
            List<AddonFile> addonFilesInstalled = ADDONFILES_TO_INSTALL;
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
                        var dlcPath = Utilities.GetGamePath(Game);
                        string pathroot = Path.GetPathRoot(dlcPath);
                        pathroot = pathroot.Substring(0, 1);
                        if (pathroot == @"\")
                        {
                            diskType = -2; //-2 = UNC
                        }
                        else if (Utilities.IsWindows8OrNewer())
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
                                dlcPath = Path.Combine(dlcPath, "DLC");
                                break;
                            case 2:
                            case 3:
                                dlcPath = Path.Combine(dlcPath, "BIOGame", "DLC");
                                break;
                        }
                        if (Directory.Exists(dlcPath))
                        {
                            var directories = Directory.EnumerateDirectories(dlcPath);
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
                                        case "STAGE_PRESCAN":
                                            task = ProgressWeightPercentages.JOB_PRESCAN;
                                            break;
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
                                    ProgressWeightPercentages.AddTask(task, INSTALLING_THREAD_GAME);
                                    break;
                                }
                            case "STAGE_WEIGHT":
                                string[] parameters = param.Split(' ');
                                try
                                {
                                    double scale = Double.Parse(parameters[1]);
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
                                        case "STAGE_PRESCAN":
                                            task = "Performing installation prescan";
                                            break;
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
                                    //InstallingOverlay_BottomLabel.Text = CurrentTask + " " + CurrentTaskPercent + "%";
                                    //InstallingOverlay_OverallLabel.Text = "(" + progressval.ToString() + "%)";
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
                                worker.ReportProgress(completed, new ThreadCommand(UPDATE_STAGE_OF_STAGE_LABEL));
                                worker.ReportProgress(completed, new ThreadCommand(UPDATE_CURRENTTASK_NAME, param));

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