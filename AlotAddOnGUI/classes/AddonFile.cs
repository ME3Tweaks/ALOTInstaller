using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
                } else
                {
                    if (Ready)
                    {
                        return "images/greencheckmark.png";
                    }
                    else
                    {
                        return "images/orangedownload.png";
                    }
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
                                    return "ALOT Update imported, will be applied";
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
                            return "ALOT Update imported, will be installed";
                        }
                    }
                }
                if (UserFile)
                {
                    return "User file is ready for processing";
                }
                else
                {
                    return "Addon file is imported";
                }
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

        public bool Ready
        {

            get { return m_ready; }
            set
            {
                m_ready = value;
                OnPropertyChanged("ReadyIconPath");
                OnPropertyChanged("Ready");
            }
        }

        public bool UserFile { get; internal set; }
        public string UserFilePath { get; internal set; }

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
    }
}
