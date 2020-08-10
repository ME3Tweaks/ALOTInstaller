using System;
using System.Collections;
using System.Collections.Generic;
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
    public static class ManifestHandler
    {
        /// <summary>
        /// The loaded master manifest package. Populated by LoadMasterManifest().
        /// </summary>
        public static MasterManifestPackage MasterManifest { get; set; }

        /// <summary>
        /// Convenience variable for library wrappers to use for storing the current selected mode
        /// </summary>
        public static ManifestMode CurrentMode { get; set; }

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
                //if (USING_BETA)
                //{
                //    Log.Information("In BETA mode.");
                //    url = "https://raw.githubusercontent.com/ME3Tweaks/ALOTInstaller/master/manifest-beta.xml";
                //    Title += " BETA MODE";
                //}

                var fetchedManifest = webClient.DownloadString(new Uri(url));
                //var fetchedManifest = File.ReadAllText(@"E:\Documents\Visual Studio 2015\Projects\AlotAddOnGUI\manifest.xml");

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
            MasterManifest.ManifestModePackageMappping[ManifestMode.None] = new ManifestModePackage(); //Blank none mode

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
                    //AllTutorials.AddRange((from e in rootElement.Elements("tutorial")
                    //                       select new ManifestTutorial
                    //                       {
                    //                           Link = (string)e.Attribute("link"),
                    //                           Text = (string)e.Attribute("text"),
                    //                           ToolTip = (string)e.Attribute("tooltip"),
                    //                           MEUITMOnly = e.Attribute("meuitm") != null ? (bool)e.Attribute("meuitm") : false
                    //                       }).ToList());



                    // Add PREINSTALL MODS
                    mp.ManifestFiles.AddRange((from e in manifestElement.Elements("preinstallmod")
                                               select new PreinstallMod()
                                               {
                                                   FileSize = TryConvert.ToInt64(e.Element("file").Attribute("size")?.Value, 0L),
                                                   AlotVersionInfo = new TextureModInstallationInfo(0, 0, 0, 0),
                                                   Author = (string)e.Attribute("author"),
                                                   FriendlyName = (string)e.Attribute("friendlyname"),
                                                   //Optional = e.Attribute("optional") != null ? (bool)e.Attribute("optional") : false,
                                                   m_me1 = e.Element("games") != null ? (bool)e.Element("games").Attribute("me1") : false,
                                                   m_me2 = e.Element("games") != null ? (bool)e.Element("games").Attribute("me2") : false,
                                                   m_me3 = e.Element("games") != null ? (bool)e.Element("games").Attribute("me3") : false,
                                                   Filename = (string)e.Element("file").Attribute("filename"),
                                                   Tooltipname = e.Element("file").Attribute("tooltipname") != null
                                                       ? (string)e.Element("file").Attribute("tooltipname")
                                                       : (string)e.Attribute("friendlyname"),
                                                   DownloadLink = (string)e.Element("file").Attribute("downloadlink"),
                                                   UnpackedSingleFilename = e.Element("file").Attribute("unpackedsinglefilename") != null
                                                       ? (string)e.Element("file").Attribute("unpackedsinglefilename")
                                                       : null,
                                                   FileMD5 = (string)e.Element("file").Attribute("md5"),
                                                   UnpackedFileMD5 = (string)e.Element("file").Attribute("unpackedmd5"),
                                                   UnpackedFileSize = e.Element("file").Attribute("unpackedsize") != null
                                                       ? Convert.ToInt64((string)e.Element("file").Attribute("unpackedsize"))
                                                       : 0L,
                                                   TorrentFilename = (string)e.Element("file").Attribute("torrentfilename"),
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
                                                           IsDLC = d.Attribute("isdlc") != null ? (bool)d.Attribute("isdlc") : false,
                                                           ModVersion = (string)d.Attribute("version")
                                                       }).ToList(),
                                                   RecommendationString = e.Attribute("recommendation")?.Value
                                               }));

                    // ADD TEXTURE MODS
                    mp.ManifestFiles.AddRange((from e in manifestElement.Elements("addonfile")
                                               select new ManifestFile()
                                               {
                                                   FileSize = TryConvert.ToInt64(e.Element("file").Attribute("size")?.Value, 0L),
                                                   // MEUITM, ALOT
                                                   AlotVersionInfo = new TextureModInstallationInfo(
                                                       TryConvert.ToInt16(e.Attribute("alotversion")?.Value, 0),
                                                       TryConvert.ToByte(e.Attribute("alotupdateversion")?.Value, 0),
                                                       0, //Hotfix version was never used
                                                       TryConvert.ToInt32(e.Attribute("meuitmver")?.Value, 0)),

                                                   StageModFiles = TryConvert.ToBool(e.Attribute("stagemodfiles")?.Value, false),
                                                   Author = (string)e.Attribute("author"),
                                                   FriendlyName = (string)e.Attribute("friendlyname"),
                                                   //Optional = e.Attribute("optional") != null ? (bool)e.Attribute("optional") : false,
                                                   m_me1 = e.Element("games") != null ? (bool)e.Element("games").Attribute("me1") : false,
                                                   m_me2 = e.Element("games") != null ? (bool)e.Element("games").Attribute("me2") : false,
                                                   m_me3 = e.Element("games") != null ? (bool)e.Element("games").Attribute("me3") : false,
                                                   Filename = (string)e.Element("file").Attribute("filename"),
                                                   Tooltipname = e.Element("file").Attribute("tooltipname") != null
                                                       ? (string)e.Element("file").Attribute("tooltipname")
                                                       : (string)e.Attribute("friendlyname"),
                                                   DownloadLink = (string)e.Element("file").Attribute("downloadlink"),
                                                   UnpackedSingleFilename = e.Element("file").Attribute("unpackedsinglefilename") != null
                                                       ? (string)e.Element("file").Attribute("unpackedsinglefilename")
                                                       : null,
                                                   FileMD5 = (string)e.Element("file").Attribute("md5"),
                                                   UnpackedFileMD5 = (string)e.Element("file").Attribute("unpackedmd5"),
                                                   UnpackedFileSize = e.Element("file").Attribute("unpackedsize") != null
                                                       ? Convert.ToInt64((string)e.Element("file").Attribute("unpackedsize"))
                                                       : 0L,
                                                   TorrentFilename = (string)e.Element("file").Attribute("torrentfilename"),
                                                   UIPriority = TryConvert.ToInt32(e.Attribute("uipriority")?.Value, 5),
                                                   PackageFiles = e.Elements("packagefile")
                                                       .Select(r => new PackageFile
                                                       {
                                                           ChoiceTitle = "", //unused in this block
                                                           SourceName = (string)r.Attribute("sourcename"),
                                                           DestinationName = (string)r.Attribute("destinationname"),
                                                           MoveDirectly = r.Attribute("movedirectly") != null ? true : false,
                                                           CopyDirectly = r.Attribute("copydirectly") != null ? true : false,
                                                           m_me1 = r.Attribute("me1") != null ? true : false,
                                                           m_me2 = r.Attribute("me2") != null ? true : false,
                                                           m_me3 = r.Attribute("me3") != null ? true : false,
                                                           Processed = false
                                                       }).ToList(),

                                                   // Configurable mod options
                                                   ChoiceFiles = e.Elements("choicefile")
                                                       .Select(q => new ChoiceFile
                                                       {
                                                           ChoiceTitle = (string)q.Attribute("choicetitle"),
                                                           Choices = q.Elements("packagefile").Select(c => new PackageFile
                                                           {
                                                               ChoiceTitle = (string)c.Attribute("choicetitle"),
                                                               SourceName = (string)c.Attribute("sourcename"),
                                                               DestinationName = (string)c.Attribute("destinationname"),
                                                               MoveDirectly = c.Attribute("movedirectly") != null ? true : false,
                                                               CopyDirectly = c.Attribute("copydirectly") != null ? true : false,
                                                               m_me1 = c.Attribute("me1") != null ? true : false,
                                                               m_me2 = c.Attribute("me2") != null ? true : false,
                                                               m_me3 = c.Attribute("me3") != null ? true : false,
                                                               Processed = false
                                                           }).ToList()
                                                       }).ToList(),
                                                   // Included zip files
                                                   ZipFiles = e.Elements("zipfile")
                                                       .Select(q => new ZipFile
                                                       {
                                                           ChoiceTitle = (string)q.Attribute("choicetitle"),
                                                           Optional = q.Attribute("optional") != null ? (bool)q.Attribute("optional") : false,
                                                           DefaultOption = q.Attribute("default") != null
                                                               ? (bool)q.Attribute("default")
                                                               : true,
                                                           InArchivePath = q.Attribute("inarchivepath").Value,
                                                           GameDestinationPath = q.Attribute("gamedestinationpath").Value,
                                                           DeleteShaders = q.Attribute("deleteshaders") != null
                                                               ? (bool)q.Attribute("deleteshaders")
                                                               : false, //me1 only
                                                           MEUITMSoftShadows = q.Attribute("meuitmsoftshadows") != null
                                                               ? (bool)q.Attribute("meuitmsoftshadows")
                                                               : false, //me1,meuitm only
                                                       }).ToList(),
                                                   // Files that are copied into game directory
                                                   CopyFiles = e.Elements("copyfile")
                                                       .Select(q => new CopyFile
                                                       {
                                                           ChoiceTitle = (string)q.Attribute("choicetitle"),
                                                           Optional = q.Attribute("optional") != null
                                                                   ? (bool)q.Attribute("optional")
                                                                   : false,
                                                           DefaultOption = q.Attribute("default") != null
                                                                   ? (bool)q.Attribute("default")
                                                                   : true,
                                                           InArchivePath = q.Attribute("inarchivepath").Value,
                                                           GameDestinationPath = q.Attribute("gamedestinationpath").Value,
                                                       }
                                                       ).ToList(),
                                               }).OrderBy(p => p.UIPriority).ThenBy(o => o.Author).ThenBy(x => x.FriendlyName));

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
    }
}
