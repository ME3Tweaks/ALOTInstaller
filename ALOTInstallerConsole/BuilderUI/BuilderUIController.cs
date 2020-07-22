using System;
using System.Collections.Generic;
using System.Text;
using ALOTInstallerCore.Objects;
using Terminal.Gui;

namespace ALOTInstallerConsole.BuilderUI
{
    class BuilderUIController : UIController
    {
        private Label currentStatusLabel;
        private InstallOptionsPackage installOptions;
        public void SetOptionsPackage(InstallOptionsPackage package)
        {
            installOptions = package;
        }
        public override void BeginFlow()
        {
        }

        public override void SetupUI()
        {
            Label l = new Label("Building texture installation package")
            {
                Y = Pos.Center() - 2,
                X = Pos.Center(),
                Height = 1
            };
            currentStatusLabel = new Label("Extracting files")
            {
                Y = Pos.Center(),
                X = Pos.Center(),
                Height = 1
            };

            Add(l);
            Add(currentStatusLabel);
        }
    }
}
