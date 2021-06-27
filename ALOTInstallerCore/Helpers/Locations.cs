using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ALOTInstallerCore.ModManager.Objects;
using ALOTInstallerCore.Objects;
using ALOTInstallerCore.Objects.Manifest;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Packages;
using PropertyChanged;
using Serilog;

namespace ALOTInstallerCore.Helpers
{
    /// <summary>
    /// Contains locations for various ALOT Installer items. Paths that are configurable by the user are accessible in the Settings class.
    /// </summary>
    public static class Locations
    {
        // This technically shouldn't be here but I don't really know where else to put it
        public static readonly MEGame[] AllMEGames = new[] { MEGame.ME1, MEGame.ME2, MEGame.ME3 };


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
            Log.Information("[AICORE] Loading game targets");
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
        public static string MEMPath(bool forceCached = false) => !forceCached ?
            forcedMemPath ?? Path.Combine(AppDataFolder(), @"MassEffectModderNoGui.exe") :
            Path.Combine(AppDataFolder(), @"MassEffectModderNoGui.exe");
#elif LINUX
        public static string MEMPath(bool forceCached = false) => !forceCached ? 
            forcedMemPath ?? Path.Combine(AppDataFolder(), @"MassEffectModderNoGui.exe") :
            Path.Combine(AppDataFolder(), @"MassEffectModderNoGui");
#endif

        public static GameTarget ME1Target { get; set; }
        public static GameTarget ME2Target { get; set; }
        public static GameTarget ME3Target { get; set; }

        /// <summary>
        /// UI display string of the ME1 target path. Do not trust this value as a true path, use the target instead.
        /// </summary>
        [DependsOn(nameof(ME1Target))] public static string ME1GamePath => ME1Target?.TargetPath ?? "Not installed";
        /// <summary>
        /// UI display string of the ME2 target path. Do not trust this value as a true path, use the target instead.
        /// </summary>
        [DependsOn(nameof(ME2Target))] public static string ME2GamePath => ME2Target?.TargetPath ?? "Not installed";
        /// <summary>
        /// UI display string of the ME3 target path. Do not trust this value as a true path, use the target instead.
        /// </summary>
        [DependsOn(nameof(ME3Target))] public static string ME3GamePath => ME3Target?.TargetPath ?? "Not installed";

        public static string ConfigPathME1 { get; set; }
        public static string ConfigPathME2 { get; set; }
        public static string ConfigPathME3 { get; set; }

        private static void LoadGamePaths()
        {
            var gameLocations = MEMIPCHandler.GetGameLocations();
            foreach (var item in gameLocations)
            {
                var keyName = item.Key.ToString();
                var game = Enum.Parse<MEGame>(keyName.Substring(0, 3));
                var type = keyName.Substring(3);
                if (type == "GamePath")
                {
                    if (item.Value == null)
                    {
                        Utilities.WriteDebugLog($"[AICORE] Could not find game path for game {game}");
                    }
                    else
                    {
                        var path = item.Value.TrimEnd(Path.DirectorySeparatorChar);
                        string exePath = MEDirectories.GetExecutablePath(game, path);

                        if (File.Exists(exePath))
                        {
                            Utilities.WriteDebugLog("[AICORE] Game executable exists - returning this path: " + exePath);
                            internalSetTarget(game, path);
                        }
                        else
                        {
                            Log.Warning($@"[AICORE] Executable not found: {exePath}. This target is not available.");
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


        public static bool SetConfigPath(MEGame game, string itemValue, bool setMEM)
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
                    case MEGame.ME1:
                        ConfigPathME1 = itemValue;
                        break;
                    case MEGame.ME2:
                        ConfigPathME2 = itemValue;
                        break;
                    case MEGame.ME3:
                        ConfigPathME3 = itemValue;
                        break;
                }
            }

            return returnValue;
        }


        private static bool internalSetTarget(MEGame game, string path)
        {
            GameTarget gt = new GameTarget(game, path, false);
            var failedValidationReason = gt.ValidateTarget();
            if (failedValidationReason != null)
            {
                Log.Error($@"[AICORE] Game target {path} failed validation: {failedValidationReason}");
                return false;
            }
            switch (game)
            {
                case MEGame.ME1:
                    ME1Target = gt;
                    return true;
                case MEGame.ME2:
                    ME2Target = gt;
                    return true;
                case MEGame.ME3:
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
        public static bool SetTarget(GameTarget target, bool setMEMPath = true)
        {
            var successful = !setMEMPath || MEMIPCHandler.SetGamePath(target.Game, target.TargetPath);
            switch (target.Game)
            {
                case MEGame.ME1:
                    ME1Target = target;
                    break;
                case MEGame.ME2:
                    ME2Target = target;
                    break;
                case MEGame.ME3:
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

        public static GameTarget GetTarget(MEGame meGame)
        {
            switch (meGame)
            {
                case MEGame.ME1:
                    return ME1Target;
                case MEGame.ME2:
                    return ME2Target;
                case MEGame.ME3:
                    return ME3Target;
                default:
                    return null;
            }
        }

        public static void ReloadTarget(MEGame game)
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

#if WPF
        public static string GetInstallModeMusicFilePath(InstallOptionsPackage iop)
        {
            if (iop.InstallerMode == ManifestMode.MEUITM)
            {
                return Path.Combine(Locations.MusicDirectory, $"meuitm_{iop.InstallTarget.Game.ToString().ToLower()}.mp3");
            }
            return Path.Combine(Locations.MusicDirectory, iop.InstallTarget.Game.ToString().ToLower() + ".mp3");
        }
#endif
    }
}
