using System.Diagnostics;

namespace ALOTInstallerCore.Objects.Manifest
{
    [DebuggerDisplay("PackageFile {SourceName}, Transient={Transient}, MoveDirectory={MoveDirectly}, CopyDirectly={CopyDirectly}, Processed={Processed}")]
    /// <summary>
    /// A file that is part of a manifest file, after extraction. These files are extracted from their source file and then staged for building into the addon. 
    /// </summary>
    public class PackageFile
    {
        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="x"></param>
        public PackageFile(PackageFile source)
        {
            Transient = source.Transient;
            SourceName = source.SourceName;
            MoveDirectly = source.MoveDirectly;
            CopyDirectly = source.CopyDirectly;
            Processed = source.Processed;
            ApplicableGames = source.ApplicableGames;
            TPFSource = source.TPFSource;
        }

        public PackageFile() { }

        /// <summary>
        /// Transient Package Files only exist in the PackageFiles list of a manifest file for a single installation session and are added from the selected list of ChoiceFiles. They are temporary package files and are internally removed from an <c>InstallerFile</c>'s PackageFiles list when staging begins.
        /// </summary>
        public bool Transient { get; set; }
        /// <summary>
        /// Filename of this singular file
        /// </summary>
        public string SourceName { get; set; }
        /// <summary>
        /// Destination filename
        /// </summary>
        public string DestinationName { get; set; }
        /// <summary>
        /// Move this file to the staging directory
        /// </summary>
        public bool MoveDirectly { get; set; }
        /// <summary>
        /// Directly copy this file to the staging directory. This is used if a texture can be applied to multiple areas
        /// </summary>
        public bool CopyDirectly { get; set; }
        /// <summary>
        /// The source TPF file this package file is contained in, if any. This will trigger a decompile of that TPF
        /// </summary>
        public string TPFSource { get; set; }
        /// <summary>
        /// Games this package file is applicable to
        /// </summary>
        public ApplicableGame ApplicableGames { get; set; }
        /// <summary>
        /// If this file has been processed (extracted and moved for staging/install)
        /// </summary>
        public bool Processed { get; set; }
        /// <summary>
        /// Title of this package file for showing to the user when this file is able to be selected
        /// </summary>
        public string ChoiceTitle { get; internal set; }

        /// <summary>
        /// Loading indicator that this is an ME3 file. On setting this, it will set the bit in ApplicableGames. Do not use this variable, use ApplicableGames instead.
        /// </summary>
        internal bool m_me3
        {
            set
            {
                if (value)
                    ApplicableGames |= ApplicableGame.ME3;
                else
                    ApplicableGames &= ~ApplicableGame.ME3;
            }
        }
        /// <summary>
        /// Loading indicator that this is an ME2 file. On setting this, it will set the bit in ApplicableGames. Do not use this variable, use ApplicableGames instead.
        /// </summary>
        internal bool m_me2
        {
            set
            {
                if (value)
                    ApplicableGames |= ApplicableGame.ME2;
                else
                    ApplicableGames &= ~ApplicableGame.ME2;
            }
        }
        /// <summary>
        /// Loading indicator that this is an ME1 file. On setting this, it will set the bit in ApplicableGames. Do not use this variable, use ApplicableGames instead.
        /// </summary>
        internal bool m_me1
        {
            set
            {
                if (value)
                    ApplicableGames |= ApplicableGame.ME1;
                else
                    ApplicableGames &= ~ApplicableGame.ME1;
            }
        }

        public override string ToString()
        {
            return $"PackageFile {SourceName}, ChoiceTitle(if any): {ChoiceTitle}, IsTransient: {Transient}";
        }
    }
}