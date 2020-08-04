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
        /// <summary>
        /// Callback to update the 'overall' status text of this step
        /// </summary>
        public Action<string> UpdateStatusCallback { get; set; }
        /// <summary>
        /// Callback to update the 'overall' progress of this step
        /// </summary>
        public Action<int,int> UpdateProgressCallback { get; set; }

        private void ExtractFile(InstallerFile instFile, string substagingDir)
        {
            instFile.IsProcessing = true;
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

            void handleIPC(string command, string param)
            {
                switch (command)
                {
                    case "TASK_PROGRESS":
                        instFile.StatusText = $"Extracting archive {param}%";
                        break;
                    case "FILENAME":
                        // Unpacking file
                        break;
                    default:
                        Debug.WriteLine($"Unhandled IPC: {command} {param}");
                        break;
                }
            }

            switch (extension)
            {
                case ".7z":
                case ".rar":
                case ".zip":
                    // Extract archive
                    UpdateStatusCallback($"Extracting {instFile.FriendlyName}");
                    MEMIPCHandler.RunMEMIPCUntilExit($"--unpack-archive --input \"{filepath}\" --output \"{substagingDir}\" --ipc",
                        null,
                        handleIPC,
                        x => Log.Error($"StdError on {filepath}: {x}"),
                        null); //Change to catch exit code of non zero.
                    break;
            }
        }

        /// <summary>
        /// Performs the staging step.
        /// </summary>
        /// <returns></returns>
        public void PerformStaging(object sender, DoWorkEventArgs e)
        {
            package.FilesToInstall = getFilesToStage(package.FilesToInstall.Where(x => x.Ready && (x.ApplicableGames & package.InstallTarget.Game.ToApplicableGame()) != 0));

            Log.Information(@"The following files will be staged for installation:");
            foreach (var f in package.FilesToInstall)
            {
                Log.Information(f.Filename);
            }

            int numDone = 0;
            int numToDo = package.FilesToInstall.Count;
            foreach (var f in package.FilesToInstall)
            {
                var outputDir = Path.Combine(Settings.BuildLocation, Path.GetFileNameWithoutExtension(f.Filename));
                ExtractFile(f, outputDir);
                Interlocked.Increment(ref numDone);
                UpdateProgressCallback?.Invoke(numDone, numToDo);
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
                filesToStage.AddRange(readyFiles.Where(x => x.AlotVersionInfo != null && x.AlotVersionInfo.IsNotVersioned() && x is ManifestFile)); //Add Addon files that don't have a set ALOTVersionInfo.
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
