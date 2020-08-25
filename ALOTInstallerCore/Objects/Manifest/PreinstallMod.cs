using System.Collections.Generic;
using System.ComponentModel;

namespace ALOTInstallerCore.Objects.Manifest
{
    /// <summary>
    /// Predefined mod that will be installed after build but before installation of textures.
    /// This is only used for ALOV mods currently and should not be really be expanded (use Mod Manager instead)
    /// </summary>
    public class PreinstallMod : ManifestFile, INotifyPropertyChanged //this must be here to make fody run on this
    {
        /// <summary>
        /// List of redirections of files from extracted path => game path
        /// </summary>
        public List<ExtractionRedirect> ExtractionRedirects { get; set; }
        /// <summary>
        /// A mutual exclusive group option. If multiple files are ready at install time with same option group,
        /// a dialog must be shown so the user can pick which one to use. Doing more than one will waste disk space and time
        /// </summary>
        public string OptionGroup { get; set; }
        public override string Category => "ALOV";

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
