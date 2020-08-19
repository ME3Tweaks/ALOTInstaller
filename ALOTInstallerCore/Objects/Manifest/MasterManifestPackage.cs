using System.Collections.Generic;

namespace ALOTInstallerCore.Objects.Manifest
{
    /// <summary>
    /// Describes a support mode of operation, which is used to determine the files for install and the rules applied for that mode
    /// </summary>
    public enum ManifestMode
    {
        /// <summary>
        /// No manifest. Install whatever you want, but you get to deal with the side effects
        /// </summary>
        Free,
        /// <summary>
        /// MEUITM manifest. Installs only MEUITM with MEUITM defaults (+ user files)
        /// </summary>
        MEUITM,
        /// <summary>
        /// ALOT manifest. Applies installation using ALOT rules for version upgrades
        /// </summary>
        ALOT,
    }

    /// <summary>
    /// Global overall manifest object, which contains the manifest mapping for each mode
    /// </summary>
    public class MasterManifestPackage
    {
        /// <summary>
        /// List of mirrors for installer music pack
        /// </summary>
        public List<string> MusicPackMirrors = new List<string>();

        /// <summary>
        /// Mapping of modes to their manifest packages
        /// </summary>
        public Dictionary<ManifestMode, ManifestModePackage> ManifestModePackageMappping = new Dictionary<ManifestMode, ManifestModePackage>();
        
        /// <summary>
        /// List of ME2 DLC foldernames that are known to have bad texture exports that must be fixed prior to install
        /// </summary>
        public List<string> ME3DLCRequiringTextureExportFixes = new List<string>();

        /// <summary>
        /// List of ME3 DLC foldernames that are known to have bad texture exports that must be fixed prior to install
        /// </summary>
        public List<string> ME2DLCRequiringTextureExportFixes = new List<string>();

        /// <summary>
        /// Indicates that the manifest was loaded from disk rather than the live version. May indicate network issue
        /// </summary>
        public bool UsingBundled { get; internal set; }
    }
}
