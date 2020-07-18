namespace AlotAddOnGUI.classes
{
    /// <summary>
    /// Describes version information for a texture
    /// </summary>
    public class TextureModInstallationInfo
    {
        public short ALOTVER;
        public byte ALOTUPDATEVER;
        public byte ALOTHOTFIXVER;
        public int MEUITMVER;
        public int ALOT_INSTALLER_VERSION_USED;
        public int MEM_VERSION_USED;
        public TextureModInstallationInfo(short ALOTVersion, byte ALOTUpdaterVersion, byte ALOTHotfixVersion, int MEUITMVersion, short memVersionUsed, short alotInstallerVersionUsed)
        {
            this.ALOTVER = ALOTVersion;
            this.ALOTUPDATEVER = ALOTUpdaterVersion;
            this.ALOTHOTFIXVER = ALOTHotfixVersion;
            this.MEUITMVER = MEUITMVersion;
            this.MEM_VERSION_USED = memVersionUsed;
            this.ALOT_INSTALLER_VERSION_USED = alotInstallerVersionUsed;
        }

        public override string ToString()
        {
            return $"ALOTVer {ALOTVER}.{ALOTUPDATEVER}.{ALOTHOTFIXVER}, MEUITM v{MEUITMVER}";
        }
    }
}