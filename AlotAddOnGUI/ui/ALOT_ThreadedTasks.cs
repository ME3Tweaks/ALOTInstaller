using AlotAddOnGUI.classes;
using ByteSizeLib;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using Serilog;
using SlavaGu.ConsoleAppLauncher;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AlotAddOnGUI
{
    public partial class MainWindow : MetroWindow
    {
        private int INSTALLING_THREAD_GAME;
        private List<AddonFile> ADDONFILES_TO_BUILD;
        public const string UPDATE_TASK_PROGRESS = "UPDATE_TASK_PROGRESS";
        public const string UPDATE_OVERALL_TASK = "UPDATE_OVERALL_TASK";
        private readonly int INSTALL_OK = 1;
        public string CurrentTask;
        public int CurrentTaskPercent;

        private bool ExtractAddon(AddonFile af)
        {
            string stagingdirectory = System.AppDomain.CurrentDomain.BaseDirectory + MEM_STAGING_DIR + "\\";

            string prefix = "[" + Path.GetFileNameWithoutExtension(af.Filename) + "] ";
            Log.Information(prefix + "Processing extraction on " + af.Filename);
            string fileextension = System.IO.Path.GetExtension(af.Filename);
            ulong freeBytes;
            ulong diskSize;
            ulong totalFreeBytes;

            long length = new System.IO.FileInfo(EXE_DIRECTORY + "Downloaded_Mods\\" + af.Filename).Length;
            string processingStr = "Processing " + af.FriendlyName + " (" + ByteSize.FromBytes(length) + ")";
            TasksDisplayEngine.SubmitTask(processingStr);
            BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_OPERATION_LABEL, processingStr));

            try
            {
                switch (fileextension)
                {
                    case ".7z":
                    case ".zip":
                    case ".rar":
                        {
                            //Extract file

                            Log.Information(prefix + "Extracting file: " + af.Filename);
                            string exe = BINARY_DIRECTORY + "7z.exe";
                            string extractpath = EXE_DIRECTORY + "Extracted_Mods\\" + System.IO.Path.GetFileNameWithoutExtension(af.Filename);
                            string args = "x \"" + EXE_DIRECTORY + "Downloaded_Mods\\" + af.Filename + "\" -aoa -r -o\"" + extractpath + "\"";
                            runProcess(exe, args);

                            //get free space for debug purposes
                            Utilities.GetDiskFreeSpaceEx(stagingdirectory, out freeBytes, out diskSize, out totalFreeBytes);
                            Log.Information("[SIZE] ADDONEXTRACTFINISH Free Space on current drive: " + ByteSize.FromBytes(freeBytes) + " " + freeBytes);
                            var moveableFiles = Directory.EnumerateFiles(extractpath, "*.*", SearchOption.AllDirectories) //<--- .NET 4.5
                                .Where(file => file.ToLower().EndsWith("tpf") || file.ToLower().EndsWith("mem"))
                                .ToList();
                            if (af.ALOTVersion > 0)
                            {
                                Debug.WriteLine("BREAK");
                            }
                            if (moveableFiles.Count() > 0)
                            {
                                //check for copy directly items first, and move them.

                                foreach (string moveableFile in moveableFiles)
                                {
                                    string name = Utilities.GetRelativePath(moveableFile, extractpath);
                                    foreach (PackageFile pf in af.PackageFiles)
                                    {
                                        string fname = Path.GetFileName(name);
                                        if (pf.MoveDirectly && pf.SourceName == name && pf.AppliesToGame(CURRENT_GAME_BUILD) && af.ALOTVersion > 0)
                                        {
                                            //It's an ALOT file. We will move this directly to the output directory.
                                            Log.Information("ALOT MAIN FILE - moving to output: " + fname);
                                            string movename = getOutputDir(CURRENT_GAME_BUILD) + "000_" + fname;
                                            if (File.Exists(movename))
                                            {
                                                File.Delete(movename);
                                            }
                                            File.Move(moveableFile, movename);
                                            pf.Processed = true; //no more ops on this package file.
                                            break;
                                        }
                                        if (pf.MoveDirectly && pf.SourceName == name && pf.AppliesToGame(CURRENT_GAME_BUILD) && af.ALOTUpdate)
                                        {
                                            //It's an ALOT update file. We will move this directly to the output directory.
                                            Log.Information("ALOT UPDATE FILE - moving to output: " + fname);
                                            string movename = getOutputDir(CURRENT_GAME_BUILD) + "001_" + fname;
                                            if (File.Exists(movename))
                                            {
                                                File.Delete(movename);
                                            }
                                            File.Move(moveableFile, movename); pf.Processed = true; //no more ops on this package file.
                                            break;
                                        }
                                        if (pf.MoveDirectly && pf.SourceName == name && pf.AppliesToGame(CURRENT_GAME_BUILD) && name.ToLower().EndsWith(".mem"))
                                        {
                                            //It's a already built MEM file. Move MEM to build folder
                                            Log.Information("MoveDirectly on MEM file specified - moving MEM to output: " + fname);
                                            int fileprefix = Interlocked.Increment(ref PREBUILT_MEM_INDEX);
                                            string paddedVer = fileprefix.ToString("000");
                                            string movename = getOutputDir(CURRENT_GAME_BUILD) + paddedVer + "_" + fname;
                                            if (File.Exists(movename))
                                            {
                                                File.Delete(movename);
                                            }
                                            File.Move(moveableFile, movename); pf.Processed = true; //no more ops on this package file.
                                            break;
                                        }
                                        if (pf.MoveDirectly && pf.SourceName == name && pf.AppliesToGame(CURRENT_GAME_BUILD))
                                        {
                                            Log.Information("MoveDirectly specified - moving TPF/MEM to staging: " + fname);
                                            File.Move(moveableFile, STAGING_DIRECTORY + fname);
                                            pf.Processed = true; //no more ops on this package file.
                                            break;
                                        }
                                        if (pf.CopyDirectly && pf.SourceName == name && pf.AppliesToGame(CURRENT_GAME_BUILD))
                                        {
                                            Log.Information("CopyDirectly specified - copy TPF/MEM to staging: " + fname);
                                            File.Copy(moveableFile, STAGING_DIRECTORY + fname, true);
                                            pf.Processed = true; //We will still extract this as it is a copy step.
                                            break;
                                        }
                                        if (pf.Delete && pf.SourceName == name && pf.AppliesToGame(CURRENT_GAME_BUILD))
                                        {
                                            Log.Information("Delete specified - deleting unused TPF/MEM: " + fname);
                                            File.Delete(moveableFile);
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
                            BuildWorker.ReportProgress(0, new ThreadCommand(INCREMENT_COMPLETION_EXTRACTION));
                            break;
                        }
                    case ".tpf":
                        {
                            BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_OPERATION_LABEL, "Preparing " + af.FriendlyName));

                            string source = EXE_DIRECTORY + "Downloaded_Mods\\" + af.Filename;
                            string destination = EXE_DIRECTORY + "Extracted_Mods\\" + Path.GetFileName(af.Filename);
                            File.Copy(source, destination, true);
                            BuildWorker.ReportProgress(0, new ThreadCommand(INCREMENT_COMPLETION_EXTRACTION));
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
                            BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_OPERATION_LABEL, "Preparing " + af.FriendlyName));

                            //Copy to output folder
                            File.Copy(EXE_DIRECTORY + "Downloaded_Mods\\" + af.Filename, getOutputDir(CURRENT_GAME_BUILD) + af.Filename, true);
                            BuildWorker.ReportProgress(0, new ThreadCommand(INCREMENT_COMPLETION_EXTRACTION));
                            break;
                        }
                }
                BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_PROGRESSBAR_INDETERMINATE, false));
            }
            catch (Exception e)
            {
                Log.Error("ERROR EXTRACTING AND PROCESSING FILE!");
                Log.Error(e.ToString());
                BuildWorker.ReportProgress(0, new ThreadCommand("ERROR_OCCURED", e.Message));
                TasksDisplayEngine.ReleaseTask(af.FriendlyName);
                return false;
            }
            Utilities.GetDiskFreeSpaceEx(stagingdirectory, out freeBytes, out diskSize, out totalFreeBytes);

            Log.Information("[SIZE] ADDON EXTRACTADDON COMPLETED Free Space on current drive: " + ByteSize.FromBytes(freeBytes) + " " + freeBytes);
            string newText = TasksDisplayEngine.ReleaseTask(processingStr);
            if (newText != null)
            {
                BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_OPERATION_LABEL, "Processing " + newText));
            }
            return true;
        }

        private bool ExtractAddons(int game)
        {
            string stagingdirectory = System.AppDomain.CurrentDomain.BaseDirectory + MEM_STAGING_DIR + "\\";
            PREBUILT_MEM_INDEX = 9;
            SHOULD_HAVE_OUTPUT_FILE = false; //will set to true later
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

            ADDONFILES_TO_BUILD = new List<AddonFile>();
            foreach (AddonFile af in addonfiles)
            {
                if (af.Ready && (game == 1 && af.Game_ME1 || game == 2 && af.Game_ME2 || game == 3 && af.Game_ME3))
                {
                    Log.Information("Adding AddonFile to installation list: " + af.FriendlyName);
                    ADDONFILES_TO_BUILD.Add(af);
                }
            }
            ADDONSTOINSTALL_COUNT = ADDONFILES_TO_BUILD.Count;
            int completed = 0;
            BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_OPERATION_LABEL, "Extracting Mods..."));
            BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_PROGRESSBAR_INDETERMINATE, true));

            BuildWorker.ReportProgress(0);

            bool modextractrequired = false; //not used currently.

            int threads = Environment.ProcessorCount;
            if (threads > 1)
            {
                threads--; //cores - 1
            }
            bool[] results = ADDONFILES_TO_BUILD.AsParallel().WithDegreeOfParallelism(threads).Select(ExtractAddon).ToArray();
            foreach (bool result in results)
            {
                if (!result)
                {
                    Log.Error("Failed to extract a file! Check previous entries in this log");
                    BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_PROGRESSBAR_INDETERMINATE, false));
                    BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_OPERATION_LABEL, "Failed to extract a file - check logs"));
                    CURRENT_GAME_BUILD = 0; //reset
                    return false;
                }
            }
            TasksDisplayEngine.ClearTasks();
            //if (tpfextractrequired)
            {
                BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_PROGRESSBAR_INDETERMINATE, true));
                BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_OPERATION_LABEL, "Extracting TPFs..."));
                BuildWorker.ReportProgress(0);

                Log.Information("Extracting TPF files.");
                string exe = BINARY_DIRECTORY + MEM_EXE_NAME;
                string args = "-extract-tpf \"" + EXE_DIRECTORY + "Extracted_Mods\" \"" + EXE_DIRECTORY + "Extracted_Mods\"";
                runProcess(exe, args);
            }

            BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_OPERATION_LABEL, "Extracting MOD files..."));
            if (modextractrequired)
            {
                BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_PROGRESSBAR_INDETERMINATE, true));
                BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_OPERATION_LABEL, "Extracting MOD files..."));
                BuildWorker.ReportProgress(0);

                Log.Information("Extracting MOD files.");
                string exe = BINARY_DIRECTORY + MEM_EXE_NAME;
                string args = "-extract-mod " + game + " \"" + EXE_DIRECTORY + "Downloaded_Mods\" \"" + EXE_DIRECTORY + "Extracted_Mods\"";
                runProcess(exe, args);
            }

            //InstallWorker.ReportProgress(completed, new ThreadCommand(UPDATE_OPERATION_LABEL, "Removing Duplicates..."));
            //Thread.Sleep(7000);

            BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_OPERATION_LABEL, "Preparing to create MEM package..."));
            BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_PROGRESSBAR_INDETERMINATE, false));

            //Calculate how many files to install...
            int totalfiles = 0;
            foreach (AddonFile af in ADDONFILES_TO_BUILD)
            {
                totalfiles += af.PackageFiles.Count;
            }

            basepath = EXE_DIRECTORY + @"Extracted_Mods\";
            Directory.CreateDirectory(stagingdirectory);
            int numcompleted = 0;
            foreach (AddonFile af in ADDONFILES_TO_BUILD)
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
                                SHOULD_HAVE_OUTPUT_FILE = true;
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
                            BuildWorker.ReportProgress(progress);
                        }
                        //  Thread.Sleep(1000);
                    }
                }
            }

            Utilities.GetDiskFreeSpaceEx(stagingdirectory, out freeBytes, out diskSize, out totalFreeBytes);
            Log.Information("[SIZE] POSTEXTRACT_PRESTAGING Free Space on current drive: " + ByteSize.FromBytes(freeBytes) + " " + freeBytes);

            //COLEANUP EXTRACTION DIR
            Log.Information("Completed staging. Now cleaning up extraction directory");
            BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_OPERATION_LABEL, "Cleaning up extraction directory"));
            BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_PROGRESSBAR_INDETERMINATE, true));
            BuildWorker.ReportProgress(100);
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
            BuildWorker.ReportProgress(0);
            BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_OPERATION_LABEL, "Building Addon MEM Package..."));
            int buildresult = -2;
            {
                Log.Information("Building MEM Package.");
                string exe = BINARY_DIRECTORY + MEM_EXE_NAME;
                string filename = "002_ALOT_ME" + game + "_Addon.mem";
                string args = "-convert-to-mem " + game + " \"" + EXE_DIRECTORY + MEM_STAGING_DIR + "\" \"" + getOutputDir(game) + filename + "\" -ipc";

                runMEM_Backup(exe, args, BuildWorker);
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
                    BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_OPERATION_LABEL, "Cleaning up staging directories"));
                }

                if (buildresult != 0)
                {
                    Log.Error("Non-Zero return code! Something probably went wrong.");
                }
                if (buildresult == 0 && !File.Exists(getOutputDir(game) + filename) && SHOULD_HAVE_OUTPUT_FILE)
                {

                    Log.Error("Process went OK but no outputfile... Something probably went wrong.");
                    buildresult = -1;
                }
            }
            //cleanup staging
            BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_OPERATION_LABEL, "Cleaning up staging directory"));
            BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_PROGRESSBAR_INDETERMINATE, true));
            BuildWorker.ReportProgress(100);
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
            BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_PROGRESSBAR_INDETERMINATE, false));
            CURRENT_GAME_BUILD = 0; //reset
            return buildresult == 0;
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

            runMEM_Backup(exe, args, BackupWorker, acceptedIPC);
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
            string[] ignoredExtensions = { ".wav", ".pdf", ".bak" };
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

        private void InstallALOT(int game)
        {
            InstallingOverlay_TopLabel.Text = "Installing ALOT for Mass Effect" + getGameNumberSuffix(game);
            InstallWorker = new BackgroundWorker();
            InstallWorker.DoWork += InstallALOT;
            InstallWorker.WorkerReportsProgress = true;
            InstallWorker.ProgressChanged += InstallWorker_ProgressChanged;
            InstallWorker.RunWorkerCompleted += InstallCompleted;
            INSTALLING_THREAD_GAME = game;
            WindowButtonCommandsOverlayBehavior = WindowCommandsOverlayBehavior.Flyouts;
            InstallingOverlayFlyout.Theme = FlyoutTheme.Dark;
            InstallingOverlayFlyout.IsOpen = true;
            Installing = true;
            HeaderLabel.Text = "Installing MEMs";
            InstallWorker.RunWorkerAsync(getOutputDir(INSTALLING_THREAD_GAME));
        }

        private void InstallCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            INSTALLING_THREAD_GAME = 0;
            ADDONFILES_TO_BUILD = null;
            Installing = false;
            InstallingOverlayFlyout.IsOpen = false;
        }

        private void InstallALOT(object sender, DoWorkEventArgs e)
        {
            Log.Information("Installing ALOT...");

            List<string> acceptedIPC = new List<string>();
            acceptedIPC.Add("TASK_PROGRESS");
            acceptedIPC.Add("PHASE");

            string exe = BINARY_DIRECTORY + MEM_EXE_NAME;
            string args = "";

            if (INSTALLING_THREAD_GAME == 3)
            {
                //Unpack DLC
                CurrentTask = "Unpacking DLC";
                CurrentTaskPercent = 0;
                InstallWorker.ReportProgress(0, new ThreadCommand(UPDATE_OPERATION_LABEL, CurrentTask));
                InstallWorker.ReportProgress(0, new ThreadCommand(UPDATE_TASK_PROGRESS, CurrentTaskPercent));

                args = "-unpack-dlcs -ipc";
                runMEM_Install(exe, args, InstallWorker);
                while (BACKGROUND_MEM_PROCESS.State == AppState.Running)
                {
                    Thread.Sleep(250);
                }
            }

            //Scan and remove empty MipMaps
            args = "-scan-with-remove " + INSTALLING_THREAD_GAME + " -ipc";
            runMEM_Install(exe, args, InstallWorker);
            while (BACKGROUND_MEM_PROCESS.State == AppState.Running)
            {
                Thread.Sleep(250);
            }

            //Install Textures
            string outputDir = getOutputDir(INSTALLING_THREAD_GAME, false);
            CurrentTask = "Installing Textures";
            CurrentTaskPercent = 0;
            InstallWorker.ReportProgress(0, new ThreadCommand(UPDATE_OPERATION_LABEL, CurrentTask));
            InstallWorker.ReportProgress(0, new ThreadCommand(UPDATE_TASK_PROGRESS, CurrentTaskPercent));
            args = "-install-mods " + INSTALLING_THREAD_GAME + " \"" + outputDir + "\" -ipc";
            runMEM_Install(exe, args, InstallWorker);
            while (BACKGROUND_MEM_PROCESS.State == AppState.Running)
            {
                Thread.Sleep(250);
            }

            //Apply LOD
            CurrentTask = "Updating Mass Effect" + getGameNumberSuffix(INSTALLING_THREAD_GAME) + "'s graphics settings";
            CurrentTaskPercent = 0;
            InstallWorker.ReportProgress(0, new ThreadCommand(UPDATE_OPERATION_LABEL, CurrentTask));
            InstallWorker.ReportProgress(0, new ThreadCommand(UPDATE_TASK_PROGRESS, CurrentTaskPercent));

            args = "-apply-me1-laa";
            runMEM_Install(exe, args, InstallWorker);
            while (BACKGROUND_MEM_PROCESS.State == AppState.Running)
            {
                Thread.Sleep(250);
            }

            if (INSTALLING_THREAD_GAME == 1)
            {
                //Apply ME1 LAA
                CurrentTask = "Making Mass Effect Large Address Aware";
                CurrentTaskPercent = 0;
                InstallWorker.ReportProgress(0, new ThreadCommand(UPDATE_OPERATION_LABEL, CurrentTask));
                InstallWorker.ReportProgress(0, new ThreadCommand(UPDATE_TASK_PROGRESS, CurrentTaskPercent));

                args = "-apply-me1-laa";
                runMEM_Install(exe, args, InstallWorker);
                while (BACKGROUND_MEM_PROCESS.State == AppState.Running)
                {
                    Thread.Sleep(250);
                }
            }

            foreach (AddonFile af in ADDONFILES_TO_BUILD)
            {
                if (af.ALOTVersion > 0)
                {
                    CurrentTask = "Setting installation marker";
                    CurrentTaskPercent = 0;
                    InstallWorker.ReportProgress(0, new ThreadCommand(UPDATE_OPERATION_LABEL, CurrentTask));
                    InstallWorker.ReportProgress(0, new ThreadCommand(UPDATE_TASK_PROGRESS, CurrentTaskPercent));

                    args = "-apply-mod-tag " + INSTALLING_THREAD_GAME + " " + af.ALOTVersion + " 0";
                    runMEM_Install(exe, args, InstallWorker);
                    while (BACKGROUND_MEM_PROCESS.State == AppState.Running)
                    {
                        Thread.Sleep(250);
                    }
                    break;
                }
            }

            //Install Binkw32
            if (INSTALLING_THREAD_GAME == 2 || INSTALLING_THREAD_GAME == 3)
            {
                Utilities.InstallBinkw32Bypass(INSTALLING_THREAD_GAME);
            }

            e.Result = INSTALL_OK;
        }


        private async void InstallWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            ThreadCommand tc = (ThreadCommand)e.UserState;
            switch (tc.Command)
            {
                case UPDATE_OVERALL_TASK:
                    InstallingOverlay_TopLabel.Text = (string)tc.Data;
                    break;
                case UPDATE_OPERATION_LABEL:
                    CurrentTask = (string)tc.Data;
                    InstallingOverlay_BottomLabel.Text = CurrentTask + " " + CurrentTaskPercent + "%";
                    break;
                case UPDATE_TASK_PROGRESS:
                    if (tc.Data is string)
                    {
                        CurrentTaskPercent = Convert.ToInt32((string)tc.Data);
                    }
                    else
                    {
                        CurrentTaskPercent = (int)tc.Data;
                    }
                    InstallingOverlay_BottomLabel.Text = CurrentTask + " " + CurrentTaskPercent + "%";
                    break;
                case UPDATE_PROGRESSBAR_INDETERMINATE:
                    //Install_ProgressBar.IsIndeterminate = (bool)tc.Data;
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
            }
        }

        private void runMEM_Backup(string exe, string args, BackgroundWorker worker, List<string> acceptedIPC = null)
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

        private void runMEM_Install(string exe, string args, BackgroundWorker worker, List<string> acceptedIPC = null)
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
                            //worker.ReportProgress(completed, new ThreadCommand(UPDATE_PROGRESSBAR_INDETERMINATE, false));
                            //int percentInt = Convert.ToInt32(param);
                            //worker.ReportProgress(percentInt);
                            case "PROCESSING_FILE":
                                //worker.ReportProgress(completed, new ThreadCommand(UPDATE_OPERATION_LABEL, param));
                                break;
                            case "OVERALL_PROGRESS":
                            //This will be changed later
                            case "TASK_PROGRESS":
                                worker.ReportProgress(completed, new ThreadCommand(UPDATE_TASK_PROGRESS, param));
                                break;
                            case "PHASE":
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

        private void RestoreGame(object sender, DoWorkEventArgs e)
        {
            string gamePath = Utilities.GetGamePath(BACKUP_THREAD_GAME);
            string backupPath = Utilities.GetGameBackupPath(BACKUP_THREAD_GAME);
            BackupWorker.ReportProgress(completed, new ThreadCommand(UPDATE_PROGRESSBAR_INDETERMINATE, true));
            BackupWorker.ReportProgress(completed, new ThreadCommand(UPDATE_OPERATION_LABEL, "Deleting existing game installation"));
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
            Log.Information("Reverting lod settings");
            IniSettingsHandler.removeLOD(BACKUP_THREAD_GAME);
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
    }

}
