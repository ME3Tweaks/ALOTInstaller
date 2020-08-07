using System.Collections.Generic;
using System.IO;
using ALOTInstallerCore.Helpers;

namespace ALOTInstallerCore.Objects.Manifest
{
    /// <summary>
    /// Describes a file in the manifest.
    /// </summary>
    public class ManifestFile : InstallerFile
    {

        /// <summary>
        /// Loading indicator that this is an ME3 file. On setting this, it will set the bit in ApplicableGames. Do not use this variable, use ApplicableGames instead.
        /// </summary>
        internal bool m_me3
        {
            set
            {
                if (value)
                    ApplicableGames |= ApplicableGame.ME3;
                else
                    ApplicableGames &= ~ApplicableGame.ME3;
            }
        }
        /// <summary>
        /// Loading indicator that this is an ME2 file. On setting this, it will set the bit in ApplicableGames. Do not use this variable, use ApplicableGames instead.
        /// </summary>
        internal bool m_me2
        {
            set
            {
                if (value)
                    ApplicableGames |= ApplicableGame.ME2;
                else
                    ApplicableGames &= ~ApplicableGame.ME2;
            }
        }
        /// <summary>
        /// Loading indicator that this is an ME1 file. On setting this, it will set the bit in ApplicableGames. Do not use this variable, use ApplicableGames instead.
        /// </summary>
        internal bool m_me1
        {
            set
            {
                if (value)
                    ApplicableGames |= ApplicableGame.ME1;
                else
                    ApplicableGames &= ~ApplicableGame.ME1;
            }
        }


        //        private bool m_ready;
        //        public string ComparisonsLink { get; set; }

        //        public bool IsRequiredFile
        //        {
        //            get { return ALOTVersion > 0 || ALOTUpdateVersion > 0; }
        //        }
        //        public bool IsALOTUpdate
        //        {
        //            get { return ALOTUpdateVersion > 0; }
        //        }
        //        private string _readystatustext;
        //        private string _readyiconpath;

        //        /// <summary>
        //        /// UI ONLY
        //        /// </summary>
        //        public string ReadyIconPath
        //        {
        //            get
        //            {
        //                if (_readyiconpath != null)
        //                {
        //                    return _readyiconpath;
        //                }
        //                else if ((ALOTUpdateVersion > 0 || ALOTVersion > 0 || MEUITM) && Ready)
        //                {
        //                    //Get current info
        //                    ALOTVersionInfo info = null;
        //                    if (Game_ME1)
        //                    {
        //                        info = MainWindow.CURRENTLY_INSTALLED_ME1_ALOT_INFO;
        //                    }
        //                    else if (Game_ME2)
        //                    {
        //                        info = MainWindow.CURRENTLY_INSTALLED_ME2_ALOT_INFO;
        //                    }
        //                    else
        //                    {
        //                        info = MainWindow.CURRENTLY_INSTALLED_ME3_ALOT_INFO;
        //                    }


        //                    if (info != null)
        //                    {
        //                        if (ALOTVersion > 0 && (ALOTVersion != info.ALOTVER && (info.MEUITMVER == 0 || info.ALOTVER > 0)))
        //                        {
        //                            return "images/greycheckmark.png";
        //                        }

        //                        if (ALOTUpdateVersion > 0 && ALOTMainVersionRequired != info.ALOTVER)
        //                        {
        //                            return "images/greycheckmark.png";
        //                        }
        //                    }
        //                }
        //                //Not ALOT
        //                if (Ready && Enabled)
        //                {
        //                    return "images/greencheckmark.png";
        //                }
        //                else if (Ready && !Enabled)
        //                {
        //                    return "images/greycheckmark.png";
        //                }
        //                else
        //                {
        //                    return "images/orangedownload.png";
        //                }
        //            }

        //            set
        //            {
        //                _readyiconpath = value;
        //                OnPropertyChanged("ReadyIconPath");
        //            }
        //        }

        //        public string ReadyStatusText { get; set; }

        //        public string ReadyStatusTextOLD
        //        {
        //            get
        //            {
        //                if (_readystatustext != null)
        //                {
        //                    return _readystatustext;
        //                }
        //                if (!Enabled)
        //                {
        //                    return "Disabled";
        //                }
        //                if (Staged)
        //                {
        //                    return "File staged for installation";
        //                }
        //                if (ALOTUpdateVersion > 0 || ALOTVersion > 0 || MEUITM)
        //                {
        //                    //Get current info
        //                    ALOTVersionInfo info = null;
        //                    if (Game_ME1)
        //                    {
        //                        info = MainWindow.CURRENTLY_INSTALLED_ME1_ALOT_INFO;
        //                    }
        //                    else if (Game_ME2)
        //                    {
        //                        info = MainWindow.CURRENTLY_INSTALLED_ME2_ALOT_INFO;
        //                    }
        //                    else
        //                    {
        //                        info = MainWindow.CURRENTLY_INSTALLED_ME3_ALOT_INFO;
        //                    }

        //                    //Major Upgrade, including on unknown versinos
        //                    if (ALOTVersion > 0)
        //                    {
        //                        if (info != null)
        //                        {
        //                            if (ALOTVersion > info.ALOTVER && (info.ALOTVER != 0 || info.ALOTVER == 0 && info.MEUITMVER == 0)) //me1 issue - we cannot detect 5.0 with no meuitm
        //                            {
        //                                //newer version of ALOT is available
        //                                return "Restore to unmodified to install upgrade";
        //                            }

        //                            if (ALOTVersion > info.ALOTVER && info.ALOTVER == 0 && info.MEUITMVER > 0) //me1 issue - we cannot detect 5.0 with no meuitm
        //                            {
        //                                //alot not installed, meuitm installed. This could also be 5.0 with MEUITM but... :(
        //                                return "ALOT main file imported";
        //                            }
        //                            if (ALOTVersion == info.ALOTVER)
        //                            {
        //                                return "Already installed";
        //                            }
        //                            if (ALOTVersion == info.ALOTVER)
        //                            {
        //                                return "Newer major version of ALOT already installed";
        //                            }
        //                        }
        //                        else
        //                        {
        //                            return "ALOT main file imported";
        //                        }
        //                    }

        //                    if (ALOTUpdateVersion > 0)
        //                    {
        //                        if (info != null)
        //                        {
        //                            if (ALOTMainVersionRequired == info.ALOTVER)
        //                            {
        //                                if (ALOTUpdateVersion > info.ALOTUPDATEVER)
        //                                {
        //                                    return "ALOT update imported";
        //                                }
        //                                else
        //                                {
        //                                    return "Update already installed";
        //                                }
        //                                //Update applies to this version of installed ALOT
        //                            }
        //                            else
        //                            {
        //                                return "Update does not apply to installed ALOT version";
        //                            }
        //                        }
        //                        else
        //                        {
        //                            return "ALOT update imported";
        //                        }
        //                    }

        //                    if (MEUITM)
        //                    {
        //                        if (Ready)
        //                        {
        //                            if (info != null)
        //                            {
        //                                if (info.MEUITMVER >= MEUITMVer)
        //                                {
        //                                    return "MEUITM v" + MEUITMVer + " already installed";
        //                                }
        //                                else if (info.MEUITMVER == 0)
        //                                {
        //                                    return "MEUITM imported";
        //                                }
        //                                else if (info.MEUITMVER < MEUITMVer)
        //                                {
        //                                    return "MEUITM upgrade imported";
        //                                }
        //                            }
        //                            else
        //                            {
        //                                return "MEUITM imported";
        //                            }
        //                        }
        //                        else
        //                        {
        //                            if (info != null && info.MEUITMVER != 0 && info.MEUITMVER < MEUITMVer)
        //                            {
        //                                return "MEUITM upgrade available";
        //                            }
        //                            return "";
        //                        }
        //                    }
        //                }
        //                if (UserFile)
        //                {
        //                    return "User file is ready for processing";
        //                }

        //                if (MEUITM)
        //                {
        //                    return "MEUITM is imported";
        //                }
        //                return "Addon file is imported";
        //            }
        //            set
        //            {
        //                _readystatustext = value;
        //                OnPropertyChanged("ReadyStatusText");
        //                OnPropertyChanged("LeftBlockColor"); //ui update for this property
        //            }
        //        }
        /// <summary>
        /// If this file should be directly placed into the staging directory, rather than decompiling the file (.mod files)
        /// </summary>
        public bool ProcessAsModFile { get; set; }
        public string UnpackedSingleFilename { get; set; }

        //        public string ALOTMainPackedFilename { get; set; }
        public string TorrentFilename { get; set; }
        public int UIPriority { get; set; } //lower numbers i think are sorted to lower

        //        public string ALOTArchiveInFilePath { get; set; }
        public string Author { get; set; }

        public string Tooltipname { get; set; }

        public string DownloadLink { get; set; }
        //        public List<string> Duplicates { get; set; }

        //        public List<PackageFile> PackageFiles { get; set; }
        public List<ChoiceFile> ChoiceFiles { get; set; }
        public List<ZipFile> ZipFiles { get; set; }
        public List<CopyFile> CopyFiles { get; set; }

        //        public string DownloadAssistantString
        //        {
        //            get
        //            {

        //                return " - File title: " + Tooltipname;
        //            }
        //        }

        /// <summary>
        /// Indicates that this file is available to be installed (file resides on disk)
        /// </summary>

        //        public bool UserFile { get; internal set; }
        //        public string UserFilePath { get; internal set; }
        //        public bool MEUITM { get; internal set; }
        //        public bool Staged { get; internal set; }
        //        public bool Building { get; internal set; }


        public string FileMD5 { get; internal set; }
        public string UnpackedFileMD5 { get; set; }
        public long UnpackedFileSize { get; set; }
        public RecommendationType Recommendation { get; set; }
        //        public bool Optional { get; internal set; }
        //        private bool _enabled;
        //        public bool CopyDirectly { get; internal set; }

        //        public bool Enabled
        //        {
        //            get { return _enabled; }
        //            internal set
        //            {
        //                _enabled = value;
        //                OnPropertyChanged("LeftBlockColor"); //ui update for tihs property
        //                OnPropertyChanged("ReadyIconPath"); //ui update for tihs property
        //            }
        //        }

        //        public bool AlreadyInstalled { get; set; }

        //        public Color LeftBlockColor
        //        {
        //            get
        //            {
        //                if ((ALOTUpdateVersion > 0 || ALOTVersion > 0 || MEUITM) && Ready)
        //                {
        //                    //Get current info
        //                    ALOTVersionInfo info = null;
        //                    if (Game_ME1)
        //                    {
        //                        info = MainWindow.CURRENTLY_INSTALLED_ME1_ALOT_INFO;
        //                    }
        //                    else if (Game_ME2)
        //                    {
        //                        info = MainWindow.CURRENTLY_INSTALLED_ME2_ALOT_INFO;
        //                    }
        //                    else
        //                    {
        //                        info = MainWindow.CURRENTLY_INSTALLED_ME3_ALOT_INFO;
        //                    }

        //                    //Major Upgrade, including on unknown versions
        //                    if (info != null)
        //                    {
        //                        if (ALOTVersion > 0 && (ALOTVersion != info.ALOTVER && info.MEUITMVER == 0) || (ALOTVersion != info.ALOTVER && ALOTVersion != 0 && info.MEUITMVER != 0))
        //                        {
        //                            //Disabled
        //                            return Color.FromRgb((byte)0x60, (byte)0x60, (byte)0x60);
        //                        }

        //                        if (ALOTUpdateVersion > 0 && ALOTMainVersionRequired != info.ALOTVER)
        //                        {
        //                            return Color.FromRgb((byte)0x60, (byte)0x60, (byte)0x60);
        //                        }
        //                    }
        //                }

        //                if (Ready && Enabled)
        //                {
        //                    return Color.FromRgb((byte)0x31, (byte)0xae, (byte)0x90);
        //                }
        //                else if (Ready || !Enabled)
        //                {
        //                    //Disabled
        //                    return Color.FromRgb((byte)0x60, (byte)0x60, (byte)0x60);
        //                }
        //                else
        //                {
        //                    return Color.FromRgb((byte)0xd9, (byte)0x22, (byte)0x44);
        //                }
        //            }

        //        }

        //        public bool InstallME1DLCASI { get; internal set; }
        //        public bool TrackTelemetry { get; internal set; }

        //        private void OnPropertyChanged(string propertyName)
        //        {
        //            var handler = PropertyChanged;
        //            if (handler != null)
        //                handler(this, new PropertyChangedEventArgs(propertyName));
        //        }

        //        public override string ToString()
        //        {
        //            return FriendlyName;
        //        }

        //        internal void SetWorking()
        //        {
        //            ReadyIconPath = "images/workingicon.png";
        //        }
        //        internal void SetError()
        //        {
        //            ReadyIconPath = "images/redx_large.png";
        //        }
        //        internal bool IsInErrorState()
        //        {
        //            return ReadyIconPath == "images/redx_large.png";
        //        }
        //        internal void SetIdle()
        //        {
        //            ReadyIconPath = null;
        //        }

        //        internal bool IsCurrentlySingleFile()
        //        {
        //            if (Ready)
        //            {
        //                if (!UserFile)
        //                {
        //                    if (UnpackedSingleFilename != null && File.Exists(Path.Combine(MainWindow.DOWNLOADED_MODS_DIRECTORY, UnpackedSingleFilename)))
        //                    {
        //                        return true;
        //                    }
        //                }
        //            }

        //            return false;
        //        }
        //        internal string GetFile()
        //        {
        //            if (!UserFile)
        //            {
        //                if (UnpackedSingleFilename != null && File.Exists(Path.Combine(MainWindow.DOWNLOADED_MODS_DIRECTORY,UnpackedSingleFilename)))
        //                {
        //                    return Path.Combine(MainWindow.DOWNLOADED_MODS_DIRECTORY,UnpackedSingleFilename);
        //                }

        //                if (File.Exists(Path.Combine(MainWindow.DOWNLOADED_MODS_DIRECTORY, Filename)))
        //                {
        //                    return Path.Combine(MainWindow.DOWNLOADED_MODS_DIRECTORY, Filename);
        //                }
        //            }
        //            else
        //            {

        //                //if (File.Exists(UserFilePath))
        //                //{
        //                return UserFilePath;
        //                //}
        //            }

        //            return null;
        //        }
        //    }
        public override string ToString() => FriendlyName;
        public override bool UpdateReadyStatus()
        {
            var oldReady = Ready;
            var fp = GetUsedFilepath();
            if (File.Exists(fp))
            {
                var filesize = new FileInfo(fp).Length;
                if (Path.GetFileName(fp) == Filename || (TorrentFilename != null && Path.GetFileName(fp).Equals(TorrentFilename)))
                {
                    Ready = filesize == FileSize;
                    return oldReady != Ready;
                }

                if (UnpackedSingleFilename != null && Path.GetFileName(fp).Equals(UnpackedSingleFilename))
                {
                    Ready = filesize == UnpackedFileSize;
                    return oldReady != Ready;
                }
            }
            Ready = false;
            return oldReady != Ready;
        }

        /// <summary>
        /// Gets the backing file for this object. The unpacked single file version will be used if this file supports single unpacked files and the file is present.
        /// This method will check if unpacked file exists and is the correct size. It will not check if packed file exists or is the correct size.
        /// </summary>
        /// <returns></returns>
        public override string GetUsedFilepath()
        {
            if (UnpackedSingleFilename != null && UnpackedFileSize > 0 && UnpackedFileMD5 != null)
            {
                // This file supports unpacked mode
                var filePathUnpacked = Path.Combine(Settings.TextureLibraryLocation, UnpackedSingleFilename);
                if (File.Exists(filePathUnpacked) && new FileInfo(filePathUnpacked).Length == UnpackedFileSize)
                {
                    return filePathUnpacked;
                }
            }

            // Check if torrent file exists.
            if (TorrentFilename != null)
            {
                var filepathTorrent = Path.Combine(Settings.TextureLibraryLocation, TorrentFilename);
                if (File.Exists(filepathTorrent) && new FileInfo(filepathTorrent).Length == FileSize)
                {
                    return filepathTorrent;
                }
            }
            return Path.Combine(Settings.TextureLibraryLocation, Filename);
        }
    }
}