using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Shell;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.ModManager.ME3Tweaks;
using ALOTInstallerCore.Objects;
using ALOTInstallerWPF.Helpers;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Packages;
using MahApps.Metro.Controls.Dialogs;
using Microsoft.WindowsAPICodePack.Dialogs;
using Microsoft.WindowsAPICodePack.Taskbar;

namespace ALOTInstallerWPF.Controllers
{
    class RestoreController
    {
        public static async void PerformRestore(MEGame game, bool hasTarget, MainWindow mw, Action updateGameStatuses)
        {
            string destinationPath = Locations.GetTarget(game)?.TargetPath;

            if (hasTarget)
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


                if (result == MessageDialogResult.Negative)
                {
                    destinationPath = null; //Force backend to prompt
                }
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
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            pd.SetProgress(done * 1d / total);
                            if (total != 0)
                            {
                                TaskbarHelper.SetProgressState(TaskbarProgressBarState.Normal);
                                TaskbarHelper.SetProgress(done * 1.0 / total);
                            }
                        }),
                    SetProgressIndeterminateCallback = indeterminate => Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (indeterminate) pd.SetIndeterminate();
                        TaskbarHelper.SetProgressState(indeterminate ? TaskbarProgressBarState.Indeterminate : TaskbarProgressBarState.Normal);
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
                TaskbarHelper.SetProgressState(TaskbarProgressBarState.NoProgress);

                updateGameStatuses?.Invoke();
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
    }
}
