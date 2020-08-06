namespace ALOTInstallerCore.Objects.Manifest
{
    /// <summary>
    /// A file that is part of a manifest file, after extraction. These files are extracted from their source file and then staged for building into the addon. 
    /// </summary>
    public class PackageFile
    {
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
        /// This file should be deleted after extracting the whole archive as it is not used
        /// </summary>
        public bool Delete { get; set; }
        public string TPFSource { get; set; }
        public ApplicableGame ApplicableGames { get; set; }
        /// <summary>
        /// If this file has been processed (extracted and moved for staging/install)
        /// </summary>
        public bool Processed { get; set; }
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
    }
}