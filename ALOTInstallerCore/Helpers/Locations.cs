using ALOTInstallerCore.PlatformSpecific.Windows;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using static ALOTInstallerCore.Hook;

namespace ALOTInstallerCore.Helpers
{
    /// <summary>
    /// Contains locations for various ALOT Installer items.
    /// </summary>
    public static class Locations
    {
        public static string AppDataFolder() => Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Path.GetFileNameWithoutExtension(System.Diagnostics.Process.GetCurrentProcess().MainModule.ModuleName))).FullName;
        public static string TempDirectory() => Directory.CreateDirectory(Path.Combine(AppDataFolder(), "Temp")).FullName;
        public static string GetCachedManifestPath() => Path.Combine(AppDataFolder(), "manifest.xml");

        internal static void LoadLocations(Platform platform)
        {
            switch (platform)
            {
                case Platform.Windows:
                    {
                        LoadLocationsWin64();
                    }
                    break;
                case Platform.Linux:
                    {
                        LoadLocationsLinux64();
                    }
                    break;
            }
        }

        private static void LoadLocationsWin64()
        {
            string librarydir = RegistryHandler.GetRegistrySettingString(SettingsKeys.SettingsKeyMapping[SettingsKeys.SettingKeys.TextureLibraryDirectory]);
            if (librarydir != null && Directory.Exists(librarydir))
            {
                TextureLibraryLocation = librarydir;
            } else
            {
                TextureLibraryLocation = Path.Combine(Utilities.GetExecutingAssemblyFolder(), "Downloaded_Mods");
                Directory.CreateDirectory(TextureLibraryLocation); //Create to ensure existence
            }
        }

        private static void LoadLocationsLinux64()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Location of the texture library that the manifests use
        /// </summary>
        public static string TextureLibraryLocation { get; set; }
        /// <summary>
        /// Location that can be used to build and stage textures in preparation for installation
        /// </summary>
        public static string BuildLocation { get; set; }

    }
}
