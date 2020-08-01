using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using ALOTInstallerConsole.InstallerUI;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.ModManager.Objects.MassEffectModManagerCore.modmanager.objects;
using ALOTInstallerCore.Objects;
using ALOTInstallerCore.Startup;
using NStack;
using Terminal.Gui;

namespace ALOTInstallerConsole.BuilderUI
{
    public class FileSelectionUIController : UIController
    {
        private InstallerFileDataSource dataSource;


        public override void SetupUI()
        {
            Title = "ALOT Installer";
            List<InstallerFile> ifS = new List<InstallerFile>();

            if (Program.CurrentManifestPackage != null)
            {
                Title += $" Manifest version {Program.CurrentManifestPackage.ManifestVersion}";
                ifS.AddRange(Program.CurrentManifestPackage.ManifestFiles);
            }
            dataSource = new InstallerFileDataSource(ifS);
            var ManifestFilesList = new ListView(dataSource)
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
            fv.Add(ManifestFilesList);
            Add(fv);

            // RIGHT SIDE
            fv = new FrameView("Selected file information")
            {
                Width = Dim.Fill(),
                Height = Dim.Fill() - 1,
                X = 50,
                Y = 0
            };

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

            Add(fv);

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

            Button installButton = new Button("Install")
            {
                X = Pos.Right(this) - 14,
                Y = Pos.Bottom(this) - 3,
                Height = 1,
                Width = 11,
                Clicked = InstallButton_Click
            };
            Add(installButton);
        }

        private void InstallButton_Click()
        {
            // Perform precheck here
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
            int warningResponse = MessageBox.Query("Warning", "Once you install texture mods, you will not be able to further install mods that contain .pcc, .u, .upk or .sfm files without breaking textures in the game. Please make sure you have all of your DLC and non-texture mods installed now, as you will not be able to safely install them later.", "OK", "Abort install");
            if (warningResponse != 0) return; //abort

            // Show options
            if (Program.CurrentManifestPackage.ManifestFiles.Any())
            {
                bool buildAndInstall = false;
                var buildAndInstallButton = new Button("Build & Install")
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

                int y = 0;
                CheckBox alotCheckbox = new CheckBox("ALOT")
                {
                    X = 1,
                    Y = y++,
                    Width = 30,
                    Height = 1
                };
                CheckBox alotUpdate = new CheckBox("ALOT Update")
                {
                    X = 1,
                    Y = y++,
                    Width = 30,
                    Height = 1
                };
                CheckBox meuitmCheckbox = new CheckBox("MEUITM")
                {
                    X = 1,
                    Y = y++,
                    Width = 30,
                    Height = 1
                };
                CheckBox addonCheckBox = new CheckBox("ALOT Addon")
                {
                    X = 1,
                    Y = y++,
                    Width = 30,
                    Height = 1
                };
                CheckBox userFilesCheckBox = new CheckBox("User files")
                {
                    X = 1,
                    Y = y++,
                    Width = 30,
                    Height = 1
                };
                var whatToBuildDialog = new Dialog("Choose items to install", buildAndInstallButton, cancelButton)
                {
                    Height = 10,
                    Width = 40
                };

                whatToBuildDialog.Add(alotCheckbox);
                whatToBuildDialog.Add(alotUpdate);
                if (target.Game == Enums.MEGame.ME1)
                {
                    whatToBuildDialog.Add(meuitmCheckbox);
                }
                whatToBuildDialog.Add(addonCheckBox);
                whatToBuildDialog.Add(userFilesCheckBox);
                Application.Run(whatToBuildDialog);

                if (buildAndInstall)
                {
                    //var builderUI = new BuilderUI.BuilderUIController();
                    //builderUI.SetOptionsPackage(new InstallOptionsPackage()
                    //{
                    //    InstallTarget = target,
                    //    AllInstallerFiles = dataSource.InstallerFiles,
                    //    InstallALOT = alotCheckbox.Checked,
                    //    InstallALOTUpdate = alotCheckbox.Checked,
                    //    InstallMEUITM = meuitmCheckbox.Checked,
                    //    InstallALOTAddon = addonCheckBox.Checked,
                    //    InstallUserfiles = userFilesCheckBox.Checked
                    //});
                    //builderUI.SetupUI();
                    //Program.SwapToNewView(builderUI);

                    var installerUI = new InstallerUIController();
                    installerUI.SetInstallPackage(new InstallOptionsPackage()
                    {
                        InstallTarget = target,
                        AllInstallerFiles = dataSource.InstallerFiles,
                        InstallALOT = alotCheckbox.Checked,
                        InstallALOTUpdate = alotCheckbox.Checked,
                        InstallMEUITM = meuitmCheckbox.Checked,
                        InstallALOTAddon = addonCheckBox.Checked,
                        InstallUserfiles = userFilesCheckBox.Checked
                    });
                    installerUI.SetupUI();
                    Program.SwapToNewView(installerUI);
                }
            }
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
            var n = MessageBox.Query(50, 7, "Mode selector", "Select a mode for ALOT Installer.", str);
            Program.CurrentManifestPackage = Program.ManifestModes[Program.ManifestModes.Keys.ToList()[n]];
            dataSource.InstallerFiles.Clear();
            dataSource.InstallerFiles.AddRange(Program.CurrentManifestPackage.ManifestFiles);
            Application.Refresh();
        }

        public Label FilenameTextBlock { get; set; }
        public Label UnpackedFileHashTextBlock { get; set; }
        public Label UnpackedFilesizeTextBlock { get; set; }
        public Label UnpackedFilenameTextBlock { get; set; }
        public Label ReadyTextBlock { get; set; }
        public Label ExpectedFileSizeHashTextBlock { get; set; }
        public Label ExpectedFileSizeTextBlock { get; set; }

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
            ExpectedFileSizeTextBlock.Text = "File size: " + mf.FileSize;

            // Unpacked
            UnpackedFilenameTextBlock.Text = "Unpacked filename: " + (mf.UnpackedSingleFilename != null ? mf.UnpackedSingleFilename : "N/A");
            UnpackedFilesizeTextBlock.Text = "Unpacked file size: " + (mf.UnpackedFileSize > 0 ? mf.UnpackedFileSize.ToString() : "N/A");
            UnpackedFileHashTextBlock.Text = "Unpacked file MD5: " + (mf.UnpackedFileMD5 != null ? mf.UnpackedFileMD5 : "N/A");
        }

        public Label AuthorTextBlock { get; set; }
        public Label AppliesToGamesTextBlock { get; private set; }

        public override void BeginFlow()
        {
            // Disabled for now.
            //TextureLibrary.SetupLibraryWatcher();
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
