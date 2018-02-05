using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlotAddOnGUI.classes
{
    class IniSettingsHandler
    {
        public static string GetConfigIniPath(int game)
        {
            switch (game)
            {
                case 1:
                    return Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\BioWare\Mass Effect\Config\BIOEngine.ini";
                case 2:
                    return Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\BioWare\Mass Effect 2\BIOGame\Config\GamerSettings.ini";
                case 3:
                    return Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\BioWare\Mass Effect 3\BIOGame\Config\GamerSettings.ini";
            }
            return null;
        }
    }
}
