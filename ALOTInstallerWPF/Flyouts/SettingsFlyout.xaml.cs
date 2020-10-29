using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using ALOTInstallerCore;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.Helpers.AppSettings;
using ALOTInstallerCore.ModManager.ME3Tweaks;
using ALOTInstallerCore.ModManager.Objects;
using ALOTInstallerCore.ModManager.Services;
using ALOTInstallerCore.Objects;
using ALOTInstallerCore.Objects.Manifest;
using ALOTInstallerWPF.Controllers;
using ALOTInstallerWPF.InstallerUI;
using ALOTInstallerWPF.Objects;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using Serilog;
using Path = System.IO.Path;

namespace ALOTInstallerWPF.Flyouts
{
    /// <summary>
    /// Interaction logic for SettingsFlyout.xaml
    /// </summary>
    public partial class SettingsFlyout : UserControl, INotifyPropertyChanged
    {
        public bool ME1Available => Locations.GetTarget(Enums.MEGame.ME1) != null;
        public bool ME2Available => Locations.GetTarget(Enums.MEGame.ME2) != null;
        public bool ME3Available => Locations.GetTarget(Enums.MEGame.ME3) != null;
        public string ME1TextureInstallInfo { get; private set; }
        public string ME2TextureInstallInfo { get; private set; }
        public string ME3TextureInstallInfo { get; private set; }
        public bool ShowGameMissingText { get; set; }
        public bool AnyGameInstalled { get; set; }

        public ObservableCollectionExtended<ManifestTutorial> AllTutorials { get; } =
            new ObservableCollectionExtended<ManifestTutorial>();


        public SettingsFlyout()
        {
            DataContext = this;
            LoadCommands();
            if (ManifestHandler.MasterManifest != null)
            {
                AllTutorials.ReplaceAll(ManifestHandler.MasterManifest.Tutorials);
            }

            InitializeComponent();

        }


        public GenericCommand SetBuildLocationCommand { get; set; }
        public GenericCommand SetLibraryLocationCommand { get; set; }
        public RelayCommand LinkUnlinkBackupCommand { get; set; }
        public RelayCommand CheckGameIsVanillaCommand { get; set; }
        public RelayCommand BackupRestoreCommand { get; set; }
        public GenericCommand OpenALOTDiscordCommand { get; set; }
        public GenericCommand CleanupLibraryCommand { get; set; }
        public GenericCommand CleanupBuildLocationCommand { get; set; }
        public GenericCommand DebugShowInstallerFlyoutCommand { get; set; }
        public GenericCommand DebugShowOriginFlyoutCommand { get; set; }
        public GenericCommand LogsDiagnosticsCommand { get; set; }
        public GenericCommand OpenLODSwitcherCommand { get; set; }
        public RelayCommand OpenTutorialLinkCommand { get; set; }
        public GenericCommand LaunchMEMGuiCommand { get; set; }
        private void LoadCommands()
        {
            SetLibraryLocationCommand = new GenericCommand(ChangeLibraryLocation);
            SetBuildLocationCommand = new GenericCommand(ChangeBuildLocation);
            LinkUnlinkBackupCommand = new RelayCommand(PerformLinkUnlink);
            BackupRestoreCommand = new RelayCommand(PerformBackupRestore, CanBackupRestore);
            CheckGameIsVanillaCommand = new RelayCommand(CheckVanilla, CanCheckVanilla);
            OpenALOTDiscordCommand = new GenericCommand(OpenAlotDiscord);
            CleanupLibraryCommand = new GenericCommand(CleanupLibrary);
            CleanupBuildLocationCommand = new GenericCommand(CleanupBuildLocation);
            LogsDiagnosticsCommand = new GenericCommand(OpenDiagnosticsFlyout);
            OpenLODSwitcherCommand = new GenericCommand(OpenLODSwitcher, () => Locations.GetAllAvailableTargets().Any());
            OpenTutorialLinkCommand = new RelayCommand(OpenTutorialLink);
            LaunchMEMGuiCommand = new GenericCommand(LaunchMEM);
#if DEBUG
            DebugShowInstallerFlyoutCommand = new GenericCommand(() =>
            {
                var game = Enums.MEGame.ME3;
                InstallerUIController iuic = new InstallerUIController(new InstallOptionsPackage()
                {
                    DebugNoInstall = true,
                    InstallALOT = true,
                    InstallTarget = Locations.GetTarget(game)
                })
                {
                    InstallerTextTop = "Installing X TOP TEXT",
                    InstallerTextMiddle = "This is middle text",
                    InstallerTextBottom = "Bottom Text 15%",

                };
                if (Application.Current.MainWindow is MainWindow mw)
                {
                    mw.OpenInstallerUI(iuic,
                        InstallerUIController.GetInstallerBackgroundImage(game, ManifestHandler.CurrentMode), true);
                }
            });
            DebugShowOriginFlyoutCommand = new GenericCommand(() =>
            {
                if (Application.Current.MainWindow is MainWindow mw)
                {
                    mw.OpenOriginFlyout(Enums.MEGame.ME1);
                }
            });
#endif
        }

        private void LaunchMEM()
        {
            MEMGUILaunchController.LaunchMEMGUI();
        }

        private void OpenTutorialLink(object obj)
        {
            if (obj is string str)
            {
                Utilities.OpenWebPage(str);
            }
        }

        private void OpenLODSwitcher()
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                mw.LODSwitcherOpen = true;
                mw.SettingsOpen = false;
            }
        }

        private void OpenDiagnosticsFlyout()
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                mw.SettingsOpen = false;
                mw.DiagnosticsOpen = true;
            }
        }

        private async void CleanupBuildLocation()
        {
            // Todo: Attempt reimport before cleanup.
            if (Application.Current.MainWindow is MainWindow mw)
            {
                if (!Directory.Exists(Settings.BuildLocation))
                {
                    await mw.ShowMessageAsync("Error cleaning build directory", "The build directory does not exist.");
                    return;
                }

                NamedBackgroundWorker nbw = new NamedBackgroundWorker("CleanupBuildLocWorker");
                var pd = await mw.ShowProgressAsync("Importing leftover files from build directory",
                    "Checking if any files in build directory need to be re-imported to library...");
                nbw.DoWork += (sender, args) =>
                {
                    var bdSize = Utilities.GetSizeOfDirectory(Settings.BuildLocation);
                    foreach (var file in Directory.GetFiles(Settings.BuildLocation, "*.*", SearchOption.AllDirectories))
                    {
                        TextureLibrary.AttemptImportManifestFile(file,
                            ManifestHandler.GetAllManifestFiles(),
                            (x, y) =>
                            {
                                if (x)
                                {
                                    Log.Information($"Reimported {y} to texture library");
                                }
                            },
                            (file, done, total) =>
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    pd.SetMessage($"Reimporting {file} to library");
                                    pd.Maximum = total;
                                    pd.SetProgress(done);
                                });
                            });
                    }

                    foreach (var d in Directory.GetDirectories(Settings.BuildLocation))
                    {
                        Utilities.DeleteFilesAndFoldersRecursively(d);
                    }

                    Application.Current.Dispatcher.Invoke(async () =>
                    {
                        await pd.CloseAsync();
                        await mw.ShowMessageAsync("Build directory cleaned",
                            $"The build directory has been cleaned up. {FileSizeFormatter.FormatSize(bdSize)} of data was deleted.");
                    });
                };
                nbw.RunWorkerAsync();
            }
        }

        private async void CleanupLibrary()
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                var unusedFilesInLib = TextureLibrary.GetUnusedFilesInLibrary();
                if (unusedFilesInLib.Any())
                {
                    string message =
                        "The following files located in the texture library are no longer used, or were moved into the texture library manually (and are not used), and can be safely deleted:\n";
                    List<string> sizedItems = new List<string>();
                    foreach (var v in unusedFilesInLib)
                    {
                        sizedItems.Add(
                            $"{v} ({FileSizeFormatter.FormatSize(new FileInfo(Path.Combine(Settings.TextureLibraryLocation, v)).Length)})");
                    }

                    var result = await mw.ShowScrollMessageAsync("Irrelevant files found in texture library", message,
                        "Delete these files?",
                        sizedItems, MessageDialogStyle.AffirmativeAndNegative,
                        new MetroDialogSettings()
                        {
                            AffirmativeButtonText = "Delete unused files",
                            NegativeButtonText = "Keep unused files",
                            DefaultButtonFocus = MessageDialogResult.Affirmative
                        });
                    if (result == MessageDialogResult.Affirmative)
                    {
                        // Delete em'
                        foreach (var v in unusedFilesInLib)
                        {
                            var fullPath = Path.Combine(Settings.TextureLibraryLocation, v);
                            Log.Information($"Deleting unused file in texture library: {fullPath}");
                            try
                            {
                                File.Delete(fullPath);
                            }
                            catch (Exception e)
                            {
                                Log.Error($"Error deleting file: {e.Message}");
                            }
                        }

                        // Todo: Show status?
                    }
                }
                else
                {
                    await mw.ShowMessageAsync("Texture Library is clean",
                        "No unused files were found in the texture library.");
                }
            }
        }

        private async void OpenAlotDiscord()
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                await mw.ShowMessageAsync("Joining ALOT Discord",
                    "If you're joining the ALOT Discord for assistance, please generate an installer log (if the application is having issues) and/or game diagnostic (if a game is having issues) and include the link to it with your message, as the developers and support community will always ask for one.");
                Utilities.OpenWebPage(ALOTCommunity.DiscordInviteLink);
            }
        }


        private async void CheckVanilla(object obj)
        {
            if (obj is string gameStr && Enum.TryParse<Enums.MEGame>(gameStr, out var game) &&
                Application.Current.MainWindow is MainWindow mw)
            {
                var target = Locations.GetTarget(game);
                if (target.GetInstalledALOTInfo() != null)
                {
                    await mw.ShowMessageAsync("Game is texture modded",
                        "Unable to check the vanilla status of a game that has been texture modded, as all files will have been modified.");
                    return;
                }

                var pd = await mw.ShowProgressAsync($"Verifying {game.ToGameName()}",
                    "Please wait while your game is verified", true);
                CancellationTokenSource cts = new CancellationTokenSource();
                pd.Canceled += (sender, args) => { cts.Cancel(); };
                List<string> nonVanillaFiles = new List<string>();
                NamedBackgroundWorker nbw = new NamedBackgroundWorker("VerifyVanillaWorker");
                nbw.DoWork += (a, b) => // MM CODE
                    VanillaDatabaseService.ValidateTargetAgainstVanilla(target, f => { nonVanillaFiles.Add(f); },
                        su => { Application.Current.Dispatcher.Invoke(() => { pd.SetMessage(su); }); },
                        (done, total) =>
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                pd.Maximum = total;
                                pd.SetProgress(done);
                            });
                        }
                        , true, cts.Token);
                nbw.RunWorkerCompleted += async (a, b) =>
                {
                    if (pd.IsOpen)
                    {
                        await pd.CloseAsync();
                    }

                    if (!cts.IsCancellationRequested)
                    {
                        if (nonVanillaFiles.Any())
                        {
                            await mw.ShowScrollMessageAsync($"{game.ToGameName()} has modifications",
                                "The following files appear to have been modified:",
                                "There may be additional files also added to the game that this tool does not check for.",
                                nonVanillaFiles);
                        }
                        else
                        {
                            await mw.ShowMessageAsync($"{game.ToGameName()} appears vanilla",
                                "This installation of the game does not appear to have any modified files.");
                        }
                    }
                };
                nbw.RunWorkerAsync();
            }
        }

        private bool CanCheckVanilla(object obj)
        {
            if (obj is string gameStr && Enum.TryParse<Enums.MEGame>(gameStr, out var game))
            {
                return Locations.GetTarget(game) != null;
            }

            return false;
        }

        private void ChangeBuildLocation()
        {
            CommonOpenFileDialog selector = new CommonOpenFileDialog()
            {
                Title = "Select location to build textures installation packages",
                IsFolderPicker = true,
                InitialDirectory = Settings.BuildLocation
            };
            if (selector.ShowDialog() == CommonFileDialogResult.Ok)
            {
                Settings.BuildLocation = selector.FileName;
                Settings.Save();
            }
        }

        private void ChangeLibraryLocation()
        {
            CommonOpenFileDialog selector = new CommonOpenFileDialog()
            {
                Title = "Select location to store library",
                IsFolderPicker = true,
                InitialDirectory = Settings.TextureLibraryLocation
            };
            if (selector.ShowDialog() == CommonFileDialogResult.Ok)
            {
                Settings.TextureLibraryLocation = selector.FileName;
                TextureLibrary.ResetAllReadyStatuses(ManifestHandler.GetAllManifestFiles().OfType<InstallerFile>()
                    .ToList());
                Settings.Save();
            }
        }

        private async void PerformLinkUnlink(object obj)
        {
            if (obj is BackupService.GameBackupStatus gbs && Application.Current.MainWindow is MainWindow mw)
            {
                var buPath = BackupService.GetGameBackupPath(gbs.Game, out _, forceReturnPath: true);
                var linked = buPath != null;
                if (linked)
                {

                    var unlinkRes = await mw.ShowMessageAsync("Warning: Unlinking backup",
                        $"Unlinking your backup for {gbs.Game.ToGameName()} will make modding programs, including ME3Explorer, {Utilities.GetAppPrefixedName()} Installer, and ME3Tweaks Mod Manager unable to find a backup for this game. These programs use this backup for various features. Unlinking a backup will not delete your existing backup. You can link to an existing backup once you've unlinked your existing backup.\n\nBackup Path: {buPath}\n\nUnlink your backup for {gbs.Game.ToGameName()}?",
                        MessageDialogStyle.AffirmativeAndNegative,
                        new MetroDialogSettings()
                        {
                            AffirmativeButtonText = "Unlink backup",
                            NegativeButtonText = "Don't unlink backup",
                            DefaultButtonFocus = MessageDialogResult.Negative
                        });
                    if (unlinkRes == MessageDialogResult.Affirmative)
                    {
                        BackupHandler.UnlinkBackup(gbs.Game);
                        BackupService.UpdateBackupStatus(gbs.Game, false);
                        await mw.ShowMessageAsync("Backup unlinked",
                            $"The backup for {gbs.Game.ToGameName()} has been unlinked. Modding programs will no longer think there is a game backup for this game.");
                    }
                }
                else
                {
                    // Link to an existing backup
                    //var unlinkRes = await mw.ShowMessageAsync("Warning: Linking backup",
                    //    $"Link an existing backup for {gbs.Game.ToGameName()} to make modding programs, including ME3Explorer, ALOT Installer, and ME3Tweaks Mod Manager, recognize your backup. Linking a backup requires the backup to be unmodified from a vanilla version of the game.\n\nDO NOT SELECT YOUR MAIN GAME INSTALL, once designated as a backup, modding programs will refuse to modify it.",
                    //    MessageDialogStyle.AffirmativeAndNegative,
                    //    new MetroDialogSettings()
                    //    {
                    //        AffirmativeButtonText = "Link an existing backup",
                    //        NegativeButtonText = "Don't link backup",
                    //        DefaultButtonFocus = MessageDialogResult.Affirmative
                    //    });
                    //if (unlinkRes == MessageDialogResult.Affirmative)
                    //{
                    performBackup(gbs.Game, true, mw);
                    //}
                }
            }
        }

        private async void performBackup(Enums.MEGame game, bool linkMode, MetroWindow mw)
        {
            BackupController.PerformBackup(game, linkMode, mw);

        }

        private bool CanBackupRestore(object obj)
        {
            if (obj is BackupService.GameBackupStatus gbs)
            {
                var backedUp = BackupService.GetGameBackupPath(gbs.Game, out _, false) != null;
                if (backedUp) return true;
                return Locations.GetTarget(gbs.Game) != null; //Game is installed. Can backup target
            }

            return false;
        }

        private async void PerformBackupRestore(object obj)
        {
            if (obj is BackupService.GameBackupStatus gbs && Application.Current.MainWindow is MainWindow mw)
            {
                var buPath = BackupService.GetGameBackupPath(gbs.Game, out _, forceCmmVanilla: false);
                var backupExists = buPath != null;
                if (!backupExists)
                {
                    if (Locations.GetTarget(gbs.Game) != null)
                    {
                        // Can backup
                        performBackup(gbs.Game, false, mw);
                    }
                }
                else
                {
                    performRestore(gbs.Game, Locations.GetTarget(gbs.Game) != null, mw);
                }
            }
        }

        private async void performRestore(Enums.MEGame game, bool hasTarget, MainWindow mw)
        {
            RestoreController.PerformRestore(game, hasTarget, mw, UpdateGameStatuses);
        }


        private void Expander_Expanded(object sender, RoutedEventArgs e)
        {
            ((Expander)sender).BringIntoView();
        }

        private bool isDecidingToggleSetting;


        public event PropertyChangedEventHandler PropertyChanged;

        private bool hasOpenedOnce = false;

        public void UpdateGameStatuses()
        {
            hasOpenedOnce = true;
            bool anyMissingInstall = false;
            var me1Target = Locations.ME1Target;
            if (me1Target == null)
            {
                ME1TextureInstallInfo = "ME1: Not installed";
                anyMissingInstall = true;
            }
            else if (me1Target.GetInstalledALOTInfo() == null)
            {
                ME1TextureInstallInfo = "ME1: No textures installed";
            }
            else
            {
                ME1TextureInstallInfo = $"ME1: {me1Target.GetInstalledALOTInfo().ToString()}";
            }

            var me2Target = Locations.ME2Target;
            if (me2Target == null)
            {
                ME2TextureInstallInfo = "ME2: Not installed";
                anyMissingInstall = true;
            }
            else if (me2Target.GetInstalledALOTInfo() == null)
            {
                ME2TextureInstallInfo = "ME2: No textures installed";
            }
            else
            {
                ME2TextureInstallInfo = $"ME2: {me2Target.GetInstalledALOTInfo().ToString()}";
            }

            var me3Target = Locations.ME3Target;
            if (me3Target == null)
            {
                ME3TextureInstallInfo = "ME3: Not installed";
                anyMissingInstall = true;
            }
            else if (me3Target.GetInstalledALOTInfo() == null)
            {
                ME3TextureInstallInfo = "ME3: No textures installed";
            }
            else
            {
                ME3TextureInstallInfo = $"ME3: {me3Target.GetInstalledALOTInfo().ToString()}";
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ME1Available)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ME2Available)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ME3Available)));
            ShowGameMissingText = anyMissingInstall;
            AnyGameInstalled = me1Target != null || me2Target != null || me3Target != null;
        }

        private async void BetaMode_Toggled(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                if (!isDecidingToggleSetting && hasOpenedOnce && Settings.BetaMode)
                {
                    isDecidingToggleSetting = true;
                    var result = await mw.ShowMessageAsync("Switching to beta mode",
                        $"Beta mode of {Utilities.GetAppPrefixedName()} Installer will restart the application and cause the following things to occur:\n - Updates to the application become mandatory\n - You will receive updates that are not yet approved for users in Stable mode\n - You will always download the latest version of MassEffectModderNoGui\n - You will use the beta version of the manifest, which may differ from the Stable one\n - You are expected to report feedback to the developers in the ALOT Discord if things don't work as expected\n - You are OK with a less stable experience\n\nSwitch to Beta mode?",
                        MessageDialogStyle.AffirmativeAndNegative, new MetroDialogSettings()
                        {
                            AffirmativeButtonText = "Switch to Beta",
                            NegativeButtonText = "Remain on Stable",
                            DefaultButtonFocus = MessageDialogResult.Affirmative
                        });
                    if (result == MessageDialogResult.Negative)
                    {
                        Settings.BetaMode = false; //Turn back off
                    }
                    isDecidingToggleSetting = false;
                }
            }
        }

        private async void Telemetry_Toggled(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                if (!isDecidingToggleSetting && hasOpenedOnce && !Settings.Telemetry)
                {
                    isDecidingToggleSetting = true;
                    var result = await mw.ShowMessageAsync("Disabling telemetry",
                        $"The developers of {Utilities.GetAppPrefixedName()} Installer use telemetry data to improve this product. This includes information such as install time, application crash reports, operating systems of users, file importing status, and other data points that help guide us to develop features that users will use or to improve different areas of the installer. Personally identifying information is not collected.\n\nTurn off application telemetry?",
                        MessageDialogStyle.AffirmativeAndNegative, new MetroDialogSettings()
                        {
                            AffirmativeButtonText = "Turn off telemetry",
                            NegativeButtonText = "Leave telemetry on",
                            DefaultButtonFocus = MessageDialogResult.Negative
                        });
                    if (result == MessageDialogResult.Negative)
                    {
                        Settings.Telemetry = true; //turn back on
                    }
                    isDecidingToggleSetting = false;
                }
            }
        }
    }
}
