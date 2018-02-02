using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

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

        public bool IsRequiredFile
        {
            get { return ALOTVersion > 0 || ALOTUpdateVersion > 0; }
        }
        public bool IsALOTUpdate
        {
            get { return ALOTUpdateVersion > 0; }
        }
        private string _readystatustext;
        private string _readyiconpath;

        public string ReadyIconPath
        {
            get
            {
                if (_readyiconpath != null)
                {
                    return _readyiconpath;
                }
                else if (ALOTUpdateVersion > 0 || ALOTVersion > 0)
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
                    if (ALOTVersion > 0 && info != null && ALOTVersion > info.ALOTVER)
                    {
                        return "images/greycheckmark.png";
                    }
                }
                //Not ALOT
                if (Ready && Enabled)
                {
                    return "images/greencheckmark.png";
                }
                else if (Ready && !Enabled)
                {
                    return "images/greycheckmark.png";
                }
                else
                {
                    return "images/orangedownload.png";
                }
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
                if (_readystatustext != null)
                {
                    return _readystatustext;
                }
                if (!Enabled)
                {
                    return "Disabled";
                }
                if (Staged)
                {
                    return "File staged for installation";
                }
                if (ALOTUpdateVersion > 0 || ALOTVersion > 0)
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

                    //Major Upgrade, including on unknown versinos
                    if (ALOTVersion > 0)

                    {
                        if (info != null)
                        {
                            if (ALOTVersion > info.ALOTVER)
                            {
                                return "Restore to unmodified to install upgrade";
                            }
                            if (ALOTVersion == info.ALOTVER)
                            {
                                return "Already installed";
                            }
                            if (ALOTVersion == info.ALOTVER)
                            {
                                return "Newer version of ALOT Main already installed";
                            }
                        }
                        else
                        {
                            return "ALOT main file imported, will be installed";
                        }
                    }

                    if (ALOTUpdateVersion > 0)
                    {
                        if (info != null)
                        {
                            if (ALOTMainVersionRequired == info.ALOTVER)
                            {
                                if (ALOTUpdateVersion > info.ALOTUPDATEVER)
                                {
                                    return "ALOT update imported, will be applied";
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
                            return "ALOT update imported, will be installed";
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
                return "Addon file is imported";
            }
            set
            {
                _readystatustext = value;
                OnPropertyChanged("ReadyStatusText");
            }
        }
        public bool ProcessAsModFile { get; set; }
        public string UnpackedSingleFilename { get; set; }
        public string Author { get; set; }
        public string FriendlyName { get; set; }
        public bool Game_ME1 { get; set; }
        public bool Game_ME2 { get; set; }
        public bool Game_ME3 { get; set; }
        public string Filename { get; set; }
        public string Tooltipname { get; set; }
        public string DownloadLink { get; set; }
        public List<String> Duplicates { get; set; }
        public List<PackageFile> PackageFiles { get; set; }
        public string DownloadAssistantString
        {
            get
            {

                return " - File title: " + Tooltipname;
            }
        }

        public bool Ready
        {

            get { return m_ready; }
            set
            {
                m_ready = value;
                OnPropertyChanged("LeftBlockColor"); //ui update for tihs property
                OnPropertyChanged("ReadyIconPath");
                OnPropertyChanged("Ready");
            }
        }

        public bool UserFile { get; internal set; }
        public string UserFilePath { get; internal set; }
        public bool MEUITM { get; internal set; }
        public bool Staged { get; internal set; }
        public bool Building { get; internal set; }
        public long FileSize { get; internal set; }
        public string BuildID { get; internal set; }
        public string FileMD5 { get; internal set; }
        public bool Optional { get; internal set; }
        private bool _enabled;
        public bool Enabled
        {
            get { return _enabled; }
            internal set
            {
                _enabled = value;
                OnPropertyChanged("LeftBlockColor"); //ui update for tihs property
                OnPropertyChanged("ReadyIconPath"); //ui update for tihs property
            }
        }

        public Color LeftBlockColor
        {
            get
            {
                if (ALOTUpdateVersion > 0 || ALOTVersion > 0)
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
                    if (ALOTVersion > 0 && info != null && ALOTVersion > info.ALOTVER)
                    {
                        //Disabled
                        return Color.FromRgb((byte)0x60, (byte)0x60, (byte)0x60);
                    }
                }

                if (Ready && Enabled)
                {
                    return Color.FromRgb((byte)0x31, (byte)0xae, (byte)0x90);
                }
                else if (Ready)
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
        internal void SetIdle()
        {
            ReadyIconPath = null;
        }

        internal bool IsCurrentlySingleFile()
        {
            if (Ready)
            {
                if (!UserFile)
                {
                    if (UnpackedSingleFilename != null && File.Exists(MainWindow.EXE_DIRECTORY + "Downloaded_Mods\\" + UnpackedSingleFilename))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
        internal string GetFile()
        {
            if (Ready)
            {
                if (!UserFile)
                {
                    if (UnpackedSingleFilename != null && File.Exists(MainWindow.EXE_DIRECTORY + "Downloaded_Mods\\" + UnpackedSingleFilename))
                    {
                        return MainWindow.EXE_DIRECTORY + "Downloaded_Mods\\" + UnpackedSingleFilename;
                    }

                    if (File.Exists(MainWindow.EXE_DIRECTORY + "Downloaded_Mods\\" + Filename))
                    {
                        return MainWindow.EXE_DIRECTORY + "Downloaded_Mods\\" + Filename;
                    }
                }
                else
                {
                    if (File.Exists(UserFilePath))
                    {
                        return UserFilePath;
                    }
                }
            }

            return null;
        }



    }
}
