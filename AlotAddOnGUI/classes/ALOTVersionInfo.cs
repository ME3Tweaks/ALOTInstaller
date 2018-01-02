namespace AlotAddOnGUI.classes
{
    public class ALOTVersionInfo
    {
        public short ALOTVER;
        public byte ALOTUPDATEVER;
        public byte ALOTHOTFIXVER;
        public int MEUITMVER;

        public ALOTVersionInfo(short ALOTVersion, byte ALOTUpdaterVersion, byte ALOTHotfixVersion, int MEUITMVersion)
        {
            this.ALOTVER = ALOTVersion;
            this.ALOTUPDATEVER = ALOTUpdaterVersion;
            this.ALOTHOTFIXVER = ALOTHotfixVersion;
            this.MEUITMVER = MEUITMVersion;
        }
    }
}