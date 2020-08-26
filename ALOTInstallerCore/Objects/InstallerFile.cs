using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
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
        public List<PackageFile> PackageFiles = new List<PackageFile>();
        /// <summary>
        /// Developer of this file
        /// </summary>
        public string Author { get; set; }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="source"></param>
        public InstallerFile(InstallerFile source)
        {
            this.ApplicableGames = source.ApplicableGames;
            this.PackageFiles = source.PackageFiles.Select(x => new PackageFile(x)).ToList();
            this.AlotVersionInfo = new TextureModInstallationInfo(source.AlotVersionInfo);
            this.FriendlyName = source.FriendlyName;
            this.FileSize = source.FileSize;
            this.Filename = source.Filename;
            this.Ready = false; // Must be updated by UpdateReadyStatus();
            this.InstallPriority = source.InstallPriority;
            this.Author = source.Author;
        }

        public InstallerFile() { }

        /// <summary>
        /// Friendly name to display
        /// </summary>
        public string FriendlyName { get; set; }
        /// <summary>
        /// Filename for the backing file. Do not use this variable when dealing with UserFiles
        /// </summary>
        public string Filename { get; set; }
        /// <summary>
        /// Size of the backing file
        /// </summary>
        public long FileSize { get; set; }
        /// <summary>
        /// File is ready to be processed or not
        /// </summary>
        public bool Ready { get; set; }
        /// <summary>
        /// UI category of file, such as Addon, ALOV, UserFile
        /// </summary>
        public abstract string Category { get; }

        /// <summary>
        /// String that can be used to display the status of this manifest file.
        /// </summary>
        public string StatusText { get; set; }
        /// <summary>
        /// Indicates if this file is currently the active processing file
        /// </summary>
        public bool IsProcessing { get; set; }
        /// <summary>
        /// Indicates if this file is currently waiting for processing
        /// </summary>
        public bool IsWaiting { get; set; }
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
        /// Indicates that any .mod files found in the extraction directory (or file itself) shouldn't be decompiled and instead directly staged to be compiled to .mem file (will keep mesh changes)
        /// </summary>
        public abstract bool StageModFiles { get; set; }

        /// <summary>
        /// If this file is disabled by the user and will not install. Only files that are not required can have this value set
        /// </summary>
        public bool Disabled
        {
            get;
            set;
        }
        public void OnDisabledChanged()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Self)));
        }
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

        /// <summary>
        /// Automatically called when IsWaiting is changed. This will trigger the Self variable to update, which is often bound to in the UI.
        /// </summary>
        public void OnIsWaitingChanged()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Self)));
        }
        /// <summary>
        /// Automatically called when IsProcessing is changed. This will trigger the Self variable to update, which is often bound to in the UI.
        /// </summary>
        public void OnIsProcessingChanged()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Self)));
        }



        /// <summary>
        /// Reference to self. Can be used to force data binding updates for wrapper applications. Will be property notified on changes to things such as Ready status.
        /// </summary>
        public InstallerFile Self => this;

        /// <summary>
        /// Causes the data bindings for the Self object to refresh.
        /// </summary>
        internal void NotifyStatusUpdate()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Self)));
        }
        /// <summary>
        /// Returns if this file has any package files.
        /// </summary>
        /// <returns></returns>
        public virtual bool HasAnyPackageFiles() => PackageFiles.Any();
    }
}
