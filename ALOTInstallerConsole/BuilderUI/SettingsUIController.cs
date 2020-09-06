using System.IO;
using System.Linq;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.Helpers.AppSettings;
using ALOTInstallerCore.ModManager.Objects;
using ALOTInstallerCore.Objects;
using Terminal.Gui;

namespace ALOTInstallerConsole.BuilderUI
{
    public class SettingsUIController : UIController
    {
        private TextField me1PathField;
        private TextField me2PathField;
        private TextField me3PathField;
        private TextField textureLibraryLocation;
        private TextField buildLocation;

        public override void SetupUI()
        {
            //Title = "Settings";

            FrameView gamePathsFv = new FrameView("Game paths")
            {
                X = 1,
                Y = 0,
                Width = 67,
                Height = 10
            };
            int y = 0;
            // ME1 Path
            gamePathsFv.Add(new Label("Mass Effect 1 game path")
            {
                X = 2,
                Y = y++,
                Width = 25,
                Height = 1
            });
            me1PathField = new TextField(Locations.ME1Target?.TargetPath ?? "")
            {
                X = 2,
                Y = y,
                Width = 50,
                Height = 1,
                ReadOnly = true
            };
            gamePathsFv.Add(me1PathField);
            gamePathsFv.Add(new Button("Change")
            {
                X = 53,
                Y = y,
                Width = 10,
                Height = 1,
                Clicked = ChangeME1Path
            });
            y++;

            // ME2 Path
            y++;
            gamePathsFv.Add(new Label("Mass Effect 2 game path")
            {
                X = 2,
                Y = y++,
                Width = 25,
                Height = 1
            });
            me2PathField = new TextField(Locations.ME2Target?.TargetPath ?? "")
            {
                X = 2,
                Y = y,
                Width = 50,
                Height = 1,
                ReadOnly = true
            };
            gamePathsFv.Add(me2PathField);
            gamePathsFv.Add(new Button("Change")
            {
                X = 53,
                Y = y,
                Width = 10,
                Height = 1,
                Clicked = ChangeME2Path
            });
            y++;

            // ME3 Path
            y++;
            gamePathsFv.Add(new Label("Mass Effect 3 game path")
            {
                X = 2,
                Y = y++,
                Width = 25,
                Height = 1
            });

            me3PathField = new TextField(Locations.ME3Target?.TargetPath ?? "")
            {
                X = 2,
                Y = y,
                Width = 50,
                Height = 1,
                ReadOnly = true
            };
            gamePathsFv.Add(me3PathField);
            gamePathsFv.Add(new Button("Change")
            {
                X = 53,
                Y = y,
                Width = 10,
                Height = 1,
                Clicked = ChangeME3Path
            });

            Add(gamePathsFv);


            // Texture library location
            FrameView fileLocationsFv = new FrameView("Disk locations")
            {
                X = 1,
                Y = 10,
                Width = 67,
                Height = 9
            };
            y = 0;
            fileLocationsFv.Add(new Label("Texture library directory (ALOT/MEUITM mode only)")
            {
                X = 2,
                Y = y++,
                Width = 46,
                Height = 1
            });
            textureLibraryLocation = new TextField(Settings.TextureLibraryLocation)
            {
                X = 2,
                Y = y,
                Width = 50,
                Height = 1,
                ReadOnly = true
            };
            fileLocationsFv.Add(textureLibraryLocation);
            fileLocationsFv.Add(new Button("Change")
            {
                X = 53,
                Y = y,
                Width = 10,
                Height = 1,
                Clicked = ChangeTextureLibraryLocation
            });
            y++;

            // Build Location
            y++;
            fileLocationsFv.Add(new Label("Staging directory (where textures are prepared for install)")
            {
                X = 2,
                Y = y++,
                Width = 65,
                Height = 1
            });
            buildLocation = new TextField(Settings.BuildLocation)
            {
                X = 2,
                Y = y,
                Width = 50,
                Height = 1,
                ReadOnly = true
            };
            fileLocationsFv.Add(buildLocation);
            fileLocationsFv.Add(new Button("Change")
            {
                X = 53,
                Y = y,
                Width = 10,
                Height = 1,
                Clicked = ChangeBuildLocation
            });

            y++;
            y++;

            Button close = new Button("Close")
            {
                X = Pos.Right(this) - 12,
                Y = Pos.Bottom(this) - 3,
                Height = 1,
                Clicked = Close_Clicked
            };
            Add(close);
        }

        private void ChangeBuildLocation()
        {
            OpenDialog selector = new OpenDialog("Select location to build textures for installation", "Select the location you would like to build the installation package at. This will take up considerable space depending on what will be installed.")
            {
                CanChooseDirectories = true,
                CanChooseFiles = false,
                DirectoryPath = Directory.Exists(Settings.BuildLocation) ? Settings.BuildLocation : null
            };
            Application.Run(selector);
            if (!selector.Canceled && selector.FilePath != null && Directory.Exists(selector.FilePath.ToString()))
            {
                buildLocation.Text = Settings.BuildLocation = selector.FilePaths.First();
            }
        }

        private void ChangeTextureLibraryLocation()
        {
            OpenDialog selector = new OpenDialog("Select location to store textures for installation", "Select the location you would like to store the texture files that are part of the ALOT manifest. This location is where textures are stored, not where they are installed.")
            {
                CanChooseDirectories = true,
                CanChooseFiles = false,
                DirectoryPath = Directory.Exists(Settings.TextureLibraryLocation) ? Settings.TextureLibraryLocation : null
            };
            Application.Run(selector);
            if (!selector.Canceled && selector.FilePath != null && Directory.Exists(selector.FilePath.ToString()))
            {
                textureLibraryLocation.Text = Settings.TextureLibraryLocation = selector.FilePaths.First();
                Settings.Save();
                TextureLibrary.StopLibraryWatcher(); //This will be reloaded when we return to the manifest controller
            }
        }

        private void ChangeME1Path()
        {
            OpenDialog selector = new OpenDialog("Select MassEffect.exe", "Select the executable for Mass Effect, located in the Binaries directory.")
            {
                CanChooseDirectories = false,
                AllowedFileTypes = new[] { ".exe" },
            };
            Application.Run(selector);
            if (!selector.Canceled && selector.FilePaths.Any() && File.Exists(selector.FilePaths.First()))
            {
                var target = new GameTarget(Enums.MEGame.ME1, selector.FilePaths.First(), false, false);
                var invalidReason = target.ValidateTarget();
                if (invalidReason == null)
                {
                    if (!Locations.SetTarget(target))
                    {
                        MessageBox.ErrorQuery("Error setting game path", "An error occurred setting the game path. See the log for more details.", "OK");
                    }
                    else
                    {
                        UITools.SetText(me1PathField, selector.FilePaths.First());
                    }
                }
                else
                {
                    MessageBox.ErrorQuery("Invalid target selected", invalidReason, "OK");
                }
            }
        }

        private void ChangeME2Path()
        {
            OpenDialog selector = new OpenDialog("Select MassEffect2.exe", "Select the executable for Mass Effect 2, located in the Binaries directory.")
            {
                CanChooseDirectories = false,
                AllowedFileTypes = new[] { ".exe" },
            };
            Application.Run(selector);
            if (!selector.Canceled && selector.FilePaths.Any() && File.Exists(selector.FilePaths.First()))
            {
                var target = new GameTarget(Enums.MEGame.ME2, selector.FilePaths.First(), false, false);
                var invalidReason = target.ValidateTarget();
                if (invalidReason == null)
                {
                    if (!Locations.SetTarget(target))
                    {
                        MessageBox.ErrorQuery("Error setting game path", "An error occurred setting the game path. See the log for more details.", "OK");
                    }
                    else
                    {
                        UITools.SetText(me2PathField, selector.FilePaths.First());
                    }
                }
                else
                {
                    MessageBox.ErrorQuery("Invalid target selected", invalidReason, "OK");
                }
            }
        }

        private void ChangeME3Path()
        {
            OpenDialog selector = new OpenDialog("Select MassEffect3.exe", "Select the executable for Mass Effect 3, located in binaries/win32.")
            {
                CanChooseDirectories = false,
                AllowedFileTypes = new[] { ".exe" },
            };
            Application.Run(selector);
            if (!selector.Canceled && selector.FilePaths.Any() && File.Exists(selector.FilePaths.First()))
            {
                var target = new GameTarget(Enums.MEGame.ME3, selector.FilePaths.First(), false, false);
                var invalidReason = target.ValidateTarget();
                if (invalidReason == null)
                {
                    if (!Locations.SetTarget(target))
                    {
                        MessageBox.ErrorQuery("Error setting game path", "An error occurred setting the game path. See the log for more details.", "OK");
                    }
                    else
                    {
                        UITools.SetText(me3PathField, selector.FilePaths.First());
                    }
                }
                else
                {
                    MessageBox.ErrorQuery("Invalid target selected", invalidReason, "OK");
                }

            }
        }

        private void Close_Clicked()
        {
            FileSelectionUIController bui = new FileSelectionUIController();
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
