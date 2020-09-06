using System.Collections.Generic;

namespace ALOTInstallerCore.Helpers.AppSettings
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
            BuildLocation,
            PlayMusic,
            Telemetry,
            LastContentCheck,
#if !WINDOWS
            ME1BackupPath,
            ME2BackupPath,
            ME3BackupPath
#endif
        }

        public static Dictionary<SettingKeys, string> SettingsKeyMapping = new Dictionary<SettingKeys, string>()
        {
            {SettingKeys.DebugLogging,"DebugLogging" },
            {SettingKeys.DontForceUpgrades,"DontForceUpgrades"}, //v2
            {SettingKeys.TextureLibraryDirectory,"LibraryDir"},
            {SettingKeys.RepackGameFilesME2,"RepackGameFiles"}, //v3
            {SettingKeys.RepackGameFilesME3,"RepackGameFilesME3"}, //v3
            {SettingKeys.ImportAsMove,"ImportAsMove"},
            {SettingKeys.BetaMode,"BetaMode"},
            {SettingKeys.LastBetaAdvertisement,"LastBetaAdvertisement"},
            {SettingKeys.DownloadsFolder,"DownloadsFolder"},
            {SettingKeys.BuildLocation,"BuildLocation"}, //v4
            {SettingKeys.PlayMusic,"PlayMusic"},
            {SettingKeys.Telemetry,"Telemetry"}, //v4
            {SettingKeys.LastContentCheck,"LastME3TweaksContentCheck"}, //v4
#if !WINDOWS
            {SettingKeys.ME1BackupPath,"ME1BackupPath"}, //v4
            {SettingKeys.ME2BackupPath,"ME2BackupPath"}, //v4
            {SettingKeys.ME3BackupPath,"ME3BackupPath"}, //v4
#endif
        };

    }
}
