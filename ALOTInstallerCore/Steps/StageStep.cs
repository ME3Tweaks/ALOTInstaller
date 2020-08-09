using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.Objects;
using ALOTInstallerCore.Objects.Manifest;
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
        private InstallOptionsPackage installOptions;
        private int AddonID = -1; //ID of the Addon
        public StageStep(InstallOptionsPackage installOptions, NamedBackgroundWorker worker)
        {
            this.installOptions = installOptions;
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
        /// Callback to allow the UI to prompt the user to choose what mutual exclusive mod to install.
        /// This callback allows you to return null, which means abort staging.
        /// </summary>
        public Func<List<InstallerFile>, InstallerFile> ResolveMutualExclusiveMods { get; set; }

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
            var stagingDir = Path.Combine(Settings.BuildLocation, installOptions.InstallTarget.Game.ToString());
            if (Directory.Exists(stagingDir))
            {
                Utilities.DeleteFilesAndFoldersRecursively(stagingDir);
            }
            installOptions.FilesToInstall = getFilesToStage(installOptions.FilesToInstall.Where(x => x.Ready && (x.ApplicableGames & installOptions.InstallTarget.Game.ToApplicableGame()) != 0));
            installOptions.FilesToInstall = resolveMutualExclusiveGroups();
            if (installOptions.FilesToInstall == null)
            {
                // Abort!
                Log.Information("A mutual group conflict was not resolved. Staging aborted by user");
                return;
            }

            Log.Information(@"The following files will be staged for installation:");
            int buildID = 0;
            foreach (var f in installOptions.FilesToInstall)
            {
                f.ResetBuildVars();
                if (f.AlotVersionInfo.IsNotVersioned() && AddonID < 0)
                {
                    // First non-versioned file. Versioned files are always able to be overriden so
                    // first non versioned file will be first addon file (or user file).
                    AddonID = buildID++; //Addon will install at this ID
                }
                f.BuildID = buildID++;

                Log.Information($"{f.Filename}, Build ID {f.BuildID}");
            }

            int numDone = 0;
            int numToDo = installOptions.FilesToInstall.Count;
            // Final location where MEM will install packages from. 
            var finalBuiltPackagesDestination = Path.Combine(stagingDir, "InstallationPackages");
            if (Directory.Exists(finalBuiltPackagesDestination))
            {
                Utilities.DeleteFilesAndFoldersRecursively(finalBuiltPackagesDestination);
            }
            Directory.CreateDirectory(finalBuiltPackagesDestination);

            // Where the addon file's individual textures are staged to where they will be compiled into a .mem file in the final build packages folder.
            var addonStagingPath = Path.Combine(stagingDir, "AddonStaging");
            if (Directory.Exists(addonStagingPath))
            {
                Utilities.DeleteFilesAndFoldersRecursively(addonStagingPath);
            }
            Directory.CreateDirectory(addonStagingPath);


            foreach (var installerFile in installOptions.FilesToInstall)
            {
                var outputDir = Path.Combine(stagingDir, Path.GetFileNameWithoutExtension(installerFile.GetUsedFilepath()));
                Directory.CreateDirectory(outputDir);
                // Extract Archive
                var archiveExtracted = ExtractArchive(installerFile, outputDir);
                if (!archiveExtracted && FiletypeRequiresDecompilation(installerFile.GetUsedFilepath()))
                {
                    // Decompile file instead 
                    if (Path.GetExtension(installerFile.GetUsedFilepath()) == ".mod" && installerFile.StageModFiles)
                    {
                        var modDest = Path.Combine(stagingDir, Path.GetFileName(installerFile.GetUsedFilepath()));
                        Log.Information($"Copying .mod file to staging (due to StageModFiles=true): {installerFile.GetUsedFilepath()} -> {modDest}");
                        File.Copy(installerFile.GetUsedFilepath(), modDest);
                    }
                    else
                    {
                        ExtractTextureContainer(installOptions.InstallTarget.Game, installerFile.GetUsedFilepath(), outputDir, installerFile);
                    }
                }
                else if (installerFile.PackageFiles.Any(x => !x.MoveDirectly))
                {
                    // Files that are not move only may be contained in texture container format.
                    var subfilesToExtract = Directory.GetFiles(outputDir, "*.*", SearchOption.AllDirectories)
                        .Where(x => FiletypeRequiresDecompilation(x));
                    foreach (var sf in subfilesToExtract)
                    {
                        // todo: Prevent decompilation of package files marked as ProcessAsModFile as it's pointless
                        if (Path.GetExtension(sf) == ".mod" && installerFile.StageModFiles)
                        {
                            var modDest = Path.Combine(stagingDir, Path.GetFileName(sf));
                            Log.Information($"Moving .mod file to staging (due to StageModFiles=true): {sf} -> {modDest}");
                            File.Move(sf, modDest);
                        }
                        else
                        {
                            ExtractTextureContainer(installOptions.InstallTarget.Game, sf, outputDir, installerFile);
                        }
                    }
                }

                StageForBuilding(installerFile, outputDir, addonStagingPath, finalBuiltPackagesDestination, installOptions.InstallTarget.Game);
                Interlocked.Increment(ref numDone);
                UpdateProgressCallback?.Invoke(numDone, numToDo);
            }

            if (Directory.GetFiles(addonStagingPath).Any())
            {
                // Addon needs built
                BuildMEMPackageFile("ALOT Addon", addonStagingPath, Path.Combine(finalBuiltPackagesDestination, $"{AddonID:D3}_ALOTAddon.mem"), installOptions.InstallTarget.Game);
            }
        }

        private List<InstallerFile> resolveMutualExclusiveGroups()
        {
            var files = new List<InstallerFile>();
            Dictionary<string, List<InstallerFile>> mutualExclusiveMods = new Dictionary<string, List<InstallerFile>>();
            foreach (var v in installOptions.FilesToInstall)
            {
                if (v is PreinstallMod pm)
                {
                    if (pm.OptionGroup != null)
                    {
                        if (!mutualExclusiveMods.TryGetValue(pm.OptionGroup, out var _))
                        {
                            mutualExclusiveMods[pm.OptionGroup] = new List<InstallerFile>();
                        }
                        mutualExclusiveMods[pm.OptionGroup].Add(pm);
                    }
                }
                else
                {
                    files.Add(v);
                }
            }

            foreach (var pair in mutualExclusiveMods)
            {
                if (pair.Value.Count > 1)
                {
                    // Has issue
                    var chosenFile = ResolveMutualExclusiveMods?.Invoke(pair.Value);
                    if (chosenFile == null) return null;//abort
                    files.Add(chosenFile);
                }
            }
            //foreach (var groupsWithIssues in )

            return files;
        }

        /// <summary>
        /// Copies files from the source directory to the stagingDest according to the items listed in the installer file. THE SOURCE DIRECTORY WILL BE DELETED AFTER STAGING HAS COMPLETED!
        /// </summary>
        /// <param name="installerFile"></param>
        /// <param name="sourceDirectory">Where files are extracted to prior to being staged</param>
        /// <param name="compilingStagingDest">Where files are staged to to be compiled into a .mem file</param>
        /// <param name="finalDest">Where compiled (.mem) files for installation will be placed</param>
        private void StageForBuilding(InstallerFile installerFile, string sourceDirectory, string compilingStagingDest, string finalDest, MEGame targetGame)
        {
            if (installerFile is ManifestFile mf)
            {
                var filesInSource = Directory.GetFiles(sourceDirectory, "*.*", SearchOption.AllDirectories);
                int? numPackageFiles = mf.PackageFiles.Count + mf.ChoiceFiles?.Count + mf.ZipFiles?.Count + mf.CopyFiles?.Count;
                if (numPackageFiles > 0)
                {
                    int numPackageFilesStaged = 0;
                    foreach (var pf in mf.PackageFiles)
                    {
                        stagePackageFile(mf, pf, compilingStagingDest, finalDest, filesInSource, ref numPackageFilesStaged, numPackageFiles.Value);
                    }

                    foreach (var cf in mf.ChoiceFiles)
                    {
                        // Stage choice files
                        var chosenOption = cf.GetChosenFile();
                        if (chosenOption != null)
                        {
                            Log.Information($"Option chosen on {mf.FriendlyName}, using choicefile {cf.ChoiceTitle}: {cf.GetChosenFile()}");
                            stagePackageFile(mf, cf.GetChosenFile(), compilingStagingDest, finalDest, filesInSource, ref numPackageFilesStaged, numPackageFiles.Value);
                        }
                        else
                        {
                            Log.Information($"Not installing {cf.ChoiceTitle}");
                        }
                    }

                    //CopyFile and ZipFiles must be staged or they will simply be deleted
                    int stagedID = 1;
                    foreach (ZipFile zip in mf.ZipFiles)
                    {
                        if (zip.IsSelectedForInstallation())
                        {
                            string zipfile = Path.Combine(sourceDirectory, zip.InArchivePath);
                            string stagedPath = Path.Combine(finalDest, $"{mf.BuildID}_{stagedID}_{Path.GetFileName(zip.InArchivePath)}");
                            File.Move(zipfile, stagedPath);
                            zip.StagedPath = stagedPath;
                            zip.ID = stagedID;
                            stagedID++;
                        }
                    }

                    stagedID = 1;
                    foreach (CopyFile copy in mf.CopyFiles)
                    {
                        if (copy.IsSelectedForInstallation())
                        {
                            string singleFile = Path.Combine(sourceDirectory, copy.InArchivePath);
                            string stagedPath = Path.Combine(finalDest, $"{mf.BuildID}_{stagedID}_{Path.GetFileName(copy.InArchivePath)}");
                            File.Move(singleFile, stagedPath);
                            copy.StagedPath = stagedPath;
                            copy.ID = stagedID; //still useful?
                            stagedID++;
                        }
                    }

                    if (mf.PackageFiles.Any(x => !x.Processed && x.ApplicableGames.HasFlag(targetGame.ToApplicableGame())))
                    {
                        Log.Warning("Not all package files were marked as processed!");
                    }
                    installerFile.StatusText = "Cleaning temporary files";
                    Utilities.DeleteFilesAndFoldersRecursively(sourceDirectory);
                    installerFile.StatusText = "Staged for building";
                }
            }
        }

        /// <summary>
        /// Stages a package file for install. This method is broken out to support objects that support encapsulating package files as sub objects
        /// </summary>
        /// <param name="installerFile"></param>
        /// <param name="pf"></param>
        /// <param name="compilingStagingDest"></param>
        /// <param name="finalDest"></param>
        /// <param name="filesInSource"></param>
        /// <param name="numPackageFilesStaged"></param>
        /// <param name="numPackageFiles"></param>
        private void stagePackageFile(InstallerFile installerFile, PackageFile pf, string compilingStagingDest, string finalDest, string[] filesInSource, ref int numPackageFilesStaged, int numPackageFiles)
        {
            // Stage package files
            if (!pf.Processed && pf.ApplicableGames.HasFlag(installOptions.InstallTarget.Game.ToApplicableGame()))
            {
                var matchingFile = filesInSource.FirstOrDefault(x => Path.GetFileName(x).Equals(Path.GetFileName(pf.SourceName), StringComparison.InvariantCultureIgnoreCase));
                if (matchingFile != null)
                {
                    // found file to stage.
                    string extension = Path.GetExtension(matchingFile);
                    if (pf.MoveDirectly && extension == ".mem")
                    {
                        // Directly move .mem file to output
                        var destinationF = Path.Combine(finalDest, $"{installerFile.BuildID:D3}_{Path.GetFileName(pf.SourceName)}");
                        Log.Information($"Moving .mem file to builtdir: {pf.SourceName} -> {destinationF}");
                        if (File.Exists(destinationF)) File.Delete(destinationF);
                        File.Move(matchingFile, destinationF);
                        pf.Processed = true;
                        return;
                    }

                    if (pf.MoveDirectly)
                    {
                        // not mem file. Move to staging
                        var destinationF = Path.Combine(compilingStagingDest, pf.DestinationName ?? pf.SourceName);
                        Log.Information($"Moving package file to staging: {pf.SourceName} -> {pf.DestinationName ?? pf.SourceName}");
                        if (File.Exists(destinationF)) File.Delete(destinationF);
                        File.Move(matchingFile, destinationF);
                        pf.Processed = true;
                        return;
                    }

                    //if (pf.CopyDirectly)
                    //{
                    //    var destinationF = Path.Combine(stagingDest, pf.DestinationName);
                    //    File.Copy(matchingFile, destinationF, true);
                    //    pf.Processed = true;
                    //    continue;
                    //}

                    if (pf.DestinationName != null)
                    {
                        // Found file to stage
                        Log.Information($"Copying package file: {pf.SourceName} -> {pf.DestinationName}");
                        string destinationF = Path.Combine(compilingStagingDest, pf.DestinationName);
                        File.Copy(matchingFile, destinationF, true);
                        numPackageFilesStaged++;
                        installerFile.StatusText = $"Staging files {numPackageFilesStaged}/{numPackageFiles}";
                        pf.Processed = true;
                    }
                    else if (pf.DestinationName == null)
                    {
                        Log.Error(
                            $"Package file destinationname value is null. This is an error in the manifest file, please contact the developers. File: {installerFile.FriendlyName}, PackageFile: {pf.SourceName}");
                    }
                }
                else
                {
                    Log.Error("File specified by manifest doesn't exist after extraction: " +
                              pf.SourceName);
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
                    case "PROCESSING_FILE":
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
            if (installOptions.InstallALOT)
            {
                filesToStage.AddRange(readyFiles.Where(x => x.AlotVersionInfo != null && x.AlotVersionInfo.ALOTVER > 0 && x.AlotVersionInfo.ALOTUPDATEVER == 0)); //Add MAJOR ALOT file
            }

            if (installOptions.InstallALOTUpdate)
            {
                filesToStage.AddRange(readyFiles.Where(x => x.AlotVersionInfo != null && x.AlotVersionInfo.ALOTVER > 0 && x.AlotVersionInfo.ALOTUPDATEVER != 0)); //Add MINOR ALOT file
            }

            if (installOptions.InstallMEUITM)
            {
                filesToStage.AddRange(readyFiles.Where(x => x.AlotVersionInfo != null && x.AlotVersionInfo.MEUITMVER != 0)); //Add MEUITM file
            }

            if (installOptions.InstallALOTAddon)
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
