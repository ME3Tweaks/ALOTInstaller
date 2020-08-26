using System;
using System.Collections.Generic;

namespace ALOTInstallerCore.Objects
{
    /// <summary>
    /// Describes version information for a texture mod and/or installation
    /// </summary>
    public class TextureModInstallationInfo
    {
        /// <summary>
        /// Major version of ALOT, e.g. 11
        /// </summary>
        public short ALOTVER;
        /// <summary>
        /// Update version of ALOT, e.g. the .2 of 11.2
        /// </summary>
        public byte ALOTUPDATEVER;
        /// <summary>
        /// Hotfix version for ALOT. This attribute has not been used and may never be
        /// </summary>
        public byte ALOTHOTFIXVER;
        /// <summary>
        /// Version of MEUITM, such as 2
        /// </summary>
        public int MEUITMVER;
        /// <summary>
        /// What the build number of the installer that was used for this installation was. This is the third set of digits in the version number (e.g. 586)
        /// </summary>
        public short ALOT_INSTALLER_VERSION_USED;
        /// <summary>
        /// The version of MEM that was used to perform the installation of the textures
        /// </summary>
        public short MEM_VERSION_USED;
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

        private TextureModInstallationInfo()
        {

        }

        /// <summary>
        /// Calculates the maximum version numbers between two TextureModInstallationInfo objects and returns the result.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public TextureModInstallationInfo MergeWith(TextureModInstallationInfo other)
        {
            TextureModInstallationInfo tmii = new TextureModInstallationInfo();
            tmii.ALOTVER = Math.Max(ALOTVER, other.ALOTVER);
            tmii.ALOTUPDATEVER = Math.Max(ALOTUPDATEVER, other.ALOTUPDATEVER);
            tmii.ALOTHOTFIXVER = Math.Max(ALOTHOTFIXVER, other.ALOTHOTFIXVER);
            tmii.MEUITMVER = Math.Max(MEUITMVER, other.MEUITMVER);
            return tmii;
        }

        /// <summary>
        /// Creates a installation information object, without information about what was used to install it.
        /// </summary>
        /// <param name="ALOTVersion"></param>
        /// <param name="ALOTUpdateVersion"></param>
        /// <param name="ALOTHotfixVersion"></param>
        /// <param name="MEUITMVersion"></param>
        public TextureModInstallationInfo(short ALOTVersion, byte ALOTUpdateVersion, byte ALOTHotfixVersion, int MEUITMVersion)
        {
            this.ALOTVER = ALOTVersion;
            this.ALOTUPDATEVER = ALOTUpdateVersion;
            this.ALOTHOTFIXVER = ALOTHotfixVersion;
            this.MEUITMVER = MEUITMVersion;
        }

        public TextureModInstallationInfo(TextureModInstallationInfo alotVersionInfo)
        {
        }

        public override string ToString()
        {
            string str = "";
            if (IsNotVersioned)
            {
                return "Non MEUITM/ALOT textures installed";
            }

            if (ALOTVER > 0)
            {
                str += $"ALOT {ALOTVER}.{ALOTUPDATEVER}";
            }

            if (MEUITMVER > 0)
            {
                if (str != "")
                {
                    str += ", ";
                }

                str += $"MEUITM v{MEUITMVER}";
            }

            return str;
        }

        /// <summary>
        /// Returns if this object doesn't represent an actual ALOT/MEUITM installation (no values set)
        /// </summary>
        /// <returns></returns>
        public bool IsNotVersioned => ALOTVER == 0 && ALOTHOTFIXVER == 0 & ALOTUPDATEVER == 0 && MEUITMVER == 0;

        /// <summary>
        /// Calculates an installation marker based on the existing and installed file sets
        /// </summary>
        /// <param name="existing"></param>
        /// <param name="packageFilesToInstall"></param>
        /// <returns></returns>
        public static TextureModInstallationInfo CalculateMarker(TextureModInstallationInfo existing, List<InstallerFile> packageFilesToInstall)
        {
            TextureModInstallationInfo final = existing ?? new TextureModInstallationInfo();
            foreach (var v in packageFilesToInstall)
            {
                final = final.MergeWith(v.AlotVersionInfo);
            }
            return final;
        }

        public Version ToVersion()
        {
            return new Version(ALOTVER, ALOTUPDATEVER, ALOTHOTFIXVER, MEUITMVER);
        }

        public static bool operator <(TextureModInstallationInfo first, TextureModInstallationInfo second)
        {
            return first.ToVersion().CompareTo(second.ToVersion()) < 0;
        }

        public static bool operator >(TextureModInstallationInfo first, TextureModInstallationInfo second)
        {
            return first.ToVersion().CompareTo(second.ToVersion()) > 0;
        }
    }
}