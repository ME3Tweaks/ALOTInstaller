using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
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

            Button installButton = new Button("Install ALOT")
            {
                X = Pos.Right(this) - 19,
                Y = Pos.Bottom(this) - 3,
                Height = 1,
                Width = 16
            };
            Add(installButton);
        }

        private void Settings_Clicked()
        {
            SettingsUIController sui = new SettingsUIController();
            sui.SetupUI();
            Program.SwapToNewView(sui);
        }

        private void ChangeMode_Clicked()
        {
            var str = Program.ManifestModes.Keys.Select(x=>(ustring) x.ToString()).ToArray();
            var n = MessageBox.Query(50, 7, "Mode selector", "Select a mode for ALOT Installer.",str);
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
            ReadyTextBlock.Text = "Ready: " + mf.Ready.ToString();
            AuthorTextBlock.Text = "Author: " + mf.Author;
            FilenameTextBlock.Text = "Filename: " + mf.Filename;
            ExpectedFileSizeHashTextBlock.Text = "File MD5: " + mf.FileMD5;
            ExpectedFileSizeTextBlock.Text = "File size: " + mf.FileSize;

            // Unpacked
            UnpackedFilenameTextBlock.Text = "Unpacked filename: " + (mf.UnpackedSingleFilename != null ? mf.UnpackedSingleFilename : "N/A");
            UnpackedFilesizeTextBlock.Text = "Unpacked file size: " + (mf.UnpackedFileSize != null ? mf.UnpackedFileSize.ToString() : "N/A");
            UnpackedFileHashTextBlock.Text = "Unpacked file MD5: " + (mf.UnpackedFileMD5 != null ? mf.UnpackedFileMD5 : "N/A");
        }

        public Label AuthorTextBlock { get; set; }

        public override void BeginFlow()
        {
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
                // Equivalent to an interpolated string like $"{Scenarios[item].Name, -widtestname}"; if such a thing were possible
                RenderUstr(driver, instF.Ready ? "✓" : "X", 1, 0, 2);
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
