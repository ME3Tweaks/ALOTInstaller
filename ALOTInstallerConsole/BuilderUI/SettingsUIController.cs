using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.Startup;
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
            Title = "Settings";

            int y = 1;
            // ME1 Path
            Add(new Label("Mass Effect 1 game path")
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
            Add(me1PathField);
            Add(new Button("Change")
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
            Add(new Label("Mass Effect 2 game path")
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
            Add(me2PathField);
            Add(new Button("Change")
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
            Add(new Label("Mass Effect 3 game path")
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
            Add(me3PathField);
            Add(new Button("Change")
            {
                X = 53,
                Y = y,
                Width = 10,
                Height = 1,
                Clicked = ChangeME3Path
            });
            y++;

            // Texture library location
            y++;
            Add(new Label("Texture library directory (ALOT mode only)")
            {
                X = 2,
                Y = y++,
                Width = 42,
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
            Add(textureLibraryLocation);
            Add(new Button("Change")
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
            Add(new Label("Staging directory (where textures are prepared for install)")
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
            Add(buildLocation);
            Add(new Button("Change")
            {
                X = 53,
                Y = y,
                Width = 10,
                Height = 1,
                Clicked = ChangeBuildLocation
            });
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
            throw new NotImplementedException();
        }

        private void ChangeTextureLibraryLocation()
        {
            throw new NotImplementedException();
        }

        private void ChangeME1Path()
        {
            throw new NotImplementedException();
        }

        private void ChangeME2Path()
        {
            throw new NotImplementedException();
        }

        private void ChangeME3Path()
        {
            OpenDialog selector = new OpenDialog("Select MassEffect3.exe", "Select the executable for Mass Effect 3, located in binaries/win32.")
            {
                CanChooseDirectories = false,
                AllowedFileTypes = new[] {".exe"},
                
            };
            Application.Run(selector);
            if (selector.FilePaths.Any())
            {
                UITools.SetText(me3PathField, selector.FilePaths.First());

                // COMMIT CHANGE HERE
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
    }
}
