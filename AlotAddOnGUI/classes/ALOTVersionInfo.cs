namespace AlotAddOnGUI.classes
{
    public class ALOTVersionInfo
    {
        public short ALOTVER;
        public byte ALOTUPDATEVER;
        public byte ALOTHOTFIXVER;
        public int MEUITMVER;
        public int ALOT_INSTALLER_VERSION_USED;
        public int MEM_VERSION_USED;
        public ALOTVersionInfo(short ALOTVersion, byte ALOTUpdaterVersion, byte ALOTHotfixVersion, int MEUITMVersion, short memVersionUsed, short alotInstallerVersionUsed)
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
            return "ALOTVer " + ALOTVER + "." + ALOTUPDATEVER + "." + ALOTHOTFIXVER + ", MEUITM v" + MEUITMVER;
        }
    }
}