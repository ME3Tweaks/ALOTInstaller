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

        public bool IsRequiredFile
        {
            get { return ALOTVersion > 0 || ALOTUpdateVersion > 0; }
        }
        public bool IsALOTUpdate
        {
            get { return ALOTUpdateVersion > 0; }
        }
        public bool ProcessAsModFile { get; set; }
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
                OnPropertyChanged(string.Empty);
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
    }
}
