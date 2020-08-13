using System;
using System.Collections.Generic;
using System.ComponentModel;
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
            var listItems = new List<string>(50);
            Random random = new Random();

            for (int i = 0; i < 50; i++)
            {
                StringBuilder str_build = new StringBuilder();

                char letter;
                var length = random.Next(30) + 1;
                for (int j = 0; j < length; j++)
                {
                    double flt = random.NextDouble();
                    int shift = Convert.ToInt32(Math.Floor(25 * flt));
                    letter = Convert.ToChar(shift + 65);
                    str_build.Append(letter);
                    //j++;
                }
                listItems.Add(str_build.ToString());
            }
            ScrollDialog.Prompt("Test", "I am the message", listItems, "OK");
            //View scrollableContent = new View()
            //{
            //    Width = 40,
            //    Height = 50,
            //};


            //int maxW = 0;
            //int maxH = 0;
            //for (int i = 0; i < 50; i++)
            //{
            //    var str = "TEST I AM A LINE SHOW ME PLS LINE " + i;
            //    int w = 47;
            //    scrollableContent.Add(new Label(str)
            //    {
            //        X = 0,
            //        Y = i,
            //        Width = w,
            //        ColorScheme = Colors.Menu
            //    });
            //    maxW = Math.Max(maxW, str.Length);
            //    maxW = Math.Max(maxW, w);
            //    maxH++;
            //}


            //ScrollDialog sd = new ScrollDialog("Scroll dialog", "This is top message", "This is bottom message", scrollableContent, maxW, maxH, new Button("OK")
            //{
            //    Clicked = Application.RequestStop
            //});
            //Application.Run(sd);



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
