using System.Linq;
using ALOTInstallerConsole.BuilderUI;
using ALOTInstallerCore;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.Objects;
using ALOTInstallerCore.Steps;
using ALOTInstallerCore.Steps.Installer;
using Terminal.Gui;

namespace ALOTInstallerConsole.InstallerUI
{
    public class InstallerUIController : UIController
    {
        private InstallOptionsPackage package;
        private Label topLabel;
        private Label middleLabel;
        private Label bottomLabel;
        private Button continueButton;

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

            continueButton = new Button("Continue")
            {
                X = Pos.Center(),
                Y = positionY + 3,
                Width = 12,
                Height = 1,
                Clicked = returnToFileSelection,
                Visible = false
            };

            Add(topLabel, middleLabel, bottomLabel, continueButton);
        }

        private void returnToFileSelection()
        {
            FileSelectionUIController bui = new FileSelectionUIController();
            Program.SwapToNewView(bui);
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
                SetInstallString = x => installString = x,
                SetTopTextCallback = x => setTextFromThread(topLabel, x),
                SetMiddleTextCallback = x => setTextFromThread(middleLabel, x),
                SetBottomTextCallback = x => setTextFromThread(bottomLabel, x),
                SetTopTextVisibilityCallback = x => setVisibilityFromThread(topLabel, x),
                SetMiddleTextVisibilityCallback = x => setVisibilityFromThread(middleLabel, x),
                SetBottomTextVisibilityCallback = x => setVisibilityFromThread(bottomLabel, x),
                ShowStorefrontDontClickUpdateCallback = showStorefrontNoUpdateUI
            };
            installerWorker.WorkerReportsProgress = true;
            installerWorker.DoWork += ss.InstallTextures;
            installerWorker.RunWorkerCompleted += (a, b) =>
            {
                if (b.Error != null)
                {

                }
                else if (b.Result is InstallStep.InstallResult installResult)
                {
                    handleResult(installResult, installString);
                    continueButton.Visible = true;
                    //PostInstallUIController bui = new PostInstallUIController(installResult, installString);
                    //Program.SwapToNewView(bui);
                }

            };
            installerWorker.RunWorkerAsync();
        }

        private void handleResult(InstallStep.InstallResult installResult, string installString)
        {
            middleLabel.Visible = topLabel.Visible = bottomLabel.Visible = true;
            if (installResult == InstallStep.InstallResult.InstallOK)
            {
                middleLabel.Visible = false;
                topLabel.Text = $"Installed {installString}";
                bottomLabel.Text = $"Texture installation succeeded. Ensure you do not install package files (files ending in .pcc, .u, .upk, .sfm) outside of {Utilities.GetAppPrefixedName()} Installer to this game, or you will corrupt it.";
            }
            else if (installResult == InstallStep.InstallResult.InstallOKWithWarning)
            {
                topLabel.Text = $"Installed {installString}";
                middleLabel.Text = "Installation completed with warnings";
                bottomLabel.Text = $"Texture installation succeeded with warnings. Check the installer log for more information on these warnings. Ensure you do not install package files (files ending in .pcc, .u, .upk, .sfm) outside of {Utilities.GetAppPrefixedName()} Installer to this game, or you will corrupt it.";
            }
            else
            {
                // Is this a stage failure?
                StageFailure sf = null;
                foreach (var stage in ProgressHandler.DefaultStages)
                {
                    var failure = stage.FailureInfos?.FirstOrDefault(x => x.FailureResultCode == installResult);
                    if (failure != null)
                    {
                        sf = failure;
                        break;
                    }
                }
                if (sf != null)
                {
                    topLabel.Text = sf.FailureTopText;
                    middleLabel.Text = sf.FailureBottomText;
                    bottomLabel.Text = sf.FailureHeaderText;
                }
            }
        }

        private void showStorefrontNoUpdateUI(Enums.MEGame obj)
        {
            throw new System.NotImplementedException();
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
