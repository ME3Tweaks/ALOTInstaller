using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ALOTInstallerConsole.UserControls;
using ALOTInstallerCore;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.ModManager.Services;
using ALOTInstallerCore.Objects.Manifest;
using ME3ExplorerCore;
using NickStrupat;
using Terminal.Gui;

namespace ALOTInstallerConsole.BuilderUI
{
    public class StartupUIController : UIController
    {
        #region UI

        private Label startupStatusLabel;
        private Action loggerSetupFunc;

        #endregion
        public override void BeginFlow()
        {
            BackgroundWorker bw = new BackgroundWorker();
            bw.DoWork += (a, b) =>
            {
                Application.MainLoop.Invoke(() =>
                {
                    startupStatusLabel.Text = "Starting up";
                });

                var alotManifestModePackage = ManifestHandler.LoadMasterManifest((x) => Application.MainLoop.Invoke(() =>
                {
                    startupStatusLabel.Text = x;
                }));

                void downloadProgressChanged(long bytes, long total)
                {
                    //Log.Information("Download: "+bytes);
                    Application.MainLoop.Invoke(() =>
                    {
                        startupStatusLabel.Text = $"Updating MassEffectModderNoGui {bytes * 100 / total}%";
                    });
                }
                Application.MainLoop.Invoke(() =>
                {
                    startupStatusLabel.Text = "Checking for MassEffectModderNoGui updates";
                });
                MEMUpdater.UpdateMEM(downloadProgressChanged);

                ALOTInstallerCoreLib.PostCriticalStartup(x =>
                    {
                        Application.MainLoop.Invoke(() =>
                        {
                            startupStatusLabel.Text = x;
                        });
                    },
                    x => x()
                    );

                b.Result = alotManifestModePackage;
            };
            bw.RunWorkerCompleted += (a, b) =>
            {
                if (b.Error == null)
                {
                    FileSelectionUIController bui = new FileSelectionUIController();
                    if (ManifestHandler.MasterManifest != null)
                    {
                        ManifestHandler.SetCurrentMode(ManifestMode.ALOT);
                    }
                    Program.SwapToNewView(bui);
                }
                else
                {
                    startupStatusLabel.Text = "Error preparing application: " + b.Error.Message;
                }
            };
            bw.RunWorkerAsync();
        }

        public override void SignalStopping()
        {

        }

        public override void SetupUI()
        {
            //var top = Application.Top;

            startupStatusLabel = new Label()
            {
                Text = "Starting up",
                TextAlignment = TextAlignment.Centered,
                X = Pos.Center(),
                Y = Pos.Center() + 1,
                Height = 1,
                Width = Dim.Fill()
            };

            Add(new Label()
            {
                Text = $"{Utilities.GetAppPrefixedName()} Installer {Utilities.GetAppVersion()}",
                TextAlignment = TextAlignment.Centered,
                X = Pos.Center(),
                Y = Pos.Center() - 3,
                Height = 1,
                Width = Dim.Fill()
            },
            startupStatusLabel);
            //top.Add(this);
        }

        public void SetLoggerSetupFunc(Action setupLogger)
        {
            loggerSetupFunc = setupLogger;
        }
    }
}
