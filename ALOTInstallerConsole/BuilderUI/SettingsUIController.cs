using System;
using System.Collections.Generic;
using System.Text;
using ALOTInstallerCore.Startup;
using Terminal.Gui;

namespace ALOTInstallerConsole.BuilderUI
{
    public class SettingsUIController : UIController
    {
        public override void SetupUI()
        {
            Title = "Settings";

            Button close = new Button("Close")
            {
                X = Pos.Right(this) - 12,
                Y = Pos.Bottom(this) - 3,
                Height = 1,
                Clicked = Close_Clicked
            };
            Add(close);
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
