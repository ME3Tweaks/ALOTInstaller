using System;
using System.Collections.Generic;
using System.Text;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.ModManager.ME3Tweaks;
using ALOTInstallerCore.ModManager.Services;
using ALOTInstallerCore.Objects;
using Terminal.Gui;

namespace ALOTInstallerConsole.BuilderUI
{
    public class BackupRestoreUIController : UIController
    {
        private TextField me1BackupField;
        private Button me1UnlinkButton;
        private Label me1BackupStatus;
        private Button me1RestoreButton;

        public override void SetupUI()
        {
            Title = "Backup & Restore";
            BackupService.RefreshBackupStatus(Locations.GetAllAvailableTargets(), false);
            buildUI();
        }

        private void buildUI()
        {
            RemoveAll();
            int y = 1;

            foreach (var e in Enums.AllGames)
            {
                var gameBackupPath = BackupService.GetGameBackupPath(e, false, false, false);
                Add(new Label($"{e.ToGameName()}")
                {
                    X = 1,
                    Y = y++,
                    Height = 1,
                });
                me1BackupStatus = new Label(BackupService.GetBackupStatus(e))
                {
                    X = 1,
                    Y = y++,
                    Height = 1
                };
                Add(me1BackupStatus);
                me1BackupField = new TextField()
                {
                    ReadOnly = true,
                    Width = 80,
                    Height = 1,
                    X = 1,
                    Y = y,
                    Text = gameBackupPath ?? "No backup or backup unavailable"
                };
                Add(me1BackupField);
                y++;

                me1UnlinkButton = new Button("Unlink")
                {
                    Height = 1,
                    X = 1,
                    Y = y,
                };
                Add(me1UnlinkButton);

                me1RestoreButton = new Button("Restore")
                {
                    Height = 1,
                    X = 70,
                    Y = y,
                };
                Add(me1RestoreButton);
                y++;
                y++;
            }


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
