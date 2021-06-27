using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.Helpers.AppSettings;
using ALOTInstallerCore.ModManager.Objects;
using ALOTInstallerCore.Objects;
using ALOTInstallerCore;
using ME3ExplorerCore.Gammtek.Extensions;
using ME3ExplorerCore.Packages;
using Terminal.Gui;

namespace ALOTInstallerConsole.BuilderUI
{
    public class SettingsUIController : UIController
    {
        private TextField me1PathField;
        private TextField me2PathField;
        private TextField me3PathField;
        private TextField me1ConfigPathField;
        private TextField me2ConfigPathField;
        private TextField me3ConfigPathField;
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
            var button = new Button("Change")
            {
                X = 53,
                Y = y,
                Width = 10,
                Height = 1,
            };
            button.Clicked += () => changeGamePath(MEGame.ME1);
            gamePathsFv.Add(button);
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
            button = new Button("Change")
            {
                X = 53,
                Y = y,
                Width = 10,
                Height = 1,
            };
            button.Clicked += () => changeGamePath(MEGame.ME2);
            gamePathsFv.Add(button); ;
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
            button = new Button("Change")
            {
                X = 53,
                Y = y,
                Width = 10,
                Height = 1,
            };

            button.Clicked += () => changeGamePath(MEGame.ME3);
            gamePathsFv.Add(button);

            Add(gamePathsFv);

            FrameView gameConfigPathsFv = new FrameView("Config file paths")
            {
                X = 1,
                Y = 10,
                Width = 67,
                Height = 10
            };
            y = 0;
            // ME1 Path
            gameConfigPathsFv.Add(new Label("Mass Effect 1 config path")
            {
                X = 2,
                Y = y++,
                Width = 25,
                Height = 1
            });
            me1ConfigPathField = new TextField(Locations.ConfigPathME1 ?? "")
            {
                X = 2,
                Y = y,
                Width = 50,
                Height = 1,
                ReadOnly = true
            };
            gameConfigPathsFv.Add(me1ConfigPathField);
#if !WINDOWS
            button = new Button("Change")
            {
                X = 53,
                Y = y,
                Width = 10,
                Height = 1,
            };
            button.Clicked += () => changeConfigPath(MEGame.ME1);
            gameConfigPathsFv.Add(button);
#endif
            y++;

            // ME2 Path
            y++;
            gameConfigPathsFv.Add(new Label("Mass Effect 2 config path")
            {
                X = 2,
                Y = y++,
                Width = 25,
                Height = 1
            });
            me2ConfigPathField = new TextField(Locations.ConfigPathME2 ?? "")
            {
                X = 2,
                Y = y,
                Width = 50,
                Height = 1,
                ReadOnly = true
            };

            gameConfigPathsFv.Add(me2ConfigPathField);
#if !WINDOWS
            button = new Button("Change")
            {
                X = 53,
                Y = y,
                Width = 10,
                Height = 1,
            };
            button.Clicked += () => changeConfigPath(MEGame.ME2);
            gameConfigPathsFv.Add(button);
#endif
            y++;

            // ME3 Path
            y++;
            gameConfigPathsFv.Add(new Label("Mass Effect 3 config path")
            {
                X = 2,
                Y = y++,
                Width = 25,
                Height = 1
            });

            me3ConfigPathField = new TextField(Locations.ConfigPathME3 ?? "")
            {
                X = 2,
                Y = y,
                Width = 50,
                Height = 1,
                ReadOnly = true
            };
            gameConfigPathsFv.Add(me3ConfigPathField);
#if !WINDOWS
            button = new Button("Change")
            {
                X = 53,
                Y = y,
                Width = 10,
                Height = 1,
            };
            button.Clicked += () => changeConfigPath(MEGame.ME3);
            gameConfigPathsFv.Add(button);
#endif

            Add(gameConfigPathsFv);


            // Texture library location
            FrameView fileLocationsFv = new FrameView("Disk locations")
            {
                X = 1,
                Y = 20,
                Width = 67,
                Height = 7
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
            button = new Button("Change")
            {
                X = 53,
                Y = y,
                Width = 10,
                Height = 1,
            };
            button.Clicked += ChangeTextureLibraryLocation;
            fileLocationsFv.Add(button);
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
            buildLocation = new TextField(Settings.StagingLocation)
            {
                X = 2,
                Y = y,
                Width = 50,
                Height = 1,
                ReadOnly = true
            };
            fileLocationsFv.Add(buildLocation);

            button = new Button("Change")
            {
                X = 53,
                Y = y,
                Width = 10,
                Height = 1,
            };
            button.Clicked += ChangeBuildLocation;
            fileLocationsFv.Add(button);

            Add(fileLocationsFv);
            y++;
            y++;

            Button close = new Button("Close")
            {
                X = Pos.Right(this) - 12,
                Y = Pos.Bottom(this) - 3,
                Height = 1,
            };
            close.Clicked += Close_Clicked;
            Add(close);
        }

        private void ChangeBuildLocation()
        {
            OpenDialog selector = new OpenDialog("Select location to build textures for installation", "Select the location you would like to build the installation package at. This will take up considerable space depending on what will be installed.")
            {
                CanChooseDirectories = true,
                CanChooseFiles = false,
                DirectoryPath = Directory.Exists(Settings.StagingLocation) ? Settings.StagingLocation : null
            };
            Application.Run(selector);
            if (!selector.Canceled && selector.FilePath != null && Directory.Exists(selector.FilePath.ToString()))
            {
                buildLocation.Text = Settings.StagingLocation = selector.FilePath.ToString();
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
                textureLibraryLocation.Text = Settings.TextureLibraryLocation = selector.FilePath.ToString();
                Settings.Save();
                TextureLibrary.StopLibraryWatcher(); //This will be reloaded when we return to the manifest controller
            }
        }

#if !WINDOWS
        private async void changeConfigPath(MEGame game)
        {
            OpenDialog selector = new OpenDialog($"Select config directory for {game}",
                "Select the directory where the game configuration files are stored.")
            {
                CanChooseDirectories = true,
                CanChooseFiles = false,
            };
            Application.Run(selector);
            if (!selector.Canceled && selector.FilePath != null &&
                Directory.Exists(selector.FilePath.ToString()))
            {
                var selectedPath = selector.FilePath.ToString();
                var pathSet = await Task.Run(() => Locations.SetConfigPath(game, selectedPath, true));

                if (!pathSet)
                {
                    MessageBox.ErrorQuery("Error setting game config path",
                        "An error occurred setting the game config path. See the log for more details.", "OK");
                }
                else
                {
                    switch (game)
                    {
                        case MEGame.ME1:
                            me1ConfigPathField.Text = selectedPath;
                            break;
                        case MEGame.ME2:
                            me2ConfigPathField.Text = selectedPath;
                            break;
                        case MEGame.ME3:
                            me3ConfigPathField.Text = selectedPath;
                            break;

                    }
                }
            }
        }
#endif
        private async void changeGamePath(MEGame game)
        {
            var gameexename = game.ToGameName().Replace(" ", "");
            OpenDialog selector = new OpenDialog($"Select {gameexename}.exe", $"Select the executable for {game.ToGameName()}, located in the Binaries directory.")
            {
                CanChooseDirectories = false,
                AllowedFileTypes = new[] { $"{gameexename}.exe" },
            };
            Application.Run(selector);
            if (!selector.Canceled && selector.FilePaths.Any() && File.Exists(selector.FilePaths.First()))
            {
                var targetPath = Utilities.GetGamePathFromExe(game, selector.FilePaths.First());
                var target = new GameTarget(game, targetPath, false, false);
                var invalidReason = target.ValidateTarget();
                if (invalidReason == null)
                {
                    var targetSet = await Task.Run(() => Locations.SetTarget(target));

                    if (!targetSet)
                    {
                        MessageBox.ErrorQuery("Error setting game path", "An error occurred setting the game path. See the log for more details.", "OK");
                    }
                    else
                    {
                        switch (game)
                        {
                            case MEGame.ME1:
                                me1PathField.Text = targetPath;
                                break;
                            case MEGame.ME2:
                                me2PathField.Text = targetPath;
                                break;
                            case MEGame.ME3:
                                me3PathField.Text = targetPath;
                                break;

                        }
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
