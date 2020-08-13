using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ALOTInstallerConsole.UserControls;
using ALOTInstallerCore;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.ModManager.ME3Tweaks;
using ALOTInstallerCore.ModManager.Objects;
using ALOTInstallerCore.Objects;
using ALOTInstallerCore.Objects.Manifest;
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
        private FrameView leftsideListContainer;
        private FrameView selectedFileInfoFrameView;
        private ScrollView leftSideScrollView;

        public override void SetupUI()
        {

            //Title = "ALOT Installer";
            #region Menu

            var modeMenuItems = new List<MenuItem>();
            foreach (var m in ManifestHandler.MasterManifest.ManifestModePackageMappping)
            {
                modeMenuItems.Add(new MenuItem(m.Key.ToString(), $"({ManifestHandler.MasterManifest.ManifestModePackageMappping[m.Key].ModeDescription})", () => ChangeMode(m.Key)));
            }

            List<MenuItem> textureInstallationInfoMenuItems = new List<MenuItem>();
            foreach (var v in Locations.GetAllAvailableTargets())
            {
                var tii = v.GetInstalledALOTInfo();
                if (tii != null)
                {
                    textureInstallationInfoMenuItems.Add(new MenuItem($"{v.Game}: {v.GetInstalledALOTInfo()}", "", null));
                }
                else
                {
                    textureInstallationInfoMenuItems.Add(new MenuItem($"{v.Game}: No textures installed", "", null));
                }
            }


            MenuBar mb = new MenuBar(new MenuBarItem[]
            {
                new MenuBarItem("Files", new MenuItem[]
                {
                    new MenuItem("_Import manifest files", "(Import files listed in manifest)", ImportManifestFiles, ()=> ManifestHandler.MasterManifest.ManifestModePackageMappping.Count > 1),
                    new MenuItem("_Add user files to current mode", "(Add your own files to install)",  AddUserFiles),
                    new MenuItem("_Cleanup texture library folder", "(Delete old files from texture library)", CleanupTextureLibrary),
                }),
                new MenuBarItem("Change mode", modeMenuItems.ToArray()),
                new MenuBarItem("Game Status",
                    textureInstallationInfoMenuItems.ToArray()),
                new MenuBarItem("_Tools",new MenuItem[] {
                    new MenuItem("Backup & Restore", "(Manage game backups & restores)", () =>
                    {
                        Program.SwapToNewView(new BackupRestoreUIController());
                    }),
                    new MenuItem("Run AutoTOC", "(Update ME3 TOC files)",RunAutoToc, ()=>Locations.ME3Target != null),
                }),
                new MenuBarItem("_Help",new MenuItem[] {
                    new MenuItem("ALOT Discord", "(Support for ALOT Installer)",()=>Utilities.OpenWebPage(ALOTCommunity.DiscordInviteLink)),
                })
            });

            Add(mb);
            #endregion

            dataSource = new InstallerFileDataSource(ManifestHandler.GetManifestFilesForMode(ManifestHandler.CurrentMode, true));
            ManifestFilesListView = new ListView(dataSource)
            {
                Width = 48,
                Height = Dim.Fill(),
                X = 0,
                Y = 0,
                SelectedItemChanged = SelectedListViewFileChanged
            };

            // LEFT SIDE
            leftsideListContainer = new FrameView()
            {
                Width = 50,
                Height = Dim.Fill() - 2,
                X = 0,
                Y = 1
            };

            leftSideScrollView = new ScrollView()
            {
                X = 0,
                Y = 0,
                Height = Dim.Fill(),
                Width = 50,
            };

            //uncomment whenever gui.cs scrollview gets fixed for Dim.Fill()
            //leftSideScrollView.Add(ManifestFilesListView);
            //leftsideListContainer.Add(leftSideScrollView);

            leftsideListContainer.Add(ManifestFilesListView);
            Add(leftsideListContainer);

            // RIGHT SIDE
            selectedFileInfoFrameView = new FrameView("Selected file information")
            {
                Width = Dim.Fill(),
                Height = Dim.Fill() - 2,
                X = 50,
                Y = 1
            };
            Add(selectedFileInfoFrameView);


            Button settingsButton = new Button("Settings")
            {
                X = Pos.Left(this),
                Y = Pos.Bottom(this) - 2,
                Height = 1,
                Width = 12,
                Clicked = Settings_Clicked
            };
            Add(settingsButton);

            CheckBox me1FilterCheckbox = new CheckBox("Show ME1 files")
            {
                X = 17,
                Y = Pos.Bottom(this) - 2,
                Height = 1,
                Width = 15,
                Checked = true,
                Toggled = x => changeFilter(Enums.MEGame.ME1, !x)
            };
            Add(me1FilterCheckbox);

            CheckBox me2FilterCheckbox = new CheckBox("Show ME2 files")
            {
                X = 37,
                Y = Pos.Bottom(this) - 2,
                Height = 1,
                Width = 15,
                Checked = true,
                Toggled = x => changeFilter(Enums.MEGame.ME2, !x)
            };
            Add(me2FilterCheckbox);

            CheckBox me3FilterCheckbox = new CheckBox("Show ME3 files")
            {
                X = 57,
                Y = Pos.Bottom(this) - 2,
                Height = 1,
                Width = 15,
                Checked = true,
                Toggled = x => changeFilter(Enums.MEGame.ME3, !x)
            };
            Add(me3FilterCheckbox);

            //if (ManifestHandler.MasterManifest != null)
            //{
            //    Add(new Button("Import assistant")
            //    {
            //        X = 30,
            //        Y = Pos.Bottom(this) - 4,
            //        Height = 1,
            //        Width = 20,
            //        Clicked = ImportAssistant_Click
            //    });
            //}

            Button diagButton = new Button("Diagnostics")
            {
                X = Pos.Right(this) - 27,
                Y = Pos.Bottom(this) - 2,
                Height = 1,
                Clicked = Diagnostics_Click
            };
            Add(diagButton);

            Button installButton = new Button("Install")
            {
                X = Pos.Right(this) - 12,
                Y = Pos.Bottom(this) - 2,
                Height = 1,
                Width = 11,
                Clicked = InstallButton_Click
            };
            Add(installButton);

            TextureLibrary.ResetAllReadyStatuses(ManifestHandler.GetManifestFilesForMode(ManifestHandler.CurrentMode, true));
            SetLeftsideTitle();
            UpdateLeftSideScrollViewSizing();
        }

        private void RunAutoToc()
        {
            ProgressDialog pd = new ProgressDialog("AutoTOC", "Performing AutoTOC", "Calculating Table of Contents (TOC) files", true);
            NamedBackgroundWorker nbw = new NamedBackgroundWorker("AutoTocWorker");
            nbw.DoWork += (a, b) =>
            {
                AutoTOC.RunTOCOnGameTarget(Locations.ME3Target, x => pd.ProgressValue = x);
            };
            nbw.RunWorkerCompleted += (a, b) =>
            {
                if (pd.IsCurrentTop)
                {
                    Application.RequestStop(); // Close dialog
                }

                MessageBox.Query("AutoTOC complete", "AutoTOC of Mass Effect 3 complete.", "OK");
            };
            nbw.RunWorkerAsync();
            Application.Run(pd);
        }

        private void UpdateLeftSideScrollViewSizing()
        {
            int w = 0;
            int h = 0;
            foreach (var v in dataSource.ShownFiles)
            {
                w = Math.Max(w, v.FriendlyName.Length);
                h++;

            }
            //leftSideScrollView.Bounds = new Rect(1, 2, 50, Application.Top.Bounds.Height);
            leftSideScrollView.ContentSize = new Size(w, h);
        }

        private void CleanupTextureLibrary()
        {
            var unusedFilesInLib = TextureLibrary.GetUnusedFilesInLibrary();
            if (unusedFilesInLib.Any())
            {
                string message = "The following files located in the texture library are no longer used or were moved into the texture library manually and are unused, and can be safely deleted:\n";
                foreach (var v in unusedFilesInLib)
                {
                    message += $"\n{v} ({FileSizeFormatter.FormatSize(new FileInfo(Path.Combine(Settings.TextureLibraryLocation, v)).Length)})";

                }
                message += "\n\nDelete these files?";
                int result = MessageBox.Query(100, 20, "Irrelevant files found", message, "Delete files", "Leave files");
                if (result == 0)
                {
                    // Delete em'
                    foreach (var v in unusedFilesInLib)
                    {
                        var fullPath = Path.Combine(Settings.TextureLibraryLocation, v);
                        Log.Information($"Deleting unused file in texture library: {fullPath}");
                        try
                        {
                            File.Delete(fullPath);
                        }
                        catch (Exception e)
                        {
                            Log.Error($"Error deleting file: {e.Message}");
                        }
                    }
                }
            }
            else
            {
                MessageBox.Query("Library is clean", "No unused files were found in the texture library folder.", "OK");
            }
        }

        public FileSelectionUIController() : base("", -1)
        {

        }

        private void AddUserFiles()
        {
            LibraryImporterController.LoadUserFile();
        }

        private void ImportManifestFiles()
        {
            LibraryImporterController.ImportManifestFilesFromFolder();
        }

        private void Diagnostics_Click()
        {
            DiagnosticsController.InitDiagnostics();

        }

        public ListView ManifestFilesListView { get; set; }

        private void DownloadClicked()
        {
            if (ManifestFilesListView.SelectedItem >= 0)
            {
                var file = dataSource.ShownFiles[ManifestFilesListView.SelectedItem];
                if (file is ManifestFile mf)
                {
                    Utilities.OpenWebPage(mf.DownloadLink);
                }
            }
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

            // Warn user
            int warningResponse = MessageBox.Query("Warning", "Once you install texture mods, you will not be able to further install mods that contain .pcc, .u, .upk or .sfm files without breaking textures in the game. Make sure you have all of your DLC and non-texture mods installed now, as you will not be able to install them later.", "OK", "Abort install");
            if (warningResponse != 0) return; //abort

            // Show options
            if (dataSource.ShownFiles.Any(x => x.Ready && !x.Disabled))
            {
                showOptionsDialog(target);
            }
            else
            {
                Log.Error("Cannot start install process: No files are ready or enabled for install in library.");
                MessageBox.Query("Cannot install textures",
                    "There are no files ready for installation. Import manifest into the texture library, or add your own user files to install.", "OK");
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

            var availableInstallOptions = InstallOptionsStep.CalculateInstallOptions(target, ManifestHandler.CurrentMode, dataSource.ShownFiles);

            // Begin setting up UI items
            int y = 0;
            int maxWidth = 53;
            int requiredHeight = 3;
            var installOptionsPicker = new Dialog("Choose installation options", buildAndInstallButton, cancelButton)
            {
                Height = 10,
                Width = 90
            };
            installOptionsPicker.Add(new Label($"Installer mode: {ManifestHandler.CurrentMode}")
            {
                X = 1,
                Y = y++,
                Width = Dim.Fill(),
                Height = 1
            });

            var currentInfo = target.GetInstalledALOTInfo();
            string displayStr = "Current game status: " + (currentInfo != null ? currentInfo.ToString() : "Textures not installed");
            installOptionsPicker.Add(new Label(displayStr)
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
                CheckBox cb = new CheckBox(getUIString(v, dataSource.ShownFiles))
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


            CheckBox reimportUnpackedFiles = new CheckBox("Optimize texture library")
            {
                X = 1,
                Y = y++,
                Width = Dim.Fill(),
                Height = 1,
                Checked = true
            };

            if (ManifestHandler.CurrentMode == ManifestMode.ALOT)
            {
                installOptionsPicker.Add(reimportUnpackedFiles);
            }
            else
            {
                y--; //Roll back up 1 Y
            }

#if DEBUG
            CheckBox debugNoInstallCb = new CheckBox("Debug: Skip main install block")
            {
                X = 1,
                Y = y++,
                Width = Dim.Fill(),
                Height = 1,
            };
            installOptionsPicker.Add(debugNoInstallCb);
#endif

            maxWidth = Math.Max(reimportUnpackedFiles.Text.Length + 4, maxWidth);

            y++;
            y++;
            installOptionsPicker.Add(new Label("Items marked with * cannot be toggled on or off")
            {
                X = 1,
                Y = y++,
                Width = Dim.Fill(),
                Height = 1,
            });

            installOptionsPicker.Height = y + requiredHeight;
            installOptionsPicker.Width = maxWidth;

            Application.Run(installOptionsPicker);

            if (buildAndInstall)
            {
                var optionsPackage = new InstallOptionsPackage()
                {
                    InstallTarget = target,
                    FilesToInstall = dataSource.ShownFiles,
                    InstallALOT = getInstallOptionValue(InstallOptionsStep.InstallOption.ALOT, installOptionMapping),
                    InstallALOTUpdate = getInstallOptionValue(InstallOptionsStep.InstallOption.ALOTUpdate, installOptionMapping),
                    InstallMEUITM = getInstallOptionValue(InstallOptionsStep.InstallOption.MEUITM, installOptionMapping),
                    InstallALOTAddon = getInstallOptionValue(InstallOptionsStep.InstallOption.ALOTAddon, installOptionMapping),
                    InstallUserfiles = getInstallOptionValue(InstallOptionsStep.InstallOption.UserFiles, installOptionMapping),
                    InstallerMode = ManifestHandler.CurrentMode,
                    RepackGameFiles = compressPackagesCb.Checked,
                    Limit2K = use2KLodsCb.Checked,
                    ImportNewlyUnpackedFiles = reimportUnpackedFiles.Checked,
#if DEBUG
                    DebugNoInstall = debugNoInstallCb.Checked
#endif

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
            Program.SwapToNewView(sui);
        }

        private void ChangeMode(ManifestMode newMode)
        {
            ManifestHandler.CurrentMode = newMode;
            SetLeftsideTitle();
            RefreshShownFiles();
        }

        private void SetLeftsideTitle()
        {
            leftsideListContainer.Title = $"Files to install (Mode: {ManifestHandler.CurrentMode})";
        }

        private void RefreshShownFiles()
        {
            TextureLibrary.ResetAllReadyStatuses(ManifestHandler.GetManifestFilesForMode(ManifestHandler.CurrentMode));
            var userFiles = dataSource.ShownFiles.Where(x => x is UserFile);
            dataSource.ShownFiles.Clear();
            dataSource.ShownFiles.AddRange(ManifestHandler.GetManifestFilesForMode(ManifestHandler.CurrentMode).Where(x => (x.ApplicableGames & VisibleGames) != 0));
            dataSource.ShownFiles.AddRange(userFiles);
            Application.Refresh();

            var selectedIndex = ManifestFilesListView.SelectedItem;
            updateDisplayedInfo(selectedIndex >= 0 && dataSource.Count > selectedIndex ? dataSource.ShownFiles[selectedIndex] : null);
        }


        private void SelectedListViewFileChanged(ListViewItemEventArgs obj)
        {
            updateDisplayedInfo(obj.Value);
        }

        private void updateDisplayedInfo(object obj)
        {
            selectedFileInfoFrameView.RemoveAll();
            int y = 0;
            if (obj is ManifestFile mf)
            {
                y = UpdateDisplayedManifestFile(mf, y);
            }

            if (obj is PreinstallMod pm)
            {
                y++;
                y = UpdateDisplayedPreinstallMod(pm, y);
            }
            if (obj is UserFile uf)
            {
                y = UpdateDisplayedUserFile(uf, y);
            }

            if (obj is InstallerFile ifx)
            {
                y = UpdateDisplayInstallerFile(ifx, ref y);
            }

            if (obj == null)
            {
                UpdateNoDisplayedFile(y);
            }
        }

        private int UpdateDisplayInstallerFile(InstallerFile ifx, ref int i)
        {
            if (ifx.Ready && (ifx is UserFile || ifx is ManifestFile mf && mf.Recommendation != RecommendationType.Required))

                selectedFileInfoFrameView.Add(new CheckBox("Don't install file")
                {
                    Width = "Don't install file".Length + 4,
                    Height = 1,
                    X = 0,
                    Y = Pos.Bottom(selectedFileInfoFrameView) - 4,
                    Checked = ifx.Disabled,
                    Toggled = (old) =>
                    {
                        ifx.Disabled = !old;
                        ManifestFilesListView.SetNeedsDisplay();
                    }
                });
            return i;
        }

        private int UpdateDisplayedPreinstallMod(PreinstallMod pm, int y)
        {
            AddSFIFVLabel("This mod will be installed before textures are installed", ref y);
            return y;
        }

        private void UpdateNoDisplayedFile(int y)
        {
            AddSFIFVLabel("Select a file", ref y);
        }


        private int UpdateDisplayedUserFile(UserFile uf, int y)
        {
            AddSFIFVLabel("User supplied file", ref y);
            y++;

            AddSFIFVLabel($"Applies to games: {uf.ApplicableGames}", ref y);
            AddSFIFVLabel($"Ready: {uf.Ready}", ref y);
            y++;

            AddSFIFVLabel($"Filename: {uf.Filename}", ref y);
            AddSFIFVLabel($"File size: {uf.FileSize} ({FileSizeFormatter.FormatSize(uf.FileSize)})", ref y);


            return y;
        }

        private void AddSFIFVLabel(string text, ref int y)
        {
            selectedFileInfoFrameView.Add(new Label(text)
            {
                Width = Dim.Fill(),
                Height = 1,
                Y = y++,
            });
        }

        private int UpdateDisplayedManifestFile(ManifestFile mf, int y)
        {
            AddSFIFVLabel("Manifest file", ref y);
            y++;

            AddSFIFVLabel($"Author: {mf.Author}", ref y);
            AddSFIFVLabel($"Applies to game(s): {mf.ApplicableGames}", ref y);
            if (mf.UnpackedSingleFilename != null && mf.UnpackedFileSize != 0 && mf.UnpackedFileMD5 != null)
            {

                AddSFIFVLabel($"Ready: {mf.Ready} via {(mf.IsBackedByUnpacked() ? "unpacked file" : "primary file")}", ref y);
            }
            else
            {
                AddSFIFVLabel($"Ready: {mf.Ready}", ref y);
            }

            AddSFIFVLabel($"Recommendation: {mf.RecommendationString}", ref y);
            AddSFIFVLabel("Recommendation reason:", ref y);
            AddSFIFVLabel(mf.RecommendationReason, ref y);

            y++;

            AddSFIFVLabel($"Filename: {mf.Filename}", ref y);
            AddSFIFVLabel($"File size: {mf.FileSize} ({FileSizeFormatter.FormatSize(mf.FileSize)})", ref y);
            AddSFIFVLabel($"File MD5: {mf.FileMD5}", ref y);
            if (mf.UnpackedSingleFilename != null && mf.UnpackedFileSize != 0 && mf.UnpackedFileMD5 != null)
            {
                y++;
                AddSFIFVLabel($"This file supports unpacked mode", ref y);
                AddSFIFVLabel($"Unpacked filename: {mf.UnpackedSingleFilename}", ref y);
                AddSFIFVLabel($"Unpacked size: {mf.UnpackedFileSize} ({FileSizeFormatter.FormatSize(mf.UnpackedFileSize)})", ref y);
                AddSFIFVLabel($"Unpacked file MD5: {mf.UnpackedFileMD5}", ref y);
            }

            var textForWebbutton = mf.Ready ? "Open mod web page" : "Download";
            selectedFileInfoFrameView.Add(new Button(textForWebbutton)
            {
                Width = textForWebbutton.Length + 4,
                Height = 1,
                X = Pos.Right(selectedFileInfoFrameView) - selectedFileInfoFrameView.X - textForWebbutton.Length - 6,
                Y = Pos.Bottom(selectedFileInfoFrameView) - 4,
                Clicked = DownloadClicked
            });

            return y;
        }


        private void ManifestFileReadyStatusChanged(ManifestFile mf)
        {
            Application.Refresh();
        }
        public override void BeginFlow()
        {
            TextureLibrary.SetupLibraryWatcher(ManifestHandler.GetManifestFilesForMode(ManifestHandler.CurrentMode).OfType<ManifestFile>().ToList(), ManifestFileReadyStatusChanged);
        }

        public override void SignalStopping()
        {
            // This window will close. We need to clear the ready status changed action so we don't keep reference to us
            TextureLibrary.UnregisterCallbacks();
        }

        internal class InstallerFileDataSource : IListDataSource
        {
            public List<InstallerFile> ShownFiles { get; set; }

            public bool IsMarked(int item) => false;

            public int Count => ShownFiles.Count;

            public InstallerFileDataSource(List<InstallerFile> itemList) => ShownFiles = itemList;

            public void Render(ListView container, ConsoleDriver driver, bool selected, int item, int col, int line, int width)
            {
                container.Move(col, line);

                InstallerFile instF = ShownFiles[item];
                if (instF.Ready)
                {
                    if (instF.Disabled)
                    {
                        driver.SetColors(ConsoleColor.Red, ConsoleColor.Blue);
                        RenderUstr(driver, "D", 1, 0, 2);
                    }
                    else if (instF is ManifestFile)
                    {
                        driver.SetColors(ConsoleColor.Green, ConsoleColor.Blue);
                        RenderUstr(driver, "*", 1, 0, 2);
                    }
                    else if (instF is UserFile)
                    {
                        driver.SetColors(ConsoleColor.Yellow, ConsoleColor.Blue);
                        RenderUstr(driver, "U", 1, 0, 2);
                    }
                }
                else
                {
                    RenderUstr(driver, " ", 1, 0, 2);
                }
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
                return ShownFiles;
            }
        }
    }
}
