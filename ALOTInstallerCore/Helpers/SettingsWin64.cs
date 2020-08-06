using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
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

        private static bool Loaded = false;
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
            if (Loaded) Save();
            return true;
        }
        #endregion

        /// <summary>
        /// Location of the texture library that the manifests use
        /// </summary>
        public static string TextureLibraryLocation { get; set; }
        /// <summary>
        /// Location that can be used to build and stage textures in preparation for installation
        /// </summary>
        public static string BuildLocation { get; set; }

        /// <summary>
        /// Allows updating to beta, prerelease versions of items
        /// </summary>
        public static bool BetaMode { get; set; }
        public static bool MoveFilesWhenImporting { get; set; }


        /*
        private static bool _logModStartup = false;
        public static bool LogModStartup
        {
            get => _logModStartup;
            set => SetProperty(ref _logModStartup, value);
        }


        private static bool _logModUpdater = true;
        public static bool LogModUpdater
        {
            get => _logModUpdater;
            set => SetProperty(ref _logModUpdater, value);
        }

        private static bool _enableTelemetry = true;
        public static bool EnableTelemetry
        {
            get => _enableTelemetry;
            set => SetProperty(ref _enableTelemetry, value);
        }
        private static bool _betaMode = false;
        public static bool BetaMode
        {
            get => _betaMode;
            set => SetProperty(ref _betaMode, value);
        }

        private static bool _modMakerAutoInjectCustomKeybindsOption = false;

        public static bool ModMakerAutoInjectCustomKeybindsOption
        {
            get => _modMakerAutoInjectCustomKeybindsOption;
            set => SetProperty(ref _modMakerAutoInjectCustomKeybindsOption, value);
        }

        private static bool _modMakerControllerModOption = false;

        public static bool ModMakerControllerModOption
        {
            get => _modMakerControllerModOption;
            set => SetProperty(ref _modMakerControllerModOption, value);
        }

        private static string _updaterServiceUsername;
        public static string UpdaterServiceUsername
        {
            get => _updaterServiceUsername;
            set => SetProperty(ref _updaterServiceUsername, value);
        }

        private static int _webclientTimeout = 5; // Defaults to 5
        public static int WebClientTimeout
        {
            get => _webclientTimeout;
            set => SetProperty(ref _webclientTimeout, value);
        }

        private static string _updateServiceLZMAStoragePath;
        public static string UpdaterServiceLZMAStoragePath
        {
            get => _updateServiceLZMAStoragePath;
            set => SetProperty(ref _updateServiceLZMAStoragePath, value);
        }

        private static string _updateServiceManifestStoragePath;
        public static string UpdaterServiceManifestStoragePath
        {
            get => _updateServiceManifestStoragePath;
            set => SetProperty(ref _updateServiceManifestStoragePath, value);
        }

        private static bool _logMixinStartup = false;
        public static bool LogMixinStartup
        {
            get => _logMixinStartup;
            set => SetProperty(ref _logMixinStartup, value);
        }

        private static bool _logModInstallation = false;
        public static bool LogModInstallation
        {
            get => _logModInstallation;
            set => SetProperty(ref _logModInstallation, value);
        }

        private static bool _developerMode;
        public static bool DeveloperMode
        {
            get => _developerMode;
            set => SetProperty(ref _developerMode, value);
        }

        private static bool _darkTheme;
        public static bool DarkTheme
        {
            get => _darkTheme;
            set => SetProperty(ref _darkTheme, value);
        }

        private static bool _autoUpdateLods = true;
        public static bool AutoUpdateLODs
        {
            get => _autoUpdateLods;
            set => SetProperty(ref _autoUpdateLods, value);
        }




        private static string _modLibraryPath;
        public static string ModLibraryPath
        {
            get => _modLibraryPath;
            set => SetProperty(ref _modLibraryPath, value);
        }

        private static string _language;
        public static string Language
        {
            get => _language;
            set => SetProperty(ref _language, value);
        }

        public static DateTime LastContentCheck { get; internal set; }

        private static bool _showedPreviewPanel;
        public static bool ShowedPreviewPanel
        {
            get => _showedPreviewPanel;
            set => SetProperty(ref _showedPreviewPanel, value);
        }

        private static bool _logModMakerCompiler;
        public static bool LogModMakerCompiler
        {
            get => _logModMakerCompiler;
            set => SetProperty(ref _logModMakerCompiler, value);
        }
        */

        public static readonly string SettingsPath = Path.Combine(Locations.AppDataFolder(), "settings.ini");

        public static void Load()
        {
            TextureLibraryLocation = LoadDirectorySetting(SettingsKeys.SettingKeys.TextureLibraryDirectory, @"Downloaded_Mods");
            BuildLocation = LoadDirectorySetting(SettingsKeys.SettingKeys.BuildLocation, @"Staging");
            MoveFilesWhenImporting = LoadSettingBool(SettingsKeys.SettingKeys.ImportAsMove, false);
            //Language = LoadSettingString(settingsIni, "ModManager", "Language", "int");
            //LastContentCheck = LoadSettingDateTime(settingsIni, "ModManager", "LastContentCheck", DateTime.MinValue);
            //BetaMode = LoadSettingBool(settingsIni, "ModManager", "BetaMode", false);
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

        private static DateTime LoadSettingDateTime(DuplicatingIni ini, string section, string key, DateTime defaultValue)
        {
            if (ini == null) return defaultValue;

            if (string.IsNullOrEmpty(ini[section][key]?.Value)) return defaultValue;
            try
            {
                if (long.TryParse(ini[section][key]?.Value, out var dateLong))
                {
                    return DateTime.FromBinary(dateLong);
                }
            }
            catch (Exception)
            {
            }
            return defaultValue;
        }

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
            string dir = RegistryHandler.GetRegistrySettingString(SettingsKeys.SettingsKeyMapping[key]);
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
        public static SettingsSaveResult Save()
        {
            try
            {
                SaveSettingString(SettingsKeys.SettingKeys.BuildLocation, BuildLocation);
                SaveSettingString( SettingsKeys.SettingKeys.TextureLibraryDirectory, TextureLibraryLocation);


                //SaveSettingBool(settingsIni, "ALOTInstallerCore", "LogModStartup", LogModStartup);
                //SaveSettingBool(settingsIni, "ALOTInstallerCore", "LogMixinStartup", LogMixinStartup);
                //SaveSettingBool(settingsIni, "ALOTInstallerCore", "LogModMakerCompiler", LogModMakerCompiler);
                //SaveSettingBool(settingsIni, "ALOTInstallerCore", "EnableTelemetry", EnableTelemetry);
                //SaveSettingString(settingsIni, "ALOTInstallerCore", SettingsKeys.SettingKeys.BuildLocation, Locations.BuildLocation);
                //SaveSettingString(settingsIni, "ALOTInstallerCore", "LZMAStoragePath", UpdaterServiceLZMAStoragePath);
                //SaveSettingString(settingsIni, "UpdaterService", "ManifestStoragePath", UpdaterServiceManifestStoragePath);
                //SaveSettingBool(settingsIni, "UI", "DeveloperMode", DeveloperMode);
                //SaveSettingBool(settingsIni, "UI", "DarkTheme", DarkTheme);
                //SaveSettingBool(settingsIni, "Logging", "LogModInstallation", LogModInstallation);
                //SaveSettingString(settingsIni, "ModLibrary", "LibraryPath", ModLibraryPath);
                //SaveSettingString(settingsIni, "ModManager", "Language", Language);
                //SaveSettingDateTime(settingsIni, "ModManager", "LastContentCheck", LastContentCheck);
                //SaveSettingBool(settingsIni, "ModManager", "BetaMode", BetaMode);
                //SaveSettingBool(settingsIni, "ModManager", "ShowedPreviewMessage2", ShowedPreviewPanel);
                //SaveSettingBool(settingsIni, "ModManager", "AutoUpdateLODs", AutoUpdateLODs);
                //SaveSettingInt(settingsIni, "ModManager", "WebclientTimeout", WebClientTimeout);
                //SaveSettingBool(settingsIni, "ModMaker", "AutoAddControllerMixins", ModMakerControllerModOption);
                //SaveSettingBool(settingsIni, "ModMaker", "AutoInjectCustomKeybinds", ModMakerAutoInjectCustomKeybindsOption);

                return SettingsSaveResult.SAVED;
            }
            catch (UnauthorizedAccessException uae)
            {
                Log.Error($"Unauthorized access exception: {uae.Flatten()}");
                return SettingsSaveResult.FAILED_UNAUTHORIZED;
            }
            catch (Exception e)
            {
                Log.Error($"Error commiting settings: {e.Flatten()}");
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
            RegistryHandler.WriteRegistrySettingString(SettingsKeys.SettingsKeyMapping[key], value.ToBinary().ToString());
        }
    }
}