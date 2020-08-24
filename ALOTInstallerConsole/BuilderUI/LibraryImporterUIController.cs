using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ALOTInstallerConsole.UserControls;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.Objects;
using ALOTInstallerCore.Objects.Manifest;
using NStack;
using Serilog;
using Terminal.Gui;

namespace ALOTInstallerConsole.BuilderUI
{
    public static class LibraryImporterController
    {

        public static void LoadUserFile()
        {
            OpenDialog selector = new OpenDialog("Select file",
                "Supported extensions: .7z, .rar, .zip, .dds, .mem, .tpf, .mod, .png, .tga")
            {
                CanChooseDirectories = false,
                CanChooseFiles = true,
                AllowedFileTypes = TextureLibrary.ImportableFileTypes,
                DirectoryPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) //Default to user profile cause idk if there is easy way to get downloads folder on linux
            };
            Application.Run(selector);
            Debug.WriteLine(selector.FilePath);
            if (!selector.Canceled && selector.FilePaths.Any() && File.Exists(selector.FilePaths.First()))
            {
                var selectedFile = selector.FilePaths.First();
                if (!ManifestHandler.MasterManifest.ManifestModePackageMappping[ManifestHandler.CurrentMode].UserFiles.Any(X => X.FullFilePath == selectedFile))
                {

                    ApplicableGame games = ApplicableGame.None;
                    List<string> paths = new List<string>();
                    if (Locations.ME1Target != null) paths.Add("ME1");
                    if (Locations.ME2Target != null) paths.Add("ME2");
                    if (Locations.ME3Target != null) paths.Add("ME3");
                    paths.Add("Abort");
                    var selectedIndex = MessageBox.Query("Select game", $"Select which game {Path.GetFileName(selectedFile)} applies to.", paths.Select(x => (ustring)x.ToString()).ToArray());
                    if (paths[selectedIndex] == "Abort" || selectedIndex < 0) return;
                    games = Enum.Parse<ApplicableGame>(paths[selectedIndex]);

                    UserFile uf = new UserFile()
                    {
                        AlotVersionInfo = new TextureModInstallationInfo(0, 0, 0, 0),
                        ApplicableGames = games,
                        FullFilePath = selector.FilePath.ToString(),
                        FriendlyName = Path.GetFileNameWithoutExtension(selectedFile),
                        Filename = Path.GetFileName(selectedFile),
                        FileSize = new FileInfo(selectedFile).Length
                    };
                    ManifestHandler.MasterManifest.ManifestModePackageMappping[ManifestHandler.CurrentMode].UserFiles.Add(uf);
                }
                else
                {
                    MessageBox.Query("Already added", "This user file is already loaded for install.", "OK");
                }

            }
        }

        public static void ImportManifestFilesFromFolder()
        {
            if (ManifestHandler.MasterManifest != null)
            {
                if (ManifestHandler.MasterManifest.ManifestModePackageMappping.TryGetValue(ManifestMode.ALOT, out var manifestP))
                {
                    OpenDialog selector = new OpenDialog("Select location to import files from", "Select a folder containing manifest files, such as your downloads folder.")
                    {
                        CanChooseDirectories = true,
                        CanChooseFiles = false,
                        DirectoryPath =
                            Environment.GetFolderPath(Environment.SpecialFolder
                                .UserProfile) //Default to user profile cause idk if there is easy way to get downloads folder on linux
                    };
                    Application.Run(selector);
                    Debug.WriteLine(selector.FilePath);
                    if (!selector.Canceled && selector.FilePath != null &&
                        Directory.Exists(selector.FilePath.ToString()))
                    {
                        ProgressDialog pd = new ProgressDialog("Importing files from folder",
                            "Please wait while files are imported.", "Preparing to import files...", true);
                        NamedBackgroundWorker nbw = new NamedBackgroundWorker("ImportFromFolderThread");
                        nbw.DoWork += (a, b) =>
                        {
                            TextureLibrary.ImportFromFolder(selector.FilePath.ToString(),
                                manifestP.ManifestFiles.OfType<ManifestFile>().ToList(),
                                (uiString, d, t) =>
                                {
                                    Application.MainLoop.Invoke(() =>
                                    {
                                        pd.BottomMessage = $"Importing {uiString}";
                                        pd.ProgressMax = t;
                                        pd.ProgressValue = d;
                                    });
                                },
                                imported =>
                                {
                                    Application.MainLoop.Invoke(() =>
                                    {
                                        if (pd.IsCurrentTop)
                                        {
                                            // Close progress dialog.
                                            // Due to how drawing is done, it will still be visible, sadly.
                                            Application.RequestStop();
                                        }

                                        if (imported.Any())
                                        {
                                            var importedItems =
                                                "\n\n" + string.Join("\n  ", imported.Select(x => x.FriendlyName));
                                            MessageBox.Query("Files imported",
                                                $"The following manifest files were imported to the texture library:{importedItems}",
                                                "OK");
                                            // Refresh library?
                                        }
                                        else
                                        {
                                            MessageBox.Query("No files imported",
                                                $"No manifest files were found in the specified directory (that were not already imported):\n{selector.FilePath}",
                                                "OK");
                                        }
                                    });
                                }
                            );
                        };
                        nbw.RunWorkerAsync();
                        Application.Run(pd); //can this be run from background thread?
                    }
                }
                else
                {
                    Log.Error("Cannot import files: Manifest files are not loaded");
                    MessageBox.Query("Cannot import files",
                        "The ALOT manifest has not been loaded. Manifest files cannot be imported.", "OK");
                }
            }
        }
    }
}
