using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using ALOTInstallerConsole.InstallerUI;
using ALOTInstallerConsole.UserControls;
using ALOTInstallerCore.Builder;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.Objects;
using ALOTInstallerCore.Steps;
using NStack;
using Serilog;
using Terminal.Gui;

namespace ALOTInstallerConsole.BuilderUI
{
    class StagingUIController : UIController
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
                UpdateProgressCallback = updateProgress,
                ResolveMutualExclusiveMods = resolveMutualExclusiveMod
            };
            builderWorker.WorkerReportsProgress = true;
            builderWorker.DoWork += ss.PerformStaging;
            builderWorker.RunWorkerCompleted += (a, b) =>
            {
                if (b.Error == null)
                {
                    performPreinstallCheck();
                }
                else
                {
                    MessageBox.ErrorQuery("Error occured while building textures", $"Error occured while building textures: {b.Error.Message}", "OK");
                }
                FileSelectionUIController fsuic = new FileSelectionUIController();
                fsuic.SetupUI();
                Program.SwapToNewView(fsuic);
            };
            builderWorker.RunWorkerAsync();
        }

        private void performPreinstallCheck()
        {
            MessageDialog md = new MessageDialog("Performing installation precheck [2/2]");
            NamedBackgroundWorker preinstallCheckWorker = new NamedBackgroundWorker("PrecheckWorker-Preinstall");
            preinstallCheckWorker.DoWork += (a, b) =>
            {
                b.Result = Precheck.PerformPreInstallCheck(installOptions);
            };
            preinstallCheckWorker.RunWorkerCompleted += (sender, b) =>
            {
                if (md.IsCurrentTop)
                {
                    // Close the dialog
                    Application.RequestStop();
                }
                if (b.Error != null)
                {
                    Log.Error($"Exception occured in precheck for pre-install: {b.Error.Message}");
                    MessageBox.Query("Precheck failed", b.Result as string, "OK");
                    BuilderUI.FileSelectionUIController fsuic = new FileSelectionUIController();
                    fsuic.SetupUI();
                    Program.SwapToNewView(fsuic);
                }
                else if (b.Result != null)
                {
                    // Precheck failed
                    MessageBox.Query("Precheck failed", b.Result as string, "OK");
                    BuilderUI.FileSelectionUIController fsuic = new FileSelectionUIController();
                    fsuic.SetupUI();
                    Program.SwapToNewView(fsuic);
                }
                else
                {
                    // Precheck passed
                    if (installOptions.FilesToInstall != null)
                    {
                        InstallerUIController installerController = new InstallerUIController();
                        installerController.SetInstallPackage(installOptions);
                        installerController.SetupUI();
                        Program.SwapToNewView(installerController);
                    }
                }
            };
            preinstallCheckWorker.RunWorkerAsync();
            Application.Run(md);

        }

        public override void SignalStopping()
        {

        }

        private InstallerFile resolveMutualExclusiveMod(List<InstallerFile> arg)
        {
            var options = arg.Select(x => (ustring)x.FriendlyName).ToList();
            int abortIndex = options.Count;
            options.Add((ustring)"Abort install");
            int selectedIndex = abortIndex;
            selectedIndex = MessageBox.Query("Select which file to use",
            "Only one of the following mods can be installed. Select which one to use:", options.ToArray());
            if (selectedIndex == abortIndex) return null;
            return arg[selectedIndex];
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
