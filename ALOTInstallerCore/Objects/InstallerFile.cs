using System;
using System.Collections.Generic;
using System.Text;
using AlotAddOnGUI.classes;

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
    public abstract class InstallerFile
    {
        /// <summary>
        /// Games this file is applicable to
        /// </summary>
        public ApplicableGame ApplicableGames { get; set; }
        /// <summary>
        /// Information about this file, if it is ALOT. If it is an update, the major and minor versions will be set.
        /// </summary>
        public TextureModInstallationInfo AlotVersionInfo { get; set; }
        /// <summary>
        /// If this file is required to be in the Ready state to begin build step
        /// </summary>
        public bool IsReadyRequiredForBuild { get; set; }
    }
}
