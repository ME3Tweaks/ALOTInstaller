using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Xml.Linq;

namespace AlotAddOnGUI.classes
{
    public sealed class AddonFile : INotifyPropertyChanged
    {
        public bool Showing { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;
        private bool m_ready;
        public short ALOTVersion { get; set; }
        public byte ALOTUpdateVersion { get; set; }
        public short ALOTMainVersionRequired { get; set; }
        public string ComparisonsLink { get; set; }

        public enum FileState
        {
            NotReady, //not available
            ManualDisabled, //disabled by user
            AutoDisabled, //disabled by program (e.g. conflicting update and installed)
            Ready, //ready to install
            Processing,
            WaitingForProcessing,
            Staged //staged for install
        }

        public FileState State;

        /// <summary>
        /// Is a file with ALOTVErsion of ALOTUpdateVersion set
        /// </summary>
        public bool IsALOTRequiredFile => ALOTVersion > 0 || ALOTUpdateVersion > 0;

        /// <summary>
        /// Has ALOTUpdateVersion > 0
        /// </summary>
        public bool IsALOTUpdate => ALOTUpdateVersion > 0;

        //private string _readystatustext;
        private string _readyiconpath;

        public string ReadyIconPath
        {
            get
            {
                if (_readyiconpath != null)
                {
                    return _readyiconpath;
                }

                if ((ALOTUpdateVersion > 0 || ALOTVersion > 0 || MEUITM) && IsReady)
                {
                    //Get current info
                    ALOTVersionInfo info = null;
                    if (Game_ME1)
                    {
                        info = MainWindow.CURRENTLY_INSTALLED_ME1_ALOT_INFO;
                    }
                    else if (Game_ME2)
                    {
                        info = MainWindow.CURRENTLY_INSTALLED_ME2_ALOT_INFO;
                    }
                    else
                    {
                        info = MainWindow.CURRENTLY_INSTALLED_ME3_ALOT_INFO;
                    }


                    if (info != null && info.ALOTVER > 0)
                    {
                        if (ALOTVersion != info.ALOTVER)
                        {
                            return "images/greycheckmark.png";
                        }

                        if (ALOTUpdateVersion > 0 && ALOTMainVersionRequired != info.ALOTVER)
                        {
                            return "images/greycheckmark.png";
                        }
                    }
                }
                //Not ALOT
                if (IsReady)
                {
                    return "images/greencheckmark.png";
                }
                if (IsDisabled)
                {
                    return "images/greycheckmark.png";
                }
             
                return "images/orangedownload.png";
            }

            set
            {
                _readyiconpath = value;
                OnPropertyChanged("ReadyIconPath");
            }
        }

        public string ReadyStatusText
        {
            get
            {
                //if (_readystatustext != null)
                //{
                //    return _readystatustext;
                //}
                if (State == FileState.ManualDisabled)
                {
                    return "Disabled, will not install";
                }
                if (State == FileState.Staged)
                {
                    return "File staged for installation";
                }
                if (IsALOTRequiredFile || MEUITM)
                {
                    //Get current info
                    ALOTVersionInfo info = null;
                    if (Game_ME1)
                    {
                        info = MainWindow.CURRENTLY_INSTALLED_ME1_ALOT_INFO;
                    }
                    else if (Game_ME2)
                    {
                        info = MainWindow.CURRENTLY_INSTALLED_ME2_ALOT_INFO;
                    }
                    else
                    {
                        info = MainWindow.CURRENTLY_INSTALLED_ME3_ALOT_INFO;
                    }

                    //Major Upgrade, including on unknown versions
                    if (ALOTVersion > 0)
                    {
                        if (info != null)
                        {
                            if (ALOTVersion != 0)
                                Debug.WriteLine("i");
                            if (ALOTVersion > info.ALOTVER && info.ALOTVER != 0) //ALOT was prevoiusly installed and its version does not match
                            {
                                //newer version of ALOT is available
                                return "Restore to unmodified to install new version of ALOT";
                            }
                            //else if (ALOTVersion > info.ALOTVER && info.ALOTVER == 0 && info.MEUITMVER > 0) //me1 issue - we cannot detect 5.0 with no meuitm
                            //{
                            //    //alot not installed, meuitm installed. This could also be 5.0 with MEUITM but... :(
                            //    return "ALOT main file imported";
                            //}
                            else if (ALOTVersion == info.ALOTVER)
                            {
                                return "Already installed";
                            }
                            else if (ALOTVersion == info.ALOTVER)
                            {
                                return "Newer major version of ALOT already installed";
                            }
                            else
                            {
                                return "ALOT main file imported";
                            }
                        }
                        else
                        {
                            return "ALOT main file imported";
                        }
                    }

                    if (ALOTUpdateVersion > 0)
                    {
                        if (info != null && info.ALOTVER > 0)
                        {
                            if (ALOTMainVersionRequired == info.ALOTVER)
                            {
                                if (ALOTUpdateVersion > info.ALOTUPDATEVER)
                                {
                                    return "ALOT update imported";
                                }
                                else
                                {
                                    return "Update already installed";
                                }
                                //Update applies to this version of installed ALOT
                            }
                            else
                            {
                                return "Update does not apply to installed ALOT version";
                            }
                        }
                        else
                        {
                            return "ALOT update imported";
                        }
                    }

                    if (MEUITM)
                    {
                        if (IsReady)
                        {
                            if (info != null)
                            {
                                if (info.MEUITMVER >= MEUITMVer)
                                {
                                    return "MEUITM v" + MEUITMVer + " already installed";
                                }
                                else if (info.MEUITMVER == 0)
                                {
                                    return "MEUITM imported";
                                }
                                else if (info.MEUITMVER < MEUITMVer)
                                {
                                    return "MEUITM upgrade imported";
                                }
                            }
                            else
                            {
                                return "MEUITM imported";
                            }
                        }
                        else
                        {
                            if (info != null && info.MEUITMVER != 0 && info.MEUITMVER < MEUITMVer)
                            {
                                return "MEUITM upgrade available";
                            }
                            return "";
                        }
                    }
                }
                if (UserFile)
                {
                    return "User file is ready for processing";
                }

                if (MEUITM)
                {
                    return "MEUITM is imported";
                }
                return "Addon file is imported (addon files are not tracked as installed or not)";
            }
            set
            {
                //_readystatustext = value;
                OnPropertyChanged("ReadyStatusText");
                OnPropertyChanged("LeftBlockColor"); //ui update for this property
            }
        }
        public bool ProcessAsModFile { get; set; }
        public string UnpackedSingleFilename { get; set; }
        public string ALOTMainPackedFilename { get; set; }
        public string TorrentFilename { get; set; }
        public string ALOTArchiveInFilePath { get; set; }
        public string Author { get; set; }
        public string FriendlyName { get; set; }
        public bool Game_ME1 { get; set; }
        public bool Game_ME2 { get; set; }
        public bool Game_ME3 { get; set; }
        public string Filename { get; set; }
        public string Tooltipname { get; set; }
        public string DownloadLink { get; set; }
        public List<string> Duplicates { get; set; }
        public bool IsModManagerMod { get; set; }
        public List<ExtractionRedirect> ExtractionRedirects { get; set; }

        public List<PackageFile> PackageFiles { get; set; }
        public List<ChoiceFile> ChoiceFiles { get; set; }
        public List<ZipFile> ZipFiles { get; set; }
        public List<CopyFile> CopyFiles { get; set; }

        public string DownloadAssistantString => " - File title: " + Tooltipname;

        //public bool Ready
        //{

        //    get { return m_ready; }
        //    set
        //    {
        //        m_ready = value;
        //        OnPropertyChanged("LeftBlockColor"); //ui update for tihs property
        //        OnPropertyChanged("ReadyIconPath");
        //        OnPropertyChanged("Ready");
        //    }
        //}

        public bool UserFile { get; internal set; }
        public string UserFilePath { get; internal set; }
        public bool MEUITM { get; internal set; }
        //public bool Staged { get; internal set; }
        public bool Building { get; internal set; }
        public long FileSize { get; internal set; }
        public string BuildID { get; internal set; }
        public string FileMD5 { get; internal set; }
        public string UnpackedFileMD5 { get; set; }
        public long UnpackedFileSize { get; set; }
        public bool Optional { get; internal set; }
        private bool _enabled;
        public bool CopyDirectly { get; internal set; }

        //public bool Enabled
        //{
        //    get { return _enabled; }
        //    internal set
        //    {
        //        _enabled = value;
        //        OnPropertyChanged("LeftBlockColor"); //ui update for tihs property
        //        OnPropertyChanged("ReadyIconPath"); //ui update for tihs property
        //    }
        //}

        public bool AlreadyInstalled { get; set; }

        public Color LeftBlockColor
        {
            get
            {
                if ((ALOTUpdateVersion > 0 || ALOTVersion > 0 || MEUITM) && State >= FileState.Ready)
                {
                    //Get current info
                    ALOTVersionInfo info = null;
                    if (Game_ME1)
                    {
                        info = MainWindow.CURRENTLY_INSTALLED_ME1_ALOT_INFO;
                    }
                    else if (Game_ME2)
                    {
                        info = MainWindow.CURRENTLY_INSTALLED_ME2_ALOT_INFO;
                    }
                    else
                    {
                        info = MainWindow.CURRENTLY_INSTALLED_ME3_ALOT_INFO;
                    }

                    //Major Upgrade, including on unknown versions
                    if (info != null && ALOTVersion > 0)
                    {
                        if (ALOTVersion != info.ALOTVER)
                        {
                            //Disabled
                            return Color.FromRgb((byte)0x60, (byte)0x60, (byte)0x60);
                        }

                        if (ALOTUpdateVersion > 0 && ALOTMainVersionRequired != info.ALOTVER)
                        {
                            return Color.FromRgb((byte)0x60, (byte)0x60, (byte)0x60);
                        }
                    }
                }

                if (State >= FileState.Ready)
                {
                    return Color.FromRgb((byte)0x31, (byte)0xae, (byte)0x90);
                }
                else if (IsDisabled)
                {
                    //Disabled
                    return Color.FromRgb((byte)0x60, (byte)0x60, (byte)0x60);
                }
                else
                {
                    return Color.FromRgb((byte)0xd9, (byte)0x22, (byte)0x44);
                }
            }

        }

        public bool IsDisabled => State == FileState.AutoDisabled || State == FileState.ManualDisabled;

        public int MEUITMVer { get; internal set; }
        public bool InstallME1DLCASI { get; internal set; }
        public bool TrackTelemetry { get; internal set; }

        private void OnPropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(propertyName));
        }

        public override string ToString()
        {
            return FriendlyName;
        }

        internal void SetWorking()
        {
            ReadyIconPath = "images/workingicon.png";
        }
        internal void SetError()
        {
            ReadyIconPath = "images/redx_large.png";
        }
        internal bool IsInErrorState()
        {
            return ReadyIconPath == "images/redx_large.png";
        }
        internal void SetIdle()
        {
            ReadyIconPath = null;
        }

        internal bool IsCurrentlySingleFile()
        {
            if (IsReady)
            {
                if (!UserFile)
                {
                    if (UnpackedSingleFilename != null && File.Exists(Path.Combine(MainWindow.DOWNLOADED_MODS_DIRECTORY, UnpackedSingleFilename)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public bool IsReady => State >= FileState.Ready;

        internal string GetFile()
        {
            if (!UserFile)
            {
                if (UnpackedSingleFilename != null && File.Exists(Path.Combine(MainWindow.DOWNLOADED_MODS_DIRECTORY, UnpackedSingleFilename)))
                {
                    return Path.Combine(MainWindow.DOWNLOADED_MODS_DIRECTORY, UnpackedSingleFilename);
                }

                if (File.Exists(Path.Combine(MainWindow.DOWNLOADED_MODS_DIRECTORY, Filename)))
                {
                    return Path.Combine(MainWindow.DOWNLOADED_MODS_DIRECTORY, Filename);
                }
            }
            else
            {

                //if (File.Exists(UserFilePath))
                //{
                return UserFilePath;
                //}
            }

            return null;
        }
    }

    public class ExtractionRedirect
    {
        public string RelativeDestinationDirectory { get; set; }
        public string ArchiveRootPath { get; set; }
        public string OptionalRequiredDLC { get; set; }
        public string OptionalAnyDLC { get; set; }
        public bool IsDLC { get; internal set; }
        public string ModVersion { get; internal set; }
        public string LoggingName { get; internal set; }
        public string OptionalRequiredFiles { get; set; }
        public string OptionalRequiredFilesSizes { get; set; }
    }
}
