﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ALOTInstallerCore.Helpers.AppSettings;
using ALOTInstallerCore.Objects.Manifest;
using Serilog;
using ALOTInstallerCore.Objects;

namespace ALOTInstallerCore.Helpers
{
    /// <summary>
    /// Class for handling the Texture Library
    /// </summary>
    public static class TextureLibrary
    {
        /// <summary>
        /// A list of all manifest files, across all modes
        /// </summary>
        private static FileSystemWatcher watcher;

        private static List<ManifestFile> manifestFiles;
        private static Action<ManifestFile> readyStatusChanged;
        private static System.Timers.Timer fullRefreshTimer;

        /// <summary>
        /// Types of files that the installer will recognize for importing/user files
        /// </summary>
        public static string[] ImportableFileTypes { get; } = new[]
            {".7z", ".rar", ".zip", ".bmp", ".dds", ".mem", ".tpf", ".mod", ".png", ".tga"};

        /// <summary>
        /// Sets up the folder watcher for the texture library folder.
        /// </summary>
        /// <param name="watchedManifestFiles"></param>
        /// <param name="readyStatusChangedCallback"></param>
        public static void SetupLibraryWatcher(List<ManifestFile> watchedManifestFiles,
            Action<ManifestFile> readyStatusChangedCallback)
        {
            if (watcher != null)
            {
                StopLibraryWatcher();
            }

            TextureLibrary.manifestFiles = watchedManifestFiles;
            TextureLibrary.readyStatusChanged = readyStatusChangedCallback;
            Debug.WriteLine("Starting filesystem watcher on " + Settings.TextureLibraryLocation);

            watcher = new FileSystemWatcher(Settings.TextureLibraryLocation)
            {
                // Just notify on everything because it seems things like move are done through attributes (??)
                NotifyFilter = NotifyFilters.Attributes |
                               NotifyFilters.CreationTime |
                               NotifyFilters.FileName |
                               NotifyFilters.LastWrite |
                               NotifyFilters.Size |
                               NotifyFilters.Security,
                Filter = "*.*" //Filters is not supported on .NET Standard 2.1

            };
            // Add event handlers.
            if (fullRefreshTimer == null)
            {
                fullRefreshTimer = new System.Timers.Timer(15000)
                {
                    AutoReset = true,
                    Enabled = true
                };
                fullRefreshTimer.Elapsed += async (a, b) =>
                {
                    if (ManifestHandler.MasterManifest != null)
                    {
                        Debug.WriteLine("Full ready status refresh");
                        await Task.Run(() =>
                        {
                            var updatedFiles =
                                TextureLibrary.ResetAllReadyStatuses(ManifestHandler.MasterManifest.GetAllInstallerFiles());
                            if (updatedFiles.Any())
                            {
                                readyStatusChangedCallback?.Invoke(null);
                            }
                        });
                    }
                };
            }
            else
            {
                fullRefreshTimer.Enabled = true;
            }

            watcher.Changed += OnLibraryFileChanged;
            watcher.Created += OnLibraryFileChanged;
            watcher.Deleted += OnLibraryFileChanged;
            watcher.Renamed += OnLibraryFileChanged;
            watcher.EnableRaisingEvents = true;

        }

        private static void OnLibraryFileChanged(object sender, FileSystemEventArgs e)
        {
            if (e.Name != null)
            {
                Debug.WriteLine($"Change {e.ChangeType} for {e.Name}");
                try
                {
                    var matchingManifestFile = manifestFiles.Find(x =>
                        Path.GetFileName(x.GetUsedFilepath()).Equals(e.Name, StringComparison.InvariantCultureIgnoreCase));
                    if (matchingManifestFile?.UpdateReadyStatus() ?? false)
                    {
                        readyStatusChanged?.Invoke(matchingManifestFile);
                    }

                    if (e.ChangeType == WatcherChangeTypes.Renamed && e is RenamedEventArgs rea)
                    {
                        // Trigger on old name too.
                        matchingManifestFile = manifestFiles.Find(x =>
                            Path.GetFileName(x.GetUsedFilepath())
                                .Equals(rea.OldName, StringComparison.InvariantCultureIgnoreCase));
                        if (matchingManifestFile?.UpdateReadyStatus() ?? false)
                        {
                            readyStatusChanged?.Invoke(matchingManifestFile);
                            return;
                        }
                    }

                    if (e.ChangeType == WatcherChangeTypes.Deleted)
                    {
                        // Edge case: Unpacked file is moved out of library. This makes GetUsedPath() fail as file does not exist, but file was just moved.
                        matchingManifestFile = manifestFiles.Find(x =>
                            x.UnpackedSingleFilename != null &&
                            x.UnpackedSingleFilename.Equals(e.Name, StringComparison.InvariantCultureIgnoreCase));
                        if (matchingManifestFile?.UpdateReadyStatus() ?? false)
                        {
                            readyStatusChanged?.Invoke(matchingManifestFile);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Apparently this can occur if files are moved in bulk in or out. Not really sure why or how
                    Log.Error($@"[AICORE] Error updating status of manifest file {e.Name} due to file change: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Attempts to import manifest files from the specified folder into the texture library. This method runs synchronously and should be run on a background thread
        /// </summary>
        /// <param name="folder"></param>
        /// <param name="manifestFiles"></param>
        public static void ImportFromFolder(string folder, List<ManifestFile> manifestFiles,
            Action<string, long, long> progressCallback = null,
            Action<List<ManifestFile>> importFinishedResultsCallback = null)
        {
            // Todo: support (1), (2), etc extensions on filenames due to duplicates.
            object syncObj = new object();
            List<ManifestFile> importedFiles = new List<ManifestFile>();

            var filesInFolder = Directory.GetFiles(folder, "*.*", SearchOption.TopDirectoryOnly);
            foreach (var manifestFileToCheck in manifestFiles.Where(x => !x.Ready))
            {
                void importFinished(bool imported, string failureReason)
                {
                    if (imported)
                    {
                        importedFiles.Add(manifestFileToCheck);
                    }
                    else
                    {
                        Log.Error($"[AICORE] Error importing file: {failureReason}");
                    }

                    lock (syncObj)
                    {
                        Monitor.Pulse(syncObj);
                    }
                }

                // Main file
                var matchingFolderFile = filesInFolder.FirstOrDefault(x =>
                    Path.GetFileName(x).Equals(manifestFileToCheck.Filename, StringComparison.InvariantCultureIgnoreCase));

                if (matchingFolderFile != null && new FileInfo(matchingFolderFile).Length == manifestFileToCheck.FileSize)
                {
                    // Import
                    importFileToLibrary(manifestFileToCheck, matchingFolderFile, false, progressCallback, importFinished);
                    lock (syncObj)
                    {
                        Monitor.Wait(syncObj);
                    }

                    foreach (var mf in manifestFiles.Where(x => !x.Ready && x.Filename == manifestFileToCheck.Filename ))
                    {
                        // Multiple manifest files are tied to this single file (multiple modes)
                        // We should update the status
                        mf.UpdateReadyStatus(); // Update all of them
                    }
                    continue;
                }

                // Torrent file
                if (manifestFileToCheck.TorrentFilename != null)
                {
                    matchingFolderFile = filesInFolder.FirstOrDefault(x =>
                        Path.GetFileName(x).Equals(manifestFileToCheck.TorrentFilename, StringComparison.InvariantCultureIgnoreCase));
                    if (matchingFolderFile != null && new FileInfo(matchingFolderFile).Length == manifestFileToCheck.FileSize)
                    {
                        // Import
                        importFileToLibrary(manifestFileToCheck, matchingFolderFile, false, progressCallback, importFinished);
                        lock (syncObj)
                        {
                            Monitor.Wait(syncObj);
                        }

                        continue;
                    }
                }

                // Unpacked file
                if (manifestFileToCheck.UnpackedSingleFilename != null)
                {
                    matchingFolderFile = filesInFolder.FirstOrDefault(x =>
                        Path.GetFileName(x).Equals(manifestFileToCheck.UnpackedSingleFilename,
                            StringComparison.InvariantCultureIgnoreCase));
                    if (matchingFolderFile != null && new FileInfo(matchingFolderFile).Length == manifestFileToCheck.UnpackedFileSize)
                    {
                        // Import
                        importFileToLibrary(manifestFileToCheck, matchingFolderFile, true, progressCallback, importFinished);
                        lock (syncObj)
                        {
                            Monitor.Wait(syncObj);
                        }
                    }
                }
            }

            importFinishedResultsCallback?.Invoke(importedFiles);
        }

        /// <summary>
        /// Attempts to import the specified file into the library. This method is asynchronous, use the callbacks to be informed of progress and results.
        /// </summary>
        /// <param name="filename">The file to be tested for importing</param>
        /// <param name="manifestFiles">Manifest files to check against</param>
        /// <param name="fileImported">Notification when file is imported, and the result (as a string of why it failed, null if successful)</param>
        /// <param name="progressCallback">Callback to be notified of progress (copy mode only)</param>
        /// <returns>Tee manifest file that is attempting to be imported, null if the listed filename matched nothing</returns>
        public static ManifestFile AttemptImportManifestFile(string filename,
            List<ManifestFile> manifestFiles,
            Action<bool, string> fileImported,
            Action<string, long, long> progressCallback = null)
        {
            var fsize = new FileInfo(filename).Length;
            var matchingMF = manifestFiles.FirstOrDefault(x =>
                Path.GetFileName(filename).Equals(x.Filename, StringComparison.InvariantCultureIgnoreCase) &&
                x.FileSize == fsize);
            if (matchingMF != null)
            {
                // Try main
                if (!matchingMF.Ready)
                {
                    importFileToLibrary(matchingMF, filename, false, progressCallback, fileImported);
                }
                return matchingMF;
            }

            matchingMF = manifestFiles.FirstOrDefault(x => x.TorrentFilename != null &&
                                                           Path.GetFileName(filename).Equals(x.TorrentFilename,
                                                               StringComparison.InvariantCultureIgnoreCase) &&
                                                           x.FileSize == fsize);
            if (matchingMF != null)
            {
                // Torrent file => library main
                if (!matchingMF.Ready)
                {
                    importFileToLibrary(matchingMF, filename, false, progressCallback, fileImported);
                }
                return matchingMF;
            }

            matchingMF = manifestFiles.FirstOrDefault(x =>
                x.UnpackedFileSize != 0 && x.UnpackedSingleFilename != null &&
                Path.GetFileName(filename)
                    .Equals(x.UnpackedSingleFilename, StringComparison.InvariantCultureIgnoreCase) &&
                x.UnpackedFileSize == fsize);
            if (matchingMF != null)
            {
                // Single file unpacked
                if (!matchingMF.Ready)
                {
                    importFileToLibrary(matchingMF, filename, true, progressCallback, fileImported);
                }
                return matchingMF;
            }

            return null;
        }

        /// <summary>
        /// Imports file to the library. This call is asynchronous, use the callbacks to be notified of progress and completion.
        /// </summary>
        /// <param name="mf"></param>
        /// <param name="sourceFile"></param>
        /// <param name="isUnpacked"></param>
        /// <param name="progressCallback"></param>
        /// <param name="importFinishedCallback"></param>
        private static void importFileToLibrary(ManifestFile mf, string sourceFile, bool isUnpacked,
            Action<string, long, long> progressCallback = null, Action<bool, string> importFinishedCallback = null)
        {
            // Precheck file is not already ready, which might occur if user does stuff manually
            // or we have multiple same-files
            if (mf.Ready)
                return; // File is already ready, nothing to do

            Log.Information($"[AICORE] Importing {sourceFile} into texture library");
            // This may need to be WINDOWS ONLY for roots
            string importingfrom = Path.GetPathRoot(sourceFile);
            string importingto = Path.GetPathRoot(Settings.TextureLibraryLocation);

            NamedBackgroundWorker nbw = new NamedBackgroundWorker("ImportWorker");
            nbw.DoWork += (a, b) =>
            {
                var destFile = Path.Combine(Settings.TextureLibraryLocation,
                    isUnpacked ? mf.UnpackedSingleFilename : mf.Filename);
                if (File.Exists(destFile)) File.Delete(destFile);
                if (Settings.MoveFilesWhenImporting && importingfrom == importingto)
                {
                    // Move
                    try
                    {
                        File.Move(sourceFile, destFile);
                        importFinishedCallback?.Invoke(true, null);
                    }
                    catch (Exception e)
                    {
                        Log.Error($"[AICORE] Error moving {sourceFile} to library: {e.Message}");
                        importFinishedCallback?.Invoke(false,
                            $"An error occurred moving the file to the library: {e.Message}.");
                    }
                }
                else
                {
                    // Copy with progress
                    WebClient downloadClient = new WebClient();
                    downloadClient.DownloadProgressChanged += (s, e) =>
                    {
                        progressCallback?.Invoke(mf.FriendlyName, e.BytesReceived, e.TotalBytesToReceive);
                    };
                    downloadClient.DownloadFileCompleted += (s, e) =>
                    {
                        if (e.Error != null)
                        {
                            Log.Error($"[AICORE] An error occurred copying the file to the destination:");
                            e.Error.WriteToLog("[AICORE] ");
                            importFinishedCallback?.Invoke(false,
                                $"An error occurred copying the file to the library: {e.Error.Message}.");
                        }
                        else if (File.Exists(destFile))
                        {
                            importFinishedCallback?.Invoke(true, null);
                        }
                        else
                        {
                            Log.Error(
                                "[AICORE] Destination file doesn't exist after file copy. This may need some more analysis to determine the exact cause.");
                            Log.Error("[AICORE] Destination file: " + destFile);
                            importFinishedCallback?.Invoke(false,
                                $"Destination file doesn't exist after copy: {destFile}. This may be a bug in the program, view the application log for more information");
                        }

                    };
                    downloadClient.DownloadFileAsync(new Uri(sourceFile), destFile);
                }
            };
            nbw.RunWorkerCompleted += (a, b) =>
            {
                if (b.Error != null)
                {
                    Log.Error($"[AICORE] Error importing {sourceFile}: {b.Error.Message}");
                    importFinishedCallback?.Invoke(false,
                        $"An error occurred while importing {sourceFile}: {b.Error.Message}");
                }
            };
            nbw.RunWorkerAsync();
        }

        /// <summary>
        /// Unhooks and stops the library watcher
        /// </summary>
        public static void StopLibraryWatcher()
        {
            Debug.WriteLine("Killing filesystem watcher");
            if (fullRefreshTimer != null) fullRefreshTimer.Enabled = false;
            readyStatusChanged = null;
            manifestFiles = null;
            if (watcher != null)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Changed -= OnLibraryFileChanged;
                watcher.Created -= OnLibraryFileChanged;
                watcher.Deleted -= OnLibraryFileChanged;
                watcher.Renamed -= OnLibraryFileChanged;
                watcher.Dispose();
                watcher = null;
            }
        }

        /// <summary>
        /// Clears actions callbacks
        /// </summary>
        public static void UnregisterCallbacks()
        {
            readyStatusChanged = null;
        }

        /// <summary>
        /// Forces all items in the specified list to refresh their ready status
        /// </summary>
        public static List<InstallerFile> ResetAllReadyStatuses(List<InstallerFile> files)
        {
            var updatedFiles = new List<InstallerFile>();
            foreach (var v in files)
            {
                if (v.UpdateReadyStatus())
                {
                    updatedFiles.Add(v);
                }
            }

            return updatedFiles;
        }

        /// <summary>
        /// Gets a list of filenames (not paths!) in the texture library that are not used. This can be due to the manifest changing over time.
        /// </summary>
        /// <returns></returns>
        public static List<string> GetUnusedFilesInLibrary()
        {
            var files = Directory.GetFiles(Settings.TextureLibraryLocation).Select(o => Path.GetFileName(o)).ToList();
            foreach (var f in ManifestHandler.GetAllManifestFiles())
            {
                var fname = Path.GetFileName(f.GetUsedFilepath());
                var numRemoved = files.RemoveAll(n => n.Equals(fname, StringComparison.OrdinalIgnoreCase));
            }

            foreach (var f in files)
            {
                Debug.WriteLine($"Unused file in library: {f}");
            }

            return files;
        }

        /// <summary>
        /// Attempts to import unpacked versions of files from the specified directory to the texture library
        /// </summary>
        /// <param name="directory"></param>
        /// <param name="manifestFiles"></param>
        /// <returns></returns>
        public static bool AttemptImportUnpackedFiles(string directory, List<ManifestFile> manifestFiles,
            bool switchFilesToUnpacked = true, Action<string, long, long> progressCallback = null,
            bool forceCopy = false, bool unReadyOnly = false, bool dontCheckFilename = false)
        {
            try
            {
                DriveInfo sDi = new DriveInfo(directory);
                DriveInfo dDi = new DriveInfo(Settings.TextureLibraryLocation);
                var files = Directory.GetFiles(directory);
                Dictionary<ManifestFile, string> mfToUnpackedMap = new Dictionary<ManifestFile, string>();
                foreach (var mf in manifestFiles)
                {
                    mf.UpdateReadyStatus(); //Make sure have the most up to date data
                    if (mf.Ready && unReadyOnly) continue;
                    if (mf.UnpackedSingleFilename != null)
                    {
                        if (dontCheckFilename || Path.GetFileName(mf.StagedName).Equals(mf.Filename, StringComparison.InvariantCultureIgnoreCase))
                        {
                            // The ready file is the normal file but there is unpacked single file support for this
                            // This file was extracted or copied so it's still in library
                            // Find the unpacked file
                            foreach (var uf in files)
                            {
                                var len = new FileInfo(uf).Length;
                                if (len == mf.UnpackedFileSize && Path.GetExtension(mf.UnpackedSingleFilename) ==
                                    Path.GetExtension(uf))
                                {
                                    if (len < 1000000000)
                                    {
                                        // < 1GB. Bigger would make this take a long time... not much we can do about this
                                        var md5 = Utilities.CalculateMD5(uf);
                                        if (md5 != mf.UnpackedFileMD5)
                                            continue; //This is not correct unpacked file
                                    }

                                    // It's the right file, or is probably the right file... The chance of same sized files this big is probably pretty rare, right?
                                    mfToUnpackedMap[mf] = uf;
                                    break;
                                }
                            }
                        }
                        else if (mf.StagedName != null && !File.Exists(mf.StagedName) && Path.GetExtension(mf.StagedName).Equals(Path.GetExtension(mf.UnpackedSingleFilename), StringComparison.InvariantCultureIgnoreCase))
                        {
                            // Ready file is using unpacked file but the unpacked file isn't available so it returned the main one
                            // This needs to be moved back
                            foreach (var uf in files)
                            {
                                var len = new FileInfo(uf).Length;
                                if (len == mf.UnpackedFileSize && Path.GetExtension(mf.UnpackedSingleFilename) ==
                                    Path.GetExtension(uf))
                                {
                                    if (len < 1000000000)
                                    {
                                        // < 1GB. Bigger would make this take a long time... not much we can do about this
                                        var md5 = Utilities.CalculateMD5(uf);
                                        if (md5 != mf.UnpackedFileMD5)
                                            continue; //This is not correct unpacked file
                                    }

                                    mfToUnpackedMap[mf] = uf;
                                    break;
                                }
                            }
                        }
                    }
                }

                foreach (var movableFile in mfToUnpackedMap)
                {
                    var oldFname = movableFile.Key.GetUsedFilepath();
                    var destF = Path.Combine(Settings.TextureLibraryLocation, movableFile.Key.UnpackedSingleFilename);
                    bool cancelDueToError = false;
                    if (sDi.RootDirectory.FullName == dDi.RootDirectory.FullName && !forceCopy)
                    {
                        // Move
                        if (!File.Exists(destF))
                        {
                            Log.Information($"[AICORE] Moving unpacked file to texture library: {movableFile.Value} -> {destF}");
                            File.Move(movableFile.Value, destF);
                        }
                    }
                    else
                    {
                        //Copy
                        Log.Information($"[AICORE] Copying unpacked file to texture library: {movableFile.Value} -> {destF}");
                        CopyTools.CopyFileWithProgress(movableFile.Value, destF,
                            (x, y) => progressCallback?.Invoke(movableFile.Key.FriendlyName, x, y),
                            x => cancelDueToError = true
                        );
                    }
                    movableFile.Key.UpdateReadyStatus();
                    if (switchFilesToUnpacked && !cancelDueToError && oldFname != movableFile.Key.GetUsedFilepath())
                    {
                        if (!movableFile.Key.IsBackedByUnpacked())
                        {
                            Log.Error("[AICORE] File copied back did not trigger switch to unpacked version! Something probably went wrong on file copy.");
                        }
                        else if (File.Exists(oldFname))
                        {
                            // Switched to unpacked
                            Log.Information(
                                $"[AICORE] Deleting packed version of manifest file now that it is in unpacked mode: {oldFname}");
                            try
                            {
                                File.Delete(oldFname);
                            }
                            catch (Exception e)
                            {
                                Log.Error(
                                    $"[AICORE] Unable to delete packed version of {movableFile.Key.FriendlyName}: {e.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error($"[AICORE] Error trying to move unpacked files to texture library: {e.Message}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if the specified file is considered a valid user file. Valid user file archives have at least one valid file type in them. Texture files must have hash in filename 0xHHHHHHHH
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static bool IsUserFileUsable(string file, out string failureReason)
        {
            failureReason = null;
            List<string> filenamesToCheck = new List<string>();
            var MFextension = Path.GetExtension(file.ToLower());
            bool isArchive = false;
            if (MFextension == ".7z" || MFextension == ".rar" || MFextension == ".zip")
            {
                isArchive = true;
                filenamesToCheck.ReplaceAll(MEMIPCHandler.GetFileListing(file));
            }
            else
            {
                filenamesToCheck.Add(file);
            }

            bool hasAnOkayItem = false;
            foreach (var v in filenamesToCheck)
            {
                hasAnOkayItem |= isFilenameOkay(Path.GetFileNameWithoutExtension(v.ToLower()), Path.GetExtension(v.ToLower()));
            }

            if (!hasAnOkayItem)
            {
                failureReason = isArchive ? "No file types/names in this archive are usable" : "File type/name is not usable";
            }

            return hasAnOkayItem;
        }

        /// <summary>
        /// Determines if filename + extension are acceptable for use
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="extension"></param>
        /// <returns></returns>
        private static bool isFilenameOkay(string filename, string extension)
        {
            switch (extension)
            {
                case ".mod": return true; //File is usable
                case ".tpf": return true; //File is usable
                case ".mem": return true; //File is usable
                case ".png":
                case ".dds":
                case ".bmp":
                case ".tga":
                    string regex = "0x[0-9a-f]{8}"; //This matches even if user has more chars after the 8th hex so...
                    var isOK = Regex.IsMatch(filename, regex);
                    if (!isOK)
                    {
                        Log.Warning(
                            $"[AICORE] Rejecting image/texture file {filename} due to missing 0xhhhhhhhh texture CRC to replace");
                    }

                    return isOK;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Handle incoming files from drag/drop or an importing interface
        /// </summary>
        /// <param name="files">List of full file paths to attempt to ingest</param>
        /// <param name="userFileMode">Null to attempt manifest then user file, false to force manifest mode, true to force user mode</param>
        /// <param name="setStatusTextCallback"></param>
        /// <param name="importingProgressCallback"></param>
        /// <param name="selectGameCallback"></param>
        /// <param name="unknown"></param>
        public static List<ImportResult> IngestFiles(IEnumerable<string> files,
            bool? userFileMode,
            Action<string> setStatusTextCallback = null,
            Action<string, long, long> importingProgressCallback = null,
            Func<string, ApplicableGame?> selectGameCallback = null, Action<InstallerFile> addedFileToModeCallback = null)
        {
            var manifestFiles = ManifestHandler.GetAllManifestFiles();
            List<ImportResult> importResults = new List<ImportResult>();
            object syncObj = new object();
            foreach (var file in files)
            {
                if (Directory.GetParent(file).FullName.StartsWith(Settings.TextureLibraryLocation,
                    StringComparison.InvariantCultureIgnoreCase))
                {
                    importResults.Add(new ImportResult()
                    {
                        ImportName = Path.GetFileName(file),
                        Result = "Cannot add files to installer from texture library directory",
                        Accepted = false
                    });
                    continue;
                }


                // Okay to import or add from location - official only for now
                bool importInManifestMode = !userFileMode.HasValue || !userFileMode.Value;
                bool importInUserMode = !userFileMode.HasValue || userFileMode.Value;
                bool handled = false;
                if (importInManifestMode)
                {
                    bool successful = false;
                    string failedReason = null;
                    var attemptingImport = TextureLibrary.AttemptImportManifestFile(file, manifestFiles,
                        (successfullyImported, failureReason) =>
                        {
                            successful = successfullyImported;
                            failedReason = failureReason;
                            lock (syncObj)
                            {
                                Monitor.Pulse(syncObj);
                            }
                        },
                        importingProgressCallback);

                    if (attemptingImport != null)
                    {
                        if (!attemptingImport.Ready)
                        {
                            Debug.WriteLine($@"Ingesting manifest file {file}");
                            // Wait till import completes
                            lock (syncObj)
                            {
                                Monitor.Wait(syncObj);
                            }
                            Debug.WriteLine($@"Ingested manifest file {file}");

                            handled = successful;
                            importResults.Add(new ImportResult()
                            {
                                ImportName = attemptingImport.FriendlyName,
                                Result = successful ? "Manifest file imported" : failedReason,
                                Accepted = successful
                            });
                            continue;
                        }
                        else
                        {
                            handled = true;
                            importResults.Add(new ImportResult()
                            {
                                ImportName = attemptingImport.FriendlyName,
                                Result = "Already imported",
                                Accepted = true
                            });
                            continue;
                        }
                    }
                    else if (!importInUserMode)
                    {
                        // Will not process as user file.
                        importResults.Add(new ImportResult()
                        {
                            ImportName = Path.GetFileName(file),
                            Result = "Not a manifest file",
                            Accepted = false
                        });
                        continue;
                    }
                } // END MANIFEST FILE PARSING

                // Check other locations as the file won't be moved/copied.
                if (Directory.GetParent(file).FullName.StartsWith(Settings.StagingLocation,
                    StringComparison.InvariantCultureIgnoreCase))
                {
                    importResults.Add(new ImportResult()
                    {
                        ImportName = Path.GetFileName(file),
                        Result = "Cannot add files to installer from staging directory",
                        Accepted = false
                    });
                    continue;
                }

                if (Directory.GetParent(file).FullName.StartsWith(Path.GetTempPath(),
                    StringComparison.InvariantCultureIgnoreCase))
                {
                    importResults.Add(new ImportResult()
                    {
                        ImportName = Path.GetFileName(file),
                        Result = "Cannot add files to installer from temp directory - if this is a manifest file, drop the archive directly, if this is a user file, extract it first",
                        Accepted = false
                    });
                    continue;
                }

                bool shouldContinue = true;
                foreach (var v in Locations.GetAllAvailableTargets())
                {
                    if (Directory.GetParent(file).FullName.StartsWith(v.TargetPath,
                        StringComparison.InvariantCultureIgnoreCase))
                    {
                        importResults.Add(new ImportResult()
                        {
                            ImportName = Path.GetFileName(file),
                            Result = "Cannot add files to installer from within a game directory",
                            Accepted = false
                        });
                        shouldContinue = false;
                        break;
                    }
                }

                if (!shouldContinue) continue; //Skip to next file


                //if (!handled && importInUserMode)
                //{
                // User file
                var fi = new FileInfo(file);
                //var matchingManifestFile = TextureLibrary.manifestFiles.FirstOrDefault(x => x.FileSize == fi.Length);
                //if (matchingManifestFile != null && ManifestHandler.CurrentMode != ManifestMode.Free)
                //{
                //    // Did user rename file?

                //}

                var preinstallMods = ManifestHandler.GetAllPreinstallMods();
                PreinstallMod matchingPIM = preinstallMods.FirstOrDefault(x => x.FileSize == fi.Length
                    && (x.Filename.Equals(Path.GetFileName(file),
                            StringComparison.InvariantCultureIgnoreCase) ||
                        x.TorrentFilename.Equals(Path.GetFileName(file),
                            StringComparison.InvariantCultureIgnoreCase)));
                if (matchingPIM != null)
                {
                    // It's a preinstall mod user added.
                    // Add the (cloned) original ManifestFile to this mode
                    var newObj = new PreinstallMod(matchingPIM)
                    {
                        ForcedSourcePath = file
                    };
                    newObj.UpdateReadyStatus();
                    ManifestHandler.MasterManifest.ManifestModePackageMappping[ManifestHandler.CurrentMode].ManifestFiles.Add(newObj);
                    importResults.Add(new ImportResult()
                    {
                        Result = $"Added for install",
                        ImportName = matchingPIM.FriendlyName,
                        Accepted = true
                    });
                    addedFileToModeCallback?.Invoke(newObj);
                    continue;
                }
                else
                {
                    // Standard user file

                    var usable = TextureLibrary.IsUserFileUsable(file, out var notUsableReason);
                    if (!usable)
                    {
                        // File is not usable
                        importResults.Add(new ImportResult()
                        {
                            Result = "Not usable",
                            Reason = notUsableReason,
                            ImportName = Path.GetFileName(file),
                            Accepted = false
                        });
                        continue;
                    }
                    else
                    {
                        // File is usable

                        Debug.WriteLine($@"Ingesting user file {file}");
                        var failedToAddReason = ManifestHandler.MasterManifest.ManifestModePackageMappping[ManifestHandler.CurrentMode].AttemptAddUserFile(file, selectGameCallback, out var addedUserFile);
                        Debug.WriteLine($@"Ingested user file {file}");
                        var importResult = new ImportResult()
                        {
                            Result = failedToAddReason ?? "Added for install",
                            ImportName = Path.GetFileName(file),
                            Accepted = failedToAddReason == null
                        };

                        importResults.Add(importResult);
                        if (addedUserFile != null)
                        {
                            importResult.Result += $" ({addedUserFile.ApplicableGames.ToCommaUIString()})";
                            addedFileToModeCallback?.Invoke(addedUserFile);
                        }
                        continue;
                        //}
                        //else
                        //{
                        //    importResults.Add(new ImportResult()
                        //    {
                        //        Result = "Skipped",
                        //        ImportName = Path.GetFileName(file)
                        //    });
                        //}
                    }
                }
                //}
            }
            return importResults;
        }

        public static void AttemptReimportFromStaging()
        {
            if (new DriveInfo(Settings.TextureLibraryLocation).RootDirectory.Name ==
                new DriveInfo(Settings.StagingLocation).RootDirectory.Name)
            {
                foreach (var game in Locations.AllMEGames)
                {
                    var path = Path.Combine(Settings.StagingLocation, game.ToString(), "InstallationPackages");
                    if (Directory.Exists(path))
                    {
                        Log.Information($@"[AICORE] Attempting reimport of possibly moved files from {path}");
                        TextureLibrary.AttemptImportUnpackedFiles(path,
                            ManifestHandler.GetAllManifestFiles()
                                .Where(x => x.UnpackedSingleFilename != null && !x.Ready)
                                .ToList(), false, null, false, unReadyOnly: true, dontCheckFilename: true);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Result of file import
    /// </summary>
    public class ImportResult
    {
        /// <summary>
        /// The short name for the import
        /// </summary>
        public string ImportName { get; set; }
        /// <summary>
        /// The result of the import
        /// </summary>
        public string Result { get; set; }
        /// <summary>
        /// The reason (if any) of the result
        /// </summary>
        public string Reason { get; set; }
        /// <summary>
        /// If the result was accepted by the installer
        /// </summary>
        public bool Accepted { get; set; }
    }
}