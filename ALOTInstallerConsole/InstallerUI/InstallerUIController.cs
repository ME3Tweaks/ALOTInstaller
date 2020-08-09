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
            var positionY = Pos.Center() - 1;
            topLabel = new Label("Overall Progress")
            {
                X = 0,
                Y = positionY - 1,
                Width = Dim.Fill(),
                Height = 1,
                TextAlignment = TextAlignment.Centered
            };

            middleLabel = new Label("")
            {
                X = 0,
                Y = positionY,
                Width = Dim.Fill(),
                Height = 1,
                TextAlignment = TextAlignment.Centered
            };

            // Dynamically computed
            bottomLabel = new Label("Installing Textures")
            {
                X = 0,
                Y = positionY + 1,
                Width = Dim.Fill(),
                Height = 1,
                TextAlignment = TextAlignment.Centered
            };

            Add(topLabel);
            Add(middleLabel);
            Add(bottomLabel);
        }

        private void setTextFromThread(Label label, string text)
        {
            Application.MainLoop.Invoke(() =>
            {
                label.Text = text;
            });
        }

        public override void BeginFlow()
        {
            string installString = null;
            NamedBackgroundWorker installerWorker = new NamedBackgroundWorker("InstallerWorker");
            InstallStep ss = new InstallStep(package)
            {
                SetInstallString = x=> installString = x,
                SetTopTextCallback = x => setTextFromThread(topLabel, x),
                SetMiddleTextCallback = x => setTextFromThread(middleLabel, x),
                SetBottomTextCallback = x => setTextFromThread(bottomLabel, x),
                SetTopTextVisibilityCallback = x => setVisibilityFromThread(topLabel, x),
                SetMiddleTextVisibilityCallback = x => setVisibilityFromThread(middleLabel, x),
                SetBottomTextVisibilityCallback = x => setVisibilityFromThread(bottomLabel, x)
            };
            installerWorker.WorkerReportsProgress = true;
            installerWorker.DoWork += ss.InstallTextures;
            installerWorker.RunWorkerCompleted += (a, b) =>
            {

                PostInstallUIController bui = new PostInstallUIController();
                bui.setInstalledString(installString);
                bui.SetupUI();
                Program.SwapToNewView(bui);
            };
            installerWorker.RunWorkerAsync();
        }

        public override void SignalStopping()
        {
            
        }

        private void setVisibilityFromThread(Label label, bool visible)
        {
            Application.MainLoop.Invoke(() =>
            {
                if (!visible)
                {
                    //?? Wait for better way to do this, I guess
                    label.Text = "";
                }
            });
        }
    }
}
