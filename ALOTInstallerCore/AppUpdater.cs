using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.Helpers.AppSettings;
using ALOTInstallerCore.ModManager.ME3Tweaks;
using AuthenticodeExaminer;
using ME3ExplorerCore.Compression;
using Octokit;
using Serilog;

namespace ALOTInstallerCore
{

    public class AppUpdateInteropPackage
    {
        public string GithubOwner { get; set; }
        public string GithubReponame { get; set; }
        public string UpdateAssetPrefix { get; set; }
        public string UpdateFilenameInArchive { get; set; }
        /// <summary>
        /// Invoked when an update prompt should be shown with title, description, and two buttons. Returns true if accepted, false if declined
        /// </summary>
        public Func<string, string, string, string, bool> ShowUpdatePromptCallback { get; set; }
        /// <summary>
        /// Invoked when a dialog that has a piece of text (title, description, can be canceled) should be updated
        /// </summary>
        public Action<string, string, bool> ShowUpdateProgressDialogCallback { get; set; } // title, message, cancancel
        /// <summary>
        /// Invoked when the update dialog box's text should be updated
        /// </summary>
        public Action<string> SetUpdateDialogTextCallback { get; set; }
        /// <summary>
        /// Invoked when there is progress to be shown.
        /// </summary>
        public Action<long, long> ProgressCallback { get; set; }
        /// <summary>
        /// Invoked when progress should be set as indeterminate
        /// </summary>
        public Action ProgressIndeterminateCallback { get; set; }
        /// <summary>
        /// Invoked when a message needs to be shown
        /// </summary>
        public Action<string, string> ShowMessageCallback { get; set; }
        /// <summary>
        /// Invoked if the latest release is a beta release
        /// </summary>
        public Action NotifyBetaAvailable { get; set; }
        /// <summary>
        /// Invoked when the download has completed and the cancel button (if any) should be hidden
        /// </summary>
        public Action DownloadCompleted { get; set; } // When we should hide cancel button
        /// <summary>
        /// Invoked when the update is canceled.
        /// </summary>
        public CancellationTokenSource cancellationTokenSource { get; set; }
        /// <summary>
        /// Request header that is required for sending to GitHub. Can be any non-empty string.
        /// </summary>
        public string RequestHeader { get; set; }
        /// <summary>
        /// Text that is used for logging
        /// </summary>
        public string ApplicationName { get; set; }
        /// <summary>
        /// The amount of applicable releases that are newer than ours that can exist before we force the client to upgrade without prompt. Set to zero to disable this feature. This can be used to prevent people from using very outdated downloads
        /// </summary>
        public int ForcedUpgradeMaxReleaseAge { get; set; }
    }

    public class AppUpdater
    {
        /// <summary>
        /// Checks for application updates. The hosting app must implement the reboot and swap logic
        /// </summary>
        public static async void PerformGithubAppUpdateCheck(AppUpdateInteropPackage interopPackage)
        {
#if APPUPDATESUPPORT
            Log.Information($"[AICORE] Checking for application updates from github. Mode: {(Settings.BetaMode ? "Beta" : "Stable")}");
            var currentAppVersionInfo = Utilities.GetAppVersion();
            var client = new GitHubClient(new ProductHeaderValue(interopPackage.RequestHeader));
            try
            {
                int myReleaseAge = 0;
                var releases = client.Repository.Release.GetAll(interopPackage.GithubOwner, interopPackage.GithubReponame).Result;
                if (releases.Count > 0)
                {
                    Log.Information("[AICORE] Fetched application releases from github");

                    //The release we want to check is always the latest
                    Release latest = null;
                    Version latestVer = new Version("0.0.0.0");
                    bool betaAvailableButOnStable = false;
                    foreach (Release onlineRelease in releases)
                    {
                        Version onlineReleaseVersion = new Version(onlineRelease.TagName);

                        if (onlineReleaseVersion <= currentAppVersionInfo && ((Settings.BetaMode && onlineRelease.Prerelease) || !onlineRelease.Prerelease))
                        {
                            Log.Information($@"[AICORE] The version of {interopPackage.ApplicationName} that we have is higher than/equal to the latest release from github, no updates available. Latest applicable github release is {onlineReleaseVersion}");
                            break;
                        }

                        // Check if applicable
                        if (onlineRelease.Assets.All(x => !x.Name.StartsWith(interopPackage.UpdateAssetPrefix)))
                        {
                            continue; //This release is not applicable to us
                        }

                        if (!Settings.BetaMode && onlineRelease.Prerelease && currentAppVersionInfo.Build < onlineReleaseVersion.Build)
                        {
                            betaAvailableButOnStable = true;
                            continue;
                        }

                        // Checked values (M): M.X.M.X
                        if (currentAppVersionInfo.Major == onlineReleaseVersion.Major && currentAppVersionInfo.Build < onlineReleaseVersion.Build)
                        {
                            myReleaseAge++;
                        }

                        if (onlineReleaseVersion > latestVer)
                        {
                            latest = onlineRelease;
                            latestVer = onlineReleaseVersion;
                        }
                    }

                    if (latest != null)
                    {
                        Log.Information("[AICORE] Latest available applicable update: " + latest.TagName);
                        Version releaseName = new Version(latest.TagName);
                        if (currentAppVersionInfo < releaseName)
                        {
                            bool upgrade = false;
                            bool canCancel = true;
                            Log.Information("[AICORE] Latest release is applicable to us.");
                            if (interopPackage.ForcedUpgradeMaxReleaseAge > 0 && myReleaseAge > interopPackage.ForcedUpgradeMaxReleaseAge)
                            {
                                Log.Warning("[AICORE] This is an old release. We are force upgrading the application.");
                                upgrade = true;
                                canCancel = false;
                            }
                            else
                            {
                                string uiVersionInfo = "";
                                if (latest.Prerelease)
                                {
                                    uiVersionInfo += " This update is a beta build. You are receiving this update because you have opted into Beta Mode in the settings.";
                                }
                                int daysAgo = (DateTime.Now - latest.PublishedAt.Value).Days;
                                string ageStr = "";
                                if (daysAgo == 1)
                                {
                                    ageStr = "1 day ago";
                                }
                                else if (daysAgo == 0)
                                {
                                    ageStr = "Today";
                                }
                                else
                                {
                                    ageStr = $"{daysAgo} days ago";
                                }

                                uiVersionInfo += $"\nReleased: {ageStr}";
                                string title = $"{interopPackage.ApplicationName} {releaseName} is available";

                                var message = latest.Body;
                                var msgLines = latest.Body.Split('\n');
                                message = string.Join('\n', msgLines.Where(x => !x.StartsWith("hash: "))).Trim();
                                upgrade = interopPackage.ShowUpdatePromptCallback != null && interopPackage.ShowUpdatePromptCallback.Invoke(title, $"You are currently using version {currentAppVersionInfo}.{uiVersionInfo}\n\n{message}", "Update", "Later");
                            }
                            if (upgrade)
                            {
                                Log.Information("[AICORE] Downloading update for application");
                                //there's an update
                                string message = $"Downloading update for {interopPackage.ApplicationName}...";
                                if (!canCancel)
                                {
                                    if (!Settings.BetaMode)
                                    {
                                        message = $"This copy of {interopPackage.ApplicationName} is outdated and must be updated.";
                                    }
                                }

                                interopPackage.ShowUpdateProgressDialogCallback?.Invoke($"Updating {interopPackage.ApplicationName}", message, canCancel);
                                // First here should be OK since we checked it above...

                                // PATCH UPDATE
                                if (attemptPatchUpdate(latest, interopPackage.ProgressCallback, interopPackage.ProgressIndeterminateCallback, interopPackage.SetUpdateDialogTextCallback, interopPackage.DownloadCompleted, interopPackage.cancellationTokenSource))
                                {
                                    // Patch update succeeded. The code below is the default update
                                    return;
                                }


                                var asset = latest.Assets.First(x => x.Name.StartsWith(interopPackage.UpdateAssetPrefix));
                                var downloadResult = await OnlineContent.DownloadToMemory(asset.BrowserDownloadUrl, interopPackage.ProgressCallback,
                                    logDownload: true, cancellationTokenSource: interopPackage.cancellationTokenSource);

                                // DEBUG ONLY
                                //(MemoryStream result, string errorMessage) downloadResult = (new MemoryStream(File.ReadAllBytes(@"C:\Users\mgame\source\repos\ME2Randomizer\ME2Randomizer\Deployment\Releases\ME2Randomizer_0.9.1.0.7z")), null);
                                if (downloadResult.result == null & downloadResult.errorMessage == null)
                                {
                                    // Canceled
                                    Log.Warning("[AICORE] The download was canceled.");
                                    return;
                                }

                                if (downloadResult.errorMessage != null)
                                {
                                    // There was an error downloading the update.
                                    interopPackage.ShowMessageCallback?.Invoke("Update failed", $"There was an error downloading the update: {downloadResult.errorMessage}");
                                    return;
                                }

                                // Comment out if you are debug testing new use of update check
                                if (downloadResult.result.Length != asset.Size)
                                {
                                    // The download is wrong size
                                    Log.Error($@"[AICORE] The downloaded file was incomplete. Downloaded size: {downloadResult.result.Length}, expected size: {asset.Size}");
                                    interopPackage.ShowMessageCallback?.Invoke("Update failed", "The downloaded file was incomplete.");
                                    return;
                                }

                                // Download is OK
                                interopPackage.DownloadCompleted?.Invoke();
                                interopPackage.ProgressIndeterminateCallback?.Invoke();
                                interopPackage.ShowUpdateProgressDialogCallback?.Invoke($"Updating {interopPackage.ApplicationName}", "Preparing to apply update", false);
                                var updateFailedResult = extractUpdate(downloadResult.result, Path.GetFileName(asset.Name), interopPackage.UpdateFilenameInArchive, interopPackage.SetUpdateDialogTextCallback);
                                if (updateFailedResult != null)
                                {
                                    // The download is wrong size
                                    Log.Error($@"[AICORE] Applying the update failed: {updateFailedResult}");
                                    interopPackage.ShowMessageCallback?.Invoke("Update failed", $"Applying the update failed: {updateFailedResult}");
                                    return;
                                }
                            }
                            else
                            {
                                Log.Warning("[AICORE] Application update was declined by user");
                                interopPackage.ShowMessageCallback?.Invoke("Old versions are not supported", $"Outdated versions of {interopPackage.ApplicationName} Installer are not supported and may not work in the future.");
                            }
                        }
                        else
                        {
                            //up to date
                            Log.Information("[AICORE] Application is up to date.");
                            if (betaAvailableButOnStable && Settings.LastBetaAdvert < (DateTime.UtcNow.AddDays(-3)))
                            {
                                interopPackage.NotifyBetaAvailable?.Invoke();
                                Settings.LastBetaAdvert = DateTime.Now;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error("[AICORE] Error checking for update: " + e);
            }
#endif
        }

#if APPUPDATESUPPORT

        private static bool attemptPatchUpdate(Release latestRelease, Action<long, long> progressCallback, Action progressIndeterminateCallback,
            Action<string> setUpdateDialogTextCallback,
            Action downloadCompletedCallback,
            CancellationTokenSource cancellationTokenSource)
        {
            var hashLine = latestRelease.Body.Split('\n').FirstOrDefault(x => x.StartsWith("hash: "));

            if (hashLine != null)
            {
                var destMd5 = hashLine.Substring(5).Trim();
                if (destMd5.Length != 32)
                {
                    Log.Warning(
                        $"[AICORE] Release {latestRelease.TagName} has invalid hash length in body, cannot use patch update strategy");
                    return false; //no hash
                }

                // Mapping of MD5 patches to destination. Value is a list of mirrors we can use, preferring github first. AI only uses Github
                Dictionary<string, List<(string downloadhash, string downloadLink, string timetamp)>> patchMappingSourceMd5ToLinks
 = new Dictionary<string, List<(string downloadhash, string downloadLink, string timetamp)>>();

                var localExecutableHash = Utilities.CalculateMD5(Utilities.GetExecutablePath());

                // Find applicable patch
                foreach (var asset in latestRelease.Assets.Where(x => x.Name.StartsWith("upd-")))
                {
                    var updateinfo = asset.Name.Split(@"-");
                    if (updateinfo.Length >= 4)
                    {
                        var sourceHash = updateinfo[1];
                        var destHash = updateinfo[2];
                        var downloadHash = updateinfo[3];
                        var timestamp = updateinfo.Length > 4 ? updateinfo[4] : @"0";

                        if (localExecutableHash == sourceHash && destHash == destMd5)
                        {

                            if (!patchMappingSourceMd5ToLinks.TryGetValue(sourceHash, out var patchMappingList))
                            {
                                // ^ Don't bother adding items that will never be useful ^
                                patchMappingList =
                                    new List<(string downloadhash, string downloadLink, string timetamp)>();
                                patchMappingSourceMd5ToLinks[sourceHash] = patchMappingList;
                            }

                            // Insert at front.
                            patchMappingList.Insert(0, (downloadHash, asset.BrowserDownloadUrl, timestamp));
                        }
                    }
                }

                if (patchMappingSourceMd5ToLinks.TryGetValue(localExecutableHash, out var downloadInfoMirrors))
                {
                    foreach (var downloadInfo in downloadInfoMirrors)
                    {
                        Log.Information($@"[AICORE] Downloading patch file {downloadInfo.downloadLink}");
                        var patchUpdate = OnlineContent.DownloadToMemory(downloadInfo.downloadLink, progressCallback,
                            downloadInfo.downloadhash, cancellationTokenSource: cancellationTokenSource).Result;
                        if (patchUpdate.errorMessage != null)
                        {
                            Log.Warning($@"[AICORE] Patch update download failed: {patchUpdate.errorMessage}");
                            return false;
                        }
                        downloadCompletedCallback?.Invoke();
                        Log.Information(@"[AICORE] Download OK: Building new executable");
                        setUpdateDialogTextCallback?.Invoke("Building new executable");
                        progressIndeterminateCallback?.Invoke();
                        var newExecutable = BuildUpdateFromPatch(patchUpdate.result, destMd5, downloadInfo.timetamp);
                        if (newExecutable != null)
                        {
                            var validationResult = ValidateUpdate(newExecutable, setUpdateDialogTextCallback);
                            return validationResult == null;
                        }
                    }
                }
                else
                {
                    Log.Warning($"[AICORE] No patch is applicable to bridge our current hash {localExecutableHash} to the destination hash {destMd5}");
                }
            }
            else
            {
                Log.Warning($"[AICORE] Release {latestRelease.TagName} is missing hash in body, cannot use patch update strategy");
                return false; //no hash
            }
            return false;
        }

        /// <summary>
        /// Builds the new update from a patch update
        /// </summary>
        /// <param name="patchStream"></param>
        /// <param name="expectedFinalHash"></param>
        /// <returns>The destination update file, or null if it failed</returns>
        private static string BuildUpdateFromPatch(MemoryStream patchStream, string expectedFinalHash, string fileTimestamp)
        {
            // patch stream is LZMA'd
            try
            {
                patchStream = new MemoryStream(LZMA.DecompressLZMAFile(patchStream.ToArray()));
                using var currentBuildStream = File.OpenRead(Utilities.GetExecutablePath());
                //using var currentBuildStream = File.OpenRead(@"C:\Users\Mgamerz\source\repos\ME3Tweaks\MassEffectModManager\MassEffectModManagerCore\Deployment\Staging\ME3TweaksModManager\ME3TweaksModManager.exe");

                MemoryStream outStream = new MemoryStream();
                JPatch.ApplyJPatch(currentBuildStream, patchStream, outStream);
                var calculatedHash = Utilities.CalculateMD5(outStream);
                if (calculatedHash == expectedFinalHash)
                {
                    Log.Information(@"Patch application successful: Writing new executable to disk");
                    var outDirectory = Directory.CreateDirectory(Path.Combine(Locations.TempDirectory(), @"update"))
                        .FullName;
                    var updateFile = Path.Combine(outDirectory, $"{Utilities.GetHostingProcessname()}.exe");
                    outStream.WriteToFile(updateFile);

                    if (long.TryParse(fileTimestamp, out var buildDateLong) && buildDateLong > 0)
                    {
                        Log.Information(@"Updating timestamp on new executable to the original value");
                        try
                        {
                            File.SetLastWriteTimeUtc(updateFile, new DateTime(buildDateLong));
                        }
                        catch (Exception ex)
                        {
                            Log.Error($@"Could not set executable date: {ex.Message}");
                        }
                    }
                    Log.Information(@"New executable patching complete");
                    return updateFile;
                }
                else
                {
                    Log.Error($@"Patch application failed. The resulting hash was wrong. Expected {expectedFinalHash}, got {calculatedHash}");
                }
            }
            catch (Exception e)
            {
                Log.Error($@"Error applying patch update: {e.Message}");
            }

            return null;
        }


        private static string extractUpdate(MemoryStream ms, string assetFilename, string updateFileName, Action<string> setDialogText = null)
        {
            var outDir = Path.Combine(Locations.TempDirectory(), Path.GetFileNameWithoutExtension(assetFilename));
            var archiveFile = Path.Combine(Locations.TempDirectory(), assetFilename);
            ms.WriteToFile(archiveFile);
            setDialogText?.Invoke("Extracting update");
            if (LZMA.ExtractSevenZipArchive(archiveFile, outDir))
            {
                // Extraction complete
                var fileToValidate = Directory.GetFiles(outDir, updateFileName, SearchOption.AllDirectories).FirstOrDefault();
                if (fileToValidate != null)
                {
                    return ValidateUpdate(fileToValidate, setDialogText);
                }
                else
                {
                    // Could not find update in archive!
                    return $"Could not find {updateFileName} in the downloaded archive.";
                }
            }

            return "The update archive failed to extract.";
        }

        private static string ValidateUpdate(string fileToValidate, Action<string> setDialogText = null)
        {
#if WINDOWS
            setDialogText?.Invoke("Verifying update");
            // Signature check
            var authenticodeInspector = new FileInspector(fileToValidate);
            var validationResult = authenticodeInspector.Validate();
            if (validationResult != SignatureCheckResult.Valid)
            {
                Log.Error($@"[AICORE] The update file does not have a valid signature: {validationResult}. Update will be aborted.");
                return "The update file has an invalid signature. See the application log for more details.";
            }
#endif

            // Validated
            setDialogText?.Invoke("Applying update");
            applyUpdate(fileToValidate, setDialogText);
            return null;
        }

        private static void applyUpdate(string newExecutable, Action<string> setDialogText = null)
        {
            string args = @"--update-boot";
            Log.Information($@"[AICORE] Booting new version of the application to perform first time extraction: {newExecutable} {args}");

            Process process = new Process();
            // Stop the process from opening a new window
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

            // Setup executable and parameters
            process.StartInfo.FileName = newExecutable;
            process.StartInfo.Arguments = args;
            process.Start();
            process.WaitForExit();

            setDialogText?.Invoke($"Restarting application");
            Thread.Sleep(2000);
            args = $"--update-dest-path \"{Utilities.GetExecutablePath()}\"";
            Log.Information($@"[AICORE] Running proxy update: {newExecutable} {args}");

            process = new Process();
            // Stop the process from opening a new window
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

            // Setup executable and parameters
            process.StartInfo.FileName = newExecutable;
            process.StartInfo.Arguments = args;
            process.Start();
            Log.Information(@"[AICORE] Stopping application to allow executable swap");
            Log.CloseAndFlush();

            // If this throws exception and the app dies... oh well, I guess?
            Environment.Exit(0);
        }
#endif
    }
}
