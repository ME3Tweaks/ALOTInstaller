using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using ALOTInstallerCore.Objects;

namespace ALOTInstallerCore.ModManager.Services
{
    [Localizable(false)]
    public class ThirdPartyServices
    {
        /// <summary>
        /// Looks up information about a DLC mod through the third party identification service
        /// </summary>
        /// <param name="dlcName"></param>
        /// <param name="game">Game to look in database for</param>
        /// <returns>Third party mod info about dlc folder, null if not found</returns>
        public static ThirdPartyModInfo GetThirdPartyModInfo(string dlcName, Enums.MEGame game)
        {
            if (ThirdPartyIdentificationService.ModDatabase == null) return null; //Not loaded
            if (ThirdPartyIdentificationService.ModDatabase.TryGetValue(game.ToString(), out var infosForGame))
            {
                if (infosForGame.TryGetValue(dlcName, out var info))
                {
                    return info;
                }
            }

            return null;
        }

        public class ThirdPartyModInfo : INotifyPropertyChanged
        {
            /// <summary>
            /// Denotes that this TPMI object represents a preview object (such as in Starter Kit)
            /// </summary>
            public bool IsPreview { get; internal set; }

            /// <summary>
            /// Denotes this TPMI object is selected in a listbox. (UI only)
            /// </summary>
            public bool IsSelected { get; set; }

            public string dlcfoldername { get; set; } //This is also the key into the TPMIS dictionary. 
            public string modname { get; set; }
            public string moddev { get; set; }
            public string modsite { get; set; }
            public string moddesc { get; set; }
            public string mountpriority { get; set; }
            public string modulenumber { get; set; } //ME2 only
            public string preventimport { get; set; }
            public string updatecode { get; set; } //has to be string I guess

            /// <summary>
            /// Do not use this attribute, use IsOutdated instead.
            /// </summary>
            public string outdated { get; set; }

            public bool IsOutdated => string.IsNullOrWhiteSpace(outdated) ? true : int.Parse(outdated) != 0;
            public bool PreventImport => preventimport == "1" ? true : false;

            public int MountPriorityInt => string.IsNullOrWhiteSpace(mountpriority) ? 0 : int.Parse(mountpriority);
            public string StarterKitString => $"{MountPriorityInt} - {modname}{(modulenumber != null ? " - Module # " + modulenumber : "")}"; //not worth localizing

            public event PropertyChangedEventHandler PropertyChanged;
        }

        internal static List<ThirdPartyModInfo> GetThirdPartyModInfosByModuleNumber(int modDLCModuleNumber)
        {
            if (ThirdPartyIdentificationService.ModDatabase == null) return new List<ThirdPartyModInfo>(); //Not loaded
            var me2Values = ThirdPartyIdentificationService.ModDatabase["ME2"];
            return me2Values.Where(x => x.Value.modulenumber == modDLCModuleNumber.ToString()).Select(x => x.Value).ToList();
        }

        internal static List<ThirdPartyModInfo> GetThirdPartyModInfosByMountPriority(Enums.MEGame game, int modMountPriority)
        {
            if (ThirdPartyIdentificationService.ModDatabase == null) return new List<ThirdPartyModInfo>(); //Not loaded
            var gameValues = ThirdPartyIdentificationService.ModDatabase[game.ToString()];
            return gameValues.Where(x => x.Value.MountPriorityInt == modMountPriority).Select(x => x.Value).ToList();
        }
    }
}
