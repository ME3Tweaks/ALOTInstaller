using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.Helpers.AppSettings;
using ME3ExplorerCore.Compression;
using Octokit;
using Serilog;

namespace ALOTInstallerCore
{
    /// <summary>
    /// Handles updates to MEM
    /// </summary>
    public class MEMUpdater
    {
        /// <summary>
        /// Soak gates for MEM updates on stable channel
        /// </summary>
        private static int[] SoakThresholds = { 25, 75, 150, 350, 650 };
        /// <summary>
        /// Highest supported version of MEM (that is not soak testing)
        /// </summary>
        public static int HighestSupportedMEMVersion { get; set; } = 999; //Default to very high number
        /// <summary>
        /// The date the soak test version of MEM was started
        /// </summary>
        public static DateTime SoakStartDate { get; set; }
        /// <summary>
        /// Version of MEM That is currently being targeted for soak testing. After the amount of days (as indexes) has passed for SoakThreshholds, this will effectively become the main version.
        /// </summary>
        public static int SoakTestingMEMVersion { get; set; }

        /// <summary>
        /// Checks for and updates mem if necessary
        /// </summary>
        public static void UpdateMEM(Action<long, long> downloadProgressChanged = null, Action<Exception> exceptionUpdating = null, Action<string> statusMessageUpdate = null)
        {
            int memVersion = 0;
            var mempath = Locations.MEMPath();
            var downloadMEM = !File.Exists(mempath);
            if (!downloadMEM)
            {
                // File exists
                memVersion = MEMIPCHandler.GetMemVersion();
            }

            try
            {
                Log.Information("[AICORE] Checking for updates to MassEffectModderNoGui. The local version is " + memVersion);
                if (Settings.BetaMode)
                {
                    Log.Information("[AICORE] Beta mode enabled, will include prerelease builds");
                }
                var client = new GitHubClient(new ProductHeaderValue("ALOTInstaller"));
                var releases = client.Repository.Release.GetAll("MassEffectModder", "MassEffectModder").Result;
                Log.Information("[AICORE] Fetched MEMNOGui releases from github...");
                Release latestReleaseWithApplicableAsset = null;
                if (releases.Any())
                {
                    //The release we want to check is always the latest, so [0]
                    foreach (Release r in releases)
                    {
                        if (!Settings.BetaMode && r.Prerelease)
                        {
                            // Beta only release
                            continue;
                        }
                        if (r.Assets.Count == 0)
                        {
                            // Release has no assets
                            continue;
                        }

                        int releaseNameInt = Convert.ToInt32(r.TagName);
                        if (releaseNameInt > memVersion && getApplicableAssetForPlatform(r) != null)
                        {
                            ReleaseAsset applicableAsset = getApplicableAssetForPlatform(r);
                            // This is an update...
                            if (Settings.BetaMode)
                            {
                                // Use this update
                                latestReleaseWithApplicableAsset = r;
                                break;
                            }

                            // Check if this is the soak testing build
                            if (releaseNameInt == SoakTestingMEMVersion)
                            {
                                var comparisonAge = SoakStartDate == default ? DateTime.Now - r.PublishedAt.Value : DateTime.Now - SoakStartDate;
                                int soakTestReleaseAge = (comparisonAge).Days;
                                if (soakTestReleaseAge >= SoakThresholds.Length)
                                {
                                    Log.Information("[AICORE] New MassEffectModderNoGui update is past soak period, accepting this release as an update");
                                    latestReleaseWithApplicableAsset = r;
                                    break;
                                }
                                int soakThreshold = SoakThresholds[soakTestReleaseAge];

                                //Soak gating
                                if (applicableAsset.DownloadCount > soakThreshold)
                                {
                                    Log.Information($"[AICORE] New MassEffectModderNoGui update is soak testing and has reached the daily soak threshold of {soakThreshold}. This update is not applicable to us today, threshold will expand tomorrow.");
                                    continue;
                                }
                                else
                                {
                                    Log.Information("[AICORE] New MassEffectModderNoGui update is available and soaking, this client will participate in this soak test.");
                                    latestReleaseWithApplicableAsset = r;
                                    break;
                                }
                            }
                            // Check if this build is approved for stable
                            if (!Settings.BetaMode && releaseNameInt > HighestSupportedMEMVersion)
                            {
                                Log.Information("[AICORE] New MassEffectModderNoGui update is available, but is not yet approved for stable channel: " + releaseNameInt);
                                continue;
                            }

                            if (releaseNameInt > memVersion)
                            {
                                Log.Information($"[AICORE] New MassEffectModderNoGui update is available: {releaseNameInt}");
                                latestReleaseWithApplicableAsset = r;
                                break;
                            }

                        }
                        else
                        {
                            Log.Information("[AICORE] Latest release that is available and has been approved for use is v" + releaseNameInt + " - no update available for us");
                            break;
                        }
                    }

                    //No local version, no latest, but we have asset available somehwere
                    if (memVersion == 0 && latestReleaseWithApplicableAsset == null)
                    {
                        Log.Information("[AICORE] MassEffectModderNoGui does not exist locally, and no applicable version can be found, force pulling latest from github");
                        latestReleaseWithApplicableAsset = releases.FirstOrDefault(x => getApplicableAssetForPlatform(x) != null);
                    }
                    else if (memVersion == 0 && latestReleaseWithApplicableAsset == null)
                    {
                        //No local version, and we have no server version
                        Log.Error("[AICORE] Cannot pull a copy of MassEffectModderNoGui from server, could not find one with assets. ALOTInstallerCore will not work properly without this!");
                    }
                    else if (memVersion == 0)
                    {
                        Log.Information("[AICORE] MassEffectModderNoGui does not exist locally. Pulling a copy from Github.");
                    }

                    if (latestReleaseWithApplicableAsset != null)
                    {
                        ReleaseAsset asset = getApplicableAssetForPlatform(latestReleaseWithApplicableAsset);
                        Log.Information("[AICORE] MassEffectModderNoGui update available: " + latestReleaseWithApplicableAsset.TagName);
                        //there's an update
                        var downloadClient = new WebClient();
                        downloadClient.Headers["Accept"] = "application/vnd.github.v3+json";
                        downloadClient.Headers["user-agent"] = "ALOTInstallerCore";
                        string downloadPath = Path.Combine(Locations.TempDirectory(), "MEM_Update" + Path.GetExtension(asset.BrowserDownloadUrl));
                        DownloadHelper.DownloadFile(new Uri(asset.BrowserDownloadUrl), downloadPath, (bytesReceived, totalBytes) =>
                        {
                            downloadProgressChanged?.Invoke(bytesReceived, totalBytes);

                        });
                        
                        // Handle unzip code here.
                        statusMessageUpdate?.Invoke("Extracting MassEffectModderNoGui");
                        var res = LZMA.ExtractSevenZipArchive(downloadPath, Locations.AppDataFolder(), true);
                    }
                    else
                    {
                        //up to date
                        Log.Information("[AICORE] No updates for MassEffectModderNoGui are available");
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error("[AICORE] An error occurred running MassEffectModderNoGui updater: " + e.Message);
                exceptionUpdating?.Invoke(e);
            }
        }

        private static ReleaseAsset getApplicableAssetForPlatform(Release r)
        {
            foreach (var a in r.Assets)
            {
                // Todo: Make Windows if
                if (a.Name.StartsWith("MassEffectModderNoGui-v")) return a;

#if LINUX

#elif MACOS

#endif
            }

            return null; //no asset for platform
        }

        private static void unzipMEMUpdate(object sender, AsyncCompletedEventArgs e)
        {

        }
    }
}
