﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.Helpers.AppSettings;
using ALOTInstallerCore.ModManager.Objects;
using ALOTInstallerCore.ModManager.Services;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using Serilog;
#if WINDOWS
using Microsoft.Win32;
using ALOTInstallerCore.PlatformSpecific.Windows;
#endif

namespace ALOTInstallerCore.ModManager.ME3Tweaks
{


    /// <summary>
    /// Class that contains classes for handling backups and restores
    /// </summary>
    public static class BackupHandler
    {

        #region BACKUP
        public class GameBackup : INotifyPropertyChanged
        {
            public string GameName => Game.ToGameName();
            public MEGame Game { get; }
            public ObservableCollectionExtended<GameTarget> AvailableTargetsToBackup { get; } = new ObservableCollectionExtended<GameTarget>();

            /// <summary>
            /// Reports the current progress of the backup
            /// </summary>
            public Action<long, long> BackupProgressCallback { get; set; }
            /// <summary>
            /// Called when there is a blocking action, such as game running
            /// </summary>
            public Action<string, string> BlockingActionCallback { get; set; }
            /// <summary>
            /// Called when there is a warning that needs a yes/no answer
            /// </summary>
            public Func<string, string, string, string, bool> WarningActionCallback { get; set; }
            /// <summary>
            /// Called when the user must select a game executable (for backup). Return null to indicate the user aborted the prompt.
            /// </summary>
            public Func<MEGame, GameTarget> SelectGameExecutableCallback { get; set; }

            /// <summary>
            /// Called when the user must select a backup folder destination. Return null to indicate user aborted the prompt.
            /// </summary>
            public Func<string> SelectGameBackupFolderDestination { get; set; }
            /// <summary>
            /// Called when the backup thread has completed.
            /// </summary>
            public Action NotifyBackupThreadCompleted { get; set; }
            /// <summary>
            /// Called when there is a warning that has a potentially long list of items in it, with a title, top and bottom message, as well as a list of strings. These items should be placed in a scrolling mechanism
            /// </summary>
            public Func<string, string, string, List<string>, string, string, bool> WarningListCallback { get; set; }

            /// <summary>
            /// Called when there is a new status message that should be displayed, such as what is being backed up.
            /// </summary>
            public Action<string> UpdateStatusCallback { get; set; }
            /// <summary>
            /// Sets the progressbar (if any) with this backup operation to indeterminate or not.
            /// </summary>
            public Action<bool> SetProgressIndeterminateCallback { get; set; }

            public GameBackup(MEGame game, IEnumerable<GameTarget> availableBackupSources)
            {
                this.Game = game;
                this.AvailableTargetsToBackup.AddRange(availableBackupSources);
                this.AvailableTargetsToBackup.Add(new GameTarget(Game, "Link to an existing backup", false, true));
                ResetBackupStatus();
            }

            public bool PerformRestore(string targetDirectory)
            {

                return true;
            }

            public bool PerformBackup()
            {
                var targetToBackup = BackupSourceTarget;
                if (!targetToBackup.IsCustomOption)
                {
                    Log.Information($"[AICORE] PerformBackup() on {BackupSourceTarget.TargetPath}");
                    // Backup target
                    if (Utilities.IsGameRunning(targetToBackup.Game))
                    {
                        BlockingActionCallback?.Invoke("Cannot backup game", $"Cannot backup while {BackupSourceTarget.Game.ToGameName()} is running.");
                        return false;
                    }
                }
                else
                {
                    // Point to existing game installation
                    Log.Information(@"[AICORE] PerformBackup() with IsCustomOption.");
                    var linkOK = WarningActionCallback?.Invoke("Ensure correct game chosen", "The path you specify will be checked if it is a vanilla backup. Once this check is complete it will be marked as a backup and modding tools will refuse to modify it. Ensure this is not your active game path or you will be unable to mod the game.",
                        "I understand", "Abort linking");
                    if (!linkOK.HasValue || !linkOK.Value)
                    {
                        Log.Information(@"[AICORE] User aborted linking due to dialog");
                        return false;
                    }

                    Log.Information(@"[AICORE] Prompting user to select executable of link target");

                    targetToBackup = SelectGameExecutableCallback?.Invoke(Game);

                    if (targetToBackup == null)
                    {
                        Log.Warning("[AICORE] User did not choose game executable to link as backup. Aborting");
                        return false;
                    }

                    if (AvailableTargetsToBackup.Any(x => x.TargetPath.Equals(targetToBackup.TargetPath, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        // Can't point to an existing modding target
                        Log.Error(@"[AICORE] This target is not valid to point to as a backup: It is listed a modding target already, it must be removed as a target first");
                        BlockingActionCallback?.Invoke("Cannot backup game", "Cannot use this target as backup: It is the current game path for this game.");
                        return false;
                    }

                    var validationFailureReason = targetToBackup.ValidateTarget(ignoreCmmVanilla: true);
                    if (!targetToBackup.IsValid)
                    {
                        Log.Error(@"[AICORE] This installation is not valid to point to as a backup: " + validationFailureReason);
                        BlockingActionCallback?.Invoke("Cannot backup game", $"Cannot use this target as backup: {validationFailureReason}");
                        return false;
                    }
                }


                Log.Information(@"[AICORE] Starting the backup thread. Checking path: " + targetToBackup.TargetPath);
                BackupInProgress = true;

                List<string> nonVanillaFiles = new List<string>();

                void nonVanillaFileFoundCallback(string filepath)
                {
                    Log.Error($@"[AICORE] Non-vanilla file found: {filepath}");
                    nonVanillaFiles.Add(filepath.Substring(targetToBackup.TargetPath.Length + 1)); //Oh goody i'm sure this won't cause issues
                }

                List<string> inconsistentDLC = new List<string>();

                void inconsistentDLCFoundCallback(string filepath)
                {
                    if (targetToBackup.Supported)
                    {
                        Log.Error($@"[AICORE] DLC is in an inconsistent state: {filepath}");
                        inconsistentDLC.Add(filepath);
                    }
                    else
                    {
                        Log.Error(@"[AICORE] Detected an inconsistent DLC, likely due to an unofficial copy of the game");
                    }
                }

                UpdateStatusCallback?.Invoke("Validating backup source");
                SetProgressIndeterminateCallback?.Invoke(true);
                Log.Information(@"[AICORE] Checking target is vanilla");
                bool isVanilla = VanillaDatabaseService.ValidateTargetAgainstVanilla(targetToBackup, nonVanillaFileFoundCallback);

                Log.Information(@"[AICORE] Checking DLC consistency");
                bool isDLCConsistent = VanillaDatabaseService.ValidateTargetDLCConsistency(targetToBackup, inconsistentDLCCallback: inconsistentDLCFoundCallback);

                Log.Information(@"[AICORE] Checking only vanilla DLC is installed");
                List<string> dlcModsInstalled = VanillaDatabaseService.GetInstalledDLCMods(targetToBackup).Select(x =>
                {
                    var tpmi = ThirdPartyServices.GetThirdPartyModInfo(x, targetToBackup.Game);
                    if (tpmi != null) return $@"{x} ({tpmi.modname})";
                    return x;
                }).ToList();
                List<string> installedDLC = VanillaDatabaseService.GetInstalledOfficialDLC(targetToBackup);
                List<string> allOfficialDLC = MEDirectories.OfficialDLC(targetToBackup.Game).ToList();

                if (installedDLC.Count() < allOfficialDLC.Count())
                {
                    var dlcList = string.Join("\n - ", allOfficialDLC.Except(installedDLC).Select(x => $@"{MEDirectories.OfficialDLCNames(targetToBackup.Game)[x]} ({x})")); //do not localize
                    dlcList = @" - " + dlcList;
                    Log.Information(@"[AICORE] The following dlc will be missing in the backup if user continues: " + dlcList);
                    string message =
                        $"This target does not have have all OFFICIAL DLC installed. Ensure you have installed all OFFICIAL DLC you want to include in your backup, otherwise a game restore will not include all of it.\n\nThe following DLC is not installed:\n{dlcList}\n\nMake a backup of this target?";
                    var okToBackup = WarningActionCallback?.Invoke("Warning: some official DLC missing", message, "Continue backing up", "Abort backup");
                    if (!okToBackup.HasValue || !okToBackup.Value)
                    {
                        Log.Information("[AICORE] User canceled backup due to some missing data");
                        return false;
                    }
                }

                if (!isDLCConsistent)
                {
                    if (targetToBackup.Supported)
                    {
                        BlockingActionCallback?.Invoke("Issue detected", "Detected inconsistent DLCs, which is due to having vanilla DLC files with unpacked archives. Delete (do not repair) your game installation and reinstall the game to fix this issue.");
                        return false;
                    }
                    else
                    {
                        BlockingActionCallback?.Invoke("Issue detected", "Detected inconsistent DLCs, likely due to using an unofficial copy of the game. This game cannot be backed up.");
                        return false;
                    }
                }


                if (!isVanilla)
                {
                    //Show UI for non vanilla
                    string message = "The following files were found to be modified and are not vanilla. You can continue making a backup, however other modding tools such as ME3Tweaks Mod Manager will not accept this as a valid backup source. It is highly recommended that all backups of a game be unmodified, as a broken modified backup is a worthless backup.";
                    string bottomMessage = "Make a backup anyways (NOT RECOMMENDED)?";
                    var continueBackup = WarningListCallback?.Invoke("Found non vanilla files", message, bottomMessage, nonVanillaFiles, "Backup anyways", "Abort backup");
                    if (!continueBackup.HasValue || !continueBackup.Value)
                    {
                        Log.Information("[AICORE] User aborted backup due to non-vanilla files found");
                        return false;
                    }
                }
                else if (Enumerable.Any(dlcModsInstalled))
                {
                    //Show UI for non vanilla
                    string message = "The following DLC mods were found to be installed. These mods are not part of the original game. You can continue making a backup, however other modding tools such as ME3Tweaks Mod Manager will not accept this as a valid backup source. It is highly recommended that all backups of a game be unmodified, as a broken modified backup is a worthless backup.";
                    string bottomMessage = "Make a backup anyways (NOT RECOMMENDED)?";
                    var continueBackup = WarningListCallback?.Invoke("Found installed DLC mods", message, bottomMessage, dlcModsInstalled, "Backup anyways", "Abort backup");
                    if (!continueBackup.HasValue || !continueBackup.Value)
                    {
                        Log.Information("[AICORE] User aborted backup due to found DLC mods");
                        return false;
                    }
                }

                BackupStatus = "Waiting for user input";

                string backupPath = null;
                if (!targetToBackup.IsCustomOption)
                {
                    // Creating a new backup
                    Log.Information(@"[AICORE] Prompting user to select backup destination");
                    backupPath = SelectGameBackupFolderDestination?.Invoke();
                    if (backupPath != null && Directory.Exists(backupPath))
                    {
                        Log.Information(@"[AICORE] Backup path chosen: " + backupPath);
                        bool okToBackup = validateBackupPath(backupPath, targetToBackup);
                        if (!okToBackup)
                        {
                            EndBackup();
                            return false;
                        }
                    }
                    else
                    {
                        EndBackup();
                        return false;
                    }
                }
                else
                {
                    Log.Information(@"[AICORE] Linking existing backup at " + targetToBackup.TargetPath);
                    backupPath = targetToBackup.TargetPath;
                    // Linking existing backup
                    bool okToBackup = validateBackupPath(targetToBackup.TargetPath, targetToBackup);
                    if (!okToBackup)
                    {
                        EndBackup();
                        return false;
                    }
                }

                if (!targetToBackup.IsCustomOption)
                {
                    #region callbacks and copy code

                    // Copy to new backup
                    void fileCopiedCallback()
                    {
                        ProgressValue++;
                        BackupProgressCallback?.Invoke(ProgressValue, ProgressMax);
                    }

                    string dlcFolderpath = M3Directories.GetDLCPath(targetToBackup) + Path.DirectorySeparatorChar;
                    int dlcSubStringLen = dlcFolderpath.Length;
                    var officialDLCNames = MEDirectories.OfficialDLCNames(targetToBackup.Game);

                    bool aboutToCopyCallback(string file)
                    {
                        try
                        {
                            if (file.Contains(@"\cmmbackup\")) return false; //do not copy cmmbackup files
                            if (file.StartsWith(dlcFolderpath, StringComparison.InvariantCultureIgnoreCase))
                            {
                                //It's a DLC!
                                string dlcname = file.Substring(dlcSubStringLen);
                                var dlcFolderNameEndPos = dlcname.IndexOf(Path.DirectorySeparatorChar);
                                if (dlcFolderNameEndPos > 0)
                                {
                                    dlcname = dlcname.Substring(0, dlcFolderNameEndPos);
                                    if (officialDLCNames.TryGetValue(dlcname, out var hrName))
                                    {
                                        UpdateStatusCallback?.Invoke($"Backing up {hrName}");
                                    }
                                    else
                                    {
                                        UpdateStatusCallback?.Invoke($"Backing up {dlcname}");
                                    }
                                }
                                else
                                {
                                    // Loose files in the DLC folder
                                    UpdateStatusCallback?.Invoke($"Backing up basegame");
                                }
                            }
                            else
                            {
                                //It's basegame
                                if (file.EndsWith(@".bik"))
                                {
                                    UpdateStatusCallback?.Invoke("Backing up movies");
                                }
                                else if (new FileInfo(file).Length > 52428800)
                                {

                                    UpdateStatusCallback?.Invoke($"Backing up {Path.GetFileName(file)}");
                                }
                                else
                                {
                                    UpdateStatusCallback?.Invoke("Backing up basegame");
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Log.Error($"[AICORE] Error about to copy file: {e.Message}");
                            CoreCrashes.TrackError?.Invoke(e);
                        }

                        return true;
                    }

                    void bigFileProgressCallback(string fileBeingCopied, long dataCopied, long totalDataToCopy)
                    {
                        if (fileBeingCopied.StartsWith(dlcFolderpath, StringComparison.InvariantCultureIgnoreCase))
                        {
                            //It's a DLC!
                            string dlcname = fileBeingCopied.Substring(dlcSubStringLen);
                            int index = dlcname.IndexOf(Path.DirectorySeparatorChar);
                            try
                            {
                                string prefix = "Backing up ";
                                dlcname = dlcname.Substring(0, index);
                                if (officialDLCNames.TryGetValue(dlcname, out var hrName))
                                {
                                    prefix += hrName;
                                }
                                else
                                {
                                    prefix += dlcname;
                                }

                                UpdateStatusCallback?.Invoke($"{prefix} {(int)(dataCopied * 100d / totalDataToCopy)}%");
                            }
                            catch
                            {

                            }
                        }
                        else
                        {
                            UpdateStatusCallback?.Invoke($"Backing up {Path.GetFileName(fileBeingCopied)} {(int)(dataCopied * 100d / totalDataToCopy)}%");
                        }
                    }


                    void totalFilesToCopyCallback(int total)
                    {
                        ProgressValue = 0;
                        ProgressIndeterminate = false;
                        ProgressMax = total;
                    }

                    BackupStatus = "Creating backup";
                    Log.Information($@"[AICORE] Backing up {targetToBackup.TargetPath} to {backupPath}");
                    CopyTools.CopyAll_ProgressBar(new DirectoryInfo(targetToBackup.TargetPath),
                        new DirectoryInfo(backupPath),
                        totalItemsToCopyCallback: totalFilesToCopyCallback,
                        aboutToCopyCallback: aboutToCopyCallback,
                        fileCopiedCallback: fileCopiedCallback,
                        ignoredExtensions: new[] { @"*.pdf", @"*.mp3", @"*.wav" },
                        bigFileProgressCallback: bigFileProgressCallback);
                    #endregion
                }

                // Write location
                WriteBackupLocation(Game, backupPath);

                // Write vanilla marker
                if (isVanilla && !Enumerable.Any(dlcModsInstalled))
                {
                    var cmmvanilla = Path.Combine(backupPath, @"cmm_vanilla");
                    if (!File.Exists(cmmvanilla))
                    {
                        Log.Information(@"[AICORE] Writing cmm_vanilla to " + cmmvanilla);
                        File.Create(cmmvanilla).Close();
                    }
                }
                else
                {
                    Log.Information("[AICORE] Not writing vanilla marker as this is not a vanilla backup");
                }

                Log.Information(@"[AICORE] Backup completed.");

                CoreAnalytics.TrackEvent?.Invoke(@"Created a backup", new Dictionary<string, string>()
                        {
                                {@"game", Game.ToString()},
                                {@"Result", @"Success"},
                                {@"Type", targetToBackup.IsCustomOption ? @"Linked" : @"Copy"}
                        });

                EndBackup();
                return true;
            }

            private bool validateBackupPath(string backupPath, GameTarget targetToBackup)
            {
                //Check empty
                if (!targetToBackup.IsCustomOption && Directory.Exists(backupPath))
                {
                    if (Directory.GetFiles(backupPath).Length > 0 ||
                        Directory.GetDirectories(backupPath).Length > 0)
                    {
                        //Directory not empty
                        Log.Error(@"[AICORE] Selected backup directory is not empty.");
                        BlockingActionCallback?.Invoke("Invalid backup destination",
                            "The backup destination directory must be empty. Delete the files and folders in this directory, or select a different empty path.");
                        return false;
                    }
                }
                if (!targetToBackup.IsCustomOption)
                {

                    //Check space
                    DriveInfo di = new DriveInfo(backupPath);
                    var requiredSpace = (long)(Utilities.GetSizeOfDirectory(new DirectoryInfo(targetToBackup.TargetPath)) * 1.1); //10% buffer
                    Log.Information($@"[AICORE] Backup space check. Backup size required: {FileSize.FormatSize(requiredSpace)}, free space: {FileSize.FormatSize(di.AvailableFreeSpace)}");
                    if (di.AvailableFreeSpace < requiredSpace)
                    {
                        //Not enough space.
                        Log.Error($@"[AICORE] Not enough disk space to create backup at {backupPath}");
                        BlockingActionCallback?.Invoke("Not enough free disk space", $"There is not enough free disk space to make a game backup at this location.\n\nFree space: {FileSize.FormatSize(di.AvailableFreeSpace)}\nRequired space: {FileSize.FormatSize(requiredSpace)}");
                        return false;
                    }

                    //Check writable
                    var writable = Utilities.IsDirectoryWritable(backupPath);
                    if (!writable)
                    {
                        //Not enough space.
                        Log.Error($@"[AICORE] Backup destination selected is not writable.");
                        BlockingActionCallback?.Invoke("Invalid backup destination", "Selected backup folder does not have write permissions from this account. Select a different directory.");
                        return false;
                    }
                }
                //Check is Documents folder
                var docsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"BioWare", targetToBackup.Game.ToGameName());
                if (backupPath.Equals(docsPath, StringComparison.InvariantCultureIgnoreCase) || backupPath.IsSubPathOf(docsPath))
                {
                    Log.Error(@"[AICORE] User chose path in or around the documents path for the game - not allowed as game can load files from here.");

                    BlockingActionCallback?.Invoke($"Invalid backup destination", $"The backup destination cannot be a subdirectory of the Documents/BioWare/{targetToBackup.Game.ToGameName()} folder. Select a different directory.");
                    return false;
                }


                //Check it is not subdirectory of the game (we might want to check its not subdir of a target)
                foreach (var target in AvailableTargetsToBackup)
                {
                    if (backupPath.IsSubPathOf(target.TargetPath))
                    {
                        //Not enough space.
                        Log.Error($@"[AICORE] A backup cannot be created in a subdirectory of a game. {backupPath} is a subdir of {targetToBackup.TargetPath}");
                        BlockingActionCallback?.Invoke("Invalid backup destination", $"You cannot place a backup into a subdirectory of the game you are backing up. Select another directory.");
                        return false;
                    }
                }

                return true;
            }


            private void EndBackup()
            {
                Log.Information(@"[AICORE] EndBackup()");
                ResetBackupStatus();
                ProgressIndeterminate = false;
                ProgressVisible = false;
                BackupInProgress = false;
            }

            private void ResetBackupStatus()
            {
                BackupService.UpdateBackupStatus(Game, false);
            }

            public event PropertyChangedEventHandler PropertyChanged;
            public GameTarget BackupSourceTarget { get; set; }
            public string BackupStatus { get; set; }
            public int ProgressMax { get; set; } = 100;
            public int ProgressValue { get; set; } = 0;
            public bool ProgressIndeterminate { get; set; } = true;
            public bool ProgressVisible { get; set; } = false;
            public bool BackupInProgress { get; set; }

        }

        #endregion

        #region RESTORE

        public class GameRestore : INotifyPropertyChanged
        {
            private MEGame Game;

            public event PropertyChangedEventHandler PropertyChanged;

            /// <summary>
            /// Callback for when there is a blocking error and the restore cannot be performed
            /// </summary>
            public Action<string, string> BlockingErrorCallback { get; set; }
            /// <summary>
            /// Callback for when there is an error during the restore. This may mean you need to keep UI target available still so user can try again without losing the target
            /// </summary>
            public Action<string, string> RestoreErrorCallback { get; set; }
            /// <summary>
            /// Callback to select a directory for custom restore location
            /// </summary>
            public Func<string, string, string> SelectDestinationDirectoryCallback { get; set; }
            /// <summary>
            /// Callback to confirm restoration over existing game
            /// </summary>
            public Func<string, string, bool> ConfirmationCallback { get; set; }
            /// <summary>
            /// Callback when the status string on the UI should be updated
            /// </summary>
            public Action<string> UpdateStatusCallback { get; set; }
            /// <summary>
            /// Callback when there is a progress update for the UI
            /// </summary>
            public Action<long, long> UpdateProgressCallback { get; set; }
            /// <summary>
            /// Callback when the progressbar should change indeterminate states
            /// </summary>
            public Action<bool> SetProgressIndeterminateCallback { get; set; }

            public GameRestore(MEGame game)
            {
                this.Game = game;
            }

            /// <summary>
            /// Restores the game to the specified directory (game location). Pass in null if you wish to restore to a custom location.
            /// </summary>
            /// <param name="destinationDirectory">Game directory that will be replaced with backup</param>
            /// <returns></returns>
            public bool PerformRestore(string destinationDirectory)
            {
                int ProgressValue = 0;
                int ProgressMax = 0;
                if (Utilities.IsGameRunning(Game))
                {
                    BlockingErrorCallback?.Invoke("Cannot restore game", $"Cannot restore {Game.ToGameName()} while an instance of it is running.");
                    return false;
                }

                bool restore = destinationDirectory == null; // Restore to custom location
                if (!restore)
                {
                    var confirmDeletion = ConfirmationCallback?.Invoke($"Restoring {Game.ToGameName()} will delete existing installation", $"Restoring {Game.ToGameName()} will delete the existing installation, copy your backup to its original location, and reset the texture Level of Detail (LOD) settings for your game.");
                    restore |= confirmDeletion.HasValue && confirmDeletion.Value;
                }

                if (restore)
                {
                    string backupPath = BackupService.GetGameBackupPath(Game, out var isVanilla, forceCmmVanilla: false);

                    if (destinationDirectory == null)
                    {
                        destinationDirectory = SelectDestinationDirectoryCallback?.Invoke("Select destination location for restore", "Select a directory to restore the game to. This directory must be empty.");
                        if (destinationDirectory != null)
                        {
                            //Check empty
                            if (Directory.Exists(destinationDirectory))
                            {
                                if (Directory.GetFiles(destinationDirectory).Length > 0 || Directory.GetDirectories(destinationDirectory).Length > 0)
                                {
                                    //Directory not empty
                                    BlockingErrorCallback?.Invoke("Cannot restore game", "The destination directory for restores must be an empty directory. Remove the files and folders in this directory, or choose another directory.");
                                    return false;
                                }

                                //TODO: PREVENT RESTORING TO DOCUMENTS/BIOWARE

                            }

                            CoreAnalytics.TrackEvent?.Invoke(@"Chose to restore game to custom location", new Dictionary<string, string>() { { @"Game", Game.ToString() } });

                        }
                        else
                        {
                            Log.Warning("[AICORE] User declined to choose destination directory");
                            return false;
                        }
                    }

                    SetProgressIndeterminateCallback?.Invoke(true);
                    UpdateStatusCallback?.Invoke("Deleting existing game installation");
                    if (Directory.Exists(destinationDirectory))
                    {
                        if (Enumerable.Any(Directory.GetFiles(destinationDirectory)) || Enumerable.Any(Directory.GetDirectories(destinationDirectory)))
                        {
                            Log.Information(@"[AICORE] Deleting existing game directory: " + destinationDirectory);
                            try
                            {
                                bool deletedDirectory = Utilities.DeleteFilesAndFoldersRecursively(destinationDirectory);
                                if (deletedDirectory != true)
                                {
                                    RestoreErrorCallback?.Invoke("Could not delete game directory", $"Could not delete the game directory for {Game.ToGameName()}. The game will be in a semi deleted state, please manually delete it and then restore to the same location as the game to fully restore the game.");
                                    //b.Result = RestoreResult.ERROR_COULD_NOT_DELETE_GAME_DIRECTORY;
                                    return false;
                                }
                            }
                            catch (Exception ex)
                            {
                                //todo: handle this better
                                Log.Error($@"[AICORE] Exception deleting game directory: {destinationDirectory}: {ex.Message}");
                                RestoreErrorCallback?.Invoke("Error deleting game directory", $"Could not delete the game directory for {Game.ToGameName()}: {ex.Message}. The game will be in a semi deleted state, please manually delete it and then restore to the same location as the game to fully restore the game.");
                                //b.Result = RestoreResult.EXCEPTION_DELETING_GAME_DIRECTORY;
                                return false;
                            }
                        }
                    }
                    else
                    {
                        Log.Error(@"[AICORE] Game directory not found! Was it removed while the app was running?");
                    }

                    //todo: remove IndirectSound settings? (MEUITM)

                    var created = Utilities.CreateDirectoryWithWritePermission(destinationDirectory);
                    if (!created)
                    {
                        RestoreErrorCallback?.Invoke("Error creating game directory", $"Could not create the game directory for {Game.ToGameName()}. You may not have permissions to create folders in the directory that contains the game directory.");
                        //b.Result = RestoreResult.ERROR_COULD_NOT_CREATE_DIRECTORY;
                        return false;
                    }

                    UpdateStatusCallback?.Invoke("Restoring game from backup");
                    //callbacks

                    #region callbacks

                    void fileCopiedCallback()
                    {
                        ProgressValue++;
                        if (ProgressMax != 0)
                        {
                            UpdateProgressCallback?.Invoke(ProgressValue, ProgressMax);
                        }
                    }

                    string dlcFolderpath = MEDirectories.GetDLCPath(Game, backupPath) + Path.DirectorySeparatorChar; //\ at end makes sure we are restoring a subdir
                    int dlcSubStringLen = dlcFolderpath.Length;
                    //Debug.WriteLine(@"DLC Folder: " + dlcFolderpath);
                    //Debug.Write(@"DLC Folder path len:" + dlcFolderpath);

                    // Cached stuff to avoid hitting same codepath thousands of times
                    var officialDLCNames = MEDirectories.OfficialDLCNames(Game);

                    bool aboutToCopyCallback(string fileBeingCopied)
                    {
                        if (fileBeingCopied.Contains(@"\cmmbackup\")) return false; //do not copy cmmbackup files
                        Debug.WriteLine(fileBeingCopied);
                        if (fileBeingCopied.StartsWith(dlcFolderpath, StringComparison.InvariantCultureIgnoreCase))
                        {
                            //It's a DLC!
                            string dlcname = fileBeingCopied.Substring(dlcSubStringLen);
                            int index = dlcname.IndexOf(Path.DirectorySeparatorChar);
                            if (index > 0) //Files directly in the DLC directory won't have path sep
                            {
                                try
                                {
                                    dlcname = dlcname.Substring(0, index);
                                    if (officialDLCNames.TryGetValue(dlcname, out var hrName))
                                    {
                                        UpdateStatusCallback?.Invoke($"Restoring {hrName}");
                                    }
                                    else
                                    {
                                        UpdateStatusCallback?.Invoke($"Restoring {dlcname}");
                                    }
                                }
                                catch (Exception e)
                                {
                                    CoreCrashes.TrackError2?.Invoke(e, new Dictionary<string, string>()
                                    {
                                        {@"Source", @"Restore UI display callback"},
                                        {@"Value", fileBeingCopied},
                                        {@"DLC Folder path", dlcFolderpath}
                                    });
                                }
                            }
                        }
                        else
                        {
                            //It's basegame
                            if (fileBeingCopied.EndsWith(@".bik"))
                            {
                                UpdateStatusCallback?.Invoke("Restoring movies");
                            }
                            else if (new FileInfo(fileBeingCopied).Length > 52428800)
                            {
                                UpdateStatusCallback?.Invoke($"Restoring {Path.GetFileName(fileBeingCopied)}");
                            }
                            else
                            {
                                UpdateStatusCallback?.Invoke($"Restoring basegame");
                            }
                        }

                        return true;
                    }

                    void totalFilesToCopyCallback(int total)
                    {
                        ProgressValue = 0;
                        SetProgressIndeterminateCallback?.Invoke(false);
                        ProgressMax = total;
                    }

                    void bigFileProgressCallback(string fileBeingCopied, long dataCopied, long totalDataToCopy)
                    {
                        if (fileBeingCopied.StartsWith(dlcFolderpath, StringComparison.InvariantCultureIgnoreCase))
                        {
                            //It's a DLC!
                            string dlcname = fileBeingCopied.Substring(dlcSubStringLen);
                            int index = dlcname.IndexOf(Path.DirectorySeparatorChar);
                            try
                            {
                                string prefix = "Restoring ";
                                dlcname = dlcname.Substring(0, index);
                                if (officialDLCNames.TryGetValue(dlcname, out var hrName))
                                {
                                    prefix += hrName;
                                }
                                else
                                {
                                    prefix += dlcname;
                                }

                                UpdateStatusCallback?.Invoke($"{prefix} {(int)(dataCopied * 100d / totalDataToCopy)}%");
                            }
                            catch
                            {

                            }
                        }
                        else
                        {
                            UpdateStatusCallback?.Invoke($"Restoring {Path.GetFileName(fileBeingCopied)} {(int)(dataCopied * 100d / totalDataToCopy)}%");
                        }
                    }

                    #endregion

                    UpdateStatusCallback?.Invoke("Calculating how many files will be restored");
                    Log.Information($@"[AICORE] Copying backup to game directory: {backupPath} -> {destinationDirectory}");
                    CopyTools.CopyAll_ProgressBar(new DirectoryInfo(backupPath), new DirectoryInfo(destinationDirectory),
                        totalItemsToCopyCallback: totalFilesToCopyCallback,
                        aboutToCopyCallback: aboutToCopyCallback,
                        fileCopiedCallback: fileCopiedCallback,
                        ignoredExtensions: new[] { @"*.pdf", @"*.mp3" },
                        bigFileProgressCallback: bigFileProgressCallback);
                    Log.Information(@"[AICORE] Restore of game data has completed");

                    //Check for cmmvanilla file and remove it present

                    string cmmVanilla = Path.Combine(destinationDirectory, @"cmm_vanilla");
                    if (File.Exists(cmmVanilla))
                    {
                        Log.Information(@"[AICORE] Removing cmm_vanilla file");
                        File.Delete(cmmVanilla);
                    }

                    Log.Information(@"[AICORE] Restore thread wrapping up");
                    Locations.ReloadTarget(Game);
                    return true;
                    //b.Result = RestoreResult.RESTORE_OK;
                    /*if (b.Result is RestoreResult result)
                    {
                        switch (result)
                        {
                            case RestoreResult.ERROR_COULD_NOT_CREATE_DIRECTORY:
                                Analytics.TrackEvent(@"Restored game", new Dictionary<string, string>()
                                {
                                    {@"Game", Game.ToString()},
                                    {@"Result", @"Failure, Could not create target directory"}
                                });
                                M3L.ShowDialog(window, M3L.GetString(M3L.string_dialogCouldNotCreateGameDirectoryAfterDeletion), M3L.GetString(M3L.string_errorRestoringGame), MessageBoxButton.OK, MessageBoxImage.Error);
                                break;
                            case RestoreResult.ERROR_COULD_NOT_DELETE_GAME_DIRECTORY:
                                Analytics.TrackEvent(@"Restored game", new Dictionary<string, string>()
                                {
                                    {@"Game", Game.ToString()},
                                    {@"Result", @"Failure, Could not delete existing game directory"}
                                });
                                M3L.ShowDialog(window, M3L.GetString(M3L.string_dialogcouldNotFullyDeleteGameDirectory), M3L.GetString(M3L.string_errorRestoringGame), MessageBoxButton.OK, MessageBoxImage.Error);
                                break;
                            case RestoreResult.EXCEPTION_DELETING_GAME_DIRECTORY:
                                Analytics.TrackEvent(@"Restored game", new Dictionary<string, string>()
                                {
                                    {@"Game", Game.ToString()},
                                    {@"Result", @"Failure, Exception deleting existing game directory"}
                                });
                                M3L.ShowDialog(window, M3L.GetString(M3L.string_dialogErrorOccuredDeletingGameDirectory), M3L.GetString(M3L.string_errorRestoringGame), MessageBoxButton.OK, MessageBoxImage.Error);
                                break;
                            case RestoreResult.RESTORE_OK:
                                Analytics.TrackEvent(@"Restored game", new Dictionary<string, string>()
                                {
                                    {@"Game", Game.ToString()},
                                    {@"Result", @"Success"}
                                });
                                break;
                        }
                    }*/
                }

                return false;
            }
        }

        #endregion
        public const string LEGACY_REGISTRY_KEY_ME3CMM = @"Software\Mass Effect 3 Mod Manager";

        /// <summary>
        /// ALOT Addon Registry Key, used for ME1 and ME2 backups
        /// </summary>
        public const string LEGACY_BACKUP_REGISTRY_KEY = @"Software\ALOTAddon"; //Shared. Do not change

        private static void WriteBackupLocation(MEGame game, string backupPath)
        {
#if WINDOWS
            switch (game)
            {
                case MEGame.ME1:
                case MEGame.ME2:
                    RegistryHandler.WriteRegistryKey(Registry.CurrentUser, LEGACY_BACKUP_REGISTRY_KEY, game + @"VanillaBackupLocation", backupPath);
                    break;
                case MEGame.ME3:
                    RegistryHandler.WriteRegistryKey(Registry.CurrentUser, LEGACY_REGISTRY_KEY_ME3CMM, @"VanillaCopyLocation", backupPath);
                    break;
            }
#else
            Settings.SaveBackupPath(game, backupPath);
#endif
        }

        public static void UnlinkBackup(MEGame meGame)
        {
            Log.Information($"[AICORE] Unlinking backup for {meGame}");
            var gbPath = BackupService.GetGameBackupPath(meGame, out _, forceReturnPath: true);
            if (gbPath != null)
            {
                var cmmVanilla = Path.Combine(gbPath, "cmm_vanilla");
                if (File.Exists(cmmVanilla))
                {
                    Log.Information("[AICORE] Deleting cmm_vanilla file: " + cmmVanilla);
                    File.Delete(cmmVanilla);
                }
            }
            BackupService.RemoveBackupPath(meGame);
        }
    }
}