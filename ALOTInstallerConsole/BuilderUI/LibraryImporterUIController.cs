using System;
using System.Collections.Generic;
using System.Text;
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
            throw new NotImplementedException();
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
