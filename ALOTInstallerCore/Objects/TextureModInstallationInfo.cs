namespace ALOTInstallerCore.Objects
{
    /// <summary>
    /// Describes version information for a texture mod and/or installation
    /// </summary>
    public class TextureModInstallationInfo
    {
        public short ALOTVER;
        public byte ALOTUPDATEVER;
        public byte ALOTHOTFIXVER;
        public int MEUITMVER;
        public int ALOT_INSTALLER_VERSION_USED;
        public int MEM_VERSION_USED;
        /// <summary>
        /// Creates a installation information object, with the information about what was used to install it.
        /// </summary>
        /// <param name="ALOTVersion"></param>
        /// <param name="ALOTUpdaterVersion"></param>
        /// <param name="ALOTHotfixVersion"></param>
        /// <param name="MEUITMVersion"></param>
        /// <param name="memVersionUsed"></param>
        /// <param name="alotInstallerVersionUsed"></param>
        public TextureModInstallationInfo(short ALOTVersion, byte ALOTUpdaterVersion, byte ALOTHotfixVersion, int MEUITMVersion, short memVersionUsed, short alotInstallerVersionUsed)
        {
            this.ALOTVER = ALOTVersion;
            this.ALOTUPDATEVER = ALOTUpdaterVersion;
            this.ALOTHOTFIXVER = ALOTHotfixVersion;
            this.MEUITMVER = MEUITMVersion;
            this.MEM_VERSION_USED = memVersionUsed;
            this.ALOT_INSTALLER_VERSION_USED = alotInstallerVersionUsed;
        }

        /// <summary>
        /// Creates a installation information object, without information about what was used to install it.
        /// </summary>
        /// <param name="ALOTVersion"></param>
        /// <param name="ALOTUpdaterVersion"></param>
        /// <param name="ALOTHotfixVersion"></param>
        /// <param name="MEUITMVersion"></param>
        public TextureModInstallationInfo(short ALOTVersion, byte ALOTUpdaterVersion, byte ALOTHotfixVersion, int MEUITMVersion)
        {
            this.ALOTVER = ALOTVersion;
            this.ALOTUPDATEVER = ALOTUpdaterVersion;
            this.ALOTHOTFIXVER = ALOTHotfixVersion;
            this.MEUITMVER = MEUITMVersion;
        }

        public override string ToString()
        {
            return $"ALOTVer {ALOTVER}.{ALOTUPDATEVER}.{ALOTHOTFIXVER}, MEUITM v{MEUITMVER}";
        }
    }
}