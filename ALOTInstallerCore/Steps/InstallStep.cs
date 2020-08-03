using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
            // Todo: Can't do until MEM supports extraction of archives on windows
            #endregion

            #region Fix broken mods like Bonus Powers Pack

            #endregion

            #region Apply Post-mars post-hackett fix (ME3 only)

            #endregion

            #region Main installation phase
            {
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
                            pm.CompleteAndMoveToStage(param);
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
            }
            #endregion

        }

    }
}
