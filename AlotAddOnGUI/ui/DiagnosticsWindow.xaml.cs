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
using System.Management;
using Microsoft.Win32;
using MahApps.Metro.Controls.Dialogs;
using System.Linq;

namespace AlotAddOnGUI.ui
{
    /// <summary>
    /// Interaction logic for DiagnosticsWindow.xaml
    /// </summary>
    public partial class DiagnosticsWindow : MetroWindow
    {
        public const string SHOW_DIALOG_BAD_LOD = "SHOW_DIALOG_BAD_LOD";
        private const string SET_DIAG_TEXT = "SET_DIAG_TEXT";
        private const string SET_DIAGTASK_ICON_WORKING = "SET_DIAGTASK_ICON_WORKING";
        private const string SET_DIAGTASK_ICON_GREEN = "SET_DIAGTASK_ICON_GREEN";
        private const string SET_DIAGTASK_ICON_RED = "SET_DIAGTASK_ICON_RED";
        private const string SET_FULLSCAN_PROGRESS = "SET_STEP_PROGRESS";
        private const string SET_REPLACEDFILE_PROGRESS = "SET_REPLACEDFILE_PROGRESS";
        private const string RESET_REPLACEFILE_TEXT = "RESET_REPLACEFILE_TEXT";
        private const string TURN_OFF_TASKBAR_PROGRESS = "TURN_OFF_TASKBAR_PROGRESS";
        private const string TURN_ON_TASKBAR_PROGRESS = "TURN_ON_TASKBAR_PROGRESS";
        private const string UPLOAD_LINKED_LOG = "UPLOAD_LINKED_LOG";
        private const int CONTEXT_NORMAL = 0;
        private const int CONTEXT_FULLMIPMAP_SCAN = 1;
        private const int CONTEXT_REPLACEDFILE_SCAN = 2;
        private bool TextureCheck = false;
        private static int DIAGNOSTICS_GAME = 0;
        private static ConsoleApp BACKGROUND_MEM_PROCESS;
        private List<string> BACKGROUND_MEM_PROCESS_ERRORS;
        private List<string> BACKGROUND_MEM_PROCESS_PARSED_ERRORS;
        BackgroundWorker diagnosticsWorker;
        private StringBuilder diagStringBuilder;
        private int Context = CONTEXT_NORMAL;
        private bool MEMI_FOUND = true;
        private bool FIXED_LOD_SETTINGS = false;
        private List<string> AddedFiles = new List<string>();
        private string LINKEDLOGURL;

        public DiagnosticsWindow()
        {
            InitializeComponent();
            string me1Path = Utilities.GetGamePath(1, false);
            string me2Path = Utilities.GetGamePath(2, false);
            string me3Path = Utilities.GetGamePath(3, false);

            Button_ManualFileME1.IsEnabled = me1Path != null;
            Button_ManualFileME2.IsEnabled = me2Path != null;
            Button_ManualFileME3.IsEnabled = me3Path != null;

            if (me1Path == null)
            {
                Button_ManualFileME1.ToolTip = "Mass Effect is not installed";
            }
            if (me2Path == null)
            {
                Button_ManualFileME2.ToolTip = "Mass Effect 2 is not installed";
            }
            if (me3Path == null)
            {
                Button_ManualFileME3.ToolTip = "Mass Effect 3 is not installed";
            }

        }

        private void Button_DiagnosticsME3_Click(object sender, RoutedEventArgs e)
        {
            Button_ManualFileME3.ToolTip = "Diagnostic will be run on Mass Effect 3";
            Button_ManualFileME1.Visibility = Visibility.Collapsed;
            Button_ManualFileME2.Visibility = Visibility.Collapsed;
            Button_ManualFileME3.Visibility = Visibility.Collapsed;
            Image_DiagME3.Visibility = Visibility.Visible;
            DIAGNOSTICS_GAME = 3;
            ShowDiagnosticTypes();
        }

        private void Button_DiagnosticsME1_Click(object sender, RoutedEventArgs e)
        {
            Image_DiagME1.ToolTip = "Diagnostic will be run on Mass Effect";
            Button_ManualFileME1.Visibility = Visibility.Collapsed;
            Button_ManualFileME2.Visibility = Visibility.Collapsed;
            Button_ManualFileME3.Visibility = Visibility.Collapsed;
            Image_DiagME1.Visibility = Visibility.Visible;
            DIAGNOSTICS_GAME = 1;
            ShowDiagnosticTypes();
        }

        private void Button_DiagnosticsME2_Click(object sender, RoutedEventArgs e)
        {
            Image_DiagME2.ToolTip = "Diagnostic will be run on Mass Effect 2";
            Button_ManualFileME1.Visibility = Visibility.Collapsed;
            Button_ManualFileME2.Visibility = Visibility.Collapsed;
            Button_ManualFileME3.Visibility = Visibility.Collapsed;
            Image_DiagME2.Visibility = Visibility.Visible;
            DIAGNOSTICS_GAME = 2;
            ShowDiagnosticTypes();
        }

        private void ShowDiagnosticTypes()
        {
            DiagnosticHeader.Text = "Select type of diagnostic.";
            ALOTVersionInfo avi = Utilities.GetInstalledALOTInfo(DIAGNOSTICS_GAME);
            if (avi == null)
            {
                Button_FullDiagnostic.IsEnabled = false;
                Button_FullDiagnostic.ToolTip = "MEMI tag missing - full scan won't provide any useful info";
            }
            Panel_DiagnosticsTypes.Visibility = Visibility.Visible;
        }

        private void RunDiagnostics(int game, bool full)
        {
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

        private async void DiagnosticsProgressChanged(object sender, ProgressChangedEventArgs e)
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
                case UPDATE_ADDONUI_CURRENTTASK:
                    //AddonFilesLabel.Text = (string)tc.Data;
                    break;
                case SET_FULLSCAN_PROGRESS:
                    {
                        int progress = (int)tc.Data;
                        TaskbarManager.Instance.SetProgressValue(progress, 100);
                        TextBlock_FullCheck.Text = "Scanning textures " + progress + "%";
                        break;
                    }
                case SET_REPLACEDFILE_PROGRESS:
                    {
                        int progress = (int)tc.Data;
                        TextBlock_DataAfter.Text = "Checking for replaced files " + progress + "%";
                        break;
                    }
                case RESET_REPLACEFILE_TEXT:
                    TextBlock_DataAfter.Text = "Check for replaced files";

                    break;
                case TURN_OFF_TASKBAR_PROGRESS:
                    TaskbarManager.Instance.SetProgressValue(0, 100);
                    TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.NoProgress);
                    break;
                case TURN_ON_TASKBAR_PROGRESS:
                    TaskbarManager.Instance.SetProgressValue(0, 100);
                    TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.Normal);
                    break;
                case UPLOAD_LINKED_LOG:
                    LINKEDLOGURL = await ((MainWindow)(Owner)).uploadLatestLog(false, false);
                    ((EventWaitHandle)(tc.Data)).Set();
                    break;
                case SHOW_DIALOG_BAD_LOD:
                    ThreadCommandDialogOptions tcdo = (ThreadCommandDialogOptions)tc.Data;
                    Width = 720;
                    this.Left = Owner.Left + (Owner.Width - this.ActualWidth) / 2;
                    this.Top = Owner.Top + (Owner.Height - this.ActualHeight) / 2;
                    MetroDialogSettings settings = new MetroDialogSettings();
                    settings.NegativeButtonText = "Don't fix";
                    settings.AffirmativeButtonText = "Fix";
                    settings.DefaultButtonFocus = MessageDialogResult.Affirmative;
                    MessageDialogResult result = await this.ShowMessageAsync("Texture settings won't work with current installation", "The current texture settings for the game will cause black textures or the game to possibly crash. It is recommended you restore these settings to their unmodified states to prevent this issue. This will not change your game files, only the texture settings.", MessageDialogStyle.AffirmativeAndNegative, settings);
                    if (result == MessageDialogResult.Affirmative)
                    {
                        Log.Information("Removing bad LOD values from game");
                        string exe = BINARY_DIRECTORY + MEM_EXE_NAME;
                        string args = "-remove-lods " + DIAGNOSTICS_GAME;
                        int returncode = Utilities.runProcess(exe, args);
                        if (returncode == 0)
                        {
                            FIXED_LOD_SETTINGS = true;
                        }
                        else
                        {
                            Log.Warning("Failed to remove LOD settings, return code not 0: " + returncode);
                        }
                    }
                    tcdo.signalHandler.Set();
                    break;
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
                                    && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
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
            bool pairLog = false;
            addDiagLine("ALOT Installer " + System.Reflection.Assembly.GetEntryAssembly().GetName().Version + " Game Diagnostic");
            addDiagLine("Diagnostic for Mass Effect " + DIAGNOSTICS_GAME);
            addDiagLine("Diagnostic generated on " + DateTime.Now.ToShortDateString());
            var versInfo = FileVersionInfo.GetVersionInfo(BINARY_DIRECTORY + MEM_EXE_NAME);
            int fileVersion = versInfo.FileMajorPart;
            addDiagLine("Using MassEffectModderNoGui v" + fileVersion);
            addDiagLine("Game is installed at " + Utilities.GetGamePath(DIAGNOSTICS_GAME));

            ALOTVersionInfo avi = Utilities.GetInstalledALOTInfo(DIAGNOSTICS_GAME);
            MEMI_FOUND = avi != null;

            string exePath = Utilities.GetGameEXEPath(DIAGNOSTICS_GAME);
            string gamePath = Utilities.GetGamePath(DIAGNOSTICS_GAME);
            if (File.Exists(exePath))
            {
                versInfo = FileVersionInfo.GetVersionInfo(exePath);
                addDiagLine("===Executable information");
                addDiagLine("Version: " + versInfo.FileMajorPart + "." + versInfo.FileMinorPart + "." + versInfo.FileBuildPart + "." + versInfo.FilePrivatePart);
                if (DIAGNOSTICS_GAME == 1)
                {
                    bool me1LAAEnabled = Utilities.GetME1LAAEnabled();
                    if (MEMI_FOUND && !me1LAAEnabled)
                    {
                        addDiagLine(" - DIAG ERROR: Large Address Aware: " + me1LAAEnabled + " - ALOT/MEUITM is installed - this will almost certainly cause crashes");
                    }
                    else
                    {
                        addDiagLine("Large Address Aware: " + me1LAAEnabled);
                    }
                }

                using (var md5 = MD5.Create())
                {
                    using (var stream = File.OpenRead(exePath))
                    {
                        addDiagLine("Hash: " + BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLower());
                    }
                }
                string d3d9file = Path.GetDirectoryName(exePath) + "\\d3d9.dll";
                if (File.Exists(d3d9file))
                {
                    addDiagLine("~~~d3d9.dll exists - External dll is hooking via DirectX into game process");
                }
                string fpscounter = Path.GetDirectoryName(exePath) + @"\fpscounter\fpscounter.dll";
                if (File.Exists(fpscounter))
                {
                    addDiagLine("~~~fpscounter.dll exists - FPS Counter plugin detected");
                }
                string dinput8 = Path.GetDirectoryName(exePath) + "\\dinput8.dll";
                if (File.Exists(dinput8))
                {
                    addDiagLine("~~~dinput8.dll exists - External dll is hooking via input dll into game process");
                }
            }


            addDiagLine("===System information");
            OperatingSystem os = Environment.OSVersion;
            Version osBuildVersion = os.Version;

            //Windows 10 only
            string releaseId = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ReleaseId", "").ToString();
            string productName = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ProductName", "").ToString();
            string verLine = "Running " + productName;
            if (osBuildVersion.Major == 10)
            {
                verLine += " " + releaseId;
            }
            addDiagLine(verLine);
            addDiagLine("Version " + osBuildVersion);
            addDiagLine("");
            addDiagLine("Processors");
            addDiagLine(GetCPUString());
            long ramInBytes = Utilities.GetInstalledRamAmount();
            addDiagLine("System Memory: " + ByteSize.FromKiloBytes(ramInBytes));
            if (ramInBytes == 0)
            {
                addDiagLine("~~~Unable to get the read amount of physically installed ram. This may be a sign of impending hardware failure in the SMBIOS");
            }
            ManagementObjectSearcher objvide = new ManagementObjectSearcher("select * from Win32_VideoController");
            int vidCardIndex = 1;
            foreach (ManagementObject obj in objvide.Get())
            {
                addDiagLine("");
                addDiagLine("Video Card " + vidCardIndex);
                addDiagLine("Name: " + obj["Name"]);

                //Get Memory
                string vidKey = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\";
                vidKey += (vidCardIndex - 1).ToString().PadLeft(4, '0');
                long regValue = (long)Registry.GetValue(vidKey, "HardwareInformation.qwMemorySize", 0L);
                string displayVal = "Unable to read value from registry";
                if (regValue != 0)
                {
                    displayVal = ByteSize.FromBytes(regValue).ToString();
                }
                else
                {
                    try
                    {
                        UInt32 wmiValue = (UInt32)obj["AdapterRam"];
                        displayVal = ByteSize.FromBytes((long)wmiValue).ToString();
                        if (displayVal == "4GB")
                        {
                            displayVal += " (possibly more, variable is 32-bit unsigned)";
                        }
                    }
                    catch (Exception)
                    {
                        displayVal = "Unable to read value from registry/WMI";

                    }
                }
                addDiagLine("Memory: " + displayVal);
                addDiagLine("DriverVersion: " + obj["DriverVersion"]);
                vidCardIndex++;
            }



            addDiagLine("===Latest MEMI Marker Information");
            if (avi == null)
            {
                addDiagLine("ALOT Marker file does not have MEMI tag. ALOT/MEUITM not installed, or at least not installed through an installer.");
            }
            else
            {
                addDiagLine("ALOT Version: " + avi.ALOTVER + "." + avi.ALOTUPDATEVER + "." + avi.ALOTHOTFIXVER);
                if (DIAGNOSTICS_GAME == 1)
                {
                    addDiagLine("MEUITM: " + avi.MEUITMVER);
                }
            }


            //Start diagnostics
            string exe = BINARY_DIRECTORY + MEM_EXE_NAME;
            string args = "-check-game-data-mismatch " + DIAGNOSTICS_GAME + " -ipc";
            if (MEMI_FOUND)
            {
                bool textureMapFileExists = File.Exists(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\MassEffectModder\me" + DIAGNOSTICS_GAME + "map.bin");
                if (textureMapFileExists)
                {
                    diagnosticsWorker.ReportProgress(0, new ThreadCommand(SET_DIAGTASK_ICON_WORKING, Image_DataMismatch));
                    runMEM_Diagnostics(exe, args, diagnosticsWorker);
                    WaitForMEM();
                    addDiagLine("===Files added (or removed) after ALOT" + (DIAGNOSTICS_GAME == 1 ? "/MEUITM" : "") + " install");

                    if (BACKGROUND_MEM_PROCESS_PARSED_ERRORS.Count > 0)
                    {
                        if (MEMI_FOUND)
                        {
                            addDiagLine("Diagnostic reports some files appear to have been added or removed since texture scan took place:");
                        }
                        else
                        {
                            addDiagLine("Diagnostic reports some files appear to have been added or removed since texture scan took place. MEMI tag was not found - the game may have been restored, so these are likely not errors:");
                        }
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
                }
                else
                {
                    addDiagLine("===Files added (or removed) after ALOT" + (DIAGNOSTICS_GAME == 1 ? "/MEUITM" : "") + " install");
                    if (avi == null)
                    {
                        addDiagLine("Texture map file is not present: me" + DIAGNOSTICS_GAME + "map.bin - MEMI tag missing so this is OK");

                    }
                    else
                    {
                        addDiagLine(" - DIAG ERROR: Texture map file is missing: me" + DIAGNOSTICS_GAME + "map.bin but MEMI tag is present - was game migrated to new system or on different user account?");
                    }
                    diagnosticsWorker.ReportProgress(0, new ThreadCommand(SET_DIAGTASK_ICON_RED, Image_DataMismatch));
                }
            }
            else
            {
                addDiagLine("===Files added (or removed) after ALOT" + (DIAGNOSTICS_GAME == 1 ? "/MEUITM" : "") + " install");
                addDiagLine("MEMI tag was not found - ALOT/MEUITM not installed, skipping this check.");
                diagnosticsWorker.ReportProgress(0, new ThreadCommand(SET_DIAGTASK_ICON_GREEN, Image_DataMismatch));
            }


            args = "-check-game-data-after " + DIAGNOSTICS_GAME + " -ipc";
            diagnosticsWorker.ReportProgress(0, new ThreadCommand(SET_DIAGTASK_ICON_WORKING, Image_DataAfter));
            Context = CONTEXT_REPLACEDFILE_SCAN;
            runMEM_Diagnostics(exe, args, diagnosticsWorker);
            WaitForMEM();
            if (MEMI_FOUND)
            {
                addDiagLine("===Replaced files scan (after textures were installed)");
                addDiagLine("This check will detect if files were replaced after textures were installed in an unsupported manner.");
                addDiagLine("");
            }
            else
            {
                addDiagLine("===Preinstallation file scan");
                addDiagLine("This check will make sure all files can be opened for reading.");
                addDiagLine("");
            }

            if (BACKGROUND_MEM_PROCESS_PARSED_ERRORS.Count > 0)
            {

                if (MEMI_FOUND)
                {
                    addDiagLine("Diagnostic reports some files appear to have been replaced after textures were installed:");
                }
                else
                {
                    addDiagLine("The following files did not pass the file scan check:");
                }
                foreach (String str in BACKGROUND_MEM_PROCESS_PARSED_ERRORS)
                {
                    if (MEMI_FOUND)
                    {
                        addDiagLine(" - DIAG ERROR: " + str);
                    }
                    else
                    {
                        addDiagLine(" - " + str);

                    }
                }
                if (BACKGROUND_MEM_PROCESS.ExitCode == null || BACKGROUND_MEM_PROCESS.ExitCode != 0)
                {
                    pairLog = true;
                    addDiagLine("MEMNoGui returned non zero exit code, or null (crash) during -check-game-data-after. Some data was returned. The return code was: " + BACKGROUND_MEM_PROCESS.ExitCode);
                } else
                {
                    if (MEMI_FOUND)
                    {
                        addDiagLine("Diagnostic reports some files appear to have been replaced after textures were installed:");
                    }
                    else
                    {
                        addDiagLine("The following files did not pass the file scan check:");
                    }
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
                    pairLog = true;
                    addDiagLine("MEMNoGui returned non zero exit code, or null (crash) during -check-game-data-after: " + BACKGROUND_MEM_PROCESS.ExitCode);
                }
            }

            diagnosticsWorker.ReportProgress(0, new ThreadCommand(RESET_REPLACEFILE_TEXT));
            diagnosticsWorker.ReportProgress(0, new ThreadCommand(SET_DIAGTASK_ICON_GREEN, Image_DataAfter));
            Context = CONTEXT_NORMAL;

            //FULL CHECK
            if (TextureCheck)
            {
                addDiagLine("===Full Textures Check");
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
                        addDiagLine(" - DIAG ERROR: " + str);
                    }
                    if (BACKGROUND_MEM_PROCESS.ExitCode == null || BACKGROUND_MEM_PROCESS.ExitCode != 0)
                    {
                        pairLog = true;
                        addDiagLine("MEMNoGui returned non zero exit code, or null (crash) during -check-game-data-textures. Some data was returned. The return code was: " + BACKGROUND_MEM_PROCESS.ExitCode);
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
                        pairLog = true;
                        addDiagLine("MEMNoGui returned non zero exit code, or null (crash) during -check-game-data-textures: " + BACKGROUND_MEM_PROCESS.ExitCode);
                    }
                }
                diagnosticsWorker.ReportProgress(0, new ThreadCommand(TURN_OFF_TASKBAR_PROGRESS));
                diagnosticsWorker.ReportProgress(0, new ThreadCommand(SET_DIAGTASK_ICON_GREEN, Image_FullCheck));
            }

            addDiagLine("===Basegame mods");
            addDiagLine("Items in this block are only accurate if ALOT is not installed or items have been installed after ALOT.");
            addDiagLine("If ALOT was installed, detection of mods in this block means you installed items after ALOT was installed, which will break the game.");

            args = "-detect-mods " + DIAGNOSTICS_GAME + " -ipc";
            diagnosticsWorker.ReportProgress(0, new ThreadCommand(SET_DIAGTASK_ICON_WORKING, Image_DataBasegamemods));
            runMEM_Diagnostics(exe, args, diagnosticsWorker);
            WaitForMEM();
            addDiagLine("");

            string prefix = "";
            if (MEMI_FOUND)
            {
                prefix = " - DIAG ERROR: ";
            }
            if (BACKGROUND_MEM_PROCESS_PARSED_ERRORS.Count > 0)
            {
                if (MEMI_FOUND)
                {
                    addDiagLine("[ERROR]The following basegame mods were detected:");
                }
                else
                {
                    addDiagLine("The following basegame mods were detected:");
                }
                foreach (String str in BACKGROUND_MEM_PROCESS_PARSED_ERRORS)
                {
                    addDiagLine(prefix + " - " + str);
                }
                if (MEMI_FOUND)
                {
                    addDiagLine("[ERROR]These mods appear to be installed after ALOT. This will break the game. Follow the directions for ALOT to avoid this issue.");
                }
            }
            else
            {
                addDiagLine("Diagnostics did not detect any known basegame mods (-detect-mods).");
            }

            args = "-detect-bad-mods " + DIAGNOSTICS_GAME + " -ipc";
            runMEM_Diagnostics(exe, args, diagnosticsWorker);
            WaitForMEM();
            if (BACKGROUND_MEM_PROCESS_PARSED_ERRORS.Count > 0)
            {
                addDiagLine("Diagnostic reports the following incompatible mods are installed:");
                foreach (String str in BACKGROUND_MEM_PROCESS_PARSED_ERRORS)
                {
                    addDiagLine(" - DIAG ERROR: " + str);
                }
            }
            else
            {
                addDiagLine("Diagnostic did not detect any known incompatible mods.");
            }

            //Get DLCs
            var dlcPath = gamePath;
            switch (DIAGNOSTICS_GAME)
            {
                case 1:
                    dlcPath = Path.Combine(dlcPath, "DLC");
                    break;
                case 2:
                case 3:
                    dlcPath = Path.Combine(dlcPath, "BIOGame", "DLC");
                    break;
            }

            addDiagLine("===Installed DLC");
            addDiagLine("The following folders are present in the DLC directory:");
            if (Directory.Exists(dlcPath))
            {

                var directories = Directory.EnumerateDirectories(dlcPath);
                bool metadataPresent = false;
                bool hasUIMod = false;
                bool compatPatchInstalled = false;
                bool hasNonUIDLCMod = false;
                Dictionary<int, string> priorities = new Dictionary<int, string>();
                foreach (string dir in directories)
                {
                    string value = Path.GetFileName(dir);
                    if (value == "__metadata")
                    {
                        metadataPresent = true;
                        continue;
                    }
                    long sfarsize = 0;
                    long propersize = 32L;
                    bool hasSfarSizeError = false;
                    string duplicatePriorityStr = "";
                    if (DIAGNOSTICS_GAME == 3)
                    {
                        //check for ISM/Controller patch
                        int mountpriority = GetDLCPriority(dir);
                        if (mountpriority != -1)
                        {
                            if (priorities.ContainsKey(mountpriority))
                            {
                                duplicatePriorityStr = priorities[mountpriority];
                            }
                            else
                            {
                                priorities[mountpriority] = value;
                            }
                        }
                        if (mountpriority == 31050)
                        {
                            compatPatchInstalled = true;
                        }
                        if (value != "DLC_CON_XBX" && value != "DLC_CON_UIScaling" && value != "DLC_CON_UIScaling_Shared" && InteralGetDLCName(value) == null)
                        {
                            hasNonUIDLCMod = true;
                        }
                        if (value == "DLC_CON_XBX" || value == "DLC_CON_UIScaling" || value == "DLC_CON_UIScaling_Shared")
                        {
                            hasUIMod = true;
                        }
                        string sfar = dir + "\\CookedPCConsole\\Default.sfar";
                        if (File.Exists(sfar))
                        {
                            FileInfo fi = new FileInfo(sfar);
                            sfarsize = fi.Length;
                            hasSfarSizeError = sfarsize != propersize;
                        }
                    }
                    if (hasSfarSizeError && MEMI_FOUND)
                    {
                        addDiagLine("[ERROR]" + GetDLCDisplayString(value));
                        addDiagLine("[ERROR]      SFAR is not unpacked size. Should be 32 bytes, however SFAR is " + ByteSize.FromBytes(sfarsize) + ".");
                        addDiagLine("[ERROR]      If HQ graphics settings are on (MEMI was found so they should be) this will 99% of the time crash the game. See below for current LOD settings.");
                    }
                    else
                    {
                        addDiagLine(GetDLCDisplayString(value));
                    }
                    if (duplicatePriorityStr != "")
                    {
                        addDiagLine(" - DIAG ERROR: This DLC has the same mount priority as another DLC: " + duplicatePriorityStr);
                        addDiagLine("[ERROR]     These conflicting DLCs will likely encounter issues as the game will not know which files should be used");
                    }
                }

                if (hasUIMod && hasNonUIDLCMod && compatPatchInstalled)
                {
                    addDiagLine("This installation requires a UI compatibility patch. This patch appears to be installed.");
                }
                else if (hasUIMod && hasNonUIDLCMod && !compatPatchInstalled)
                {
                    addDiagLine("This installation may require a UI compatibility patch from Mass Effect 3 Mod Manager due to installation of a UI mod with other mods.");
                    addDiagLine("In Mass Effect 3 Mod Manager use Mod Management > Check for Custom DLC conflicts to see if you need one.");
                }
                else if (!hasUIMod && compatPatchInstalled)
                {
                    addDiagLine(" - DIAG ERROR: This installation does not require a UI compatibilty patch but one is installed. This may lead to game crashing.");
                }

                if (metadataPresent)
                {
                    addDiagLine("__metadata folder is present");
                }
            }
            else
            {
                if (DIAGNOSTICS_GAME == 3)
                {
                    addDiagLine(" - DIAG ERROR: DLC directory is missing: " + dlcPath + ". Mass Effect 3 always has a DLC folder so this should not be missing.");
                }
                else
                {
                    addDiagLine("DLC directory is missing: " + dlcPath + ". If no DLC is installed, this folder will be missing.");
                }
            }

            //TOC SIZE CHECK
            if (DIAGNOSTICS_GAME == 3)
            {
                addDiagLine("===File Table of Contents (TOC) size check");
                addDiagLine("PCConsoleTOC.bin files list the size of each file the game can load.");
                addDiagLine("If the size is smaller than the actual file, the game will not allocate enough memory to load the file and will hang or crash, typically at loading screens.");
                bool hadTocError = false;
                string[] tocs = Directory.GetFiles(Path.Combine(gamePath, "BIOGame"), "PCConsoleTOC.bin", SearchOption.AllDirectories);
                string markerfile = Utilities.GetALOTMarkerFilePath(3);
                foreach (string toc in tocs)
                {
                    TOCBinFile tbf = new TOCBinFile(toc);
                    foreach (TOCBinFile.Entry ent in tbf.Entries)
                    {
                        //Console.WriteLine(index + "\t0x" + ent.offset.ToString("X6") + "\t" + ent.size + "\t" + ent.name);
                        string filepath = Path.Combine(gamePath, ent.name);
                        if (File.Exists(filepath) && !filepath.Equals(markerfile, StringComparison.InvariantCultureIgnoreCase) && !filepath.ToLower().EndsWith("pcconsoletoc.bin"))
                        {
                            FileInfo fi = new FileInfo(filepath);
                            long size = fi.Length;
                            if (ent.size < size)
                            {
                                addDiagLine(" - DIAG ERROR: " + filepath + " size is " + size + ", but TOC lists " + ent.size);
                                hadTocError = true;
                            }
                        }
                    }
                }
                if (!hadTocError)
                {
                    addDiagLine("All TOC files passed check. No files have a size larger than the TOC size.");
                }
                else
                {
                    addDiagLine("[ERROR]Some files are larger than the listed TOC size. This typically won't happen unless you manually installed some files.");
                    addDiagLine("[ERROR]The game will always hang while loading these files. To fix, open MEM from Settings, go to Mass Effect 3, and choose 'Update TOCs'.");
                }
            }


            //Get LODs
            String lodStr = GetLODStr(DIAGNOSTICS_GAME, avi);
            addDiagLine("===LOD Information");
            addDiagLine(lodStr);

            diagnosticsWorker.ReportProgress(0, new ThreadCommand(SET_DIAGTASK_ICON_GREEN, Image_DataBasegamemods));
            diagnosticsWorker.ReportProgress(0, new ThreadCommand(SET_DIAGTASK_ICON_WORKING, Image_Upload));

            //ME1: LOGS
            if (DIAGNOSTICS_GAME == 1)
            {
                //GET LOGS
                string logsdir = Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\BioWare\Mass Effect\Logs";
                if (Directory.Exists(logsdir))
                {
                    DirectoryInfo info = new DirectoryInfo(logsdir);
                    FileInfo[] files = info.GetFiles().Where(f => f.LastWriteTime > DateTime.Now.AddDays(-7)).OrderByDescending(p => p.LastWriteTime).ToArray();
                    DateTime threeDaysAgo = DateTime.Now.AddDays(-3);
                    Console.WriteLine("---");
                    foreach (FileInfo file in files)
                    {
                        Console.WriteLine(file.Name + " " + file.LastWriteTime);
                        var logLines = File.ReadAllLines(file.FullName);
                        int crashIndex = 0;
                        int index = 0;
                        foreach (string line in logLines)
                        {

                            if (line.Contains("Critical: appError called"))
                            {
                                crashIndex = index;
                                Log.Information("Found crash in ME1 log " + file.Name + " on line " + index);
                                break;
                            }
                            index++;
                        }

                        if (crashIndex > 0)
                        {
                            crashIndex = Math.Max(0, crashIndex - 10);
                            //this log has a crash
                            addDiagLine("===Mass Effect crash log " + file.Name);
                            if (crashIndex > 0)
                            {
                                addDiagLine("[CRASHLOG]...");
                            }
                            for (int i = crashIndex; i < logLines.Length; i++)
                            {
                                addDiagLine("[CRASHLOG]" + logLines[i]);
                            }
                        }
                    }
                }
            }

            if (pairLog)
            {
                //program has had issue and log should be linked
                EventWaitHandle waitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);
                diagnosticsWorker.ReportProgress(0, new ThreadCommand(UPLOAD_LINKED_LOG, waitHandle));
                waitHandle.WaitOne();
                if (LINKEDLOGURL != null)
                {
                    Log.Information("Linked log for this diagnostic: " + LINKEDLOGURL);
                    addDiagLine("[LINKEDLOG]" + LINKEDLOGURL);
                }
            }



            string diag = diagStringBuilder.ToString();
            string hash = Utilities.sha256(diag);
            diag += hash;

            string date = DateTime.Now.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
            string diagfilename = EXE_DIRECTORY + "\\logs\\diagnostic_me" + DIAGNOSTICS_GAME + "_" + date + ".txt";
            System.IO.File.WriteAllText(diagfilename, diag);

            //upload
            string alotInstallerVer = System.Reflection.Assembly.GetEntryAssembly().GetName().Version.ToString();


            //Compress with LZMA for VPS Upload
            string outfile = diagfilename + ".lzma";
            args = "e \"" + diagfilename + "\" \"" + outfile + "\" -mt2";
            Utilities.runProcess(BINARY_DIRECTORY + "lzma.exe", args);
            var lzmalog = File.ReadAllBytes(outfile);
            var responseString = "https://vps.me3tweaks.com/alot/logupload.php".PostUrlEncodedAsync(new { LogData = Convert.ToBase64String(lzmalog), ALOTInstallerVersion = alotInstallerVer, Type = "diag" }).ReceiveString().Result;
            Uri uriResult;
            bool result = Uri.TryCreate(responseString, UriKind.Absolute, out uriResult)
                && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
            File.Delete(outfile);
            if (result)
            {
                //should be valid URL.
                diagnosticsWorker.ReportProgress(0, new ThreadCommand(SET_DIAGTASK_ICON_GREEN, Image_Upload));
                e.Result = responseString;
                Log.Information("Result from server for log upload: " + responseString);
            }
            else
            {
                diagnosticsWorker.ReportProgress(0, new ThreadCommand(SET_DIAG_TEXT, "Error from log server: " + responseString));
                diagnosticsWorker.ReportProgress(0, new ThreadCommand(SET_DIAGTASK_ICON_RED, Image_Upload));
                Log.Error("Error uploading log. The server responded with: " + responseString);
                e.Result = "Diagnostic complete.";
                Log.Warning("Log was rejected from log server");
                Utilities.OpenAndSelectFileInExplorer(diagfilename);
            }
            //}
        }

        private string GetCPUString()
        {
            string str = "";

            ManagementObjectSearcher mosProcessor = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");

            foreach (ManagementObject moProcessor in mosProcessor.Get())
            {
                if (str != "")
                {
                    str += "\n";
                }

                if (moProcessor["name"] != null)
                {
                    str += moProcessor["name"].ToString();
                    str += "\n";
                }
                if (moProcessor["maxclockspeed"] != null)
                {
                    str += "Maximum reported clock speed: ";
                    str += moProcessor["maxclockspeed"].ToString();
                    str += " Mhz\n";
                }
                if (moProcessor["numberofcores"] != null)
                {
                    str += "Cores: ";

                    str += moProcessor["numberofcores"].ToString();
                    str += "\n";
                }
                if (moProcessor["numberoflogicalprocessors"] != null)
                {
                    str += "Logical processors: ";
                    str += moProcessor["numberoflogicalprocessors"].ToString();
                    str += "\n";
                }

            }
            return str
               .Replace("(TM)", "™")
               .Replace("(tm)", "™")
               .Replace("(R)", "®")
               .Replace("(r)", "®")
               .Replace("(C)", "©")
               .Replace("(c)", "©")
               .Replace("    ", " ")
               .Replace("  ", " ");
        }

        private string GetLODStr(int gameID, ALOTVersionInfo avi)
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

                        if (engineConf.Read("TEXTUREGROUP_Character_1024", "TextureLODSettings") != "(MinLODSize=32,MaxLODSize=1024,LODBias=0)")
                        {
                            if (avi != null)
                            {
                                log += "HQ LOD settings appear to be set - game will be able to request higher resolution assets." + Environment.NewLine;
                            }
                            else
                            {
                                log += " - DIAG ERROR: HQ LOD settings appear to be set but MEMI marker is missing - game will likely have unused mip crashes." + Environment.NewLine;
                                log = ShowBadLODDialog(log);
                            }
                        }
                        else
                        {
                            if (avi != null)
                            {
                                log += " - DIAG ERROR: HQ LOD settings appear to be missing - MEMI tag is present - game will not use new high quality assets!" + Environment.NewLine;
                            }
                            else
                            {
                                log += "HQ LOD settings appear to be missing - MEMI tag is missing so this install is vanilla-ish." + Environment.NewLine;
                            }
                        }
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

                        if (engineConf.Read("TEXTUREGROUP_Character_1024", "SystemSettings") != "")
                        {
                            if (avi != null)
                            {
                                log += "HQ LOD settings appear to be set - game will be able to request higher resolution assets." + Environment.NewLine;
                            }
                            else
                            {
                                log += " - DIAG ERROR: HQ LOD settings appear to be set but MEMI marker is missing - game will likely have black textures." + Environment.NewLine;
                                log = ShowBadLODDialog(log);
                            }
                        }
                        else
                        {
                            if (avi != null)
                            {
                                log += " - DIAG ERROR: HQ LOD settings appear to be missing - MEMI tag is present - game will not use new high quality assets!" + Environment.NewLine;
                            }
                            else
                            {
                                log += "HQ LOD settings appear to be missing - MEMI tag is missing so this install is vanilla-ish." + Environment.NewLine;
                            }
                        }
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
                        if (engineConf.Read("TEXTUREGROUP_Character_1024", "SystemSettings") != "")
                        {
                            if (avi != null)
                            {
                                log += "HQ LOD settings appear to be set - game will be able to request higher resolution assets." + Environment.NewLine;
                            }
                            else
                            {
                                log += " - DIAG ERROR: HQ LOD settings appear to be set but MEMI marker is missing - game will likely have black textures." + Environment.NewLine;
                                log = ShowBadLODDialog(log);

                            }
                        }
                        else
                        {
                            //HQ LOD MISSING
                            if (avi != null)
                            {
                                log += " - DIAG ERROR: HQ LOD settings appear to be missing - MEMI tag is present - game will not use new high quality assets!" + Environment.NewLine;

                            }
                            else
                            {
                                log += "HQ LOD settings appear to be missing - MEMI tag is missing so this install is probably vanilla-ish." + Environment.NewLine;
                            }
                        }
                        break;
                }
            }
            else
            {
                log += " - DIAG ERROR: Could not find LOD config file: " + iniPath;

            }
            return log;
        }

        private string ShowBadLODDialog(string log)
        {
            ThreadCommandDialogOptions tcdo = new ThreadCommandDialogOptions();
            tcdo.signalHandler = new EventWaitHandle(false, EventResetMode.AutoReset);
            diagnosticsWorker.ReportProgress(0, new ThreadCommand(SHOW_DIALOG_BAD_LOD, tcdo));
            tcdo.signalHandler.WaitOne();
            if (FIXED_LOD_SETTINGS)
            {
                log += "User has selected to fix LOD settings. This should fix black textures or crashes due to game being restored manually/repaired but configuration files were not reset." + Environment.NewLine;
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
            string gamePath = Utilities.GetGamePath(DIAGNOSTICS_GAME);
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
                                if (MEMI_FOUND)
                                {
                                    BACKGROUND_MEM_PROCESS_PARSED_ERRORS.Add("DIAG ERROR: File was removed after textures scan: " + param);
                                }
                                else
                                {
                                    BACKGROUND_MEM_PROCESS_PARSED_ERRORS.Add("File was removed after textures scan: " + param);
                                }
                                break;
                            case "ERROR_ADDED_FILE":
                                if (MEMI_FOUND)
                                {
                                    AddedFiles.Add(param.ToLower());
                                    BACKGROUND_MEM_PROCESS_PARSED_ERRORS.Add("DIAG ERROR: File was added after textures scan: " + param + " " + File.GetCreationTimeUtc(gamePath + param));
                                }
                                else
                                {
                                    BACKGROUND_MEM_PROCESS_PARSED_ERRORS.Add("File was added after textures scan: " + param + " " + File.GetCreationTimeUtc(gamePath + param));
                                }
                                break;
                            case "ERROR_VANILLA_MOD_FILE":
                                if (MEMI_FOUND)
                                {
                                    string subpath = param;
                                    if (param.Length > gamePath.Length)
                                    {
                                        subpath = subpath.Substring(gamePath.Length);
                                    }
                                    if (!AddedFiles.Contains(subpath.ToLower()))
                                    {
                                        BACKGROUND_MEM_PROCESS_PARSED_ERRORS.Add("DIAG ERROR: File missing MEM/MEMNOGUI marker was found: " + subpath);
                                    }
                                }
                                break;
                            case "MOD":
                                BACKGROUND_MEM_PROCESS_PARSED_ERRORS.Add("Detected mod: " + param);
                                break;
                            case "TASK_PROGRESS":
                            case "OVERALL_PROGRESS": //will be removed in future
                                //worker.ReportProgress(0, new ThreadCommand(UPDATE_PROGRESSBAR_INDETERMINATE, false));
                                int percentInt = Convert.ToInt32(param);
                                if (Context == CONTEXT_FULLMIPMAP_SCAN)
                                {
                                    worker.ReportProgress(0, new ThreadCommand(SET_FULLSCAN_PROGRESS, percentInt));
                                }
                                else if (Context == CONTEXT_REPLACEDFILE_SCAN)
                                {
                                    worker.ReportProgress(0, new ThreadCommand(SET_REPLACEDFILE_PROGRESS, percentInt));
                                }
                                break;
                            case "PROCESSING_FILE":
                                worker.ReportProgress(0, new ThreadCommand(UPDATE_ADDONUI_CURRENTTASK, param));
                                break;
                            case "ERROR":
                                Log.Error("IPC ERROR: " + param);
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
            Button_FullDiagnostic.Visibility = Visibility.Collapsed;
            Button_QuickDiagnostic.Visibility = Visibility.Collapsed;
            TextBlock_DiagnosticType.Text = "FULL DIAGNOSTIC";
            TextBlock_DiagnosticType.Visibility = Visibility.Visible;
            RunDiagnostics(DIAGNOSTICS_GAME, true);
        }

        private void QuickDiagnostic_Click(object sender, RoutedEventArgs e)
        {
            Button_FullDiagnostic.Visibility = Visibility.Collapsed;
            Button_QuickDiagnostic.Visibility = Visibility.Collapsed;
            TextBlock_DiagnosticType.Text = "QUICK DIAGNOSTIC";
            TextBlock_DiagnosticType.Visibility = Visibility.Visible;
            RunDiagnostics(DIAGNOSTICS_GAME, false);
        }


        private string GetDLCDisplayString(string str)
        {
            string name = InteralGetDLCName(str);
            if (name != null)
            {
                return " - " + str + " (" + name + ")";
            }

            return "[DLC]" + str;
        }

        public static int GetDLCPriority(string DLCBasePath)
        {
            string mountfile = DLCBasePath + "\\CookedPCConsole\\Mount.dlc";
            if (File.Exists(mountfile))
            {
                try
                {
                    using (System.IO.FileStream s = new FileStream(mountfile, FileMode.Open))
                    {
                        byte[] pdata = new byte[2];
                        s.Seek(16, SeekOrigin.Begin);
                        s.Read(pdata, 0, 2);
                        // swap bytes
                        //byte b = pdata[0];
                        //pdata[0] = pdata[1];
                        //pdata[1] = b;
                        return BitConverter.ToUInt16(pdata, 0);
                    }
                }
                catch (Exception)
                {
                    return -1;
                }
            }
            else
            {
                return -1;
            }
        }

        private string InteralGetDLCName(string str)
        {
            switch (str)
            {
                //ME1
                case "DLC_Vegas": return "Pinnacle Station";
                case "DLC_UNC": return "Bring Down the Sky";

                //ME2:
                case "DLC_CER_Arc": return "Cerberus Arc Projector";
                case "DLC_CER_02": return "Aegis Pack";
                case "DLC_CON_Pack01": return "Alternate Appearance Pack 1 (Garrus, Thane, Jack)";
                case "DLC_CON_Pack02": return "Alternate Appearance Pack 2 (Tali, Grunt, Miranda)";
                case "DLC_DHME1": return "Mass Effect: Genesis";
                case "DLC_EXP_Part01": return "Lair of the Shadow Broker";
                case "DLC_EXP_Part02": return "Arrival";
                case "DLC_HEN_MT": return "Kasumi - Stolen Memory";
                case "DLC_HEN_VT": return "Zaeed - The Price of Revenge";
                case "DLC_UNC_Pack01": return "Overlord Pack";
                case "DLC_MCR_01": return "Firepower Pack";
                case "DLC_MCR_03": return "Equalizer Pack";
                case "DLC_PRE_Cerberus": return "Cerberus Weapon and Armor";
                case "DLC_PRE_Collectors": return "Collectors and Digital Deluxe Edition Bonus Content";
                case "DLC_PRE_DA": return "Blood Dragon Armor";
                case "DLC_PRE_Gamestop": return "Terminus Weapon and Armor";
                case "DLC_PRE_General": return "Inferno Armor";
                case "DLC_PRE_Incisor": return "M-29 Incisor";
                case "DLC_PRO_Gulp01": return "Sentry Interface";
                case "DLC_PRO_Pepper01": return "Umbra Visor";
                case "DLC_PRO_Pepper02": return "Recon Hood";
                case "DLC_UNC_Hammer01": return "Firewalker Pack";
                case "DLC_UNC_Moment01": return "Normandy Crash Site";

                //ME3
                case "DLC_CON_MP1": return "Resugence";
                case "DLC_CON_MP2": return "Rebellion";
                case "DLC_CON_MP3": return "Earth";
                case "DLC_CON_MP4": return "Retaliation";
                case "DLC_CON_MP5": return "Reckoning";
                case "DLC_UPD_Patch01": return "Multiplayer Balance Changes Cache 1";
                case "DLC_UPD_Patch02": return "Multiplayer Balance Changes Cache 2";
                case "DLC_HEN_PR": return "From Ashes";
                case "DLC_CON_END": return "Extended Cut";
                case "DLC_EXP_Pack001": return "Leviathan";
                case "DLC_EXP_Pack002": return "Omega";
                case "DLC_EXP_Pack003": return "Citadel";
                case "DLC_EXP_Pack003_Base": return "Citadel Base";
                case "DLC_OnlinePassHidCE": return "Collectors and Digital Deluxe Edition Bonus Content";
                case "DLC_CON_APP01": return "Alternate Appearance Pack";
                case "DLC_CON_DH1": return "Genesis 2";
                case "DLC_CON_GUN01": return "Firefight Pack";
                case "DLC_CON_GUN02": return "Groundside Resistance Pack";

                //case "DLC_CON_XBX": return "[MOD] Singleplayer Native Controller Support";
                //case "DLC_CON_UIScaling": return "[MOD] Interface Scaling Mod";
                //case "DLC_CON_UIScaling_Shared": return "[MOD] Interface Scaling Add-on";

                default:
                    return null;
            }
        }
    }
}
