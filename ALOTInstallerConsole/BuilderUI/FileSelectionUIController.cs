using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ALOTInstallerConsole.UserControls;
using ALOTInstallerCore;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.ModManager.Objects;
using ALOTInstallerCore.Objects;
using ALOTInstallerCore.Objects.Manifest;
using ALOTInstallerCore.Startup;
using ALOTInstallerCore.Steps;
using NStack;
using Serilog;
using Terminal.Gui;

namespace ALOTInstallerConsole.BuilderUI
{
    public class FileSelectionUIController : UIController
    {
        private InstallerFileDataSource dataSource;
        private ApplicableGame VisibleGames = ApplicableGame.ME1 | ApplicableGame.ME2 | ApplicableGame.ME3;

        public override void SetupUI()
        {
            Locations.ME3Target.InstallBinkBypass();
            Title = "ALOT Installer";
            List<InstallerFile> ifS = new List<InstallerFile>();

            if (Program.CurrentManifestPackage != null)
            {
                Title += $" Manifest version {Program.CurrentManifestPackage.ManifestVersion}";
                ifS.AddRange(Program.CurrentManifestPackage.ManifestFiles);
            }
            dataSource = new InstallerFileDataSource(ifS);
            ManifestFilesListView = new ListView(dataSource)
            {
                Width = 48,
                Height = Dim.Fill(),
                X = 0,
                Y = 0,
                SelectedItemChanged = SelectedManifestFileChanged
            };

            // LEFT SIDE
            var fv = new FrameView("ALOT manifest files")
            {
                Width = 50,
                Height = Dim.Fill() - 1,
                X = 0,
                Y = 0
            };

            fv.Add(ManifestFilesListView);
            Add(fv);

            // RIGHT SIDE
            fv = new FrameView("Selected file information")
            {
                Width = Dim.Fill(),
                Height = Dim.Fill() - 1,
                X = 50,
                Y = 0
            };
            Add(fv);

            int y = 0;
            AuthorTextBlock = new Label("")
            {
                Width = Dim.Fill(),
                Height = 1,
                Y = y++
            };
            fv.Add(AuthorTextBlock);

            FilenameTextBlock = new Label("")
            {
                Width = Dim.Fill(),
                Height = 1,
                Y = y++
            };
            fv.Add(FilenameTextBlock);

            AppliesToGamesTextBlock = new Label("")
            {
                Width = Dim.Fill(),
                Height = 1,
                Y = y++
            };
            fv.Add(AppliesToGamesTextBlock);

            ExpectedFileSizeTextBlock = new Label("")
            {
                Width = Dim.Fill(),
                Height = 1,
                Y = y++
            };
            fv.Add(ExpectedFileSizeTextBlock);

            ExpectedFileSizeHashTextBlock = new Label("")
            {
                Width = Dim.Fill(),
                Height = 1,
                Y = y++
            };
            fv.Add(ExpectedFileSizeHashTextBlock);

            ReadyTextBlock = new Label("")
            {
                Width = Dim.Fill(),
                Height = 1,
                Y = y++
            };
            fv.Add(ReadyTextBlock);

            UnpackedFilenameTextBlock = new Label("")
            {
                Width = Dim.Fill(),
                Height = 1,
                Y = y++
            };
            fv.Add(UnpackedFilenameTextBlock);

            UnpackedFilesizeTextBlock = new Label("")
            {
                Width = Dim.Fill(),
                Height = 1,
                Y = y++
            };
            fv.Add(UnpackedFilesizeTextBlock);
            UnpackedFileHashTextBlock = new Label("")
            {
                Width = Dim.Fill(),
                Height = 1,
                Y = y++
            };
            fv.Add(UnpackedFileHashTextBlock);

            DownloadButton = new Button("Download")
            {
                Width = 12,
                Height = 1,
                X = Pos.Right(fv) - fv.X - 14,
                Y = Pos.Bottom(fv) - 3,
                Clicked = DownloadClicked
            };
            fv.Add(DownloadButton);


            Button settingsButton = new Button("Settings")
            {
                X = Pos.Left(this),
                Y = Pos.Bottom(this) - 3,
                Height = 1,
                Width = 12,
                Clicked = Settings_Clicked
            };
            Add(settingsButton);

            Button changeModeButton = new Button("Change mode")
            {
                X = Pos.Left(this) + 13,
                Y = Pos.Bottom(this) - 3,
                Height = 1,
                Width = 15,
                Clicked = ChangeMode_Clicked
            };
            Add(changeModeButton);

            CheckBox me1FilterCheckbox = new CheckBox("ME1")
            {
                X = Pos.Left(this) + 29,
                Y = Pos.Bottom(this) - 3,
                Height = 1,
                Width = 15,
                Checked = true,
                Toggled = x => changeFilter(Enums.MEGame.ME1, !x)
            };
            Add(me1FilterCheckbox);

            CheckBox me2FilterCheckbox = new CheckBox("ME2")
            {
                X = Pos.Left(this) + 37,
                Y = Pos.Bottom(this) - 3,
                Height = 1,
                Width = 15,
                Checked = true,
                Toggled = x => changeFilter(Enums.MEGame.ME2, !x)
            };
            Add(me2FilterCheckbox);

            CheckBox me3FilterCheckbox = new CheckBox("ME3")
            {
                X = Pos.Left(this) + 45,
                Y = Pos.Bottom(this) - 3,
                Height = 1,
                Width = 15,
                Checked = true,
                Toggled = x => changeFilter(Enums.MEGame.ME3, !x)
            };
            Add(me3FilterCheckbox);

            Add(new Button("Import assistant")
            {
                X = Pos.Right(this) - 34,
                Y = Pos.Bottom(this) - 3,
                Height = 1,
                Width = 20,
                Clicked = ImportAssistant_Click
            });

            Button installButton = new Button("Install")
            {
                X = Pos.Right(this) - 13,
                Y = Pos.Bottom(this) - 3,
                Height = 1,
                Width = 11,
                Clicked = InstallButton_Click
            };
            Add(installButton);

            TextureLibrary.ResetAllReadyStatuses(Program.CurrentManifestPackage.ManifestFiles);
        }

        public ListView ManifestFilesListView { get; set; }

        private void DownloadClicked()
        {
            if (ManifestFilesListView.SelectedItem >= 0)
            {
                var file = dataSource.InstallerFiles[ManifestFilesListView.SelectedItem];
                if (file is ManifestFile mf)
                {
                    Utilities.OpenWebPage(mf.DownloadLink);
                }
            }
        }

        private void ImportAssistant_Click()
        {
            LibraryImporterUIController luic = new LibraryImporterUIController();
            luic.SetupUI();
            Program.SwapToNewView(luic);
        }

        private void changeFilter(Enums.MEGame game, bool nowChecked)
        {
            Debug.WriteLine($"{game} now checked: {nowChecked}");
            if (nowChecked)
                VisibleGames |= game.ToApplicableGame();
            else
                VisibleGames &= ~game.ToApplicableGame();
            Debug.WriteLine($" > {VisibleGames}");
            RefreshShownFiles();
        }

        private async void InstallButton_Click()
        {
            List<string> paths = new List<string>();
            if (Locations.ME1Target != null) paths.Add("ME1");
            if (Locations.ME2Target != null) paths.Add("ME2");
            if (Locations.ME3Target != null) paths.Add("ME3");
            paths.Add("Abort");
            var selectedIndex = MessageBox.Query("Select game", "Select which game to install for.", paths.Select(x => (ustring)x.ToString()).ToArray());
            if (paths[selectedIndex] == "Abort" || selectedIndex < 0) return;

            GameTarget target = null;
            if (paths[selectedIndex] == "ME1") target = Locations.ME1Target;
            if (paths[selectedIndex] == "ME2") target = Locations.ME2Target;
            if (paths[selectedIndex] == "ME3") target = Locations.ME3Target;

            // Precheck done
            int warningResponse = MessageBox.Query("Warning", "Once you install texture mods, you will not be able to further install mods that contain .pcc, .u, .upk or .sfm files without breaking textures in the game. Make sure you have all of your DLC and non-texture mods installed now, as you will not be able to install them later.", "OK", "Abort install");
            if (warningResponse != 0) return; //abort

            // Show options
            if (Program.CurrentManifestPackage.ManifestFiles.Any())
            {
                showOptionsDialog(target);

            }
        }

        /// <summary>
        /// Shows the options dialog. Performs precheck
        /// </summary>
        /// <param name="target"></param>
        private void showOptionsDialog(GameTarget target)
        {
            #region Button handlers
            bool buildAndInstall = false;
            var buildAndInstallButton = new Button("Install")
            {
                Clicked = () =>
                {
                    Application.RequestStop();
                    buildAndInstall = true;
                }
            };
            var cancelButton = new Button("Cancel")
            {
                Clicked = () => Application.RequestStop()
            };
            #endregion

            var availableInstallOptions = InstallOptionsStep.CalculateInstallOptions(target, CurrentMode, dataSource.InstallerFiles);

            // Begin setting up UI items
            int y = 0;
            int maxWidth = 53;
            int requiredHeight = 3;
            var installOptionsPicker = new Dialog("Choose installation options", buildAndInstallButton, cancelButton)
            {
                Height = 10,
                Width = 90
            };
            installOptionsPicker.Add(new Label($"Installer mode: {CurrentMode}")
            {
                X = 1,
                Y = y++,
                Width = Dim.Fill(),
                Height = 1
            });

            y++; // Newline
            Dictionary<InstallOptionsStep.InstallOption, CheckBox> installOptionMapping = new Dictionary<InstallOptionsStep.InstallOption, CheckBox>();
            foreach (var v in availableInstallOptions)
            {
                CheckBox cb = new CheckBox(getUIString(v, dataSource.InstallerFiles))
                {
                    X = 1,
                    Y = y++,
                    Width = Dim.Fill(),
                    Height = 1,
                    Checked = v.Value.state == InstallOptionsStep.OptionState.CheckedVisible ||
                              v.Value.state == InstallOptionsStep.OptionState.ForceCheckedVisible,
                    CanFocus = v.Value.state == InstallOptionsStep.OptionState.CheckedVisible ||
                               v.Value.state == InstallOptionsStep.OptionState.UncheckedVisible
                };
                installOptionsPicker.Add(cb);
                installOptionMapping[v.Key] = cb;
                maxWidth = Math.Max(maxWidth, cb.Text.Length + 4);

                if (v.Value.reasonForState != null)
                {
                    installOptionsPicker.Add(new Label(v.Value.reasonForState)
                    {
                        X = 1,
                        Y = y++,
                        Width = Dim.Fill(),
                        Height = 1,
                    });
                    maxWidth = Math.Max(maxWidth, v.Value.reasonForState.Length + 4);
                }
            }

            y++;
            CheckBox compressPackagesCb = new CheckBox("Compress packages")
            {
                X = 1,
                Y = y++,
                Width = Dim.Fill(),
                Height = 1,
                Checked = target.Game != Enums.MEGame.ME1
            };

            if (target.Game > Enums.MEGame.ME1)
            {
                installOptionsPicker.Add(compressPackagesCb);
            }
            else
            {
                y--; // remove newline it made
            }

            CheckBox use2KLodsCb = new CheckBox("Set 2K LODs instead of 4K")
            {
                X = 1,
                Y = y++,
                Width = Dim.Fill(),
                Height = 1,
            };
            installOptionsPicker.Add(use2KLodsCb);

            y++;
            y++;
            installOptionsPicker.Add(new Label("Items marked with * cannot be toggled on or off")
            {
                X = 1,
                Y = y++,
                Width = Dim.Fill(),
                Height = 1,
            });
            // We start at 50 so this is already not too long

            installOptionsPicker.Height = y + requiredHeight;
            installOptionsPicker.Width = maxWidth;

            Application.Run(installOptionsPicker);

            if (buildAndInstall)
            {
                var optionsPackage = new InstallOptionsPackage()
                {
                    InstallTarget = target,
                    FilesToInstall = dataSource.InstallerFiles,
                    InstallALOT = getInstallOptionValue(InstallOptionsStep.InstallOption.ALOT, installOptionMapping),
                    InstallALOTUpdate = getInstallOptionValue(InstallOptionsStep.InstallOption.ALOTUpdate, installOptionMapping),
                    InstallMEUITM = getInstallOptionValue(InstallOptionsStep.InstallOption.MEUITM, installOptionMapping),
                    InstallALOTAddon = getInstallOptionValue(InstallOptionsStep.InstallOption.ALOTAddon, installOptionMapping),
                    InstallUserfiles = getInstallOptionValue(InstallOptionsStep.InstallOption.UserFiles, installOptionMapping),
                    InstallerMode = CurrentMode,
                    RepackGameFiles = compressPackagesCb.Checked,
                    Limit2K = use2KLodsCb.Checked
                };

                MessageDialog md = new MessageDialog("Performing installation precheck [1/2]");
                NamedBackgroundWorker prestageCheckWorker = new NamedBackgroundWorker("PrecheckWorker-Prestaging");
                prestageCheckWorker.DoWork += (a, b) => { b.Result = Precheck.PerformPreStagingCheck(optionsPackage); };
                prestageCheckWorker.RunWorkerCompleted += (sender, b) =>
                {
                    if (md.IsCurrentTop)
                    {
                        // Close the dialog
                        Application.RequestStop();
                    }

                    if (b.Error != null)
                    {
                        Log.Error($"Exception occured in precheck for pre-stage: {b.Error.Message}");
                        MessageBox.Query("Precheck failed", b.Result as string, "OK");
                    }
                    else if (b.Result != null)
                    {
                        // Precheck failed
                        MessageBox.Query("Precheck failed", b.Result as string, "OK");
                    }
                    else
                    {
                        // Precheck passed
                        var builderUI = new BuilderUI.StagingUIController();
                        builderUI.SetOptionsPackage(optionsPackage);
                        builderUI.SetupUI();
                        TextureLibrary.StopLibraryWatcher(); // Kill watcher
                        Program.SwapToNewView(builderUI);
                    }
                };
                prestageCheckWorker.RunWorkerAsync();
                Application.Run(md);
            }
        }

        /// <summary>
        /// Method to make code less ugly
        /// </summary>
        /// <param name="key"></param>
        /// <param name="installOptionMapping"></param>
        /// <returns></returns>
        private bool getInstallOptionValue(InstallOptionsStep.InstallOption key, Dictionary<InstallOptionsStep.InstallOption, CheckBox> installOptionMapping)
        {
            return installOptionMapping.ContainsKey(key) ? installOptionMapping[key].Checked : false;
        }

        private ustring getUIString(KeyValuePair<InstallOptionsStep.InstallOption, (InstallOptionsStep.OptionState state, string reasonForState)> option, List<InstallerFile> installerFiles)
        {

            var forcedOption = option.Value.state == InstallOptionsStep.OptionState.ForceCheckedVisible || option.Value.state == InstallOptionsStep.OptionState.DisabledVisible;
            string suffix = forcedOption ? "*" : "";

            if (option.Key == InstallOptionsStep.InstallOption.ALOT) return (installerFiles.FirstOrDefault(x => x.AlotVersionInfo.ALOTVER > 0 && x.AlotVersionInfo.ALOTUPDATEVER == 0)?.FriendlyName ?? "ALOT") + suffix;
            if (option.Key == InstallOptionsStep.InstallOption.ALOTUpdate) return (installerFiles.FirstOrDefault(x => x.AlotVersionInfo.ALOTVER > 0 && x.AlotVersionInfo.ALOTUPDATEVER != 0)?.FriendlyName ?? "ALOT update") + suffix;
            if (option.Key == InstallOptionsStep.InstallOption.ALOTAddon) return "ALOT Addon" + suffix;
            if (option.Key == InstallOptionsStep.InstallOption.MEUITM) return "MEUITM" + suffix;
            if (option.Key == InstallOptionsStep.InstallOption.UserFiles) return "User files" + suffix;
            return "UNKNOWN OPTION";
        }

        private void Settings_Clicked()
        {
            SettingsUIController sui = new SettingsUIController();
            sui.SetupUI();
            Program.SwapToNewView(sui);
        }

        private void ChangeMode_Clicked()
        {
            var str = Program.ManifestModes.Keys.Select(x => (ustring)x.ToString()).ToArray();
            var res = MessageBox.Query(50, 7, "Mode selector", "Select a mode for ALOT Installer.", str);
            CurrentMode = Program.ManifestModes.Keys.ToList()[res];
            RefreshShownFiles();
        }

        private void RefreshShownFiles()
        {
            Program.CurrentManifestPackage = Program.ManifestModes[CurrentMode];
            TextureLibrary.ResetAllReadyStatuses(Program.CurrentManifestPackage.ManifestFiles);
            //var userFiles = dataSource.InstallerFiles.Where(x => x is UserFile);
            dataSource.InstallerFiles.Clear();
            dataSource.InstallerFiles.AddRange(Program.CurrentManifestPackage.ManifestFiles.Where(x => (x.ApplicableGames & VisibleGames) != 0));
            //dataSource.InstallerFiles.AddRange(userFiles);
            Application.Refresh();
        }

        /// <summary>
        /// The current mode
        /// </summary>
        public OnlineContent.ManifestMode CurrentMode { get; set; } = OnlineContent.ManifestMode.ALOT;
        public Label FilenameTextBlock { get; set; }
        public Label UnpackedFileHashTextBlock { get; set; }
        public Label UnpackedFilesizeTextBlock { get; set; }
        public Label UnpackedFilenameTextBlock { get; set; }
        public Label ReadyTextBlock { get; set; }
        public Label ExpectedFileSizeHashTextBlock { get; set; }
        public Label ExpectedFileSizeTextBlock { get; set; }
        public Button DownloadButton { get; set; }

        private void SelectedManifestFileChanged(ListViewItemEventArgs obj)
        {
            if (obj.Value is ManifestFile mf)
            {
                UpdateDisplayedManifestFile(mf);
            }
        }

        private void UpdateDisplayedManifestFile(ManifestFile mf)
        {
            AppliesToGamesTextBlock.Text = "Applies to: " + string.Join(' ', mf.SupportedGames());
            ReadyTextBlock.Text = "Ready: " + mf.Ready.ToString();
            AuthorTextBlock.Text = "Author: " + mf.Author;
            FilenameTextBlock.Text = "Filename: " + mf.Filename;
            ExpectedFileSizeHashTextBlock.Text = "File MD5: " + mf.FileMD5;
            ExpectedFileSizeTextBlock.Text = $"File size: {mf.FileSize} ({FileSizeFormatter.FormatSize(mf.FileSize)})";

            // Unpacked
            UnpackedFilenameTextBlock.Text = "Unpacked filename: " + (mf.UnpackedSingleFilename != null ? mf.UnpackedSingleFilename : "N/A");
            UnpackedFilesizeTextBlock.Text = "Unpacked file size: " + (mf.UnpackedFileSize > 0 ? mf.UnpackedFileSize.ToString() : "N/A");
            UnpackedFileHashTextBlock.Text = "Unpacked file MD5: " + (mf.UnpackedFileMD5 != null ? mf.UnpackedFileMD5 : "N/A");
        }

        public Label AuthorTextBlock { get; set; }
        public Label AppliesToGamesTextBlock { get; private set; }

        private void ManifestFileReadyStatusChanged(ManifestFile mf)
        {
            Application.Refresh();
        }
        public override void BeginFlow()
        {
            TextureLibrary.SetupLibraryWatcher(Program.CurrentManifestPackage.ManifestFiles.OfType<ManifestFile>().ToList(), ManifestFileReadyStatusChanged);
        }

        public override void SignalStopping()
        {
            // This window will close. We need to clear the ready status changed action so we don't keep reference to us
            TextureLibrary.UnregisterCallbacks();
        }

        internal class InstallerFileDataSource : IListDataSource
        {
            public List<InstallerFile> InstallerFiles { get; set; }

            public bool IsMarked(int item) => false;

            public int Count => InstallerFiles.Count;

            public InstallerFileDataSource(List<InstallerFile> itemList) => InstallerFiles = itemList;

            public void Render(ListView container, ConsoleDriver driver, bool selected, int item, int col, int line, int width)
            {
                container.Move(col, line);

                InstallerFile instF = InstallerFiles[item];
                RenderUstr(driver, instF.Ready ? "*" : " ", 1, 0, 2);
                RenderUstr(driver, instF.FriendlyName, 4, 0, 48);
            }

            public void SetMark(int item, bool value)
            {
            }

            // A slightly adapted method from: https://github.com/migueldeicaza/gui.cs/blob/fc1faba7452ccbdf49028ac49f0c9f0f42bbae91/Terminal.Gui/Views/ListView.cs#L433-L461
            private void RenderUstr(ConsoleDriver driver, ustring ustr, int col, int line, int width)
            {
                int used = 0;
                int index = 0;
                while (index < ustr.Length)
                {
                    (var rune, var size) = Utf8.DecodeRune(ustr, index, index - ustr.Length);
                    var count = System.Rune.ColumnWidth(rune);
                    if (used + count >= width) break;
                    driver.AddRune(rune);
                    used += count;
                    index += size;
                }

                while (used < width)
                {
                    driver.AddRune(' ');
                    used++;
                }
            }

            public IList ToList()
            {
                return InstallerFiles;
            }
        }
    }
}
