using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using ALOTInstallerConsole.UserControls;
using ALOTInstallerCore;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.Objects.Manifest;
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
            //List<string> items = new List<string>();
            //for (int i = 0; i < 50; i++)
            //{
            //    items.Add("TEST I AM A LINE SHOW ME PLS LINE " + i);
            //}


            //ScrollDialog.Prompt("Scroll dialog", "This is top message", "This is bottom message", items, Colors.Menu, "OK");

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

                // Load the ready state while we are still in background thread
                //Application.MainLoop.Invoke(() =>
                //{
                //    startupStatusLabel.Text = "Checking texture library";
                //});

                //TextureLibrary.ResetAllReadyStatuses(Program.CurrentManifestModePackage.ManifestFiles);

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

                b.Result = alotManifestModePackage;
            };
            bw.RunWorkerCompleted += (a, b) =>
            {
                if (b.Error == null)
                {
                    FileSelectionUIController bui = new FileSelectionUIController();
                    if (ManifestHandler.MasterManifest != null)
                    {
                        ManifestHandler.CurrentMode = ManifestMode.ALOT;
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
                Text = $"ALOT Installer {Utilities.GetAppVersion()}",
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
