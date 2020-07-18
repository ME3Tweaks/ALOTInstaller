using System;
using System.Collections.Generic;
using System.Text;
using ALOTInstallerCore.Objects;
using ALOTInstallerCore.Startup;
using Terminal.Gui;

namespace ALOTInstallerConsole.BuilderUI
{
    public class BuilderUIController : UIController
    {
        private OnlineContent.ManifestPackage manifestPackage;
        private ListView ManifestFilesList;


        public override void SetupUI()
        {
            Title = $"ALOT Installer - Manifest version {manifestPackage.ManifestVersion}";
            ManifestFilesList = new ListView(manifestPackage.ManifestFiles)
            {
                Width = 40,
                Height = Dim.Fill(),
                X = 0,
                Y = 0,
                SelectedItemChanged = SelectedManifestFileChanged
            };
            var fv = new FrameView("ALOT manifest files")
            {
                Width = 42,
                Height = Dim.Fill(),
                X = 0,
                Y = 0,
            };
            fv.Add(ManifestFilesList);
            Add(fv);

            fv = new FrameView("Selected file information")
            {
                Width = Dim.Fill(),
                Height = Dim.Fill() - 1,
                X = 42,
                Y = 0
            };
            AuthorTextBlock = new Label("Select a file")
            {
                Width = Dim.Fill(),
                Height = 1,
                Y = 1
            };
            fv.Add(AuthorTextBlock);

            ExpectedFileSizeTextBlock = new Label("")
            {
                Width = Dim.Fill(),
                Height = 1,
                Y = 2
            };
            fv.Add(ExpectedFileSizeTextBlock);

            ExpectedFileSizeHash = new Label("")
            {
                Width = Dim.Fill(),
                Height = 1,
                Y = 3
            };
            fv.Add(ExpectedFileSizeHash);

            Add(fv);

            Button installButton = new Button("Install ALOT")
            {
                X = Pos.Right(this) - 20,
                Y = Pos.Bottom(this) - 3,
                Height = 1,
                Width = 17
            };
            Add(installButton);

            //// Creates a menubar, the item "New" has a help menu.
            //var menu = new MenuBar(new MenuBarItem[] {
            //    new MenuBarItem ("_File", new MenuItem [] {
            //        new MenuItem ("_New", "Creates new file", ()=> {}),
            //        new MenuItem ("_Close", "", () => {}),
            //        new MenuItem ("_Quit", "", () => { Application.RequestStop(); })
            //    }),
            //    new MenuBarItem ("_Edit", new MenuItem [] {
            //        new MenuItem ("_Copy", "", null),
            //        new MenuItem ("C_ut", "", null),
            //        new MenuItem ("_Paste", "", null)
            //    })
            //});
            //top.Add(menu);

            // Add some controls
            //Add(
            //    new Label(3, 2, "Login: "),
            //    new TextField(14, 2, 40, ""),
            //    new Label(3, 4, "Password: "),
            //    new TextField(14, 4, 40, "") { },
            //    new CheckBox(3, 6, "Remember me"),
            //    new Button(3, 14, "Ok"),
            //    new Button(10, 14, "Cancel"),
            //    new Label(3, 18, "Press ESC and 9 to activate the menubar"));
        }

        public Label ExpectedFileSizeHash { get; set; }
        public Label ExpectedFileSizeTextBlock { get; set; }

        private void SelectedManifestFileChanged(ListViewItemEventArgs obj)
        {
            if (obj.Value is ManifestFile mf)
            {
                AuthorTextBlock.Text = "Author: " + mf.Author;
                ExpectedFileSizeHash.Text = "File MD5: " + mf.FileMD5;
                ExpectedFileSizeTextBlock.Text = "File size: " + mf.FileSize;
            }
        }

        public Label AuthorTextBlock { get; set; }

        public override void BeginFlow()
        {
        }

        public void SetManifestPackage(OnlineContent.ManifestPackage eResult)
        {
            manifestPackage = eResult;
        }
    }
}
