using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlotAddOnGUI.classes
{
    class ME3Constants
    {
        private static List<string> _me3foldernames;
        /// <summary>
        ///  Gets the list of standard folders in the DLC folder. Includes the __metadata directory.
        /// </summary>
        /// <returns> Arraylist of strings of things like DLC_CON_MP1 etc. Does not include DLC_TESTPATCH as that is not supposed to be here (should not be unpacked).</returns>
        public static List<String> getStandardDLCFolders()
        {
            if (_me3foldernames == null)
            {
                List<String> foldernames = new List<String>();
                foldernames.Add("DLC_CON_MP1");
                foldernames.Add("DLC_CON_MP2");
                foldernames.Add("DLC_CON_MP3");
                foldernames.Add("DLC_CON_MP4");
                foldernames.Add("DLC_CON_MP5");
                foldernames.Add("DLC_UPD_Patch01");
                foldernames.Add("DLC_UPD_Patch02");
                foldernames.Add("DLC_HEN_PR");
                foldernames.Add("DLC_CON_END");
                foldernames.Add("DLC_EXP_Pack001");
                foldernames.Add("DLC_EXP_Pack002");
                foldernames.Add("DLC_EXP_Pack003");
                foldernames.Add("DLC_EXP_Pack003_Base");
                foldernames.Add("DLC_CON_APP01");
                foldernames.Add("DLC_CON_GUN01");
                foldernames.Add("DLC_CON_GUN02");
                foldernames.Add("DLC_CON_DH1");
                foldernames.Add("DLC_OnlinePassHidCE");
                foldernames.Add("__metadata"); //don't delete
                _me3foldernames = foldernames;
            }
            return _me3foldernames;
        }

        /// <summary>
        /// Gets ME3 DLC Path.
        /// </summary>
        /// <returns></returns>
        public static string GetDLCPath()
        {
            string path = Utilities.GetGamePath(3);
            return (path != null ) ? Path.Combine(path, @"BIOGame\\DLC") : null;
        }
    }
}