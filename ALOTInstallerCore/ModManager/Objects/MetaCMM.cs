using System.IO;
using System.Linq;

namespace ALOTInstallerCore.ModManager.Objects
{
    /// <summary>
    /// Class that represents data in _metacmm.txt files - files that describe the installed mod
    /// </summary>
    public class MetaCMM
    {
        public string ModName { get; set; }
        public string Version { get; set; }
        public string InstalledBy { get; set; }
        public string InstallerInstanceGUID { get; set; }

        public MetaCMM(string metaFile)
        {
            var lines = File.ReadAllLines(metaFile).ToList();
            int i = 0;
            foreach (var line in lines)
            {
                switch (i)
                {
                    case 0:
                        ModName = line;
                        break;
                    case 1:
                        Version = line;
                        break;
                    case 2:
                        InstalledBy = line;
                        break;
                    case 3:
                        InstallerInstanceGUID = line;
                        break;
                    default:
                        // Nothing
                        break;
                }
                i++;
            }



        }
    }
}
