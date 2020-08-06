using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
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
        private static FileSystemWatcher watcher;
        public static void SetupLibraryWatcher()
        {
            if (watcher != null)
            {
                watcher.Changed -= OnLibraryFileChanged;
                watcher.Created -= OnLibraryFileChanged;
                watcher.Deleted -= OnLibraryFileChanged;
                watcher.Renamed -= OnLibraryFileChanged;
                watcher.Dispose();
            }
            watcher = new FileSystemWatcher(Settings.TextureLibraryLocation)
            {
                NotifyFilter = NotifyFilters.LastWrite
                                 | NotifyFilters.FileName
                                 | NotifyFilters.Size,

                Filter = "*.zip;*.tpf;*.mem;*.rar;*.7z"
            };

            // Add event handlers.
            watcher.Changed += OnLibraryFileChanged;
            watcher.Created += OnLibraryFileChanged;
            watcher.Deleted += OnLibraryFileChanged;
            watcher.Renamed += OnLibraryFileChanged;
        }

        private static void OnLibraryFileChanged(object sender, FileSystemEventArgs e)
        {
            Debug.WriteLine("Change " + e.ChangeType);
        }

        /// <summary>
        /// Attempts to import manifest files from the specified folder into the texture library. This method runs synchronously and should be run on a background thread
        /// </summary>
        /// <param name="folder"></param>
        /// <param name="manifestFiles"></param>
        public static void ImportFromFolder(string folder, List<ManifestFile> manifestFiles, Action<long, long> progressCallback = null, Action<List<ManifestFile>> importFinishedResultsCallback = null)
        {
            // Todo: support (1), (2), etc extensions on filenames due to duplicates.
            object syncObj = new object();
            List<ManifestFile> importedFiles = new List<ManifestFile>();

            var filesInFolder = Directory.GetFiles(folder, "*.*", SearchOption.TopDirectoryOnly);
            foreach (var v in manifestFiles.Where(x => !x.Ready))
            {
                void importFinished(bool imported, string failureReason)
                {
                    lock (syncObj)
                    {
                        Monitor.Pulse(syncObj);
                    }

                    if (imported)
                    {
                        manifestFiles.Add(v);
                    }
                    else
                    {
                        Log.Error($"Error importing file: {failureReason}");
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
                        importFileToLibrary(v, matchingFile, false);
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
        public static bool AttemptImportManifestFile(string filename, List<ManifestFile> manifestFiles, Action<bool, string> fileImported, Action<long, long> progressCallback = null)
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
        private static void importFileToLibrary(ManifestFile mf, string sourceFile, bool isUnpacked, Action<long, long> progressCallback = null, Action<bool, string> importFinishedCallback = null)
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
                else
                {
                    importFinishedCallback?.Invoke((bool)b.Result, null);
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
                        progressCallback?.Invoke(e.BytesReceived, e.TotalBytesToReceive);
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
    }
}
