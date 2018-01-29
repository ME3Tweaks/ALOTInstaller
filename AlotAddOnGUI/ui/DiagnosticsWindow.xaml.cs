using AlotAddOnGUI.classes;
using MahApps.Metro.Controls;
using Serilog;
using SlavaGu.ConsoleAppLauncher;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using static AlotAddOnGUI.MainWindow;
using Flurl;
using Flurl.Http;
using System.Diagnostics;
using ByteSizeLib;
using Microsoft.WindowsAPICodePack.Taskbar;

namespace AlotAddOnGUI.ui
{
    /// <summary>
    /// Interaction logic for DiagnosticsWindow.xaml
    /// </summary>
    public partial class DiagnosticsWindow : MetroWindow
    {
        private const string SET_DIAG_TEXT = "SET_DIAG_TEXT";
        private const string SET_DIAGTASK_ICON_WORKING = "SET_DIAGTASK_ICON_WORKING";
        private const string SET_DIAGTASK_ICON_GREEN = "SET_DIAGTASK_ICON_GREEN";
        private const string SET_DIAGTASK_ICON_RED = "SET_DIAGTASK_ICON_RED";
        private const string SET_FULLSCAN_PROGRESS = "SET_STEP_PROGRESS";
        private const string TURN_OFF_TASKBAR_PROGRESS = "TURN_OFF_TASKBAR_PROGRESS";
        private const string TURN_ON_TASKBAR_PROGRESS = "TURN_ON_TASKBAR_PROGRESS";
        private const int CONTEXT_NORMAL = 0;
        private const int CONTEXT_FULLMIPMAP_SCAN = 1;
        private bool TextureCheck = false;
        private static int DIAGNOSTICS_GAME = 0;
        private static ConsoleApp BACKGROUND_MEM_PROCESS;
        private List<string> BACKGROUND_MEM_PROCESS_ERRORS;
        private List<string> BACKGROUND_MEM_PROCESS_PARSED_ERRORS;
        BackgroundWorker diagnosticsWorker;
        private StringBuilder diagStringBuilder;
        private int Context = CONTEXT_NORMAL;
        private bool MEMI_FOUND = true;

        public DiagnosticsWindow()
        {
            InitializeComponent();
            string me1Path = Utilities.GetGamePath(1, false);
            string me2Path = Utilities.GetGamePath(2, false);
            string me3Path = Utilities.GetGamePath(3, false);

            Button_ManualFileME1.IsEnabled = me1Path != null;
            Button_ManualFileME2.IsEnabled = me2Path != null;
            Button_ManualFileME3.IsEnabled = me3Path != null;
        }

        private void Button_DiagnosticsME3_Click(object sender, RoutedEventArgs e)
        {
            Button_ManualFileME1.Visibility = Visibility.Collapsed;
            Button_ManualFileME2.Visibility = Visibility.Collapsed;
            Button_ManualFileME3.Click -= Button_DiagnosticsME3_Click;
            DIAGNOSTICS_GAME = 3;
            ShowDiagnosticTypes();
        }

        private void Button_DiagnosticsME1_Click(object sender, RoutedEventArgs e)
        {
            Button_ManualFileME2.Visibility = Visibility.Collapsed;
            Button_ManualFileME3.Visibility = Visibility.Collapsed;
            Button_ManualFileME1.Click -= Button_DiagnosticsME1_Click;
            DIAGNOSTICS_GAME = 1;
            ShowDiagnosticTypes();
        }

        private void Button_DiagnosticsME2_Click(object sender, RoutedEventArgs e)
        {
            Button_ManualFileME1.Visibility = Visibility.Collapsed;
            Button_ManualFileME3.Visibility = Visibility.Collapsed;
            Button_ManualFileME2.Click -= Button_DiagnosticsME2_Click;
            DIAGNOSTICS_GAME = 2;
            ShowDiagnosticTypes();
        }

        private void ShowDiagnosticTypes()
        {
            DiagnosticHeader.Text = "Select type of diagnostic.";
            Panel_DiagnosticsTypes.Visibility = Visibility.Visible;
        }

        private void RunDiagnostics(int game, bool full)
        {
            Debug.WriteLine("running diags...");
            DiagnosticHeader.Text = "Performing diagnostics...";
            TextureCheck = full;
            if (TextureCheck)
            {
                QuickCheckPanel.Visibility = Visibility.Collapsed;
                FullCheckPanel.Visibility = Visibility.Visible;
            }
            Panel_Progress.Visibility = Visibility.Visible;

            diagnosticsWorker = new BackgroundWorker();
            diagnosticsWorker.WorkerReportsProgress = true;
            diagnosticsWorker.ProgressChanged += DiagnosticsProgressChanged;
            diagnosticsWorker.DoWork += PerformDiagnostics;
            diagnosticsWorker.RunWorkerCompleted += FinishedDiagnostics;
            diagnosticsWorker.RunWorkerAsync();
        }

        private void DiagnosticsProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            ThreadCommand tc = (ThreadCommand)e.UserState;
            switch (tc.Command)
            {
                case SET_DIAGTASK_ICON_GREEN:
                    ((Image)tc.Data).Source = new BitmapImage(new Uri(@"../images/greencheckmark.png", UriKind.Relative));
                    break;
                case SET_DIAGTASK_ICON_WORKING:
                    ((Image)tc.Data).Source = new BitmapImage(new Uri(@"../images/workingicon.png", UriKind.Relative));
                    break;
                case SET_DIAG_TEXT:
                    DiagnosticHeader.Text = (string)tc.Data;
                    break;
                case SET_DIAGTASK_ICON_RED:
                    ((Image)tc.Data).Source = new BitmapImage(new Uri(@"../images/redx_large.png", UriKind.Relative));
                    break;
                case RESTORE_FAILED_COULD_NOT_DELETE_FOLDER:
                    //await this.ShowMessageAsync("Restore failed", "Could not delete the existing game directory. This is usually due to something open or running from within the game folder. Close other programs and try again.");
                    return;
                case UPDATE_OPERATION_LABEL:
                    //AddonFilesLabel.Text = (string)tc.Data;
                    break;
                case SET_FULLSCAN_PROGRESS:
                    int progress = (int)tc.Data;
                    TaskbarManager.Instance.SetProgressValue(progress, 100);
                    TextBlock_FullCheck.Text = "Scanning textures " + progress + "%";
                    break;
                case TURN_OFF_TASKBAR_PROGRESS:
                    TaskbarManager.Instance.SetProgressValue(0, 100);
                    TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.NoProgress);
                    break;
                case TURN_ON_TASKBAR_PROGRESS:
                    TaskbarManager.Instance.SetProgressValue(0, 100);
                    TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.Normal);
                    break;
                case UPDATE_HEADER_LABEL:
                    //HeaderLabel.Text = (string)tc.Data;
                    break;
                case UPDATE_PROGRESSBAR_INDETERMINATE:
                    //TaskbarManager.Instance.SetProgressState((bool)tc.Data ? TaskbarProgressBarState.Indeterminate : TaskbarProgressBarState.Normal);
                    //Build_ProgressBar.IsIndeterminate = (bool)tc.Data;
                    break;
                case ERROR_OCCURED:
                    //Build_ProgressBar.IsIndeterminate = false;
                    //Build_ProgressBar.Value = 0;
                    //await this.ShowMessageAsync("Error building Addon MEM Package", "An error occured building the addon. The logs will provide more information. The error message given is:\n" + (string)tc.Data);
                    break;
                case SHOW_DIALOG:
                    //KeyValuePair<string, string> messageStr = (KeyValuePair<string, string>)tc.Data;
                    //await this.ShowMessageAsync(messageStr.Key, messageStr.Value);
                    break;
                //    case SHOW_DIALOG_YES_NO:
                //ThreadCommandDialogOptions tcdo = (ThreadCommandDialogOptions)tc.Data;
                //MetroDialogSettings settings = new MetroDialogSettings();
                //ettings.NegativeButtonText = tcdo.NegativeButtonText;
                //settings.AffirmativeButtonText = tcdo.AffirmativeButtonText;
                //MessageDialogResult result = await this.ShowMessageAsync(tcdo.title, tcdo.message, MessageDialogStyle.AffirmativeAndNegative, settings);
                /*if (result == MessageDialogResult.Negative)
                {
                    CONTINUE_BACKUP_EVEN_IF_VERIFY_FAILS = false;
                }
                else
                {
                    CONTINUE_BACKUP_EVEN_IF_VERIFY_FAILS = true;
                }
                tcdo.signalHandler.Set();*/
                //      break;
                case INCREMENT_COMPLETION_EXTRACTION:
                    //TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.Normal);

                    //Interlocked.Increment(ref completed);
                    //Build_ProgressBar.Value = (completed / (double)ADDONSTOBUILD_COUNT) * 100;

                    break;
            }
        }

        private void FinishedDiagnostics(object sender, RunWorkerCompletedEventArgs e)
        {
            Button_Close.Visibility = Visibility.Visible;
            if (e.Error != null)
            {
                Log.Error("Error performing diagnostics: " + e);
                DiagnosticHeader.Text = "Error occured performing diagnostics.";
                Image_Upload.Source = new BitmapImage(new Uri(@"../images/redx_large.png", UriKind.Relative));

            }
            else if (e.Result != null)
            {
                Uri uriResult;
                bool result = Uri.TryCreate((string)e.Result, UriKind.Absolute, out uriResult)
                    && uriResult.Scheme == Uri.UriSchemeHttp;
                if (result)
                {
                    Clipboard.SetText((string)e.Result);
                    DiagnosticHeader.Text = "Diagnostic completed.\nLink to the result has been copied to the clipboard.";
                    System.Diagnostics.Process.Start((string)e.Result);
                    Image_Upload.Source = new BitmapImage(new Uri(@"../images/greencheckmark.png", UriKind.Relative));
                }
                else
                {
                    DiagnosticHeader.Text = (string)e.Result;
                    Image_Upload.Source = new BitmapImage(new Uri(@"../images/redx_large.png", UriKind.Relative));
                }
            }
            else
            {
                //DiagnosticHeader.Text = "Diagnostic completed but no response from the server was given. Check the logs directory for the file.";
                Image_Upload.Source = new BitmapImage(new Uri(@"../images/redx_large.png", UriKind.Relative));
            }
        }

        private void PerformDiagnostics(object sender, DoWorkEventArgs e)
        {
            diagStringBuilder = new StringBuilder();
            addDiagLine("ALOT Installer " + System.Reflection.Assembly.GetEntryAssembly().GetName().Version + " Game Diagnostic");
            addDiagLine("Diagnostic for Mass Effect " + DIAGNOSTICS_GAME);
            var versInfo = FileVersionInfo.GetVersionInfo(BINARY_DIRECTORY + MEM_EXE_NAME);
            int fileVersion = versInfo.FileMajorPart;
            addDiagLine("Using MassEffectModderNoGui v" + fileVersion);

            addDiagLine("Game is installed at " + Utilities.GetGamePath(DIAGNOSTICS_GAME));
            string exePath = Utilities.GetGameEXEPath(DIAGNOSTICS_GAME);
            if (File.Exists(exePath))
            {
                using (var md5 = MD5.Create())
                {
                    using (var stream = File.OpenRead(exePath))
                    {
                        addDiagLine("Executable hash is " + BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLower());
                    }
                }
            }

            addDiagLine("System Memory: " + ByteSize.FromKiloBytes(Utilities.GetInstalledRamAmount()));


            ALOTVersionInfo avi = Utilities.GetInstalledALOTInfo(DIAGNOSTICS_GAME);
            addDiagLine("====INSTALLED ALOT INFO===");
            if (avi == null)
            {
                addDiagLine("ALOT Marker file does not have MEMI tag. ALOT/MEUITM not installed, or at least not installed through an installer.");
                MEMI_FOUND = false;
            }
            else
            {
                addDiagLine("ALOT Version: " + avi.ALOTVER + "." + avi.ALOTUPDATEVER + "." + avi.ALOTHOTFIXVER);
                if (DIAGNOSTICS_GAME == 1)
                {
                    addDiagLine("MEUITM: " + avi.MEUITMVER);
                }
            }


            string exe = BINARY_DIRECTORY + MEM_EXE_NAME;
            string args = "-check-game-data-mismatch " + DIAGNOSTICS_GAME + " -ipc";
            diagnosticsWorker.ReportProgress(0, new ThreadCommand(SET_DIAGTASK_ICON_WORKING, Image_DataMismatch));
            runMEM_Diagnostics(exe, args, diagnosticsWorker);
            WaitForMEM();
            addDiagLine("");
            addDiagLine("===Files added (or removed) before/after ALOT/MEUITM install===");

            if (BACKGROUND_MEM_PROCESS_PARSED_ERRORS.Count > 0)
            {
                addDiagLine("Diagnostic reports some files appear to have been added or removed since texture scan took place:");
                foreach (String str in BACKGROUND_MEM_PROCESS_PARSED_ERRORS)
                {
                    addDiagLine(" - " + str);
                }
            }
            else
            {
                addDiagLine("Diagnostic reports no files appear to have been added or removed since texture scan took place.");
            }
            diagnosticsWorker.ReportProgress(0, new ThreadCommand(SET_DIAGTASK_ICON_GREEN, Image_DataMismatch));

            args = "-check-game-data-after " + DIAGNOSTICS_GAME + " -ipc";
            diagnosticsWorker.ReportProgress(0, new ThreadCommand(SET_DIAGTASK_ICON_WORKING, Image_DataAfter));
            runMEM_Diagnostics(exe, args, diagnosticsWorker);
            WaitForMEM();
            addDiagLine("\n===Replaced files scan (after textures were installed)===");

            if (BACKGROUND_MEM_PROCESS_PARSED_ERRORS.Count > 0)
            {
                addDiagLine("Diagnostic reports some files appear to have been replaced after textures were installed:");
                foreach (String str in BACKGROUND_MEM_PROCESS_PARSED_ERRORS)
                {
                    addDiagLine(" - " + str);
                }
            }
            else
            {
                if (BACKGROUND_MEM_PROCESS.ExitCode != null && BACKGROUND_MEM_PROCESS.ExitCode == 0)
                {
                    addDiagLine("Diagnostic reports no files appear to have been replaced after textures were installed.");
                }
                else
                {
                    addDiagLine("MEM returned non zero exit code, or null (crash) during -check-game-data-after: " + BACKGROUND_MEM_PROCESS.ExitCode);
                }
            }
            diagnosticsWorker.ReportProgress(0, new ThreadCommand(SET_DIAGTASK_ICON_GREEN, Image_DataAfter));


            //FULL CHECK
            if (TextureCheck)
            {
                diagnosticsWorker.ReportProgress(0, new ThreadCommand(SET_DIAGTASK_ICON_WORKING, Image_FullCheck));
                diagnosticsWorker.ReportProgress(0, new ThreadCommand(TURN_ON_TASKBAR_PROGRESS));
                args = "-check-game-data-textures " + DIAGNOSTICS_GAME + " -ipc";
                Context = CONTEXT_FULLMIPMAP_SCAN;
                runMEM_Diagnostics(exe, args, diagnosticsWorker);
                WaitForMEM();
                Context = CONTEXT_NORMAL;

                if (BACKGROUND_MEM_PROCESS_PARSED_ERRORS.Count > 0)
                {
                    addDiagLine("Full texture check reported errors:");
                    foreach (String str in BACKGROUND_MEM_PROCESS_PARSED_ERRORS)
                    {
                        addDiagLine(" - " + str);
                    }
                }
                else
                {
                    if (BACKGROUND_MEM_PROCESS.ExitCode != null && BACKGROUND_MEM_PROCESS.ExitCode == 0)
                    {
                        addDiagLine("Diagnostics textures check (full) did not find any issues.");
                    }
                    else
                    {
                        addDiagLine("MEM returned non zero exit code, or null (crash) during -check-game-data-textures: " + BACKGROUND_MEM_PROCESS.ExitCode);
                    }
                }
                diagnosticsWorker.ReportProgress(0, new ThreadCommand(TURN_OFF_TASKBAR_PROGRESS));
                diagnosticsWorker.ReportProgress(0, new ThreadCommand(SET_DIAGTASK_ICON_GREEN, Image_FullCheck));
            }
            args = "-detect-mods " + DIAGNOSTICS_GAME + " -ipc";
            diagnosticsWorker.ReportProgress(0, new ThreadCommand(SET_DIAGTASK_ICON_WORKING, Image_DataBasegamemods));
            runMEM_Diagnostics(exe, args, diagnosticsWorker);
            WaitForMEM();
            if (BACKGROUND_MEM_PROCESS_PARSED_ERRORS.Count > 0)
            {
                addDiagLine("The following mods were detected:");
                foreach (String str in BACKGROUND_MEM_PROCESS_PARSED_ERRORS)
                {
                    addDiagLine(" - " + str);
                }
            }
            else
            {
                addDiagLine("Diagnostics did not detect any mods (-detect-mods) - if the files had their textures updated already, this detection will likely not be accurate.");
            }

            args = "-detect-bad-mods " + DIAGNOSTICS_GAME + " -ipc";
            runMEM_Diagnostics(exe, args, diagnosticsWorker);
            WaitForMEM();
            if (BACKGROUND_MEM_PROCESS_PARSED_ERRORS.Count > 0)
            {
                addDiagLine("Diagnostic reports the following incompatible mods are installed:");
                foreach (String str in BACKGROUND_MEM_PROCESS_PARSED_ERRORS)
                {
                    addDiagLine(" - " + str);
                }
            }
            else
            {
                addDiagLine("Diagnostic did not find any bad mods installed - if the files were updated already this detection may not be accurate");
            }

            //Get LODs
            String lodStr = GetLODStr(DIAGNOSTICS_GAME);
            addDiagLine("");
            addDiagLine("=====LOD INFORMATION=====");
            addDiagLine(lodStr);

            diagnosticsWorker.ReportProgress(0, new ThreadCommand(SET_DIAGTASK_ICON_GREEN, Image_DataBasegamemods));



            diagnosticsWorker.ReportProgress(0, new ThreadCommand(SET_DIAGTASK_ICON_WORKING, Image_Upload));

            string diag = diagStringBuilder.ToString();
            string hash = Utilities.sha256(diag);
            diag += hash;

            string date = DateTime.Now.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
            string diagfilename = EXE_DIRECTORY + "\\logs\\diagnostic_me" + DIAGNOSTICS_GAME + "_" + date + ".txt";
            System.IO.File.WriteAllText(diagfilename, diag);

            //upload
            if (diag.Length < 512000)
            {

                string alotInstallerVer = System.Reflection.Assembly.GetEntryAssembly().GetName().Version.ToString();
                var responseString = "https://me3tweaks.com/alotinstaller/loguploader".PostUrlEncodedAsync(new { LogData = diag, ALOTInstallerVersion = alotInstallerVer }).ReceiveString().Result;
                Uri uriResult;
                bool result = Uri.TryCreate(responseString, UriKind.Absolute, out uriResult)
                    && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
                if (result)
                {
                    //should be valid URL.
                    diagnosticsWorker.ReportProgress(0, new ThreadCommand(SET_DIAGTASK_ICON_GREEN, Image_Upload));
                    e.Result = responseString;
                    Log.Information("Result from server for log upload: " + responseString);
                }
                else
                {
                    diagnosticsWorker.ReportProgress(0, new ThreadCommand(SET_DIAG_TEXT, "Error from relay: " + responseString));
                    diagnosticsWorker.ReportProgress(0, new ThreadCommand(SET_DIAGTASK_ICON_RED, Image_Upload));
                    Log.Error("Error uploading log. The server responded with: " + responseString);
                }
            }
            else
            {
                e.Result = "Diagnostic complete.";
                Log.Warning("Log is too big to upload to pastebin");
                Utilities.OpenAndSelectFileInExplorer(diagfilename);
            }
        }

        private string GetLODStr(int gameID)
        {
            string log = "";
            string iniPath = IniSettingsHandler.GetConfigIniPath(gameID);
            if (File.Exists(iniPath))
            {
                IniFile engineConf = new IniFile(iniPath);
                switch (gameID)
                {
                    case 1:
                        log += "TEXTUREGROUP_World=" + engineConf.Read("TEXTUREGROUP_World", "TextureLODSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_WorldNormalMap=" + engineConf.Read("TEXTUREGROUP_WorldNormalMap", "TextureLODSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_AmbientLightMap=" + engineConf.Read("TEXTUREGROUP_AmbientLightMap", "TextureLODSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_LightAndShadowMap=" + engineConf.Read("TEXTUREGROUP_LightAndShadowMap", "TextureLODSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_Environment_64=" + engineConf.Read("TEXTUREGROUP_Environment_64", "TextureLODSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_Environment_128=" + engineConf.Read("TEXTUREGROUP_Environment_128", "TextureLODSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_Environment_256=" + engineConf.Read("TEXTUREGROUP_Environment_256", "TextureLODSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_Environment_512=" + engineConf.Read("TEXTUREGROUP_Environment_512", "TextureLODSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_Environment_1024=" + engineConf.Read("TEXTUREGROUP_Environment_1024", "TextureLODSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_VFX_64=" + engineConf.Read("TEXTUREGROUP_VFX_64", "TextureLODSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_VFX_128=" + engineConf.Read("TEXTUREGROUP_VFX_128", "TextureLODSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_VFX_256=" + engineConf.Read("TEXTUREGROUP_VFX_256", "TextureLODSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_VFX_512" + engineConf.Read("TEXTUREGROUP_VFX_512", "TextureLODSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_VFX_1024=" + engineConf.Read("TEXTUREGROUP_VFX_1024", "TextureLODSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_APL_128=" + engineConf.Read("TEXTUREGROUP_APL_128", "TextureLODSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_APL_256=" + engineConf.Read("TEXTUREGROUP_APL_256", "TextureLODSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_APL_512=" + engineConf.Read("TEXTUREGROUP_APL_512", "TextureLODSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_APL_1024=" + engineConf.Read("TEXTUREGROUP_APL_1024", "TextureLODSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_GUI=" + engineConf.Read("TEXTUREGROUP_GUI", "TextureLODSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_Promotional=" + engineConf.Read("TEXTUREGROUP_Promotional", "TextureLODSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_Character_1024=" + engineConf.Read("TEXTUREGROUP_Character_1024", "TextureLODSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_Character_Diff=" + engineConf.Read("TEXTUREGROUP_Character_Diff", "TextureLODSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_Character_Norm=" + engineConf.Read("TEXTUREGROUP_Character_Norm", "TextureLODSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_Character_Spec=" + engineConf.Read("TEXTUREGROUP_Character_Spec", "TextureLODSettings") + Environment.NewLine;
                        break;
                    case 2:
                        log += "TEXTUREGROUP_World=" + engineConf.Read("TEXTUREGROUP_World", "SystemSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_WorldNormalMap=" + engineConf.Read("TEXTUREGROUP_WorldNormalMap", "SystemSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_AmbientLightMap=" + engineConf.Read("TEXTUREGROUP_AmbientLightMap", "SystemSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_LightAndShadowMap=" + engineConf.Read("TEXTUREGROUP_LightAndShadowMap", "SystemSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_RenderTarget=" + engineConf.Read("TEXTUREGROUP_RenderTarget", "SystemSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_Environment_64=" + engineConf.Read("TEXTUREGROUP_Environment_64", "SystemSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_Environment_128=" + engineConf.Read("TEXTUREGROUP_Environment_128", "SystemSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_Environment_256=" + engineConf.Read("TEXTUREGROUP_Environment_256", "SystemSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_Environment_512=" + engineConf.Read("TEXTUREGROUP_Environment_512", "SystemSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_Environment_1024=" + engineConf.Read("TEXTUREGROUP_Environment_1024", "SystemSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_VFX_64=" + engineConf.Read("TEXTUREGROUP_VFX_64", "SystemSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_VFX_128=" + engineConf.Read("TEXTUREGROUP_VFX_128", "SystemSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_VFX_256=" + engineConf.Read("TEXTUREGROUP_VFX_256", "SystemSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_VFX_512=" + engineConf.Read("TEXTUREGROUP_VFX_512", "SystemSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_VFX_1024=" + engineConf.Read("TEXTUREGROUP_VFX_1024", "SystemSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_APL_128=" + engineConf.Read("TEXTUREGROUP_APL_128", "SystemSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_APL_256=" + engineConf.Read("TEXTUREGROUP_APL_256", "SystemSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_APL_512=" + engineConf.Read("TEXTUREGROUP_APL_512", "SystemSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_APL_1024=" + engineConf.Read("TEXTUREGROUP_APL_1024", "SystemSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_UI=" + engineConf.Read("TEXTUREGROUP_UI", "SystemSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_Promotional=" + engineConf.Read("TEXTUREGROUP_Promotional", "SystemSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_Character_1024=" + engineConf.Read("TEXTUREGROUP_Character_1024", "SystemSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_Character_Diff=" + engineConf.Read("TEXTUREGROUP_Character_Diff", "SystemSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_Character_Norm=" + engineConf.Read("TEXTUREGROUP_Character_Norm", "SystemSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_Character_Spec=" + engineConf.Read("TEXTUREGROUP_Character_Spec", "SystemSettings") + Environment.NewLine;
                        break;
                    case 3:
                        log += "TEXTUREGROUP_World=" + engineConf.Read("TEXTUREGROUP_World", "SystemSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_WorldSpecular=" + engineConf.Read("TEXTUREGROUP_WorldSpecular", "SystemSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_WorldNormalMap=" + engineConf.Read("TEXTUREGROUP_WorldNormalMap", "SystemSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_AmbientLightMap=" + engineConf.Read("TEXTUREGROUP_AmbientLightMap", "SystemSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_LightAndShadowMap=" + engineConf.Read("TEXTUREGROUP_LightAndShadowMap", "SystemSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_RenderTarget=" + engineConf.Read("TEXTUREGROUP_RenderTarget", "SystemSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_Environment_64=" + engineConf.Read("TEXTUREGROUP_Environment_64", "SystemSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_Environment_128=" + engineConf.Read("TEXTUREGROUP_Environment_128", "SystemSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_Environment_256=" + engineConf.Read("TEXTUREGROUP_Environment_256", "SystemSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_Environment_512=" + engineConf.Read("TEXTUREGROUP_Environment_512", "SystemSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_Environment_1024=" + engineConf.Read("TEXTUREGROUP_Environment_1024", "SystemSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_VFX_64=" + engineConf.Read("TEXTUREGROUP_VFX_64", "SystemSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_VFX_128=" + engineConf.Read("TEXTUREGROUP_VFX_128", "SystemSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_VFX_256=" + engineConf.Read("TEXTUREGROUP_VFX_256", "SystemSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_VFX_512=" + engineConf.Read("TEXTUREGROUP_VFX_512", "SystemSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_VFX_1024=" + engineConf.Read("TEXTUREGROUP_VFX_1024", "SystemSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_APL_128=" + engineConf.Read("TEXTUREGROUP_APL_128", "SystemSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_APL_256=" + engineConf.Read("TEXTUREGROUP_APL_256", "SystemSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_APL_512=" + engineConf.Read("TEXTUREGROUP_APL_512", "SystemSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_APL_1024=" + engineConf.Read("TEXTUREGROUP_APL_1024", "SystemSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_UI=" + engineConf.Read("TEXTUREGROUP_UI", "SystemSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_Promotional=" + engineConf.Read("TEXTUREGROUP_Promotional", "SystemSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_Character_1024=" + engineConf.Read("TEXTUREGROUP_Character_1024", "SystemSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_Character_Diff=" + engineConf.Read("TEXTUREGROUP_Character_Diff", "SystemSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_Character_Norm=" + engineConf.Read("TEXTUREGROUP_Character_Norm", "SystemSettings") + Environment.NewLine;
                        log += "TEXTUREGROUP_Character_Spec=" + engineConf.Read("TEXTUREGROUP_Character_Spec", "SystemSettings") + Environment.NewLine;
                        break;
                }
            }
            else
            {
                log += "Could not find LOD config file: " + iniPath;

            }
            return log;
        }

        private void addDiagLine(string v)
        {
            if (diagStringBuilder == null)
            {
                diagStringBuilder = new StringBuilder();
            }
            diagStringBuilder.Append(v);
            diagStringBuilder.Append("\n");
        }

        private void WaitForMEM()
        {
            while (BACKGROUND_MEM_PROCESS.State == AppState.Running)
            {
                Thread.Sleep(250);
            }
        }

        private void runMEM_Diagnostics(string exe, string args, BackgroundWorker worker, List<string> acceptedIPC = null)
        {
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
                    if (endOfCommand >= 0)
                    {
                        command = command.Substring(0, endOfCommand);
                    }
                    if (acceptedIPC == null || acceptedIPC.Contains(command))
                    {
                        string param = str.Substring(endOfCommand + 5).Trim();
                        switch (command)
                        {
                            case "ERROR_REMOVED_FILE":
                                BACKGROUND_MEM_PROCESS_PARSED_ERRORS.Add("DIAG ERROR: File was removed after textures scan: " + param);
                                break;
                            case "ERROR_ADDED_FILE":
                                BACKGROUND_MEM_PROCESS_PARSED_ERRORS.Add("DIAG ERROR: File was added after textures scan: " + param);
                                break;
                            case "ERROR_VANILLA_MOD_FILE":
                                if (MEMI_FOUND)
                                {
                                    BACKGROUND_MEM_PROCESS_PARSED_ERRORS.Add("DIAG ERROR: File missing MEM/MEMNOGUI marker was found: " + param);
                                }
                                break;
                            case "MOD":
                                BACKGROUND_MEM_PROCESS_PARSED_ERRORS.Add("Detected mod: " + param);
                                break;
                            case "OVERALL_PROGRESS":
                                //worker.ReportProgress(0, new ThreadCommand(UPDATE_PROGRESSBAR_INDETERMINATE, false));
                                int percentInt = Convert.ToInt32(param);
                                if (Context == CONTEXT_FULLMIPMAP_SCAN)
                                {
                                    worker.ReportProgress(0, new ThreadCommand(SET_FULLSCAN_PROGRESS, percentInt));
                                }
                                break;
                            case "PROCESSING_FILE":
                                worker.ReportProgress(0, new ThreadCommand(UPDATE_OPERATION_LABEL, param));
                                break;
                            case "ERROR":
                                Log.Information("[ERROR] Realtime Process Output: " + param);
                                BACKGROUND_MEM_PROCESS_PARSED_ERRORS.Add(param);
                                break;
                            case "ERROR_TEXTURE_SCAN_DIAGNOSTIC":
                            case "ERROR_MIPMAPS_NOT_REMOVED":
                                BACKGROUND_MEM_PROCESS_PARSED_ERRORS.Add(param);
                                break;
                            case "ERROR_FILE_NOT_COMPATIBLE":
                                Log.Error("MEM reporting file is not compatible: " + param);
                                BACKGROUND_MEM_PROCESS_PARSED_ERRORS.Add(param);
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

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void FullDiagnostic_Click(object sender, RoutedEventArgs e)
        {
            Button_QuickDiagnostic.Visibility = Visibility.Collapsed;
            Button_FullDiagnostic.Click -= FullDiagnostic_Click;
            RunDiagnostics(DIAGNOSTICS_GAME, true);
        }

        private void QuickDiagnostic_Click(object sender, RoutedEventArgs e)
        {
            Button_FullDiagnostic.Visibility = Visibility.Collapsed;
            Button_QuickDiagnostic.Click -= QuickDiagnostic_Click;
            RunDiagnostics(DIAGNOSTICS_GAME, false);
        }
    }
}
