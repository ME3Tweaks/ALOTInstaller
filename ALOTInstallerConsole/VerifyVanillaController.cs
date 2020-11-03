using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ALOTInstallerConsole.UserControls;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.ModManager.Objects;
using ALOTInstallerCore.ModManager.Services;
using ALOTInstallerCore.Objects;
using ME3ExplorerCore.Packages;
using Terminal.Gui;

namespace ALOTInstallerConsole
{
    public static class VerifyVanillaController
    {
        public static void VerifyVanilla(MEGame game)
        {
            var target = Locations.GetTarget(game);
            if (target == null) return; //Can't do anything.

            verifyVanillaMM(target);

            // MEM CODE
        }

        private static void verifyVanillaMM(GameTarget target)
        {
            ProgressDialog pd = new ProgressDialog("Verifying game", "Please wait while your game is verified", "Preparing to verify game", true);


            List<string> nonVanillaFiles = new List<string>();
            NamedBackgroundWorker nbw = new NamedBackgroundWorker("VerifyVanillaWorker");
            nbw.DoWork += (a, b) => // MM CODE
             VanillaDatabaseService.ValidateTargetAgainstVanilla(target, f =>
                 {
                     nonVanillaFiles.Add(f);
                 },
                 su =>
                 {
                     Application.MainLoop.Invoke(() =>
                     {
                         pd.BottomMessage = su;
                     });
                 },
                 (done, total) =>
                 {
                     Application.MainLoop.Invoke(() =>
                     {
                         pd.ProgressMax = total;
                         pd.ProgressValue = done;
                     });
                 }
                 , true);
            nbw.RunWorkerCompleted += (a, b) =>
            {
                if (pd.IsCurrentTop)
                {
                    Application.RequestStop();
                }

                if (nonVanillaFiles.Any())
                {
                    ScrollDialog.Prompt("Game has modifications", "The following files appear to have been modified:", "There may be additional files also added to the game that this tool does not check for.", nonVanillaFiles, Colors.Dialog, "OK");
                }
                else
                {
                    MessageBox.Query("Game appears vanilla", "This installation of the game does not appear to have any modified files.", "OK");
                }
            };
            nbw.RunWorkerAsync();
            Application.Run(pd);
        }
    }
}
