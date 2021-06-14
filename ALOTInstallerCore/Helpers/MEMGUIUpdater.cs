using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ALOTInstallerCore.ModManager.ME3Tweaks;
using Octokit;
using Serilog;

namespace ALOTInstallerCore.Helpers
{
    public class MEMGUIUpdater
    {
        /// <summary>
        /// Versions 500 and above are LE only
        /// </summary>
        public static int MaxSupportedMEMVersion = 499;
#if WINDOWS
        private static ReleaseAsset getPlatformApplicableAsset(Release release) => release.Assets.FirstOrDefault(x => x.Name.StartsWith("MassEffectModder-v"));
#elif LINUX
        private static ReleaseAsset getPlatformApplicableAsset(Release release) => release.Assets.FirstOrDefault(x => x.Name.StartsWith("MassEffectModder-Linux-v"));
#elif MACOS
        private static ReleaseAsset getPlatformApplicableAsset(Release release) => release.Assets.FirstOrDefault(x => x.Name.StartsWith("MassEffectModder-macOS-v"));
#endif

        private static bool hasPlatformApplicableAsset(Release release) => getPlatformApplicableAsset(release) != null;


        /// <summary>
        /// Checks for updates and downloads Mass Effect Modder, if necessary. The calling application should throttle this call to once per session.
        /// </summary>
        /// <param name="setMessageCallback"></param>
        /// <param name="progressCallback"></param>
        public static async Task<bool?> UpdateMEMGUI(Action<string> setTitleCallback = null, Action<string> setMessageCallback = null, Action<long, long> progressCallback = null)
        {
            int fileVersion = 0;
            var memLocation = Locations.GetCachedExecutable(@"MassEffectModder", true);
            if (File.Exists(memLocation))
            {
                // Don't think this will work on Linux...
                var versInfo = FileVersionInfo.GetVersionInfo(memLocation);
                fileVersion = versInfo.FileMajorPart;

                Log.Information("[AICORE] Fetched MEMNOGui releases from github...");
                if (fileVersion >= 500)
                {
                    // Force downgrade
                    Log.Warning(@"The local MEMGui version is higher than the supported version. We are forcibly downgrading this client.");
                    fileVersion = 0;
                }
            }

            try
            {
                setMessageCallback?.Invoke("Checking for updates to Mass Effect Modder...");
                var client = new GitHubClient(new ProductHeaderValue("ALOTInstaller"));
                var releases = await client.Repository.Release.GetAll("MassEffectModder", "MassEffectModder");
                if (releases.Any())
                {
                    // Find the latest release that is greater than ours
                    Release latest = null;
                    int latestReleaseNum = 0;
                    foreach (var release in releases)
                    {
                        if (int.TryParse(release.TagName, out var version) && version <= MaxSupportedMEMVersion)
                        {
                            if (hasPlatformApplicableAsset(release) && (latest == null || version > latestReleaseNum))
                            {
                                latest = release;
                                latestReleaseNum = version;
                            }
                        }
                    }

                    if (latest == null || fileVersion >= latestReleaseNum)
                    {
                        return false; // No update
                    }

                    var asset = getPlatformApplicableAsset(latest);
                    if (asset != null)
                    {
                        var extension = Path.GetExtension(asset.BrowserDownloadUrl);
                        setTitleCallback?.Invoke("Updating Mass Effect Modder");
                        setMessageCallback?.Invoke($"Downloading Mass Effect Modder v{latestReleaseNum}...");

                        var downloadResult = await OnlineContent.DownloadToMemory(asset.BrowserDownloadUrl, progressCallback);
                        if (downloadResult.errorMessage != null)
                        {
                            return null; //Error
                        }
                        else
                        {
                            string downloadLocation = Path.Combine(Locations.TempDirectory(), "MEMGUI_Update" + extension);
                            downloadResult.result.WriteToFile(downloadLocation);
                            MEMIPCHandler.ExtractArchiveToDirectory(downloadLocation, Locations.GetCachedExecutablesDirectory());
                            return true;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error("[AICORE] Error checking for MEM GUI update: " + e.Message);
            }

            return null;
        }
    }
}
