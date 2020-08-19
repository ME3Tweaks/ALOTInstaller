using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using ALOTInstallerCore;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.Objects.Manifest;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;

namespace ALOTInstallerWPF.Controllers
{
    public static class StartupController
    {
        public static async void BeginFlow(MetroWindow window)
        {
            var pd = await window.ShowProgressAsync("Starting up", "ALOT Installer is starting up. Please wait.");
            NamedBackgroundWorker bw = new NamedBackgroundWorker("StartupThread");
            bw.DoWork += (a, b) =>
            {
                var alotManifestModePackage = ManifestHandler.LoadMasterManifest(x => pd.SetMessage(x));
                void downloadProgressChanged(long bytes, long total)
                {
                    //Log.Information("Download: "+bytes);
                    pd.SetMessage($"Updating MassEffectModderNoGui {bytes * 100 / total}%");
                }
                pd.SetMessage("Checking for MassEffectModderNoGui updates");
                MEMUpdater.UpdateMEM(downloadProgressChanged);
                b.Result = alotManifestModePackage;
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
