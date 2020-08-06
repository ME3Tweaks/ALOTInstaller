﻿using System;
using System.Collections.Generic;
using System.Text;
using ALOTInstallerConsole.BuilderUI;
using Terminal.Gui;

namespace ALOTInstallerConsole.InstallerUI
{
    public class PostInstallUIController : UIController
    {
        private string installedString;
        public void setInstalledString(string str)
        {
            installedString = str;
        }
        public override void SetupUI()
        {
            var yPos = Pos.Center() - 2;
            Add(new Label($"Installed {installedString}")
            {
                X = 0,
                Y = yPos,
                Width = Dim.Fill(),
                Height = 2,
                TextAlignment = TextAlignment.Centered,
            });

            Add(new Label("Installing any mods from now on will cause texture references to become invalid and may break your game.\nDo not install further mods without a game restore.")
            {
                X = 0,
                Y = yPos + 2,
                Width = Dim.Fill(),
                Height = 2,
                TextAlignment = TextAlignment.Centered,
            });

            Button continueButton = new Button("Continue")
            {
                X = 0,
                Y = yPos + 6,
                Width = 12,
                Height = 1,
                TextAlignment = TextAlignment.Centered,
                Clicked = continueClicked
            };
            Add(continueButton);

        }

        private void continueClicked()
        {
            // Return to the primary menu
            FileSelectionUIController bui = new FileSelectionUIController();
            bui.SetupUI();
            Program.SwapToNewView(bui);
        }

        public override void BeginFlow()
        {

        }
    }
}