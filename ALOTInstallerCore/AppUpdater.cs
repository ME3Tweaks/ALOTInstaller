using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.ModManager.ME3Tweaks;
using AuthenticodeExaminer;
using Microsoft.Win32;
using Octokit;
using Serilog;

namespace ALOTInstallerCore
{
    public class AppUpdater
    {
        /// <summary>
        /// Checks for application updates. The hosting app must implement the reboot and swap logic.
        /// </summary>
        /// <param name="owner"></param>
        /// <param name="repo"></param>
        /// <param name="assetPrefix"></param>
        /// <param name="updateFilenameInArchive"></param>
        /// <param name="showUpdatePromptCallback"></param>
        /// <param name="showUpdateProgressDialogCallback"></param>
        /// <param name="setUpdateDialogTextCallback"></param>
        /// <param name="progressCallback"></param>
        /// <param name="progressIndeterminateCallback"></param>
        /// <param name="showMessageCallback"></param>
        /// <param name="notifyBetaAvailable"></param>
        public static async void PerformGithubAppUpdateCheck(string owner, string repo, string assetPrefix, string updateFilenameInArchive,
            Func<string, string, string, string, bool> showUpdatePromptCallback,
            Action<string, string, bool> showUpdateProgressDialogCallback, // title, message, cancancel
            Action<string> setUpdateDialogTextCallback,
            Action<long, long> progressCallback,
            Action progressIndeterminateCallback,
            Action<string, string> showMessageCallback,
            Action notifyBetaAvailable,
            CancellationTokenSource cancellationTokenSource)
        {
#if APPUPDATESUPPORT
            Log.Information("Checking for application updates from gitub");
            var currentAppVersionInfo = System.Reflection.Assembly.GetEntryAssembly().GetName().Version;
            var client = new GitHubClient(new ProductHeaderValue($"{Utilities.GetAppPrefixedName()}Installer"));
            try
            {
                int myReleaseAge = 0;
                var releases = client.Repository.Release.GetAll(owner, repo).Result;
                if (releases.Count > 0)
                {
                    Log.Information("Fetched application releases from github");

                    //The release we want to check is always the latest
                    Release latest = null;
                    Version latestVer = new Version("0.0.0.0");
                    bool betaAvailableButOnStable = false;
                    foreach (Release onlineRelease in releases)
                    {
                        Version onlineReleaseVersion = new Version(onlineRelease.TagName);

                        // Check if applicable
                        if (onlineRelease.Assets.Any(x => !x.Name.StartsWith(assetPrefix)))
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
                        Log.Information("Latest available applicable update: " + latest.TagName);
                        Version releaseName = new Version(latest.TagName);
                        if (currentAppVersionInfo < releaseName)
                        {
                            bool upgrade = false;
                            bool canCancel = true;
                            Log.Information("Latest release is applicable to us.");
                            if (myReleaseAge > 5)
                            {
                                Log.Warning("This is an old release. We are force upgrading this client.");
                                upgrade = true;
                                canCancel = false;
                            }
                            else
                            {
                                string uiVersionInfo = "";
                                if (latest.Prerelease)
                                {
                                    uiVersionInfo += " This is a beta build. You are receiving this update because you have opted into Beta Mode in settings.";
                                }
                                int daysAgo = (DateTime.Now - latest.PublishedAt.Value).Days;
                                string ageStr = "";
                                if (daysAgo == 1)
                                {
                                    ageStr = "1 day ago";
                                }
                                else if (daysAgo == 0)
                                {
                                    ageStr = "today";
                                }
                                else
                                {
                                    ageStr = $"{daysAgo} days ago";
                                }

                                uiVersionInfo += $"\nReleased {ageStr}";
                                string title = $"{Utilities.GetAppPrefixedName()} Installer {releaseName} is available";

                                upgrade = showUpdatePromptCallback != null && showUpdatePromptCallback.Invoke(title, $"You are currently using version {currentAppVersionInfo}.{uiVersionInfo}\nChangelog:\n\n{latest.Body}", "Update", "Later");
                            }
                            if (upgrade)
                            {
                                Log.Information("Downloading update for application");
                                //there's an update
                                string message = $"Downloading update for {Utilities.GetAppPrefixedName()} Installer...";
                                if (!canCancel)
                                {
                                    if (!Settings.BetaMode)
                                    {
                                        message = $"This copy of {Utilities.GetAppPrefixedName()} Installer is outdated and must be updated.";
                                    }
                                }

                                showUpdateProgressDialogCallback?.Invoke($"Updating {Utilities.GetAppPrefixedName()} Installer", message, canCancel);
                                // First here should be OK since we checked it above...
                                var asset = latest.Assets.First(x => x.Name.StartsWith(assetPrefix));
                                var downloadResult = await OnlineContent.DownloadToMemory(asset.BrowserDownloadUrl, progressCallback,
                                    logDownload: true, cancellationTokenSource: cancellationTokenSource);
                                if (downloadResult.result == null & downloadResult.errorMessage == null)
                                {
                                    // Canceled
                                    Log.Warning("The download was canceled.");
                                    return;
                                }
                                if (downloadResult.errorMessage != null)
                                {
                                    // There was an error downloading the update.
                                    showMessageCallback?.Invoke("Update failed", $"There was an error downloading the update: {downloadResult.errorMessage}");
                                    return;

                                }
                                if (downloadResult.result.Length != asset.Size)
                                {
                                    // The download is wrong size
                                    showMessageCallback?.Invoke("Update failed", "The downloaded file was incomplete.");
                                    return;
                                }
                                // Download is OK

                                progressIndeterminateCallback?.Invoke();
                                showUpdateProgressDialogCallback?.Invoke($"Updating {Utilities.GetAppPrefixedName()} Installer", "Preparing to apply update", false);
                                var updateFailedResult = extractUpdate(downloadResult.result, Path.GetFileName(asset.Name), updateFilenameInArchive, setUpdateDialogTextCallback);
                                if (updateFailedResult != null)
                                {
                                    // The download is wrong size
                                    showMessageCallback?.Invoke("Update failed", $"Applying the update failed: {updateFailedResult}");
                                    return;
                                }
                            }
                            else
                            {
                                Log.Warning("Application update was declined by user");
                                showMessageCallback?.Invoke("Old versions are not supported", $"Outdated versions of {Utilities.GetAppPrefixedName()} Installer are not supported and may stop working when online components, such as the installation manifest, are updated.");
                            }
                        }
                        else
                        {
                            //up to date
                            Log.Information("Application is up to date.");

                            if (betaAvailableButOnStable && Settings.LastBetaAdvert < (DateTimeOffset.UtcNow.AddDays(-3)))
                            {
                                notifyBetaAvailable?.Invoke();
                                Settings.LastBetaAdvert = DateTime.Now;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error("Error checking for update: " + e);
            }
#endif
        }

        private static string extractUpdate(MemoryStream ms, string assetFilename, string updateFileName, Action<string> setDialogText = null)
        {
            var outDir = Path.Combine(Locations.TempDirectory(), Path.GetFileNameWithoutExtension(assetFilename));
            var archiveFile = Path.Combine(Locations.TempDirectory(), assetFilename);
            ms.WriteToFile(archiveFile);
            if (SevenZipHelper.LZMA.ExtractSevenZipArchive(archiveFile, outDir))
            {
                // Extraction complete
#if WINDOWS
                setDialogText?.Invoke("Verifying update");
#endif
                var fileToValidate = Directory.GetFiles(outDir, updateFileName, SearchOption.AllDirectories).FirstOrDefault();
                if (fileToValidate != null)
                {
#if WINDOWS
                    // Signature check
                    var authenticodeInspector = new FileInspector(fileToValidate);
                    var validationResult = authenticodeInspector.Validate();
                    if (validationResult != SignatureCheckResult.Valid)
                    {
                        Log.Error($@"The update file does not have a valid signature: {validationResult}. Update will be aborted.");
                        return "The update file has an invalid signature. See the application log for more details.";
                    }
#endif

                    // Validated
                    setDialogText?.Invoke("Applying update");
                    applyUpdate(fileToValidate, setDialogText);
                    return null;
                }
                else
                {
                    // Could not find update in archive!
                    return $"Could not find {updateFileName} in the downloaded archive.";
                }

            }

            return "The update archive failed to extract.";
        }

        private static void applyUpdate(string newExecutable, Action<string> setDialogText = null)
        {
            string args = @"--update-boot";
            Log.Information($@"Booting new version of the installer to perform first time extraction: {newExecutable} {args}");

            Process process = new Process();
            // Stop the process from opening a new window
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

            // Setup executable and parameters
            process.StartInfo.FileName = newExecutable;
            process.StartInfo.Arguments = args;
            process.Start();
            process.WaitForExit();
            setDialogText?.Invoke($"Restarting {Utilities.GetAppPrefixedName()} Installer");
            Thread.Sleep(2000);
            args = $"--update-dest-path \"{System.Reflection.Assembly.GetExecutingAssembly().Location}\"";
            Log.Information($@"Running proxy update: {newExecutable} {args}");

            process = new Process();
            // Stop the process from opening a new window
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

            // Setup executable and parameters
            process.StartInfo.FileName = newExecutable;
            process.StartInfo.Arguments = args;
            process.Start();
            Log.Information(@"Stopping installer to allow executable swap");
            Log.CloseAndFlush();

            // If this throws exception and the app dies... oh well, I guess?
            Environment.Exit(0);
        }
    }
}
