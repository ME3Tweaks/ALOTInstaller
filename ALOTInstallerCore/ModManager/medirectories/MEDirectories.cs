using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.ModManager.Objects;
using ALOTInstallerCore.ModManager.Services;
using ALOTInstallerCore.Objects;
using Serilog;

namespace ALOTInstallerCore.ModManager.GameDirectories

{
    [Localizable(false)]
    public static class MEDirectories
    {
        public static string CookedPath(Enums.MEGame game, string forcedPath = null)
        {
            switch (game)
            {
                case Enums.MEGame.ME1:
                    return forcedPath != null ? ME1Directory.CookedPath(forcedPath) : ME1Directory.cookedPath;
                case Enums.MEGame.ME2:
                    return forcedPath != null ? ME2Directory.CookedPath(forcedPath) : ME2Directory.cookedPath;
                case Enums.MEGame.ME3:
                    return forcedPath != null ? ME3Directory.CookedPath(forcedPath) : ME3Directory.cookedPath;
                default:
                    throw new ArgumentOutOfRangeException(nameof(game), game, null);
            }
        }
        public static string CookedPath(GameTarget target)
        {
            switch (target.Game)
            {
                case Enums.MEGame.ME1:
                    return ME1Directory.CookedPath(target);
                case Enums.MEGame.ME2:
                    return ME2Directory.CookedPath(target);
                case Enums.MEGame.ME3:
                    return ME3Directory.CookedPath(target);
                default:
                    throw new ArgumentOutOfRangeException(nameof(target.Game), target.Game, null);
            }
        }


        public static string ASIPath(GameTarget target)
        {
            switch (target.Game)
            {
                case Enums.MEGame.ME1:
                    return ME1Directory.ASIPath(target);
                case Enums.MEGame.ME2:
                    return ME2Directory.ASIPath(target);
                case Enums.MEGame.ME3:
                    return ME3Directory.ASIPath(target);
                default:
                    throw new ArgumentOutOfRangeException(nameof(target.Game), target.Game, null);
            }
        }

        public static string GamePath(Enums.MEGame game)
        {
            switch (game)
            {
                case Enums.MEGame.ME1:
                    return ME1Directory.gamePath;
                case Enums.MEGame.ME2:
                    return ME2Directory.gamePath;
                case Enums.MEGame.ME3:
                    return ME3Directory.gamePath;
                default:
                    throw new ArgumentOutOfRangeException(nameof(game), game, null);
            }
        }
        public static string BioGamePath(Enums.MEGame game)
        {
            switch (game)
            {
                case Enums.MEGame.ME1:
                    return ME1Directory.bioGamePath;
                case Enums.MEGame.ME2:
                    return ME2Directory.bioGamePath;
                case Enums.MEGame.ME3:
                    return ME3Directory.biogamePath;
                default:
                    throw new ArgumentOutOfRangeException(nameof(game), game, null);
            }
        }

        public static string BioGamePath(GameTarget target) => Path.Combine(target.TargetPath, target.Game == Enums.MEGame.ME3 ? "BIOGame" : "BioGame"); //all games use same biogame path.


        public static Dictionary<string, string> OfficialDLCNames(Enums.MEGame game)
        {
            switch (game)
            {
                case Enums.MEGame.ME1:
                    return ME1Directory.OfficialDLCNames;
                case Enums.MEGame.ME2:
                    return ME2Directory.OfficialDLCNames;
                case Enums.MEGame.ME3:
                    return ME3Directory.OfficialDLCNames;
                default:
                    throw new ArgumentOutOfRangeException(nameof(game), game, null);
            }
        }

        /// <summary>
        /// Gets path to executable for the specified game and it's root path
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        public static string ExecutablePath(Enums.MEGame game, string rootPath)
        {
            switch (game)
            {
                case Enums.MEGame.ME1:
                    return ME1Directory.ExecutablePath(rootPath);
                case Enums.MEGame.ME2:
                    return ME2Directory.ExecutablePath(rootPath);
                case Enums.MEGame.ME3:
                    return ME3Directory.ExecutablePath(rootPath);
                default:
                    throw new ArgumentOutOfRangeException(nameof(game), game, null);
            }
        }

        /// <summary>
        /// Gets path to executable for the specified Game Target
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public static string ExecutablePath(GameTarget target)
        {
            return ExecutablePath(target.Game, target.TargetPath);
        }

        /// <summary>
        /// Refreshes the registry active paths for all three games
        /// </summary>
        public static void ReloadGamePaths()
        {
            ME1Directory.ReloadActivePath();
            ME2Directory.ReloadActivePath();
            ME3Directory.ReloadActivePath();
        }

        public static string DLCPath(Enums.MEGame game)
        {
            switch (game)
            {
                case Enums.MEGame.ME1:
                    return ME1Directory.DLCPath;
                case Enums.MEGame.ME2:
                    return ME2Directory.DLCPath;
                case Enums.MEGame.ME3:
                    return ME3Directory.DLCPath;
                default:
                    throw new ArgumentOutOfRangeException(nameof(game), game, null);
            }
        }

        public static string DLCPath(GameTarget target)
        {
            switch (target.Game)
            {
                case Enums.MEGame.ME1:
                    return Path.Combine(target.TargetPath, "DLC");
                case Enums.MEGame.ME2:
                    return Path.Combine(target.TargetPath, "BioGame", "DLC");
                case Enums.MEGame.ME3:
                    return Path.Combine(target.TargetPath, "BIOGame", "DLC");
                default:
                    throw new ArgumentOutOfRangeException(nameof(target.Game), target.Game, null);
            }
        }

        public static List<string> OfficialDLC(Enums.MEGame game)
        {
            switch (game)
            {
                case Enums.MEGame.ME1:
                    return ME1Directory.OfficialDLC;
                case Enums.MEGame.ME2:
                    return ME2Directory.OfficialDLC;
                case Enums.MEGame.ME3:
                    return ME3Directory.OfficialDLC;
                default:
                    throw new ArgumentOutOfRangeException(nameof(game), game, null);
            }
        }

        //public static bool IsInBasegame(this IMEPackage pcc) => IsInBasegame(pcc.FilePath, pcc.Game);

        public static bool IsInBasegame(string path, Enums.MEGame game) => path.StartsWith(CookedPath(game));

        public static bool IsInBasegame(string path, GameTarget target) => path.StartsWith(CookedPath(target), StringComparison.InvariantCultureIgnoreCase);

        //public static bool IsInOfficialDLC(this IMEPackage pcc) => IsInOfficialDLC(pcc.FilePath, pcc.Game);

        public static bool IsInOfficialDLC(string path, Enums.MEGame game)
        {
            if (game == Enums.MEGame.Unknown)
            {
                return false;
            }
            string dlcPath = DLCPath(game);

            return OfficialDLC(game).Any(dlcFolder => path.StartsWith(Path.Combine(dlcPath, dlcFolder)));
        }

        public static bool IsInOfficialDLC(string path, GameTarget target)
        {
            if (target.Game == Enums.MEGame.Unknown)
            {
                return false;
            }
            string dlcPath = DLCPath(target);

            return OfficialDLC(target.Game).Any(dlcFolder => path.StartsWith(Path.Combine(dlcPath, dlcFolder), StringComparison.CurrentCultureIgnoreCase));
        }

        public static List<string> EnumerateGameFiles(Enums.MEGame gameVersion, string searchPath, bool recurse = true, Predicate<string> predicate = null)
        {
            List<string> files = Directory.EnumerateFiles(searchPath, "*", recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly).ToList();

            files = EnumerateGameFiles(gameVersion, files, predicate);

            return files;
        }

        public static List<string> EnumerateGameFiles(Enums.MEGame gameVersion, List<string> files, Predicate<string> predicate = null)
        {
            if (predicate == null)
            {
                // KFreon: Set default search predicate.
                switch (gameVersion)
                {
                    case Enums.MEGame.ME1:
                        predicate = s => s.ToLowerInvariant().EndsWith(".upk", true, null) || s.ToLowerInvariant().EndsWith(".u", true, null) || s.ToLowerInvariant().EndsWith(".sfm", true, null);
                        break;
                    case Enums.MEGame.ME2:
                    case Enums.MEGame.ME3:
                        predicate = s => s.ToLowerInvariant().EndsWith(".pcc", true, null) || s.ToLowerInvariant().EndsWith(".tfc", true, null) || s.ToLowerInvariant().EndsWith(".afc", true, null);
                        break;
                }
            }

            return files.Where(t => predicate(t)).ToList();
        }

        internal static List<string> GetInstalledDLC(GameTarget target, bool includeDisabled = false)
        {
            var dlcDirectory = MEDirectories.DLCPath(target);
            if (Directory.Exists(dlcDirectory))
            {
                return Directory.GetDirectories(dlcDirectory).Where(x => Path.GetFileName(x).StartsWith("DLC_") || (includeDisabled && Path.GetFileName(x).StartsWith("xDLC_"))).Select(x => Path.GetFileName(x)).ToList();
            }

            return new List<string>();
        }

        /// <summary>
        /// Gets DLC path based on specified game root and game.
        /// </summary>
        /// <param name="gameRoot"></param>
        /// <param name="game"></param>
        /// <returns></returns>
        internal static string DLCPath(string gameRoot, Enums.MEGame game)
        {
            if (game == Enums.MEGame.ME1) return Path.Combine(gameRoot, @"DLC");
            if (game == Enums.MEGame.ME3) return Path.Combine(gameRoot, "BioGame", @"DLC");
            if (game == Enums.MEGame.ME3) return Path.Combine(gameRoot, "BIOGame", @"DLC");
            return null;
        }

        public static string ALOTMarkerPath(GameTarget selectedDiagnosticTarget)
        {
            switch (selectedDiagnosticTarget.Game)
            {
                case Enums.MEGame.ME1:
                    return Path.Combine(selectedDiagnosticTarget.TargetPath, @"BioGame", "CookedPC", "testVolumeLight_VFX.upk");
                case Enums.MEGame.ME2:
                    return Path.Combine(selectedDiagnosticTarget.TargetPath, @"BioGame", "CookedPC", "BIOC_Materials.pcc");
                case Enums.MEGame.ME3:
                    return Path.Combine(selectedDiagnosticTarget.TargetPath, @"BIOGame", "CookedPCConsole", "adv_combat_tutorial_xbox_D_Int.afc");
                default:
                    return null;
            }
        }

        /// <summary>
        /// ME1: BioEngine.ini, ME2/3: GameSettings.ini
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        internal static string LODConfigFile(Enums.MEGame game)
        {
            switch (game)
            {
                case Enums.MEGame.ME1:
                    return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), @"BioWare", "Mass Effect", "Config", "BIOEngine.ini");
                case Enums.MEGame.ME2:
                    return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), @"BioWare", "Mass Effect 2", "BioGame", "Config", "GamerSettings.ini");
                case Enums.MEGame.ME3:
                    return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), @"BioWare", "Mass Effect 3", "BIOGame", "Config", "GamerSettings.ini");
                default:
                    return null;
            }
        }

        public static string[] ExecutableNames(Enums.MEGame game)
        {
            switch (game)
            {
                case Enums.MEGame.ME1:
                    return new[] { "MassEffect.exe" };
                case Enums.MEGame.ME2:
                    return new[] { "MassEffect2.exe", "ME2Game.exe" };
                case Enums.MEGame.ME3:
                    return new[] { "MassEffect3.exe" };
                default:
                    return null;
            }
        }

        public static Dictionary<string, MetaCMM> GetMetaMappedInstalledDLC(GameTarget target)
        {
            var installedDLC = GetInstalledDLC(target);
            var metamap = new Dictionary<string, MetaCMM>();
            var dlcpath = DLCPath(target);
            foreach (var v in installedDLC)
            {
                var meta = Path.Combine(dlcpath, v, "_metacmm.txt");
                MetaCMM mf = null;
                if (File.Exists(meta))
                {
                    mf = new MetaCMM(meta);
                }

                metamap[v] = mf;
            }

            return metamap;
        }

        /// <summary>
        /// Gets a list of superceding package files from the DLC of the game. Only files in DLC mods are returned
        /// </summary>
        /// <param name="target">Target to get supercedances for</param>
        /// <returns>Dictionary mapping filename to list of DLCs that contain that file, in order of highest priority to lowest</returns>
        public static Dictionary<string, List<string>> GetFileSupercedances(GameTarget target)
        {
            //make dictionary from basegame files
            var fileListMapping = new CaseInsensitiveDictionary<List<string>>();
            var directories = MELoadedFiles.GetEnabledDLC(target).OrderBy(dir => MELoadedFiles.GetMountPriority(dir, target.Game));
            foreach (string directory in directories)
            {
                var dlc = Path.GetFileName(directory);
                if (MEDirectories.OfficialDLC(target.Game).Contains(dlc)) continue; //skip
                foreach (string filePath in MELoadedFiles.GetCookedFiles(target.Game, directory, false))
                {
                    string fileName = Path.GetFileName(filePath);
                    if (fileName != null && fileName.RepresentsPackageFilePath())
                    {
                        if (fileListMapping.TryGetValue(fileName, out var supercedingList))
                        {
                            supercedingList.Insert(0, dlc);
                        }
                        else
                        {
                            fileListMapping[fileName] = new List<string>(new[] { dlc });
                        }
                    }
                }
            }

            return fileListMapping;
        }

        public static Dictionary<string, int> GetMountPriorities(GameTarget selectedTarget)
        {
            //make dictionary from basegame files
            var dlcmods = VanillaDatabaseService.GetInstalledDLCMods(selectedTarget);
            var mountMapping = new Dictionary<string, int>();
            foreach (var dlc in dlcmods)
            {
                var mountpath = Path.Combine(MEDirectories.DLCPath(selectedTarget), dlc);
                try
                {
                    mountMapping[dlc] = MELoadedFiles.GetMountPriority(mountpath, selectedTarget.Game);
                }
                catch (Exception e)
                {
                    Log.Error($"[AICORE] Exception getting mount priority from file: {mountpath}: {e.Message}");
                }
            }

            return mountMapping;
        }

        public static string ExecutableDirectory(GameTarget gameTarget)
        {
            switch (gameTarget.Game)
            {
                case Enums.MEGame.ME1:
                case Enums.MEGame.ME2:
                    return Path.Combine(gameTarget.TargetPath, "Binaries");
                case Enums.MEGame.ME3:
                    return Path.Combine(gameTarget.TargetPath, "Binaries", "Win32");
            }
            throw new ArgumentOutOfRangeException(nameof(gameTarget.Game));
        }

        public static List<string> VanillaDlls(Enums.MEGame game)
        {
            switch (game)
            {
                case Enums.MEGame.ME1:
                    return ME1Directory.VanillaDlls;
                case Enums.MEGame.ME2:
                    return ME2Directory.VanillaDlls;
                case Enums.MEGame.ME3:
                    return ME3Directory.VanillaDlls;
                default:
                    throw new ArgumentOutOfRangeException(nameof(game), game, null);
            }
            throw new NotImplementedException();
        }

        public static string CookedName(Enums.MEGame game)
        {
            switch (game)
            {
                case Enums.MEGame.ME1:
                case Enums.MEGame.ME2:
                    return "CookedPC";
                case Enums.MEGame.ME3:
                    return "CookedPCConsole";
                default:
                    throw new ArgumentOutOfRangeException(nameof(game), game, null);
            }
        }
    }
}
