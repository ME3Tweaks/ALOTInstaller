using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Shell;
using ALOTInstallerCore;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.ModManager.ME3Tweaks;
using ALOTInstallerCore.ModManager.Objects;
using ALOTInstallerCore.ModManager.Services;
using ALOTInstallerCore.Objects;
using ALOTInstallerWPF.Helpers;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Packages;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using Microsoft.WindowsAPICodePack.Taskbar;

namespace ALOTInstallerWPF.Controllers
{
    class BackupController
    {
        public static async void PerformBackup(MEGame game, bool linkMode, MetroWindow mw)
        {
            var pd = await mw.ShowProgressAsync($"{(linkMode ? "Linking" : "Creating")} backup of {game.ToGameName()}",
                "Checking game...");
            pd.SetIndeterminate();
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
                                Filter = $"{game.ToGameName()} executable|{game.ToGameName().Replace(" ", "")}.exe",
                                CheckFileExists = true
                            };
                            var result = ofd.ShowDialog();
                            if (result.HasValue && result.Value)
                            {
                                string invalidReason = null;
                                var path = Utilities.GetGamePathFromExe(game, ofd.FileName);
                                if (path != null)
                                {
                                    var target = new GameTarget(_game, path, false, true);
                                    invalidReason = target.ValidateTarget(true);
                                    if (invalidReason == null)
                                    {
                                        // Valid target to test against
                                        gt = target;
                                        return;
                                    }
                                }
                                else
                                {
                                    invalidReason =
                                        "Game executable cannot possibly be located this close to root of volume";
                                }

                                await pd.CloseAsync();
                                await mw.ShowMessageAsync("Cannot create backup", invalidReason);
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
                    WarningActionCallback = (title, message, affirmativetext, negativetext) =>
                    {
                        bool response = false;
                        Application.Current.Dispatcher.Invoke(async () =>
                        {
                            response = await mw.ShowMessageAsync(title, message,
                                MessageDialogStyle.AffirmativeAndNegative,
                                new MetroDialogSettings()
                                {
                                    AffirmativeButtonText = affirmativetext,
                                    NegativeButtonText = negativetext,
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
                            if (progressMax != 0)
                            {
                                TaskbarHelper.SetProgressState(TaskbarProgressBarState.Normal);
                                TaskbarHelper.SetProgress(progressVal * 1.0 / progressMax);
                            }
                        });
                    },
                    SetProgressIndeterminateCallback = (indeterminate) =>
                    {
                        if (indeterminate)
                        {
                            pd.SetIndeterminate();
                        }
                        TaskbarHelper.SetProgressState(indeterminate ? TaskbarProgressBarState.Indeterminate : TaskbarProgressBarState.Normal);
                        // ?? setting progress will unset it
                    },
                    UpdateStatusCallback = newstatus => pd.SetMessage(newstatus),
                    WarningListCallback = (title, message, bottommessage, list, affirmativetext, negativetext) =>
                    {
                        bool response = false;
                        Application.Current.Dispatcher.Invoke(async () =>
                        {
                            response = await mw.ShowScrollMessageAsync(title, message, bottommessage, list,
                                MessageDialogStyle.AffirmativeAndNegative,
                                new MetroDialogSettings()
                                {
                                    AffirmativeButtonText = affirmativetext,
                                    NegativeButtonText = negativetext,
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
                TaskbarHelper.SetProgressState(TaskbarProgressBarState.NoProgress);
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
    }
}
