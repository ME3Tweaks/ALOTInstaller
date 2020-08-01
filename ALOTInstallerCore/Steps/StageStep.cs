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

namespace ALOTInstallerCore.Builder
{
    // Extraction + Staging <<
    // Building
    // Installing


    /// <summary>
    /// Object that handles the staging step of texture package building
    /// </summary>
    public class StageStep
    {
        private InstallOptionsPackage package;

        public StageStep(InstallOptionsPackage package, NamedBackgroundWorker worker)
        {
            this.package = package;
        }

        private void ExtractFile(InstallerFile instFile, string substagingDir)
        {
            string filepath = null;
            if (instFile is ManifestFile mf)
            {
                filepath = Path.Combine(Settings.TextureLibraryLocation, mf.Filename);
            }

            var extension = Path.GetExtension(filepath);
            if (extension == ".mem") return; //no need to process this file.
            if (extension == ".tpf") return; //This file will be broken down at the next step
            if (extension == ".dds") return; //no need to extract this file
            if (extension == ".png") return; //no need to extract this file

            Directory.CreateDirectory(substagingDir);
            object lockObject = new object();
            void appStart(int processID)
            {
                // This might need to be waited on after method is called.
                Debug.WriteLine(@"Process launched. Process ID: " + processID);
            }

            void handleIPC(string command, string param)
            {
                switch (command)
                {
                    case "TASK_PROGRESS":

                        break;
                    case "FILENAME":

                        break;
                    default:
                        Debug.WriteLine($"Unhandled IPC: {command} {param}");
                        break;
                }
            }

            void appExited(int code)
            {
                lock (lockObject)
                {
                    Monitor.Pulse(lockObject);
                }
            }

            switch (extension)
            {
                case ".7z":
                case ".rar":
                case ".zip":
                    // Extract archive
                    MEMIPCHandler.RunMEMIPC($"--unpack-archive --input \"{filepath}\" --output \"{substagingDir}\" --ipc",
                        appStart,
                        handleIPC,
                        x => Log.Error($"StdError on {filepath}: {x}"),
                        appExited);
                    lock (lockObject)
                    {
                        Monitor.Wait(lockObject);
                    }
                    break;
            }
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
