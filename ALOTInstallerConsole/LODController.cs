using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.ModManager.Objects;
using NStack;
using Octokit;
using Terminal.Gui;

namespace ALOTInstallerConsole
{
    /// <summary>
    /// Controls LOD switching
    /// </summary>
    public static class LODController
    {
        public static void PromptForLODs()
        {
            List<string> paths = new List<string>();
            if (Locations.ME1Target != null) paths.Add("ME1");
            if (Locations.ME2Target != null) paths.Add("ME2");
            if (Locations.ME3Target != null) paths.Add("ME3");
            paths.Add("Abort");
            var selectedIndex = MessageBox.Query("Select game", "Select which game to set LODs for.", paths.Select(x => (ustring)x.ToString()).ToArray());
            if (paths[selectedIndex] == "Abort" || selectedIndex < 0) return;

            GameTarget target = null;
            if (paths[selectedIndex] == "ME1") target = Locations.ME1Target;
            if (paths[selectedIndex] == "ME2") target = Locations.ME2Target;
            if (paths[selectedIndex] == "ME3") target = Locations.ME3Target;
            if (target != null)
            {
                PromptForLODs(target);
            }
        }

        private static void PromptForLODs(GameTarget target)
        {
            var availableLODOptions = LODHelper.GetAvailableLODs(target);
            List<string> options = availableLODOptions.Select(x => x.Item1).ToList();
            options.Add("Don't change LODs");
            int result = MessageBox.Query("Texture LOD Selector", "Select your texture level of detail (LOD). Higher LODs use more memory but will let higher quality assets load.", options.Select(x => (ustring)x).ToArray());
            if (result != options.Count - 1)
            {
                // Did not pick abort
                var lodOption = availableLODOptions[result];
                NamedBackgroundWorker nbw = new NamedBackgroundWorker("LODSetWorker");
                nbw.DoWork += (a, b) => { b.Result = MEMIPCHandler.SetLODs(target.Game, lodOption.Item2); };
                nbw.RunWorkerCompleted += (a, b) =>
                {
                    var res = (bool)b.Result;
                    if (res)
                    {
                        MessageBox.Query("LODs set", $"Texture settings have been set to {availableLODOptions[result].Item1}.", "OK");
                    }
                    else
                    {
                        MessageBox.ErrorQuery("Error setting LODs", "An error occurred settings the LODs. See the program log for more information.", "OK");
                    }
                };
                nbw.RunWorkerAsync();
            }
        }
    }
}
