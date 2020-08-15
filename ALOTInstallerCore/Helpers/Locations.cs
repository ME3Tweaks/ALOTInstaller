using ALOTInstallerCore.PlatformSpecific.Windows;
using System;
using System.Collections.Generic;
using System.IO;
using ALOTInstallerCore.ModManager.GameINI;
using ALOTInstallerCore.ModManager.Objects;
using ALOTInstallerCore.Objects;
using Serilog;

namespace ALOTInstallerCore.Helpers
{
    /// <summary>
    /// Contains locations for various ALOT Installer items. Paths that are configurable by the user are accessible in the Settings class.
    /// </summary>
    public static class Locations
    {
        public static string AppDataFolder() => Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create), Utilities.GetHostingProcessname())).FullName;
        public static string TempDirectory() => Directory.CreateDirectory(Path.Combine(AppDataFolder(), "Temp")).FullName;
        public static string GetCachedManifestPath() => Path.Combine(AppDataFolder(), "manifest.xml");

        internal static void LoadLocations()
        {
#if WINDOWS
            //LoadLocationsWin64();
#elif MACOS

#elif LINUX
            LoadLocationsLinux64();
#endif
            LoadGamePaths();
        }
        //#if WINDOWS

        private static string forcedMemPath;

        /// <summary>
        /// Allows the wrapping application to force the location of Mass Effect Modder No Gui
        /// </summary>
        /// <param name="forcedPath"></param>
        public static void OverrideMEMPath(string forcedPath)
        {
            forcedMemPath = forcedPath;
        }
        public static string MEMPath() => forcedMemPath ?? Path.Combine(AppDataFolder(), @"MassEffectModderNoGui.exe");
        //#endif
#if LINUX
        public static string MEMPath() => Path.Combine(AppDataFolder(), @"MassEffectModderNoGui");
#endif

        //private static void LoadLocationsWin64()
        //{
        //    TextureLibraryLocation = GetFolderSetting(SettingsKeys.SettingKeys.TextureLibraryDirectory, "Downloaded_Mods");

        //    //V4 only
        //    BuildLocation = GetFolderSetting(SettingsKeys.SettingKeys.BuildLocation, "BuildLocation");
        //}

        private static string GetFolderSetting(SettingsKeys.SettingKeys key, string defaultF)
        {
            string dir = RegistryHandler.GetRegistrySettingString(SettingsKeys.SettingsKeyMapping[key]);
            if (dir != null && Directory.Exists(dir))
            {
                return dir;
            }
            else
            {
                var path = Path.Combine(Utilities.GetExecutingAssemblyFolder(), defaultF);
                Directory.CreateDirectory(path);
                return path;
            }
        }

        public static GameTarget ME1Target { get; set; }
        public static GameTarget ME2Target { get; set; }
        public static GameTarget ME3Target { get; set; }

        private static void LoadGamePaths()
        {
            //Read config file.
            string path = null;
            string mempath = null;

            // MIGHT NEED CHANGED ON LINUX
            // TODO: USE MEM --game-paths
            string inipath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MassEffectModder");
            inipath = Path.Combine(inipath, "MassEffectModder.ini");
            DuplicatingIni configIni = null;
            if (File.Exists(inipath))
            {
                configIni = DuplicatingIni.LoadIni(inipath);
            }

            if (configIni != null)
            {
                foreach (var game in Enums.AllGames)
                {
                    string key = game.ToString();
                    path = configIni["GameDataPath"][key]?.Value;
                    if (!string.IsNullOrEmpty(path))
                    {
                        GameTarget gt = new GameTarget(game, path, false);
                        var failedValidationReason = gt.ValidateTarget();
                        if (failedValidationReason == null)
                        {
                            switch (game)
                            {
                                case Enums.MEGame.ME1:
                                    ME1Target = gt;
                                    continue;
                                case Enums.MEGame.ME2:
                                    ME2Target = gt;
                                    continue;
                                case Enums.MEGame.ME3:
                                    ME3Target = gt;
                                    continue;
                            }
                        }
                    }

#if WINDOWS
                    //does not exist in ini (or ini does not exist).
                    string softwareKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\";
                    string key64 = @"Wow6432Node\";
                    string gameKey = @"BioWare\Mass Effect";
                    string entry = "Path";

                    if (gameID == 2)
                        gameKey += @" 2";
                    else if (gameID == 3)
                    {
                        gameKey += @" 3";
                        entry = "Install Dir";
                    }

                    path = (string)RegistryHandler.GetValue(softwareKey + gameKey, entry, null);
                    if (path == null)
                    {
                        path = (string)Registry.GetValue(softwareKey + key64 + gameKey, entry, null);
                    }
                    if (path != null)
                    {
                        Utilities.WriteDebugLog("Found game path via registry: " + path);
                        path = path.TrimEnd(Path.DirectorySeparatorChar);

                        string GameEXEPath = "";
                        switch (gameID)
                        {
                            case 1:
                                GameEXEPath = Path.Combine(path, @"Binaries\MassEffect.exe");
                                break;
                            case 2:
                                GameEXEPath = Path.Combine(path, @"Binaries\MassEffect2.exe");
                                break;
                            case 3:
                                GameEXEPath = Path.Combine(path, @"Binaries\Win32\MassEffect3.exe");
                                break;
                        }
                        Utilities.WriteDebugLog("GetGamePath Registry EXE Check Path: " + GameEXEPath);

                        if (File.Exists(GameEXEPath))
                        {
                            Utilities.WriteDebugLog("EXE file exists - returning this path: " + GameEXEPath);
                            return path; //we have path now
                        }
                    }
                    else
                    {
                        Utilities.WriteDebugLog("Could not find game via registry.");
                    }
#endif
                }
            }
        }

        private static void LoadLocationsLinux64()
        {

        }


        /// <summary>
        /// Location where cached ASI files are placed
        /// </summary>
        public static readonly string CachedASIsFolder = Directory.CreateDirectory(Path.Combine(AppDataFolder(), @"CachedASIs")).FullName;

        public static ObservableCollectionExtended<GameTarget> GameTargets { get; } = new ObservableCollectionExtended<GameTarget>();

        /// <summary>
        /// Sets the game path that MEM and ALOTInstallerCore will use for the game specified by the target.
        /// </summary>
        /// <param name="target"></param>
        public static bool SetTarget(GameTarget target)
        {

            var successful = MEMIPCHandler.SetGamePath(target.Game, target.TargetPath);
            switch (target.Game)
            {
                case Enums.MEGame.ME1:
                    ME1Target = target;
                    break;
                case Enums.MEGame.ME2:
                    ME2Target = target;
                    break;
                case Enums.MEGame.ME3:
                    ME3Target = target;
                    break;
            }
            return successful;
            // Manual method
            //var memSettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            //    "MassEffectModder");
            //if (!Directory.Exists(memSettingsPath))
            //    Directory.CreateDirectory(memSettingsPath);

            //var memIni = Path.Combine(memSettingsPath, "MassEffectModder.ini");
            //DuplicatingIni ini = File.Exists(memIni) ? DuplicatingIni.LoadIni(memIni) : new DuplicatingIni();
            //ini["GameDataPaths"][target.Game.ToString()].Value = target.TargetPath;
            //File.WriteAllText(memIni, ini.ToString());
        }

        /// <summary>
        /// Gets a list of all available game targets
        /// </summary>
        /// <returns></returns>
        public static List<GameTarget> GetAllAvailableTargets()
        {
            List<GameTarget> gameTargets = new List<GameTarget>();
            if (ME1Target != null) gameTargets.Add(ME1Target);
            if (ME2Target != null) gameTargets.Add(ME2Target);
            if (ME3Target != null) gameTargets.Add(ME3Target);
            return gameTargets;
        }

        public static GameTarget GetTarget(Enums.MEGame meGame)
        {
            switch (meGame)
            {
                case Enums.MEGame.ME1:
                    return ME1Target;
                case Enums.MEGame.ME2:
                    return ME2Target;
                case Enums.MEGame.ME3:
                    return ME3Target;
                default:
                    return null;
            }
        }

        public static void ReloadTarget(Enums.MEGame game)
        {
            var target = GetTarget(game);
            target?.ReloadGameTarget(true, true);
        }
    }
}
