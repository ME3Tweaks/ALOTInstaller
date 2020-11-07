using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks.Dataflow;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.Helpers.AppSettings;
using ALOTInstallerCore.Objects;
using ALOTInstallerCore.Objects.Manifest;
using ME3ExplorerCore.Packages;
using Serilog;

namespace ALOTInstallerCore.Steps
{
    // Extraction + Staging <<
    // Building
    // Installing


    /// <summary>
    /// Object that handles the staging step of texture package building
    /// </summary>
    public class StageStep
    {
        private InstallOptionsPackage _installOptions;
        private int _addonID = -1; //ID of the Addon
        private bool _abortStaging;
        private int _numTasksCompleted;
        private int _numTotalTasks;

        public StageStep(InstallOptionsPackage installOptions, NamedBackgroundWorker worker)
        {
            this._installOptions = installOptions;
        }

        /// <summary>
        /// Callback that is invoked with a message about why staging failed
        /// </summary>
        public Action<string> ErrorStagingCallback { get; set; }

        /// <summary>
        /// Callback to update the 'overall' status text of this step
        /// </summary>
        public Action<string> UpdateOverallStatusCallback { get; set; }

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
        /// Callback to allow the UI to prompt the user to choose what options to use for a manifest file
        /// that uses ZipFiles, CopyFiles and ChoiceFiles. The caller should set the SelectedIndex value
        /// on the objects, as they will be reset to their defaults before being passed to the UI.
        /// Return false to abort staging.
        /// </summary>
        public Func<ManifestFile, List<ConfigurableMod>, bool> ConfigureModOptions { get; set; }
        /// <summary>
        /// Invoked when the list of files to stage has been calculated
        /// </summary>
        public Action<List<InstallerFile>> FinalizedFileSet { get; set; }
        /// <summary>
        /// Invoked when a new file is being processed. This can be used for things like ensuring something is in view in the UI.
        public Action<InstallerFile> NotifyFileBeingProcessed { get; set; }
        /// <summary>
        /// Callback that is invoked when the staging step has reached a point of no return. This occurs after the user has selected all options. This must be set as this
        /// function is not checked for null before use
        /// </summary>
        public Func<bool> PointOfNoReturnNotification { get; set; }
        /// <summary>
        /// Callback for when addon file is being built. This occurs after staging has completed
        /// </summary>
        public Action NotifyAddonBuild { get; set; }
        /// <summary>
        /// Extracts an archive file (7z/zip/rar). Returns if a file was extracted or not.
        /// </summary>
        /// <param name="instFile"></param>
        /// <param name="substagingDir"></param>
        private bool? ExtractArchive(InstallerFile instFile, string substagingDir, ApplicableGame targetGame)
        {
            string filepath = instFile.GetUsedFilepath();

            var extension = Path.GetExtension(filepath);
            if (extension == ".mem") return false; //no need to process this file.
            if (extension == ".tpf") return false; //This file will be broken down at the next step
            if (extension == ".dds") return false; //no need to extract this file
            if (extension == ".png") return false; //no need to extract this file
            if (extension == ".tga") return false; //no need to extract this file
            if (extension == ".bik") return false; //no need to extract this file
            if (extension == ".mod") return false; //no need to extract this file

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
                    int exitcode = -1;
                    //UpdateStatusCallback($"Extracting {instFile.FriendlyName}");
                    instFile.StatusText = "Extracting archive";

                    var args = $"--unpack-archive --input \"{filepath}\" --output \"{substagingDir}\"";
                    // Determine extensions
                    if (instFile.HasAnyPackageFiles())
                    {
                        var extensions = instFile.PackageFiles.Select(x => Path.GetExtension(x.SourceName)).ToList();
                        if (instFile is ManifestFile mFile)
                        {
                            // Add copy files to calculation
                            extensions.AddRange(mFile.CopyFiles.Where(x => x.IsSelectedForInstallation()).Select(x => Path.GetExtension(x.SourceName)));

                            // Add choice files to calculation
                            extensions.AddRange(mFile.ChoiceFiles.Where(x => x.IsSelectedForInstallation()).Select(x => Path.GetExtension(x.GetChosenFile().SourceName)));

                            // Add zip files to calculation
                            extensions.AddRange(mFile.ZipFiles.Where(x => x.IsSelectedForInstallation()).Select(x => Path.GetExtension(x.SourceName)));

                            if (mFile.UnpackedSingleFilename != null)
                            {
                                // Must add this or we might filter out on extraction.
                                extensions.Add(Path.GetExtension(mFile.UnpackedSingleFilename));
                            }
                        }
                        extensions = extensions.Distinct().ToList();
                        // If any package files list TPFSource disable this space optimization
                        if (extensions.Count == 1 && instFile.PackageFiles.Where(x => x.ApplicableGames.HasFlag(targetGame)).All(x => x.TPFSource == null))
                        {
                            // We have only one extension type! We can filter what we extract with MEM
                            args += $" --filter-with-ext {extensions.First().Substring(1)}"; //remove the '.'
                        }
                    }

                    args += " --ipc";
                    MEMIPCHandler.RunMEMIPCUntilExit(args,
                        null,
                        handleIPC,
                        x => Log.Error($"[AICORE] StdError on {filepath}: {x}"),
                        x => exitcode = x); //Change to catch exit code of non zero.
                    if (exitcode == 0) return true;
                    return null;
                default:
                    Log.Error("[AICORE] Unsupported file extension: " + extension);
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
            var stagingDir = Path.Combine(Settings.StagingLocation, _installOptions.InstallTarget.Game.ToString());
            if (Directory.Exists(stagingDir))
            {
                Utilities.DeleteFilesAndFoldersRecursively(stagingDir);
            }
            _installOptions.FilesToInstall = getFilesToStage(_installOptions.FilesToInstall.Where(x => x.Ready && !x.Disabled && (x.ApplicableGames & _installOptions.InstallTarget.Game.ToApplicableGame()) != ApplicableGame.None));
            _installOptions.FilesToInstall = resolveMutualExclusiveGroups();
            if (_installOptions.FilesToInstall == null)
            {
                e.Result = false;
                return;
            }

            if (!promptModConfiguration())
            {
                e.Result = false;
                return; //abort.
            }

            if (_installOptions.FilesToInstall == null)
            {
                // Abort!
                Log.Warning("[AICORE] A mutual group conflict was not resolved. Staging aborted by user");
                e.Result = false;
                return;
            }

            if (!_installOptions.FilesToInstall.Any())
            {
                // Abort!
                Log.Error("[AICORE] There are no files to install! Is this a bug?");
                e.Result = false;
                CoreCrashes.TrackError?.Invoke(new Exception("There were no install files to process in the stage step"));
                return;
            }

            FinalizedFileSet?.Invoke(_installOptions.FilesToInstall);
            // Show point of no return prompt if textures are not installed
            if (_installOptions.InstallTarget.GetInstalledALOTInfo() == null && _installOptions.FilesToInstall.Any(x => !(x is PreinstallMod)) && !PointOfNoReturnNotification())
            {
                Log.Information("[AICORE] User aborted install at point of no return callback");
                e.Result = false;
                return;
            }

            Log.Information(@"[AICORE] The following files will be staged for installation (and installed) in the following order:");

            // ORDER THE INSTALL ITEMS HERE AS THIS WILL DETERMINE THE FINAL INSTALLATION ORDER
            sortInstallerFileSet(_installOptions, ref _addonID);

            _numTotalTasks = _installOptions.FilesToInstall.Count;
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


            var block = new ActionBlock<InstallerFile>(
                job => PrepareSingleFile(job, stagingDir, addonStagingPath, finalBuiltPackagesDestination, _installOptions.InstallTarget.Game.ToApplicableGame()),
                new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 2 }); // How to maximize this?

            foreach (var v in _installOptions.FilesToInstall)
            {
#if DEBUG
                //if (v.FriendlyName.Contains("Jacob"))
                // {
                block.Post(v);
                //}
#else 
                // Helps make sure I don't publish broken code
                block.Post(v);
#endif
            }
            block.Complete();
            block.Completion.Wait();

            if (_abortStaging)
            {
                if (!QuickFixHelper.IsQuickFixEnabled(QuickFixHelper.QuickFixName.nocleanstaging))
                {
                    Utilities.DeleteFilesAndFoldersRecursively(addonStagingPath);
                }

                // Error callback goes here
                ErrorStagingCallback?.Invoke(@"There was an error staging files for installation. You can review the application log for more information about why staging failed.");
                e.Result = false;
                return;
            }

            if (Directory.GetFiles(addonStagingPath).Any())
            {
                NotifyAddonBuild?.Invoke();
                // Addon needs built
                var resultcode = BuildMEMPackageFile("ALOT Addon", addonStagingPath,
                    Path.Combine(finalBuiltPackagesDestination, $"{_addonID:D3}_ALOTAddon.mem"), _installOptions.InstallTarget.Game,
                    out var buildFailedReason,
                    UpdateProgressCallback);
                if (resultcode != 0 || buildFailedReason != null)
                {
                    Log.Error($@"[AICORE] The ALOT Addon package failed to build");
                    _abortStaging = true;
                    ErrorStagingCallback?.Invoke(@"The ALOT Addon package failed to build. You can review the application log for more information about why it failed to build.");
                }
            }


            if (!QuickFixHelper.IsQuickFixEnabled(QuickFixHelper.QuickFixName.nocleanstaging))
            {
                Utilities.DeleteFilesAndFoldersRecursively(addonStagingPath);
            }

            if (_abortStaging)
            {
                // Error callback goes here
                e.Result = false;
                return;
            }
            e.Result = true;
        }

        /// <summary>
        /// Sorts the installation file set.
        /// </summary>
        /// <param name="installOptions">install options package</param>
        private void sortInstallerFileSet(InstallOptionsPackage installOptions, ref int _addonID)
        {
            int buildID = 0;
            List<InstallerFile> sortedSet = new List<InstallerFile>();
            sortedSet.AddRange(installOptions.FilesToInstall.OfType<ManifestFile>().OrderBy(x => x.InstallPriority));
            foreach (var f in sortedSet)
            {
                bool incremented = false;
                f.ResetBuildVars();
                if (f.AlotVersionInfo.IsNotVersioned)
                {
                    // First non-versioned file. Versioned files are always able to be overriden so
                    // first non versioned file will be first addon file (or user file).
                    _addonID = ++buildID; //Addon will install at this ID
                    incremented = true;
                }

                if (!incremented)
                {
                    buildID++;
                }
                f.BuildID = buildID;
                f.StatusText = "Pending staging";
                f.IsWaiting = true;
                Log.Information($"[AICORE]    {f.GetType().Name} {f.Filename}, Build ID {f.BuildID}");
            }

            _addonID++; //Add one in case the final file was versioned
            buildID++; // Just make sure we don't override this ID
            Log.Information($"[AICORE]    The Addon package will stage to build ID {_addonID}, if it needs to be built");

            foreach (var f in installOptions.FilesToInstall.OfType<UserFile>())
            {
                f.ResetBuildVars();
                f.BuildID = buildID++;
                f.StatusText = "Pending staging";
                f.IsWaiting = true;
                Log.Information($"[AICORE]    {f.GetType().Name} {f.Filename}, Build ID {f.BuildID}");
                sortedSet.Add(f);
            }
            installOptions.FilesToInstall.ReplaceAll(sortedSet);
        }

        private bool? PrepareSingleFile(InstallerFile installerFile, string stagingDir, string addonStagingPath, string finalBuiltPackagesDestination, ApplicableGame targetGame)
        {
            if (_abortStaging) return null;
            var prefix = installerFile.FriendlyName;
            Log.Information($"[AICORE] [{prefix}] Processing staging for {installerFile.FriendlyName}");
            NotifyFileBeingProcessed?.Invoke(installerFile);
            installerFile.IsProcessing = true;
            installerFile.IsWaiting = false;
            bool stage = true; // If file doesn't need processing this is not necessary
            bool error = false; //If there is error. Used to not set the status text
            if (installerFile is ManifestFile mf)
            {
                var outputDir = Path.Combine(stagingDir, Path.GetFileNameWithoutExtension(installerFile.GetUsedFilepath()));
                mf.StagedName = installerFile.GetUsedFilepath();
                // Extract Archive
                var archiveExtractedN = installerFile.PackageFiles.Any(x => x.ApplicableGames.HasFlag(targetGame)) ? ExtractArchive(installerFile, outputDir, targetGame) : false;
                if (archiveExtractedN == null)
                {
                    // There was an error
                    //UpdateStatusCallback?.Invoke($"Error extracting {installerFile.FriendlyName}, checking file");
                    installerFile.StatusText = "Error extracting, checking archive";
                    using var sourcefStream = File.OpenRead(installerFile.GetUsedFilepath());
                    long sizeToHash = sourcefStream.Length;
                    if (sizeToHash > 0)
                    {
                        var hash = HashAlgorithmExtensions.ComputeHashAsync(MD5.Create(), sourcefStream,
                            progress: x =>
                            {
                                //UpdateStatusCallback?.Invoke($"Error extracting {installerFile.FriendlyName}, checking file {(int) (x * 100f / sizeToHash)}%");
                                installerFile.StatusText = $"Error extracting, checking archive {(int)(x * 100f / sizeToHash)}%";
                            }).Result;
                        if (hash == mf.GetBackingHash())
                        {
                            ErrorStagingCallback?.Invoke($"Error extracting {installerFile.GetUsedFilepath()}, but file matches manifest - possible disk issues?");
                        }
                        else
                        {
                            ErrorStagingCallback?.Invoke($"File is corrupt: {installerFile.GetUsedFilepath()}, this file should be deleted and redownloaded.\nExpected hash:{mf.GetBackingHash()}\nHash of file: {hash}");
                        }
                    }
                    else
                    {
                        ErrorStagingCallback?.Invoke($"Unable to read {installerFile.GetUsedFilepath()}, size is 0 bytes");
                    }
                    _abortStaging = true;
                    throw new Exception($"{installerFile.FriendlyName} failed to extract");
                }

                var archiveExtracted = archiveExtractedN.Value;
                if (archiveExtracted && _installOptions.ImportNewlyUnpackedFiles
                                     && installerFile is ManifestFile _mf
                                     && _mf.UnpackedSingleFilename != null
                                     && Path.GetExtension(_mf.UnpackedSingleFilename) != ".mem")
                {
                    // mem files will be directly moved to install source. All other files will be staged for build so we need to 
                    // copy them back before we delete the extraction dir after we stage the files
                    TextureLibrary.AttemptImportUnpackedFiles(outputDir, new List<ManifestFile>(new[] { _mf }), true,
                        (filename, x, y) =>
                        {
                            //UpdateStatusCallback?.Invoke($"Optimizing {filename} for future installs {(int) (x * 100f / y)}%");
                            installerFile.StatusText = $"Optimizing {filename} for future installs {(int)(x * 100f / y)}%";
                        },
                        forceCopy: true
                    );
                }

                bool decompiled = false;

                // Check if listed file is a decompilable format and not archive format 
                if (!archiveExtracted && installerFile.PackageFiles.Any(x => !x.MoveDirectly && x.ApplicableGames.HasFlag(targetGame)) && FiletypeRequiresDecompilation(installerFile.GetUsedFilepath()))
                {
                    // Decompile file instead 
                    if (Path.GetExtension(installerFile.GetUsedFilepath()) == ".mod" && installerFile.StageModFiles)
                    {
                        var modDest = Path.Combine(stagingDir, Path.GetFileName(installerFile.GetUsedFilepath()));
                        Log.Information($"[AICORE] [{prefix}] Copying .mod file to staging (due to StageModFiles=true): {installerFile.GetUsedFilepath()} -> {modDest}");
                        File.Copy(installerFile.GetUsedFilepath(), modDest);
                    }
                    else
                    {
                        Directory.CreateDirectory(outputDir);
                        ExtractTextureContainer(_installOptions.InstallTarget.Game,
                            installerFile.GetUsedFilepath(),
                            outputDir, installerFile);

                        // Check if this file has CopyDirectly set
                        // If it does it will try to look in the extraction dir for it
                        // So we just copy it here and sets it processed so it doesn't try to stage
                        var copyDirectlyPackageFile = installerFile.PackageFiles.FirstOrDefault(x => x.SourceName == (mf.UnpackedSingleFilename ?? mf.Filename) && x.CopyDirectly && x.ApplicableGames.HasFlag(targetGame));
                        if (copyDirectlyPackageFile != null)
                        {
                            var modDest = Path.Combine(stagingDir, Path.GetFileName(installerFile.GetUsedFilepath()));
                            Log.Information($"[AICORE] [{prefix}] Copying an installer file to staging (due to a subitem having CopyFile=true): {installerFile.GetUsedFilepath()} -> {modDest}");
                            File.Copy(installerFile.GetUsedFilepath(), modDest);
                            copyDirectlyPackageFile.Processed = true;
                        }

                        decompiled = true;
                    }
                }
                else if (archiveExtracted)
                {
                    // This installer file was extracted from an archive, and there are files in it that are not marked as move directly
                    // Decompile all files not marked as MoveDirectly

                    //// See if any files need decompiled (TPF)
                    var subfilesToExtract = Directory.GetFiles(outputDir, "*.*", SearchOption.AllDirectories).ToList();
                    var tpfsToDecomp = installerFile.PackageFiles.Where(x => x.TPFSource != null && x.ApplicableGames.HasFlag(targetGame)).Select(x => x.TPFSource).Distinct().ToList();
                    if (!tpfsToDecomp.Any() && mf.UnpackedSingleFilename != null && installerFile.PackageFiles.All(x => !x.MoveDirectly && x.ApplicableGames.HasFlag(targetGame)) && Path.GetExtension(mf.UnpackedSingleFilename) == ".tpf")
                    {
                        // Our files will always be in this
                        Log.Information($@"[AICORE] [{prefix}] No listed TPFs to decomp but we have a single file unpacked TPF, decompiling it");
                        tpfsToDecomp.Add(mf.UnpackedSingleFilename);
                    }

                    foreach (var v in tpfsToDecomp)
                    {
                        var matchingTpfFile = subfilesToExtract.Find(x => Path.GetFileName(x) == v);
                        if (matchingTpfFile == null)
                        {
                            Log.Error($"[AICORE] [{prefix}] Could not find TPF source! Missing TPF: {v}");
                            CoreCrashes.TrackError?.Invoke(new Exception($"Could not find a TPF source for an item in {installerFile.FriendlyName}! Missing TPF: {v}"));
                            continue;
                        }

                        ExtractTextureContainer(_installOptions.InstallTarget.Game, matchingTpfFile, outputDir, installerFile);
                        decompiled = true;
                    }

                    if (tpfsToDecomp.Any())
                    {
                        // Recalculate
                        subfilesToExtract = Directory.GetFiles(outputDir, "*.*", SearchOption.AllDirectories).ToList();
                    }


                    long unpackedSize = mf.UnpackedFileSize;

                    foreach (var sf in subfilesToExtract)
                    {
                        if (Path.GetExtension(sf) == ".mod" && installerFile.StageModFiles)
                        {
                            var modDest = Path.Combine(addonStagingPath, Path.GetFileName(sf));
                            Log.Information($"[AICORE] [{prefix}] Moving .mod file to staging (due to StageModFiles=true): {sf} -> {modDest}");
                            File.Move(sf, modDest);
                            continue;
                        }


                        var matchingPackageFiles = installerFile.PackageFiles.Where(x => Path.GetFileName(x.SourceName) == Path.GetFileName(sf) && x.ApplicableGames.HasFlag(targetGame)).ToList();
                        foreach (var mpf in matchingPackageFiles)
                        {
                            if (mpf.MoveDirectly)
                            {
                                if (unpackedSize != 0)
                                {
                                    // Ensure file extracted correct size
                                    var len = new FileInfo(sf).Length;
                                    if (len != unpackedSize)
                                    {
                                        installerFile.StatusText = "Extraction produced incorrect file";
                                        Log.Error($"[AICORE] [{prefix}] ERROR ON ARCHIVE EXTRACTION FOR {installerFile.Filename}: EXTRACTED PACKAGE FILE IS WRONG SIZE (MOVEDIRECTLY) FOR FILE {mpf.SourceName}. Expected: {unpackedSize} ({FileSizeFormatter.FormatSize(unpackedSize)}), Found: {len} ({FileSizeFormatter.FormatSize(len)})");
                                        _abortStaging = true;
                                        return false;
                                    }
                                }

                                // This file will be handled by stagePackageFile(); Do not try to operate on it
                                // We could move it here but let's just keep code in one place
                            }

                            if (mpf.CopyDirectly)
                            {
                                if (unpackedSize != 0)
                                {
                                    // Ensure file extracted correct size
                                    var len = new FileInfo(sf).Length;
                                    if (len != unpackedSize)
                                    {
                                        installerFile.StatusText = "Extraction produced incorrect file";
                                        Log.Error($"[AICORE] [{prefix}] ERROR ON ARCHIVE EXTRACTION FOR {installerFile.Filename}: EXTRACTED PACKAGE FILE IS WRONG SIZE (COPYDIRECTLY) FOR FILE {mpf.SourceName}. Expected: {unpackedSize} ({FileSizeFormatter.FormatSize(unpackedSize)}), Found: {len} ({FileSizeFormatter.FormatSize(len)})");
                                        _abortStaging = true;
                                        return false;
                                    }
                                }

                                // This file will be handled by stagePackageFile(); Do not try to operate on it
                                // We could move it here but let's just keep code in one place
                            }

                            // Else: Not move directly
                            // Could be multi-copy package file, e.g. one to many from source file
                        }

                        if (!matchingPackageFiles.Any())
                        {
                            if (FiletypeRequiresDecompilation(sf) && !tpfsToDecomp.Contains(Path.GetFileName(sf)))
                            {
                                // we check if tpfstodecomp to prevent double decompile
                                ExtractTextureContainer(_installOptions.InstallTarget.Game, sf, outputDir, installerFile);
                                decompiled = true;
                            }
                            else
                            {
                                // File skipped
                                Log.Information($"[AICORE] [{prefix}] File skipped for processing: {sf}");
                            }
                        }
                    }
                }

                // Single file unpacked
                if (!archiveExtracted && !decompiled && installerFile is ManifestFile mfx && mfx.IsBackedByUnpacked())
                {
                    // File must just be moved directly it seems
                    var destF = Path.Combine(finalBuiltPackagesDestination, $"{installerFile.BuildID:D3}_{Path.GetFileName(installerFile.GetUsedFilepath())}");

                    if (new DriveInfo(installerFile.GetUsedFilepath()).RootDirectory.Name == new DriveInfo(finalBuiltPackagesDestination).RootDirectory.Name)
                    {
                        // Move
                        Log.Information($"[AICORE] [{prefix}] Moving unpacked file to install packages directory: {installerFile.GetUsedFilepath()} -> {destF}");
                        File.Move(installerFile.GetUsedFilepath(), destF);
                    }
                    else
                    {
                        //Copy
                        Log.Information($"[AICORE] [{prefix}] Copying unpacked file to install packages directory: {installerFile.GetUsedFilepath()} -> {destF}");
                        CopyTools.CopyFileWithProgress(installerFile.GetUsedFilepath(), destF,
                            (x, y) =>
                            {
                                installerFile.StatusText = $"Copying file to staging {(int)(x * 100f / y)}%";
                            },
                            exception => { _abortStaging = true; });
                    }

                    stage = false;
                }
                else if (archiveExtracted && installerFile.PackageFiles.Where(x => x.ApplicableGames.HasFlag(targetGame)).All(x => x.MoveDirectly))
                {
                    // Subfiles move to dest
                }
                else if (decompiled)
                {
                    // Files will be staged
                }
                else if (mf is PreinstallMod pm && !pm.PackageFiles.Any())
                {
                    // Nothing to stage. Will install before textures
                    stage = false;
                }
                else
                {
                    Log.Error($"[AICORE] [{prefix}] STAGING NOT HANDLED!");
                    CoreCrashes.TrackError?.Invoke(new Exception($"STAGING NOT HANDLED FOR {installerFile.FriendlyName}"));
                }

                if (stage)
                {
                    // Staging for addon
                    StageForBuilding(installerFile, outputDir, addonStagingPath, finalBuiltPackagesDestination, _installOptions.InstallTarget.Game);
                }
            }
            else if (installerFile is UserFile uf)
            {
                var userFileExtractionPath = Path.Combine(stagingDir, "USER_" + Path.GetFileNameWithoutExtension(installerFile.GetUsedFilepath()));
                var userFileBuildMemPath = Path.Combine(userFileExtractionPath, "BuildSource");
                Directory.CreateDirectory(userFileBuildMemPath);

                // Extract Archive
                var archiveExtractedN = ExtractArchive(installerFile, userFileExtractionPath, targetGame);
                if (archiveExtractedN == null)
                {
                    ErrorStagingCallback?.Invoke($"Unable to extract {installerFile.GetUsedFilepath()}");
                    _abortStaging = true;
                    throw new Exception($"{installerFile.FriendlyName} failed to extract");
                }


                var archiveExtracted = archiveExtractedN.Value;
                // File is a direct copy if it's not extracted by extract archive. Put it in staging.
                if (!archiveExtracted)
                {
                    if (Path.GetExtension(uf.GetUsedFilepath()) == ".mem")
                    {
                        var destF = Path.Combine(finalBuiltPackagesDestination, $"{uf.BuildID:D3}_USER_{Path.GetFileName(uf.GetUsedFilepath())}");
                        Log.Information($@"[AICORE] [{prefix}] Copying precompiled mem directly to the mem input path: {uf.GetUsedFilepath()} -> {destF}");
                        CopyTools.CopyFileWithProgress(installerFile.GetUsedFilepath(), destF,
                            (x, y) =>
                            {
                                installerFile.StatusText = $"Staging for install {(int)(x * 100f / y)}%";
                            },
                            x =>
                            {
                                // Do something here. Not sure what
                                Log.Error($@"[AICORE] [{prefix}] Error occurred performing staging: {x.Message}");
                                error = true;
                                installerFile.StatusText = $"Failed to stage file(s): {x.Message}";
                                _abortStaging = true;
                            }
                        );
                    }
                    else
                    {
                        // Must be built

                        // Copy to the staging dir
                        var destF = Path.Combine(userFileBuildMemPath, Path.GetFileName(uf.GetUsedFilepath()));
                        Log.Information($@"[AICORE] [{prefix}] Copying user file into mem build for user file -> {destF}");
                        CopyTools.CopyFileWithProgress(installerFile.GetUsedFilepath(), destF,
                            (x, y) =>
                            {
                                installerFile.StatusText = $"Staging for build {(int)(x * 100f / y)}%";
                            },
                            x =>
                            {
                                // Do something here. Not sure what
                                Log.Error($@"[AICORE] [{prefix}] Error occurred performing staging: {x.Message}");
                                error = true;
                                installerFile.StatusText = $"Failed to stage file(s): {x.Message}";
                                _abortStaging = true;
                            }
                        );


                        // don't add progress indicator here. We don't need more than the text
                        var resultcode = BuildMEMPackageFile(uf.FriendlyName, userFileBuildMemPath, Path.Combine(finalBuiltPackagesDestination, $"{uf.BuildID:D3}_USER_{uf.FriendlyName}.mem"),
                            _installOptions.InstallTarget.Game, out var buildFailedReason, installerFile: uf);
                        if (resultcode != 0 || buildFailedReason != null)
                        {
                            Log.Error($@"[AICORE] [{prefix}] User file failed to build");
                            error = true;
                            installerFile.Disabled = true;
                            installerFile.StatusText = buildFailedReason ?? $"Failed to build, exit code {resultcode}. File has been disabled";
                            _abortStaging = true;
                        }
                    }
                }
                else
                {
                    // Files are in archive. Find files to stage to mem
                    installerFile.StatusText = "Staging files for build";
                    var subfilesToStage = Directory.GetFiles(userFileExtractionPath, "*.*", SearchOption.AllDirectories);
                    int stagedID = 0;
                    foreach (var sf in subfilesToStage)
                    {
                        if (Path.GetExtension(sf) == ".mem")
                        {
                            // Can be staged directly
                            var destF = Path.Combine(finalBuiltPackagesDestination, $"{uf.BuildID:D3}_{stagedID}_USER_{Path.GetFileName(sf)}");
                            Log.Information($"[AICORE] [{prefix}] Moving prebuilt .mem archive subfile to installation packages folder: {sf} -> {destF}");
                            File.Move(sf, destF);
                            stagedID++;
                        }
                        else if (userSubfileShouldBeStaged(_installOptions.InstallTarget.Game, sf))
                        {
                            // Move to build
                            var modDest = Path.Combine(userFileBuildMemPath, Path.GetFileName(sf));
                            Log.Information($"[AICORE] [{prefix}] Moving archive subfile to user staging: {sf} -> {modDest}");
                            File.Move(sf, modDest);
                        }
                    }

                    if (Directory.GetFiles(userFileBuildMemPath).Any())
                    {
                        // Requires build
                        // don't add progress indicator here. We don't need more than the text
                        var resultcode = BuildMEMPackageFile(uf.FriendlyName, userFileBuildMemPath,
                            Path.Combine(finalBuiltPackagesDestination, $"{uf.BuildID:D3}_{stagedID}_USER_{uf.FriendlyName}.mem"),
                            _installOptions.InstallTarget.Game, out var buildFailedReason);
                        if (resultcode != 0 || buildFailedReason != null)
                        {
                            Log.Error($@"[AICORE] [{prefix}] User file failed to build");
                            error = true;
                            installerFile.Disabled = true;
                            installerFile.StatusText = buildFailedReason ?? $"Failed to build, exit code {resultcode}. File has been disabled";
                            _abortStaging = true;
                        }
                    }
                }

                if (!QuickFixHelper.IsQuickFixEnabled(QuickFixHelper.QuickFixName.nocleanstaging))
                {
                    Utilities.DeleteFilesAndFoldersRecursively(userFileBuildMemPath);
                    Utilities.DeleteFilesAndFoldersRecursively(userFileExtractionPath);
                }
            }

            Interlocked.Increment(ref _numTasksCompleted);
            UpdateProgressCallback?.Invoke(_numTasksCompleted, _numTotalTasks);
            installerFile.IsProcessing = false;
            if (installerFile is PreinstallMod)
            {
                installerFile.StatusText = installerFile.PackageFiles.Any(x => x.ApplicableGames.HasFlag(targetGame)) ? "Textures staged, mod component install during install step" : "Mod will install during install step";
            }
            else if (!error)
            {
                installerFile.StatusText = "Staged for installation";
            }
            Log.Information($@"[AICORE] [{prefix}] Staging completed");
            return true;
        }

        private bool promptModConfiguration()
        {
            foreach (var m in _installOptions.FilesToInstall.Where(x => x is ManifestFile mf && (mf.CopyFiles.Any() || mf.ChoiceFiles.Any() || mf.ZipFiles.Any())))
            {
                var mf = m as ManifestFile;
                mf.PackageFiles.RemoveAll(x => x.Transient);
                var configurableOptions = new List<ConfigurableMod>();
                configurableOptions.AddRange(mf.ChoiceFiles);
                configurableOptions.AddRange(mf.CopyFiles);
                configurableOptions.AddRange(mf.ZipFiles);
                foreach (var v in configurableOptions)
                {
                    v.AddNoInstallIfApplicable();
                    v.SelectedIndex = v.DefaultSelectedIndex; //Reset to default option
                }

                var result = ConfigureModOptions?.Invoke(mf, configurableOptions);
                if (!result.HasValue || !result.Value)
                {
                    return false;
                }

                foreach (var v in configurableOptions.OfType<ChoiceFile>())
                {
                    var chosenFile = v.GetChosenFile();
                    if (chosenFile != null)
                    {
                        mf.PackageFiles.Add(chosenFile);
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Method that determines if a filepath should be staged for user file build
        /// </summary>
        /// <param name="sf"></param>
        /// <returns></returns>
        private bool userSubfileShouldBeStaged(MEGame targetGame, string sf)
        {
            var extension = Path.GetExtension(sf.ToLower());
            var filename = Path.GetFileNameWithoutExtension(sf.ToLower());
            switch (extension)
            {
                case ".mod":
                    {
                        var modGame = ModFileFormats.GetGameForMod(sf);
                        var shouldStage = modGame.Usable && targetGame.ToApplicableGame().HasFlag(modGame.ApplicableGames);
                        if (!shouldStage)
                        {
                            if (!modGame.Usable)
                            {
                                Log.Warning($"Archive file {sf} cannot apply to {targetGame}: {modGame.Description}");
                            }
                            else //Not applicable
                            {
                                Log.Warning($"Archive file {sf} is not applicable to {targetGame}, applies to {modGame.ApplicableGames}");
                            }
                        }

                        return shouldStage;
                    }
                case ".tpf": return true;
                case ".png":
                case ".dds":
                case ".bmp":
                case ".tga":
                    string regex = "0x[0-9a-f]{8}"; //This matches even if user has more chars after the 8th hex so...
                    var isOK = Regex.IsMatch(filename, regex);
                    if (!isOK)
                    {
                        Log.Warning($"[AICORE] Rejecting {Path.GetFileName(sf)} from userfile build due to missing 0xhhhhhhhh texture CRC to replace");
                    }
                    return isOK;

                default: return false;
            }

        }

        private List<InstallerFile> resolveMutualExclusiveGroups()
        {
            var files = new List<InstallerFile>();
            Dictionary<string, List<InstallerFile>> mutualExclusiveMods = new Dictionary<string, List<InstallerFile>>();
            foreach (var v in _installOptions.FilesToInstall)
            {
                if (v is ManifestFile mf)
                {
                    if (mf.OptionGroup != null)
                    {
                        if (!mutualExclusiveMods.TryGetValue(mf.OptionGroup, out var _))
                        {
                            mutualExclusiveMods[mf.OptionGroup] = new List<InstallerFile>();
                        }
                        mutualExclusiveMods[mf.OptionGroup].Add(mf);
                    }
                    else
                    {
                        files.Add(v); //No mutual exclusivity
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
                    files.Insert(0, chosenFile);
                }
                else
                {
                    files.Insert(0, pair.Value[0]); //No mutual exclusivity
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
            var prefix = installerFile.FriendlyName;
            if (installerFile is ManifestFile mf)
            {
                var filesInSource = Directory.GetFiles(sourceDirectory, "*.*", SearchOption.AllDirectories);
                int? numPackageFiles = mf.PackageFiles.Count(x => x.ApplicableGames.HasFlag(targetGame.ToApplicableGame())) + mf.ChoiceFiles?.Count + mf.ZipFiles?.Count + mf.CopyFiles?.Count;
                if (numPackageFiles > 0)
                {
                    int numPackageFilesStaged = 0;
                    foreach (var pf in mf.PackageFiles.Where(x => x.ApplicableGames.HasFlag(targetGame.ToApplicableGame())))
                    {
                        stagePackageFile(mf, pf, compilingStagingDest, finalDest, filesInSource, ref numPackageFilesStaged, numPackageFiles.Value);
                    }

                    foreach (var cf in mf.ChoiceFiles)
                    {
                        // Stage choice files
                        var chosenOption = cf.GetChosenFile();
                        if (chosenOption != null)
                        {
                            Log.Information($"[AICORE] [{prefix}] Using choicefile option {cf.ChoiceTitle}: {cf.GetChosenFile()}");
                            stagePackageFile(mf, cf.GetChosenFile(), compilingStagingDest, finalDest, filesInSource, ref numPackageFilesStaged, numPackageFiles.Value);
                        }
                        else
                        {
                            Log.Information($"[AICORE] [{prefix}] Not installing {cf.ChoiceTitle}");
                        }
                    }

                    //CopyFile and ZipFiles must be staged or they will simply be deleted
                    int stagedID = 1;
                    foreach (ZipFile zip in mf.ZipFiles)
                    {
                        if (zip.IsSelectedForInstallation())
                        {
                            string zipfile = Path.Combine(sourceDirectory, zip.SourceName);
                            string stagedPath = Path.Combine(finalDest, $"{mf.BuildID:D3}_{stagedID}_{Path.GetFileName(zip.SourceName)}");
                            Log.Information($@"[AICORE] [{prefix}] Installing ZipFile item: {zipfile} -> {stagedPath}");
                            File.Move(zipfile, stagedPath);
                            zip.StagedPath = stagedPath;
                            stagedID++;
                        }
                    }

                    stagedID = 1;
                    foreach (CopyFile copy in mf.CopyFiles)
                    {
                        if (copy.IsSelectedForInstallation())
                        {
                            string singleFile = Path.Combine(sourceDirectory, copy.SourceName);
                            string stagedPath = Path.Combine(finalDest, $"{mf.BuildID:D3}_{stagedID}_{Path.GetFileName(copy.SourceName)}");
                            Log.Information($@"[AICORE] [{prefix}] Installing CopyFile item: {singleFile} -> {stagedPath}");

                            File.Move(singleFile, stagedPath);
                            copy.StagedPath = stagedPath;
                            //copy.ID = stagedID; //still useful?
                            stagedID++;
                        }
                    }

                    if (mf.PackageFiles.Where(x => x.ApplicableGames.HasFlag(targetGame.ToApplicableGame())).Any(x => !x.Processed))
                    {
                        Log.Warning($"[AICORE] [{prefix}] Not all package files were marked as processed!");
                    }
                }

                if (!QuickFixHelper.IsQuickFixEnabled(QuickFixHelper.QuickFixName.nocleanstaging))
                {
                    installerFile.StatusText = "Cleaning temporary files";
                    Log.Information($"[AICORE] [{prefix}] Cleaning up {sourceDirectory}");
                    Utilities.DeleteFilesAndFoldersRecursively(sourceDirectory);
                }

                installerFile.StatusText = "Staged for building";
                Log.Information($"[AICORE] [{prefix}] Staged for build complete");
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
            if (!pf.Processed && pf.ApplicableGames.HasFlag(_installOptions.InstallTarget.Game.ToApplicableGame()))
            {
                var prefix = installerFile.FriendlyName;
                var matchingFile = filesInSource.FirstOrDefault(x => Path.GetFileName(x).Equals(Path.GetFileName(pf.SourceName), StringComparison.InvariantCultureIgnoreCase));
                if (matchingFile != null)
                {
                    // found file to stage.
                    string extension = Path.GetExtension(matchingFile);
                    if (pf.MoveDirectly && extension == ".mem")
                    {
                        // Directly move .mem file to output
                        var destinationF = Path.Combine(finalDest, $"{installerFile.BuildID:D3}_{Path.GetFileName(pf.SourceName)}");
                        Log.Information($"[AICORE] [{prefix}] Moving .mem file to builtdir: {pf.SourceName} -> {destinationF}");
                        if (File.Exists(destinationF)) File.Delete(destinationF);
                        File.Move(matchingFile, destinationF);
                        pf.Processed = true;
                        return;
                    }

                    if (pf.MoveDirectly)
                    {
                        // not mem file. Move to staging
                        var destinationF = Path.Combine(compilingStagingDest, pf.DestinationName ?? pf.SourceName);
                        if (File.Exists(destinationF)) File.Delete(destinationF);
                        Log.Information($"[AICORE] [{prefix}] Moving file to staging via MoveDirectly flag on package file: {pf.SourceName} -> {destinationF}");
                        File.Move(matchingFile, destinationF);
                        pf.Processed = true;
                        return;
                    }

                    if (pf.CopyDirectly)
                    {
                        var destinationF = Path.Combine(compilingStagingDest, pf.DestinationName ?? Path.GetFileName(matchingFile));
                        Log.Information($"[AICORE] [{prefix}] Copying file to staging via CopyDirectly flag on package file: {pf.SourceName} -> {destinationF}");
                        File.Copy(matchingFile, destinationF, true);
                        pf.Processed = true;
                        return;
                    }

                    if (pf.DestinationName != null)
                    {
                        // Found file to stage
                        Log.Information($"[AICORE] [{prefix}] Copying package file: {pf.SourceName} -> {pf.DestinationName}");
                        string destinationF = Path.Combine(compilingStagingDest, pf.DestinationName);
                        File.Copy(matchingFile, destinationF, true);
                        numPackageFilesStaged++;
                        installerFile.StatusText = $"Staging files {numPackageFilesStaged}/{numPackageFiles}";
                        pf.Processed = true;
                    }
                    else /*if (pf.DestinationName == null)*/
                    {
                        Log.Error(
                            $"[AICORE] [{prefix}] Package file destinationname value is null. This is an error in the manifest file, please contact the developers. File: {installerFile.FriendlyName}, PackageFile: {pf.SourceName}");
                        CoreCrashes.TrackError?.Invoke(new Exception($"{installerFile.FriendlyName} has a package file with a null source name! This must be fixed."));
                    }
                }
                else
                {
                    Log.Error($"[AICORE] [{prefix}] File specified by manifest doesn't exist after extraction for file: {pf.SourceName}. This is an error in the manifest.");
                    CoreCrashes.TrackError?.Invoke(new Exception($"{installerFile.FriendlyName} has a package file that didn't exist after preparing and could not stage: {pf.SourceName}"));
                }
            }
        }

        /// <summary>
        /// Converts a folder of files into a MEM package with the specified filename.
        /// </summary>
        /// <param name="sourceDir"></param>
        /// <param name="outputFile"></param>
        /// <param name="targetGame"></param>
        private int BuildMEMPackageFile(string uiname, string sourceDir, string outputFile, MEGame targetGame, out string buildFailedReason, Action<int, int> progressCallback = null, InstallerFile installerFile = null)
        {
            buildFailedReason = null;
            string localBuildFailedReason = null; // We can't use out params in lambdas
            void handleIPC(string command, string param)
            {
                switch (command)
                {
                    case "TASK_PROGRESS":
                        if (installerFile != null)
                        {
                            // Local progress
                            installerFile.StatusText = $"Building MEM installation package {param}%";
                        }
                        else
                        {
                            progressCallback?.Invoke(TryConvert.ToInt32(param, 0), 100);
                            UpdateOverallStatusCallback?.Invoke($"Building install package for {uiname}");
                        }
                        break;
                    case "PROCESSING_FILE":
                        Log.Information($"[AICORE] MEMCompiler PROCESSING_FILE {param}");
                        // Unpacking file
                        break;
                    case "ERROR_NO_BUILDABLE_FILES":
                        Log.Error(@"[AICORE] MEM reports there are no buildable files. This may occur if the targeted textures are not part of the basegame/official dlc set - installing textures from non-mem file to custom textures is not yet supported");
                        localBuildFailedReason = "Install package not built: no usable files. See log for more info";
                        break;
                    default:
                        Debug.WriteLine($"Unhandled IPC: {command} {param}");
                        break;
                }
            }

            Log.Information($"[AICORE] Building MEM package {uiname} from {sourceDir}, output to {outputFile}");

            if (Settings.DebugLogs)
            {
                Log.Debug($@"[AICORE] [{uiname}] Input files to MEM compiler");
                var files = Directory.GetFiles(sourceDir);
                foreach (var f in files)
                {
                    Log.Debug($@"[AICORE] [{uiname}]      {Path.GetFileName(f)}");
                }
            }


            int exitcode = -1;
            MEMIPCHandler.RunMEMIPCUntilExit($"--convert-to-mem --gameid {targetGame.ToGameNum()} --input \"{sourceDir}\" --output \"{outputFile}\" --ipc",
                null,
                handleIPC,
                x => Log.Error($"[AICORE] StdError building {uiname}: {x}"),
                x => exitcode = x); //Change to catch exit code of non zero.
            buildFailedReason = localBuildFailedReason;
            return exitcode;
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
                    case "PROCESSING_FILE":
                        // Unpacking file
                        if (file != null)
                        {
                            file.StatusText = $"Decompiling {param}";
                        }
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
                    Log.Error("[AICORE] Unsupported file extension: " + extension);
                    break;
            }

            MEMIPCHandler.RunMEMIPCUntilExit(args,
                null,
                handleIPC,
                x => Log.Error($"[AICORE] StdError decompiling {sourceFile}: {x}"),
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
            if (_installOptions.InstallALOT)
            {
                filesToStage.AddRange(readyFiles.Where(x => x.AlotVersionInfo != null && x.AlotVersionInfo.ALOTVER > 0 && x.AlotVersionInfo.ALOTUPDATEVER == 0)); //Add MAJOR ALOT file
            }

            if (_installOptions.InstallALOTUpdate)
            {
                filesToStage.AddRange(readyFiles.Where(x => x.AlotVersionInfo != null && x.AlotVersionInfo.ALOTVER > 0 && x.AlotVersionInfo.ALOTUPDATEVER != 0)); //Add MINOR ALOT file
            }

            if (_installOptions.InstallMEUITM)
            {
                filesToStage.AddRange(readyFiles.Where(x => x.AlotVersionInfo != null && x.AlotVersionInfo.MEUITMVER != 0)); //Add MEUITM file
            }

            if (_installOptions.InstallAddons)
            {
                filesToStage.AddRange(readyFiles.Where(x => x.AlotVersionInfo != null && x.AlotVersionInfo.IsNotVersioned && x is ManifestFile && !(x is PreinstallMod))); //Add Addon files that don't have a set ALOTVersionInfo.
            }

            if (_installOptions.InstallPreinstallMods)
            {
                filesToStage.AddRange(readyFiles.Where(x => x is PreinstallMod)); //Add Addon files that don't have a set ALOTVersionInfo.
            }

            if (_installOptions.InstallUserfiles)
            {
                filesToStage.AddRange(readyFiles.Where(x => x is UserFile));
            }

            //DEBUG ONLY
            // filesToStage.ReplaceAll(filesToStage.Where(x =>
            // {
            //    return x.FriendlyName.Contains("EDI From");
            //    //var finfo = new FileInfo(x.GetUsedFilepath()).Length;
            //    //return finfo < (250 * 1024 * 1024);
            // }).ToList());

            return filesToStage.OrderBy(x => x.InstallPriority).ToList();
        }
    }
}
