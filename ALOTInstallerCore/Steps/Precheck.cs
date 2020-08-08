using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ALOTInstallerCore.ModManager.Objects.ALOTInstallerCore.modmanager.objects;
using ALOTInstallerCore.Objects;
using ALOTInstallerCore.Startup;

namespace ALOTInstallerCore.Steps
{
    /// <summary>
    /// This class performs a precheck of the installation target to ensure it is possible to install mods against it.
    /// </summary>
    public class Precheck
    {
        /// <summary>
        /// Performs an installation precheck against the target with the listed manifest mode and list of files that will be installed
        /// </summary>
        /// <param name="target"></param>
        /// <param name="mode"></param>
        /// <param name="filesToInstall"></param>
        public static void PerformPrecheck(GameTarget target, OnlineContent.ManifestMode mode, List<InstallerFile> filesToInstall)
        {
            var pc = new Precheck()
            {
                target = target,
                mode = mode,
                filesToInstall = filesToInstall
            };

            if (!pc.checkRequiredFiles())
            {

                return;
            }
        }

        private GameTarget target { get; set; }
        private OnlineContent.ManifestMode mode { get; set; }
        private List<InstallerFile> filesToInstall { get; set; }

        private Precheck()
        {

        }

        private bool checkRequiredFiles()
        {
            if (mode == OnlineContent.ManifestMode.None) return true;
            return filesToInstall.Any(x => x.Recommendation == RecommendationType.Required);
        }
    }
}
