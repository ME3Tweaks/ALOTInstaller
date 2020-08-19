using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using ALOTInstallerCore;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.Objects.Manifest;
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
            var pd = await window.ShowProgressAsync("Starting up", "ALOT Installer is starting up. Please wait.");
            pd.SetIndeterminate();
            NamedBackgroundWorker bw = new NamedBackgroundWorker("StartupThread");
            bw.DoWork += (a, b) =>
            {
                ALOTInstallerCoreLib.Startup(SetWrapperLogger);
                Settings.Load();
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
                    // Todo: Have some way for app to change defaults (to MEUITM? Last mode set? ??)
                    ManifestHandler.CurrentMode = ManifestMode.ALOT;
                }

                pd.SetMessage("Preparing texture library");
                TextureLibrary.ResetAllReadyStatuses(
                    ManifestHandler.GetManifestFilesForMode(ManifestHandler.CurrentMode));

                pd.SetMessage("Preparing interface");
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (Application.Current.MainWindow is MainWindow mw)
                    {
                        mw.Title = $"ALOT Installer {Utilities.GetAppVersion()}";
                        mw.ContentGrid.Children.Add(new FileSelectionUIController());
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
    }
}