using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using ALOTInstallerConsole.InstallerUI;
using ALOTInstallerCore.Builder;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.Objects;
using Terminal.Gui;

namespace ALOTInstallerConsole.BuilderUI
{
    class BuilderUIController : UIController
    {
        private Label currentStatusLabel;
        private InstallOptionsPackage installOptions;
        private ProgressBar progressbar;

        public void SetOptionsPackage(InstallOptionsPackage package)
        {
            installOptions = package;
        }

        void updateProgress(int done, int total)
        {
            var value = done * 1.0f / total;
            Application.MainLoop.Invoke(() => { progressbar.Fraction = value; });
        }

        void updateStatus(string newStatus)
        {
            Application.MainLoop.Invoke(() => { currentStatusLabel.Text = newStatus; });
        }

        public override void BeginFlow()
        {
            foreach (var f in installOptions.FilesToInstall)
            {
                f.PropertyChanged += InstallerFilePropertyChanged;
            }

            NamedBackgroundWorker builderWorker = new NamedBackgroundWorker("BuilderWorker");
            StageStep ss = new StageStep(installOptions, builderWorker)
            {
                UpdateStatusCallback = updateStatus,
                UpdateProgressCallback = updateProgress
            };
            builderWorker.WorkerReportsProgress = true;
            builderWorker.DoWork += ss.PerformStaging;
            builderWorker.RunWorkerCompleted += (a, b) =>
            {
                if (b.Error == null)
                {
                    InstallerUIController fsuic = new InstallerUIController();
                    fsuic.SetInstallPackage(installOptions);
                    fsuic.SetupUI();
                    Program.SwapToNewView(fsuic);
                }
                else
                {
                    MessageBox.ErrorQuery("Error occured while building textures", $"Error occured while building textures: {b.Error.Message}");
                    FileSelectionUIController fsuic = new FileSelectionUIController();
                    fsuic.SetupUI();
                    Program.SwapToNewView(fsuic);
                }
            };
            builderWorker.RunWorkerAsync();
        }

        private void InstallerFilePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is InstallerFile ifx && ifx.IsProcessing)
            {
                if (e.PropertyName == nameof(InstallerFile.StatusText))
                {
                    updateStatus($"[{ifx.FriendlyName}] {ifx.StatusText}");
                }
            }
        }

        public override void SetupUI()
        {
            var ypos = Pos.Center() - 2;
            Label l = new Label("Building texture installation packages")
            {
                Y = ypos,
                X = 0,
                Height = 1,
                Width = Dim.Fill(),
                TextAlignment = TextAlignment.Centered
            };
            currentStatusLabel = new Label("Preparing to build textures")
            {
                Y = ypos + 1,
                X = 0,
                Height = 1,
                Width = Dim.Fill(),
                TextAlignment = TextAlignment.Centered
            };
            progressbar = new ProgressBar()
            {
                X = Pos.Center(),
                Y = ypos + 2,
                Width = 50,
                Height = 1,
                ColorScheme = Colors.Dialog
            };
            Add(l);
            Add(currentStatusLabel);
            Add(progressbar);
        }
    }
}
