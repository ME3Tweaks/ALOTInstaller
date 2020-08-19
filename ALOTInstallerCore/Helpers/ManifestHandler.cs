﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Xml.Linq;
using ALOTInstallerCore.Objects;
using ALOTInstallerCore.Objects.Manifest;
using ALOTInstallerCore.Steps.Installer;
using Serilog;

namespace ALOTInstallerCore.Helpers
{
    /// <summary>
    /// Class for handling the download/parsing of the manifests
    /// </summary>
    public class ManifestHandler : INotifyPropertyChanged
    {
        /// <summary>
        /// The loaded master manifest package. Populated by LoadMasterManifest().
        /// </summary>
        public static MasterManifestPackage MasterManifest { get; set; }

        /// <summary>
        /// Convenience variable for library wrappers to use for storing the current selected mode
        /// </summary>
        public static ManifestMode CurrentMode { get; private set; }

        /// <summary>
        /// Updates the current mode, and notifies whoever is listening on OnManifestModeChanged, if any.
        /// </summary>
        /// <param name="mode"></param>
        public static void SetCurrentMode(ManifestMode mode)
        {
            var oldMode = CurrentMode;
            CurrentMode = mode;
            if (oldMode != CurrentMode)
            {
                OnManifestModeChanged?.Invoke(CurrentMode);
            }
        }
        /// <summary>
        /// Callback than be assigned for when the manifest mode has changed.
        /// </summary>
        public static Action<ManifestMode> OnManifestModeChanged { get; set; }

        /// <summary>
        /// Fetches and parses the master manifest into the manifests for each supported mode. This method will block.
        /// </summary>
        /// <param name="setCurrentOperationCallback">Callback to update text indicating what is currently happening</param>
        public static bool LoadMasterManifest(Action<string> setCurrentOperationCallback = null, Action<Exception> ErrorParsingManifest = null)
        {
            bool returnValue = false;
            using WebClient webClient = new WebClient();
            webClient.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);
            //Log.Information("Fetching latest manifest from github");
            //Build_ProgressBar.IsIndeterminate = true;
            setCurrentOperationCallback?.Invoke("Downloading latest installer manifest");
            //if (!File.Exists("DEV_MODE"))
            //{
            try
            {
                //File.Copy(@"C:\Users\mgame\Downloads\Manifest.xml", MANIFEST_LOC);
                string url = "https://raw.githubusercontent.com/ME3Tweaks/ALOTInstaller/ALOT-v4/manifest.xml";
                if (Settings.BetaMode)
                {
                    Log.Information("BETA MODE: Fetching beta mode manifest.");
                    url = "https://raw.githubusercontent.com/ME3Tweaks/ALOTInstaller/ALOT-v4/manifest-beta.xml";
                }

                var fetchedManifest = webClient.DownloadString(new Uri(url));
                //var fetchedManifest = File.ReadAllText(@"C:\Users\Mgamerz\source\repos\AlotAddOnGUI\manifest.xml");

                if (Utilities.TestXMLIsValid(fetchedManifest))
                {
                    Log.Information("Manifest fetched.");
                    try
                    {
                        File.WriteAllText(Locations.GetCachedManifestPath(), fetchedManifest);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Unable to write and remove old manifest! We're probably headed towards a crash.");
                        Log.Error(ex.Flatten());
                        //UsingBundledManifest = true;
                    }
                    setCurrentOperationCallback?.Invoke("Parsing installer manifest");
                    returnValue = ParseManifest(false, fetchedManifest);
                    //ManifestDownloaded();
                }
                else
                {
                    Log.Error("Response from server was not valid XML! " + fetchedManifest);
                    //Crashes.TrackError(new Exception("Invalid XML from server manifest!"));
                    if (File.Exists(Locations.GetCachedManifestPath()))
                    {
                        Log.Information("Reading cached manifest instead.");
                        //ManifestDownloaded();
                    }
                    else if (!File.Exists(Locations.GetCachedManifestPath())/* && File.Exists(MANIFEST_BUNDLED_LOC)*/)
                    {
                        Log.Information("Reading bundled manifest instead.");
                        //File.Copy(MANIFEST_BUNDLED_LOC, MANIFEST_LOC);
                        //UsingBundledManifest = true;
                        //ManifestDownloaded();
                    }
                    else
                    {
                        Log.Error("Local manifest also doesn't exist! No manifest is available.");
                        //await this.ShowMessageAsync("No Manifest Available", "An error occured downloading or reading the manifest for ALOT Installer. There is no local bundled version available. Information that is required to build and install ALOT is not available. Check the program logs.");
                        //Environment.Exit(1);
                    }

                }
            }
            catch (WebException e)
            {
                Log.Error("WebException occured getting manifest from server: " + e.Flatten());
                //if (!File.Exists(MANIFEST_LOC) && File.Exists(MANIFEST_BUNDLED_LOC))
                //{
                //    Log.Information("Reading bundled manifest instead.");
                //    File.Delete(MANIFEST_LOC);
                //    File.Copy(MANIFEST_BUNDLED_LOC, MANIFEST_LOC);
                //    UsingBundledManifest = true;
                //    ManifestDownloaded();
                //}
            }
            //}
            //catch (Exception e)
            //{
            //    Debug.WriteLine(DateTime.Now);
            //    Log.Error("Other Exception occured getting manifest from server/reading manifest: " + e.ToString());
            //    if (!File.Exists(MANIFEST_LOC) && File.Exists(MANIFEST_BUNDLED_LOC))
            //    {
            //        Log.Information("Reading bundled manifest instead.");
            //        File.Delete(MANIFEST_LOC);
            //        File.Copy(MANIFEST_BUNDLED_LOC, MANIFEST_LOC);
            //        UsingBundledManifest = true;
            //    }
            //}
            return returnValue;
        }

        private static bool ParseManifest(bool usingBundled, string manifestText, Action<Exception> ErrorParsingManifest = null)
        {
            Log.Information("Reading master manifest...");
            MasterManifest = new MasterManifestPackage()
            {
                UsingBundled = usingBundled
            };
            MasterManifest.ManifestModePackageMappping[ManifestMode.Free] = new ManifestModePackage(); //Blank none mode

            try
            {
                //if (manifestText == null) File.ReadAllText(manifestText);
                XElement rootElement = XElement.Parse(manifestText);

                #region Master Manifest
                string version = (string)rootElement.Attribute("version") ?? "";
                Debug.WriteLine("Master manifest version: " + version);
                MasterManifest.MusicPackMirrors = rootElement.Elements("musicpackmirror").Select(xe => xe.Value).ToList();
                MEMUpdater.HighestSupportedMEMVersion = TryConvert.ToInt32(rootElement.Element("highestapprovedmemversion")?.Value, 999);
                XElement soakElem = rootElement.Element("soaktestingmemversion");
                if (soakElem != null)
                {
                    MEMUpdater.SoakTestingMEMVersion = (int)soakElem;
                    var soakStartDateElem = soakElem.Attribute("soakstartdate");
                    if (soakStartDateElem != null)
                    {
                        string soakStartDateStr = soakStartDateElem.Value;
                        MEMUpdater.SoakStartDate = DateTime.ParseExact(soakStartDateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                    }
                }

                Utilities.SUPPORTED_HASHES_ME1.Clear();
                Utilities.SUPPORTED_HASHES_ME2.Clear();
                Utilities.SUPPORTED_HASHES_ME3.Clear();

                if (rootElement.Element("supportedhashes") != null)
                {
                    var supportedHashesList = rootElement.Element("supportedhashes").Descendants("supportedhash");
                    foreach (var item in supportedHashesList)
                    {
                        KeyValuePair<string, string> kvp = new KeyValuePair<string, string>(item.Value, (string)item.Attribute("name"));
                        switch ((string)item.Attribute("game"))
                        {
                            case "me1":
                                Utilities.SUPPORTED_HASHES_ME1.Add(kvp);
                                break;
                            case "me2":
                                Utilities.SUPPORTED_HASHES_ME2.Add(kvp);
                                break;
                            case "me3":
                                Utilities.SUPPORTED_HASHES_ME3.Add(kvp);
                                break;
                        }
                    }
                }

                if (rootElement.Element("me3dlctexturefixes") != null)
                {
                    MasterManifest.ME3DLCRequiringTextureExportFixes = rootElement.Elements("me3dlctexturefixes").Descendants("dlc").Select(x => x.Attribute("name").Value.ToUpperInvariant()).ToList();
                }

                if (rootElement.Element("me2dlctexturefixes") != null)
                {
                    MasterManifest.ME2DLCRequiringTextureExportFixes = rootElement.Elements("me2dlctexturefixes").Descendants("dlc").Select(x => x.Attribute("name").Value.ToUpperInvariant()).ToList();
                }

                if (rootElement.Element("stages") != null)
                {
                    ProgressHandler.DefaultStages =
                        (from stage in rootElement.Element("stages").Descendants("stage")
                         select new Stage
                         {
                             StageName = stage.Attribute("name")?.Value,
                             TaskName = stage.Attribute("tasktext")?.Value,
                             Weight = Convert.ToDouble(stage.Attribute("weight")?.Value, CultureInfo.InvariantCulture),
                             ME1Scaling = TryConvert.ToDouble(stage.Attribute("me1weightscaling")?.Value, 1),
                             ME2Scaling = TryConvert.ToDouble(stage.Attribute("me2weightscaling")?.Value, 1),
                             ME3Scaling = TryConvert.ToDouble(stage.Attribute("me3weightscaling")?.Value, 1),
                             FailureInfos = stage.Elements("failureinfo").Select(z => new StageFailure
                             {
                                 FailureIPCTrigger = z.Attribute("ipcerror")?.Value,
                                 FailureBottomText = z.Attribute("failedbottommessage")?.Value,
                                 FailureTopText = z.Attribute("failedtopmessage")?.Value,
                                 FailureHeaderText = z.Attribute("failedheadermessage")?.Value,
                                 FailureResultCode = Convert.ToInt32(z.Attribute("resultcode").Value),
                                 Warning = TryConvert.ToBool(z.Attribute("warning")?.Value, false)
                             }).ToList()
                         }).ToList();
                }
                else
                {
                    ProgressHandler.UseBuiltinDefaultStages();
                }

                #endregion

                #region Manifest Modes

                var manifests = rootElement.Elements("manifest");

                foreach (var manifestElement in manifests)
                {
                    ManifestModePackage mp = new ManifestModePackage();
                    mp.ModeDescription = manifestElement.Attribute("description")?.Value;
                    mp.Tutorials.AddRange((from e in rootElement.Elements("tutorial")
                                           select new ManifestTutorial
                                           {
                                               Link = (string)e.Attribute("link"),
                                               Text = (string)e.Attribute("text"),
                                               ToolTip = (string)e.Attribute("tooltip"),
                                           }).ToList());

                    // Add PREINSTALL MODS
                    mp.ManifestFiles.AddRange((from e in manifestElement.Elements("preinstallmod")
                                               select new PreinstallMod()
                                               {
                                                   FileSize = TryConvert.ToInt64(e.Element("file").Attribute("size")?.Value, 0L),
                                                   AlotVersionInfo = new TextureModInstallationInfo(0, 0, 0, 0),
                                                   Author = (string)e.Attribute("author"),
                                                   FriendlyName = (string)e.Attribute("friendlyname"),
                                                   //Optional = e.Attribute("optional") != null ? (bool)e.Attribute("optional") : false,
                                                   m_me1 = TryConvert.ToBool(e.Element("games")?.Attribute("me1")?.Value, false),
                                                   m_me2 = TryConvert.ToBool(e.Element("games")?.Attribute("me2")?.Value, false),
                                                   m_me3 = TryConvert.ToBool(e.Element("games")?.Attribute("me3")?.Value, false),
                                                   Filename = (string)e.Element("file").Attribute("filename"),
                                                   Tooltipname = e.Element("file").Attribute("tooltipname")?.Value ?? e.Attribute("friendlyname").Value,
                                                   DownloadLink = (string)e.Element("file").Attribute("downloadlink"),
                                                   //UnpackedSingleFilename = e.Element("file").Attribute("unpackedsinglefilename")?.Value,
                                                   FileMD5 = (string)e.Element("file").Attribute("md5"),
                                                   //UnpackedFileMD5 = (string)e.Element("file").Attribute("unpackedmd5"),
                                                   //UnpackedFileSize = TryConvert.ToInt64(e.Element("file").Attribute("unpackedsize")?.Value, 0L),
                                                   TorrentFilename = (string)e.Element("file").Attribute("torrentfilename"),
                                                   InstallPriority = TryConvert.ToInt32(e.Attribute("installpriority")?.Value, 5),
                                                   UIPriority = TryConvert.ToInt32(e.Attribute("uipriority")?.Value, 5),
                                                   OptionGroup = e.Attribute("optiongroup")?.Value,
                                                   PackageFiles = e.Elements("packagefile")
                                                       .Select(r => new PackageFile
                                                       {
                                                           SourceName = (string)r.Attribute("sourcename"),
                                                           MoveDirectly = TryConvert.ToBool(r.Attribute("movedirectly")?.Value, false),
                                                           m_me1 = TryConvert.ToBool(r.Attribute("me1")?.Value, false),
                                                           m_me2 = TryConvert.ToBool(r.Attribute("me2")?.Value, false),
                                                           m_me3 = TryConvert.ToBool(r.Attribute("me3")?.Value, false),
                                                       }).ToList(),
                                                   ExtractionRedirects = e.Elements("extractionredirect")
                                                       .Select(d => new PreinstallMod.ExtractionRedirect
                                                       {
                                                           ArchiveRootPath = (string)d.Attribute("archiverootpath"),
                                                           RelativeDestinationDirectory = (string)d.Attribute("relativedestinationdirectory"),
                                                           OptionalRequiredDLC = (string)d.Attribute("optionalrequireddlc"),
                                                           OptionalAnyDLC = (string)d.Attribute("optionalanydlc"),
                                                           OptionalRequiredFiles = (string)d.Attribute("optionalrequiredfiles"),
                                                           OptionalRequiredFilesSizes = (string)d.Attribute("optionalrequiredfilessizes"),
                                                           LoggingName = (string)d.Attribute("loggingname"),
                                                           IsDLC = TryConvert.ToBool(d.Attribute("isdlc")?.Value, false),
                                                           ModVersion = (string)d.Attribute("version")
                                                       }).ToList(),
                                                   RecommendationString = e.Attribute("recommendation")?.Value,
                                                   RecommendationReason = e.Attribute("recommendationreason")?.Value,
                                               }));

                    // ADD TEXTURE MODS
                    mp.ManifestFiles.AddRange((from e in manifestElement.Elements("addonfile")
                                               select new ManifestFile()
                                               {
                                                   // MEUITM, ALOT
                                                   AlotVersionInfo = new TextureModInstallationInfo(
                                                       TryConvert.ToInt16(e.Attribute("alotversion")?.Value, 0),
                                                       TryConvert.ToByte(e.Attribute("alotupdateversion")?.Value, 0),
                                                       0, //Hotfix version was never used
                                                       TryConvert.ToInt32(e.Attribute("meuitmver")?.Value, 0)),

                                                   StageModFiles = TryConvert.ToBool(e.Attribute("stagemodfiles")?.Value, false),
                                                   Author = (string)e.Attribute("author"),
                                                   FriendlyName = (string)e.Attribute("friendlyname"),
                                                   m_me1 = TryConvert.ToBool(e.Element("games")?.Attribute("me1")?.Value, false),
                                                   m_me2 = TryConvert.ToBool(e.Element("games")?.Attribute("me2")?.Value, false),
                                                   m_me3 = TryConvert.ToBool(e.Element("games")?.Attribute("me3")?.Value, false),

                                                   Filename = (string)e.Element("file").Attribute("filename"),
                                                   FileSize = TryConvert.ToInt64(e.Element("file").Attribute("size")?.Value, 0L),
                                                   FileMD5 = (string)e.Element("file").Attribute("md5"),

                                                   UnpackedSingleFilename = e.Element("file").Attribute("unpackedsinglefilename")?.Value,
                                                   UnpackedFileMD5 = (string)e.Element("file").Attribute("unpackedmd5"),
                                                   UnpackedFileSize = TryConvert.ToInt64(e.Element("file").Attribute("unpackedsize")?.Value, 0L),
                                                   TorrentFilename = (string)e.Element("file").Attribute("torrentfilename"),

                                                   InstallPriority = TryConvert.ToInt32(e.Attribute("installpriority")?.Value, 5),
                                                   UIPriority = TryConvert.ToInt32(e.Attribute("uipriority")?.Value, 5),
                                                   Tooltipname = e.Element("file").Attribute("tooltipname")?.Value ?? e.Attribute("friendlyname").Value,
                                                   DownloadLink = (string)e.Element("file").Attribute("downloadlink"),

                                                   PackageFiles = e.Elements("packagefile")
                                                       .Select(r => new PackageFile
                                                       {
                                                           ChoiceTitle = "", //unused in this block
                                                           SourceName = r.Attribute("sourcename").Value,
                                                           DestinationName = r.Attribute("destinationname")?.Value,
                                                           MoveDirectly = TryConvert.ToBool(r.Attribute("movedirectly")?.Value, false),
                                                           CopyDirectly = TryConvert.ToBool(r.Attribute("copydirectly")?.Value, false),
                                                           m_me1 = TryConvert.ToBool(r.Attribute("me1")?.Value, false),
                                                           m_me2 = TryConvert.ToBool(r.Attribute("me2")?.Value, false),
                                                           m_me3 = TryConvert.ToBool(r.Attribute("me3")?.Value, false),
                                                       }).ToList(),

                                                   // Configurable mod options
                                                   ChoiceFiles = e.Elements("choicefile")
                                                       .Select(q => new ChoiceFile
                                                       {
                                                           ChoiceTitle = q.Attribute("choicetitle").Value,
                                                           Choices = q.Elements("packagefile").Select(c => new PackageFile
                                                           {
                                                               ChoiceTitle = c.Attribute("choicetitle").Value,
                                                               SourceName = c.Attribute("sourcename").Value,
                                                               DestinationName = c.Attribute("destinationname")?.Value,
                                                               MoveDirectly = TryConvert.ToBool(c.Attribute("movedirectly")?.Value, false),
                                                               CopyDirectly = TryConvert.ToBool(c.Attribute("copydirectly")?.Value, false),
                                                               m_me1 = TryConvert.ToBool(c.Attribute("me1")?.Value, false),
                                                               m_me2 = TryConvert.ToBool(c.Attribute("me2")?.Value, false),
                                                               m_me3 = TryConvert.ToBool(c.Attribute("me3")?.Value, false),
                                                               Transient = true
                                                           }).ToList()
                                                       }).ToList(),
                                                   // Included zip files
                                                   ZipFiles = e.Elements("zipfile")
                                                       .Select(q => new ZipFile
                                                       {
                                                           ChoiceTitle = q.Attribute("choicetitle").Value,
                                                           Optional = TryConvert.ToBool(q.Attribute("optional")?.Value, false),
                                                           DefaultOption = TryConvert.ToBool(q.Attribute("default")?.Value, true),
                                                           InArchivePath = q.Attribute("inarchivepath").Value,
                                                           GameDestinationPath = q.Attribute("gamedestinationpath").Value,
                                                           DeleteShaders = TryConvert.ToBool(q.Attribute("deleteshaders")?.Value, false),
                                                           MEUITMSoftShadows = TryConvert.ToBool(q.Attribute("meuitmsoftshadows")?.Value, true),
                                                       }).ToList(),
                                                   // Files that are copied into game directory
                                                   CopyFiles = e.Elements("copyfile")
                                                       .Select(q => new CopyFile
                                                       {
                                                           ChoiceTitle = (string)q.Attribute("choicetitle"),
                                                           Optional = TryConvert.ToBool(q.Attribute("optional")?.Value, false),
                                                           DefaultOption = TryConvert.ToBool(q.Attribute("default")?.Value, true),
                                                           InArchivePath = q.Attribute("inarchivepath").Value,
                                                           GameDestinationPath = q.Attribute("gamedestinationpath").Value,
                                                       }
                                                       ).ToList(),
                                                   RecommendationString = e.Attribute("recommendation")?.Value,
                                                   RecommendationReason = e.Attribute("recommendationreason")?.Value,
                                               }));

                    mp.ManifestFiles = mp.ManifestFiles.OrderBy(p => p.UIPriority).ThenBy(o => o.Author).ThenBy(x => x.FriendlyName).ToList();

                    //Set Game
                    foreach (var mf in mp.ManifestFiles)
                    {
                        foreach (var msf in mf.PackageFiles)
                        {
                            mf.ApplicableGames |= msf.ApplicableGames;
                        }
                        if (mf.ApplicableGames == ApplicableGame.None)
                        {
                            // If none, then all
                            mf.ApplicableGames = ApplicableGame.ME1 | ApplicableGame.ME2 | ApplicableGame.ME3;
                        }
                    }

                    string modeString = manifestElement.Attribute("mode")?.Value;
                    if (Enum.TryParse<ManifestMode>(modeString, out var mode))
                    {
                        string manifestVersion = manifestElement.Attribute("version")?.Value;
                        Log.Information($"{mode} manifest version: {manifestVersion}");
                        mp.ManifestVersion = version;
                        MasterManifest.ManifestModePackageMappping[mode] = mp;
                    }
                }

                #endregion

                if (usingBundled)
                {
                    Log.Information("Using bundled manifest instead of live manifest.");
                }

                //throw new Exception("Test error.");
            }
            catch (Exception e)
            {
                Log.Error("Error has occured parsing the manifest XML!");
                Log.Error(e.Flatten());
                ErrorParsingManifest?.Invoke(e);
                return false;
            }
            return true; //Parsing succeeded
        }

        /// <summary>
        /// Gets a list of all manifest files across all modes. There may be duplicates if the same backing file is used in multiple modes (such as MEUITM)
        /// </summary>
        /// <returns></returns>
        public static List<ManifestFile> GetAllManifestFiles()
        {
            var manifestFiles = new List<ManifestFile>();
            if (MasterManifest != null)
            {
                foreach (var mapping in MasterManifest.ManifestModePackageMappping)
                {
                    manifestFiles.AddRange(mapping.Value.ManifestFiles);
                }
            }

            return manifestFiles;
        }

        public static List<InstallerFile> GetManifestFilesForMode(ManifestMode mode, bool includeUserFiles = false)
        {
            List<InstallerFile> files = new List<InstallerFile>();
            if (MasterManifest != null && MasterManifest.ManifestModePackageMappping.TryGetValue(mode, out var mp))
            {
                files.AddRange(mp.ManifestFiles);
                if (includeUserFiles)
                {
                    files.AddRange(mp.UserFiles);
                }
            }

            return files;
        }
        
        /// <summary>
        /// Gets the level of readiness of non-optional manifest files. THis can be used to show the user how 'ready' the recommended experience for the current mode is before install
        /// </summary>
        /// <returns></returns>
        public static (long ready, long recommendedCount) GetNonOptionalReadyness()
        {
            var filesToCheck = GetManifestFilesForMode(CurrentMode).OfType<ManifestFile>().Where(x => x.Recommendation == RecommendationType.Recommended || x.Recommendation == RecommendationType.Required);
            int readyx = filesToCheck.Count(x=>x.Ready);
            int recommendedCountx = filesToCheck.Count();
            return (readyx, recommendedCountx);
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}