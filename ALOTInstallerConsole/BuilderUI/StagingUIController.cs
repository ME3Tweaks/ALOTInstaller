using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using ALOTInstallerConsole.InstallerUI;
using ALOTInstallerConsole.UserControls;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.Objects;
using ALOTInstallerCore.Objects.Manifest;
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
            NamedBackgroundWorker builderWorker = new NamedBackgroundWorker("BuilderWorker");
            StageStep ss = new StageStep(installOptions, builderWorker)
            {
                UpdateOverallStatusCallback = updateStatus,
                UpdateProgressCallback = updateProgress,
                ResolveMutualExclusiveMods = resolveMutualExclusiveMod,
                ErrorStagingCallback = errorStaging,
                ConfigureModOptions = configureModOptions,
                NotifyFileBeingProcessed = newFileBeingProcessed,
                PointOfNoReturnNotification = () => true //We already warned user
            };
            builderWorker.WorkerReportsProgress = true;
            builderWorker.DoWork += ss.PerformStaging;
            builderWorker.RunWorkerCompleted += (a, b) =>
            {
                if (b.Error == null && b.Result is bool ok)
                {
                    if (ok)
                    {
                        performPreinstallCheck();
                        return; //precheck will set the next UI
                    }
                    else
                    {
                        MessageBox.ErrorQuery("Unspecified error has occurred", "An unspecified error occurred during staging. Check the log for more information.", "OK");
                    }
                }
                else
                {
                    MessageBox.ErrorQuery("Error occurred while building textures", $"Error occurred while building textures: {b.Error.Message}", "OK");
                }
                FileSelectionUIController fsuic = new FileSelectionUIController();
                Program.SwapToNewView(fsuic);
            };
            builderWorker.RunWorkerAsync();
        }

        private ConcurrentDictionary<FrameView, InstallerFilePrepContainer> processingFVMap = new ConcurrentDictionary<FrameView, InstallerFilePrepContainer>();

        private void newFileBeingProcessed(InstallerFile obj)
        {
            int startingY = 2 + processingFVMap.Count * 3;
            // Lock as Values doesn't provide thread safety
            lock (processingFVMap)
            {
                var availableFv = processingFVMap.First(x => x.Value == null);
                InstallerFilePrepContainer ifpc = new InstallerFilePrepContainer(this, availableFv.Key, obj, startingY);
                Debug.WriteLine("Assigning slot");
                processingFVMap[availableFv.Key] = ifpc;
            }
        }

        private bool configureModOptions(ManifestFile mf, List<ConfigurableMod> optionsToConfigure)
        {
            bool continueStaging = true;
            int num = 1;
            foreach (var v in optionsToConfigure)
            {
                var choices = v.ChoicesHuman.Select(x => (ustring)x).ToList();
                choices.Add((ustring)"Abort install");

                var sIndex = MessageBox.Query($"{mf.FriendlyName} configuration [{num}/{optionsToConfigure.Count}]", $"Select what option you'd like to use for:\n\n{v.ChoiceTitle}\n", choices.ToArray());
                if (sIndex == choices.Count - 1)
                {
                    Log.Information($"User aborted staging on config mod option {v.ChoiceTitle}");
                    continueStaging = false;
                    break;
                }

                v.SelectedIndex = sIndex;
                num++;
            }
            return continueStaging;
        }

        private void errorStaging(string obj)
        {
            MessageBox.ErrorQuery("Error occurred during staging", obj, "OK");
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
                    Log.Error($"Exception occurred in precheck for pre-install: {b.Error.Message}");
                    MessageBox.Query("Precheck failed", b.Result as string, "OK");
                    var fsuic = new FileSelectionUIController();
                    fsuic.SetupUI();
                    Program.SwapToNewView(fsuic);
                }
                else if (b.Result != null)
                {
                    // Precheck failed
                    MessageBox.Query("Precheck failed", b.Result as string, "OK");
                    var fsuic = new FileSelectionUIController();
                    Program.SwapToNewView(fsuic);
                }
                else
                {
                    // Precheck passed
                    if (installOptions.FilesToInstall != null)
                    {
                        InstallerUIController installerController = new InstallerUIController();
                        installerController.SetInstallPackage(installOptions);
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
            object syncObj = new object();
            var options = arg.Select(x => (ustring)(x.ShortFriendlyName ?? x.FriendlyName)).ToList();
            int abortIndex = options.Count;
            options.Add((ustring)"Abort install");
            int selectedIndex = abortIndex;
            Application.MainLoop.Invoke(() =>
            {
                selectedIndex = MessageBox.Query("Select which file to use", "Only one of the following mods can be installed. Select which one to use:", options.ToArray());

                lock (syncObj)
                {
                    Monitor.Pulse(syncObj);
                }
            });
            lock(syncObj){
                Monitor.Wait(syncObj);
            }
            
            if (selectedIndex == abortIndex) return null;
            return arg[selectedIndex];
        }

        internal class InstallerFilePrepContainer
        {
            private InstallerFile ifx;
            private Label statusLabel;
            private Label headerLabel;
            public FrameView ui;
            private StagingUIController uic;

            internal InstallerFilePrepContainer(StagingUIController uic, FrameView ui, InstallerFile ifx, int yOffset)
            {
                this.uic = uic;
                this.ifx = ifx;
                this.ui = ui;
                ifx.PropertyChanged += propertyChangedListener;
                Application.MainLoop.Invoke(() =>
                {

                    headerLabel = new Label(ifx.FriendlyName)
                    {
                        X = 0,
                        Y = 0,
                        Height = 1,
                        Width = Dim.Fill(),
                    };
                    statusLabel = new Label("Processing")
                    {
                        X = 0,
                        Y = 1,
                        Height = 1,
                        Width = Dim.Fill(),
                    };
                    ui.Add(headerLabel, statusLabel);

                });
            }


            internal void Destroy()
            {
                // This must be done immediately and not within the main loop or we will desync
                uic.NotifyNoLongerProcessing(this);
                Application.MainLoop.Invoke(() =>
                {
                    ifx.PropertyChanged -= propertyChangedListener;
                    ui.Remove(statusLabel);
                    ui.Remove(headerLabel);
                    ui = null;
                });
            }

            private void propertyChangedListener(object sender, PropertyChangedEventArgs e)
            {
                switch (e.PropertyName)
                {
                    case nameof(InstallerFile.StatusText):
                        Application.MainLoop.Invoke(() =>
                            statusLabel.Text = ifx.StatusText);
                        break;
                    case nameof(InstallerFile.IsProcessing) when !ifx.IsProcessing:
                        Destroy();
                        break;
                }
            }
        }

        private void NotifyNoLongerProcessing(InstallerFilePrepContainer installerFilePrepContainer)
        {
            Debug.WriteLine("Freeing slot");
            processingFVMap[installerFilePrepContainer.ui] = null; //Unset
        }

        public override void SetupUI()
        {
            var ypos = Pos.Center() - 2;
            Label l = new Label("Preparing to install textures")
            {
                Y = ypos,
                X = 0,
                Height = 1,
                Width = Dim.Fill(),
                TextAlignment = TextAlignment.Centered
            };
            currentStatusLabel = new Label("Staging files for installation")
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
            Add(l, currentStatusLabel, progressbar);

            int numStagingThreads = 2;
            for (int i = 0; i < numStagingThreads; i++)
            {
                FrameView fv = new FrameView($"Thread {i + 1}")
                {
                    Width = 56,
                    Height = 5,
                    X = Pos.Center() + (i % 2 == 0 ? -57 : 1),
                    Y = Pos.Center() + 2 + (i / 2 * 5)
                };
                Add(fv);
                processingFVMap[fv] = null; //Initial mapping
            }
        }
    }
}
