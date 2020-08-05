using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.Objects;
using ALOTInstallerCore.Steps.Installer;
using Serilog;

namespace ALOTInstallerCore.Steps
{
    /// <summary>
    /// Object that handles the install step of texture installation.
    /// </summary>
    public class InstallStep
    {
        private InstallOptionsPackage package;
        private ProgressHandler pm { get; }
        public Action<string> SetTopTextCallback { get; set; }
        public Action<string> SetMiddleTextCallback { get; set; }
        public Action<string> SetBottomTextCallback { get; set; }
        public Action<bool> SetTopTextVisibilityCallback { get; set; }
        public Action<bool> SetMiddleTextVisibilityCallback { get; set; }
        public Action<bool> SetBottomTextVisibilityCallback { get; set; }

        /// <summary>
        /// Callback for setting the 'overall' progress value, from 0 to 100. Can be used to display things like progressbars.
        /// </summary>
        public Action<int> SetOverallProgressCallback { get; set; }

        public InstallStep(InstallOptionsPackage package)
        {
            pm = new ProgressHandler();
            this.package = package;
        }

        void updateStageOfStage()
        {
            SetMiddleTextCallback?.Invoke($"Stage {pm.CurrentStage?.StageUIIndex} of {pm.Stages.Count} ({pm.GetOverallProgress()}%)");
        }

        void updateCurrentStage()
        {
            SetBottomTextCallback?.Invoke($"{pm.CurrentStage?.TaskName} {pm.CurrentStage?.Progress}%");
        }

        public void InstallTextures(object sender, DoWorkEventArgs doWorkEventArgs)
        {
            #region setup top text

            string primary = "";
            if (package.InstallALOTUpdate)
            {
                // alot update
                var updateFile = package.FilesToInstall.FirstOrDefault(x => x.AlotVersionInfo.ALOTUPDATEVER != 0);
                if (updateFile != null)
                {
                    primary = $"ALOT {updateFile.AlotVersionInfo.ALOTVER}.{updateFile.AlotVersionInfo.ALOTUPDATEVER}";
                }
            }
            else if (package.InstallALOT)
            {
                // main alot only
                var mainfile = package.FilesToInstall.FirstOrDefault(x => x.AlotVersionInfo.ALOTVER != 0);
                if (mainfile != null)
                {
                    primary = $"ALOT {mainfile.AlotVersionInfo.ALOTVER}.0";
                }
            }

            if (package.InstallMEUITM)
            {
                var meuitmFile = package.FilesToInstall.FirstOrDefault(x => x.AlotVersionInfo.MEUITMVER != 0);
                if (meuitmFile != null)
                {
                    if (primary == "")
                    {
                        primary = $"MEUITM v{meuitmFile.AlotVersionInfo.MEUITMVER}";
                    }
                    else
                    {
                        primary += $" & MEUITM v{meuitmFile.AlotVersionInfo.MEUITMVER}";
                    }
                }
            }

            if (primary == "")
            {
                primary = "texture mods";
            }

            SetTopTextCallback?.Invoke($"Installing {primary} for {package.InstallTarget.Game.ToGameName()}");

            #endregion


            #region Check for existing markers

            {
                string args = $"--check-for-markers --gameid {package.InstallTarget.Game.ToGameNum()} --ipc";
                if (package.DebugLogging)
                {
                    args += " --debug-logs";
                }

                List<string> badFiles = new List<string>();

                void handleIPC(string command, string param)
                {
                    switch (command)
                    {
                        case "TASK_PROGRESS":
                            SetBottomTextCallback?.Invoke($"Checking game files for existing texture markers {param}%");
                            break;
                        case "FILENAME":

                            break;
                        case "ERROR_FILEMARKER_FOUND":
                            Log.Error("File was part of a different texture installation: " + param);
                            badFiles.Add(param);
                            break;
                        default:
                            Debug.WriteLine($"Unhandled IPC: {command} {param}");
                            break;
                    }
                }

                int currentMemProcessId = 0;
                int lastExitCode = int.MinValue;
                MEMIPCHandler.RunMEMIPCUntilExit(args,
                    x => currentMemProcessId = x,
                    handleIPC,
                    x => Log.Error($"StdError: {x}"),
                    x =>
                    {
                        currentMemProcessId = 0;
                        lastExitCode = x;
                    });

                if (badFiles.Any())
                {
                    // Must abort!
                    SetBottomTextCallback?.Invoke("Couldn't install textures");
                    SetTopTextCallback?.Invoke("Files from a previous texture installation were found");
                    //SetContinueButtonVisibility?.Invoke(true);
                    return; //Exit
                }
            }

            #endregion

            #region Preinstall ALOV mods
            applyModManagerMods();
            #endregion

            #region Fix broken mods like Bonus Powers Pack

            #endregion

            #region Apply Post-mars post-hackett fix (ME3 only)

            #endregion

            #region Main installation phase

            {
                bool doneReached = false;

                void handleIPC(string command, string param)
                {
                    switch (command)
                    {
                        case "STAGE_ADD": // Add a new stage 
                            {
                                Log.Information("Adding stage added to install stages queue: " + param);
                                pm.AddStage(param, package.InstallTarget.Game);
                                break;
                            }
                        case "STAGE_WEIGHT": //Reweight a stage based on how long we think it will take
                            string[] parameters = param.Split(' ');
                            try
                            {
                                double scale = Utilities.GetDouble(parameters[1], 1);
                                Log.Information("Reweighting stage " + parameters[0] + " by " + parameters[1]);
                                pm.ScaleStageWeight(parameters[0], scale);
                            }
                            catch (Exception e)
                            {
                                Log.Information("STAGE_WEIGHT parameter invalid: " + e);
                            }

                            break;
                        case "STAGE_CONTEXT": //Change to new stage
                            doneReached = pm.CompleteAndMoveToStage(param);
                            updateStageOfStage();
                            updateCurrentStage();

                            break;
                        case "TASK_PROGRESS": //Report progress of a stage
                            pm.SubmitProgress(int.Parse(param));
                            updateCurrentStage();
                            updateStageOfStage();
                            break;
                        case "PROCESSING_FILE": //Report a file is being processed
                            Log.Information("Processing file " + param);
                            break;
                        default:
                            Debug.WriteLine($"Unhandled IPC: {command} {param}");
                            break;
                    }
                }

                int currentMemProcessId = 0;
                int lastExitCode = int.MinValue;
                string args = $"--install-mods --gameid {package.InstallTarget.Game.ToGameNum()} --input \"{Path.Combine(Settings.BuildLocation, package.InstallTarget.Game.ToString())}\" --alot-mode --ipc --verify";

                if (package.RepackGameFiles)
                {
                    args += " --repack-mode";
                }

                if (package.DebugLogging)
                {
                    args += " --debug-logs";
                }

                MEMIPCHandler.RunMEMIPCUntilExit(args,
                    x => currentMemProcessId = x,
                    handleIPC,
                    x => Log.Error($"StdError: {x}"),
                    x =>
                    {
                        currentMemProcessId = 0;
                        lastExitCode = x;
                    });
                if (lastExitCode != 0)
                {
                    Log.Error($@"MEM exited with non zero exit code: {lastExitCode}");
                    // Todo: Issue callbacks to handle this
                    return;
                }

                if (!doneReached)
                {
                    Log.Error(@"MEM exited without reaching STAGE_DONE!");
                    // Todo: Issue callbacks to handle this
                    return;
                }

                #endregion

                #region Post-main install modifications
                SetBottomTextCallback?.Invoke("Finishing installation");

                //installCopyFiles();
                //installZipFiles();

                applyLODs();
                if (package.InstallTarget.Game == Enums.MEGame.ME1)
                {
                    // Apply ME1 LAA
                    applyME1LAA();
                }

                stampVersionInformation();
                //applyBinkw32();
                if (package.InstallTarget.Game == Enums.MEGame.ME3)
                {
                    // Install ASIs.
                }

                #endregion

                // Todo: Handle moving back unpacked files.
            }

        }

        private void stampVersionInformation()
        {
            SetBottomTextCallback?.Invoke("Updating texture installation marker");
            TextureModInstallationInfo tmii = TextureModInstallationInfo.CalculateMarker(package.InstallTarget.GetInstalledALOTInfo(), package.FilesToInstall);
            tmii.ALOT_INSTALLER_VERSION_USED = Assembly.GetEntryAssembly().GetName().Version.Build;
            int version = 0;
            // If the current version doesn't support the --version --ipc, we just assume it is 0.
            MEMIPCHandler.RunMEMIPCUntilExit("--version --ipc", ipcCallback: (command, param) =>
            {
                if (command == "VERSION")
                {
                    tmii.MEM_VERSION_USED = int.Parse(param);
                }
            });
            package.InstallTarget.StampTextureModificationInfo(tmii);
        }

        private void applyLODs()
        {
            SetBottomTextCallback?.Invoke("Applying graphics settings");
            Log.Information("Updating LOD information");
            string args = $"--apply-lods-gfx --gameid {package.InstallTarget.Game.ToGameNum()}";

            // Todo: MEUITM soft shadows/meuitm mode
            if (package.Limit2K)
            {
                Log.Information(" > Settings 2K lods");
                args += " --limit-2k";
            }


            // We don't care about IPC on this
            MEMIPCHandler.RunMEMIPCUntilExit(args,
                null,
                null,
                x => Log.Error($"StdError setting LODs: {x}"),
                null); //Change to catch exit code of non zero.        
        }

        private void applyME1LAA()
        {
            SetBottomTextCallback?.Invoke("Applying LAA");
            Log.Information("Applying LAA/Admin to game executable");
            string args = "--apply-me1-laa";

            // We don't care about IPC on this
            MEMIPCHandler.RunMEMIPCUntilExit(args,
                null,
                null,
                x => Log.Error($"StdError setting LAA: {x}"),
                null); //Change to catch exit code of non zero.        
        }

        private void applyModManagerMods()
        {
            //Apply ALOT-verified Mod Manager mods that we support.
            SetMiddleTextVisibilityCallback?.Invoke(false);

            foreach (var modAddon in package.FilesToInstall.OfType<PreinstallMod>())
            {
                SetMiddleTextCallback?.Invoke($"Installing {modAddon.FriendlyName}");

                void progressUpdate(int percent)
                {
                    SetBottomTextCallback?.Invoke($"Extracting {percent}%");
                }

                // We will stage the archive contents into game dir to start with, so moving files should be fast.
                var stagingPath = Directory.CreateDirectory(Path.Combine(package.InstallTarget.TargetPath, "ModExtractStaging")).FullName;

                void handleIPC(string command, string param)
                {
                    switch (command)
                    {
                        case "TASK_PROGRESS":
                            SetBottomTextCallback?.Invoke($"Extracting files {param}%");
                            break;
                        case "FILENAME":
                            Log.Information($"Extracting file from archive: {param}");
                            break;
                        default:
                            Debug.WriteLine($"Unhandled IPC: {command} {param}");
                            break;
                    }
                }

                MEMIPCHandler.RunMEMIPCUntilExit($"--unpack-archive --input \"{modAddon.GetUsedFilepath()}\" --output \"{stagingPath}\" --ipc",
                    null,
                    handleIPC,
                    x => Log.Error($"StdError on {modAddon.FriendlyName}: {x}"),
                    null); //Change to catch exit code of non zero.

                SetBottomTextCallback?.Invoke("Installing files");

                //Check requirements for this extraction rule to fire.
                var dlcDirectory = package.InstallTarget.Game == Enums.MEGame.ME1
                    ? Path.Combine(package.InstallTarget.TargetPath, "DLC")
                    : Path.Combine(package.InstallTarget.TargetPath, "BioGame", "DLC");

                foreach (var extractionRedirect in modAddon.ExtractionRedirects)
                {
                    //dlc is required (all in list)
                    if (extractionRedirect.OptionalRequiredDLC != null)
                    {
                        List<string> requiredDlc = extractionRedirect.OptionalRequiredDLC.Split(';').ToList();
                        List<string> requiredFiles = new List<string>();
                        List<long> requiredFilesSizes = new List<long>();


                        if (extractionRedirect.OptionalRequiredFiles != null)
                        {
                            requiredFiles = extractionRedirect.OptionalRequiredFiles.Split(';').ToList();
                            if (extractionRedirect.OptionalRequiredFilesSizes != null)
                            {
                                //parse required sizes list. This can be null which means we don't check the list of file sizes in the list
                                requiredFilesSizes = extractionRedirect.OptionalRequiredFilesSizes.Split(';').Select(x => long.Parse(x)).ToList();
                            }
                        }

                        //check if any required dlc is missing
                        if (requiredDlc.Any(x => !Directory.Exists(Path.Combine(dlcDirectory, x))))
                        {
                            Log.Information(extractionRedirect.LoggingName + ": Extraction rule is not applicable to this setup. Rule requires all of the DLC: " + extractionRedirect.OptionalRequiredDLC);
                            continue;
                        }

                        //Check if any required file is missing
                        if (requiredFiles.Any(x => !File.Exists(Path.Combine(dlcDirectory, x))))
                        {
                            Log.Information(extractionRedirect.LoggingName + ": Extraction rule is not applicable to this setup. At least one file for this rule was not found: " + extractionRedirect.OptionalRequiredFiles);
                            continue;
                        }

                        //Check if any required file size is wrong
                        if (requiredFilesSizes.Any())
                        {
                            bool doNotInstall = false;
                            for (int i = 0; i < requiredFilesSizes.Count; i++)
                            {
                                string file = requiredFiles[i];
                                long size = requiredFilesSizes[i];
                                string finalPath = Path.Combine(dlcDirectory, file);
                                var info = new FileInfo(finalPath);
                                if (info.Length != size)
                                {
                                    Log.Information(extractionRedirect.LoggingName +
                                                    ": Extraction rule is not applicable to this setup, file size for file " + file + " does not match rule.");
                                    doNotInstall = true;
                                    break;
                                }
                            }

                            if (doNotInstall)
                            {
                                continue;
                            }
                        }
                    }

                    //dlc required (any in list)
                    if (extractionRedirect.OptionalAnyDLC != null)
                    {
                        List<string> anyDLC = extractionRedirect.OptionalAnyDLC.Split(';').ToList();
                        //check if all dlc is missing
                        if (anyDLC.All(x => !Directory.Exists(Path.Combine(dlcDirectory, x))))
                        {
                            Log.Information(extractionRedirect.LoggingName + ": Extraction rule is not applicable to this setup. Rule requires at least one of the DLC: " + extractionRedirect.OptionalRequiredDLC);
                            continue;
                        }
                    }

                    Log.Information("Applying extraction rule: " + extractionRedirect.LoggingName);
                    var rootPath = Path.Combine(stagingPath, extractionRedirect.ArchiveRootPath);
                    var filesToMove = Directory.GetFiles(rootPath, "*", SearchOption.AllDirectories);

                    var ingameDestination = Path.Combine(package.InstallTarget.TargetPath, extractionRedirect.RelativeDestinationDirectory);
                    if (extractionRedirect.IsDLC && Directory.Exists(ingameDestination))
                    {
                        //delete first
                        Utilities.DeleteFilesAndFoldersRecursively(ingameDestination);
                    }

                    Directory.CreateDirectory(ingameDestination);

                    foreach (var file in filesToMove)
                    {
                        string relativePath = file.Substring(rootPath.Length + 1);
                        var extension = Path.GetExtension(relativePath);
                        if (extension.Equals(".pcc", StringComparison.InvariantCultureIgnoreCase) ||
                            extension.Equals(".tfc", StringComparison.InvariantCultureIgnoreCase))
                        {
                            Log.Information("Skipping file that that cannot be installed after alot: " + relativePath);
                            continue;
                        }

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

                    if (extractionRedirect.IsDLC)
                    {
                        //Write a _metacmm.txt file
                        var metacmm = Path.Combine(ingameDestination, "_metacmm.txt");
                        string contents = $"{extractionRedirect.LoggingName}\n{extractionRedirect.ModVersion}\nALOT Installer {System.Reflection.Assembly.GetEntryAssembly().GetName().Version}\n{Guid.NewGuid().ToString()}";
                        File.WriteAllText(metacmm, contents);
                    }
                }

                Utilities.DeleteFilesAndFoldersRecursively(stagingPath);
            }
        }
    }
}