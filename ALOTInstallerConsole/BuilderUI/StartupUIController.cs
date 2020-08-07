﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using ALOTInstallerCore;
using ALOTInstallerCore.Startup;
using Serilog;
using Terminal.Gui;

namespace ALOTInstallerConsole.BuilderUI
{
    public class StartupUIController : UIController
    {
        #region UI

        private Label startupStatusLabel;

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
                //Initialize ALOT Installer library
                Hook.Startup();

                var manifestFiles = OnlineContent.FetchALOTManifest((x) => Application.MainLoop.Invoke(() =>
                {
                    startupStatusLabel.Text = x;
                }));

                // Load the ready state while we are still in background thread
                Application.MainLoop.Invoke(() =>
                {
                    startupStatusLabel.Text = "Checking texture library";
                });

                foreach (var v in manifestFiles.ManifestFiles)
                {
                    v.UpdateReadyStatus();
                }

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

                b.Result = manifestFiles;
            };
            bw.RunWorkerCompleted += (a, b) =>
            {
                if (b.Error == null)
                {
                    Program.CurrentManifestPackage = b.Result as OnlineContent.ManifestPackage;
                    Program.ManifestModes[OnlineContent.ManifestMode.ALOT] = b.Result as OnlineContent.ManifestPackage;
                    FileSelectionUIController bui = new FileSelectionUIController();
                    bui.SetupUI();
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
    }
}
