using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using ALOTInstallerConsole.UserControls;
using ALOTInstallerCore;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.ModManager.ME3Tweaks;
using ALOTInstallerCore.ModManager.Objects;
using ALOTInstallerCore.ModManager.Services;
using ALOTInstallerCore.Objects;
using Terminal.Gui;

namespace ALOTInstallerConsole.BuilderUI
{
    public class BackupRestoreUIController : UIController
    {

        public override void SetupUI()
        {
            Title = "Backup & Restore";
            BackupService.RefreshBackupStatus(Locations.GetAllAvailableTargets(), false);
            buildUI();
        }

        private void buildUI()
        {
            int y = 1;

            foreach (var e in Enums.AllGames)
            {
                var gameBackupPath = BackupService.GetGameBackupPath(e, out _, false, false, false);
                var gameHasListedBackupDir = BackupService.HasGameEverBeenBackedUp(e); //does game have backup path in registry?
                Add(new Label($"{e.ToGameName()}")
                {
                    X = 1,
                    Y = y++,
                    Height = 1,
                });
                Add(new Label(BackupService.GetBackupStatus(e).BackupStatus)
                {
                    X = 1,
                    Y = y++,
                    Height = 1
                });

                Add(new TextField()
                {
                    ReadOnly = true,
                    Width = 80,
                    Height = 1,
                    X = 1,
                    Y = y,
                    Text = gameBackupPath ?? BackupService.GetBackupStatusTooltip(e)
                });
                y++;

                if (gameHasListedBackupDir)
                {
                    Add(new Button("Unlink backup")
                    {
                        Height = 1,
                        X = 1,
                        Y = y,
                        Clicked = () => UnlinkBackup(e)
                    });
                }
                else
                {
                    Add(new Button("Link to existing backup")
                    {
                        Height = 1,
                        X = 1,
                        Y = y,
                        Clicked = () => CreateBackup(e, true)
                    });
                }

                if (gameBackupPath != null)
                {
                    // Has backup
                    Add(new Button("Restore")
                    {
                        Height = 1,
                        X = 70,
                        Y = y,
                        Clicked = () => RestoreBackup(e)
                    });
                }
                else if (Locations.GetTarget(e) != null)
                {
                    // No backup but game is installed
                    Add(new Button("Create backup")
                    {
                        Height = 1,
                        X = 64,
                        Y = y,
                        Clicked = () => CreateBackup(e, false)
                    });
                }
                else
                {
                    // Game not found, no backup
                    Add(new Label("Game not installed")
                    {
                        X = 68,
                        Y = y
                    });
                }

                y++;
                y++;
            }


            Button close = new Button("Close")
            {
                X = Pos.Right(this) - 12,
                Y = Pos.Bottom(this) - 3,
                Height = 1,
                Clicked = Close_Clicked
            };
            Add(close);
        }

        //private void LinkBackup(Enums.MEGame game)
        //{
        //    ProgressDialog pd = new ProgressDialog("Backup in progress",
        //                    $"Please wait while {game.ToGameName()} is backed up.", "Preparing to back up game",
        //                    true);

        //    NamedBackgroundWorker nbw = new NamedBackgroundWorker("BackupWorker");
        //    nbw.DoWork += (a, b) =>
        //    {
        //        var backupController = new BackupHandler.GameBackup(game, new[] { Locations.GetTarget(game) })
        //        {

        //            BlockingActionCallback = (title, message) =>
        //            {
        //                MessageBox.ErrorQuery(title, message, "OK");
        //            },
        //            WarningActionCallback = (title, message) => MessageBox.ErrorQuery(title, message, "Yes", "No") == 0,
        //            BackupProgressCallback = (progressVal, progressMax) =>
        //            {
        //                pd.ProgressMax = progressMax;
        //                pd.ProgressValue = progressVal;
        //            },
        //            NotifyBackupThreadCompleted = () =>
        //            {
        //                // ?
        //            },
        //            UpdateStatusCallback = (newstatus) => pd.BottomMessage = newstatus,
        //            WarningListCallback = (title, message, bottommessage, list) => ScrollDialog.Prompt(title, message, bottommessage, list, Colors.Error, "Yes", "No") == 0,
        //            SelectGameExecutableCallback = (_game) =>
        //            {
        //                OpenDialog selector = new OpenDialog($"Select {_game.ToGameName().Replace(" ", "")}.exe", $"Select the executable for your backup of {_game.ToGameName()}.")
        //                {
        //                    CanChooseDirectories = false,
        //                    AllowedFileTypes = new[] { ".exe" },
        //                };
        //                Application.Run(selector);
        //                if (!selector.Canceled && selector.FilePaths.Any() && File.Exists(selector.FilePaths.First()))
        //                {
        //                    var target = new GameTarget(_game, selector.FilePaths.First(), false, true);
        //                    var invalidReason = target.ValidateTarget();
        //                    if (invalidReason == null)
        //                    {
        //                        return target;
        //                    }
        //                    MessageBox.ErrorQuery("Invalid target selected", invalidReason, "OK");
        //                }

        //                return null;
        //            },
        //            BackupSourceTarget = Locations.GetTarget(game)
        //        };
        //        backupController.PerformBackup();
        //    };
        //    nbw.RunWorkerCompleted += (a, b) =>
        //    {
        //        if (pd.IsCurrentTop)
        //        {
        //            Application.RequestStop();
        //        }
        //        MessageBox.Query("Backup linked", $"{game.ToGameName()} now has a linked backup.", "OK");
        //        buildUI(); //Refresh the interface
        //    };
        //    nbw.RunWorkerAsync();
        //    Application.Run(pd);
        //    //}
        //}


        private void CreateBackup(Enums.MEGame game, bool linkMode)
        {

            //int response = MessageBox.ErrorQuery($"Warning: Existing game will be deleted",
            //    $"Restoring your game will fully delete the game located at {Locations.GetTarget(game).TargetPath}, after which your backup will be copied into its place. Your texture settings will also be reset to the normal settings.\n\nRestore {game.ToGameName()}?",
            //    "No, leave it alone", "Yes, restore it");
            //if (response == 1)
            //{
            // Create new backup
            ProgressDialog pd = new ProgressDialog("Backup in progress",
                $"Please wait while {game.ToGameName()} is backed up.", "Preparing to back up game",
                true);

            NamedBackgroundWorker nbw = new NamedBackgroundWorker("BackupWorker");
            nbw.DoWork += (a, b) =>
            {
                var backupController = new BackupHandler.GameBackup(game, new[] { Locations.GetTarget(game) })
                {
                    SelectGameExecutableCallback = (_game) =>
                    {
                        object o = new object();

                        GameTarget gt = null;
                        Application.MainLoop.Invoke(() =>
                        {
                            OpenDialog selector = new OpenDialog($"Select {_game.ToGameName().Replace(" ", "")}.exe",
                                $"Select the executable for your backup of {_game.ToGameName()}.")
                            {
                                CanChooseDirectories = false,
                                CanChooseFiles = true,
                                AllowedFileTypes = new[] { $"{_game.ToGameName().Replace(" ", "")}.exe" },
                            };
                            Application.Run(selector);
                            if (!selector.Canceled && selector.FilePath != null &&
                                File.Exists(selector.FilePath.ToString()))
                            {
                                var path = Utilities.GetGamePathFromExe(_game, selector.FilePath.ToString());
                                string invalidReason = null;
                                if (path != null)
                                {
                                    var target = new GameTarget(_game, path, false, true);
                                    invalidReason = target.ValidateTarget(true);
                                    if (invalidReason == null)
                                    {
                                        gt = target;
                                        lock (o)
                                        {
                                            Monitor.Pulse(o);
                                        }
                                        return;
                                    }
                                }
                                else
                                {
                                    invalidReason =
                                        "Game executable cannot possibly be located this close to root of volume";
                                }

                                MessageBox.ErrorQuery("Invalid target selected", invalidReason, "OK");

                            }
                            lock (o)
                            {
                                Monitor.Pulse(o);
                            }
                        });
                        lock (o)
                        {
                            Monitor.Wait(o);
                        }
                        return gt;
                    },
                    BlockingActionCallback = (title, message) =>
                    {
                        MessageBox.ErrorQuery(title, message, "OK");
                    },
                    WarningActionCallback = (title, message, affirmativetext, negativetext) => MessageBox.ErrorQuery(title, message, affirmativetext, negativetext) == 0,
                    BackupProgressCallback = (progressVal, progressMax) =>
                    {
                        pd.ProgressMax = progressMax;
                        pd.ProgressValue = progressVal;
                    },
                    SetProgressIndeterminateCallback = (indeterminate) =>
                    {
                        pd.Indeterminate = indeterminate;
                    },
                    UpdateStatusCallback = (newstatus) => pd.BottomMessage = newstatus,
                    WarningListCallback = (title, message, bottommessage, list, affirmativetext, negativetext) => ScrollDialog.Prompt(title, message, bottommessage, list, Colors.Error, affirmativetext, negativetext) == 0,
                    SelectGameBackupFolderDestination = () =>
                    {
                        string selectedPath = null;
                        Application.MainLoop.Invoke(() =>
                        {
                            OpenDialog selector = new OpenDialog("Select backup destination directory",
                                "Select an empty directory to copy the backup to.")
                            {
                                CanChooseDirectories = true,
                                CanChooseFiles = false,
                            };
                            Application.Run(selector);
                            if (!selector.Canceled && selector.FilePath != null &&
                                Directory.Exists(selector.FilePath.ToString()))
                            {
                                selectedPath = selector.FilePath.ToString();
                            }
                        });

                        return selectedPath;
                    },
                    BackupSourceTarget = linkMode ? new GameTarget(game, "Link to existing backup", false, true) : Locations.GetTarget(game)
                };
                b.Result = backupController.PerformBackup();
            };
            nbw.RunWorkerCompleted += (a, b) =>
            {
                if (pd.IsCurrentTop)
                {
                    Application.RequestStop();
                }

                if (b.Error == null)
                {
                    if (b.Result is bool x && x)
                    {
                        MessageBox.Query("Backup completed", $"{game.ToGameName()} has been backed up.", "OK");
                        Program.SwapToNewView(
                            new BackupRestoreUIController()); //just reload whole interface. This works around oddness in gui.cs RemoveAll();
                    }
                }
            };
            nbw.RunWorkerAsync();
            Application.Run(pd);
            //}
        }

        private void RestoreBackup(Enums.MEGame game)
        {
            string destinationPath = Locations.GetTarget(game)?.TargetPath;
            int result = MessageBox.Query("Select restore type", "Restore over the existing game, or make a copy of the game using your backup?", "Restore existing game", "Make a copy", "Cancel");

            if (result == 2) return; //Cancel

            // Perform the restore
            ProgressDialog pd = new ProgressDialog("Restore in progress",
                $"Please wait while {game.ToGameName()} is restored from backup.", "Preparing to restore game",
                true);
            NamedBackgroundWorker nbw = new NamedBackgroundWorker("RestoreWorker");
            nbw.DoWork += (a, b) =>
            {
                BackupHandler.GameRestore gr = new BackupHandler.GameRestore(game)
                {
                    ConfirmationCallback = (title, message) => MessageBox.ErrorQuery(title, message, "OK", "Cancel") == 0,
                    BlockingErrorCallback = (title, message) => MessageBox.ErrorQuery(title, message, "OK"),
                    RestoreErrorCallback = (title, message) => MessageBox.ErrorQuery(title, message, "OK"),
                    UpdateStatusCallback = message => pd.BottomMessage = message,
                    UpdateProgressCallback = (done, total) =>
                    {
                        pd.ProgressMax = total;
                        pd.ProgressValue = done;
                    },
                    SetProgressIndeterminateCallback = indeterminate => pd.Indeterminate = indeterminate,
                    SelectDestinationDirectoryCallback = (title, message) =>
                    {
                        object o = new object();
                        string path = null;
                        Application.MainLoop.Invoke(() =>
                        {
                            OpenDialog selector = new OpenDialog(title, message)
                            {
                                CanChooseDirectories = true,
                                CanChooseFiles = false
                            };
                            Application.Run(selector);
                            if (!selector.Canceled && selector.FilePath != null && Directory.Exists(selector.FilePath.ToString()))
                            {
                                path = selector.FilePath.ToString();
                                lock (o)
                                {
                                    Monitor.Pulse(o);
                                }
                            }
                        });
                        lock (o)
                        {
                            Monitor.Wait(o);
                        }

                        return path;
                    }
                };
                b.Result = gr.PerformRestore(result == 0 ? destinationPath : null);


                // Restore code here
            };
            nbw.RunWorkerCompleted += (a, b) =>
            {
                if (pd.IsCurrentTop)
                {
                    Application.RequestStop();
                }
                MessageBox.Query("Restore completed", $"{game.ToGameName()} has been restored from backup.", "OK");
                Program.SwapToNewView(new BackupRestoreUIController());
            };
            nbw.RunWorkerAsync();
            Application.Run(pd);

        }

        private void UnlinkBackup(Enums.MEGame meGame)
        {
            int response = MessageBox.ErrorQuery($"Warning: Removing backup of {meGame.ToGameName()}",
                           $"Unlinking a backup will make modding tools such as {Utilities.GetAppPrefixedName()} Installer and ME3Tweaks Mod Manager no longer attempt to find the listed backup. It will NOT delete the backup files, if they exist. You will need to create a new backup after unlinking it.\n\nUnlink the backup?",
                           "No, leave it alone", "Yes, unlink it");
            if (response == 1)
            {
                // Unlink
                BackupHandler.UnlinkBackup(meGame);
                MessageBox.Query("Backup unlinked", $"The backup for {meGame.ToGameName()} has been unlinked.", "OK");
                Program.SwapToNewView(new BackupRestoreUIController());
            }
        }

        private void Close_Clicked()
        {
            FileSelectionUIController bui = new FileSelectionUIController();
            Program.SwapToNewView(bui);
        }

        public override void BeginFlow()
        {
        }

        public override void SignalStopping()
        {
        }
    }
}
