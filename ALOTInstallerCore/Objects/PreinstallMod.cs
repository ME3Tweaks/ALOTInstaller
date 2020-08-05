using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Text;

namespace ALOTInstallerCore.Objects
{
    /// <summary>
    /// Predefined mod that will be installed after build but before installation of textures.
    /// This is only used for ALOV mods currently and should not be really be expanded (use Mod Manager instead)
    /// </summary>
    public class PreinstallMod : ManifestFile
    {
        public List<ExtractionRedirect> ExtractionRedirects { get; set; }

        public class ExtractionRedirect
        {
            public string RelativeDestinationDirectory { get; set; }
            public string ArchiveRootPath { get; set; }
            public string OptionalRequiredDLC { get; set; }
            public string OptionalAnyDLC { get; set; }
            public bool IsDLC { get; internal set; }
            public string ModVersion { get; internal set; }
            public string LoggingName { get; internal set; }
            public string OptionalRequiredFiles { get; set; }
            public string OptionalRequiredFilesSizes { get; set; }
        }
    }
}
