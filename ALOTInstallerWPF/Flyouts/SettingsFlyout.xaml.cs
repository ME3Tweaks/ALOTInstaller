using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ALOTInstallerCore;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.ModManager.ME3Tweaks;
using ALOTInstallerCore.ModManager.Objects;
using ALOTInstallerCore.ModManager.Services;
using ALOTInstallerCore.Objects;
using ALOTInstallerWPF.Objects;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;

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

        public SettingsFlyout()
        {
            DataContext = this;
            LoadCommands();
            InitializeComponent();
        }

        private void LoadCommands()
        {
            LinkUnlinkBackupCommand = new RelayCommand(PerformLinkUnlink);
            BackupRestoreCommand = new RelayCommand(PerformBackupRestore, CanBackupRestore);
        }

        public RelayCommand LinkUnlinkBackupCommand { get; set; }

        private async void PerformLinkUnlink(object obj)
        {
            if (obj is BackupService.GameBackupStatus gbs && Application.Current.MainWindow is MainWindow mw)
            {
                var buPath = BackupService.GetGameBackupPath(gbs.Game, out _, forceReturnPath: true);
                var linked = buPath != null;
                if (linked)
                {

                    var unlinkRes = await mw.ShowMessageAsync("Warning: Unlinking backup",
                        $"Unlinking your backup for {gbs.Game.ToGameName()} will make modding programs, including ME3Explorer, ALOT Installer, and ME3Tweaks Mod Manager unable to find a backup for this game. These programs use this backup for various features. Unlinking a backup will not delete your existing backup. You can link to an existing backup once you've unlinked your existing backup.\n\nBackup Path: {buPath}\n\nUnlink your backup for {gbs.Game.ToGameName()}?",
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
                    var unlinkRes = await mw.ShowMessageAsync("Warning: Linking backup",
                        $"Unlinking your backup for {gbs.Game.ToGameName()} will make modding programs, including ME3Explorer, ALOT Installer, and ME3Tweaks Mod Manager unable to find a backup for this game. These programs use this backup for various features. Unlinking a backup will not delete your existing backup. You can link to an existing backup once you've unlinked your existing backup.\n\nBackup Path: {buPath}\n\nUnlink your backup for {gbs.Game.ToGameName()}?",
                        MessageDialogStyle.AffirmativeAndNegative,
                        new MetroDialogSettings()
                        {
                            AffirmativeButtonText = "Unlink backup",
                            NegativeButtonText = "Don't unlink backup",
                            DefaultButtonFocus = MessageDialogResult.Negative
                        });
                    if (unlinkRes == MessageDialogResult.Affirmative)
                    {
                        performBackup(gbs.Game, true, mw);
                    }
                }
            }
        }

        private async void performBackup(Enums.MEGame game, bool linkMode, MetroWindow mw)
        {

            var pd = await mw.ShowProgressAsync($"Creating backup of {game.ToGameName()}", "Please wait while your backup is created.");
            NamedBackgroundWorker nbw = new NamedBackgroundWorker("BackupWorker");
            nbw.DoWork += (a, b) =>
            {
                object syncObj = new object();

                var backupController = new BackupHandler.GameBackup(game, new[] { Locations.GetTarget(game) })
                {
                    SelectGameExecutableCallback = (_game) =>
                    {
                        // Called in link mode
                        GameTarget gt = null;
                        Application.Current.Dispatcher.Invoke(async () =>
                        {
                            OpenFileDialog ofd = new OpenFileDialog()
                            {
                                Title = "Select game executable in backup directory",
                                Filter = game.ToGameName().Replace(" ", "") + ".exe",
                                CheckFileExists = true
                            };
                            var result = ofd.ShowDialog();
                            if (result.HasValue && result.Value)
                            {
                                string invalidReason = null;
                                var path = Utilities.GetGamePathFromExe(game, ofd.FileName);
                                if (path != null)
                                {

                                    GameTarget t = new GameTarget(game, path, true, true);
                                    {
                                        var target = new GameTarget(_game, path, false, true);
                                        invalidReason = target.ValidateTarget(true);
                                        if (invalidReason == null)
                                        {

                                            // Valid target to test against
                                            gt = target;
                                            lock (syncObj)
                                            {
                                                Monitor.Pulse(syncObj);
                                            }

                                            return;
                                        }
                                    }
                                }
                                else
                                {
                                    invalidReason =
                                        "Game executable cannot possibly be located this close to root of volume";
                                }

                                lock (syncObj)
                                {
                                    Monitor.Pulse(syncObj);
                                }

                                await pd.CloseAsync();
                                await mw.ShowMessageAsync("Cannot create backup", invalidReason);
                            }

                            lock (syncObj)
                            {
                                Monitor.Wait(syncObj);
                            }
                        });

                        return gt;
                    },
                    BlockingActionCallback = (title, message) =>
                    {
                        Application.Current.Dispatcher.Invoke(async () =>
                        {
                            await mw.ShowMessageAsync(title, message);
                        });
                    },
                    WarningActionCallback = (title, message) =>
                    {
                        bool response = false;
                        Application.Current.Dispatcher.Invoke(async () =>
                        {
                            response = await mw.ShowMessageAsync(title, message,
                                MessageDialogStyle.AffirmativeAndNegative,
                                new MetroDialogSettings()
                                {
                                    AffirmativeButtonText = "Yes",
                                    NegativeButtonText = "No",
                                }) == MessageDialogResult.Affirmative;
                            lock (syncObj)
                            {
                                Monitor.Pulse(syncObj);
                            }
                        });
                        lock (syncObj)
                        {
                            Monitor.Wait(syncObj);
                        }

                        return response;
                    },
                    BackupProgressCallback = (progressVal, progressMax) =>
                    {
                        // Not sure this needs to be on UI thread
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            pd.SetProgress(progressVal * 1f / progressMax);
                        });
                    },
                    SetProgressIndeterminateCallback = (indeterminate) =>
                    {
                        if (indeterminate)
                        {
                            pd.SetIndeterminate();
                        }

                        // ?? setting progress will unset it
                    },
                    UpdateStatusCallback = newstatus => pd.SetMessage(newstatus),
                    WarningListCallback = (title, message, bottommessage, list) =>
                    {
                        bool response = false;
                        Application.Current.Dispatcher.Invoke(async () =>
                        {
                            response = await mw.ShowScrollMessageAsync(title, message, bottommessage, list,
                                MessageDialogStyle.AffirmativeAndNegative,
                                new MetroDialogSettings()
                                {
                                    AffirmativeButtonText = "Yes",
                                    NegativeButtonText = "No",
                                }) == MessageDialogResult.Affirmative;
                            lock (syncObj)
                            {
                                Monitor.Pulse(syncObj);
                            }
                        });
                        lock (syncObj)
                        {
                            Monitor.Wait(syncObj);
                        }

                        return response;

                    },
                    SelectGameBackupFolderDestination = () =>
                    {
                        string selectedPath = null;
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            // Not sure if this has to be synced
                            CommonOpenFileDialog ofd = new CommonOpenFileDialog()
                            {
                                Title = "Select backup destination directory",
                                IsFolderPicker = true,
                                EnsurePathExists = true
                            };
                            if (ofd.ShowDialog() == CommonFileDialogResult.Ok)
                            {
                                selectedPath = ofd.FileName;
                            }
                        });
                        return selectedPath;
                    },
                    BackupSourceTarget = linkMode
                        ? new GameTarget(game, "Link to existing backup", false, true)
                        : Locations.GetTarget(game)
                };
                b.Result = backupController.PerformBackup();
            };
            nbw.RunWorkerCompleted += async (a, b) =>
            {
                if (pd.IsOpen)
                {
                    await pd.CloseAsync();
                }

                if (b.Error == null)
                {
                    if (b.Result is bool x && x)
                    {
                        BackupService.UpdateBackupStatus(game, false);
                        await mw.ShowMessageAsync("Backup completed", $"{game.ToGameName()} has been backed up.");
                    }
                }
            };
            nbw.RunWorkerAsync();
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
                    if (Locations.GetTarget(gbs.Game) != null)
                    {
                        // Can restore


                        performRestore(gbs.Game, mw);

                    }
                }
            }
        }

        private async void performRestore(Enums.MEGame game, MainWindow mw)
        {
            var result = await mw.ShowMessageAsync("Select restore type",
                $"Restore over the existing copy of {game.ToGameName()}, or make a new copy of the game using your backup?",
                MessageDialogStyle.AffirmativeAndNegativeAndSingleAuxiliary,
                new MetroDialogSettings()
                {
                    AffirmativeButtonText = "Restore existing game",
                    NegativeButtonText = "Make a copy",
                    FirstAuxiliaryButtonText = "Cancel",
                    DefaultButtonFocus = MessageDialogResult.Affirmative
                });

            if (result == MessageDialogResult.FirstAuxiliary) return; //Cancel

            string destinationPath = Locations.GetTarget(game)?.TargetPath;

            if (result == MessageDialogResult.FirstAuxiliary)
            {
                //    var overwriteExistingGameConf = await mw.ShowMessageAsync($"Warning: Restoring {game.ToGameName()}",
                //        $"Restoring from backup will completely delete your game installation (your save files will remain intact), copy your backup into its place, and reset your texture settings to the defaults.\n\nGame path: {Locations.GetTarget(game).TargetPath}",
                //        MessageDialogStyle.AffirmativeAndNegative,
                //        new MetroDialogSettings()
                //        {
                //            AffirmativeButtonText = "Restore game",
                //            NegativeButtonText = "Don't restore game",
                //            DefaultButtonFocus = MessageDialogResult.Affirmative
                //        });
                //    if (overwriteExistingGameConf == MessageDialogResult.Negative)
                //        return;
                //}
                //else
                //{
                destinationPath = null; //Force backend to prompt
            }



            // Perform the restore
            var pd = await mw.ShowProgressAsync($"Restoring {game.ToGameName()}",
                $"Please wait while {game.ToGameName()} is restored from backup.");
            NamedBackgroundWorker nbw = new NamedBackgroundWorker("RestoreWorker");
            nbw.DoWork += (a, b) =>
            {
                var syncObj = new object();
                BackupHandler.GameRestore gr = new BackupHandler.GameRestore(game)
                {
                    ConfirmationCallback = (title, message) =>
                    {
                        bool response = false;
                        Application.Current.Dispatcher.Invoke(async () =>
                        {
                            response = await mw.ShowMessageAsync(title, message,
                                MessageDialogStyle.AffirmativeAndNegative,
                                new MetroDialogSettings()
                                {
                                    AffirmativeButtonText = "OK",
                                    NegativeButtonText = "Cancel",
                                }) == MessageDialogResult.Affirmative;
                            lock (syncObj)
                            {
                                Monitor.Pulse(syncObj);
                            }
                        });
                        lock (syncObj)
                        {
                            Monitor.Wait(syncObj);
                        }

                        return response;
                    },
                    BlockingErrorCallback = (title, message) =>
                    {
                        Application.Current.Dispatcher.Invoke(async () =>
                        {
                            await mw.ShowMessageAsync(title, message);
                        });
                    },
                    RestoreErrorCallback = (title, message) =>
                    {
                        Application.Current.Dispatcher.Invoke(async () =>
                        {
                            await mw.ShowMessageAsync(title, message);
                        });
                    },
                    UpdateStatusCallback = message =>
                        Application.Current.Dispatcher.Invoke(() => pd.SetMessage(message)),
                    UpdateProgressCallback = (done, total) =>
                        Application.Current.Dispatcher.Invoke(() => pd.SetProgress(done * 1d / total)),
                    SetProgressIndeterminateCallback = indeterminate => Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (indeterminate) pd.SetIndeterminate();
                    }),
                    SelectDestinationDirectoryCallback = (title, message) =>
                    {
                        string selectedPath = null;
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            // Not sure if this has to be synced
                            CommonOpenFileDialog ofd = new CommonOpenFileDialog()
                            {
                                Title = "Select restore destination directory",
                                IsFolderPicker = true,
                                EnsurePathExists = true
                            };
                            if (ofd.ShowDialog() == CommonFileDialogResult.Ok)
                            {
                                selectedPath = ofd.FileName;
                            }
                        });
                        return selectedPath;
                    }
                };
                b.Result = gr.PerformRestore(destinationPath);
                // Restore code here
            };
            nbw.RunWorkerCompleted += async (a, b) =>
            {
                UpdateGameStatuses();
                if (pd.IsOpen) await pd.CloseAsync();
                if (b.Error == null && b.Result is bool x && x)
                {
                    string restoreMessage = $"{game.ToGameName()} has been restored from backup.";
                    if (destinationPath == null)
                    {
                        restoreMessage = $"A clone of {game.ToGameName()} has been created from backup.";
                    }

                    await mw.ShowMessageAsync("Restore completed", restoreMessage);
                }
            };
            nbw.RunWorkerAsync();
        }

        public RelayCommand BackupRestoreCommand { get; set; }

        private void Expander_Expanded(object sender, RoutedEventArgs e)
        {
            ((Expander)sender).BringIntoView();
        }

        private bool isDecidingBetaMode;


        public event PropertyChangedEventHandler PropertyChanged;

        public void UpdateGameStatuses()
        {
            var me1Target = Locations.ME1Target;
            if (me1Target?.GetInstalledALOTInfo() == null)
            {
                ME1TextureInstallInfo = "ME1: No textures installed";
            }
            else
            {
                ME1TextureInstallInfo = $"ME1: {me1Target.GetInstalledALOTInfo().ToString()}";
            }

            var me2Target = Locations.ME2Target;
            if (me2Target?.GetInstalledALOTInfo() == null)
            {
                ME2TextureInstallInfo = "ME2: No textures installed";
            }
            else
            {
                ME2TextureInstallInfo = $"ME2: {me2Target.GetInstalledALOTInfo().ToString()}";
            }

            var me3Target = Locations.ME3Target;
            if (me3Target?.GetInstalledALOTInfo() == null)
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
        }

        private async void BetaMode_Toggled(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                if (!isDecidingBetaMode && Settings.BetaMode)
                {
                    isDecidingBetaMode = true;
                    var result = await mw.ShowMessageAsync("Switching to beta mode", "Beta mode of ALOT Installer will restart the application and cause the following things to occur:\n - Updates to the application become mandatory\n - You will receive updates that are not yet approved for users in Stable mode\n - You will always download the latest version of MassEffectModderNoGui\n - You will use the beta version of the manifest, which may differ from the Stable one\n - You are expected to report feedback to the developers in the ALOT Discord if things don't work as expected\n - You are OK with a less stable experience\n\nSwitch to Beta mode?",
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
                    else
                    {
                        Settings.Save();
                        // Todo: Restart application
                    }
                    isDecidingBetaMode = false;
                }
            }
        }
    }
}
