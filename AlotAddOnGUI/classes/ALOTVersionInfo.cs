namespace AlotAddOnGUI.classes
{
    public class ALOTVersionInfo
    {
        public short ALOTVER;
        public byte ALOTUPDATEVER;
        public byte ALOTHOTFIXVER;
        public int MEUITMVER;

        public ALOTVersionInfo(short aLOTVER, byte aLOTUPDATEVER, byte aLOTHOTFIXVER, int mEUITMVER)
        {
            this.ALOTVER = aLOTVER;
            this.ALOTUPDATEVER = aLOTUPDATEVER;
            this.ALOTHOTFIXVER = aLOTHOTFIXVER;
            this.MEUITMVER = mEUITMVER;
        }
    }
}