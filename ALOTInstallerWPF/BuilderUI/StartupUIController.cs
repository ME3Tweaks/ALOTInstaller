using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Windows;
using ALOTInstallerCore;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.ModManager.Services;
using ALOTInstallerCore.Objects.Manifest;
using ALOTInstallerWPF.Flyouts;
using ALOTInstallerWPF.Objects;
using ControlzEx.Theming;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using Serilog;

namespace ALOTInstallerWPF.BuilderUI
{
    public class StartupUIController
    {
        private static void SetWrapperLogger(ILogger logger) => Log.Logger = logger;

        public static async void BeginFlow(MetroWindow window)
        {
            var pd = await window.ShowProgressAsync("Starting up", $"{Utilities.GetAppPrefixedName()} Installer is starting up. Please wait.");
            pd.SetIndeterminate();
            NamedBackgroundWorker bw = new NamedBackgroundWorker("StartupThread");
            bw.DoWork += (a, b) =>
            {
                ALOTInstallerCoreLib.Startup(SetWrapperLogger, RunOnUIThread);
                Settings.Load();
                if (Settings.BetaMode)
                {
                    RunOnUIThread(() =>
                    {
                        ThemeManager.Current.ChangeTheme(App.Current, "Dark.Red");
                    });
                }
                BackupService.RefreshBackupStatus(Locations.GetAllAvailableTargets(), false);

                pd.SetMessage("Loading installer manifests");
                var alotManifestModePackage = ManifestHandler.LoadMasterManifest(x => pd.SetMessage(x));

                void downloadProgressChanged(long bytes, long total)
                {
                    //Log.Information("Download: "+bytes);
                    pd.SetMessage($"Updating MassEffectModderNoGui {bytes * 100 / total}%");
                    pd.SetProgress(bytes * 1.0d / total);
                }

                void errorUpdating(Exception e)
                {
                    // ?? What do we do here.
                }

                void setStatus(string message)
                {
                    pd.SetIndeterminate();
                    pd.SetMessage(message);
                }

                pd.SetMessage("Checking for MassEffectModderNoGui updates");
                MEMUpdater.UpdateMEM(downloadProgressChanged, errorUpdating, setStatus);
                b.Result = alotManifestModePackage;

                if (ManifestHandler.MasterManifest != null)
                {
                    ManifestHandler.SetCurrentMode(ManifestHandler.GetDefaultMode());
                    pd.SetMessage("Preparing texture library");
                    foreach (var v in ManifestHandler.MasterManifest.ManifestModePackageMappping)
                    {
                        TextureLibrary.ResetAllReadyStatuses(ManifestHandler.GetManifestFilesForMode(v.Key));
                    }
                }


                pd.SetMessage("Preparing interface");
                Thread.Sleep(500); // This will allow this message to show up for moment so user can see it.
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (Application.Current.MainWindow is MainWindow mw)
                    {
                        mw.Title = $"{Utilities.GetAppPrefixedName()} Installer {Utilities.GetAppVersion()}";
                        mw.ContentGrid.Children.Add(new FileSelectionUIController());
                        mw.SettingsFlyoutControl.Content = mw.SettingsFlyoutContent = new SettingsFlyout();
                        mw.DiagnosticsFlyoutControl.Content = new DiagnosticsFlyout();
                        mw.FileImporterFlyoutContent = new FileImporterFlyout();
                        mw.LODSwitcherFlyout.Content = mw.LODSwitcherFlyoutContent = new LODSwitcherFlyout();
                    }
                });
            };
            bw.RunWorkerCompleted += (a, b) =>
                {
                    pd.CloseAsync();
                    //if (b.Error == null)
                    //{
                    //    FileSelectionUIController bui = new FileSelectionUIController();
                    //    if (ManifestHandler.MasterManifest != null)
                    //    {
                    //        ManifestHandler.CurrentMode = ManifestMode.ALOT;
                    //    }
                    //    Program.SwapToNewView(bui);
                    //}
                    //else
                    //{
                    //    startupStatusLabel.Text = "Error preparing application: " + b.Error.Message;
                    //}
                };
            bw.RunWorkerAsync();


            return;
        }

        private static void RunOnUIThread(Action obj)
        {
            Application.Current.Dispatcher.Invoke(obj);
        }
    }
}