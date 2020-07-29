using System;
using System.Collections.Generic;
using System.Text;

namespace ALOTInstallerCore.Steps.Installer
{
    /// <summary>
    /// Object that is passed from library to hosting application to indicate progress update, be it task or percent
    /// </summary>
    public class ProgressPackage
    {
        public object payload;
        public InstallStage stage;

        /// <summary>
        /// Enum that can be used to define the current stage
        /// </summary>
        public enum InstallStage
        {
            CheckMarkers,
            UnpackDLC,
            InstallMMMods,
            Prescan,
            Scan,
            InstallTextures,
            Verify,
            RemoveEmptyMips,
            CompressPackages
        }

        public ProgressPackage(InstallStage stage, object payload)
        {
            this.stage = stage;
            this.payload = payload;
        }
    }
}
