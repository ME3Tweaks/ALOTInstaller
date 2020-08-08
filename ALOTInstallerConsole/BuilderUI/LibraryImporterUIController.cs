using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ALOTInstallerConsole.UserControls;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.Objects.Manifest;
using ALOTInstallerCore.Startup;
using Serilog;
using Terminal.Gui;

namespace ALOTInstallerConsole.BuilderUI
{
    public class LibraryImporterUIController : UIController
    {
        public override void SetupUI()
        {
            Title = "Texture library importer";
            Add(new Button("Import from folder")
            {
                X = Pos.Left(this) + 2,
                Y = Pos.Bottom(this) - 3,
                Height = 1,
                Clicked = ImportFromFolder_Clicked
            });

            Add(new Button("Close")
            {
                X = Pos.Right(this) - 12,
                Y = Pos.Bottom(this) - 3,
                Height = 1,
                Clicked = Close_Clicked
            });
        }

        private void ImportFromFolder_Clicked()
        {
            if (Program.ManifestModes.TryGetValue(OnlineContent.ManifestMode.ALOT, out var manifestP))
            {
                OpenDialog selector = new OpenDialog("Select location to import files from",
                    "Select a folder containing manifest files, such as your downloads folder.\nSelect Open once you are in the folder you wish to choose.")
                {
                    CanChooseDirectories = true,
                    CanChooseFiles = false,
                    DirectoryPath =
                        Environment.GetFolderPath(Environment.SpecialFolder
                            .UserProfile) //Default to user profile cause idk if there is easy way to get downloads folder on linux
                };
                Application.Run(selector);
                Debug.WriteLine(selector.FilePath);
                if (!selector.Canceled && selector.FilePath != null && Directory.Exists(selector.FilePath.ToString()))
                {
                    ProgressDialog pd = new ProgressDialog("Importing files from folder", "Please wait while files are imported.");
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

        private void Close_Clicked()
        {
            FileSelectionUIController bui = new FileSelectionUIController();
            bui.SetupUI();
            Program.SwapToNewView(bui);
        }

        public override void BeginFlow()
        {

        }

        public override void SignalStopping()
        {
        }
    }
}
