using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Xml.Linq;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.Objects;
using ALOTInstallerCore.Objects.Manifest;
using ALOTInstallerCore.Steps.Installer;
using Serilog;

namespace ALOTInstallerCore.Startup
{
    /// <summary>
    /// Class for handling online content
    /// </summary>
    public class OnlineContent
    {
        // ManifestFetch handles downloading and loading the ALOT manifest.

        /// <summary>
        /// List of ME2 DLC foldernames that are known to have bad texture exports that must be fixed prior to install
        /// </summary>
        public static List<string> ME3DLCRequiringTextureExportFixes = new List<string>();

        /// <summary>
        /// List of ME3 DLC foldernames that are known to have bad texture exports that must be fixed prior to install
        /// </summary>
        public static List<string> ME2DLCRequiringTextureExportFixes = new List<string>();

        public enum ManifestMode
        {
            /// <summary>
            /// No manifest. Install whatever you want, but you get to deal with the side effects
            /// </summary>
            None,
            /// <summary>
            /// MEUITM manifest. Installs only MEUITM with MEUITM defaults
            /// </summary>
            MEUITM,
            /// <summary>
            /// ALOT manifest. Applies installation using ALOT rules for version upgrades
            /// </summary>
            ALOT,
        }
        public class ManifestPackage
        {
            public List<string> MusicPackMirrors;
            //public List<ManifestTutorial> Tutorials;
            /// <summary>
            /// Files that are part of the ALOT texture installation manifest. These files must be downcast to their types for accessing info on them.
            /// </summary>
            public List<InstallerFile> ManifestFiles = new List<InstallerFile>(60);
            public List<string> ME3DLCsNeedingTextureFixes;
            public List<string> ME2DLCsNeedingTextureFixes;
            public bool IsBundled;
            public string ManifestVersion;
        }

        /// <summary>
        /// Fetches the ALOT manifest. This method will block.
        /// </summary>
        /// <param name="setCurrentOperationCallback">Callback to update text indicating what is currently happening</param>
        public static ManifestPackage FetchALOTManifest(Action<string> setCurrentOperationCallback = null)
        {
            ManifestPackage returnValue = null;
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

        private static ManifestPackage ParseManifest(bool usingBundled, string manifestText = null)
        {
            Log.Information("Reading ALOT manifest...");
            ManifestPackage mp = new ManifestPackage();

            try
            {
                if (manifestText == null) File.ReadAllText(manifestText);
                XElement rootElement = XElement.Parse(manifestText);
                string version = (string)rootElement.Attribute("version") ?? "";
                Debug.WriteLine("Manifest version: " + version);
                mp.MusicPackMirrors = rootElement.Elements("musicpackmirror").Select(xe => xe.Value).ToList();
                //AllTutorials.AddRange((from e in rootElement.Elements("tutorial")
                //                       select new ManifestTutorial
                //                       {
                //                           Link = (string)e.Attribute("link"),
                //                           Text = (string)e.Attribute("text"),
                //                           ToolTip = (string)e.Attribute("tooltip"),
                //                           MEUITMOnly = e.Attribute("meuitm") != null ? (bool)e.Attribute("meuitm") : false
                //                       }).ToList());

                MEMUpdater.HighestSupportedMEMVersion = rootElement.Element("highestapprovedmemversion") == null ? 999 : (int)rootElement.Element("highestapprovedmemversion");
                if (rootElement.Element("soaktestingmemversion") != null)
                {
                    XElement soakElem = rootElement.Element("soaktestingmemversion");
                    MEMUpdater.SoakTestingMEMVersion = (int)soakElem;
                    if (soakElem.Attribute("soakstartdate") != null)
                    {
                        string soakStartDateStr = soakElem.Attribute("soakstartdate").Value;
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
                    ME3DLCRequiringTextureExportFixes = rootElement.Elements("me3dlctexturefixes").Descendants("dlc").Select(x => x.Attribute("name").Value.ToUpperInvariant()).ToList();
                }

                if (rootElement.Element("me2dlctexturefixes") != null)
                {
                    ME2DLCRequiringTextureExportFixes = rootElement.Elements("me2dlctexturefixes").Descendants("dlc").Select(x => x.Attribute("name").Value.ToUpperInvariant()).ToList();
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
                //var repackoptions = rootElement.Element("repackoptions");
                //if (repackoptions != null)
                //{
                //    ME2_REPACK_MANIFEST_ENABLED = repackoptions.Attribute("me2repackenabled") != null ? (bool)repackoptions.Attribute("me2repackenabled") : true;
                //    Log.Information("Manifest says ME2 repack option can be used: " + ME2_REPACK_MANIFEST_ENABLED);
                //    ME3_REPACK_MANIFEST_ENABLED = repackoptions.Attribute("me3repackenabled") != null ? (bool)repackoptions.Attribute("me3repackenabled") : false;
                //    Log.Information("Manifest says ME3 repack option can be used: " + ME3_REPACK_MANIFEST_ENABLED);
                //    Checkbox_RepackME2GameFiles.IsEnabled = ME2_REPACK_MANIFEST_ENABLED;
                //    Checkbox_RepackME3GameFiles.IsEnabled = ME3_REPACK_MANIFEST_ENABLED;
                //    if (!ME2_REPACK_MANIFEST_ENABLED)
                //    {
                //        Checkbox_RepackME2GameFiles.IsChecked = false;
                //        Checkbox_RepackME2GameFiles.ToolTip = "Disabled by server manifest";
                //    }
                //    if (!ME3_REPACK_MANIFEST_ENABLED)
                //    {
                //        Checkbox_RepackME3GameFiles.IsChecked = false;
                //        Checkbox_RepackME3GameFiles.ToolTip = "Disabled by server manifest";
                //    }
                //}
                //else
                //{
                //    Log.Information("Manifest does not have repackoptions - using defaults");
                //}

                if (rootElement.Element("me3dlctexturefixes") != null)
                {
                    mp.ME3DLCsNeedingTextureFixes = rootElement.Elements("me3dlctexturefixes").Descendants("dlc").Select(x => x.Attribute("name").Value.ToUpperInvariant()).ToList();
                }
                else
                {
                    mp.ME3DLCsNeedingTextureFixes = new List<string>();
                }

                if (rootElement.Element("me2dlctexturefixes") != null)
                {
                    mp.ME2DLCsNeedingTextureFixes = rootElement.Elements("me2dlctexturefixes").Descendants("dlc").Select(x => x.Attribute("name").Value.ToUpperInvariant()).ToList();
                }
                else
                {
                    mp.ME2DLCsNeedingTextureFixes = new List<string>();
                }

                // Add PREINSTALL MODS
                mp.ManifestFiles.AddRange((from e in rootElement.Elements("preinstallmod")
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
                                               Tooltipname = e.Element("file").Attribute("tooltipname") != null ? (string)e.Element("file").Attribute("tooltipname") : (string)e.Attribute("friendlyname"),
                                               DownloadLink = (string)e.Element("file").Attribute("downloadlink"),
                                               UnpackedSingleFilename = e.Element("file").Attribute("unpackedsinglefilename") != null ? (string)e.Element("file").Attribute("unpackedsinglefilename") : null,
                                               FileMD5 = (string)e.Element("file").Attribute("md5"),
                                               UnpackedFileMD5 = (string)e.Element("file").Attribute("unpackedmd5"),
                                               UnpackedFileSize = e.Element("file").Attribute("unpackedsize") != null ? Convert.ToInt64((string)e.Element("file").Attribute("unpackedsize")) : 0L,
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
                                           }));

                // ADD TEXTURE MODS
                mp.ManifestFiles.AddRange((from e in rootElement.Elements("addonfile")
                                           select new ManifestFile()
                                           {
                                               //AlreadyInstalled = false,
                                               //Showing = false,
                                               //Enabled = true,
                                               //TrackTelemetry = e.Element("telemetrytracking") != null ? (bool)e.Attribute("telemetrytracking") : false,
                                               //ComparisonsLink = (string)e.Attribute("comparisonslink"),
                                               FileSize = e.Element("file").Attribute("size") != null ? Convert.ToInt64((string)e.Element("file").Attribute("size")) : 0L,
                                               //CopyDirectly = e.Element("file").Attribute("copydirectly") != null ? (bool)e.Element("file").Attribute("copydirectly") : false,

                                               // MEUITM, ALOT
                                               AlotVersionInfo = new TextureModInstallationInfo(
                                                   TryConvert.ToInt16(e.Attribute("alotversion")?.Value, 0),
                                                   TryConvert.ToByte(e.Attribute("alotupdateversion")?.Value, 0),
                                                   0, //Hotfix version was never used
                                                   TryConvert.ToInt32(e.Attribute("meuitmver")?.Value, 0)),

                                               //ProcessAsModFile = e.Attribute("processasmodfile") != null ? (bool)e.Attribute("processasmodfile") : false,
                                               Author = (string)e.Attribute("author"),
                                               FriendlyName = (string)e.Attribute("friendlyname"),
                                               //Optional = e.Attribute("optional") != null ? (bool)e.Attribute("optional") : false,
                                               m_me1 = e.Element("games") != null ? (bool)e.Element("games").Attribute("me1") : false,
                                               m_me2 = e.Element("games") != null ? (bool)e.Element("games").Attribute("me2") : false,
                                               m_me3 = e.Element("games") != null ? (bool)e.Element("games").Attribute("me3") : false,
                                               Filename = (string)e.Element("file").Attribute("filename"),
                                               Tooltipname = e.Element("file").Attribute("tooltipname") != null ? (string)e.Element("file").Attribute("tooltipname") : (string)e.Attribute("friendlyname"),
                                               DownloadLink = (string)e.Element("file").Attribute("downloadlink"),
                                               UnpackedSingleFilename = e.Element("file").Attribute("unpackedsinglefilename") != null ? (string)e.Element("file").Attribute("unpackedsinglefilename") : null,
                                               FileMD5 = (string)e.Element("file").Attribute("md5"),
                                               UnpackedFileMD5 = (string)e.Element("file").Attribute("unpackedmd5"),
                                               UnpackedFileSize = e.Element("file").Attribute("unpackedsize") != null ? Convert.ToInt64((string)e.Element("file").Attribute("unpackedsize")) : 0L,
                                               TorrentFilename = (string)e.Element("file").Attribute("torrentfilename"),
                                               UIPriority = TryConvert.ToInt32(e.Attribute("uipriority")?.Value, 5),
                                               PackageFiles = e.Elements("packagefile")
                                                   .Select(r => new PackageFile
                                                   {
                                                       ChoiceTitle = "", //unused in this block
                                                       SourceName = (string)r.Attribute("sourcename"),
                                                       DestinationName = (string)r.Attribute("destinationname"),
                                                       TPFSource = (string)r.Attribute("tpfsource"),
                                                       MoveDirectly = r.Attribute("movedirectly") != null ? true : false,
                                                       CopyDirectly = r.Attribute("copydirectly") != null ? true : false,
                                                       Delete = r.Attribute("delete") != null ? true : false,
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
                                                           TPFSource = (string)c.Attribute("tpfsource"),
                                                           MoveDirectly = c.Attribute("movedirectly") != null ? true : false,
                                                           CopyDirectly = c.Attribute("copydirectly") != null ? true : false,
                                                           Delete = c.Attribute("delete") != null ? true : false,
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
                                                       DefaultOption = q.Attribute("default") != null ? (bool)q.Attribute("default") : true,
                                                       InArchivePath = q.Attribute("inarchivepath").Value,
                                                       GameDestinationPath = q.Attribute("gamedestinationpath").Value,
                                                       DeleteShaders = q.Attribute("deleteshaders") != null ? (bool)q.Attribute("deleteshaders") : false, //me1 only
                                                       MEUITMSoftShadows = q.Attribute("meuitmsoftshadows") != null ? (bool)q.Attribute("meuitmsoftshadows") : false, //me1,meuitm only
                                                   }).ToList(),
                                               // Files that are copied into game directory
                                               CopyFiles = e.Elements("copyfile")
                                                   .Select(q => new CopyFile
                                                   {
                                                       ChoiceTitle = (string)q.Attribute("choicetitle"),
                                                       Optional = q.Attribute("optional") != null ? (bool)q.Attribute("optional") : false,
                                                       DefaultOption = q.Attribute("default") != null ? (bool)q.Attribute("default") : true,
                                                       InArchivePath = q.Attribute("inarchivepath").Value,
                                                       GameDestinationPath = q.Attribute("gamedestinationpath").Value,
                                                   }
                                               ).ToList(),
                                           }).OrderBy(p => p.UIPriority).ThenBy(o => o.Author).ThenBy(x => x.FriendlyName));

                if (rootElement.Element("soaktestingmemversion") != null)
                {
                    XElement soakElem = rootElement.Element("soaktestingmemversion");
                    MEMUpdater.SoakTestingMEMVersion = (int)soakElem;
                    if (soakElem.Attribute("soakstartdate") != null)
                    {
                        string soakStartDateStr = soakElem.Attribute("soakstartdate").Value;
                        MEMUpdater.SoakStartDate = DateTime.ParseExact(soakStartDateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                    }
                }


                if (!version.Equals(""))
                {
                    Log.Information("Manifest version: " + version);
                    mp.ManifestVersion = version;
                    mp.IsBundled = false;
                    mp.IsBundled = usingBundled;
                    if (usingBundled)
                    {
                        Log.Information("Using bundled manifest. Something might be wrong...");
                    }

                }
                //throw new Exception("Test error.");
            }
            catch (Exception e)
            {
                Log.Error("Error has occured parsing the XML!");
                Log.Error(e.Flatten());
                //MessageDialogResult result = await this.ShowMessageAsync("Error reading file manifest", "An error occured while reading the manifest file for installation. This may indicate a network failure or a packaging failure by Mgamerz - Please submit an issue to github (http://github.com/ME3Tweaks/ALOTInstaller/issues) and include the most recent log file from the logs directory.\n\n" + e.Message, MessageDialogStyle.Affirmative);
                //AddonFilesLabel.Text = "Error parsing manifest XML! Check the logs.";
                return null;
            }

            //int meuitmindex = -1;
            //Set Game
            foreach (ManifestFile mf in mp.ManifestFiles)
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

            return mp;
        }
        //else
        //{
        //    Log.Information("DEV_MODE file found. Not using online manifest.");
        //    UsingBundledManifest = true;
        //    Title += " DEV MODE";
        //    ManifestDownloaded();
        //}

        //if (!File.Exists(MANIFEST_LOC))
        //{
        //    Log.Fatal("No local manifest exists to use, exiting...");
        //    await this.ShowMessageAsync("No Manifest Available", "An error occured downloading the manifest for addon. Information that is required to build the addon is not available. Check the program logs.");
        //    Environment.Exit(1);
        //}
    }
}
