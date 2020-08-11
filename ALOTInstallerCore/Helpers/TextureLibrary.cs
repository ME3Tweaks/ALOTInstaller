using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using ALOTInstallerCore.Objects;
using ALOTInstallerCore.Objects.Manifest;
using Serilog;

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
        private static List<ManifestFile> allManifestFiles;
        private static FileSystemWatcher watcher;
        private static List<ManifestFile> manifestFiles;
        private static Action<ManifestFile> readyStatusChanged;
        public static void SetupLibraryWatcher(List<ManifestFile> manifestFiles, Action<ManifestFile> readyStatusChanged)
        {
            TextureLibrary.manifestFiles = manifestFiles;
            TextureLibrary.readyStatusChanged = readyStatusChanged;
            Debug.WriteLine("Starting filesystem watcher on " + Settings.TextureLibraryLocation);
            if (watcher != null)
            {
                StopLibraryWatcher();
            }
            watcher = new FileSystemWatcher(Settings.TextureLibraryLocation)
            {
                NotifyFilter = NotifyFilters.LastWrite
                                 | NotifyFilters.FileName
                                 | NotifyFilters.Size,
                Filter = "*.*" //Filters is not supported on .NET Standard 2.1
            };
            // Add event handlers.
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
                //Debug.WriteLine($"Change {e.ChangeType} for {e.Name}");
                var matchingManifestFile = manifestFiles.Find(x =>
                    Path.GetFileName(x.GetUsedFilepath()).Equals(e.Name, StringComparison.InvariantCultureIgnoreCase));
                if (matchingManifestFile?.UpdateReadyStatus() ?? false)
                {
                    readyStatusChanged?.Invoke(matchingManifestFile);
                }
            }
        }

        /// <summary>
        /// Attempts to import manifest files from the specified folder into the texture library. This method runs synchronously and should be run on a background thread
        /// </summary>
        /// <param name="folder"></param>
        /// <param name="manifestFiles"></param>
        public static void ImportFromFolder(string folder, List<ManifestFile> manifestFiles, Action<string, long, long> progressCallback = null, Action<List<ManifestFile>> importFinishedResultsCallback = null)
        {
            // Todo: support (1), (2), etc extensions on filenames due to duplicates.
            object syncObj = new object();
            List<ManifestFile> importedFiles = new List<ManifestFile>();

            var filesInFolder = Directory.GetFiles(folder, "*.*", SearchOption.TopDirectoryOnly);
            foreach (var v in manifestFiles.Where(x => !x.Ready))
            {
                void importFinished(bool imported, string failureReason)
                {
                    if (imported)
                    {
                        importedFiles.Add(v);
                    }
                    else
                    {
                        Log.Error($"Error importing file: {failureReason}");
                    }
                    lock (syncObj)
                    {
                        Monitor.Pulse(syncObj);
                    }
                }
                // Main file
                var matchingFile = filesInFolder.FirstOrDefault(x =>
                    Path.GetFileName(x).Equals(v.Filename, StringComparison.InvariantCultureIgnoreCase));
                if (matchingFile != null && new FileInfo(matchingFile).Length == v.FileSize)
                {
                    // Import
                    importFileToLibrary(v, matchingFile, false, progressCallback, importFinished);
                    lock (syncObj)
                    {
                        Monitor.Wait(syncObj);
                    }
                    continue;
                }

                // Torrent file
                if (v.TorrentFilename != null)
                {
                    matchingFile = filesInFolder.FirstOrDefault(x =>
                        Path.GetFileName(x).Equals(v.TorrentFilename, StringComparison.InvariantCultureIgnoreCase));
                    if (matchingFile != null && new FileInfo(matchingFile).Length == v.FileSize)
                    {
                        // Import
                        importFileToLibrary(v, matchingFile, false, progressCallback, importFinished);
                        lock (syncObj)
                        {
                            Monitor.Wait(syncObj);
                        }
                        continue;
                    }
                }

                // Unpacked file
                if (v.UnpackedSingleFilename != null)
                {
                    matchingFile = filesInFolder.FirstOrDefault(x =>
                        Path.GetFileName(x).Equals(v.UnpackedSingleFilename,
                            StringComparison.InvariantCultureIgnoreCase));
                    if (matchingFile != null && new FileInfo(matchingFile).Length == v.UnpackedFileSize)
                    {
                        // Import
                        importFileToLibrary(v, matchingFile, true, progressCallback, importFinished);
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
        /// <param name="fileImported">Notification when file is imported, and the result</param>
        /// <param name="progressCallback">Callback to be notified of progress (copy mode only)</param>
        /// <returns>True if an import is being attempted, false if no attempt at import occured.</returns>
        public static bool AttemptImportManifestFile(string filename, List<ManifestFile> manifestFiles, Action<bool, string> fileImported, Action<string, long, long> progressCallback = null)
        {
            var fsize = new FileInfo(filename).Length;
            var matchingMF = manifestFiles.FirstOrDefault(x => Path.GetFileName(filename).Equals(x.Filename, StringComparison.InvariantCultureIgnoreCase) && x.FileSize == fsize);
            if (matchingMF != null)
            {
                // Try main
                importFileToLibrary(matchingMF, filename, false, progressCallback, fileImported);
                return true;
            }

            matchingMF = manifestFiles.FirstOrDefault(x => x.TorrentFilename != null &&
                Path.GetFileName(filename).Equals(x.TorrentFilename, StringComparison.InvariantCultureIgnoreCase) && x.FileSize == fsize);
            if (matchingMF != null)
            {
                // Torrent file => library main
                importFileToLibrary(matchingMF, filename, false, progressCallback, fileImported);
                return true;
            }

            matchingMF = manifestFiles.FirstOrDefault(x => x.UnpackedFileSize != 0 && x.UnpackedSingleFilename != null && Path.GetFileName(filename).Equals(x.UnpackedSingleFilename, StringComparison.InvariantCultureIgnoreCase) && x.UnpackedFileSize == fsize);
            if (matchingMF != null)
            {
                // Single file unpacked
                importFileToLibrary(matchingMF, filename, true, progressCallback, fileImported);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Imports file to the library. This call is asynchronous, use the callbacks to be notified of progress and completion.
        /// </summary>
        /// <param name="mf"></param>
        /// <param name="sourceFile"></param>
        /// <param name="isUnpacked"></param>
        /// <param name="progressCallback"></param>
        /// <param name="importFinishedCallback"></param>
        private static void importFileToLibrary(ManifestFile mf, string sourceFile, bool isUnpacked, Action<string, long, long> progressCallback = null, Action<bool, string> importFinishedCallback = null)
        {
            Log.Information($"Importing {sourceFile} into texture library");
            // This may need to be WINDOWS ONLY for roots
            string importingfrom = Path.GetPathRoot(sourceFile);
            string importingto = Path.GetPathRoot(Settings.TextureLibraryLocation);

            NamedBackgroundWorker nbw = new NamedBackgroundWorker("ImportWorker");
            nbw.RunWorkerCompleted += (a, b) =>
            {
                if (b.Error != null)
                {
                    Log.Error($"Error importing {sourceFile}: {b.Error.Message}");
                    importFinishedCallback?.Invoke(false, $"An error occured while importing {sourceFile}: {b.Error.Message}");
                }
            };
            nbw.DoWork += (a, b) =>
            {
                var destFile = Path.Combine(Settings.TextureLibraryLocation, isUnpacked ? mf.UnpackedSingleFilename : mf.Filename);
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
                        Log.Error($"Error moving {sourceFile} to library: {e.Message}");
                        importFinishedCallback?.Invoke(false, $"An error occured moving the file to the library: {e.Message}.");
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
                    downloadClient.DownloadFileCompleted += async (s, e) =>
                    {
                        if (e.Error != null)
                        {
                            Log.Error($"An error occured copying the file to the destination:");
                            Log.Error(e.Error.Flatten());
                            importFinishedCallback?.Invoke(false, $"An error occured copying the file to the library: {e.Error.Message}.");
                        }
                        else if (File.Exists(destFile))
                        {
                            importFinishedCallback?.Invoke(true, null);
                        }
                        else
                        {
                            Log.Error("Destination file doesn't exist after file copy. This may need some more analysis to determine the exact cause.");
                            Log.Error("Destination file: " + destFile);
                            importFinishedCallback?.Invoke(false, $"Destination file doesn't exist after copy: {destFile}. This may be a bug in the program, view the application log for more information");
                        }

                    };
                    downloadClient.DownloadFileAsync(new Uri(sourceFile), destFile);
                }
            };
            nbw.RunWorkerAsync();
        }

        /// <summary>
        /// Unhooks and stops the library watcher
        /// </summary>
        public static void StopLibraryWatcher()
        {
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
        public static void ResetAllReadyStatuses(List<InstallerFile> files)
        {
            foreach (var v in files)
            {
                v.UpdateReadyStatus();
            }
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
                files.Remove(Path.GetFileName(f.GetUsedFilepath()));
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
        public static bool AttemptImportUnpackedFiles(string directory, List<ManifestFile> manifestFiles, bool switchFilesToUnpacked = true, Action<string, long, long> progressCallback = null, bool forceCopy = false)
        {
            try
            {
                DriveInfo sDi = new DriveInfo(directory);
                DriveInfo dDi = new DriveInfo(Settings.TextureLibraryLocation);
                var files = Directory.GetFiles(directory);
                Dictionary<ManifestFile, string> mfToUnpackedMap = new Dictionary<ManifestFile, string>();
                foreach (var mf in manifestFiles)
                {
                    if (mf.UnpackedSingleFilename != null)
                    {
                        if (Path.GetFileName(mf.StagedName).Equals(mf.Filename, StringComparison.InvariantCultureIgnoreCase))
                        {
                            // The ready file is the normal file but there is unpacked single file support for this
                            // This file was extracted or copied so it's still in library
                            // Find the unpacked file
                            foreach (var uf in files)
                            {
                                var len = new FileInfo(uf).Length;
                                if (len == mf.UnpackedFileSize && Path.GetExtension(mf.UnpackedSingleFilename) == Path.GetExtension(uf))
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
                                else
                                {
                                    // It's probably the right file... The chance of same sized files this big is probably pretty rare, right?
                                    mfToUnpackedMap[mf] = uf;
                                    break;
                                }
                            }
                        }
                        else if (!File.Exists(mf.StagedName) && Path.GetExtension(mf.StagedName).Equals(Path.GetExtension(mf.UnpackedSingleFilename), StringComparison.InvariantCultureIgnoreCase))
                        {
                            // Ready file is using unpacked file but the unpacked file isn't available so it returned the main one
                            // This needs to be moved back
                            foreach (var uf in files)
                            {
                                var len = new FileInfo(uf).Length;
                                if (len == mf.UnpackedFileSize && Path.GetExtension(mf.UnpackedSingleFilename) == Path.GetExtension(uf))
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
                    if (sDi.RootDirectory == dDi.RootDirectory && !forceCopy)
                    {
                        // Move
                        if (!File.Exists(destF))
                        {
                            Log.Information($"Moving unpacked file to texture library: {movableFile.Value} -> {destF}");
                            File.Move(movableFile.Value, destF);
                        }
                    }
                    else
                    {
                        //Copy
                        Log.Information($"Copying unpacked file to texture library: {movableFile.Value} -> {destF}");
                        CopyTools.CopyFileWithProgress(movableFile.Value, destF,
                            (x, y) => progressCallback?.Invoke(movableFile.Key.FriendlyName, x, y),
                            x => cancelDueToError = true
                        );
                    }

                    if (switchFilesToUnpacked && !cancelDueToError && oldFname != movableFile.Key.GetUsedFilepath())
                    {
                        if (!movableFile.Key.IsBackedByUnpacked())
                        {
                            Log.Error("File copied back did not trigger switch to unpacked version! Something probably went wrong on file copy.");
                        }
                        else
                        {
                            // Switched to unpacked
                            Log.Information($"Deleting packed version of manifest file now that it is in unpacked mode: {oldFname}");
                            try
                            {
                                File.Delete(oldFname);
                            }
                            catch (Exception e)
                            {
                                Log.Error($"Unable to delete packed version of {movableFile.Key.FriendlyName}: {e.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error($"Error trying to move unpacked files to texture library: {e.Message}");
                return false;
            }

            return true;
        }
    }
}
