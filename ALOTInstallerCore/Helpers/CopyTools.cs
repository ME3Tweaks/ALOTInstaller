using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Threading;

namespace ALOTInstallerCore.Helpers
{
    /// <summary>
    /// Helper class for copying a directory with progress
    /// Copied and modified from ALOT Installer
    /// </summary>
    [Localizable(false)]
    public static class CopyTools
    {
        /// <summary>
        /// Copies a file using Webclient to provide progress callbacks with error handling.
        /// </summary>
        /// <param name="sourceFile"></param>
        /// <param name="destFile"></param>
        /// <param name="progressCallback"></param>
        /// <param name="errorCallback"></param>
        /// <returns></returns>
        public static bool CopyFileWithProgress(string sourceFile, string destFile, Action<long, long> progressCallback, Action<Exception> errorCallback)
        {
            WebClient downloadClient = new WebClient();
            downloadClient.DownloadProgressChanged += (s, e) =>
            {
                progressCallback?.Invoke(e.BytesReceived, e.TotalBytesToReceive);
            };
            bool result = false;
            object syncObj = new object();
            downloadClient.DownloadFileCompleted += async (s, e) =>
            {
                if (e.Error != null)
                {
                    Log.Error($"[AICORE] An error occurred copying the file to the destination:");
                    e.Error.WriteToLog("[AICORE] ");
                    errorCallback?.Invoke(e.Error);
                }
                else if (File.Exists(destFile))
                {
                    result = true;
                }
                else
                {
                    Log.Error($"[AICORE] Destination file doesn't exist after file copy: {destFile}");
                    errorCallback?.Invoke(new Exception($"Destination file doesn't exist after file copy: {destFile}"));
                }

                lock (syncObj)
                {
                    Monitor.Pulse(syncObj);
                }
            };
            downloadClient.DownloadFileAsync(new Uri(sourceFile), destFile);
            lock (syncObj)
            {
                Monitor.Wait(syncObj);
            }
            return result;
        }


        public static int CopyAll_ProgressBar(DirectoryInfo source,
            DirectoryInfo target,
            Action<int> totalItemsToCopyCallback = null,
            Action fileCopiedCallback = null,
            Func<string, bool> aboutToCopyCallback = null,
            int total = -1,
            int done = 0,
            string[] ignoredExtensions = null,
            bool testrun = false,
            Action<string, long, long> bigFileProgressCallback = null)
        {
            if (total == -1)
            {
                //calculate number of files
                total = Directory.GetFiles(source.FullName, @"*.*", SearchOption.AllDirectories).Length;
                totalItemsToCopyCallback?.Invoke(total);
            }

            int numdone = done;
            if (!testrun)
            {
                Directory.CreateDirectory(target.FullName);
            }

            // Copy each file into the new directory.
            foreach (FileInfo fi in source.GetFiles())
            {
                if (ignoredExtensions != null)
                {
                    bool skip = false;
                    foreach (string str in ignoredExtensions)
                    {
                        if (fi.Name.ToLower().EndsWith(str))
                        {
                            skip = true;
                            break;
                        }
                    }
                    if (skip)
                    {
                        numdone++;
                        fileCopiedCallback?.Invoke();
                        continue;
                    }
                }

                string displayName = fi.Name;
                //if (path.ToLower().EndsWith(".sfar") || path.ToLower().EndsWith(".tfc"))
                //{
                //    long length = new System.IO.FileInfo(fi.FullName).Length;
                //    displayName += " (" + ByteSize.FromBytes(length) + ")";
                //}
                var shouldCopy = aboutToCopyCallback?.Invoke(fi.FullName);
                if (aboutToCopyCallback == null || (shouldCopy.HasValue && shouldCopy.Value))
                {
                    try
                    {
                        if (!testrun)
                        {
                            var destPath = Path.Combine(target.FullName, fi.Name);
                            if (bigFileProgressCallback != null && fi.Length > 1024 * 1024 * 128)
                            {
                                //128MB or bigger
                                CopyTools.CopyFileWithProgress(fi.FullName, destPath, (bdone, btotal) => bigFileProgressCallback.Invoke(fi.FullName, bdone, btotal), exception => throw exception);
                            }
                            else
                            {
                                // No big copy
                                fi.CopyTo(destPath, true);
                            }

                            FileInfo dest = new FileInfo(destPath);
                            if (dest.IsReadOnly) dest.IsReadOnly = false;
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error(@"[AICORE] Error copying file: " + fi + @" -> " + Path.Combine(target.FullName, fi.Name) + @": " + e.Message);
                        throw e;
                    }
                }


                // Log.Information(@"Copying {0}\{1}", target.FullName, fi.Name);
                numdone++;
                fileCopiedCallback?.Invoke();
            }

            // Copy each subdirectory using recursion.
            foreach (DirectoryInfo diSourceSubDir in source.GetDirectories())
            {
                DirectoryInfo nextTargetSubDir = testrun ? null : target.CreateSubdirectory(diSourceSubDir.Name);
                numdone = CopyAll_ProgressBar(diSourceSubDir, nextTargetSubDir, totalItemsToCopyCallback, fileCopiedCallback, aboutToCopyCallback, total, numdone, null, testrun, bigFileProgressCallback);
            }
            return numdone;
        }

        public static void CopyFiles_ProgressBar(Dictionary<string, string> fileMapping, Action<string> fileCopiedCallback = null, bool testrun = false)
        {
            foreach (var singleMapping in fileMapping)
            {
                var source = singleMapping.Key;
                var dest = singleMapping.Value;
                if (!testrun)
                {
                    //Will attempt to create dir, prompt for admin if necessary (not sure how this will work in the wild)
                    Utilities.CreateDirectoryWithWritePermission(Directory.GetParent(dest).FullName);

                    if (File.Exists(dest))
                    {
                        FileAttributes attributes = File.GetAttributes(dest);

                        if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                        {
                            // Make the file RW
                            attributes = attributes & ~FileAttributes.ReadOnly;
                            File.SetAttributes(dest, attributes);
                        }
                    }
                    FileInfo si = new FileInfo(source);
                    if (si.IsReadOnly)
                    {
                        si.IsReadOnly = false; //remove flag. Some mod archives do this I guess.
                    }
                    File.Copy(source, dest, true);
                }
                else
                {
                    FileInfo f = new FileInfo(source); //get source info. this will throw exception if an error occurs
                }

                fileCopiedCallback?.Invoke(dest);
            }
        }
    }
}