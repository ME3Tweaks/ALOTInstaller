#if LINUX
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using ALOTInstallerCore.ModManager.GameINI;
using ALOTInstallerCore.Objects;
using ME3ExplorerCore.Packages;
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

        public static void Load(bool loadSettingsFolders)
        {
            settingsIni = new DuplicatingIni();
            if (File.Exists(SettingsPath))
            {
                settingsIni = DuplicatingIni.LoadIni(SettingsPath);
            }

            if (loadSettingsFolders)
            {
                TextureLibraryLocation = LoadDirectorySetting(SettingsKeys.SettingKeys.TextureLibraryDirectory, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ALOTInstaller", "Downloaded_Mods"), true, v => TextureLibrarySettingsLocation = v, v => TextureLibraryLocationExistedOnLoad = v);
                StagingLocation = LoadDirectorySetting(SettingsKeys.SettingKeys.BuildLocation, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ALOTInstaller", "Staging"), true, v => StagingSettingsLocation = v, v => StagingLocationExistedOnLoad = v);
            }

            MoveFilesWhenImporting = LoadSettingBool(SettingsKeys.SettingKeys.ImportAsMove, false);
            Telemetry = LoadSettingBool(SettingsKeys.SettingKeys.Telemetry, true);
            BetaMode = LoadSettingBool(SettingsKeys.SettingKeys.BetaMode, false);
            LastContentCheck = LoadSettingDateTime(SettingsKeys.SettingKeys.LastContentCheck, DateTime.MinValue);
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

        private static string LoadDirectorySetting(SettingsKeys.SettingKeys key, string defaultSubfolder, bool isDefaultFullPath, Action<string> readValue, Action<bool> readValueExists)
        {
            var dir = settingsIni["Settings"][SettingsKeys.SettingsKeyMapping[key]]?.Value;
            readValue(dir);

            if (dir != null && Directory.Exists(dir))
            {
                readValueExists(true);
                return dir;
            }
            readValueExists(false);
            var path = Path.Combine(Utilities.GetExecutingAssemblyFolder(), defaultSubfolder);
            if (isDefaultFullPath) path = defaultSubfolder;
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
                if (propertyName == nameof(StagingLocation))
                    SaveSettingString(SettingsKeys.SettingKeys.BuildLocation, StagingLocation);
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

        public static string GetBackupPath(MEGame game)
        {
            var v = settingsIni["BackupPaths"][SettingsKeys.SettingsKeyMapping[Enum.Parse<SettingsKeys.SettingKeys>($"{game}BackupPath")]]?.Value;
            return string.IsNullOrWhiteSpace(v) ? null : v;
        }

        public static void SaveBackupPath(MEGame game, string path)
        {
            settingsIni["BackupPaths"][SettingsKeys.SettingsKeyMapping[Enum.Parse<SettingsKeys.SettingKeys>($"{game}BackupPath")]].Value = path;
            File.WriteAllText(SettingsPath, settingsIni.ToString());
        }
    }
}
#endif