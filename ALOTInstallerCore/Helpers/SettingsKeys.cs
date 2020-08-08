using System.Collections.Generic;

namespace ALOTInstallerCore.Helpers
{
    public class SettingsKeys
    {
        public enum SettingKeys
        {
            DebugLogging,
            DontForceUpgrades,
            TextureLibraryDirectory,
            RepackGameFilesME2,
            RepackGameFilesME3,
            ImportAsMove,
            BetaMode,
            LastBetaAdvertisement,
            DownloadsFolder,
            BuildLocation
        }

        public static Dictionary<SettingKeys, string> SettingsKeyMapping = new Dictionary<SettingKeys, string>()
        {
            {SettingKeys.DebugLogging,"DebugLogging" },
            {SettingKeys.DontForceUpgrades,"DontForceUpgrades"},
            {SettingKeys.TextureLibraryDirectory,"LibraryDir"},
            {SettingKeys.RepackGameFilesME2,"RepackGameFiles"},
            {SettingKeys.RepackGameFilesME3,"RepackGameFilesME3"},
            {SettingKeys.ImportAsMove,"ImportAsMove"},
            {SettingKeys.BetaMode,"BetaMode"},
            {SettingKeys.LastBetaAdvertisement,"LastBetaAdvertisement"},
            {SettingKeys.DownloadsFolder,"DownloadsFolder"},
            {SettingKeys.BuildLocation,"BuildLocation"},
        };

    }
}
