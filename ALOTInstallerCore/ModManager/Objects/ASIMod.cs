using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.Objects;

namespace ALOTInstallerCore.ModManager.Objects
{
    /// <summary>
    /// Object containing information about an ASI mod in the ASI mod manifest
    /// </summary>
    public class ASIMod : INotifyPropertyChanged
    {
#if WPF
        private static Brush installedBrush = new SolidColorBrush(Color.FromArgb(0x33, 0, 0xFF, 0));
        private static Brush outdatedBrush = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0));
#endif
        public string DownloadLink { get; internal set; }
        public string SourceCodeLink { get; internal set; }
        public string Hash { get; internal set; }
        public string Version { get; internal set; }
        public string Author { get; internal set; }
        public string InstalledPrefix { get; internal set; }
        public string Name { get; internal set; }
        public Enums.MEGame Game { get; set; }
        public string Description { get; internal set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public bool UIOnly_Installed { get; set; }
        public bool UIOnly_Outdated { get; set; }
        public string InstallStatus => UIOnly_Outdated ? "Outdated version installed" : UIOnly_Installed ? "Installed" : "";
        public InstalledASIMod InstalledInfo { get; set; }

#if WPF
        public Brush BackgroundColor
        {
            get
            {
                if (UIOnly_Outdated)
                {
                    return outdatedBrush;
                }
                else if (UIOnly_Installed)
                {
                    return installedBrush;
                }
                else
                {
                    return null;
                }
            }
        }
#endif
    }


    /// <summary>
    /// Object describing an installed ASI file. It is not a general ASI mod object but it can be mapped to one
    /// </summary>
    public class InstalledASIMod
    {
        public InstalledASIMod(string asiFile, Enums.MEGame game)
        {
            Game = game;
            InstalledPath = asiFile;
            Filename = Path.GetFileNameWithoutExtension(asiFile);
            Hash = BitConverter.ToString(System.Security.Cryptography.MD5.Create()
                .ComputeHash(File.ReadAllBytes(asiFile))).Replace(@"-", "").ToLower();
        }

        public Enums.MEGame Game { get; }
        public string InstalledPath { get; set; }
        public string Hash { get; set; }
        public string Filename { get; set; }
    }

    public class ASIModUpdateGroup
    {
        public List<ASIMod> ASIModVersions { get; internal set; }
        public int UpdateGroupId { get; internal set; }
        public Enums.MEGame Game { get; internal set; }
        public bool IsHidden { get; set; }

        public ASIMod GetLatestVersion()
        {
            return ASIModVersions.MaxBy(x => x.Version);
        }
    }
}
