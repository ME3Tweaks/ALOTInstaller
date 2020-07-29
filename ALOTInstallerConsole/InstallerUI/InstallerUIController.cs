using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using ALOTInstallerCore.Builder;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.Objects;
using ALOTInstallerCore.Steps;
using Terminal.Gui;

namespace ALOTInstallerConsole.InstallerUI
{
    public class InstallerUIController : UIController
    {
        private InstallOptionsPackage package;
        private Label topLabel;
        private Label middleLabel;
        private Label bottomLabel;

        public void SetInstallPackage(InstallOptionsPackage p)
        {
            this.package = p;
        }

        public override void SetupUI()
        {
            // Dynamically computed
            topLabel = new Label("Overall Progress")
            {
                X = 0,
                Y = Pos.Center() - 1,
                Width = Dim.Fill(),
                Height = 1,
                TextAlignment = TextAlignment.Centered
            };

            middleLabel = new Label("Stage X of Y")
            {
                X = 0,
                Y = Pos.Center() - 1,
                Width = Dim.Fill(),
                Height = 1,
                TextAlignment = TextAlignment.Centered
            };

            // Dynamically computed
            bottomLabel = new Label("Installing Textures")
            {
                X = 0,
                Y = Pos.Center() + 1,
                Width = Dim.Fill(),
                Height = 1,
                TextAlignment = TextAlignment.Centered
            };

            Add(topLabel);
            Add(middleLabel);
            Add(bottomLabel);
        }

        public override void BeginFlow()
        {
            NamedBackgroundWorker installerWorker = new NamedBackgroundWorker("BuilderWorker");
            InstallStep ss = new InstallStep(package, installerWorker);
            installerWorker.WorkerReportsProgress = true;
            installerWorker.ProgressChanged += handleProgress;
            installerWorker.DoWork += ss.PerformStaging;
            installerWorker.RunWorkerAsync();
        }

        private void handleProgress(object sender, ProgressChangedEventArgs e)
        {
            if (e.UserState is ProgressPackage pp)
            {

            }
        }
    }
}
