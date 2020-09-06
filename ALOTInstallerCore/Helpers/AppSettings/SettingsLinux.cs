#if LINUX
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using ALOTInstallerCore.ModManager.GameINI;
using ALOTInstallerCore.Objects;
using Serilog;

namespace ALOTInstallerCore.Helpers.AppSettings
{
    /// <summary>
    /// Non-windows based settings loader (settingsIni based)
    /// </summary>
    public partial class Settings
    {
        private static DuplicatingIni settingsIni;
        public static readonly string SettingsPath = Path.Combine(Locations.AppDataFolder(), "settings.ini");

        public static void Load()
        {
            settingsIni = new DuplicatingIni();
            if (File.Exists(SettingsPath))
            {
                settingsIni = DuplicatingIni.LoadIni(SettingsPath);
            }
            TextureLibraryLocation = LoadDirectorySetting(SettingsKeys.SettingKeys.TextureLibraryDirectory, @"Downloaded_Mods");
            BuildLocation = LoadDirectorySetting(SettingsKeys.SettingKeys.BuildLocation, @"Staging");
            MoveFilesWhenImporting = LoadSettingBool(SettingsKeys.SettingKeys.ImportAsMove, false);
            Telemetry = LoadSettingBool(SettingsKeys.SettingKeys.Telemetry, true);
            BetaMode = LoadSettingBool(SettingsKeys.SettingKeys.BetaMode, false);
            LastContentCheck = LoadSettingDateTime(SettingsKeys.SettingKeys.LastContentCheck, DateTime.MinValue);
            //AutoUpdateLODs = LoadSettingBool("ModManager", "AutoUpdateLODs", true);
            //WebClientTimeout = LoadSettingInt("ModManager", "WebclientTimeout", 5);
            //ModMakerControllerModOption = LoadSettingBool("ModMaker", "AutoAddControllerMixins", false);
            //ModMakerAutoInjectCustomKeybindsOption = LoadSettingBool("ModMaker", "AutoInjectCustomKeybinds", false);


            //UpdaterServiceUsername = LoadSettingString("UpdaterService", "Username", null);
            //UpdaterServiceLZMAStoragePath = LoadSettingString("UpdaterService", "LZMAStoragePath", null);
            //UpdaterServiceManifestStoragePath = LoadSettingString("UpdaterService", "ManifestStoragePath", null);

            //LogModStartup = LoadSettingBool("Logging", "LogModStartup", false);
            //LogMixinStartup = LoadSettingBool("Logging", "LogMixinStartup", false);
            //EnableTelemetry = LoadSettingBool("Logging", "EnableTelemetry", true);
            //LogModInstallation = LoadSettingBool("Logging", "LogModInstallation", false);
            //LogModMakerCompiler = LoadSettingBool("Logging", "LogModMakerCompiler", false);

            //ModLibraryPath = LoadSettingString("ModLibrary", "LibraryPath", null);

            //DeveloperMode = LoadSettingBool("UI", "DeveloperMode", false);
            //DarkTheme = LoadSettingBool("UI", "DarkTheme", false);
            Loaded = true;
        }

        private static DateTime LoadSettingDateTime(SettingsKeys.SettingKeys key, DateTime defaultValue)
        {
            var str = LoadSettingString(key, null);
            if (str == null || !long.TryParse(str, out long binTime)) return defaultValue;
            try
            {
                return DateTime.FromBinary(binTime);
            }
            catch (Exception e)
            {
                Log.Error($@"[AICORE] Error loading datetime value {binTime}: {e.Message}");
                return defaultValue;
            }
        }


        private static string LoadSettingString(SettingsKeys.SettingKeys key, string defaultValue)
        {
            var value = settingsIni["Settings"][SettingsKeys.SettingsKeyMapping[key]];
            if (string.IsNullOrWhiteSpace(value.Value)) return defaultValue;
            return value.Value;
        }


        private static bool LoadSettingBool(SettingsKeys.SettingKeys key, bool defaultValue)
        {
            var value = settingsIni["Settings"][SettingsKeys.SettingsKeyMapping[key]];
            if (string.IsNullOrWhiteSpace(value.Value) || !bool.TryParse(value.Value, out var res)) return defaultValue;
            return res;
        }

        private static string LoadDirectorySetting(SettingsKeys.SettingKeys key, string defaultSubfolder)
        {
            var dir = settingsIni["Settings"][SettingsKeys.SettingsKeyMapping[key]]?.Value;
            if (dir != null && Directory.Exists(dir))
            {
                return dir;
            }
            var path = Path.Combine(Utilities.GetExecutingAssemblyFolder(), defaultSubfolder);
            Directory.CreateDirectory(path);
            return path;
        }




        /// <summary>
        /// Saves the settings. Note this does not update the Updates/EncryptedPassword value. Returns false if commiting failed
        /// </summary>
        public static SettingsSaveResult Save(string propertyName = null)
        {
            try
            {
                if (propertyName == nameof(BuildLocation))
                    SaveSettingString(SettingsKeys.SettingKeys.BuildLocation, BuildLocation);
                if (propertyName == nameof(TextureLibraryLocation))
                    SaveSettingString(SettingsKeys.SettingKeys.TextureLibraryDirectory, TextureLibraryLocation);
                if (propertyName == nameof(BetaMode))
                    SaveSettingBool(SettingsKeys.SettingKeys.BetaMode, BetaMode);
                if (propertyName == nameof(DebugLogs))
                    SaveSettingBool(SettingsKeys.SettingKeys.DebugLogging, DebugLogs);
                if (propertyName == nameof(MoveFilesWhenImporting))
                    SaveSettingBool(SettingsKeys.SettingKeys.ImportAsMove, MoveFilesWhenImporting);
                if (propertyName == nameof(LastContentCheck))
                    SaveSettingDateTime(SettingsKeys.SettingKeys.LastContentCheck, LastContentCheck);
                File.WriteAllText(SettingsPath, settingsIni.ToString());
                return SettingsSaveResult.SAVED;
            }
            catch (UnauthorizedAccessException uae)
            {
                Log.Error("[AICORE] Unauthorized access exception:");
                uae.WriteToLog("[AICORE] ");

                return SettingsSaveResult.FAILED_UNAUTHORIZED;
            }
            catch (Exception e)
            {
                Log.Error("[AICORE] Error commiting settings:");
                e.WriteToLog("[AICORE] ");
            }

            return SettingsSaveResult.FAILED_OTHER;
        }

        private static void SaveSettingString(SettingsKeys.SettingKeys key, string value)
        {
            settingsIni["Settings"][SettingsKeys.SettingsKeyMapping[key]].Value = value;
        }

        private static void SaveSettingBool(SettingsKeys.SettingKeys key, bool value)
        {
            settingsIni["Settings"][SettingsKeys.SettingsKeyMapping[key]].Value = value.ToString();
        }

        private static void SaveSettingInt(SettingsKeys.SettingKeys key, int value)
        {
            settingsIni["Settings"][SettingsKeys.SettingsKeyMapping[key]].Value = value.ToString();
        }

        private static void SaveSettingDateTime(SettingsKeys.SettingKeys key, DateTime value)
        {
            settingsIni["Settings"][SettingsKeys.SettingsKeyMapping[key]].Value = value.ToBinary().ToString();
        }

        public static string GetBackupPath(Enums.MEGame game)
        {
            return settingsIni["BackupPaths"][game.ToString()]?.Value;
        }

        public static void SaveBackupPath(Enums.MEGame game, string path)
        {
            settingsIni["BackupPaths"][SettingsKeys.SettingsKeyMapping[Enum.Parse<SettingsKeys.SettingKeys>($"{game}BackupPath")]].Value = path;
            File.WriteAllText(SettingsPath, settingsIni.ToString());
        }
    }
}
#endif