using System;
using System.Collections.Generic;
using System.Text;
using Terminal.Gui;

namespace ALOTInstallerConsole.InstallerUI
{
    public class InstallerUIController
    {
        public Window win;
        public void SetupUI()
        {
            win = new Window();
            // Dynamically computed
            var overallLabel = new Label("Installing ALOT 11.2 for Mass Effect 3")
            {
                X = 1,
                Y = Pos.Center() - 2,
                Width = Dim.Fill(),
                Height = 1,
                TextAlignment = TextAlignment.Centered
            }; 
            var overallProgress = new ProgressBar()
            {
                X = Pos.Center(),
                Y = Pos.Center() - 1,
                Width = 50,
                Height = 1
            };

            // Dynamically computed
            var currentTaskLabel = new Label("Installing Textures")
            {
                X = Pos.Center(),
                Y = Pos.Center() + 1,
                Width = Dim.Fill(),
                Height = 1,
                TextAlignment = TextAlignment.Centered
            };
            var currentProgress = new ProgressBar()
            {
                X = Pos.Center(),
                Y = Pos.Center() + 2,
                Width = 50,
                Height = 1,
            };
            win.Add(overallLabel);
            win.Add(overallProgress);
            win.Add(currentTaskLabel);
            win.Add(currentProgress);
        }
    }
}
