using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.Helpers.AppSettings;
using ALOTInstallerCore.ModManager.Services;
using Newtonsoft.Json;
using Serilog;
namespace ALOTInstallerCore.ModManager.ME3Tweaks
{
    //Localizable(false) //Leave this here for localizer tool!
    public partial class OnlineContent
    {
        private const string ThirdPartyIdentificationServiceURL = "https://me3tweaks.com/modmanager/services/thirdpartyidentificationservice?highprioritysupport=true&allgames=true";
        private const string StaticFilesBaseURL_Github = "https://raw.githubusercontent.com/ME3Tweaks/ME3TweaksModManager/master/MassEffectModManagerCore/staticfiles/";
        private const string StaticFilesBaseURL_ME3Tweaks = "https://me3tweaks.com/modmanager/tools/staticfiles/";
        private const string BasegameFileIdentificationServiceURL = "https://me3tweaks.com/modmanager/services/basegamefileidentificationservice";
        private const string BasegameFileIdentificationServiceBackupURL = "https://raw.githubusercontent.com/ME3Tweaks/ME3TweaksModManager/master/MassEffectModManagerCore/staticfiles/basegamefileidentificationservice.json";

        /// <summary>
        /// List of static files endpoints in order of preference
        /// </summary>
        public static string[] StaticFilesBaseEndpoints =
        {
            StaticFilesBaseURL_Github,
            StaticFilesBaseURL_ME3Tweaks
        };

        /// <summary>
        /// Checks if we can perform an online content fetch. This value is updated when manually checking for content updates, and on automatic 1-day intervals (if no previous manual check has occurred)
        /// </summary>
        /// <returns></returns>
        internal static bool CanFetchContentThrottleCheck()
        {
            var lastContentCheck = Settings.LastContentCheck;
            var timeNow = DateTime.Now;
            return (timeNow - lastContentCheck).TotalDays > 1;
        }

        public static Dictionary<string, CaseInsensitiveDictionary<List<BasegameFileIdentificationService.BasegameCloudDBFile>>> FetchBasegameFileIdentificationServiceManifest(bool overrideThrottling = false)
        {
            Log.Information(@"[AICORE] Fetching basegame file identification manifest");

            //read cached first.
            string cached = null;
            if (File.Exists(Locations.GetBasegameIdentificationCacheFile()))
            {
                try
                {
                    cached = File.ReadAllText(Locations.GetBasegameIdentificationCacheFile());
                }
                catch (Exception e)
                {
                    var attachments = new List<CoreCrashes.ErrorAttachmentLog>();
                    string log = LogCollector.CollectLatestLog(true);
                    if (log != null && log.Length < 1024 * 1024 * 7)
                    {
                        attachments.Add(CoreCrashes.ErrorAttachmentLog.AttachmentWithText(log, @"applog.txt"));
                    }
                    CoreCrashes.TrackError3?.Invoke(e, new Dictionary<string, string>()
                    {
                        {@"Error type", @"Error reading cached online content" },
                        {@"Service", @"Basegame File Identification Service" },
                        {@"Message", e.Message }
                    }, attachments.ToArray());
                }
            }


            if (!File.Exists(Locations.GetBasegameIdentificationCacheFile()) || overrideThrottling || OnlineContent.CanFetchContentThrottleCheck())
            {
                var urls = new[] { BasegameFileIdentificationServiceURL, BasegameFileIdentificationServiceBackupURL };
                foreach (var staticurl in urls)
                {
                    Uri myUri = new Uri(staticurl);
                    string host = myUri.Host;
                    try
                    {
                        //using var wc = new ShortTimeoutWebClient();

                        string json = HttpClientDownloadWithProgress.DownloadStringAwareOfEncoding(staticurl);
                        File.WriteAllText(Locations.GetBasegameIdentificationCacheFile(), json);
                        return JsonConvert.DeserializeObject<Dictionary<string, CaseInsensitiveDictionary<List<BasegameFileIdentificationService.BasegameCloudDBFile>>>>(json);
                    }
                    catch (Exception e)
                    {
                        //Unable to fetch latest help.
                        Log.Error($"[AICORE] Error fetching online basegame file identification service from endpoint {host}: {e.Message}");
                    }
                }

                if (cached == null)
                {
                    Log.Error("[AICORE] Unable to load basegame file identification service and local file doesn't exist. Returning a blank copy.");
                    Dictionary<string, CaseInsensitiveDictionary<List<BasegameFileIdentificationService.BasegameCloudDBFile>>> d = new Dictionary<string, CaseInsensitiveDictionary<List<BasegameFileIdentificationService.BasegameCloudDBFile>>>
                    {
                        ["ME1"] = new CaseInsensitiveDictionary<List<BasegameFileIdentificationService.BasegameCloudDBFile>>(),
                        ["ME2"] = new CaseInsensitiveDictionary<List<BasegameFileIdentificationService.BasegameCloudDBFile>>(),
                        ["ME3"] = new CaseInsensitiveDictionary<List<BasegameFileIdentificationService.BasegameCloudDBFile>>()
                    };
                    return d;
                }
            }
            Log.Information("[AICORE] Using cached BGFIS instead");

            try
            {
                return JsonConvert.DeserializeObject<Dictionary<string, CaseInsensitiveDictionary<List<BasegameFileIdentificationService.BasegameCloudDBFile>>>>(cached);
            }
            catch (Exception e)
            {
                Log.Error("[AICORE] Could not parse cached basegame file identification service file. Returning blank BFIS data instead. Reason: " + e.Message);
                return new Dictionary<string, CaseInsensitiveDictionary<List<BasegameFileIdentificationService.BasegameCloudDBFile>>>
                {
                    ["ME1"] = new CaseInsensitiveDictionary<List<BasegameFileIdentificationService.BasegameCloudDBFile>>(),
                    ["ME2"] = new CaseInsensitiveDictionary<List<BasegameFileIdentificationService.BasegameCloudDBFile>>(),
                    ["ME3"] = new CaseInsensitiveDictionary<List<BasegameFileIdentificationService.BasegameCloudDBFile>>()
                };
            }
        }

        public static Dictionary<string, CaseInsensitiveDictionary<ThirdPartyServices.ThirdPartyModInfo>> FetchThirdPartyIdentificationManifest(bool overrideThrottling = false)
        {
            Log.Information(@"[AICORE] Fetching Third Party Mod Identification Service (TPMI) manifest");

            string cached = null;
            if (File.Exists(Locations.GetThirdPartyIdentificationCachedFile()))
            {
                try
                {
                    cached = File.ReadAllText(Locations.GetThirdPartyIdentificationCachedFile());
                }
                catch (Exception e)
                {
                    var attachments = new List<CoreCrashes.ErrorAttachmentLog>();
                    string log = LogCollector.CollectLatestLog(true);
                    if (log != null && log.Length < 1024 * 1024 * 7) // 7MB
                    {
                        attachments.Add(CoreCrashes.ErrorAttachmentLog.AttachmentWithText(log, "applog.txt"));
                    }
                    CoreCrashes.TrackError3?.Invoke(e, new Dictionary<string, string>()
                    {
                        {"Error type", "Error reading cached online content" },
                        {"Service", "Third Party Identification Service" },
                        {"Message", e.Message }
                    }, attachments.ToArray());
                }
            }


            if (!File.Exists(Locations.GetThirdPartyIdentificationCachedFile()) || overrideThrottling || OnlineContent.CanFetchContentThrottleCheck())
            {
                try
                {
                    string json = HttpClientDownloadWithProgress.DownloadStringAwareOfEncoding(ThirdPartyIdentificationServiceURL);
                    File.WriteAllText(Locations.GetThirdPartyIdentificationCachedFile(), json);
                    return JsonConvert.DeserializeObject<Dictionary<string, CaseInsensitiveDictionary<ThirdPartyServices.ThirdPartyModInfo>>>(json);
                }
                catch (Exception e)
                {
                    //Unable to fetch latest help.
                    Log.Error("[AICORE] Error fetching online third party identification service: " + e.Message);

                    if (cached != null)
                    {
                        Log.Warning("[AICORE] Using cached third party identification service  file instead");
                    }
                    else
                    {
                        Log.Error("[AICORE] Unable to load third party identification service and local file doesn't exist. Returning a blank copy.");
                        Dictionary<string, CaseInsensitiveDictionary<ThirdPartyServices.ThirdPartyModInfo>> d = new Dictionary<string, CaseInsensitiveDictionary<ThirdPartyServices.ThirdPartyModInfo>>
                        {
                            ["ME1"] = new CaseInsensitiveDictionary<ThirdPartyServices.ThirdPartyModInfo>(),
                            ["ME2"] = new CaseInsensitiveDictionary<ThirdPartyServices.ThirdPartyModInfo>(),
                            ["ME3"] = new CaseInsensitiveDictionary<ThirdPartyServices.ThirdPartyModInfo>()
                        };
                        return d;
                    }
                }
            }

            try
            {
                return JsonConvert.DeserializeObject<Dictionary<string, CaseInsensitiveDictionary<ThirdPartyServices.ThirdPartyModInfo>>>(cached);
            }
            catch (Exception e)
            {
                Log.Error("[AICORE] Could not parse cached third party identification service file. Returning blank TPMI data instead. Reason: " + e.Message);
                return new Dictionary<string, CaseInsensitiveDictionary<ThirdPartyServices.ThirdPartyModInfo>>
                {
                    ["ME1"] = new CaseInsensitiveDictionary<ThirdPartyServices.ThirdPartyModInfo>(),
                    ["ME2"] = new CaseInsensitiveDictionary<ThirdPartyServices.ThirdPartyModInfo>(),
                    ["ME3"] = new CaseInsensitiveDictionary<ThirdPartyServices.ThirdPartyModInfo>()
                };
            }
        }

        public static string FetchRemoteString(string url)
        {
            try
            {
                return HttpClientDownloadWithProgress.DownloadStringAwareOfEncoding(url);
            }
            catch (Exception e)
            {
                Log.Error("[AICORE] Error downloading string: " + e.Message);
                return null;
            }
        }

        public static bool EnsureStaticAssets()
        {
            (string filename, string md5)[] objectInfoFiles = { ("ME1ObjectInfo.json", "d0b8c1786134b4aecc6a0543d32ddb59"), ("ME2ObjectInfo.json", "1c1f6f6354e7ad6be6ea0a7e473223a8"), ("ME3ObjectInfo.json", "300754261e40b58f27c9cf53b3c62005") };
            string localBaseDir = Locations.GetObjectInfoFolder();

            try
            {
                bool downloadOK = false;

                foreach (var info in objectInfoFiles)
                {
                    var localPath = Path.Combine(localBaseDir, info.filename);
                    bool download = !File.Exists(localPath);
                    if (!download)
                    {
                        var calcedMd5 = Utilities.CalculateMD5(localPath);
                        download = calcedMd5 != info.md5;
                        if (download) Log.Warning($@"[AICORE] Invalid hash for local asset {info.filename}: got {calcedMd5}, expected {info.md5}. Redownloading");
                    }
                    else
                    {
                        Log.Information($"[AICORE] Local asset missing: {info.filename}, downloading");
                    }

                    if (download)
                    {
                        foreach (var staticurl in StaticFilesBaseEndpoints)
                        {
                            var fullURL = staticurl + "objectinfos/" + info.filename;

                            try
                            {
                                using var wc = new HttpClientDownloadWithProgress(fullURL, localPath);
                                Log.Information("[AICORE] Downloading static asset: " + fullURL);
                                wc.StartDownload().RunSynchronously();
                                downloadOK = true;
                                break;
                            }
                            catch (Exception e)
                            {
                                Log.Error($"[AICORE] Could not download {info} from endpoint {fullURL} {e.Message}");
                            }
                        }
                    }
                    else downloadOK = true; //say we're OK
                }

                if (!downloadOK)
                {
                    throw new Exception("At least one static asset failed to download. Mod Manager will not properly function without these assets. See logs for more information");
                }
            }
            catch (Exception e)
            {
                Log.Error("[AICORE] Exception trying to ensure static assets: " + e.Message);
                CoreCrashes.TrackError2?.Invoke(new Exception(@"Could not download static supporting files: " + e.Message), null);
                return false;
            }

            return true;
        }

        //public static (MemoryStream result, string errorMessage) FetchString(string url)
        //{
        //    using var wc = new ShortTimeoutWebClient();
        //    string downloadError = null;
        //    MemoryStream responseStream = null;
        //    wc.DownloadDataCompleted += (a, args) =>
        //    {
        //        downloadError = args.Error?.Message;
        //        if (downloadError == null)
        //        {
        //            responseStream = new MemoryStream(args.Result);
        //        }
        //        lock (args.UserState)
        //        {
        //            //releases blocked thread
        //            Monitor.Pulse(args.UserState);
        //        }
        //    };
        //    var syncObject = new Object();
        //    lock (syncObject)
        //    {
        //        Debug.WriteLine("Download file to memory: " + url);
        //        wc.DownloadDataAsync(new Uri(url), syncObject);
        //        //This will block the thread until download completes
        //        Monitor.Wait(syncObject);
        //    }

        //    return (responseStream, downloadError);
        //}

        /// <summary>
        /// Downloads from a URL to memory. This is a blocking call and must be done on a background thread.
        /// </summary>
        /// <param name="url">URL to download from</param>
        /// <param name="progressCallback">Progress information clalback</param>
        /// <param name="hash">Hash check value (md5). Leave null if no hash check</param>
        /// <returns></returns>

        public static async Task<(MemoryStream result, string errorMessage)> DownloadToMemory(string url,
            Action<long, long> progressCallback = null,
            string hash = null,
            bool logDownload = false,
            CancellationTokenSource cancellationTokenSource = null)
        {
            MemoryStream responseStream = new MemoryStream();
            string downloadError = null;

            using var wc = new HttpClientDownloadWithProgress(url, responseStream, cancellationTokenSource?.Token ?? default);
            wc.ProgressChanged += (totalFileSize, totalBytesDownloaded, progressPercentage) =>
            {
                progressCallback?.Invoke(totalBytesDownloaded, totalFileSize ?? 0);
            };

            if (logDownload)
            {
                Log.Information(@"[AICORE] Downloading to memory: " + url);
            }
            else
            {
                Debug.WriteLine("Downloading to memory: " + url);
            }


            wc.StartDownload().Wait();
            if (cancellationTokenSource != null && cancellationTokenSource.Token.IsCancellationRequested)
            {
                return (null, null);
            }

            if (hash == null) return (responseStream, downloadError);
            var md5 = Utilities.CalculateMD5(responseStream);
            responseStream.Position = 0;
            if (md5 != hash)
            {
                responseStream = null;
                downloadError =
                    $"Hash of downloaded item ({url}) does not match expected hash. Expected: {hash}, got: {md5}"; //needs localized
            }

            return (responseStream, downloadError);
        }
    }
}