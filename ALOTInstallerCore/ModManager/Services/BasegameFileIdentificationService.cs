using System.Collections.Generic;
using System.Linq;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.ModManager.ME3Tweaks;
using ALOTInstallerCore.ModManager.Objects;
using ALOTInstallerCore.Objects;

namespace ALOTInstallerCore.ModManager.Services
{
    public class BasegameFileIdentificationService
    {
        public static Dictionary<string, CaseInsensitiveDictionary<List<BasegameCloudDBFile>>> BasegameFileIdentificationServiceDB;

        public static bool LoadService()
        {
            BasegameFileIdentificationServiceDB = OnlineContent.FetchBasegameFileIdentificationServiceManifest();
            return BasegameFileIdentificationServiceDB != null;
        }

        /// <summary>
        /// Looks up information about a basegame file using the Basegame File Identification Service
        /// </summary>
        /// <param name="target"></param>
        /// <param name="fullfilepath"></param>
        /// <returns></returns>
        public static BasegameCloudDBFile GetBasegameFileSource(GameTarget target, string fullfilepath)
        {
            if (BasegameFileIdentificationServiceDB == null) return null; //Not loaded
            if (BasegameFileIdentificationServiceDB.TryGetValue(target.Game.ToString(), out var infosForGame))
            {
                var relativeFilename = fullfilepath.Substring(target.TargetPath.Length + 1).ToUpper();

                if (infosForGame.TryGetValue(relativeFilename, out var items))
                {
                    var md5 = Utilities.CalculateMD5(fullfilepath);
                    return items.FirstOrDefault(x => x.hash == md5); //may need adjusted if multiple mods share files
                    //return info;
                }
            }

            return null;
        }

        public class BasegameCloudDBFile
        {
            public string file { get; set; }
            public string hash { get; set; }
            public string source { get; set; }
            public string game { get; set; }
            public BasegameCloudDBFile() { }
        }
    }
}