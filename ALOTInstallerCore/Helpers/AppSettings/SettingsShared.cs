using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;

namespace ALOTInstallerCore.Helpers.AppSettings
{
    public partial class Settings
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

        public static bool TextureLibraryLocationExistedOnLoad;
        public static string TextureLibrarySettingsLocation;
        public static bool StagingLocationExistedOnLoad;
        public static string StagingSettingsLocation;


        private static string _textureLibraryLocation;
        /// <summary>
        /// Location of the texture library that the manifests use
        /// </summary>
        public static string TextureLibraryLocation
        {
            get => _textureLibraryLocation;
            set => SetProperty(ref _textureLibraryLocation, value);
        }
        private static string _buildLocation;
        /// <summary>
        /// Location that can be used to build and stage textures in preparation for installation
        /// </summary>
        public static string BuildLocation
        {
            get => _buildLocation;
            set => SetProperty(ref _buildLocation, value);
        }

        private static bool _showAdvancedFileInfo;
        public static bool ShowAdvancedFileInfo
        {
            get => _showAdvancedFileInfo;
            set => SetProperty(ref _showAdvancedFileInfo, value);
        }

        private static bool _betaMode;
        /// <summary>
        /// Allows updating to beta, prerelease versions of items
        /// </summary>
        public static bool BetaMode
        {
            get => _betaMode;
            set => SetProperty(ref _betaMode, value);
        }

        private static bool _importFilesAsMove;
        /// <summary>
        /// Move files instead of copying them when files are imported. This only applies to files on the same drive (moving doesn't report progress)
        /// </summary>
        public static bool MoveFilesWhenImporting
        {
            get => _importFilesAsMove;
            set => SetProperty(ref _importFilesAsMove, value);
        }

        public static DateTime LastBetaAdvert { get; set; }
        
        

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


        public enum SettingsSaveResult
        {
            SAVED,
            FAILED_UNAUTHORIZED,
            FAILED_OTHER
        }
    }
}
