using System.Collections.Generic;
using ALOTInstallerCore.ModManager.Objects;
using ALOTInstallerCore.Objects.Manifest;

namespace ALOTInstallerCore.Objects
{
    /// <summary>
    /// Contains information to pass to the builder
    /// </summary>
    public class InstallOptionsPackage
    {
        public GameTarget InstallTarget { get; set; }
        /// <summary>
        /// List of all installer files. The builder will determine what files are applicable out of this list and this list will be refined once staging is done.
        /// This list can change depending on the mode.
        /// </summary>
        public List<InstallerFile> FilesToInstall { get; set; }
        public bool InstallALOT { get; set; }
        public bool InstallALOTUpdate { get; set; }
        public bool InstallAddons { get; set; }
        public bool InstallMEUITM { get; set; }
        public bool InstallUserfiles { get; set; }

        /// <summary>
        /// The mode to run the installer in
        /// </summary>
        public ManifestMode InstallerMode { get; set; }

        /// <summary>
        /// Indicates that we should repack game files on install
        /// </summary>
        public bool RepackGameFiles { get; set; } = true; //Default to true. Might change later
        /// <summary>
        /// Installs 2K instead of 4K lods at end of install. 2K lods use significantly less memory at the cost of less visual fidelity.
        /// </summary>
        public bool Limit2K { get; set; }
        /// <summary>
        /// Will attempt to import unpacked files when they are available. Packed versions will be deleted after import.
        /// </summary>
        public bool ImportNewlyUnpackedFiles { get; set; }
        /// <summary>
        /// DEBUG ONLY: Skip main install block and simulate OK install
        /// </summary>
        public bool DebugNoInstall { get; set; }
    }
}
