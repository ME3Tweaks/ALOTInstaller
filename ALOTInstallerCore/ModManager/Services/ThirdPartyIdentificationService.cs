using System;
using System.Collections.Generic;
using System.Text;
using ALOTInstallerCore.Helpers;

namespace ALOTInstallerCore.ModManager.Services
{
    public class ThirdPartyIdentificationService
    {
        /// <summary>
        /// Accesses the third party identification server. Key is the game enum as a string, results are dictionary of DLCName => Info.
        /// </summary>
        public static Dictionary<string, CaseInsensitiveDictionary<ThirdPartyServices.ThirdPartyModInfo>> ModDatabase;
    }
}
