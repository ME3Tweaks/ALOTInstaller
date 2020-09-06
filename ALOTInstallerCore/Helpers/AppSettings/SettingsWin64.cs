#if WINDOWS
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using ALOTInstallerCore.ModManager.GameINI;
using ALOTInstallerCore.Objects;
using ALOTInstallerCore.PlatformSpecific.Windows;
using Serilog;

namespace ALOTInstallerCore.Helpers.AppSettings
{
    /// <summary>
    /// Windows based settings loader
    /// </summary>
    public partial class Settings
    {
        
        private static bool _playMusic = true;
        /// <summary>
        /// Global indicator if music should play during the installer or not.
        /// </summary>
        public static bool PlayMusic
        {
            get => _playMusic;
            set => SetProperty(ref _playMusic, value);
        }

        public static void Load()
        {
            TextureLibraryLocation = LoadDirectorySetting(SettingsKeys.SettingKeys.TextureLibraryDirectory, @"Downloaded_Mods");
            BuildLocation = LoadDirectorySetting(SettingsKeys.SettingKeys.BuildLocation, @"Staging");
            MoveFilesWhenImporting = LoadSettingBool(SettingsKeys.SettingKeys.ImportAsMove, false);
            Telemetry = LoadSettingBool(SettingsKeys.SettingKeys.Telemetry, true);
            PlayMusic = LoadSettingBool(SettingsKeys.SettingKeys.PlayMusic, false);
            BetaMode = LoadSettingBool(SettingsKeys.SettingKeys.BetaMode, false);
            LastContentCheck = LoadSettingDateTime(SettingsKeys.SettingKeys.LastContentCheck, DateTime.MinValue);
            //AutoUpdateLODs = LoadSettingBool(settingsIni, "ModManager", "AutoUpdateLODs", true);
            //WebClientTimeout = LoadSettingInt(settingsIni, "ModManager", "WebclientTimeout", 5);
            //ModMakerControllerModOption = LoadSettingBool(settingsIni, "ModMaker", "AutoAddControllerMixins", false);
            //ModMakerAutoInjectCustomKeybindsOption = LoadSettingBool(settingsIni, "ModMaker", "AutoInjectCustomKeybinds", false);


            //UpdaterServiceUsername = LoadSettingString(settingsIni, "UpdaterService", "Username", null);
            //UpdaterServiceLZMAStoragePath = LoadSettingString(settingsIni, "UpdaterService", "LZMAStoragePath", null);
            //UpdaterServiceManifestStoragePath = LoadSettingString(settingsIni, "UpdaterService", "ManifestStoragePath", null);

            //LogModStartup = LoadSettingBool(settingsIni, "Logging", "LogModStartup", false);
            //LogMixinStartup = LoadSettingBool(settingsIni, "Logging", "LogMixinStartup", false);
            //EnableTelemetry = LoadSettingBool(settingsIni, "Logging", "EnableTelemetry", true);
            //LogModInstallation = LoadSettingBool(settingsIni, "Logging", "LogModInstallation", false);
            //LogModMakerCompiler = LoadSettingBool(settingsIni, "Logging", "LogModMakerCompiler", false);

            //ModLibraryPath = LoadSettingString(settingsIni, "ModLibrary", "LibraryPath", null);

            //DeveloperMode = LoadSettingBool(settingsIni, "UI", "DeveloperMode", false);
            //DarkTheme = LoadSettingBool(settingsIni, "UI", "DarkTheme", false);
            Loaded = true;
        }

        private static bool LoadSettingBool(DuplicatingIni ini, string section, string key, bool defaultValue)
        {
            if (ini == null) return defaultValue;
            if (bool.TryParse(ini[section][key]?.Value, out var boolValue))
            {
                return boolValue;
            }
            else
            {
                return defaultValue;
            }
        }

        private static string LoadSettingString(DuplicatingIni ini, string section, string key, string defaultValue)
        {
            if (ini == null) return defaultValue;

            if (string.IsNullOrEmpty(ini[section][key]?.Value))
            {
                return defaultValue;
            }
            else
            {
                return ini[section][key]?.Value;
            }
        }

        private static DateTime LoadSettingDateTime(SettingsKeys.SettingKeys key, DateTime defaultValue)
        {
            var regSetting = RegistryHandler.GetRegistrySettingLong(SettingsKeys.SettingsKeyMapping[key]);
            if (regSetting.HasValue)
            {
                return DateTime.FromBinary(regSetting.Value);
            }
            else
            {
                return defaultValue;
            }
        }


        //private static DateTime LoadSettingDateTime(DuplicatingIni ini, string section, string key, DateTime defaultValue)
        //{
        //    if (ini == null) return defaultValue;

        //    if (string.IsNullOrEmpty(ini[section][key]?.Value)) return defaultValue;
        //    try
        //    {
        //        if (long.TryParse(ini[section][key]?.Value, out var dateLong))
        //        {
        //            return DateTime.FromBinary(dateLong);
        //        }
        //    }
        //    catch (Exception)
        //    {
        //    }
        //    return defaultValue;
        //}

        private static int LoadSettingInt(DuplicatingIni ini, string section, string key, int defaultValue)
        {
            if (ini == null) return defaultValue;

            if (int.TryParse(ini[section][key]?.Value, out var intValue))
            {
                return intValue;
            }
            else
            {
                return defaultValue;
            }
        }


        private static bool LoadSettingBool(SettingsKeys.SettingKeys key, bool defaultValue)
        {
            var regSetting = RegistryHandler.GetRegistrySettingBool(SettingsKeys.SettingsKeyMapping[key]);
            if (regSetting.HasValue)
            {
                return regSetting.Value;
            }
            else
            {
                return defaultValue;
            }
        }

        private static string LoadDirectorySetting(SettingsKeys.SettingKeys key, string defaultSubfolder)
        {
            string dir = RegistryHandler.GetRegistryString(SettingsKeys.SettingsKeyMapping[key]);
            if (dir != null && Directory.Exists(dir))
            {
                return dir;
            }
            else
            {
                var path = Path.Combine(Utilities.GetExecutingAssemblyFolder(), defaultSubfolder);
                Directory.CreateDirectory(path);
                return path;
            }
        }


        public enum SettingsSaveResult
        {
            SAVED,
            FAILED_UNAUTHORIZED,
            FAILED_OTHER
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
                if (propertyName == nameof(PlayMusic))
                    SaveSettingBool(SettingsKeys.SettingKeys.PlayMusic, PlayMusic);
                if (propertyName == nameof(MoveFilesWhenImporting))
                    SaveSettingBool(SettingsKeys.SettingKeys.ImportAsMove, MoveFilesWhenImporting);
                if (propertyName == nameof(LastContentCheck))
                    SaveSettingDateTime(SettingsKeys.SettingKeys.LastContentCheck, LastContentCheck);

                return SettingsSaveResult.SAVED;
            }
            catch (UnauthorizedAccessException uae)
            {
                Log.Error($"[AICORE] Unauthorized access exception:");
                uae.WriteToLog("[AICORE] ");

                return SettingsSaveResult.FAILED_UNAUTHORIZED;
            }
            catch (Exception e)
            {
                Log.Error($"[AICORE] Error commiting settings:");
                e.WriteToLog("[AICORE] ");
            }

            return SettingsSaveResult.FAILED_OTHER;
        }

        private static void SaveSettingString(SettingsKeys.SettingKeys key, string value)
        {
            RegistryHandler.WriteRegistrySettingString(SettingsKeys.SettingsKeyMapping[key], value);
        }

        private static void SaveSettingBool(SettingsKeys.SettingKeys key, bool value)
        {
            RegistryHandler.WriteRegistrySettingBool(SettingsKeys.SettingsKeyMapping[key], value);
        }

        private static void SaveSettingInt(SettingsKeys.SettingKeys key, int value)
        {
            RegistryHandler.WriteRegistrySettingInt(SettingsKeys.SettingsKeyMapping[key], value);
        }

        private static void SaveSettingDateTime(SettingsKeys.SettingKeys key, DateTime value)
        {
            RegistryHandler.WriteRegistrySettingLong(SettingsKeys.SettingsKeyMapping[key], value.ToBinary());
        }
    }
}

#endif