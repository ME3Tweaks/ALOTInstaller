using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AlotAddOnGUI;
using ALOTInstallerCore;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.Objects;
using ALOTInstallerCore.Objects.Manifest;
using ALOTInstallerCore.Steps;
using ALOTInstallerWPF.InstallerUI;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using Serilog;

namespace ALOTInstallerWPF.BuilderUI
{
    public class StagingUIController
    {
        private FileSelectionUIController fsuic;
        private InstallOptionsPackage iop;
        public void StartStaging(InstallOptionsPackage iop, FileSelectionUIController fsuic)
        {
            this.iop = iop;
            this.fsuic = fsuic;
            fsuic.StagingGame = iop.InstallTarget.Game;
            fsuic.IsStaging = true;
            fsuic.StagingStatusText = "Preparing to stage packages";
            NamedBackgroundWorker builderWorker = new NamedBackgroundWorker("BuilderWorker");
            bool hasStaged = false;
            StageStep ss = new StageStep(iop, builderWorker)
            {
                UpdateOverallStatusCallback = status =>
                {
                    fsuic.StagingStatusText = status;
                },
                UpdateProgressCallback = (done, total) =>
                {
                    if (hasStaged)
                    {
                        fsuic.ProgressIndeterminate = false;
                    }

                    fsuic.ProgressMax = total;
                    fsuic.ProgressValue = done;
                },
                ResolveMutualExclusiveMods = resolveMutualExclusiveMod,
                FinalizedFileSet = finalizedFileSet,
                NotifyFileBeingProcessed = notifyNewFileProcessing,
                ErrorStagingCallback = errorStaging,
                ConfigureModOptions = configureModOptions,
                PointOfNoReturnNotification = pointOfNoReturnPrompt,
                NotifyAddonBuild = () => hasStaged = true //will make next progress updates set progressbar
            };
            builderWorker.WorkerReportsProgress = true;
            builderWorker.DoWork += ss.PerformStaging;
            builderWorker.RunWorkerCompleted += async (a, b) =>
            {
                if (b.Error != null)
                {
                    if (Application.Current.MainWindow is MainWindow mw)
                    {
                        await mw.ShowMessageAsync("Error occurred while building textures",
                            $"Error occurred while building textures: {b.Error.Message}");
                    }
                }
                else if (b.Result is bool staged)
                {

                    if (staged)
                    {
                        // Install is ready to go
                        performPreinstallCheck();
                    }
                    else
                    {
                        // Installation was aborted.
                        fsuic.IsStaging = false;
                    }
                }
            };
            fsuic.ProgressIndeterminate = true;
            builderWorker.RunWorkerAsync();
        }

        private bool pointOfNoReturnPrompt()
        {
            bool userAcceptedFinality = false;
            var syncObj = new object();
            Application.Current.Invoke(async () =>
            {
                if (Application.Current.MainWindow is MainWindow mw)
                {

                    var answer = await mw.ShowMessageAsync(
                        "WARNING: YOU WILL BE UNABLE TO INSTALL ADDITIONAL FILES TO THE GAME AFTER THIS POINT",
                        $"Once textures are installed, you will be unable to safely add or change files in your game as all files will be modified.  This means mods, DLC, and other things; your game is very likely to become unusable if you do and {Utilities.GetAppPrefixedName()} Installer will refuse to work on games modded in this way.\n\nEnsure all your non-texture mods are installed before continuing, as you will not be able to install them after this point.",
                        MessageDialogStyle.AffirmativeAndNegative,
                        new MetroDialogSettings()
                        {
                            AffirmativeButtonText = "I understand, install",
                            NegativeButtonText = "Abort install",
                            DefaultButtonFocus = MessageDialogResult.Negative
                        }, 70);
                    userAcceptedFinality = answer == MessageDialogResult.Affirmative;
                    lock (syncObj)
                    {
                        Monitor.Pulse(syncObj);
                    }
                }


            });
            lock (syncObj)
            {
                Monitor.Wait(syncObj);
            }
            return userAcceptedFinality;
        }

        private void notifyNewFileProcessing(InstallerFile fileProcessing)
        {
            Application.Current.Invoke(() =>
            {
                fsuic.StagingStatusText = $"Preparing {fileProcessing.FriendlyName} for installation";
                fsuic.InstallerFilesListBox.ScrollIntoView(fileProcessing);
            });
        }

        private void finalizedFileSet(List<InstallerFile> filesBeingInstalled)
        {
            Application.Current.Invoke(() =>
            {
                fsuic.ShownSpecificFileSet = filesBeingInstalled;
                fsuic.DisplayedFilesView.Refresh();
            });
        }


        private bool configureModOptions(ManifestFile mf, List<ConfigurableMod> optionsToConfigure)
        {
            bool continueStaging = true;
            object syncObj = new object();
            Application.Current.Dispatcher.Invoke(async () =>
            {
                if (Application.Current.MainWindow is MainWindow mw)
                {
                    var configDialog = new ModConfigurationDialog(mf, ManifestHandler.CurrentMode);
                    configDialog.closeDialogWithResult = async b =>
                    {
                        continueStaging = b;
                        await mw.HideMetroDialogAsync(configDialog);
                        lock (syncObj)
                        {
                            Monitor.Pulse(syncObj);
                        }
                    };

                    await mw.ShowMetroDialogAsync(configDialog);

                }
            });
            lock (syncObj)
            {
                Monitor.Wait(syncObj);
            }
            return continueStaging;
        }

        private void errorStaging(string message)
        {
            Application.Current.Invoke(async () =>
            {
                if (Application.Current.MainWindow is MainWindow mw)
                {
                    await mw.ShowMessageAsync("Error occurred while staging textures", message);
                }
            });
        }


        private async void performPreinstallCheck()
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                var pd = await mw.ShowProgressAsync("Performing installation precheck",
                    $"Please wait while {Utilities.GetAppPrefixedName()} Installer checks for issues that will block installation.");

                NamedBackgroundWorker preinstallCheckWorker = new NamedBackgroundWorker("PrecheckWorker-Preinstall");
                preinstallCheckWorker.DoWork += (a, b) =>
                {
                    b.Result = Precheck.PerformPreInstallCheck(iop);
                };
                preinstallCheckWorker.RunWorkerCompleted += async (sender, b) =>
                {
                    await pd.CloseAsync();
                    if (b.Error != null)
                    {
                        Log.Error($"Exception occurred in precheck for pre-install: {b.Error.Message}");
                        await mw.ShowMessageAsync("Error occurred performing install precheck",
                            $"An error occurred performing the installation precheck: {b.Error.Message}");
                        fsuic.IsStaging = false;
                    }
                    else if (b.Result != null)
                    {
                        // Precheck failed
                        await mw.ShowMessageAsync("Installation precheck failed", b.Result as string);
                        fsuic.IsStaging = false;
                    }
                    else
                    {
                        // Precheck passed
                        if (iop.FilesToInstall != null)
                        {
                            // BEGIN INSTALLATION!
                            var iuic = new InstallerUIController(iop)
                            {
                                InstallerTextTop = "Preparing texture installer",
                            };
                            mw.OpenInstallerUI(iuic, InstallerUIController.GetInstallerBackgroundImage(iop.InstallTarget.Game, iop.InstallerMode));
                        }
                    }
                };
                preinstallCheckWorker.RunWorkerAsync();
            }
        }

        private InstallerFile resolveMutualExclusiveMod(List<InstallerFile> arg)
        {
            InstallerFile option = null;
            object syncObj = new object();
            Application.Current.Dispatcher.Invoke(async () =>
            {

                if (Application.Current.MainWindow is MainWindow mw)
                {
                    var options = arg.Select(x => x.ShortFriendlyName ?? x.FriendlyName).ToList();
                    options.Add("Abort install");
                    var chosenOption = await mw.ShowMessageAsync("Select which file to use",
                        "Only one of the following mods can be installed. Select which one to use.",
                        MessageDialogStyle.AffirmativeAndNegativeAndSingleAuxiliary, new MetroDialogSettings()
                        {
                            AffirmativeButtonText = options[0],
                            NegativeButtonText = options[1],
                            FirstAuxiliaryButtonText = options[2],
                            DefaultButtonFocus = MessageDialogResult.Affirmative
                        }, 75);

                    if (chosenOption == MessageDialogResult.Affirmative) option = arg[0];
                    if (chosenOption == MessageDialogResult.Negative) option = arg[1];
                    lock (syncObj)
                    {
                        Monitor.Pulse(syncObj);
                    }

                }
            });
            lock (syncObj)
            {
                Monitor.Wait(syncObj);
            }
            return option;
        }
    }
}
