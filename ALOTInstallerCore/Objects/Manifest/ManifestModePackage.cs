using System.Collections.Generic;

namespace ALOTInstallerCore.Objects.Manifest
{
    /// <summary>
    /// Defines the files and mode-specific assets for a specific mode
    /// </summary>
    public class ManifestModePackage
    {
        /// <summary>
        /// Files that are part of the mode's installation manifest.
        /// </summary>
        public List<ManifestFile> ManifestFiles = new List<ManifestFile>(60);

        /// <summary>
        /// The version of this mode's manifest
        /// </summary>
        public string ManifestVersion { get; set; }

        /// <summary>
        /// List of tutorials for this manifest mode
        /// </summary>
        public List<ManifestTutorial> Tutorials = new List<ManifestTutorial>();

        /// <summary>
        /// List of user supplied files for this mode
        /// </summary>
        public List<UserFile> UserFiles { get; } = new List<UserFile>();

        /// <summary>
        /// Description of this mode
        /// </summary>
        public string ModeDescription { get; set; } = "No rules. Install whatever you want"; //Defaults to 'None' description. Manifest loader will override this
    }
}
