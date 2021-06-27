using System;
using System.ComponentModel;
using System.IO;
using ALOTInstallerCore.ModManager.Services;
using ALOTInstallerCore.Objects;
using LegendaryExplorerCore.Packages;

namespace ALOTInstallerCore.ModManager.Objects
{
    public class InstalledDLCMod : INotifyPropertyChanged
    {
        private string dlcFolderPath;

        public event PropertyChangedEventHandler PropertyChanged;
        public string EnableDisableTooltip { get; set; }
        public string ModName { get; private set; }
        public string DLCFolderName { get; private set; }
        public string DLCFolderNameString { get; private set; }
        public string InstalledBy { get; private set; }
        public string Version { get; private set; }
        public string InstallerInstanceGUID { get; private set; }
        public string InstallerInstanceBuild { get; private set; }
        private MEGame game;

        private Func<InstalledDLCMod, bool> deleteConfirmationCallback;
        private Action notifyDeleted;
        
        /// <summary>
        /// Indicates that this mod was installed by ALOT Installer or Mod Manager.
        /// </summary>
        public bool InstalledByManagedSolution { get; private set; }

        public InstalledDLCMod(string dlcFolderPath, MEGame game, bool modNamePrefersTPMI)
        {
            this.dlcFolderPath = dlcFolderPath;
            this.game = game;
            DLCFolderName = DLCFolderNameString = Path.GetFileName(dlcFolderPath);
            if (ThirdPartyIdentificationService.ModDatabase != null && ThirdPartyIdentificationService.ModDatabase[game.ToString()].TryGetValue(DLCFolderName.TrimStart('x'), out var tpmi))
            {
                ModName = tpmi.modname;
            }
            else
            {
                ModName = DLCFolderName;
            }
            parseInstalledBy(DLCFolderName.StartsWith('x'), modNamePrefersTPMI);

        }

        private void parseInstalledBy(bool disabled, bool modNamePrefersTPMI)
        {
            DLCFolderNameString = DLCFolderName.TrimStart('x'); //this string is not to show M3L.GetString(M3L.string_disabled)
            var metaFile = Path.Combine(dlcFolderPath, @"_metacmm.txt");
            if (File.Exists(metaFile))
            {
                InstalledByManagedSolution = true;
                InstalledBy = "Installed by Mod Manager";
                MetaCMM mcmm = new MetaCMM(metaFile);
                if (mcmm.ModName != ModName)
                {
                    DLCFolderNameString += $@" ({ModName})";
                    if (!modNamePrefersTPMI || ModName == null)
                    {
                        ModName = mcmm.ModName;
                    }
                }

                Version = mcmm.Version;
                InstallerInstanceBuild = mcmm.InstalledBy;
                if (int.TryParse(InstallerInstanceBuild, out var _))
                {
                    InstalledBy = "Installed by Mod Manager";
                }
                else
                {
                    InstalledBy = $"Installed by Mod Manager Build {InstallerInstanceBuild}";
                }
            }
            else
            {
                InstalledBy = "Not installed by Mod Manager";
            }
            if (disabled)
            {
                DLCFolderNameString += @" - Disabled";
            }
        }

        private void TriggerPropertyChangedFor(string propertyname)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyname));
        }
    }
}
