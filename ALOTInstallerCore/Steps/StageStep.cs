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
using static ALOTInstallerCore.Objects.Enums;

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
        public Action<int, int> UpdateProgressCallback { get; set; }

        /// <summary>
        /// Extracts an archive file (7z/zip/rar). Returns if a file was extracted or not.
        /// </summary>
        /// <param name="instFile"></param>
        /// <param name="substagingDir"></param>
        private bool ExtractArchive(InstallerFile instFile, string substagingDir)
        {
            instFile.IsProcessing = true;
            string filepath = instFile.GetUsedFilepath();

            var extension = Path.GetExtension(filepath);
            if (extension == ".mem") return false; //no need to process this file.
            if (extension == ".tpf") return false; //This file will be broken down at the next step
            if (extension == ".dds") return false; //no need to extract this file
            if (extension == ".png") return false; //no need to extract this file

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
                    return true;
                default:
                    Log.Error("Unsupported file extension: " + extension);
                    break;
            }
            return false;
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
            // Final location where MEM will install packages from. 
            var finalBuiltPackagesDestination = Path.Combine(Settings.BuildLocation, package.InstallTarget.Game.ToString(), "InstallationPackages");
            Directory.CreateDirectory(finalBuiltPackagesDestination);

            // Where the addon file's individual textures are staged to where they will be compiled into a .mem file in the final build packages folder.
            var addonStagingPath = Path.Combine(Settings.BuildLocation, package.InstallTarget.Game.ToString(), "AddonStaging");
            Directory.CreateDirectory(addonStagingPath);

            foreach (var f in package.FilesToInstall)
            {
                var outputDir = Path.Combine(Settings.BuildLocation, package.InstallTarget.Game.ToString(), Path.GetFileNameWithoutExtension(f.GetUsedFilepath()));
                // Extract Archive
                var archiveExtracted = ExtractArchive(f, outputDir);
                if (!archiveExtracted && FiletypeRequiresDecompilation(f.GetUsedFilepath()))
                {
                    // Decompile file instead 
                    ExtractTextureContainer(package.InstallTarget.Game, f.GetUsedFilepath(), outputDir, f);
                }

                StageForBuilding(f, outputDir, addonStagingPath, package.InstallTarget.Game);
                Interlocked.Increment(ref numDone);
                UpdateProgressCallback?.Invoke(numDone, numToDo);
            }
        }

        /// <summary>
        /// Copies files from the source directory to the stagingDest according to the items listed in the installer file. THE SOURCE DIRECTORY WILL BE DELETED AFTER STAGING HAS COMPLETED!
        /// </summary>
        /// <param name="installerFile"></param>
        /// <param name="sourceDirectory"></param>
        /// <param name="stagingDest"></param>
        private void StageForBuilding(InstallerFile installerFile, string sourceDirectory, string stagingDest, MEGame targetGame)
        {
            if (installerFile is ManifestFile mf)
            {
                var filesInSource = Directory.GetFiles(sourceDirectory, "*.*", SearchOption.AllDirectories);
                int numPackageFiles = mf.PackageFiles.Count;
                if (numPackageFiles > 0)
                {
                    int numPackageFilesStaged = 0;
                    foreach (var pf in mf.PackageFiles)
                    {
                        // Stage package files
                        if (!pf.Processed && pf.ApplicableGames.HasFlag(targetGame))
                        {
                            var matchingFile = filesInSource.FirstOrDefault(x => Path.GetFileName(x).Equals(pf.SourceName, StringComparison.InvariantCultureIgnoreCase));
                            if (matchingFile != null && pf.DestinationName != null)
                            {
                                // Found file to stage
                                Log.Information($"Copying package file: {pf.SourceName} -> {pf.DestinationName}");
                                string destinationF = Path.Combine(stagingDest, pf.DestinationName);
                                File.Copy(matchingFile, destinationF, true);
                                numPackageFilesStaged++;
                                installerFile.StatusText = $"Staging files {numPackageFilesStaged}/{numPackageFiles}";
                            }
                            else if (pf.DestinationName == null)
                            {
                                Log.Error($"Package file destinationname value is null. This is an error in the manifest file, please contact the developers. File: {installerFile.FriendlyName}, PackageFile: {pf.SourceName}");
                            }
                            else
                            {
                                Log.Error("File specified by manifest doesn't exist after extraction: " + pf.SourceName);
                            }
                        }
                    }

                    installerFile.StatusText = "Cleaning temporary files";
                    // todo: uncomment this
                    //Utilities.DeleteFilesAndFoldersRecursively(sourceDirectory);
                    installerFile.StatusText = "Staged for building";
                }
            }
        }

        /// <summary>
        /// Converts a folder of files into a MEM package with the specified filename.
        /// </summary>
        /// <param name="sourceDir"></param>
        /// <param name="outputFile"></param>
        /// <param name="targetGame"></param>
        private void BuildMEMPackageFile(string uiname, string sourceDir, string outputFile, MEGame targetGame)
        {
            void handleIPC(string command, string param)
            {
                switch (command)
                {
                    case "TASK_PROGRESS":
                        UpdateStatusCallback?.Invoke($"Building install package for {uiname}");
                        UpdateProgressCallback?.Invoke(TryConvert.ToInt32(param, 0), 100);
                        break;
                    case "FILENAME":
                        // Unpacking file
                        break;
                    default:
                        Debug.WriteLine($"Unhandled IPC: {command} {param}");
                        break;
                }
            }

            MEMIPCHandler.RunMEMIPCUntilExit($"--convert-to-mem --gameid {targetGame.ToGameNum()} --input \"{sourceDir}\" --output \"{outputFile}\" --ipc",
                null,
                handleIPC,
                x => Log.Error($"StdError building {uiname}: {x}"),
                null); //Change to catch exit code of non zero.
        }

        /// <summary>
        /// Returns true if the filetype specified requires decompilation to access it's texture contents
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        private bool FiletypeRequiresDecompilation(string filename)
        {
            string extension = Path.GetExtension(filename);
            if (extension == ".tpf") return true;
            if (extension == ".mod") return true;
            return false;
        }

        /// <summary>
        /// Extracts a file such as tpf, mod to the specified directory
        /// </summary>
        /// <param name="sourceFile"></param>
        /// <param name="outputDir"></param>
        private void ExtractTextureContainer(MEGame game, string sourceFile, string outputDir, InstallerFile file = null)
        {
            void handleIPC(string command, string param)
            {
                switch (command)
                {
                    case "TASK_PROGRESS":
                        if (file != null)
                        {
                            file.StatusText = $"Decompiling {Path.GetFileName(sourceFile)} {param}%";
                        }
                        break;
                    case "FILENAME":
                        // Unpacking file
                        break;
                    default:
                        Debug.WriteLine($"Unhandled IPC: {command} {param}");
                        break;
                }
            }

            string args = $" --gameid {game.ToGameNum()} --input \"{sourceFile}\" --output \"{outputDir}\" --ipc";
            string extension = Path.GetExtension(sourceFile);
            switch (extension)
            {
                case ".tpf":
                    args = $"--extract-tpf {args}";
                    break;
                case ".mod": //ProcessAsModFile = false on manifest
                    args = $"--extract-mod {args}";
                    break;
                default:
                    Log.Error("Unsupported file extension: " + extension);
                    break;
            }

            MEMIPCHandler.RunMEMIPCUntilExit(args,
                null,
                handleIPC,
                x => Log.Error($"StdError decompiling {sourceFile}: {x}"),
                null); //Change to catch exit code of non zero.
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



            return filesToStage.OrderBy(x => x.InstallPriority).ToList();
        }
    }
}
