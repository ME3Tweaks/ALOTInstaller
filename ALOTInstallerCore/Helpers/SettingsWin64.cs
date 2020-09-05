using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using ALOTInstallerCore.ModManager.GameINI;
using ALOTInstallerCore.PlatformSpecific.Windows;
using Serilog;

namespace ALOTInstallerCore.Helpers
{
    /// <summary>
    /// Windows based settings loader
    /// </summary>
    public class Settings
    {
        #region Static Property Changed

        public static bool Loaded { get; private set; }
        public static event PropertyChangedEventHandler StaticPropertyChanged;

        /// <summary>
        /// Sets given property and notifies listeners of its change. IGNORES setting the property to same value.
        /// Should be called in property setters.
        /// </summary>
        /// <typeparam name="T">Type of given property.</typeparam>
        /// <param name="field">Backing field to update.</param>
        /// <param name="value">New value of property.</param>
        /// <param name="propertyName">Name of property.</param>
        /// <returns>True if success, false if backing field and new value aren't compatible.</returns>
        private static bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs(propertyName));
            if (Loaded)
            {
                Save(propertyName);
            }

            if (propertyName == nameof(Telemetry))
            {
                if (Telemetry)
                {
                    CoreAnalytics.StartTelemetryCallback?.Invoke();
                }
                else
                {
                    CoreAnalytics.StopTelemetryCallback?.Invoke();
                }
            }

            return true;
        }
        #endregion

        private static string _textureLibraryLocation;
        private static string _buildLocation;
        private static bool _showAdvancedFileInfo;
        private static bool _importFilesAsMove;

        /// <summary>
        /// Location of the texture library that the manifests use
        /// </summary>
        public static string TextureLibraryLocation
        {
            get => _textureLibraryLocation;
            set => SetProperty(ref _textureLibraryLocation, value);
        }

        /// <summary>
        /// Location that can be used to build and stage textures in preparation for installation
        /// </summary>
        public static string BuildLocation
        {
            get => _buildLocation;
            set => SetProperty(ref _buildLocation, value);
        }

        public static bool ShowAdvancedFileInfo
        {
            get => _showAdvancedFileInfo;
            set => SetProperty(ref _showAdvancedFileInfo, value);
        }

        /// <summary>
        /// Allows updating to beta, prerelease versions of items
        /// </summary>
        public static bool BetaMode
        {
            get => _betaMode;
            set => SetProperty(ref _betaMode, value);
        }

        /// <summary>
        /// Move files instead of copying them when files are imported. This only applies to files on the same drive (moving doesn't report progress)
        /// </summary>
        public static bool MoveFilesWhenImporting
        {
            get => _importFilesAsMove;
            set => SetProperty(ref _importFilesAsMove, value);
        }

        private static bool _playMusic = true;
        /// <summary>
        /// Global indicator if music should play during the installer or not.
        /// </summary>
        public static bool PlayMusic
        {
            get => _playMusic;
            set => SetProperty(ref _playMusic, value);
        }

        /// <summary>
        /// Makes more output messaging occur
        /// </summary>
        public static bool DebugLogs { get; set; }

        private static DateTime _lastContentCheck;

        /// <summary>
        /// When online content was last checked, used for preventing too many requests to ME3Tweaks
        /// </summary>
        public static DateTime LastContentCheck
        {
            get => _lastContentCheck;
            set => SetProperty(ref _lastContentCheck, value);
        }

        private static bool _telemetry = true;
        /// <summary>
        /// Enables/disables telemetry
        /// </summary>
        public static bool Telemetry
        {
            get => _telemetry;
            set => SetProperty(ref _telemetry, value);
        }

        public static DateTime LastBetaAdvert { get; set; }


        public static readonly string SettingsPath = Path.Combine(Locations.AppDataFolder(), "settings.ini");
        private static bool _betaMode;

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