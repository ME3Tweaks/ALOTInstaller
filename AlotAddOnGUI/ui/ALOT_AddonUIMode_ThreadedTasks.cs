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
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Xml.Linq;

namespace AlotAddOnGUI
{
    public partial class MainWindow : MetroWindow
    {
        static Random RANDOM = new Random();
        private BackgroundWorker BuildWorker = new BackgroundWorker();
        private BackgroundWorker BackupWorker = new BackgroundWorker();
        private BackgroundWorker InstallWorker = new BackgroundWorker();
        private BackgroundWorker ImportWorker = new BackgroundWorker();

        private int INSTALLING_THREAD_GAME;
        private List<AddonFile> ADDONFILES_TO_BUILD;
        private int MEM_INSTALL_TIME_SECONDS;
        private List<AddonFile> ADDONFILES_TO_INSTALL;
        public const string UPDATE_CURRENT_STAGE_PROGRESS = "UPDATE_TASK_PROGRESS";
        public const string UPDATE_OVERALL_TASK = "UPDATE_OVERALL_TASK";
        public const string SHOW_ORIGIN_FLYOUT = "SHOW_ORIGIN_FLYOUT";
        private const int INSTALL_OK = 1;
        private WaveOut waveOut;
        private NAudio.Vorbis.VorbisWaveReader vorbisStream;
        public const string RESTORE_FAILED_COULD_NOT_DELETE_FOLDER = "RESTORE_FAILED_COULD_NOT_DELETE_FOLDER";
        public string CurrentTask;
        public int CurrentTaskPercent;
        public const string UPDATE_CURRENTTASK_NAME = "UPDATE_SUBTASK";
        private int CURRENT_STAGE_NUM = 0;
        private int STAGE_COUNT;
        private string CURRENT_STAGE_CONTEXT = null;
        public const string STAGE_CONTEXT = "STAGE_CONTEXT";

        public const string UPDATE_STAGE_OF_STAGE_LABEL = "UPDATE_STAGE_LABEL";
        public const string HIDE_ALL_STAGE_LABELS = "HIDE_STAGE_LABEL";
        public const string UPDATE_HEADER_LABEL = "UPDATE_HEADER_LABEL";
        public static ALOTVersionInfo CURRENTLY_INSTALLED_ME1_ALOT_INFO;
        public static ALOTVersionInfo CURRENTLY_INSTALLED_ME2_ALOT_INFO;
        public static ALOTVersionInfo CURRENTLY_INSTALLED_ME3_ALOT_INFO;
        int USERFILE_INDEX = 100;
        private bool WARN_USER_OF_EXIT = false;
        private List<string> TIPS_LIST;
        private const string SET_OVERALL_PROGRESS = "SET_OVERALL_PROGRESS";
        Stopwatch stopwatch;
        private string MAINTASK_TEXT;
        private string CURRENT_USER_BUILD_FILE = "";
        public bool BUILD_ALOT { get; private set; }
        private bool BUILD_ADDON_FILES = false;
        private bool BUILD_USER_FILES = false;
        private bool BUILD_ALOT_UPDATE = false;
        private bool BUILD_MEUITM = false;


        private FadeInOutSampleProvider fadeoutProvider;
        private bool MusicPaused;
        public static string DOWNLOADED_MODS_DIRECTORY = EXE_DIRECTORY + "Downloaded_Mods";
        private string EXTRACTED_MODS_DIRECTORY = EXE_DIRECTORY + "Data\\Extracted_Mods";
        private bool ERROR_OCCURED_PLEASE_STOP = false;
        private bool REPACK_GAME_FILES;
        private bool TaskbarProgressIndeterminateManaged = false;
        private const string SET_TASKBAR_INDETERMINATE = "SET_TASKBAR_INDETERMINATE";
        private const string ERROR_TEXTURE_MAP_MISSING = "ERROR_TEXTURE_MAP_MISSING";
        private const string ERROR_TEXTURE_MAP_WRONG = "ERROR_TEXTURE_MAP_WRONG";
        private const string ERROR_FILE_ADDED = "ERROR_FILE_ADDED";
        private const string ERROR_FILE_REMOVED = "ERROR_FILE_REMOVED";
        private const string SETTINGSTR_SOUND = "PlayMusic";
        private const string SET_VISIBILE_ITEMS_LIST = "SET_VISIBILE_ITEMS_LIST";
        private const int END_OF_PROCESS_POLL_INTERVAL = 100;

        public bool MusicIsPlaying { get; private set; }

        public ConsoleApp Run7zWithProgressForAddonFile(string args, AddonFile af)
        {
            Log.Information("Running 7z progress process: 7z " + args);
            ConsoleApp ca = new ConsoleApp(MainWindow.BINARY_DIRECTORY + "7z.exe", args);
            ca.ConsoleOutput += (o, args2) =>
            {
                if (args2.IsError && args2.Line.Trim() != "" && !args2.Line.Trim().StartsWith("0M"))
                {
                    int percentIndex = args2.Line.IndexOf("%");
                    string message = "";
                    if (percentIndex > 0)
                    {
                        message = "Extracting - " + args2.Line.Substring(0, percentIndex + 1).Trim();
                        if (message != af.ReadyStatusText)
                        {
                            af.ReadyStatusText = "Extracting - " + args2.Line.Substring(0, percentIndex + 1).Trim();
                        }
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

        private KeyValuePair<AddonFile, bool> ExtractAddon(AddonFile af)
        {
            if (ERROR_OCCURED_PLEASE_STOP)
            {
                return new KeyValuePair<AddonFile, bool>(af, true); //skip
            }
            string stagingdirectory = ADDON_FULL_STAGING_DIRECTORY;

            string fileToUse = af.Filename;
            bool isSingleFileMode = false;
            if (File.Exists(DOWNLOADED_MODS_DIRECTORY + "\\" + af.UnpackedSingleFilename))
            {
                isSingleFileMode = true;
                fileToUse = af.UnpackedSingleFilename;
            }

            string prefix = "[" + Path.GetFileNameWithoutExtension(fileToUse) + "] ";
            Log.Information(prefix + "Processing extraction on " + fileToUse);
            string fileextension = System.IO.Path.GetExtension(fileToUse);
            ulong freeBytes;
            ulong diskSize;
            ulong totalFreeBytes;

            string processingStr = "Processing " + af.FriendlyName;
            long length = 0L;
            if (af.UserFile)
            {
                length = new System.IO.FileInfo(af.UserFilePath).Length;

            }
            else
            {
                length = new System.IO.FileInfo(DOWNLOADED_MODS_DIRECTORY + "\\" + fileToUse).Length;
            }
            processingStr += " (" + ByteSize.FromBytes(length) + ")";

            TasksDisplayEngine.SubmitTask(processingStr);
            BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_ADDONUI_CURRENTTASK, processingStr));
            string extractpath = extractpath = EXTRACTED_MODS_DIRECTORY + "\\" + af.BuildID;
            string extractSource = DOWNLOADED_MODS_DIRECTORY + "\\" + fileToUse;
            if (af.UserFile)
            {
                extractSource = af.UserFilePath;
                extractpath = USER_FULL_STAGING_DIRECTORY + af.BuildID;
            }
            try
            {
                Directory.CreateDirectory(extractpath);
            }
            catch (Exception e)
            {
                Log.Error("Error creating extraction directory: " + extractpath);
                Log.Error(App.FlattenException(e));
                ERROR_OCCURED_PLEASE_STOP = true;
                return new KeyValuePair<AddonFile, bool>(af, false);
            }
            bool hasFilesToStage = true;
            try
            {
                switch (fileextension)
                {
                    case ".dds":
                    case ".png":
                    case ".jpg":
                    case ".jpeg":
                    case ".tga":
                    case ".bmp":
                    case ".mod":
                        af.ReadyStatusText = "Copying";
                        af.SetWorking();
                        File.Copy(extractSource, extractpath + "\\" + Path.GetFileName(extractSource), true);
                        af.ReadyStatusText = "Waiting for Addon to complete build";
                        af.SetIdle();
                        break;
                    case ".7z":
                    case ".zip":
                    case ".rar":
                        {
                            af.ReadyStatusText = "Extracting";
                            af.SetWorking();
                            //Extract file
                            int threads = Environment.ProcessorCount;
                            if (threads > 1)
                            {
                                threads--; //cores - 1
                            }
                            if (threads > 5)
                            {
                                threads = 5;
                            }

                            Log.Information(prefix + "Extracting file: " + extractSource);
                            string exe = BINARY_DIRECTORY + "7z.exe";
                            string args = "x -mmt" + threads + " -bsp2 \"" + extractSource + "\" -aoa -r -o\"" + extractpath + "\"";
                            ConsoleApp extractProcess = Run7zWithProgressForAddonFile(args, af);
                            while (extractProcess.State == AppState.Running)
                            {
                                Thread.Sleep(250);
                            }
                            var returncode = extractProcess.ExitCode;
                            if (returncode != 0)
                            {
                                af.ReadyStatusText = "Failed to extract";
                                af.SetError();
                                return new KeyValuePair<AddonFile, bool>(af, false);
                            }

                            if (af.UserFile)
                            {
                                af.ReadyStatusText = "Waiting for Addon to complete build";
                                af.SetIdle();
                                BuildWorker.ReportProgress(0, new ThreadCommand(INCREMENT_COMPLETION_EXTRACTION));
                                return new KeyValuePair<AddonFile, bool>(af, true);
                            }

                            af.ReadyStatusText = "Processing";

                            //get free space for debug purposes
                            Utilities.GetDiskFreeSpaceEx(stagingdirectory, out freeBytes, out diskSize, out totalFreeBytes);
                            Log.Information("[SIZE] ADDONEXTRACTFINISH Free Space on current drive: " + ByteSize.FromBytes(freeBytes) + " " + freeBytes);
                            var moveableFiles = Directory.EnumerateFiles(extractpath, "*.*", SearchOption.AllDirectories) //<--- .NET 4.5
                                .Where(file => file.ToLower().EndsWith("tpf") || file.ToLower().EndsWith("mem"))
                                .ToList();
                            if (moveableFiles.Count() > 0)
                            {
                                List<PackageFile> packageFiles = af.PackageFiles;
                                foreach (ChoiceFile cf in af.ChoiceFiles)
                                {
                                    Log.Information("Option chosen on " + af + ": Using choicefile " + cf.ChoiceTitle + ": " + cf.GetChosenFile().ChoiceTitle);
                                    packageFiles.Add(cf.GetChosenFile());
                                }
                                //check for copy directly items first, and move them.
                                foreach (string moveableFile in moveableFiles)
                                {
                                    string name = Utilities.GetRelativePath(moveableFile, extractpath);
                                    foreach (PackageFile pf in packageFiles)
                                    {
                                        if (pf.SourceName.Equals(name, StringComparison.InvariantCultureIgnoreCase))
                                        {
                                            string fname = Path.GetFileName(name);
                                            if (pf.MoveDirectly && pf.AppliesToGame(CURRENT_GAME_BUILD) && af.ALOTVersion > 0)
                                            {
                                                //It's an ALOT file. We will move this directly to the output directory.
                                                Log.Information("ALOT MAIN FILE - moving to output: " + fname + " -> " + getOutputDir(CURRENT_GAME_BUILD));
                                                string movename = getOutputDir(CURRENT_GAME_BUILD) + "000_" + fname;
                                                if (File.Exists(movename))
                                                {
                                                    File.Delete(movename);
                                                }
                                                File.Move(moveableFile, movename);
                                                pf.Processed = true; //no more ops on this package file.
                                                break;
                                            }
                                            if (pf.MoveDirectly && pf.AppliesToGame(CURRENT_GAME_BUILD) && af.ALOTUpdateVersion > 0)
                                            {
                                                //It's an ALOT update file. We will move this directly to the output directory.
                                                Log.Information("ALOT UPDATE FILE - moving to output: " + fname + " -> " + getOutputDir(CURRENT_GAME_BUILD));
                                                string movename = getOutputDir(CURRENT_GAME_BUILD) + "001_" + fname;
                                                if (File.Exists(movename))
                                                {
                                                    File.Delete(movename);
                                                }
                                                File.Move(moveableFile, movename);
                                                pf.Processed = true; //no more ops on this package file.
                                                break;
                                            }
                                            if (pf.MoveDirectly && pf.AppliesToGame(CURRENT_GAME_BUILD) && name.ToLower().EndsWith(".mem"))
                                            {
                                                //It's a already built MEM file. Move MEM to build folder
                                                Log.Information("MoveDirectly on MEM file specified - moving MEM to output: " + fname + " -> " + getOutputDir(CURRENT_GAME_BUILD));
                                                int fileprefix = Interlocked.Increment(ref PREBUILT_MEM_INDEX);
                                                string paddedVer = fileprefix.ToString("000");
                                                string movename = getOutputDir(CURRENT_GAME_BUILD) + paddedVer + "_" + fname;
                                                if (File.Exists(movename))
                                                {
                                                    File.Delete(movename);
                                                }
                                                File.Move(moveableFile, movename);
                                                pf.Processed = true; //no more ops on this package file.
                                                break;
                                            }
                                            if (pf.MoveDirectly && pf.AppliesToGame(CURRENT_GAME_BUILD))
                                            {
                                                SHOULD_HAVE_OUTPUT_FILE = true;
                                                Log.Information("MoveDirectly specified - moving TPF/MEM to staging: " + fname);
                                                File.Move(moveableFile, ADDON_FULL_STAGING_DIRECTORY + fname);
                                                pf.Processed = true; //no more ops on this package file.
                                                break;
                                            }
                                            if (pf.CopyDirectly && pf.AppliesToGame(CURRENT_GAME_BUILD))
                                            {
                                                SHOULD_HAVE_OUTPUT_FILE = true;
                                                Log.Information("CopyDirectly specified - copy TPF/MEM to staging: " + fname);
                                                File.Copy(moveableFile, ADDON_FULL_STAGING_DIRECTORY + fname, true);
                                                pf.Processed = true; //We will still extract this as it is a copy step.
                                                break;
                                            }
                                            if (pf.Delete && pf.AppliesToGame(CURRENT_GAME_BUILD))
                                            {
                                                Log.Information("Delete specified - deleting unused TPF/MEM: " + fname);
                                                File.Delete(moveableFile);
                                                pf.Processed = true; //no more ops on this package file.
                                                break;
                                            }
                                            if (!pf.AppliesToGame(CURRENT_GAME_BUILD) && name.ToLower().EndsWith(".mem"))
                                            {
                                                //Do not process this file. Mark it as processed.
                                                Log.Information("Extra MEM found in archive that is part of manifest file for different game than one being built for - deleting unused MEM: " + fname);
                                                File.Delete(moveableFile);
                                                pf.Processed = true;
                                                break;
                                            }
                                        }
                                    }
                                }

                                //Extract the TPFs
                                var tpfFilesList = Directory.EnumerateFiles(extractpath, "*.*", SearchOption.AllDirectories) //<--- .NET 4.5
                                .Where(file => file.ToLower().EndsWith("tpf"))
                                .ToList();
                                if (tpfFilesList.Count > 0)
                                {
                                    exe = BINARY_DIRECTORY + MEM_EXE_NAME;
                                    args = "-extract-tpf \"" + extractpath + "\" \"" + extractpath + "\"";
                                    Utilities.runProcess(exe, args);
                                }
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
                                        File.Copy(modfile, ADDON_FULL_STAGING_DIRECTORY + Path.GetFileName(modfile), true);
                                    }
                                }
                                else
                                {
                                    //Extract the MOD
                                    Log.Information("Extracting modfiles in directory: " + extractpath);

                                    exe = BINARY_DIRECTORY + MEM_EXE_NAME;
                                    args = "-extract-mod " + CURRENT_GAME_BUILD + " \"" + extractpath + "\" \"" + extractpath + "\"";
                                    Utilities.runProcess(exe, args);
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
                                    File.Move(memfile, stagingdirectory + "\\" + name + " - ME" + CURRENT_GAME_BUILD + ext);
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
                                    //Log.Information("Deleting existing file (if any): " + extractpath + "\\" + Path.GetFileName(file));
                                    if (File.Exists(destination))
                                    {
                                        Log.Information("Deleted existing file " + extractpath + "\\" + Path.GetFileName(file));
                                        File.Delete(destination);
                                    }
                                    Log.Information(file + " -> " + destination);
                                    File.Move(file, destination);
                                }
                                else
                                {
                                    Log.Information("File is already in correct place, no further processing necessary: " + extractpath + "\\" + Path.GetFileName(file));
                                }
                            }

                            //CopyFile and ZipFile
                            int stagedID = 1;
                            foreach (ZipFile zip in af.ZipFiles)
                            {
                                if (zip.IsSelectedForInstallation())
                                {
                                    //install
                                    string zipfile = Path.Combine(extractpath, zip.InArchivePath);
                                    string stagedPath = getOutputDir(CURRENT_GAME_BUILD) + af.BuildID + "_" + stagedID + "_" + Path.GetFileName(zip.InArchivePath);
                                    File.Move(zipfile, stagedPath);
                                    zip.ID = stagedID;
                                    stagedID++;
                                }
                            }

                            stagedID = 1;
                            foreach (CopyFile copy in af.CopyFiles)
                            {
                                if (copy.IsSelectedForInstallation())
                                {
                                    //install
                                    string zipfile = Path.Combine(extractpath, copy.InArchivePath);
                                    string stagedPath = getOutputDir(CURRENT_GAME_BUILD) + af.BuildID + "_" + stagedID + "_" + Path.GetFileName(copy.InArchivePath);
                                    File.Move(zipfile, stagedPath);
                                    copy.ID = stagedID;
                                    stagedID++;
                                }
                            }
                            BuildWorker.ReportProgress(0, new ThreadCommand(INCREMENT_COMPLETION_EXTRACTION));
                            break;
                        }
                    case ".tpf":
                        {
                            BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_ADDONUI_CURRENTTASK, "Preparing " + af.FriendlyName));
                            string destination = EXTRACTED_MODS_DIRECTORY + "\\" + af.BuildID + "\\" + Path.GetFileName(fileToUse);
                            if (af.UserFile)
                            {
                                destination = extractpath + "\\" + fileToUse;
                            }
                            File.Copy(extractSource, destination, true);

                            //extract files if we have any package files as we will need to obtain them.
                            if (af.UserFile || af.PackageFiles.Count > 0)
                            {
                                Log.Information(prefix + " Extracting AddonFile (TPF)");
                                string exe = BINARY_DIRECTORY + MEM_EXE_NAME;
                                string args = "-extract-tpf \"" + EXTRACTED_MODS_DIRECTORY + "\\" + af.BuildID + "\" \"" + EXTRACTED_MODS_DIRECTORY + "\\" + af.BuildID + "\"";
                                Utilities.runProcess(exe, args);

                                //Flatten Move files up to folder
                                List<string> files = new List<string>();
                                foreach (string file in Directory.EnumerateFiles(extractpath, "*.dds", SearchOption.AllDirectories))
                                {
                                    files.Add(file);
                                }

                                //move up one dir so the directory is now flattened
                                foreach (string file in files)
                                {
                                    destination = extractpath + "\\" + Path.GetFileName(file);
                                    if (!destination.ToLower().Equals(file.ToLower()))
                                    {
                                        //Log.Information("Deleting existing file (if any): " + extractpath + "\\" + Path.GetFileName(file));
                                        if (File.Exists(destination))
                                        {
                                            Log.Information("Deleted existing file " + extractpath + "\\" + Path.GetFileName(file));
                                            File.Delete(destination);
                                        }
                                        Log.Information(file + " -> " + destination);
                                        File.Move(file, destination);
                                    }
                                    else
                                    {
                                        Log.Information("File is already in correct place, no further processing necessary: " + extractpath + "\\" + Path.GetFileName(file));
                                    }
                                }
                            }
                            BuildWorker.ReportProgress(0, new ThreadCommand(INCREMENT_COMPLETION_EXTRACTION));
                            break;
                        }
                    case ".mem":
                        {
                            BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_ADDONUI_CURRENTTASK, "Preparing " + af.FriendlyName));
                            //Copy to output folder
                            if (!isSingleFileMode)
                            {
                                if (af.UserFile)
                                {
                                    //we will move this later in user build step. STage everything here
                                    File.Copy(af.UserFilePath, extractpath + "\\" + fileToUse, true);
                                    af.ReadyStatusText = "Waiting for Addon to complete build";
                                    af.SetIdle();
                                    BuildWorker.ReportProgress(0, new ThreadCommand(INCREMENT_COMPLETION_EXTRACTION));
                                    return new KeyValuePair<AddonFile, bool>(af, true);
                                }
                                else
                                {
                                    File.Copy(DOWNLOADED_MODS_DIRECTORY + "\\" + fileToUse, getOutputDir(CURRENT_GAME_BUILD) + fileToUse, true);
                                }
                            }
                            else
                            {
                                if (af.ALOTVersion > 0)
                                {
                                    //It's an ALOT file. We will move this directly to the output directory.
                                    Log.Information("ALOT MAIN FILE - Unpacked - moving to output: " + fileToUse);
                                    string movename = getOutputDir(CURRENT_GAME_BUILD) + "000_" + fileToUse;
                                    if (File.Exists(movename))
                                    {
                                        File.Delete(movename);
                                    }
                                    string importingfrom = Path.GetPathRoot(DOWNLOADED_MODS_DIRECTORY);
                                    string importingto = Path.GetPathRoot(getOutputDir(CURRENT_GAME_BUILD));
                                    af.ReadyStatusText = "Staging for installation";
                                    if (importingfrom == importingto)
                                    {
                                        File.Move(DOWNLOADED_MODS_DIRECTORY + "\\" + fileToUse, getOutputDir(CURRENT_GAME_BUILD) + "000_" + fileToUse);
                                    }
                                    else
                                    {
                                        string sfcError = null;
                                        // Instantiate the delegate using an anonymous method.
                                        SingleFileCopy.ProgressHandlerDel testDelC = (x) =>
                                        {
                                            af.ReadyStatusText = "Staging for installation - " + x.ProgressPercentage + "% (" + ByteSize.FromBytes(x.BytesReceived) + ")";
                                        };

                                        SingleFileCopy.ProgressCompleteDel completionDelegate = (x) =>
                                        {
                                            if (x.Error != null)
                                            {
                                                sfcError = x.Error.Message;
                                            }
                                            //af.ReadyStatusText = "Staging for installation - " + x.ProgressPercentage + "% (" + ByteSize.FromBytes(x.BytesReceived) + ")";
                                        };

                                        SingleFileCopy sfc = new SingleFileCopy();
                                        sfc.DownloadFile(DOWNLOADED_MODS_DIRECTORY + "\\" + fileToUse, getOutputDir(CURRENT_GAME_BUILD) + "000_" + fileToUse, testDelC, completionDelegate);
                                        if (sfcError != null)
                                        {
                                            ERROR_OCCURED_PLEASE_STOP = true;
                                            af.ReadyStatusText = "Failed to stage: " + sfcError;
                                            af.SetError();
                                            return new KeyValuePair<AddonFile, bool>(af, false);
                                        }
                                        //File.Copy(DOWNLOADED_MODS_DIRECTORY + "\\" + fileToUse, getOutputDir(CURRENT_GAME_BUILD) + "000_" + fileToUse);
                                    }
                                    foreach (PackageFile p in af.PackageFiles)
                                    {
                                        p.Processed = true; //No more processing on this addonfile.
                                    }

                                    BuildWorker.ReportProgress(0, new ThreadCommand(INCREMENT_COMPLETION_EXTRACTION));
                                    hasFilesToStage = false;
                                    break;
                                }
                            }
                            hasFilesToStage = false;
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
                af.ReadyStatusText = "Error occured during extraction";
                af.SetError();
                ERROR_OCCURED_PLEASE_STOP = true;
                return new KeyValuePair<AddonFile, bool>(af, false);
            }
            Utilities.GetDiskFreeSpaceEx(stagingdirectory, out freeBytes, out diskSize, out totalFreeBytes);

            Log.Information("[SIZE] ADDON EXTRACTADDON COMPLETED Free Space on current drive: " + ByteSize.FromBytes(freeBytes) + " " + freeBytes);
            string newText = TasksDisplayEngine.ReleaseTask(processingStr);
            if (newText != null)
            {
                BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_ADDONUI_CURRENTTASK, newText));
            }
            if (hasFilesToStage)
            {
                af.ReadyStatusText = "Queued for staging";
                af.SetIdle();
            }
            else
            {
                af.ReadyStatusText = "Staged for installation";
                af.SetIdle();
            }
            return new KeyValuePair<AddonFile, bool>(af, true);
        }

        private void BuildAddon(object sender, DoWorkEventArgs e)
        {
            Log.Information("Starting BuildAddon() thread. Performing build prechecks.");
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
            Log.Information("Starting Addon Extraction and Build via ExtractAddons()");
            bool result = ExtractAddons((int)e.Argument); //arg is game id.
            e.Result = result ? (int)e.Argument : -1; //-1 = Build Error
        }

        private bool ExtractAddons(int game)
        {
            Log.Information("Final output directory: " + getOutputDir(game) + ", exists: " + Directory.Exists(getOutputDir(game)));
            string stagingdirectory = ADDON_FULL_STAGING_DIRECTORY;
            PREBUILT_MEM_INDEX = 9;
            SHOULD_HAVE_OUTPUT_FILE = false; //will set to true later
            Log.Information("Extracting Addons and files for Mass Effect " + game);
            ulong freeBytes;
            ulong diskSize;
            ulong totalFreeBytes;
            bool gotFreeSpace = Utilities.GetDiskFreeSpaceEx(stagingdirectory, out freeBytes, out diskSize, out totalFreeBytes);
            Log.Information("[SIZE] PREBUILD Free Space on current drive: " + ByteSize.FromBytes(freeBytes) + " " + freeBytes);

            string basepath = MainWindow.DOWNLOADED_MODS_DIRECTORY;
            string destinationpath = EXTRACTED_MODS_DIRECTORY + "\\";
            Log.Information("Creating Data\\Extracted_Mods folder");
            Directory.CreateDirectory(destinationpath);

            ADDONFILES_TO_BUILD = new List<AddonFile>();
            ALOTVersionInfo CurrentGameALOTInfo = null;
            switch (game)
            {
                case 1:
                    CurrentGameALOTInfo = CURRENTLY_INSTALLED_ME1_ALOT_INFO;
                    break;
                case 2:
                    CurrentGameALOTInfo = CURRENTLY_INSTALLED_ME2_ALOT_INFO;
                    break;
                case 3:
                    CurrentGameALOTInfo = CURRENTLY_INSTALLED_ME3_ALOT_INFO;
                    break;
            }

            //compile list of addon files to process
            foreach (AddonFile af in alladdonfiles)
            {
                if (!af.Enabled)
                {
                    continue;
                }
                if (af.Ready && (game == 1 && af.Game_ME1 || game == 2 && af.Game_ME2 || game == 3 && af.Game_ME3))
                {
                    if (CurrentGameALOTInfo != null)
                    {
                        //Detected MEMI tag
                        //Check if ALOT main file is installed. If it is and this is ALOT file, skip
                        if (af.ALOTVersion > 0 && CurrentGameALOTInfo.ALOTVER >= 0 && BUILD_ALOT == false)
                        {
                            Log.Information("ALOT File in queue for processing but ALOT is already installed. Skipping...");
                            af.ReadyStatusText = "ALOT already installed";
                            continue; //skip
                        }

                        if (af.ALOTUpdateVersion > 0 && CurrentGameALOTInfo.ALOTVER == 0)
                        {
                            Log.Information("ALOT update file in queue but unknown if it applies due to unknown ALOT version. Skipping...");
                            af.ReadyStatusText = "Restore to unmodified to install this file";
                            af.SetError();
                            continue; //skip
                        }

                        //Check if ALOT update file of this version or higher is installed. If it is and this is ALOT update file, skip
                        if (af.ALOTUpdateVersion > 0 && CurrentGameALOTInfo.ALOTUPDATEVER >= af.ALOTUpdateVersion && BUILD_ALOT_UPDATE == false)
                        {
                            Log.Information("ALOT Update File in queue for processing, but ALOT update of this version or greater is already installed. Skipping...");
                            af.ReadyStatusText = "Update already applied";
                            continue;
                        }

                        //Check if ALOT update file applies to this version of ALOT main
                        if (af.ALOTUpdateVersion > 0 && CurrentGameALOTInfo.ALOTVER != af.ALOTMainVersionRequired)
                        {
                            Log.Information("ALOT Update available but currently installed ALOT version does not match. Skipping...");
                            af.ReadyStatusText = "Update not applicable";
                            continue;
                        }

                        if (af.MEUITM && !BUILD_MEUITM)
                        {
                            Log.Information("MEUITM is already installed and user has not selected it for installation, skipping...");
                            continue; //skip
                        }
                    }

                    if (af.UserFile && af.Enabled && BUILD_USER_FILES)
                    {
                        Log.Information("Adding User to build list: " + af.FriendlyName);
                        af.Building = true;
                        ADDONFILES_TO_BUILD.Add(af);
                    }

                    if (af.ALOTUpdateVersion == 0 && af.ALOTVersion == 0 && !af.UserFile && !af.MEUITM && BUILD_ADDON_FILES)
                    {
                        Log.Information("Adding AddonFile to build list: " + af.FriendlyName);
                        af.Building = true;
                        ADDONFILES_TO_BUILD.Add(af);

                    }

                    if (af.MEUITM && BUILD_MEUITM)
                    {
                        Log.Information("Adding MEUITM to build list: " + af.FriendlyName);
                        af.Building = true;
                        ADDONFILES_TO_BUILD.Add(af);
                    }

                    if ((af.ALOTVersion > 0 && BUILD_ALOT) || af.ALOTUpdateVersion > 0)
                    {
                        if (af.Enabled)
                        {
                            ADDONFILES_TO_BUILD.Add(af);
                            af.Building = true;
                        }
                    }
                }
            }

            BuildWorker.ReportProgress(completed, new ThreadCommand(SET_VISIBILE_ITEMS_LIST, ADDONFILES_TO_BUILD));
            int id = 0;
            foreach (AddonFile af in ADDONFILES_TO_BUILD)
            {
                af.BuildID = (++id).ToString().PadLeft(3, '0');
                Log.Information("Build ID for " + af.FriendlyName + ": " + af.BuildID);
            }
            //DISK SPACE CHECK
            ulong fullsize = 0;
            foreach (AddonFile af in ADDONFILES_TO_BUILD)
            {

                string file = af.GetFile();
                if (!File.Exists(file))
                {
                    //RIP
                    Log.Error("FILE FOR PROCESSING HAS GONE MISSING SINCE PRECHECK: " + file);
                    Log.Error("Aborting build");
                    BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_ADDONUI_CURRENTTASK, "Build aborted"));
                    BuildWorker.ReportProgress(completed, new ThreadCommand(SHOW_DIALOG, new KeyValuePair<string, string>("File for processing no longer available", "The following file is no longer available for processing:\n" + file + "\n\nBuild has been aborted.")));
                    return false;
                }
                ulong size = (ulong)((new FileInfo(file).Length) * 2.5);
                fullsize += size;
            }

            Utilities.GetDiskFreeSpaceEx(EXE_DIRECTORY, out freeBytes, out diskSize, out totalFreeBytes);
            Log.Information("We will need around " + ByteSize.FromBytes(fullsize) + " to build this set. The free space is " + ByteSize.FromBytes(freeBytes));
            if (freeBytes < fullsize)
            {
                Log.Error("Not enough space to build textures locally. We only have " + ByteSize.FromBytes(freeBytes) + " available but we need " + ByteSize.FromBytes(fullsize));
                //not enough disk space for build
                BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_HEADER_LABEL, "Not enough free space to build textures.\nYou will need around " + ByteSize.FromBytes(fullsize) + " of free space on " + Path.GetPathRoot(EXE_DIRECTORY) + " to build the installation packages."));
                BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_ADDONUI_CURRENTTASK, "Build aborted"));
                BuildWorker.ReportProgress(completed, new ThreadCommand(SHOW_DIALOG, new KeyValuePair<string, string>("Not enough free space to build textures", "You will need around " + ByteSize.FromBytes(fullsize) + " of free space on " + Path.GetPathRoot(EXE_DIRECTORY) + " to build the installation packages.")));

                BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_PROGRESSBAR_INDETERMINATE, false));
                return false;
            }

            foreach (AddonFile af in ADDONFILES_TO_BUILD)
            {
                af.ReadyStatusText = "Queued for processing";
                af.SetWorking();
            }

            ADDONSTOBUILD_COUNT = ADDONFILES_TO_BUILD.Count;
            completed = 0;
            BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_HEADER_LABEL, "Preparing files for installation.\nThis may take a few minutes."));
            BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_ADDONUI_CURRENTTASK, "Extracting Mods..."));
            BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_PROGRESSBAR_INDETERMINATE, true));

            BuildWorker.ReportProgress(0);

            bool modextractrequired = false; //not used currently.

            int threads = Environment.ProcessorCount;
            if (threads > 1)
            {
                threads--; //cores - 1
            }
            if (threads > 5)
            {
                threads = 5;
            }
            ERROR_OCCURED_PLEASE_STOP = false;
            TaskbarProgressIndeterminateManaged = true;
            BuildWorker.ReportProgress(0, new ThreadCommand(SET_TASKBAR_INDETERMINATE, true));
            Stopwatch sw = Stopwatch.StartNew();
            Log.Information("Starting addon extraction in parallel. Number of threads: " + threads);
            KeyValuePair<AddonFile, bool>[] results = ADDONFILES_TO_BUILD.AsParallel().WithDegreeOfParallelism(threads).WithExecutionMode(ParallelExecutionMode.ForceParallelism).Select(ExtractAddon).ToArray();
            sw.Stop();
            TaskbarProgressIndeterminateManaged = false;
            BuildWorker.ReportProgress(0, new ThreadCommand(SET_TASKBAR_INDETERMINATE, false));
            foreach (KeyValuePair<AddonFile, bool> result in results)
            {
                bool successful = result.Value;
                AddonFile af = result.Key;
                if (!successful)
                {
                    Log.Error("Failed to extract " + af.GetFile());
                    if (af.FileMD5 != null && af.FileMD5 != "" && !af.UserFile && !af.IsCurrentlySingleFile())
                    {
                        if (af.GetFile() != null)
                        {
                            Log.Information("MD5 checksumming " + af.GetFile());
                            af.SetWorking();
                            af.ReadyStatusText = "Checking file";
                            BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_PROGRESSBAR_INDETERMINATE, true));
                            BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_ADDONUI_CURRENTTASK, "Checking files that failed to extract"));
                            var md5 = Utilities.CalculateMD5(af.GetFile());
                            if (md5 != af.FileMD5)
                            {
                                af.SetError();
                                af.ReadyStatusText = "File is corrupt";
                                Log.Error("Checksum for " + af.FriendlyName + " is wrong. Imported MD5: " + md5 + ", manifest MD5: " + af.FileMD5);
                                Log.Error("This file cannot be used");
                                KeyValuePair<string, string> message = new KeyValuePair<string, string>("File for " + af.FriendlyName + " is corrupt", "An error occured extracting " + af.GetFile() + ". This file is corrupt. Delete this file and download a new copy of it.");
                                BuildWorker.ReportProgress(0, new ThreadCommand(SHOW_DIALOG, message));
                            }
                            else
                            {
                                af.SetError();
                                af.ReadyStatusText = "Failed to extract";
                                Log.Warning("Checksum for " + af.FriendlyName + " is correct, but the file failed to extract. Imported MD5: " + md5 + ", manifest MD5: " + af.FileMD5);
                                Log.Warning("This file should still be usable - but why did extraction or processing fail?");
                                KeyValuePair<string, string> message = new KeyValuePair<string, string>(af.FriendlyName + " failed to process", "An error occured extracting or processing " + af.GetFile() + ". This file does not appear to be corrupt. Attempt installation again, and if it continues to fail, come to the ALOT Discord (Settings -> Report an issue) to help troubleshoot this issue.");
                                BuildWorker.ReportProgress(0, new ThreadCommand(SHOW_DIALOG, message));
                            }
                        }
                        else
                        {
                            Log.Warning("File does not exist, may have failed staging: " + af.FriendlyName + ".");
                            KeyValuePair<string, string> message = new KeyValuePair<string, string>(af.FriendlyName + " failed to stage", "An error occured staging " + af.GetFile() + ". Try again or come to the ALOT discord server  (Settings -> Report an issue) if this error keeps occuring.");
                            BuildWorker.ReportProgress(0, new ThreadCommand(SHOW_DIALOG, message));
                        }
                        //perform MD5
                    }
                    else
                    {
                        BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_ADDONUI_CURRENTTASK, "Failed to extract " + af.FriendlyName + ". Check the logs for more information."));
                        KeyValuePair<string, string> messageStr = new KeyValuePair<string, string>("Error extracting " + af.FriendlyName, "An error occured extracting " + af.GetFile() + ". This file may be corrupt. Please check if you can open it in a archive program - if not, this file will need to be deleted.");
                        BuildWorker.ReportProgress(0, new ThreadCommand(SHOW_DIALOG, messageStr));
                        CURRENT_GAME_BUILD = 0; //reset
                    }
                    TasksDisplayEngine.ClearTasks();

                    BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_PROGRESSBAR_INDETERMINATE, false));
                    return false;
                }
            }
            TasksDisplayEngine.ClearTasks();
            if (ERROR_OCCURED_PLEASE_STOP)
            {
                return false;
            }
            var tpfFilesList = Directory.EnumerateFiles(EXTRACTED_MODS_DIRECTORY, "*.*", SearchOption.TopDirectoryOnly) //<--- .NET 4.5
                                .Where(file => file.ToLower().EndsWith("tpf"))
                                .ToList();
            if (tpfFilesList.Count > 0)
            {
                //if (tpfextractrequired)

                BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_PROGRESSBAR_INDETERMINATE, true));
                BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_ADDONUI_CURRENTTASK, "Extracting TPFs..."));
                BuildWorker.ReportProgress(0);

                Log.Information("Extracting " + tpfFilesList.Count + " TPF files.");
                string exe = BINARY_DIRECTORY + MEM_EXE_NAME;
                string args = "-extract-tpf \"" + EXTRACTED_MODS_DIRECTORY + "\" \"" + EXTRACTED_MODS_DIRECTORY + "\"";
                Utilities.runProcess(exe, args);
            }

            if (modextractrequired)
            {
                BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_PROGRESSBAR_INDETERMINATE, true));
                BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_ADDONUI_CURRENTTASK, "Extracting MOD files..."));
                BuildWorker.ReportProgress(0);

                Log.Information("Extracting MOD files.");
                string exe = BINARY_DIRECTORY + MEM_EXE_NAME;
                string args = "-extract-mod " + game + " \"" + DOWNLOADED_MODS_DIRECTORY + "\" \"" + EXTRACTED_MODS_DIRECTORY + "\"";
                Utilities.runProcess(exe, args);
            }

            BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_ADDONUI_CURRENTTASK, "Staging Addon texture files for building..."));
            BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_PROGRESSBAR_INDETERMINATE, false));

            //Calculate how many files to install...
            int totalfiles = 0;
            foreach (AddonFile af in ADDONFILES_TO_BUILD)
            {
                if (af.UserFile)
                {
                    continue;
                }
                totalfiles += af.PackageFiles.Count;
                //Debug.WriteLine(totalfiles);
            }

            basepath = EXTRACTED_MODS_DIRECTORY;
            int numcompleted = 0;
            foreach (AddonFile af in ADDONFILES_TO_BUILD)
            {
                if (af.UserFile)
                {
                    continue;
                }
                af.SetWorking();
                af.ReadyStatusText = "Staging files for ALOT Addon";
            }

            foreach (AddonFile af in ADDONFILES_TO_BUILD)
            {
                if (af.UserFile)
                {
                    continue;
                }

                if (af.CopyDirectly)
                {
                    string sourcefile = basepath + "\\" + af.BuildID + "\\" + Path.GetFileName(af.GetFile());
                    string destination = stagingdirectory + Path.GetFileName(af.GetFile());

                    Log.Information("Copy Directly for AddonFile: " + sourcefile + "->" + destination);
                    File.Copy(sourcefile, destination, true);
                    SHOULD_HAVE_OUTPUT_FILE = true;
                }

                if (af.PackageFiles.Count > 0)
                {
                    foreach (PackageFile pf in af.PackageFiles)
                    {
                        if ((game == 1 && pf.ME1 || game == 2 && pf.ME2 || game == 3 && pf.ME3) && !pf.Processed)
                        {
                            string extractedpath = basepath + "\\" + af.BuildID + "\\" + pf.SourceName;
                            if (File.Exists(extractedpath) && pf.DestinationName != null)
                            {
                                Log.Information("Copying Package File: " + pf.SourceName + "->" + pf.DestinationName);
                                string destination = stagingdirectory + pf.DestinationName;
                                File.Copy(extractedpath, destination, true);
                                SHOULD_HAVE_OUTPUT_FILE = true;
                            }
                            else if (pf.DestinationName == null)
                            {
                                Log.Error("File destination is null. This means there is a problem in the manifest or manifest as a packagefile was not previously marked as processed and does not have destinationname assigned for the staging step. AddonFile: " + af.FriendlyName + ", PackageFile: " + pf.SourceName);
                                af.SetError();
                                af.ReadyStatusText = "Staged with errors";
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
                af.SetIdle();
                af.ReadyStatusText = "Waiting for other files to finish staging";
            }
            bool hasUserFiles = false;
            bool hasAddonFiles = false;
            List<AddonFile> AddonFilesToSetStatusFor = new List<AddonFile>();
            foreach (AddonFile af in ADDONFILES_TO_BUILD)
            {
                if (af.ALOTUpdateVersion == 0 && af.ALOTVersion == 0 && !af.UserFile && !af.MEUITM)
                {
                    hasAddonFiles = true;
                }
                if (af.ALOTUpdateVersion > 0 || af.ALOTVersion > 0 || af.UserFile || af.MEUITM)
                {
                    if (af.UserFile)
                    {
                        hasUserFiles = true;
                        af.ReadyStatusText = "Waiting for Addon to complete build";
                        af.SetIdle();
                    }
                    else
                    {
                        af.ReadyStatusText = "Staged for installation";
                        af.SetIdle();
                    }

                    continue;
                }
                bool requiresBuild = false;
                foreach (PackageFile pf in af.PackageFiles)
                {
                    if (pf.Processed != true)
                    {
                        requiresBuild = true;
                        af.ReadyStatusText = "Waiting for cleanup to complete";
                        af.SetIdle();
                        AddonFilesToSetStatusFor.Add(af); //set to building into mem file... after cleanup finishes
                        break;
                    }
                }
                if (!requiresBuild)
                {
                    af.ReadyStatusText = "Staged for installation";
                    af.SetIdle();
                }
            }


            Utilities.GetDiskFreeSpaceEx(stagingdirectory, out freeBytes, out diskSize, out totalFreeBytes);
            Log.Information("[SIZE] POSTEXTRACT_PRESTAGING Free Space on current drive: " + ByteSize.FromBytes(freeBytes) + " " + freeBytes);

            //CLEANUP EXTRACTION DIR
            Log.Information("Completed staging. Now cleaning up extraction directory");
            BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_ADDONUI_CURRENTTASK, "Cleaning up extraction directory"));
            BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_PROGRESSBAR_INDETERMINATE, true));
            BuildWorker.ReportProgress(100);
            try
            {
                Utilities.DeleteFilesAndFoldersRecursively(EXTRACTED_MODS_DIRECTORY);
                Log.Information("Deleted Extracted_Mods directory");
            }
            catch (IOException e)
            {
                Log.Error("Unable to delete extraction directory: " + e.Message);
            }

            Utilities.GetDiskFreeSpaceEx(stagingdirectory, out freeBytes, out diskSize, out totalFreeBytes);
            Log.Information("[SIZE] AFTER_EXTRACTION_CLEANUP Free Space on current drive: " + ByteSize.FromBytes(freeBytes) + " " + freeBytes);

            foreach (AddonFile af in AddonFilesToSetStatusFor)
            {
                af.ReadyStatusText = "Building into Addon MEM file";
                af.SetWorking();
            }
            //BUILD MEM PACKAGE
            int mainBuildResult = SHOULD_HAVE_OUTPUT_FILE ? -2 : 0; //if we have no files just set return code for addon to 0
            if (SHOULD_HAVE_OUTPUT_FILE)
            {
                BuildWorker.ReportProgress(0);
                BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_HEADER_LABEL, "Building ALOT Addon for Mass Effect" + GetGameNumberSuffix(CURRENT_GAME_BUILD) + ".\nThis may take a few minutes."));

                BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_ADDONUI_CURRENTTASK, "Building Addon MEM Package..."));
                {
                    Log.Information("Building MEM Package.");
                    string exe = BINARY_DIRECTORY + MEM_EXE_NAME;
                    string filename = "002_ALOT_ME" + game + "_Addon.mem";
                    string args = "-convert-to-mem " + game + " \"" + ADDON_FULL_STAGING_DIRECTORY.TrimEnd('\\') + "\" \"" + getOutputDir(game) + filename + "\" -ipc";

                    runMEM_BackupAndBuild(exe, args, BuildWorker);
                    while (BACKGROUND_MEM_PROCESS.State == AppState.Running)
                    {
                        Thread.Sleep(250);
                    }

                    foreach (AddonFile af in ADDONFILES_TO_BUILD)
                    {
                        if (af.ALOTVersion > 0)
                        {
                            af.ReadyStatusText = "ALOT ready to install";
                            af.SetIdle();
                        }
                        else if (af.ALOTUpdateVersion > 0)
                        {
                            af.ReadyStatusText = "ALOT update ready to install";
                            af.SetIdle();
                        }
                        if (!af.UserFile)
                        {
                            af.ReadyStatusText = "Built into ALOT Addon file";
                            af.SetIdle();
                        }
                        else
                        {
                            af.SetWorking();
                            af.ReadyStatusText = "Queued for user file build";
                        }
                    }

                    Utilities.GetDiskFreeSpaceEx(stagingdirectory, out freeBytes, out diskSize, out totalFreeBytes);
                    Log.Information("[SIZE] POST_MEM_BUILD Free Space on current drive: " + ByteSize.FromBytes(freeBytes) + " " + freeBytes);

                    mainBuildResult = BACKGROUND_MEM_PROCESS.ExitCode ?? 6000;
                    BACKGROUND_MEM_PROCESS = null;
                    BACKGROUND_MEM_PROCESS_ERRORS = null;
                    BACKGROUND_MEM_PROCESS_PARSED_ERRORS = null;
                    if (mainBuildResult != 0)
                    {
                        Log.Error("Non-Zero return code! Something probably went wrong.");
                    }
                    else if (mainBuildResult == 0 && !File.Exists(getOutputDir(game) + filename) && SHOULD_HAVE_OUTPUT_FILE)
                    {

                        Log.Error("Process went OK but no outputfile... Something probably went wrong.");
                        mainBuildResult = -1;
                    }
                    else
                    {

                        foreach (AddonFile af in ADDONFILES_TO_BUILD)
                        {
                            if (!af.UserFile)
                            {
                                af.ReadyStatusText = "Built into Addon MEM file";
                                af.SetIdle();
                            }
                        }
                    }
                }
            }
            else
            {
                Log.Information("We don't need to build addon mem as all files added were either not true addon files or don't need to be built into mem files.");
            }
            //cleanup staging
            if (Directory.Exists(ADDON_FULL_STAGING_DIRECTORY))
            {
                BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_ADDONUI_CURRENTTASK, "Cleaning up addon staging directory"));
                BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_PROGRESSBAR_INDETERMINATE, true));
                BuildWorker.ReportProgress(100);
                try
                {
                    Utilities.DeleteFilesAndFoldersRecursively(ADDON_FULL_STAGING_DIRECTORY);
                    Log.Information("Deleted " + ADDON_FULL_STAGING_DIRECTORY);
                }
                catch (IOException e)
                {
                    Log.Error("Unable to delete staging directory. Addon should have been built however.\n" + e.ToString());
                }
            }

            //build each user file
            bool hadUserErrors = false;
            int userBuildResult = 0;
            string userBuildErrors = "The following user supplied files did not compile into usable MEM packages:";
            if (hasUserFiles)
            {
                USERFILE_INDEX = 100;
                //BUILD USER FILES
                foreach (AddonFile af in ADDONFILES_TO_BUILD)
                {
                    if (!af.UserFile)
                    {
                        continue;
                    }
                    string userFileExtractedPath = USER_FULL_STAGING_DIRECTORY + af.BuildID;
                    if (!Directory.Exists(userFileExtractedPath))
                    {
                        continue;
                    }
                    BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_PROGRESSBAR_INDETERMINATE, false));
                    BuildWorker.ReportProgress(0);

                    var mems = Directory.GetFiles(userFileExtractedPath, "*.mem", SearchOption.AllDirectories);
                    var installerinis = Directory.GetFiles(userFileExtractedPath, "*.mem", SearchOption.AllDirectories);

                    bool hasMems = mems.Count() > 0;
                    foreach (string mem in mems)
                    {
                        string memdest = getOutputDir(game) + USERFILE_INDEX.ToString("000") + "_UserAddon_" + Path.GetFileNameWithoutExtension(mem) + ".mem";
                        File.Move(mem, memdest);
                        USERFILE_INDEX++;
                    }

                    if (hasMems)
                    {
                        //check if any files remain
                        var remainingFiles = Directory.GetFiles(userFileExtractedPath, "*", SearchOption.AllDirectories);
                        if (remainingFiles.Count() == 0)
                        {
                            af.SetIdle();
                            af.ReadyStatusText = "User file is ready for install";
                            continue;
                        }
                    }


                    BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_HEADER_LABEL, "Building User Addons for Mass Effect" + GetGameNumberSuffix(CURRENT_GAME_BUILD) + ".\nDon't close the window until this operation completes."));
                    af.ReadyStatusText = "Building user MEM file from mod files";
                    af.SetWorking();

                    Log.Information("Building User MEM file from staging directory: " + userFileExtractedPath);
                    string exe = BINARY_DIRECTORY + MEM_EXE_NAME;
                    string filename = USERFILE_INDEX.ToString("000") + "_UserAddon_" + Path.GetFileNameWithoutExtension(af.UserFilePath) + ".mem";
                    string args = "-convert-to-mem " + game + " \"" + userFileExtractedPath + "\" \"" + getOutputDir(game) + filename + "\" -ipc";
                    CURRENT_USER_BUILD_FILE = af.FriendlyName;
                    runMEM_BackupAndBuild(exe, args, BuildWorker);
                    while (BACKGROUND_MEM_PROCESS.State == AppState.Running)
                    {
                        Thread.Sleep(250);
                    }

                    userBuildResult |= BACKGROUND_MEM_PROCESS.ExitCode ?? 6000;
                    if (hasMems)
                    {
                        userBuildResult = 0; //if it has mems we don't care
                    }
                    if (BACKGROUND_MEM_PROCESS_PARSED_ERRORS.Count() == 0 || hasMems)
                    {
                        af.SetIdle();
                        af.ReadyStatusText = "User file is ready for install";
                    }
                    else
                    {
                        if (!hasMems)
                        {
                            hadUserErrors = true;
                            af.SetError();
                            af.ReadyStatusText = "Error building MEM file";
                            foreach (string str in BACKGROUND_MEM_PROCESS_PARSED_ERRORS)
                            {
                                userBuildErrors += "\n - " + str;
                            }
                        }
                    }
                    if (BACKGROUND_MEM_PROCESS.ExitCode != 0 && !hasMems)
                    {
                        Log.Error("Non-Zero return code for user file! Something probably went wrong.");
                    }
                    if (BACKGROUND_MEM_PROCESS.ExitCode == 0 && !File.Exists(getOutputDir(game) + filename) && SHOULD_HAVE_OUTPUT_FILE && !hasMems)
                    {
                        Log.Error("Process went OK but no outputfile... Something probably went wrong with userfile.");
                    }

                    USERFILE_INDEX++;
                    BACKGROUND_MEM_PROCESS = null;
                    BACKGROUND_MEM_PROCESS_ERRORS = null;
                    CURRENT_USER_BUILD_FILE = null;
                }
            }

            //cleanup staging
            if (Directory.Exists(USER_FULL_STAGING_DIRECTORY))
            {
                BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_ADDONUI_CURRENTTASK, "Cleaning up user files staging directory"));
                BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_PROGRESSBAR_INDETERMINATE, true));
                BuildWorker.ReportProgress(100);
                try
                {
                    Utilities.DeleteFilesAndFoldersRecursively(USER_FULL_STAGING_DIRECTORY);
                    Log.Information("Deleted " + USER_FULL_STAGING_DIRECTORY);
                }
                catch (IOException e)
                {
                    Log.Error("Unable to delete user staging directory. User addon's have (attempted) build however.\n" + e.ToString());
                }
                if (hadUserErrors)
                {
                    BuildWorker.ReportProgress(completed, new ThreadCommand(SHOW_DIALOG, new KeyValuePair<string, string>("Some user files did not successfully build", userBuildErrors)));
                }
            }

            Utilities.GetDiskFreeSpaceEx(stagingdirectory, out freeBytes, out diskSize, out totalFreeBytes);
            Log.Information("[SIZE] FINAL Free Space on current drive: " + ByteSize.FromBytes(freeBytes) + " " + freeBytes);
            BuildWorker.ReportProgress(completed, new ThreadCommand(UPDATE_PROGRESSBAR_INDETERMINATE, false));
            //CURRENT_GAME_BUILD = 0; //reset
            bool addonResult = true;
            bool userResult = true;
            if (hasAddonFiles)
            {
                addonResult = mainBuildResult == 0;
            }
            if (hasUserFiles)
            {
                userResult = userBuildResult == 0;
            }
            return addonResult && userResult;
        }

        private async void BackupWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (e.UserState is null)
            {
                ProgressBarValue = e.ProgressPercentage;
                TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.Normal, this);
                TaskbarManager.Instance.SetProgressValue(e.ProgressPercentage, 100);
            }
            else
            {
                ThreadCommand tc = (ThreadCommand)e.UserState;
                switch (tc.Command)
                {
                    case RESTORE_FAILED_COULD_NOT_DELETE_FOLDER:
                        Log.Error("Restore has failed - could not delete existing installation. Some may be missing - consider this game installation ruined and requires a restore now.");
                        await this.ShowMessageAsync("Restore failed", "Could not delete the existing game directory. This is usually due to something still open (such as the game), or running something from within the game folder. Close other programs and try again.");
                        return;
                    case UPDATE_ADDONUI_CURRENTTASK:
                        AddonFilesLabel.Text = (string)tc.Data;
                        break;
                    case UPDATE_HEADER_LABEL:
                        HeaderLabel.Text = (string)tc.Data;
                        break;
                    case UPDATE_PROGRESSBAR_INDETERMINATE:
                        TaskbarManager.Instance.SetProgressState((bool)tc.Data ? TaskbarProgressBarState.Indeterminate : TaskbarProgressBarState.Normal);
                        Build_ProgressBar.IsIndeterminate = (bool)tc.Data;
                        break;
                    case SET_TASKBAR_INDETERMINATE:
                        TaskbarManager.Instance.SetProgressState((bool)tc.Data ? TaskbarProgressBarState.Indeterminate : TaskbarProgressBarState.NoProgress);
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

                }
            }
        }

        private void VerifyAndBackupGame(object sender, DoWorkEventArgs e)
        {
            BackupWorker.ReportProgress(completed, new ThreadCommand(UPDATE_ADDONUI_CURRENTTASK, "Calculating space requirements..."));
            BackupWorker.ReportProgress(completed, new ThreadCommand(UPDATE_PROGRESSBAR_INDETERMINATE, true));


            //Get size
            string backupPath = (string)e.Argument;

            long dirsize = Utilities.DirSize(new DirectoryInfo(Utilities.GetGamePath(BACKUP_THREAD_GAME)));
            dirsize = Convert.ToInt64(dirsize * 1.1);


            ulong freeBytes;
            ulong diskSize;
            ulong totalFreeBytes;
            bool gotFreeSpace = Utilities.GetDiskFreeSpaceEx(backupPath, out freeBytes, out diskSize, out totalFreeBytes);

            if ((long)freeBytes < dirsize)
            {
                Log.Error("Not enough free space on drive for backup. We need " + ByteSize.FromBytes(dirsize) + " but we only have " + ByteSize.FromBytes(freeBytes));
                BackupWorker.ReportProgress(completed, new ThreadCommand(SHOW_DIALOG, new KeyValuePair<string, string>("Not enough free space on drive", "There is not enough space on " + Path.GetPathRoot(backupPath) + " to store a backup.\nFree space: " + ByteSize.FromBytes(freeBytes) + "\nRequired space: " + ByteSize.FromBytes(dirsize))));
                e.Result = null;
                return;
            }



            //verify vanilla
            Log.Information("Verifying game: Mass Effect " + BACKUP_THREAD_GAME);
            string exe = BINARY_DIRECTORY + MEM_EXE_NAME;
            string args = "-check-game-data-only-vanilla " + BACKUP_THREAD_GAME + " -ipc";
            List<string> acceptedIPC = new List<string>();
            acceptedIPC.Add("TASK_PROGRESS");
            acceptedIPC.Add("ERROR");
            BackupWorker.ReportProgress(completed, new ThreadCommand(UPDATE_ADDONUI_CURRENTTASK, "Verifying game data..."));

            runMEM_BackupAndBuild(exe, args, BackupWorker, acceptedIPC);
            while (BACKGROUND_MEM_PROCESS.State == AppState.Running)
            {
                Thread.Sleep(250);
            }
            int backupVerifyResult = BACKGROUND_MEM_PROCESS.ExitCode ?? 1;
            if (backupVerifyResult != 0)
            {
                string modified = "";
                string gameDir = Utilities.GetGamePath(BACKUP_THREAD_GAME);
                foreach (String error in BACKGROUND_MEM_PROCESS_ERRORS)
                {
                    modified += "\n - " + error;
                    //.Remove(0, gameDir.Length + 1);
                }
                Log.Warning("Backup verification failed. Allowing user to choose to continue or not");
                ThreadCommandDialogOptions tcdo = new ThreadCommandDialogOptions();
                tcdo.signalHandler = new EventWaitHandle(false, EventResetMode.AutoReset);
                tcdo.title = "Game is modified";
                tcdo.message = "Mass Effect" + GetGameNumberSuffix(BACKUP_THREAD_GAME) + " has files that do not match what is in the MEM database.\nYou can continue to back this installation up, but it may not be truly unmodified." + modified;
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
                    Log.Warning("User continuing even with non-vanilla backup.");
                    CONTINUE_BACKUP_EVEN_IF_VERIFY_FAILS = false; //reset
                }
            }
            else
            {
                Log.Information("Backup verification passed - no issues.");
            }
            string gamePath = Utilities.GetGamePath(BACKUP_THREAD_GAME);
            string[] ignoredExtensions = { ".wav", ".pdf", ".bak" };
            if (gamePath != null)
            {
                Log.Information("Creating backup... Only errors will be reported.");
                try
                {
                    CopyDir.CopyAll_ProgressBar(new DirectoryInfo(gamePath), new DirectoryInfo(backupPath), BackupWorker, this, -1, 0, ignoredExtensions);
                }
                catch (Exception ex)
                {
                    Log.Error("Error creating backup:");
                    Log.Error(App.FlattenException(ex));
                    BackupWorker.ReportProgress(completed, new ThreadCommand(SHOW_DIALOG, new KeyValuePair<string, string>("Backup failed", "Backup of Mass Effect" + GetGameNumberSuffix(BACKUP_THREAD_GAME) + " failed. An error occured during the copy process. The error message was: " + ex.Message + ".\nSome files may have been copied, but this backup is not usable. You can delete the folder you were backing up files into.\nReview the installer log for more information.")));
                    Progressbar_Max = 100;
                    e.Result = null;
                    return;
                }
                Progressbar_Max = 100;
                Log.Information("Backup copy created");
            }
            if (BACKUP_THREAD_GAME == 3)
            {
                //Create Mod Manaager vanilla backup marker
                string file = backupPath + "\\cmm_vanilla";
                File.Create(file);
            }
            e.Result = backupPath;
        }

        private void runMEM_BackupAndBuild(string exe, string args, BackgroundWorker worker, List<string> acceptedIPC = null)
        {
            Debug.WriteLine("Running process: " + exe + " " + args);
            Log.Information("Running process: " + exe + " " + args);
            BACKGROUND_MEM_PROCESS = new ConsoleApp(exe, args);
            BACKGROUND_MEM_PROCESS_ERRORS = new List<string>();
            BACKGROUND_MEM_PROCESS_PARSED_ERRORS = new List<string>();
            BACKGROUND_MEM_PROCESS.ConsoleOutput += (o, args2) =>
            {
                string str = args2.Line;
                if (str.StartsWith("[IPC]", StringComparison.Ordinal))
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
                            case "TASK_PROGRESS":
                                worker.ReportProgress(completed, new ThreadCommand(UPDATE_PROGRESSBAR_INDETERMINATE, false));
                                int percentInt = Convert.ToInt32(param);
                                worker.ReportProgress(percentInt);
                                break;
                            case "PROCESSING_FILE":
                                worker.ReportProgress(completed, new ThreadCommand(UPDATE_ADDONUI_CURRENTTASK, param));
                                break;
                            case "ERROR":
                                Log.Error("Error IPC from MEM: " + param);
                                BACKGROUND_MEM_PROCESS_ERRORS.Add(param);
                                break;
                            case "ERROR_NO_BUILDABLE_FILES":
                                BACKGROUND_MEM_PROCESS_PARSED_ERRORS.Add(CURRENT_USER_BUILD_FILE + " has no files that can be used for building");
                                Log.Error("User buildable file has no files that can be converted to MEM format.");
                                break;
                            case "ERROR_FILE_NOT_COMPATIBLE":
                                Log.Error("MEM reporting file is not compatible: " + param);
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

        private void runMEM_DetectBadMods(string exe, string args, BackgroundWorker worker, List<string> acceptedIPC = null)
        {
            Debug.WriteLine("Running process: " + exe + " " + args);
            Log.Information("Running process: " + exe + " " + args);


            BACKGROUND_MEM_PROCESS = new ConsoleApp(exe, args);
            BACKGROUND_MEM_PROCESS_ERRORS = new List<string>();

            BACKGROUND_MEM_PROCESS.ConsoleOutput += (o, args2) =>
            {
                string str = args2.Line;
                if (str.StartsWith("[IPC]", StringComparison.Ordinal))
                {
                    string command = str.Substring(5);
                    int endOfCommand = command.IndexOf(' ');
                    command = command.Substring(0, endOfCommand);
                    if (acceptedIPC == null || acceptedIPC.Contains(command))
                    {
                        string param = str.Substring(endOfCommand + 5).Trim();
                        switch (command)
                        {
                            case "ERROR":
                                Log.Error("ERROR IPC received with param: " + param);
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


        private void RestoreGame(object sender, DoWorkEventArgs e)
        {
            string gamePath = Utilities.GetGamePath(BACKUP_THREAD_GAME, true);
            string backupPath = Utilities.GetGameBackupPath(BACKUP_THREAD_GAME);
            BackupWorker.ReportProgress(completed, new ThreadCommand(UPDATE_PROGRESSBAR_INDETERMINATE, true));
            BackupWorker.ReportProgress(completed, new ThreadCommand(UPDATE_CURRENT_STAGE_PROGRESS, 100));
            BackupWorker.ReportProgress(completed, new ThreadCommand(UPDATE_ADDONUI_CURRENTTASK, "Deleting existing game installation"));
            if (Directory.Exists(gamePath))
            {
                Log.Information("Deleting existing game directory: " + gamePath);
                try
                {
                    bool deletedDirectory = Utilities.DeleteFilesAndFoldersRecursively(gamePath);
                    if (deletedDirectory != true)
                    {
                        BackupWorker.ReportProgress(completed, new ThreadCommand(RESTORE_FAILED_COULD_NOT_DELETE_FOLDER));
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("Exception deleting game directory: " + gamePath + ": " + ex.Message);
                }
            }
            else
            {
                Log.Error("Game directory not found! Was it removed while the app was running?");
            }

            Log.Information("Reverting lod settings");
            string exe = BINARY_DIRECTORY + MEM_EXE_NAME;
            string args = "-remove-lods " + BACKUP_THREAD_GAME;
            Utilities.runProcess(exe, args);

            if (BACKUP_THREAD_GAME == 1)
            {
                string iniPath = IniSettingsHandler.GetConfigIniPath(1);
                if (File.Exists(iniPath))
                {
                    Log.Information("Reverting Indirect Sound ini fix for ME1");
                    IniFile engineConf = new IniFile(iniPath);
                    engineConf.DeleteKey("DeviceName", "ISACTAudio.ISACTAudioDevice");
                }
            }

            if (Utilities.IsDirectoryWritable(Directory.GetParent(gamePath).FullName))
            {
                Directory.CreateDirectory(gamePath);
            }
            else
            {
                //Must have admin rights.
                Log.Information("We need admin rights to create this directory");
                exe = BINARY_DIRECTORY + "PermissionsGranter.exe";
                args = "\"" + System.Security.Principal.WindowsIdentity.GetCurrent().Name + "\" -create-directory \"" + gamePath.TrimEnd('\\') + "\"";
                int result = Utilities.runProcessAsAdmin(exe, args);
                if (result == 0)
                {
                    Log.Information("Elevated process returned code 0, restore directory is hopefully writable now.");
                }
                else if (result == Utilities.WIN32_EXCEPTION_ELEVATED_CODE)
                {
                    Log.Information("Elevated process returned exception code, user probably declined prompt");

                    e.Result = false;
                    return;
                }
                else
                {
                    Log.Error("Elevated process returned code " + result + ", directory likely is not writable");
                    e.Result = false;
                    return;
                }
            }

            BackupWorker.ReportProgress(completed, new ThreadCommand(UPDATE_ADDONUI_CURRENTTASK, "Restoring game from backup "));
            if (gamePath != null)
            {
                Log.Information("Copying backup to game directory: " + backupPath + " -> " + gamePath);
                CopyDir.CopyAll_ProgressBar(new DirectoryInfo(backupPath), new DirectoryInfo(gamePath), BackupWorker, this, -1, 0);
                Progressbar_Max = 100;
                Log.Information("Restore of game data has completed");
            }
            if (BACKUP_THREAD_GAME == 3)
            {
                //Check for cmmvanilla file and remove it present

                string file = gamePath + "\\cmm_vanilla";
                if (File.Exists(file))
                {
                    Log.Information("Removing cmm_vanilla file");
                    File.Delete(file);
                }
            }
            e.Result = true;
        }
        private async void BuildWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (e.UserState is null)
            {
                ProgressBarValue = e.ProgressPercentage;
                TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.Normal);
                TaskbarManager.Instance.SetProgressValue(e.ProgressPercentage, 100);
            }
            else
            {
                ThreadCommand tc = (ThreadCommand)e.UserState;
                switch (tc.Command)
                {
                    case SET_VISIBILE_ITEMS_LIST:
                        List<AddonFile> afs = (List<AddonFile>)tc.Data;
                        ShowBuildingOnly = true;
                        ApplyFiltering();
                        break;
                    case UPDATE_ADDONUI_CURRENTTASK:
                        AddonFilesLabel.Text = (string)tc.Data;
                        break;
                    case UPDATE_HEADER_LABEL:
                        HeaderLabel.Text = (string)tc.Data;
                        break;
                    case UPDATE_PROGRESSBAR_INDETERMINATE:
                        if (!TaskbarProgressIndeterminateManaged)
                        {
                            TaskbarManager.Instance.SetProgressState((bool)tc.Data ? TaskbarProgressBarState.Indeterminate : TaskbarProgressBarState.Normal);
                        }
                        Build_ProgressBar.IsIndeterminate = (bool)tc.Data;
                        break;
                    case SET_TASKBAR_INDETERMINATE:
                        TaskbarManager.Instance.SetProgressState((bool)tc.Data ? TaskbarProgressBarState.Indeterminate : TaskbarProgressBarState.NoProgress);
                        break;
                    case ERROR_OCCURED:
                        Build_ProgressBar.IsIndeterminate = false;
                        ProgressBarValue = 0;
                        if (!ERROR_SHOWING)
                        {
                            ERROR_SHOWING = true;
                            await this.ShowMessageAsync("Error while building and staging textures", "An error occured building and staging files for installation. The logs will provide more information. The error message given is:\n" + (string)tc.Data);
                            ERROR_SHOWING = false;
                        }
                        break;
                    case SHOW_DIALOG:
                        KeyValuePair<string, string> messageStr = (KeyValuePair<string, string>)tc.Data;
                        await this.ShowMessageAsync(messageStr.Key, messageStr.Value);
                        break;
                    case INCREMENT_COMPLETION_EXTRACTION:
                        Interlocked.Increment(ref completed);
                        ProgressBarValue = (completed / (double)ADDONSTOBUILD_COUNT) * 100;
                        break;
                }
            }
        }

        private async Task<bool> InstallPrecheck(int game)
        {
            Log.Information("Running installation precheck for ME" + game);
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
                await this.ShowMessageAsync("Mass Effect" + GetGameNumberSuffix(game) + " has not been run yet", "Mass Effect" + GetGameNumberSuffix(game) + " must be run at least once in order for the game to generate default configuration files for this installer to edit. Start the game, and exit at the main menu to generate them.");
                return false;
            }

            ALOTVersionInfo installedInfo = Utilities.GetInstalledALOTInfo(game);
            //Check EXE version
            string exePath = Utilities.GetGameEXEPath(game);
            string gamePath = Utilities.GetGamePath(game);

            if (!Directory.Exists(gamePath))
            {
                Log.Error("Game directory is missing: " + gamePath);
                await this.ShowMessageAsync("Game directory is missing", "The game directory for Mass Effect" + GetGameNumberSuffix(game) + " is missing. This may be caused due to modification of the folder while ALOT Installer is running. Please reinstall the game.");
                return false;
            }

            if (!File.Exists(exePath))
            {
                Log.Error("Game EXE is missing.");
                await this.ShowMessageAsync("Game executable missing", "The game executable for Mass Effect" + GetGameNumberSuffix(game) + " is missing. Please reinstall the game.");
                return false;
            }
            else if (installedInfo == null)
            {
                var fvi = FileVersionInfo.GetVersionInfo(exePath);
                var exeVersion = new Version($"{fvi.FileMajorPart}.{fvi.FileMinorPart}.{fvi.FileBuildPart}.{fvi.FilePrivatePart}");
                Version requiredVersion = null;
                switch (game)
                {
                    case 1:
                        requiredVersion = new Version("1.2.20608.0");
                        break;
                    case 2:
                        requiredVersion = new Version("1.2.1604.0");
                        break;
                    case 3:
                        requiredVersion = new Version("1.5.5427.124");
                        break;
                }

                if (exeVersion < requiredVersion)
                {
                    Log.Error("Installation blocked: Game executable is not up to date for ME" + game + ": " + requiredVersion + " required, current version is " + exeVersion);
                    await this.ShowMessageAsync("Game must be updated", "Mass Effect" + GetGameNumberSuffix(game) + " is not up to date. ALOT Installer does not work work with old versions of Mass Effect games. You must update Mass Effect" + GetGameNumberSuffix(game) + " in order to install ALOT for it.");
                    return false;
                }
            }

            //Check for Texture2D.tfc
            if (installedInfo == null && (game == 2 || game == 3))
            {
                string checkPath = gamePath;
                if (game == 2) { checkPath = Path.Combine(gamePath, "BioGame", "CookedPC", "Texture2D.tfc"); }
                if (game == 3) { checkPath = Path.Combine(gamePath, "BIOGame", "CookedPCConsole", "Texture2D.tfc"); }
                if (File.Exists(checkPath))
                {
                    Log.Error("Previous installation file found: " + checkPath);
                    Log.Error("Game was not removed before reinstallation or user attempted to \"fix\" using a game repair. Game repair will not remove leftover files - delete your game installation to remove all files and reinstall");
                    string howToFixStr = "You must restore your game using the ALOT Installer restore feature, or delete your game installation(do not uninstall or repair) to fully remove leftover files.";
                    if (Utilities.GetGameBackupPath(game) == null)
                    {
                        howToFixStr = "You must delete your current game installation (do not uninstall or repair) to fully remove leftover files. You can use the ALOT Installer backup feature to backup a vanilla game once this is done.";
                    }

                    await this.ShowMessageAsync("Leftover files detected", "Files from a previous ALOT installation were detected and will cause installation to fail. " + howToFixStr);
                    return false;
                }
            }

            //Check DLC state
            if (game == 3 && installedInfo == null)
            {
                bool hasInconsistentDLC = false;
                string inconsistentDLC = "";
                var dlcPath = Path.Combine(gamePath, "BIOGame", "DLC");
                if (Directory.Exists(dlcPath))
                {
                    var directories = Directory.EnumerateDirectories(dlcPath);
                    Dictionary<int, string> priorities = new Dictionary<int, string>();
                    foreach (string dir in directories)
                    {
                        string value = Path.GetFileName(dir);
                        if (value == "__metadata")
                        {
                            continue;
                        }
                        long sfarsize = 0;
                        long unpackedSfarSize = 32L;

                        //Check for SFAR size not being 32 bytes
                        string sfar = Path.Combine(dir, "CookedPCConsole", "Default.sfar");
                        string unpackedDir = Path.Combine(dir, "CookedPCConsole");
                        if (File.Exists(sfar))
                        {
                            var unpackedFileExtensions = new List<string>() { ".pcc", ".tlk", ".bin", ".dlc" };
                            var filesInSfarDir = Directory.EnumerateFiles(unpackedDir).ToList();
                            var hasUnpackedFiles = filesInSfarDir.Any(d => unpackedFileExtensions.Contains(Path.GetExtension(d.ToLower())));

                            long sfarPackedSize = DiagnosticsWindow.GetPackedSFARSize(value);
                            long me3explorerUnpackedSize = DiagnosticsWindow.GetME3ExplorerUnpackedSFARSize(value);

                            if (sfarPackedSize != 0)
                            {
                                FileInfo fi = new FileInfo(sfar);
                                sfarsize = fi.Length;

                                if (sfarsize == sfarPackedSize && !hasUnpackedFiles)
                                {
                                    //OK
                                    Log.Information("DLC passed precheck (packed): " + value);
                                    continue;
                                }

                                if (sfarsize == sfarPackedSize && hasUnpackedFiles)
                                {
                                    //Inconsistent - has unpacked but is packed still
                                    hasInconsistentDLC = true;
                                    Log.Warning("DLC failed precheck, packed SFAR with unpacked files detected: " + value);
                                    inconsistentDLC += "\n - " + value + ": DLC packed, but detected unpacked files";
                                    continue;
                                }

                                if (sfarsize == me3explorerUnpackedSize)
                                {
                                    Log.Error("DLC precheck detects SFAR was unpacked with ME3Explorer mainline: " + value + ". ME3Explorer mainline should never be used to unpack DLC");
                                    continue;
                                }

                                if (sfarsize == unpackedSfarSize)
                                {
                                    Log.Information("DLC passed precheck (32 bytes unpacked sfar): " + value);
                                    continue;
                                }

                                if (sfarsize > sfarPackedSize && hasUnpackedFiles)
                                {
                                    //Inconsistent - has appears packed (with injected files) but is packed still
                                    hasInconsistentDLC = true;
                                    Log.Warning("DLC failed precheck, packed SFAR (with injected files from Mod Manager) with unpacked files detected: " + value);
                                    inconsistentDLC += "\n - " + value + ": DLC packed (with injected files), but detected unpacked files";
                                    continue;
                                }

                                if (sfarsize != sfarPackedSize)
                                {
                                    Log.Warning("DLC SFAR size is unknown for DLC: " + value + " " + sfarsize + " bytes");
                                    continue;
                                }
                            }
                        }
                    }
                }

                if (hasInconsistentDLC)
                {
                    await this.ShowMessageAsync("DLC is in inconsistent state", "The following DLC was detected as packed, however unpacked files were detected. These DLCs are in an inconsistent state. You should delete your game installation and reinstall the game to fix these inconsistencies. Origin game repair will not fix this issue.\n" + inconsistentDLC);
                    return false;
                }
            }

            List<AddonFile> missingRecommendedFiles = new List<AddonFile>();
            bool oneisready = false;
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
                    mds.FirstAuxiliaryButtonText = "Cancel";
                    mds.DefaultButtonFocus = MessageDialogResult.Affirmative;
                    MessageDialogResult result = await this.ShowMessageAsync("Mass Effect" + GetGameNumberSuffix(game) + " not backed up", "You should create a backup of your game before installing ALOT. In the event something goes wrong, you can quickly restore back to an unmodified state. Creating a backup is strongly recommended and should be done on an unmodified game. Create a backup before install?", MessageDialogStyle.AffirmativeAndNegativeAndSingleAuxiliary, mds);
                    if (result == MessageDialogResult.Affirmative)
                    {
                        BackupGame(game);
                        return false;
                    }
                    if (result == MessageDialogResult.FirstAuxiliary)
                    {
                        return false;
                    }
                }
            }
            bool blockDueToMissingALOTFile = installedInfo == null; //default value
            int installedALOTUpdateVersion = (installedInfo == null) ? 0 : installedInfo.ALOTUPDATEVER;
            bool blockDueToMissingALOTUpdateFile = false; //default value
            string blockDueToBadImportedFile = null; //default vaule
            bool manifestHasUpdateAvailable = false;
            AddonFile alotmainfile = null;
            foreach (AddonFile af in alladdonfiles)
            {
                if ((af.Game_ME1 && game == 1) || (af.Game_ME2 && game == 2) || (af.Game_ME3 && game == 3))
                {
                    if (af.Game_ME1 && MEUITM_INSTALLER_MODE && !af.MEUITM)
                    {
                        continue;
                    }
                    if (af.ALOTVersion > 0)
                    {
                        alotmainfile = af;
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
                            Log.Warning("Installation for ME" + game + " being blocked: ALOT/MEUITM is not installed currently, and ALOT's main file is not present or ready for use. ALOT must be installed if it's not already done so.");
                            break;
                        }
                    }

                    if (af.ALOTUpdateVersion > installedALOTUpdateVersion)
                    {
                        manifestHasUpdateAvailable = true;
                        if (!af.Ready)
                        {
                            blockDueToMissingALOTUpdateFile = true;
                            Log.Warning("Installation for ME" + game + " being blocked due to ALOT update available, but not ready for installation in the import library.");
                            break;
                        }
                    }

                    if (!af.Ready && !af.Optional)
                    {
                        //Check if MEUITM and if MEUITM is installed currently
                        if (installedInfo != null)
                        {
                            if (installedInfo.MEUITMVER > 0 && af.MEUITM)
                            {
                                continue; //this this file as meuitm is already installed
                            }
                        }
                        missingRecommendedFiles.Add(af);
                    }
                    else
                    {
                        if (af.Ready && af.Enabled && af.GetFile() != null && File.Exists(af.GetFile()))
                        {
                            if (af.GetFile() == null)
                            {
                                Debugger.Break();
                            }
                            FileInfo fi = new FileInfo(af.GetFile());
                            if (!af.IsCurrentlySingleFile() && af.FileSize > 0 && af.FileSize != fi.Length)
                            {
                                Log.Error(af.GetFile() + " has wrong size: " + fi.Length + ", manifest specifies " + af.FileSize);
                                blockDueToBadImportedFile = af.GetFile();
                                break;
                            }
                            if (af.IsCurrentlySingleFile() && af.UnpackedFileSize > 0 && af.UnpackedFileSize != fi.Length)
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

            if (blockDueToMissingALOTFile && alotmainfile != null && !MEUITM_INSTALLER_MODE)
            {
                int alotindex = ListView_Files.Items.IndexOf(alotmainfile);
                ListView_Files.SelectedIndex = alotindex;

                await this.ShowMessageAsync("ALOT main file is missing", "ALOT's main file for Mass Effect" + GetGameNumberSuffix(game) + " is not imported. This file must be imported to run the installer when ALOT is not installed.");
                return false;
            }

            if (blockDueToMissingALOTUpdateFile && manifestHasUpdateAvailable && !MEUITM_INSTALLER_MODE)
            {
                if (installedInfo == null)
                {
                    await this.ShowMessageAsync("ALOT update file is missing", "ALOT for Mass Effect" + GetGameNumberSuffix(game) + " has an update file, but it not currently imported. This update must be imported in order to install ALOT for the first time so you have the most up to date installation. Drag and drop the archive onto the interface - do not extract it.");
                }
                else
                {
                    await this.ShowMessageAsync("ALOT update file is missing", "ALOT for Mass Effect" + GetGameNumberSuffix(game) + " has an update available that is not yet applied. This update must be imported in order to continue. Drag and drop the archive onto the interface - do not extract it.");
                }
                return false;
            }

            if (blockDueToBadImportedFile != null)
            {
                await this.ShowMessageAsync("Corrupt/Bad file detected", "The file " + blockDueToBadImportedFile + " is not the correct size. This file may be corrupt or the wrong version, or was renamed in an attempt to make the program accept this file. Remove this file from Download_Mods, it is not usable.");
                return false;
            }

            if (missingRecommendedFiles.Count == 0)
            {
                TELEMETRY_ALL_ADDON_FILES = true;
                return true;
            }

            if (!oneisready)
            {
                await this.ShowMessageAsync("No files available for building", "There are no files available or relevant in the Downloaded_Mods library to install for Mass Effect" + GetGameNumberSuffix(game) + ".");
                return false;
            }
            //if alot is already installed we don't need to show missing message, unless installed via MEM directly
            if (installedInfo == null || installedInfo.ALOTVER == 0)
            {
                string missing = "";
                foreach (AddonFile af in missingRecommendedFiles)
                {
                    missing += " - " + af.FriendlyName + "\n";
                }
                Log.Information(missingRecommendedFiles.Count + " addon files are missing - prompting user to decline install.");
                MessageDialogResult result = await this.ShowMessageAsync(missingRecommendedFiles.Count + " file" + (missingRecommendedFiles.Count != 1 ? "s are" : " is") + " missing", "Some files for the Mass Effect" + GetGameNumberSuffix(game) + " addon are not imported:\n" + missing + "\nAddon files add a significant amount of high quality textures from third party artists (that cannot be included in the main ALOT file) and are tested to work with ALOT. These files must be imported if you want all of the high quality textures; these files are not included directly in ALOT because of ownership rights.\n\nNot importing these files will significantly degrade the ALOT experience. Are you sure you want to build the addon without these files?", MessageDialogStyle.AffirmativeAndNegative);
                if (result == MessageDialogResult.Affirmative)
                {
                    Log.Warning("User is continuing build step without all non-optional addon files. If user complains about a high amount of low quality textures this might be why.");
                    TELEMETRY_ALL_ADDON_FILES = false;
                    return true;
                }
                else
                {
                    Log.Information("User has aborted installation due to missing files");
                    return false;
                }
            }
            else
            {
                TELEMETRY_ALL_ADDON_FILES = true;
                return true;
            }
        }
    }
}
