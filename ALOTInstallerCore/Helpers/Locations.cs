using ALOTInstallerCore.PlatformSpecific.Windows;
using System;
using System.Collections.Generic;
using System.IO;
using ALOTInstallerCore.ModManager.GameDirectories;
using ALOTInstallerCore.ModManager.GameINI;
using ALOTInstallerCore.ModManager.Objects;
using ALOTInstallerCore.Objects;
using Microsoft.Win32;

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

#if WPF
        public static string MusicDirectory => Directory.CreateDirectory(Path.Combine(AppDataFolder(), "Music")).FullName;
#endif

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
        string dir = RegistryHandler.GetRegistryString(SettingsKeys.SettingsKeyMapping[key]);
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
                    }

                    if (path != null)
                    {
                        path = path.TrimEnd(Path.DirectorySeparatorChar);

                        string GameEXEPath = MEDirectories.ExecutablePath(game, path);
                        Utilities.WriteDebugLog("GetGamePath Registry EXE Check Path: " + GameEXEPath);

                        if (File.Exists(GameEXEPath))
                        {
                            Utilities.WriteDebugLog("EXE file exists - returning this path: " + GameEXEPath);
                            internalSetTarget(game, path);
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

    internal static string GetCachedExecutablesDirectory()
    {
        return Directory.CreateDirectory(Path.Combine(AppDataFolder(), "CachedExecutables")).FullName;
    }

    public static string GetCachedExecutable(string executableName)
    {
        return Path.Combine(GetCachedExecutablesDirectory(), executableName);
    }
}
}
