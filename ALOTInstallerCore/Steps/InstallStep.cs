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
using Serilog;

namespace ALOTInstallerCore.Steps
{
    /// <summary>
    /// Object that handles the install step of texture installation
    /// </summary>
    public class InstallStep
    {
        private InstallOptionsPackage package;
        private NamedBackgroundWorker worker;

        public InstallStep(InstallOptionsPackage package, NamedBackgroundWorker worker)
        {
            this.worker = worker;
            this.package = package;
        }

        private void InstallTextures()
        {
            #region common callbacks and objects
            object lockObject = new object();
            void appStart(int processID)
            {
                // This might need to be waited on after method is called.
                Debug.WriteLine(@"Process launched. Process ID: " + processID);
            }
            void appExited(int code)
            {
                lock (lockObject)
                {
                    Monitor.Pulse(lockObject);
                }
            }
            #endregion

            #region Check for existing markers
            {
                string args = $"--check-for-markers --gameid {package.InstallTarget.Game.ToGameNum()} --ipc";
                if (package.DebugLogging)
                {
                    args += $" --debug-logs";
                }

                List<string> badFiles = new List<string>();

                void handleIPC(string command, string param)
                {
                    switch (command)
                    {
                        case "TASK_PROGRESS":
                            worker.ReportProgress(int.Parse(param), new ); // Pass IPC to handling app for UI update
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

                MEMIPCHandler.RunMEMIPC(args,
                    appStart,
                    handleIPC,
                    x => Log.Error($"StdError: {x}"),
                    appExited);
                lock (lockObject)
                {
                    Monitor.Wait(lockObject);
                }
            }
            #endregion
        }

        /// <summary>
        /// Performs the staging step.
        /// </summary>
        /// <returns></returns>
        public void PerformStaging(object sender, DoWorkEventArgs e)
        {
            var filesToStage = getFilesToStage(package.AllInstallerFiles.Where(x => x.Ready && (x.ApplicableGames & package.InstallTarget.Game.ToApplicableGame()) != 0));

            Log.Information(@"The following files will be staged for installation:");
            foreach (var f in filesToStage)
            {
                Log.Information(f.Filename);
            }

            foreach (var f in filesToStage)
            {
                var outputDir = Path.Combine(Settings.BuildLocation, Path.GetFileNameWithoutExtension(f.Filename));
                ExtractFile(f, outputDir);
            }
        }

        /// <summary>
        /// Gets list of files that will be staged for installation based on the given options the user has chosen.
        /// </summary>
        /// <param name="readyFiles"></param>
        /// <returns></returns>
        private List<InstallerFile> getFilesToStage(IEnumerable<InstallerFile> readyFiles)
        {
            var filesToStage = new List<InstallerFile>();
            if (package.InstallALOT)
            {
                filesToStage.AddRange(readyFiles.Where(x => x.AlotVersionInfo != null && x.AlotVersionInfo.ALOTVER > 0 && x.AlotVersionInfo.ALOTUPDATEVER == 0)); //Add MAJOR ALOT file
            }

            if (package.InstallALOTUpdate)
            {
                filesToStage.AddRange(readyFiles.Where(x => x.AlotVersionInfo != null && x.AlotVersionInfo.ALOTVER == 0 && x.AlotVersionInfo.ALOTUPDATEVER != 0)); //Add MINOR ALOT file
            }

            if (package.InstallMEUITM)
            {
                filesToStage.AddRange(readyFiles.Where(x => x.AlotVersionInfo != null && x.AlotVersionInfo.MEUITMVER != 0)); //Add MEUITM file
            }

            if (package.InstallALOTAddon)
            {
                filesToStage.AddRange(readyFiles.Where(x => x.AlotVersionInfo != null && x is ManifestFile)); //Add Addon files that don't have a set ALOTVersionInfo.
            }

            // Implement when user files class is ready.
            //if (package.InstallUserfiles)
            //{
            //    filesToStage.AddRange(readyFiles.Where(x => x.AlotVersionInfo != null && x is Use)); //Add Addon files that don't have a set ALOTVersionInfo.
            //}



            return filesToStage;
        }
    }
}
