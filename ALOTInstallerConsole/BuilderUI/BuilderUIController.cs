using System;
using System.Collections.Generic;
using System.Text;
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
            NamedBackgroundWorker builderWorker = new NamedBackgroundWorker("BuilderWorker");
            StageStep ss = new StageStep(installOptions, builderWorker)
            {
                UpdateStatusCallback = updateStatus,
                UpdateProgressCallback = updateProgress
            };
            builderWorker.WorkerReportsProgress = true;
            builderWorker.DoWork += ss.PerformStaging;
            builderWorker.RunWorkerAsync();
        }

        public override void SetupUI()
        {
            var ypos = Pos.Center() - 2;
            Label l = new Label("Building texture installation package")
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
