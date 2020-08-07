using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using ALOTInstallerCore.Objects.Manifest;

namespace ALOTInstallerCore.Objects
{
    /// <summary>
    /// Describes what games a manifest file is applicable to. This is a bitmask as files can apply to multiple games.
    /// </summary>
    [Flags]
    public enum ApplicableGame
    {
        None = 0, // NOT USED
        ME1 = 1,
        ME2 = 2,
        ME3 = 4,
    }

    /// <summary>
    /// Recommendations that can be used on installer files to denote their importance in a UI
    /// </summary>
    public enum RecommendationType
    {
        /// <summary>
        /// No recommendation status
        /// </summary>
        None, 
        /// <summary>
        /// Strays from vanilla
        /// </summary>
        Optional, 
        /// <summary>
        /// Similar to vanilla
        /// </summary>
        Recommended,
        /// <summary>
        /// Required for installation
        /// </summary>
        Required
    }

    public abstract class InstallerFile : INotifyPropertyChanged
    {
        /// <summary>
        /// Games this file is applicable to
        /// </summary>
        public ApplicableGame ApplicableGames { get; set; } = ApplicableGame.None;

        /// <summary>
        /// Gets list of (strings) games that this file supports. Can be useful when building UI strings.
        /// </summary>
        /// <returns></returns>
        public List<string> SupportedGames()
        {
            List<string> games = new List<string>();
            if (ApplicableGames.HasFlag(ApplicableGame.ME1)) games.Add("ME1");
            if (ApplicableGames.HasFlag(ApplicableGame.ME2)) games.Add("ME2");
            if (ApplicableGames.HasFlag(ApplicableGame.ME3)) games.Add("ME3");
            return games;
        }

        /// <summary>
        /// Information about this file, if it is ALOT. If it is an update, the major and minor versions will be set.
        /// </summary>
        public TextureModInstallationInfo AlotVersionInfo { get; set; }

        /// <summary>
        /// List of sub-files that this mod 
        /// </summary>
        public List<PackageFile> PackageFiles;

        /// <summary>
        /// If this file is required to be in the Ready state to begin build step
        /// </summary>
        public bool IsReadyRequiredForBuild { get; set; }
        /// <summary>
        /// Friendly name to display
        /// </summary>
        public string FriendlyName { get; set; }
        /// <summary>
        /// Filename for the backing file. IF this is a userfile, it is the full file path.
        /// </summary>
        public string Filename { get; set; }
        /// <summary>
        /// Size of the backing file
        /// </summary>
        public long FileSize { get; internal set; }
        /// <summary>
        /// File is ready to be processed or not
        /// </summary>
        public bool Ready { get; set; }

        /// <summary>
        /// String that can be used to display the status of this manifest file.
        /// </summary>
        public string StatusText { get; set; }
        /// <summary>
        /// Indicates if this file is currently the active processing file
        /// </summary>
        public bool IsProcessing { get; set; }
        /// <summary>
        /// The priority that this file will install at. The higher the number, the later it will install, which means it will override earlier files.
        /// </summary>
        public int InstallPriority { get; set; }
        /// <summary>
        /// Internal ID that is used for output MEM files in the final directory
        /// </summary>
        public int BuildID { get; set; }

        /// <summary>
        /// Refreshes the Ready status for this file. Returns if the ready status changed due to this call.
        /// </summary>
        /// <returns></returns>
        public abstract bool UpdateReadyStatus();

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Returns the path to the file that this addon file points to - unpacked or packed, whatever the current backing file is.
        /// </summary>
        /// <returns></returns>
        public abstract string GetUsedFilepath();

        /// <summary>
        /// Resets variables used only during the build step
        /// </summary>
        public void ResetBuildVars()
        {
            foreach (var pf in PackageFiles)
            {
                pf.Processed = false;
            }
        }
    }
}
