using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.ModManager.Objects;
using ALOTInstallerCore.Objects;
using ALOTInstallerCore.PlatformSpecific.Windows;
using Serilog;


namespace ALOTInstallerCore.ModManager.Services
{
    /// <summary>
    /// Contains methods and bindable variables for accessing and displaying info about game backups 
    /// </summary>
    public static class BackupService
    {
        #region Static Property Changed

        public static event PropertyChangedEventHandler StaticPropertyChanged;
        public static event PropertyChangedEventHandler StaticBackupStateChanged;

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
            return true;
        }
        #endregion

        //private static bool _me1Installed;
        //public static bool ME1Installed
        //{
        //    get => _me1Installed;
        //    private set => SetProperty(ref _me1Installed, value);
        //}

        //private static bool _me2Installed;
        //public static bool ME2Installed
        //{
        //    get => _me2Installed;
        //    private set => SetProperty(ref _me2Installed, value);
        //}

        //private static bool _me3Installed;
        //public static bool ME3Installed
        //{
        //    get => _me3Installed;
        //    private set => SetProperty(ref _me3Installed, value);
        //}

        public static ObservableCollectionExtended<GameBackupStatus> GameBackupStatuses { get; } = new ObservableCollectionExtended<GameBackupStatus>()
        {

        };

        public class GameBackupStatus : INotifyPropertyChanged
        {
            public string GameName => Game.ToGameName();
            public Enums.MEGame Game { get; internal set; }
            public bool BackedUp { get; internal set; }
            public bool BackupActivity { get; internal set; }
            public string BackupStatus { get; internal set; }
            public string BackupLocationStatus { get; internal set; }
            public string LinkActionText { get; internal set; }
            public string BackupActionText { get; internal set; }

            internal GameBackupStatus(Enums.MEGame game)
            {
                Game = game;
            }

            internal void RefreshBackupStatus(bool installed, bool forceCmmVanilla)
            {

                var bPath = GetGameBackupPath(Game, out var isVanilla, forceCmmVanilla);
                if (bPath != null)
                {
                    if (!isVanilla)
                    {
                        BackupStatus = "Backed up (Not Vanilla)";
                    }
                    else
                    {
                        BackupStatus = "Backed up";
                    }
                    BackupLocationStatus = $"Backup stored at {bPath}";
                    LinkActionText = "Unlink backup";
                    BackupActionText = "Restore game";
                    return;
                }
                bPath = GetGameBackupPath(Game, out _, forceCmmVanilla, forceReturnPath: true);
                if (bPath == null)
                {
                    BackupStatus = "Not backed up";
                    BackupLocationStatus = "Game has not been backed up";
                    LinkActionText = "Link existing backup";
                    BackupActionText = "Create backup"; //This should be disabled if game is not installed. This will be handled by the wrapper
                    return;
                }
                else if (!Directory.Exists(bPath))
                {
                    BackupStatus = "Backup unavailable";
                    BackupLocationStatus = $"Backup path not accessible: {bPath}";
                    LinkActionText = "Unlink backup";
                    BackupActionText = "Create new backup";
                    return;
                }

                if (!installed)
                {
                    BackupStatus = "Game not installed";
                    BackupLocationStatus = "Game not installed. Run at least once to ensure game is fully setup";
                    LinkActionText = "Link existing backup"; //this seems dangerous to the average user
                    BackupActionText = "Can't create backup";
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
        }

        /// <summary>
        /// Initializes the backup service.
        /// </summary>
        public static void InitBackupService(Action<Action> runCodeOnUIThreadCallback)
        {
            void runOnUiThread()
            {
                GameBackupStatuses.Add(new GameBackupStatus(Enums.MEGame.ME1));
                GameBackupStatuses.Add(new GameBackupStatus(Enums.MEGame.ME2));
                GameBackupStatuses.Add(new GameBackupStatus(Enums.MEGame.ME3));
            }
            runCodeOnUIThreadCallback.Invoke(runOnUiThread);
            RefreshBackupStatus(Locations.GetAllAvailableTargets(), false);
        }

        //private static bool _me1BackedUp;
        //public static bool ME1BackedUp
        //{
        //    get => GetGameBackupPath(Enums.MEGame.ME1, out var isVanilla, true) != null;
        //    private set => SetProperty(ref _me1BackedUp, value);
        //}

        //private static bool _me2BackedUp;
        //public static bool ME2BackedUp
        //{
        //    get => GetGameBackupPath(Enums.MEGame.ME2, out var isVanilla, true) != null;
        //    private set => SetProperty(ref _me2BackedUp, value);
        //}

        //private static bool _me3BackedUp;
        //public static bool ME3BackedUp
        //{
        //    get => GetGameBackupPath(Enums.MEGame.ME3, out var isVanilla, true) != null;
        //    private set => SetProperty(ref _me3BackedUp, value);
        //}

        //private static bool _me1BackupActivity;
        //public static bool ME1BackupActivity
        //{
        //    get => _me1BackupActivity;
        //    private set => SetProperty(ref _me1BackupActivity, value);
        //}

        //private static bool _me2BackupActivity;
        //public static bool ME2BackupActivity
        //{
        //    get => _me2BackupActivity;
        //    private set => SetProperty(ref _me2BackupActivity, value);
        //}

        //private static bool _me3BackupActivity;
        //public static bool ME3BackupActivity
        //{
        //    get => _me3BackupActivity;
        //    private set => SetProperty(ref _me3BackupActivity, value);
        //}

#if WPF
        private static FontAwesomeIcon _me1ActivityIcon = FontAwesomeIcon.TimesCircle;
        public static FontAwesomeIcon ME1ActivityIcon
        {
            get => _me1ActivityIcon;
            private set => SetProperty(ref _me1ActivityIcon, value);
        }

        private static FontAwesomeIcon _me2ActivityIcon = FontAwesomeIcon.TimesCircle;
        public static FontAwesomeIcon ME2ActivityIcon
        {
            get => _me2ActivityIcon;
            private set => SetProperty(ref _me2ActivityIcon, value);
        }

        private static FontAwesomeIcon _me3ActivityIcon = FontAwesomeIcon.TimesCircle;
        public static FontAwesomeIcon ME3ActivityIcon
        {
            get => _me3ActivityIcon;
            private set => SetProperty(ref _me3ActivityIcon, value);
        }
#endif

        //public static bool AnyGameMissingBackup => (!ME1BackedUp && Locations.ME1Target != null) || (!ME2BackedUp && Locations.ME2Target != null) || (!ME3BackedUp && Locations.ME3Target != null);

        public static GameBackupStatus GetBackupStatus(Enums.MEGame game)
        {
            return GameBackupStatuses.FirstOrDefault(x => x.Game == game);
        }

        /// <summary>
        /// Refreshes the backup status of the listed game, or all if none is specified.
        /// </summary>
        /// <param name="allTargets">List of targets to determine if the game is installed or not. Passing null will assume the game is installed</param>
        /// <param name="forceCmmVanilla">If the backups will be forced to have the cmmVanilla file to be considered valid</param>
        /// <param name="game">What game to refresh. Set to unknown to refresh all.</param>
        public static void RefreshBackupStatus(List<GameTarget> allTargets, bool forceCmmVanilla = true, Enums.MEGame game = Enums.MEGame.Unknown)
        {
            foreach (var v in GameBackupStatuses)
            {
                if (v.Game == game || game == Enums.MEGame.Unknown)
                {
                    v.RefreshBackupStatus(allTargets == null || allTargets.Any(x => x.Game == Enums.MEGame.ME1), forceCmmVanilla);
                }
            }
        }

        //private static string _me1BackupStatus;
        //public static string ME1BackupStatus
        //{
        //    get => _me1BackupStatus;
        //    private set => SetProperty(ref _me1BackupStatus, value);
        //}

        //private static string _me2BackupStatus;
        //public static string ME2BackupStatus
        //{
        //    get => _me2BackupStatus;
        //    private set => SetProperty(ref _me2BackupStatus, value);
        //}

        //private static string _me3BackupStatus;
        //public static string ME3BackupStatus
        //{
        //    get => _me3BackupStatus;
        //    private set => SetProperty(ref _me3BackupStatus, value);
        //}

        //private static string _me1BackupStatusTooltip;
        //public static string ME1BackupStatusTooltip
        //{
        //    get => _me1BackupStatusTooltip;
        //    private set => SetProperty(ref _me1BackupStatusTooltip, value);
        //}

        //private static string _me2BackupStatusTooltip;
        //public static string ME2BackupStatusTooltip
        //{
        //    get => _me2BackupStatusTooltip;
        //    private set => SetProperty(ref _me2BackupStatusTooltip, value);
        //}

        //private static string _me3BackupStatusTooltip;
        //public static string ME3BackupStatusTooltip
        //{
        //    get => _me3BackupStatusTooltip;
        //    private set => SetProperty(ref _me3BackupStatusTooltip, value);
        //}

        /// <summary>
        /// Fetches the backup status string for the specific game. The status must be refreshed before the values will be initially set
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        //public static string GetBackupStatus(Enums.MEGame game)
        //{
        //    switch (game)
        //    {
        //        case Enums.MEGame.ME1: return ME1BackupStatus;
        //        case Enums.MEGame.ME2: return ME2BackupStatus;
        //        case Enums.MEGame.ME3: return ME3BackupStatus;
        //    }

        //    return null;
        //}

        /// <summary>
        /// Fetches the backup status tooltip string for the specific game. The status must be refreshed before the values will be initially set
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        public static string GetBackupStatusTooltip(Enums.MEGame game)
        {
            //TODO FIX ALOT INSTALL CONSOLE

            //switch (game)
            //{
            //    case Enums.MEGame.ME1: return ME1BackupStatusTooltip;
            //    case Enums.MEGame.ME2: return ME2BackupStatusTooltip;
            //    case Enums.MEGame.ME3: return ME3BackupStatusTooltip;
            //}

            return null;
        }

        /// <summary>
        /// Sets the status of a backup.
        /// </summary>
        /// <param name="game"></param>
        /// <param name="checkingBackup"></param>
        /// <param name="pleaseWait"></param>
        public static void SetStatus(Enums.MEGame game, string status, string tooltip)
        {
            //switch (game)
            //{
            //    case Enums.MEGame.ME1:
            //        ME1BackupStatus = status;
            //        ME1BackupStatusTooltip = tooltip;
            //        break;
            //    case Enums.MEGame.ME2:
            //        ME2BackupStatus = status;
            //        ME2BackupStatusTooltip = tooltip;
            //        break;
            //    case Enums.MEGame.ME3:
            //        ME3BackupStatus = status;
            //        ME3BackupStatusTooltip = tooltip;
            //        break;
            //}
        }

        //public static void SetActivity(Enums.MEGame game, bool p1)
        //{
        //    switch (game)
        //    {
        //        case Enums.MEGame.ME1:
        //            ME1BackupActivity = p1;
        //            break;
        //        case Enums.MEGame.ME2:
        //            ME2BackupActivity = p1;
        //            break;
        //        case Enums.MEGame.ME3:
        //            ME3BackupActivity = p1;
        //            break;
        //    }
        //}

#if WPF
        public static void SetIcon(Enums.MEGame game, FontAwesomeIcon p1)
        {
            switch (game)
            {
                case Enums.MEGame.ME1:
                    ME1ActivityIcon = p1;
                    break;
                case Enums.MEGame.ME2:
                    ME2ActivityIcon = p1;
                    break;
                case Enums.MEGame.ME3:
                    ME3ActivityIcon = p1;
                    break;
            }
        }
#endif

        public static string GetGameBackupPath(Enums.MEGame game, out bool isVanilla, bool forceCmmVanilla = true, bool logReturnedPath = false, bool forceReturnPath = false)
        {
#if WINDOWS
            string path;
            switch (game)
            {
                case Enums.MEGame.ME1:
                    path = RegistryHandler.GetRegistryString(@"HKEY_CURRENT_USER\Software\ALOTAddon", @"ME1VanillaBackupLocation");
                    break;
                case Enums.MEGame.ME2:
                    path = RegistryHandler.GetRegistryString(@"HKEY_CURRENT_USER\Software\ALOTAddon", @"ME2VanillaBackupLocation");
                    break;
                case Enums.MEGame.ME3:
                    //Check for backup via registry - Use Mod Manager's game backup key to find backup.
                    path = RegistryHandler.GetRegistryString(@"HKEY_CURRENT_USER\Software\Mass Effect 3 Mod Manager", @"VanillaCopyLocation");
                    break;
                default:
                    isVanilla = false;
                    return null;
            }

#else
            // Fetch via INI
            string path;
            switch (game)
            {
                case Enums.MEGame.ME1:
                    path = Utilities.GetRegistrySettingString(App.BACKUP_REGISTRY_KEY, @"ME1VanillaBackupLocation");
                    break;
                case Enums.MEGame.ME2:
                    path = Utilities.GetRegistrySettingString(App.BACKUP_REGISTRY_KEY, @"ME2VanillaBackupLocation");
                    break;
                case Enums.MEGame.ME3:
                    //Check for backup via registry - Use Mod Manager's game backup key to find backup.
                    path = Utilities.GetRegistrySettingString(App.REGISTRY_KEY_ME3CMM, @"VanillaCopyLocation");
                    break;
                default:
                    return null;
            }
#endif

            if (forceReturnPath)
            {
                isVanilla = true; //Just say it's vanilla
                return path; // do not check it
            }

            if (logReturnedPath)
            {
                Log.Information($@" >> Backup path lookup for {game} returned: {path}");
            }

            if (path == null || !Directory.Exists(path))
            {
                if (logReturnedPath)
                {
                    Log.Information(@" >> Path is null or directory doesn't exist.");
                }

                isVanilla = false;
                return null;
            }

            //Super basic validation
            if (!Directory.Exists(Path.Combine(path, @"BIOGame")) || !Directory.Exists(Path.Combine(path, @"Binaries")))
            {
                if (logReturnedPath)
                {
                    Log.Warning(@" >> " + path + @" is missing biogame/binaries subdirectory, invalid backup");
                }

                isVanilla = false;
                return null;
            }

            isVanilla = File.Exists(Path.Combine(path, @"cmm_vanilla"));

            if (forceCmmVanilla && !isVanilla)
            {
                if (logReturnedPath)
                {
                    Log.Warning(@" >> " + path + @" is not marked as a vanilla backup.");
                }

                return null;
            }

            if (logReturnedPath)
            {
                Log.Information(@" >> " + path + @" is considered a valid backup path");
            }

            return path;
        }

        //public static void SetBackedUp(Enums.MEGame game, bool b)
        //{
        //    switch (game)
        //    {
        //        case Enums.MEGame.ME1:
        //            ME1BackedUp = b;
        //            break;
        //        case Enums.MEGame.ME2:
        //            ME2BackedUp = b;
        //            break;
        //        case Enums.MEGame.ME3:
        //            ME3BackedUp = b;
        //            break;
        //    }
        //    StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs(nameof(AnyGameMissingBackup)));
        //    StaticBackupStateChanged?.Invoke(null, null);
        //}

        //public static void SetInstallStatuses(ObservableCollectionExtended<GameTarget> installationTargets)
        //{
        //    ME1Installed = installationTargets.Any(x => x.Game == Enums.MEGame.ME1);
        //    ME2Installed = installationTargets.Any(x => x.Game == Enums.MEGame.ME2);
        //    ME3Installed = installationTargets.Any(x => x.Game == Enums.MEGame.ME3);
        //    StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs(nameof(AnyGameMissingBackup)));
        //    StaticBackupStateChanged?.Invoke(null, null);
        //}
        public static bool HasGameEverBeenBackedUp(Enums.MEGame game)
        {
            switch (game)
            {
                case Enums.MEGame.ME1:
                    return RegistryHandler.GetRegistryString(@"HKEY_CURRENT_USER\Software\ALOTAddon",
                        @"ME1VanillaBackupLocation") != null;
                case Enums.MEGame.ME2:
                    return RegistryHandler.GetRegistryString(@"HKEY_CURRENT_USER\Software\ALOTAddon",
                        @"ME2VanillaBackupLocation") != null;
                    break;
                case Enums.MEGame.ME3:
                    //Check for backup via registry - Use Mod Manager's game backup key to find backup.
                    return RegistryHandler.GetRegistryString(
                        @"HKEY_CURRENT_USER\Software\Mass Effect 3 Mod Manager", @"VanillaCopyLocation") != null;
                default:
                    return false;
            }
        }

        public static void UpdateBackupStatus(Enums.MEGame game, bool forceCmmVanilla)
        {
            GameBackupStatuses.FirstOrDefault(x => x.Game == game)?.RefreshBackupStatus(true, forceCmmVanilla);
        }
    }
}
