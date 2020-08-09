using System;
using System.Collections.Generic;
using System.Text;

namespace ALOTInstallerCore.Objects.Manifest
{
    /// <summary>
    /// Defines the files and mode-specific assets for a specific mode
    /// </summary>
    public class ManifestModePackage
    {
        /// <summary>
        /// Files that are part of the mode's installation manifest. These files must be downcast to their types for accessing info on them.
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
    }
}
