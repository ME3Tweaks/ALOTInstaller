#if WINDOWS
using ALOTInstallerCore.PlatformSpecific.Windows;
#endif
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ALOTInstallerCore.ModManager.GameDirectories;
using ALOTInstallerCore.ModManager.GameINI;
using ALOTInstallerCore.ModManager.Objects;
using ALOTInstallerCore.Objects;
using Microsoft.Win32;
using Serilog;

namespace ALOTInstallerCore.Helpers
{
    /// <summary>
    /// Contains locations for various ALOT Installer items. Paths that are configurable by the user are accessible in the Settings class.
    /// </summary>
    public static class Locations
    {
        private static string _appDataFolderName;

        /// <summary>
        /// The name of the folder for the appdata. Set this value as soon as the hosting app loads to ensure a consistent appdata folder
        /// </summary>
        public static string AppDataFolderName
        {
            get => _appDataFolderName ?? Utilities.GetHostingProcessname();
            set
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _appDataFolderName = value;
                }
            }
        }
        public static string AppDataFolder() => Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create), AppDataFolderName)).FullName;
        public static string TempDirectory() => Directory.CreateDirectory(Path.Combine(AppDataFolder(), "Temp")).FullName;
        public static string GetCachedManifestPath() => Path.Combine(AppDataFolder(), "manifest.xml");

#if WPF
        public static string MusicDirectory => Directory.CreateDirectory(Path.Combine(AppDataFolder(), "Music")).FullName;
#endif

        internal static void LoadTargets()
        {
            Log.Information("[AICORE] Loading targets");
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
#if WINDOWS
        public static string MEMPath() => forcedMemPath ?? Path.Combine(AppDataFolder(), @"MassEffectModderNoGui.exe");
#elif LINUX
        public static string MEMPath() => forcedMemPath ?? Path.Combine(AppDataFolder(), @"MassEffectModderNoGui");
#endif

        public static GameTarget ME1Target { get; set; }
        public static GameTarget ME2Target { get; set; }
        public static GameTarget ME3Target { get; set; }
        public static string ConfigPathME1 { get; set; }
        public static string ConfigPathME2 { get; set; }
        public static string ConfigPathME3 { get; set; }

        private static void LoadGamePaths()
        {

            /*
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
                    if (!internalSetTarget(game, path))
                    {
#if WINDOWS
                        //does not exist in ini (or ini does not exist).
                        string softwareKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\";
                        string key64 = @"Wow6432Node\";
                        string gameKey = @"BioWare\Mass Effect";
                        string entry = "Path";

                        if (game == Enums.MEGame.ME2)
                            gameKey += @" 2";
                        else if (game == Enums.MEGame.ME3)
                        {
                            gameKey += @" 3";
                            entry = "Install Dir";
                        }

                        path = RegistryHandler.GetRegistryString(softwareKey + gameKey, entry);
                        if (path == null)
                        {
                            path = RegistryHandler.GetRegistryString(softwareKey + key64 + gameKey, entry);
                        }*/

            var gameLocations = MEMIPCHandler.GetGameLocations();
            foreach (var item in gameLocations)
            {
                var keyName = item.Key.ToString();
                var game = Enum.Parse<Enums.MEGame>(keyName.Substring(0, 3));
                var type = keyName.Substring(3);
                if (type == "GamePath")
                {
                    if (item.Value == null)
                    {
                        Utilities.WriteDebugLog($"Could not find game path for game {game}");
                    }
                    else
                    {
                        var path = item.Value.TrimEnd(Path.DirectorySeparatorChar);
                        string exePath = MEDirectories.ExecutablePath(game, path);

                        if (File.Exists(exePath))
                        {
                            Utilities.WriteDebugLog("Game executable exists - returning this path: " + exePath);
                            internalSetTarget(game, path);
                        }
                    }
                }
                else if (type == "ConfigPath")
                {
                    if (item.Value == null)
                    {
                        Utilities.WriteDebugLog($"Could not find game config path for game {game}");
                    }
                    else
                    {
                        Locations.SetConfigPath(game, item.Value, false);
                    }
                }

            }
        }


        public static bool SetConfigPath(Enums.MEGame game, string itemValue, bool setMEM)
        {
            bool returnValue = true;
#if !WINDOWS
            if (setMEM)
            {
                returnValue = MEMIPCHandler.SetConfigPath(game, itemValue);
            }
#endif
            if (returnValue)
            {
                switch (game)
                {
                    case Enums.MEGame.ME1:
                        ConfigPathME1 = itemValue;
                        break;
                    case Enums.MEGame.ME2:
                        ConfigPathME2 = itemValue;
                        break;
                    case Enums.MEGame.ME3:
                        ConfigPathME3 = itemValue;
                        break;
                }
            }

            return returnValue;
        }


        private static bool internalSetTarget(Enums.MEGame game, string path)
        {
            GameTarget gt = new GameTarget(game, path, false);
            var failedValidationReason = gt.ValidateTarget();
            if (failedValidationReason != null) return false;
            switch (game)
            {
                case Enums.MEGame.ME1:
                    ME1Target = gt;
                    return true;
                case Enums.MEGame.ME2:
                    ME2Target = gt;
                    return true;
                case Enums.MEGame.ME3:
                    ME3Target = gt;
                    return true;
            }

            return false; // DEFAULT
        }

        /// <summary>
        /// Location where cached ASI files are placed
        /// </summary>
        public static readonly string CachedASIsFolder = Directory.CreateDirectory(Path.Combine(AppDataFolder(), @"CachedASIs")).FullName;

        public static ObservableCollectionExtended<GameTarget> GameTargets { get; } = new ObservableCollectionExtended<GameTarget>();

#if WINDOWS
        /// <summary>
        /// Accesses the internal 'Resources' directory. This should only
        /// </summary>
        
        public static string ResourcesDir => Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName; //The internal 'resources' directory. Does not exist when running from Linux as it's fully single file
#endif

        /// <summary>
        /// Sets the game path that MEM and ALOTInstallerCore will use for the game specified by the target.
        /// This method must be run on a background thread or it will deadlock
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

        private static List<GameTarget> allTargets;
        /// <summary>
        /// Gets a list of all available game targets
        /// </summary>
        /// <returns></returns>
        public static List<GameTarget> GetAllAvailableTargets()
        {
            if (allTargets != null) return allTargets;
            List<GameTarget> gameTargets = new List<GameTarget>();
            if (ME1Target != null) gameTargets.Add(ME1Target);
            if (ME2Target != null) gameTargets.Add(ME2Target);
            if (ME3Target != null) gameTargets.Add(ME3Target);
            allTargets = gameTargets;
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

        internal static string GetME3TweaksServicesCache()
        {
            return Directory.CreateDirectory(Path.Combine(AppDataFolder(), "ME3TweaksServicesCache")).FullName;
        }

        public static string GetBasegameIdentificationCacheFile()
        {
            return Path.Combine(GetME3TweaksServicesCache(), "basegamefileidentificationservice.json");
        }

        internal static string GetThirdPartyIdentificationCachedFile()
        {
            return Path.Combine(GetME3TweaksServicesCache(), "thirdpartyidentificationservice.json");
        }

        internal static string GetObjectInfoFolder()
        {
            return Directory.CreateDirectory(Path.Combine(AppDataFolder(), "ObjectInfo")).FullName;
        }

        public static string GetCachedExecutablesDirectory()
        {
            return Directory.CreateDirectory(Path.Combine(AppDataFolder(), "CachedExecutables")).FullName;
        }

        public static string GetCachedExecutable(string executableName, bool appendWindows = false)
        {
            var result = Path.Combine(GetCachedExecutablesDirectory(), executableName);
#if WINDOWS
            if (appendWindows) result += ".exe";
#endif
            return result;
        }
    }
}
