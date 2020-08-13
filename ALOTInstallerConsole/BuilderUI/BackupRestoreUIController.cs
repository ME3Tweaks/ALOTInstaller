using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ALOTInstallerConsole.UserControls;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.ModManager.ME3Tweaks;
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
                var gameBackupPath = BackupService.GetGameBackupPath(e, false, false, false);
                var gameHasListedBackupDir = BackupService.HasGameEverBeenBackedUp(e); //does game have backup path in registry?
                Add(new Label($"{e.ToGameName()}")
                {
                    X = 1,
                    Y = y++,
                    Height = 1,
                });
                Add(new Label(BackupService.GetBackupStatus(e))
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
                        Clicked = () => CreateBackup(e)
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

#if DEBUG
            Add(new Button("Reload UI")
            {
                X = Pos.Right(this) - 30,
                Y = Pos.Bottom(this) - 3,
                Height = 1,
                Clicked = () =>
                {
                    RemoveAll();
                    buildUI();
                    Application.Refresh();
                }
            });
#endif
        }

        private void CreateBackup(Enums.MEGame game)
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
                        // For picking target
                        return "";
                    },
                    BlockingActionCallback = (title, message) =>
                    {
                        MessageBox.ErrorQuery(title, message, "OK");
                    },
                    WarningActionCallback = (title, message) => MessageBox.ErrorQuery(title, message, "Yes", "No") == 0,
                    BackupProgressCallback = (progressVal, progressMax) =>
                    {
                        pd.ProgressMax = progressMax;
                        pd.ProgressValue = progressVal;
                    },
                    NotifyBackupThreadCompleted = () =>
                    {
                        // ?
                    },
                    UpdateStatusCallback = (newstatus) => pd.BottomMessage = newstatus,
                    WarningListCallback = (title, message, bottommessage, list) => ScrollDialog.Prompt(title, message, bottommessage, list, Colors.Error, "Yes", "No") == 0,
                    SelectGameBackupFolderDestination = () =>
                    {
                        string selectedPath = null;
                        //Application.MainLoop.Invoke(() =>
                        //{
                            OpenDialog selector = new OpenDialog("Select backup destination directory",
                                "Select an empty directory to copy the backup to.")
                            {
                                CanChooseDirectories = true,
                                CanChooseFiles = false,
                            };
                            Application.Run(selector);
                            if (!selector.Canceled && selector.FilePath != null && Directory.Exists(selector.FilePath.ToString()))
                            {
                                selectedPath = selector.FilePath.ToString();
                            }
                        //});

                        return selectedPath;
                    },
                    BackupSourceTarget = Locations.GetTarget(game)
                };
                backupController.BeginBackup();
            };
            nbw.RunWorkerCompleted += (a, b) =>
            {
                if (pd.IsCurrentTop)
                {
                    Application.RequestStop();
                }
                MessageBox.Query("Backup completed", $"{game.ToGameName()} has been backed up.", "OK");
                buildUI(); //Refresh the interface
            };
            nbw.RunWorkerAsync();
            Application.Run(pd);
            //}
        }

        private void RestoreBackup(Enums.MEGame game)
        {
            int response = MessageBox.ErrorQuery($"Warning: Existing game will be deleted",
                $"Restoring your game will fully delete the game located at {Locations.GetTarget(game).TargetPath}, after which your backup will be copied into its place. Your texture settings will also be reset to the normal settings.\n\nRestore {game.ToGameName()}?",
                "No, leave it alone", "Yes, restore it");
            if (response == 1)
            {
                // Perform the restore
                ProgressDialog pd = new ProgressDialog("Restore in progress",
                    $"Please wait while {game.ToGameName()} is restored from backup.", "Preparing to restore game",
                    true);
                NamedBackgroundWorker nbw = new NamedBackgroundWorker("RestoreWorker");
                nbw.DoWork += (a, b) =>
                {
                    // Restore code here
                };
                nbw.RunWorkerCompleted += (a, b) =>
                {
                    if (pd.IsCurrentTop)
                    {
                        Application.RequestStop();
                    }
                    MessageBox.Query("Restore completed", $"{game.ToGameName()} has been restored from backup.", "OK");
                    buildUI(); //Refresh the interface
                };

                Application.Run(pd);
            }
        }

        private void UnlinkBackup(Enums.MEGame meGame)
        {
            int response = MessageBox.ErrorQuery($"Warning: Removing backup of {meGame.ToGameName()}",
                           $"Unlinking a backup will make modding tools such as ALOT Installer and ME3Tweaks Mod Manager no longer attempt to find the listed backup. It will NOT delete the backup files, if they exist. You will need to create a new backup after unlinking it.\n\nUnlink the backup?",
                           "No, leave it alone", "Yes, unlink it");
            if (response == 1)
            {
                // Unlink
                BackupHandler.UnlinkBackup(meGame);
                MessageBox.Query("Backup unlinked", $"The backup for {meGame.ToGameName()} has been unlinked.", "OK");
                buildUI(); //Refresh the interface
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
