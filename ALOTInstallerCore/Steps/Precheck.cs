using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.Helpers.AppSettings;
using ALOTInstallerCore.ModManager.GameDirectories;
using ALOTInstallerCore.ModManager.gamefileformats.sfar;
using ALOTInstallerCore.ModManager.Objects;
using ALOTInstallerCore.ModManager.Services;
using ALOTInstallerCore.Objects;
using ALOTInstallerCore.Objects.Manifest;
using Serilog;

namespace ALOTInstallerCore.Steps
{
    /// <summary>
    /// This class performs a precheck of the installation target to ensure it is possible to install mods against it.
    /// </summary>
    public class Precheck
    {

        public static string PerformPreInstallCheck(InstallOptionsPackage package)
        {
            // Get required disk space
            long requiredDiskSpace = Utilities.GetSizeOfDirectory(new DirectoryInfo(Path.Combine(Settings.BuildLocation, package.InstallTarget.Game.ToString())));
            foreach (var v in package.FilesToInstall.OfType<PreinstallMod>())
            {
                var archiveF = v.GetUsedFilepath();
                requiredDiskSpace += (long)(new FileInfo(archiveF).Length * 1.4); //Assume there is 40% compression 
            }

            if (!package.CompressPackages)
            {
                // Game files will be unpacked.
                requiredDiskSpace += (long)(Utilities.GetSizeOfDirectory(new DirectoryInfo(package.InstallTarget.TargetPath), new[] { ".pcc", ".upk", ".sfm", ".u" }) * .4);
            }

            if (package.InstallTarget.Game == Enums.MEGame.ME3)
            {
                // Check how much disk space SFAR unpacking will take
                var installedDLC = VanillaDatabaseService.GetInstalledOfficialDLC(package.InstallTarget);
                var gameDlcDir = Path.Combine(package.InstallTarget.TargetPath, "BIOGame", "DLC");
                foreach (var d in installedDLC)
                {
                    var sfar = Path.Combine(gameDlcDir, d, "CookedPCCConsole", "Default.sfar");
                    if (File.Exists(sfar) && new FileInfo(sfar).Length > 32)
                    {
                        // Packed
                        DLCPackage dlc = new DLCPackage(sfar);
                        long filesizeTotal = 0;
                        foreach (var v in dlc.Files)
                        {
                            filesizeTotal += v.RealUncompressedSize;
                        }

                        filesizeTotal -= new FileInfo(sfar).Length;
                        if (filesizeTotal > 0)
                        {
                            requiredDiskSpace += filesizeTotal;
                        }
                    }
                }

            }

            var targetDi = new DriveInfo(package.InstallTarget.TargetPath);
            if (targetDi.AvailableFreeSpace < requiredDiskSpace * 1.1)
            {
                // Less than 10% buffer
                return $"There is not enough space on the disk the game resides on to install textures. Note that the required space is only an estimate and includes required temporary space.\n\nDrive: {targetDi.Name}\nRequired space: {FileSizeFormatter.FormatSize(requiredDiskSpace)}\nAvailable space: {FileSizeFormatter.FormatSize(targetDi.AvailableFreeSpace)}";
            }
            return null;
        }

        /// <summary>
        /// Performs an installation precheck that should occur after the user has selected files, but before the staging step. This method is synchronous and should probably be run on a background thread as it may take a few seconds.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="mode"></param>
        /// <param name="filesToInstall"></param>
        public static string PerformPreStagingCheck(InstallOptionsPackage package)
        {
            var pc = new Precheck()
            {
                target = package.InstallTarget,
                mode = package.InstallerMode,
                manifestFilesToInstall = package.FilesToInstall.OfType<ManifestFile>().ToList(),
                package = package
            };

            if (!pc.checkOneOptionSelected(out var noOptionsSelectedReason))
            {
                return noOptionsSelectedReason;
            }

            if (!pc.checkRequiredFiles(out var failureReason1))
            {
                // Require files validation failed
                return failureReason1;
            }

            var blacklistedMods = pc.checkBlacklistedMods();

            if (blacklistedMods.Any())
            {
                // Mod(s) that have been blacklisted as incompatible are installed
                return $"The following mods were detected as installed and are known to be incompatible with texture mods:{string.Join("\n - ", blacklistedMods)}\n\nThese mods cannot be installed if installing textures and should not be used for any reason.";
            }

            if (pc.target.Game == Enums.MEGame.ME3)
            {
                // Sanity check for DLC
                var inconsistentDLCs = pc.checkDLCConsistency();
                if (inconsistentDLCs.Any())
                {
                    if (pc.target.Supported)
                    {
                        return $"The following DLCs are in an inconsistent state: {string.Join("\n", inconsistentDLCs)}.\n\nThe game must be restored from a vanilla backup or deleted (not repaired) and reinstalled.";
                    }

                    return "Detected inconsistent DLCs. Cannot install textures to a game with inconsistent DLCs.";
                }
            }

            if (!pc.package.DebugNoInstall)
            {
                if (pc.target.GetInstalledALOTInfo() != null)
                {
                    var replacedAddedRemovedFiles = pc.checkForReplacedAddedRemovedFiles();
                    if (replacedAddedRemovedFiles.Any())
                    {
                        Log.Error("[AICORE] The texture map has become desynchronized from the game state on disk:");
                        foreach (var v in replacedAddedRemovedFiles)
                        {
                            Log.Error($"[AICORE]  > {v}");
                        }

                        Log.Error("[AICORE] Cannot install textures when game has been modified outside of MEM-based texture tools after texture install has taken place");
                        // Texture map is inconsistent, and MEM will refuse installation because the game is not in sync with the texture map
                        return "The texture map from the previous installation has become desynchronized from the current game state. This means that files/mods were added, removed, or replaced/modified since the last texture installation took place. The game must be restored to vanilla so a new texture map can be created.";
                    }
                }
            }

            return null; //OK
        }

        private bool checkOneOptionSelected(out string failurereason)
        {
            if (!package.InstallALOT && !package.InstallALOTUpdate && !package.InstallAddons &&
                !package.InstallMEUITM && !package.InstallPreinstallMods && !package.InstallUserfiles)
            {
                failurereason = "No options for installation were selected.";
                return false;
            }

            failurereason = null;
            return true;
        }

        /// <summary>
        /// Checks if MEUITM is available for install, and throws a warning if it isn't. This should only be run in ALOT mode
        /// </summary>
        /// <param name="package"></param>
        /// <param name="missingRecommandedItemsDialogCallback"></param>
        /// <returns></returns>
        public static bool CheckMEUITM(InstallOptionsPackage package, Func<string, string, string, List<string>, bool> missingRecommandedItemsDialogCallback)
        {
            if (package.InstallerMode != ManifestMode.ALOT || package.InstallTarget.Game != Enums.MEGame.ME1) return true; // Only work in ALOT mode. Always say this check is OK if not ME1
            var installedInfo = package.InstallTarget.GetInstalledALOTInfo();
            var shouldCheck = installedInfo == null || installedInfo.MEUITMVER == 0; //if MEUITM is ever updated, this should probably be changed.

            if (shouldCheck)
            {
                var meuitmFile = package.FilesToInstall.FirstOrDefault(x => x.AlotVersionInfo.MEUITMVER > 0);
                if (meuitmFile == null)
                {
                    var result = missingRecommandedItemsDialogCallback?.Invoke("MEUITM not available for installation",
                        $"Mass Effect Updated and Improved Textures Mod (MEUITM) is highly recommended when installing ALOT for ME1, as it provides a very large amount of the textures that improve the look of Mass Effect. Without MEUITM, the game will look significantly worse. This file should be imported into the library before installing textures.",
                        "Install anyways (NOT RECOMMENDED)?", new List<string>());
                    return result.HasValue && result.Value;
                }
            }

            return true;
        }

        public static bool CheckAllRecommendedItems(InstallOptionsPackage package,
            Func<string, string, string, List<string>, bool> missingRecommandedItemsDialogCallback)
        {
            // install options package at this point will still have full list of files.

            // Get a list of ONLY non-versioned recommended files (which will only be addon files)
            var applicableManifestFiles = package.FilesToInstall.Where(x =>
                    x is ManifestFile mf && mf.ApplicableGames.HasFlag(package.InstallTarget.Game.ToApplicableGame())
                                         && mf.AlotVersionInfo.IsNotVersioned
                                         && mf.Recommendation == RecommendationType.Recommended)
                .ToList();

            var nonReadyRecommendedFiles = applicableManifestFiles.Where(x => !x.Ready).Select(x => x as ManifestFile).ToList();
            // remove option group items if the other item in the optiongroup is available
            for (int i = nonReadyRecommendedFiles.Count - 1; i >= 0; i--)
            {
                var nonReadyFile = nonReadyRecommendedFiles[i];
                if (nonReadyFile is PreinstallMod pm)
                {
                    if (pm.OptionGroup != null)
                    {
                        // Find other matching file
                        var otherMatchingItem = package.FilesToInstall.FirstOrDefault(x =>
                            x is PreinstallMod pmx && pmx.OptionGroup == pm.OptionGroup && pmx != pm);
                        if (otherMatchingItem != null && otherMatchingItem.Ready)
                        {
                            // Another version of this item is ready instead
                            nonReadyRecommendedFiles.RemoveAt(i);
                        }
                    }
                }
            }

            if (nonReadyRecommendedFiles.Any())
            {
                // At least one recommended file for this game is not ready
                var result = missingRecommandedItemsDialogCallback?.Invoke("Not all addon files available for install",
                    $"Not all addon files for {package.InstallerMode} mode are currently ready for install in the texture library. Without these files, the game will be missing significant amounts of upgraded textures. You should have these files imported into your texture library before you continue installation.",
                    "Install anyways (NOT RECOMMENDED)?", nonReadyRecommendedFiles.Select(x => x.FriendlyName).ToList());
                return result.HasValue && result.Value;
            }

            return true;
        }

        private static readonly string[] UnpackedFileExtensions = { @".pcc", @".tlk", @".bin", @".dlc", ".afc", @".tfc" };
        private InstallOptionsPackage package;

        private List<string> checkDLCConsistency()
        {
            List<string> inconsistentDLC = new List<string>();
            var dlcDir = Path.Combine(target.TargetPath, "BioGame", "DLC");
            var dlcFolders = MEDirectories.GetInstalledDLC(target).Where(x => MEDirectories.OfficialDLC(target.Game).Contains(x)).Select(x => Path.Combine(dlcDir, x)).ToList();
            foreach (var dlcFolder in dlcFolders)
            {
                string unpackedDir = Path.Combine(dlcFolder, @"CookedPCConsole");
                string sfar = Path.Combine(unpackedDir, @"Default.sfar");
                if (File.Exists(sfar))
                {
                    FileInfo fi = new FileInfo(sfar);
                    var sfarsize = fi.Length;
                    if (sfarsize > 32)
                    {
                        //Packed
                        var filesInSfarDir = Directory.EnumerateFiles(unpackedDir).ToList();
                        if (filesInSfarDir.Any(d => !Path.GetFileName(d).Equals("PCConsoleTOC.bin", StringComparison.InvariantCultureIgnoreCase) && //pcconsoletoc will be produced for all folders even with autotoc asi even if its not needed
                                                    UnpackedFileExtensions.Contains(Path.GetExtension(d.ToLower()))))
                        {
                            inconsistentDLC.Add(dlcFolder);
                        }
                    }
                    else
                    {
                        //We do not consider unpacked DLC when checking for consistency
                    }
                }
            }
            return inconsistentDLC;
        }

        private GameTarget target { get; set; }
        private ManifestMode mode { get; set; }
        private List<ManifestFile> manifestFilesToInstall { get; set; }

        private Precheck()
        {
            // No public constructor
        }

        /// <summary>
        /// Checks the texture map for consistency to the current game state
        /// </summary>
        /// <returns></returns>
        private List<string> checkForReplacedAddedRemovedFiles()
        {
            List<string> blacklistedMods = new List<string>();
            List<string> addedFiles = new List<string>();
            void handleIPC(string command, string param)
            {
                switch (command)
                {
                    case "ERROR_REMOVED_FILE":
                        blacklistedMods.Add($"File removed: {param}");
                        break;
                    case "ERROR_ADDED_FILE":
                        addedFiles.Add(param); //Used to suppress vanilla mod file
                        blacklistedMods.Add($"File added: {param}");
                        break;
                    case "ERROR_VANILLA_MOD_FILE":
                        if (!addedFiles.Contains(param, StringComparer.InvariantCultureIgnoreCase))
                        {
                            blacklistedMods.Add($"File replaced/modified: {param}");
                        }
                        break;
                    default:
                        Debug.WriteLine($"Unhandled IPC: {command} {param}");
                        break;
                }
            }

            string args = $@"--check-game-data-mismatch --gameid {target.Game.ToGameNum()} --ipc";
            int lastExitCode = int.MinValue;
            MEMIPCHandler.RunMEMIPCUntilExit(args,
                null,
                handleIPC,
                x => Log.Error($"[AICORE] StdError checking texture map: {x}"),
                x => lastExitCode = x
            );
            // Todo: Handle exit codes
            return blacklistedMods;
        }

        private List<string> checkBlacklistedMods()
        {
            List<string> blacklistedMods = new List<string>();
            void handleIPC(string command, string param)
            {
                switch (command)
                {
                    case "ERROR":
                        Log.Error($"[AICORE] Incompatible mod was detected: {param}");
                        blacklistedMods.Add(param);
                        break;
                    default:
                        Debug.WriteLine($"Unhandled IPC: {command} {param}");
                        break;
                }
            }

            string args = $"--detect-bad-mods --gameid {target.Game.ToGameNum()} --ipc";
            int lastExitCode = int.MinValue;
            MEMIPCHandler.RunMEMIPCUntilExit(args,
                null,
                handleIPC,
                x => Log.Error($"[AICORE] StdError checking for blacklisted mods: {x}"),
                x => lastExitCode = x
            );
            // Todo: Handle exit codes
            return blacklistedMods;
        }

        private bool checkRequiredFiles(out string failureReason)
        {
            failureReason = null;
            if (mode == ManifestMode.Free) return true; //no files are required in free mode

            var texturesInfo = target.GetInstalledALOTInfo();
            if (mode == ManifestMode.ALOT)
            {
                bool hasAlot = texturesInfo != null && texturesInfo.ALOTVER > 0;
                // ALOT main is required if alot is not installed
                var alotMainFile = manifestFilesToInstall.FirstOrDefault(x => x.AlotVersionInfo.ALOTVER != 0 && x.AlotVersionInfo.ALOTUPDATEVER == 0 && x.ApplicableGames.HasFlag(target.Game.ToApplicableGame())); //main file only
                if (alotMainFile == null)
                {
                    Log.Error("[AICORE] ALOT manifest is missing the ALOT main file!");
                    failureReason = "The main ALOT file is missing from the manifest! Contact the developers to get this resolved.";
                    return false;
                }

                if (hasAlot)
                {
                    if (texturesInfo.ALOTVER != alotMainFile.AlotVersionInfo.ALOTVER && alotMainFile.Ready)
                    {
                        // ALOT main versions are different
                        Log.Warning($"[AICORE] Precheck failed: {alotMainFile.FriendlyName} file cannot be installed on top of an existing version of ALOT that is different: {texturesInfo.ALOTVER}.{texturesInfo.ALOTUPDATEVER}");
                        failureReason = $"[AICORE] The manifest version of ALOT currently is {alotMainFile.AlotVersionInfo.ALOTVER}, but your current installation is {texturesInfo.ALOTVER}.{texturesInfo.ALOTUPDATEVER}. You cannot install new major versions of ALOT on top of each other, you must restore your game and perform a clean installation.";
                        return false;
                    }

                    // Other files can be installed on this installation that are not alot major/main
                }
                else
                {
                    // ALOT not installed
                    if (!alotMainFile.Ready)
                    {
                        Log.Warning($"Precheck failed: {alotMainFile.FriendlyName} file is not imported in the texture library and ALOT mode rules require it to be for initial texture installation");
                        failureReason = $"{alotMainFile.FriendlyName} must be imported into the texture library in order to install textures in ALOT mode.";
                        return false;
                    }
                }

                // ALOT update is required if available in manifest but not installed
                var alotUpdateFile = manifestFilesToInstall.FirstOrDefault(x => x.AlotVersionInfo.ALOTUPDATEVER != 0 && x.ApplicableGames.HasFlag(target.Game.ToApplicableGame()));
                if (alotUpdateFile != null)
                {
                    // An ALOT update exists for our game
                    if (hasAlot && alotUpdateFile.AlotVersionInfo > texturesInfo && !alotUpdateFile.Ready)
                    {
                        // This file is required but is not ready
                        Log.Warning("[AICORE] Precheck failed: ALOT update file is not imported but ALOT mode rules require the latest update to be imported for any subsequent installations on top of the existing ALOT install");
                        failureReason = $"ALOT updates are required to be installed if available. Your ALOT version does not have the latest update, and the ALOT update file is not imported into the texture library. You must import the {alotUpdateFile.FriendlyName} file before you can install textures in ALOT mode.";
                        return false;
                    }

                    // ALOT not installed but file not ready
                    if (!alotUpdateFile.Ready)
                    {
                        // This file is required but is not ready
                        Log.Warning("[AICORE] Precheck failed: ALOT update file is not imported but ALOT mode rules require the latest update to be imported for first time installation");
                        failureReason = $"ALOT updates are required to be installed if available. The ALOT update file is not imported into the texture library. You must import the {alotUpdateFile.FriendlyName} file before you can install textures in ALOT mode.";
                        return false;
                    }

                    if (hasAlot && texturesInfo.ALOTVER != alotUpdateFile.AlotVersionInfo.ALOTVER &&
                        alotUpdateFile.Ready)
                    {
                        Log.Warning("[AICORE] Precheck failed: ALOT update file is not applicable to installed version of ALOT");
                        // Update cannot be applied to this installation
                        failureReason = $"The file {alotUpdateFile.FriendlyName} cannot be installed against the currently installed ALOT version ({texturesInfo.ALOTVER}.{texturesInfo.ALOTUPDATEVER}).";
                        return false;
                    }
                }

                return true;
            }

            // MEUITM mode doesn't have any conditions currently except MEUITM
            return manifestFilesToInstall.Any(x => x.Recommendation == RecommendationType.Required);
        }
    }
}